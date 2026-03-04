using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    internal partial class Aokin
    {
        #region 一阶段攻击

        /// <summary>
        /// 一阶段巡逻 - 环绕玩家移动
        /// </summary>
        private void RunPhase1Patrol(Player target) {
            float orbitSpeed = 0.02f;
            float orbitRadius = 400f;

            NPC.localAI[1] += orbitSpeed;
            if (NPC.localAI[1] > MathHelper.TwoPi)
                NPC.localAI[1] -= MathHelper.TwoPi;

            Vector2 targetPos = target.Center + new Vector2(
                MathF.Cos(NPC.localAI[1]) * orbitRadius,
                MathF.Sin(NPC.localAI[1]) * orbitRadius * 0.5f - 200f
            );

            targetPos.Y += MathF.Sin(globalTime * 2f) * 30f;

            Vector2 toTarget = targetPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 0.06f, 0.08f);

            // 火焰尾迹
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                AokinHelper.CreateFireTrail(NPC.Center - NPC.velocity * 2f, NPC.velocity, 0.8f);
            }

            if (PhaseTimer > 180) {
                TransitionTo(GetRandomPhase1Attack());
            }
        }

        /// <summary>
        /// 火弹齐射 - 向玩家扇形发射火弹
        /// </summary>
        private void RunPhase1FireBarrage(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime * 1.5f) * 100f, -350);
            Vector2 toHover = hoverPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.04f, 0.1f);

            int fireInterval = Main.expertMode ? 12 : 16;
            if (AttackTimer % fireInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                int bulletCount = Main.expertMode ? 5 : 3;
                float spreadAngle = MathHelper.ToRadians(12f);

                for (int i = -bulletCount / 2; i <= bulletCount / 2; i++) {
                    Vector2 vel = toPlayer.RotatedBy(i * spreadAngle) * 10f;
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center + toPlayer * 50f,
                        vel,
                        ModContent.ProjectileType<AokinFireball>(),
                        NPC.damage / 3,
                        1f
                    );
                }

                SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.2f, Volume = 0.8f }, NPC.Center);
            }

            // 火焰粒子
            if (!VaultUtils.isServer && AttackTimer % 5 == 0) {
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                for (int i = 0; i < 3; i++) {
                    Vector2 dustVel = toPlayer.RotatedByRandom(0.5f) * Main.rand.NextFloat(3, 6);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Torch, dustVel.X, dustVel.Y, 180, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (AttackTimer > 150) {
                TransitionTo(BossPhase.Phase1_Patrol);
            }
        }

        /// <summary>
        /// 龙息喷射 - 朝玩家方向持续喷射火焰
        /// </summary>
        private void RunPhase1DragonBreath(Player target) {
            NPC.rotation = (target.Center - NPC.Center).ToRotation();

            if (target.Distance(NPC.Center) > 600) {
                NPC.velocity += (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) / 3f;
                NPC.velocity *= 0.97f;
            }
            else {
                NPC.velocity += (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) / 30f;
                NPC.velocity *= 0.99f;
            }

            int fireInterval = Main.expertMode ? 6 : 8;
            if (AttackTimer % fireInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int damage = Main.expertMode ? 35 : 50;
                float speed = Main.expertMode ? 14f : 10f;

                Vector2 direction = NPC.rotation.ToRotationVector2();
                Vector2 vel = direction.RotatedByRandom(MathHelper.ToRadians(8)) * speed;

                int p = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center + direction * 50f,
                    vel,
                    ModContent.ProjectileType<AokinFireball>(),
                    damage,
                    1f
                );
                Main.projectile[p].timeLeft = 100;
            }

            // 龙息火焰粒子
            if (!VaultUtils.isServer) {
                Vector2 breathDir = NPC.rotation.ToRotationVector2();
                for (int i = 0; i < 4; i++) {
                    Vector2 dustPos = NPC.Center + breathDir * (30 + i * 10);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Torch, breathDir.X * 8, breathDir.Y * 8, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (AttackTimer > 180) {
                TransitionTo(BossPhase.Phase1_Patrol);
            }
        }

        /// <summary>
        /// 尾鞭横扫 - 原地旋转用身体段扫荡玩家
        /// </summary>
        private void RunPhase1TailWhip(Player target) {
            NPC.velocity *= 0.93f;

            // 快速旋转
            NPC.rotation += MathF.PI / 20f;
            tailTurnSpeed = 20f;

            // 旋转火焰特效
            if (!VaultUtils.isServer && AttackTimer % 3 == 0) {
                float angle = NPC.rotation + MathF.PI;
                Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * 60f;
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.SolarFlare, 0, 0, 100, default, 2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = angle.ToRotationVector2() * 3f;
            }

            if (AttackTimer > 120) {
                tailTurnSpeed = 12f;
                TransitionTo(BossPhase.Phase1_Patrol);
            }
        }

        /// <summary>
        /// 陨石雨 - 从空中召唤火球下落
        /// </summary>
        private void RunPhase1MeteorRain(Player target) {
            NPC.velocity *= 0.95f;
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            NPC.velocity += (hoverPos - NPC.Center) * 0.002f;

            int meteorInterval = Main.expertMode ? 15 : 20;
            if (AttackTimer % meteorInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float offsetX = Main.rand.NextFloat(-400f, 400f);
                Vector2 spawnPos = target.Center + new Vector2(offsetX, -800);
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), 12f);

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    spawnPos,
                    vel,
                    ModContent.ProjectileType<AokinMeteor>(),
                    NPC.damage / 3,
                    1f
                );
            }

            // 蓄力粒子
            if (!VaultUtils.isServer && AttackTimer < 30) {
                for (int i = 0; i < 5; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(150, 150);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Torch, 0, 0, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 6f;
                }
            }

            if (AttackTimer == 10) {
                SoundEngine.PlaySound(SoundID.Item45 with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
            }

            if (AttackTimer > 150) {
                TransitionTo(BossPhase.Phase1_Patrol);
            }
        }

        #endregion
    }
}
