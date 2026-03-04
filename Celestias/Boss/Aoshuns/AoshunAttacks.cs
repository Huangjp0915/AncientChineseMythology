using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    /// <summary>
    /// 敖顺攻击辅助 - 雷电/风暴主题攻击生成逻辑
    /// </summary>
    public static class AoshunAttacks
    {
        /// <summary>
        /// 雷柱雨 - 雷柱从玩家上方随机位置降落
        /// </summary>
        public static void LightningRain(NPC npc) {
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
                ModContent.ProjectileType<AoshunLightningBolt>(),
                npc.damage / 4,
                0f
            );
        }

        /// <summary>
        /// 雷球散射 - 从Boss位置向外发射扇形雷球
        /// </summary>
        public static void ThunderBurst(NPC npc, int count, float spreadDegrees = 70f) {
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
                    ModContent.ProjectileType<AoshunThunderball>(),
                    npc.damage / 4,
                    3f
                );
            }
        }

        /// <summary>
        /// 螺旋雷球 - 从Boss位置发射螺旋排列的雷球
        /// </summary>
        public static void SpiralThunder(NPC npc, float baseAngle, int arms = 3, float speed = 8f) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            for (int arm = 0; arm < arms; arm++) {
                float angle = baseAngle + MathHelper.TwoPi * arm / arms;
                Vector2 vel = angle.ToRotationVector2() * speed;
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    vel,
                    ModContent.ProjectileType<AoshunThunderball>(),
                    npc.damage / 4,
                    2f
                );
            }
        }

        /// <summary>
        /// 追踪雷球连射 - 发射多发高追踪雷球
        /// </summary>
        public static void HomingThunderBurst(NPC npc, int count = 5) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Player player = Main.player[npc.target];
            Vector2 toPlayer = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);

            for (int i = 0; i < count; i++) {
                Vector2 vel = toPlayer.RotatedByRandom(MathHelper.ToRadians(30)) * (6f + Main.rand.NextFloat(4f));
                int p = Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center + toPlayer * 40f,
                    vel,
                    ModContent.ProjectileType<AoshunThunderball>(),
                    npc.damage / 4,
                    1f
                );
                // 延长追踪时间
                Main.projectile[p].timeLeft = 400;
            }
        }

        /// <summary>
        /// 雷电环 - 从Boss位置向全方位发射雷球环
        /// </summary>
        public static void ThunderRing(NPC npc, int count = 16, float speed = 6f) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 vel = angle.ToRotationVector2() * speed;
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    vel,
                    ModContent.ProjectileType<AoshunThunderball>(),
                    npc.damage / 5,
                    1f
                );
            }
        }

        /// <summary>
        /// 龙息雷锥 - 朝前方密集发射雷柱
        /// </summary>
        public static void BreathLightning(NPC npc, int count = 3) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 dir = npc.velocity.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedByRandom(MathHelper.ToRadians(15)) * (12f + Main.rand.NextFloat(6f));
                int p = Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center + dir * 50f,
                    vel,
                    ModContent.ProjectileType<AoshunLightningBolt>(),
                    npc.damage / 3,
                    2f
                );
                Main.projectile[p].tileCollide = false;
                Main.projectile[p].timeLeft = 120;
            }
        }

        /// <summary>
        /// 柱状激光大招 - 释放雷束
        /// </summary>
        public static void ThunderBeam(NPC npc) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Player player = Main.player[npc.target];
            float angle = (player.Center - npc.Center).ToRotation();
            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                npc.Center,
                Vector2.Zero,
                ModContent.ProjectileType<AoshunThunderBeam>(),
                npc.damage / 3,
                0f,
                ai1: angle
            );
        }

        /// <summary>
        /// 密集雷柱雨升级版 - 加速连续降落
        /// </summary>
        public static void LightningStorm(NPC npc, int count = 3) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            for (int i = 0; i < count; i++) {
                LightningRain(npc);
            }
        }
    }
}
