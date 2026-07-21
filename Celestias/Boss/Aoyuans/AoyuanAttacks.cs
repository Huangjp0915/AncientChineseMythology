using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// 敖闰攻击生成器 - 全部服务器权威（MultiplayerClient 直接 return）
    /// </summary>
    public static class AoyuanAttacks
    {
        /// <summary>头部基准伤害（不受伤害窗口逐帧调制影响）</summary>
        private static int BossDamage(NPC npc) => npc.ModNPC is Aoyuan a ? a.ContactDamageBase : npc.damage;

        #region 清弹（换阶段/死亡演出）

        /// <summary>清空全部敖闰敌对弹幕 — 换阶段/死亡的公平阀门</summary>
        public static void ClearHostileProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int[] types = [
                ModContent.ProjectileType<AoyuanIceball>(),
                ModContent.ProjectileType<AoyuanIcicle>(),
                ModContent.ProjectileType<AoyuanFrostVortex>(),
                ModContent.ProjectileType<AoyuanFrostBeam>(),
                ModContent.ProjectileType<AoyuanPermafrostTrail>(),
                ModContent.ProjectileType<AoyuanPillarTelegraph>(),
                ModContent.ProjectileType<AoyuanBlizzardWall>(),
                ModContent.ProjectileType<AoyuanAbsoluteZeroBurst>(),
                ModContent.ProjectileType<AoyuanIceMirror>(),
                ModContent.ProjectileType<AoyuanColdField>(),
                ModContent.ProjectileType<AoyuanFrostRidge>(),
                ModContent.ProjectileType<AoyuanIceSpike>(),
                ModContent.ProjectileType<AoyuanFreezeTrap>(),
            ];

            foreach (Projectile p in Main.ActiveProjectiles) {
                if (Array.IndexOf(types, p.type) >= 0)
                    p.Kill();
            }
        }

        #endregion

        #region 冰镜

        /// <summary>入场演出冰镜（只生长后碎裂, 无攻击）</summary>
        public static void SpawnIntroMirror(NPC npc) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<AoyuanIceMirror>(), 0, 0f, Main.myPlayer,
                ai0: 2f);
        }

        /// <summary>
        /// 冰镜·折光阵: 玩家周围弧线布 count 面镜, 依序(间隔14f)自动射折光束
        /// </summary>
        public static void SpawnMirrorArc(NPC npc, Player player, int count) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = BossDamage(npc) / 3;
            float baseAng = (npc.Center - player.Center).ToRotation() + Main.rand.NextFloat(-0.4f, 0.4f);
            for (int i = 0; i < count; i++) {
                float ang = baseAng + (i - (count - 1) * 0.5f) * 0.58f;
                Vector2 pos = player.Center + ang.ToRotationVector2() * 520f;
                // ai[1] 在折光阵模式下携带发射时刻（成形22 + 蓄光45 + 波纹错拍）
                float fireTick = 22 + 45 + i * 14;
                Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<AoyuanIceMirror>(), damage, 0f, Main.myPlayer,
                    ai0: 0f, ai1: fireTick);
            }
        }

        /// <summary>镜界·瞬狱: 玩家周围六角形布 6 面受指挥的镜</summary>
        public static void SpawnMirrorHex(NPC npc, Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = BossDamage(npc) / 3;
            float baseAng = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < 6; i++) {
                float ang = baseAng + MathHelper.TwoPi * i / 6f;
                Vector2 pos = player.Center + ang.ToRotationVector2() * 600f;
                Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<AoyuanIceMirror>(), damage, 0f, Main.myPlayer,
                    ai0: 1f);
            }
        }

        /// <summary>由镜面射出折光冰束（束自带 26f 预警线）</summary>
        public static void SpawnMirrorLance(Projectile mirror, float angle) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Projectile.NewProjectile(mirror.GetSource_FromAI(), mirror.Center, Vector2.Zero,
                ModContent.ProjectileType<AoyuanFrostBeam>(), mirror.damage, 0f, Main.myPlayer,
                ai1: angle);
        }

        /// <summary>枚举存活的镜界镜面</summary>
        private static System.Collections.Generic.IEnumerable<Projectile> RealmMirrors() {
            int type = ModContent.ProjectileType<AoyuanIceMirror>();
            //不用 Main.ActiveProjectiles: 其枚举器为 ref-struct 语义, 不能跨 yield 边界保留 (CS4007)
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && p.ai[0] == 1f)
                    yield return p;
            }
        }

        /// <summary>最近的镜界镜面（exclude 排除指定 whoAmI, 传 -1 不排除）</summary>
        public static Projectile FindNearestRealmMirror(Vector2 from, int exclude) {
            Projectile best = null;
            float bestDist = float.MaxValue;
            foreach (Projectile p in RealmMirrors()) {
                if (p.whoAmI == exclude) continue;
                float d = Vector2.DistanceSquared(from, p.Center);
                if (d < bestDist) { bestDist = d; best = p; }
            }
            return best;
        }

        /// <summary>最远的镜界镜面（出口选择, 确定性）</summary>
        public static Projectile FindFarthestRealmMirror(Vector2 from) {
            Projectile best = null;
            float bestDist = -1f;
            foreach (Projectile p in RealmMirrors()) {
                float d = Vector2.DistanceSquared(from, p.Center);
                if (d > bestDist) { bestDist = d; best = p; }
            }
            return best;
        }

        /// <summary>当前处于"出口白亮"状态的镜面</summary>
        public static Projectile FindWhitenedRealmMirror() {
            foreach (Projectile p in RealmMirrors()) {
                if (p.ai[2] == 1f)
                    return p;
            }
            return null;
        }

        /// <summary>镜界终幕: 剩余镜面齐充能锁角, 45f 后齐射折光束</summary>
        public static void CommandRealmVolley(Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            foreach (Projectile p in RealmMirrors()) {
                p.ai[1] = (player.Center - p.Center).ToRotation();
                p.ai[2] = 2f;
                p.netUpdate = true;
            }
        }

        #endregion

        #region 寒潮冻土 / 困龙局

        /// <summary>寒潮·冻土席卷: 触地点展开蔓延霜面（内部派生冰脊波与尖刺）</summary>
        public static void SpawnColdField(NPC npc, bool phase2) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + new Vector2(0f, 30f), Vector2.Zero,
                ModContent.ProjectileType<AoyuanColdField>(), BossDamage(npc) / 4, 1f, Main.myPlayer,
                ai0: phase2 ? 1f : 0f);
        }

        /// <summary>
        /// 冰封·困龙局: 玩家脚下 + 两翼布置倒计时冻结区（P2 四区且错拍引爆）
        /// 纯控制不伤血 → 冰蓝预警（诚实倒计时环）
        /// </summary>
        public static void SpawnFreezeTraps(NPC npc, Player player, bool phase2) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2[] offsets = phase2
                ? [Vector2.Zero, new Vector2(420f, 0f), new Vector2(-420f, 0f), new Vector2(0f, -380f)]
                : [Vector2.Zero, new Vector2(420f, 0f), new Vector2(-420f, 0f)];

            for (int i = 0; i < offsets.Length; i++) {
                int fuse = 90 + (phase2 ? i * 20 : 0); // P2 错拍引爆 = 节拍舞步
                Projectile.NewProjectile(npc.GetSource_FromAI(), player.Center + offsets[i], Vector2.Zero,
                    ModContent.ProjectileType<AoyuanFreezeTrap>(), 0, 0f, Main.myPlayer,
                    ai0: fuse);
            }
        }

        /// <summary>困龙局放牧压制: 一发微追踪冰晶飞棱</summary>
        public static void SuppressShot(NPC npc, Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Vector2 vel = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY) * 7.5f;
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                ModContent.ProjectileType<AoyuanIceball>(), BossDamage(npc) / 4, 1f, Main.myPlayer);
        }

        #endregion

        #region 绝对零度

        /// <summary>
        /// 绝对零度放射环（broken=true 为削弱寒潮环: 仅叠冰冻不伤血）
        /// </summary>
        public static void SpawnAbsoluteZeroBurst(NPC npc, bool broken) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<AoyuanAbsoluteZeroBurst>(), broken ? 0 : BossDamage(npc) / 3, 0f, Main.myPlayer,
                ai0: broken ? 1f : 0f);
        }

        /// <summary>绝对零度余波: 10f 后的慢速寒潮环（叠层, 无伤害）</summary>
        public static void SpawnAbsoluteZeroEcho(NPC npc) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<AoyuanAbsoluteZeroBurst>(), 0, 0f, Main.myPlayer,
                ai0: 1f, ai1: 1f);
        }

        #endregion

        #region 死亡演出

        /// <summary>死亡碎裂连锁: 击碎最靠尾部的一段存活身体（HitEffect 冰爆）</summary>
        public static void ShatterOneBodySegment(NPC head) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int bodyType = ModContent.NPCType<AoyuanBody>();
            NPC tail = null;
            int maxIdx = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.type != bodyType) continue;
                if ((int)n.ai[3] != head.whoAmI) continue;
                if ((int)n.ai[0] > maxIdx) {
                    maxIdx = (int)n.ai[0];
                    tail = n;
                }
            }
            if (tail != null) {
                tail.dontTakeDamage = false;
                tail.StrikeInstantKill();
            }
        }

        #endregion
    }
}
