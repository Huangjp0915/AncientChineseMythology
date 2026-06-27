using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// 敖闰攻击辅助 - 冰霜/寒水主题攻击生成逻辑
    /// </summary>
    public static class AoyuanAttacks
    {
        /// <summary>
        /// 冰柱雨 - 冰柱从玩家上方随机位置降落
        /// </summary>
        public static void IcicleRain(NPC npc) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Player player = Main.player[npc.target];
            float speed = 10f;

            Vector2 spawnPos = new Vector2(
                player.Center.X + Main.rand.Next(-700, 700),
                player.MountedCenter.Y - 600f - 100 * Main.rand.Next(3)
            );

            float dirY = player.Center.Y - spawnPos.Y;
            if (dirY < 20f) dirY = 20f;

            float length = MathF.Sqrt(dirY * dirY);
            float normalizedY = speed / length * dirY + Main.rand.Next(41) * 0.02f;

            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                spawnPos,
                new Vector2(0, normalizedY * 1.5f),
                ModContent.ProjectileType<AoyuanIcicle>(),
                npc.damage / 4,
                0f
            );
        }

        /// <summary>
        /// 冰晶散射 - 从Boss位置向外发射扇形冰弹
        /// </summary>
        public static void IceBurst(NPC npc, int count, float spreadDegrees = 70f) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            float spread = spreadDegrees * 0.0174f;
            float baseSpeed = npc.velocity.Length();
            if (baseSpeed < 2f) baseSpeed = 2f;

            double startAngle = Math.Atan2(npc.velocity.X, npc.velocity.Y) - spread / 2;
            double deltaAngle = spread / Math.Max(count - 1, 1);

            for (int i = 0; i < count; i++) {
                double offsetAngle = startAngle + deltaAngle * i;
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    new Vector2(
                        baseSpeed * (float)Math.Sin(offsetAngle) * 2,
                        baseSpeed * (float)Math.Cos(offsetAngle) * 2
                    ),
                    ModContent.ProjectileType<AoyuanIceball>(),
                    npc.damage / 4,
                    3f
                );
            }
        }

        /// <summary>
        /// 螺旋冰弹 - 从Boss位置发射螺旋排列的冰弹
        /// </summary>
        public static void SpiralIce(NPC npc, float baseAngle, int arms = 3, float speed = 8f) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            for (int arm = 0; arm < arms; arm++) {
                float angle = baseAngle + MathHelper.TwoPi * arm / arms;
                Vector2 vel = angle.ToRotationVector2() * speed;
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    vel,
                    ModContent.ProjectileType<AoyuanIceball>(),
                    npc.damage / 4,
                    2f
                );
            }
        }

        /// <summary>
        /// 追踪冰弹连射 - 发射多发高追踪冰弹
        /// </summary>
        public static void HomingBurst(NPC npc, int count = 5) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Player player = Main.player[npc.target];
            Vector2 toPlayer = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);

            for (int i = 0; i < count; i++) {
                Vector2 vel = toPlayer.RotatedByRandom(MathHelper.ToRadians(30)) * (6f + Main.rand.NextFloat(4f));
                int p = Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center + toPlayer * 40f,
                    vel,
                    ModContent.ProjectileType<AoyuanIceball>(),
                    npc.damage / 4,
                    1f
                );
                // 延长追踪时间
                Main.projectile[p].timeLeft = 400;
            }
        }

        /// <summary>
        /// 冰霜环 - 从Boss位置向全方位发射冰弹环
        /// </summary>
        public static void FrostRing(NPC npc, int count = 16, float speed = 6f) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 vel = angle.ToRotationVector2() * speed;
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    vel,
                    ModContent.ProjectileType<AoyuanIceball>(),
                    npc.damage / 5,
                    1f
                );
            }
        }

        /// <summary>
        /// 龙息冰锥 - 朝前方密集发射冰柱
        /// </summary>
        public static void BreathIcicles(NPC npc, int count = 3) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 dir = npc.velocity.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedByRandom(MathHelper.ToRadians(15)) * (12f + Main.rand.NextFloat(6f));
                int p = Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center + dir * 50f,
                    vel,
                    ModContent.ProjectileType<AoyuanIcicle>(),
                    npc.damage / 3,
                    2f
                );
                Main.projectile[p].tileCollide = false;
                Main.projectile[p].timeLeft = 120;
            }
        }

        /// <summary>
        /// 柱状激光大招 - 释放冰束
        /// </summary>
        public static void FrostBeam(NPC npc) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Player player = Main.player[npc.target];
            float angle = (player.Center - npc.Center).ToRotation();
            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                npc.Center,
                Vector2.Zero,
                ModContent.ProjectileType<AoyuanFrostBeam>(),
                npc.damage / 3,
                0f,
                ai1: angle
            );
        }

        /// <summary>
        /// 密集冰柱雨升级版 - 加速连续降落
        /// </summary>
        public static void IcicleStorm(NPC npc, int count = 3) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            for (int i = 0; i < count; i++) {
                IcicleRain(npc);
            }
        }

        /// <summary>
        /// 寒霜吐息（锥形）- 朝指定目标方向密集发射冰锥
        /// </summary>
        public static void BreathConeAt(NPC npc, Vector2 targetCenter, int count = 3) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 dir = (targetCenter - npc.Center).SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedByRandom(MathHelper.ToRadians(18)) * (11f + Main.rand.NextFloat(6f));
                int p = Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center + dir * 50f,
                    vel,
                    ModContent.ProjectileType<AoyuanIcicle>(),
                    npc.damage / 3,
                    2f);
                Main.projectile[p].tileCollide = false;
                Main.projectile[p].timeLeft = 120;
            }
        }

        /// <summary>
        /// 冰晶棋局 - 在玩家周围铺 3x3 预告冰柱落点，仅部分真正落下
        /// 每个格子生成一个预告弹幕（ai0=1 为真柱，会落冰；ai0=0 为虚招）
        /// </summary>
        public static void SpawnPillarChess(NPC npc, Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            const float spacingX = 150f;
            const float spacingY = 130f;
            Vector2 gridCenter = new Vector2(player.Center.X, player.Center.Y);

            // 随机决定哪些格子为真柱（9 格中选 4~5 个）
            bool[] real = new bool[9];
            int realCount = Main.expertMode ? 5 : 4;
            int placed = 0;
            int guard = 0;
            while (placed < realCount && guard < 100) {
                int idx = Main.rand.Next(9);
                if (!real[idx]) { real[idx] = true; placed++; }
                guard++;
            }

            int damage = Main.expertMode ? npc.damage / 4 : npc.damage / 3;
            for (int gy = -1; gy <= 1; gy++) {
                for (int gx = -1; gx <= 1; gx++) {
                    int idx = (gy + 1) * 3 + (gx + 1);
                    Vector2 cell = gridCenter + new Vector2(gx * spacingX, gy * spacingY);
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        cell,
                        Vector2.Zero,
                        ModContent.ProjectileType<AoyuanPillarTelegraph>(),
                        damage, 0f, Main.myPlayer,
                        ai0: real[idx] ? 1f : 0f);
                }
            }
        }

        /// <summary>
        /// 暴雪帷幕 - 从玩家一侧推进的雪墙，墙上留一道移动缺口
        /// </summary>
        public static void SpawnBlizzardVeil(NPC npc, Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int dir = player.Center.X >= npc.Center.X ? 1 : -1;
            // 反过来从玩家更空旷一侧推来，确保有反应空间
            dir = Main.rand.NextBool() ? 1 : -1;
            Vector2 spawn = new Vector2(player.Center.X - dir * 1100f, player.Center.Y);
            int damage = Main.expertMode ? npc.damage / 4 : npc.damage / 3;

            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                spawn,
                new Vector2(dir * (Main.expertMode ? 7f : 5.5f), 0f),
                ModContent.ProjectileType<AoyuanBlizzardWall>(),
                damage, 2f, Main.myPlayer,
                ai0: dir,
                ai1: Main.rand.NextFloat(-200f, 200f));
        }

        /// <summary>
        /// 绝对零度放射冻结波（broken=true 时为削弱版，仅减速不冻结）
        /// </summary>
        public static void SpawnAbsoluteZeroBurst(NPC npc, bool broken) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = Main.expertMode ? npc.damage / 3 : npc.damage / 2;
            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                npc.Center,
                Vector2.Zero,
                ModContent.ProjectileType<AoyuanAbsoluteZeroBurst>(),
                damage, 0f, Main.myPlayer,
                ai0: broken ? 1f : 0f);
        }
    }
}