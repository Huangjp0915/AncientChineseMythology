using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    /// <summary>
    /// 敖顺 V3 攻击生成器 — 全部服务器端生成, 带数量上限与公平阀门（设计文档 §4/§7）。
    ///
    /// 招式与生成器对应:
    ///  GaleCleave    → SpawnGaleBlades      臂段错相风刃(距离过滤+首波降速)
    ///  CyclonePalm   → SpawnCyclone         程序化龙卷(同屏≤2, 不追踪)
    ///  ThunderSeal   → SpawnSealFan         沿玩家动向扇形铺印
    ///  AbyssBreach   → SpawnBreachCrack / ShootBreachScales / SpawnShockwave
    ///  StormNet      → SpawnStormNet        环形电网(迁移安全缺口)
    ///  HeavensCall   → SpawnSkyBolt         天雷柱(细红线预告)
    ///  TempestPierce → SpawnElectricTrail   电痕(全局≤24)
    ///  P3 眼系       → SpawnStormEyeArena   常驻竞技场风暴之眼
    /// </summary>
    public static class AoshunAttacks
    {
        #region 通用

        /// <summary>敖顺全部敌方弹幕类型（清弹与上限统计用）</summary>
        private static int[] HostileTypes => [
            ModContent.ProjectileType<AoshunWindBlade>(),
            ModContent.ProjectileType<AoshunDragonScale>(),
            ModContent.ProjectileType<AoshunTornado>(),
            ModContent.ProjectileType<AoshunThunderSeal>(),
            ModContent.ProjectileType<AoshunSkyBolt>(),
            ModContent.ProjectileType<AoshunLightningNode>(),
            ModContent.ProjectileType<AoshunShockwave>(),
            ModContent.ProjectileType<AoshunElectricTrail>(),
            ModContent.ProjectileType<AoshunBreachCrack>(),
            ModContent.ProjectileType<AoshunStormEye>(),
        ];

        /// <summary>换阶段/死亡清弹（公平阀门: 演出期间场上无残留威胁）</summary>
        public static void ClearHostileProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            int[] types = HostileTypes;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (Array.IndexOf(types, p.type) >= 0)
                    p.Kill();
            }
        }

        /// <summary>统计某类型当前活跃数量（生成上限判定）</summary>
        public static int CountActive(int type) {
            int n = 0;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type == type)
                    n++;
            }
            return n;
        }

        /// <summary>自 fromY 向下搜索地面(60格), 找不到则返回 fromY+700</summary>
        public static float FindGroundY(float worldX, float fromY) {
            int tileX = (int)(worldX / 16f);
            int startY = (int)(fromY / 16f);
            for (int tileY = startY; tileY < startY + 60; tileY++) {
                if (tileX >= 0 && tileX < Main.maxTilesX && tileY >= 1 && tileY < Main.maxTilesY &&
                    WorldGen.SolidTile(tileX, tileY)) {
                    return tileY * 16f;
                }
            }
            return fromY + 700f;
        }

        #endregion

        #region 风刃连斩

        /// <summary>
        /// 从臂段(parity 奇偶批)向玩家预测位置放出风刃。
        /// 阀门: 仅 ≥260px 的臂段发射、每批 ≤4、speedScale 首波降速。
        /// </summary>
        public static void SpawnGaleBlades(NPC head, Player target, int parity, float speedScale) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = Main.expertMode ? head.damage / 5 : head.damage / 4;
            int armType = ModContent.NPCType<AoshunArms>();
            float speed = 11.5f * speedScale;
            Vector2 predicted = target.Center + target.velocity * 14f;

            int fired = 0;
            foreach (NPC seg in Main.ActiveNPCs) {
                if (seg.realLife != head.whoAmI || seg.type != armType)
                    continue;
                if (((int)seg.ai[2] & 1) != parity)
                    continue;
                if (Vector2.Distance(seg.Center, target.Center) < 260f)
                    continue; // 最小发射距离: 防贴脸秒杀

                Vector2 dir = (predicted - seg.Center).SafeNormalize(Vector2.UnitX)
                    .RotatedByRandom(0.12f);
                // ai0 = 弧线弯曲方向, ai1 = 起步降速比
                Projectile.NewProjectile(head.GetSource_FromAI(), seg.Center, dir * speed,
                    ModContent.ProjectileType<AoshunWindBlade>(), damage, 1f, Main.myPlayer,
                    ai0: Main.rand.NextBool() ? 1f : -1f, ai1: speedScale);

                if (++fired >= 4)
                    break;
            }
        }

        #endregion

        #region 龙卷

        /// <summary>
        /// 生成程序化龙卷。mode 0=定点扎地(不追踪), 1=沿风暴眼壁巡游(ai1=角速度方向)。
        /// 阀门: 同屏 ≤2 只。
        /// </summary>
        public static void SpawnCyclone(NPC head, Vector2 basePos, int mode, float wallDir = 1f) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (CountActive(ModContent.ProjectileType<AoshunTornado>()) >= 2)
                return;

            int damage = Main.expertMode ? head.damage / 5 : head.damage / 4;
            Projectile.NewProjectile(head.GetSource_FromAI(), basePos, Vector2.Zero,
                ModContent.ProjectileType<AoshunTornado>(), damage, 2f, Main.myPlayer,
                ai0: mode, ai1: wallDir, ai2: head.whoAmI);
        }

        #endregion

        #region 天雷印扇

        /// <summary>
        /// 沿玩家运动方向扇形铺设雷印, 依次延迟引爆（涟漪式）。
        /// 阀门: 铺设一次成型不追踪、印间距 ≥170px。
        /// </summary>
        public static void SpawnSealFan(NPC head, Player target, int count) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = Main.expertMode ? head.damage / 3 : (int)(head.damage / 2.5f);
            Vector2 dir = target.velocity.LengthSquared() > 4f
                ? target.velocity.SafeNormalize(Vector2.UnitX)
                : new Vector2(target.direction, 0f);

            for (int i = 0; i < count; i++) {
                Vector2 perp = new(-dir.Y, dir.X);
                float side = (i % 2 == 0) ? 1f : -1f;
                Vector2 pos = target.Center + dir * (150f + i * 175f) + perp * side * 46f * (i * 0.5f);
                int delay = 55 + i * 7;

                Projectile.NewProjectile(head.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<AoshunThunderSeal>(), damage, 0f, Main.myPlayer,
                    ai0: delay);
            }
        }

        #endregion

        #region 破渊突袭

        /// <summary>地裂预警标记（无伤害, warnTime 帧后自灭）</summary>
        public static void SpawnBreachCrack(NPC head, Vector2 groundPos, int warnTime) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            Projectile.NewProjectile(head.GetSource_FromAI(), groundPos, Vector2.Zero,
                ModContent.ProjectileType<AoshunBreachCrack>(), 0, 0f, Main.myPlayer,
                ai0: warnTime);
        }

        /// <summary>破土瞬间自地表向上抛射带电龙鳞（重力回落）</summary>
        public static void ShootBreachScales(NPC head, Vector2 origin, int count) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = Main.expertMode ? head.damage / 5 : head.damage / 4;
            for (int i = 0; i < count; i++) {
                float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                float angle = -MathHelper.PiOver2 + MathHelper.Lerp(-1.05f, 1.05f, t) + Main.rand.NextFloat(-0.08f, 0.08f);
                float speed = Main.rand.NextFloat(9f, 14f);
                Projectile.NewProjectile(head.GetSource_FromAI(), origin,
                    angle.ToRotationVector2() * speed,
                    ModContent.ProjectileType<AoshunDragonScale>(), damage, 1f);
            }
        }

        /// <summary>环形冲击波（破渊/压掌/怒啸共用）</summary>
        public static void SpawnShockwave(NPC head, int count) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = Main.expertMode ? head.damage / 5 : head.damage / 4;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Projectile.NewProjectile(head.GetSource_FromAI(), head.Center,
                    angle.ToRotationVector2() * 8f,
                    ModContent.ProjectileType<AoshunShockwave>(), damage, 3f);
            }
        }

        #endregion

        #region 雷链电网

        /// <summary>
        /// 以 anchor 为心生成环形电网节点（锚定施放瞬间, 不追踪）。
        /// 缺口对每 90f 顺移一位, 永存安全缝。
        /// </summary>
        public static void SpawnStormNet(NPC head, Vector2 anchor, int nodeCount) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = Main.expertMode ? head.damage / 5 : head.damage / 4;
            int gapInit = Main.rand.Next(nodeCount);
            const float Radius = 430f;

            for (int i = 0; i < nodeCount; i++) {
                float angle = MathHelper.TwoPi * i / nodeCount - MathHelper.PiOver2;
                Vector2 pos = anchor + angle.ToRotationVector2() * Radius;
                Projectile.NewProjectile(head.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<AoshunLightningNode>(), damage, 0f, Main.myPlayer,
                    ai0: i, ai1: nodeCount, ai2: gapInit);
            }
        }

        #endregion

        #region 天雷柱

        /// <summary>
        /// 在指定落点生成天雷柱（delay 帧细红线预告 → 贯天雷击）。
        /// 调用方保证柱间距 ≥190px 与每轮数量上限。
        /// </summary>
        public static void SpawnSkyBolt(NPC head, Vector2 groundPos, int delay) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = Main.expertMode ? head.damage / 3 : (int)(head.damage / 2.5f);
            Projectile.NewProjectile(head.GetSource_FromAI(), groundPos, Vector2.Zero,
                ModContent.ProjectileType<AoshunSkyBolt>(), damage, 0f, Main.myPlayer,
                ai0: delay);
        }

        #endregion

        #region 电痕

        /// <summary>冲刺沿途电痕。阀门: 全局 ≤24。</summary>
        public static void SpawnElectricTrail(NPC head) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (CountActive(ModContent.ProjectileType<AoshunElectricTrail>()) >= 24)
                return;

            int damage = Main.expertMode ? head.damage / 6 : head.damage / 5;
            Projectile.NewProjectile(head.GetSource_FromAI(), head.Center, Vector2.Zero,
                ModContent.ProjectileType<AoshunElectricTrail>(), damage, 0f);
        }

        #endregion

        #region 风暴之眼（P3 常驻竞技场）

        /// <summary>生成常驻竞技场风暴之眼（Boss 死亡/离场时由眼自身消散）</summary>
        public static int SpawnStormEyeArena(NPC head, Vector2 center) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return -1;
            if (CountActive(ModContent.ProjectileType<AoshunStormEye>()) > 0)
                return -1;

            int damage = Main.expertMode ? head.damage / 5 : head.damage / 4;
            return Projectile.NewProjectile(head.GetSource_FromAI(), center, Vector2.Zero,
                ModContent.ProjectileType<AoshunStormEye>(), damage, 0f, Main.myPlayer,
                ai0: head.whoAmI);
        }

        #endregion
    }
}
