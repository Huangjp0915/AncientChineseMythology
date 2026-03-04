using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    internal partial class Aokin
    {
        #region 二阶段攻击

        /// <summary>
        /// 狂怒冲刺 - 多次高速冲向玩家
        /// </summary>
        private void RunPhase2FuryCharge(Player target) {
            switch ((int)SubState) {
                case 0: // 初始化
                    chargeCount = 0;
                    maxChargeCount = Main.expertMode ? 4 : 3;
                    SubState = 1;
                    AttackTimer = 0;
                    break;

                case 1: // 蓄力瞄准
                    NPC.velocity *= 0.85f;

                    if (!VaultUtils.isServer) {
                        Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                        for (int i = 0; i < 3; i++) {
                            Vector2 dustPos = NPC.Center + toPlayer * (50 + i * 30);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.Torch, 0, 0, 100, default, 1.5f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = Vector2.Zero;
                        }
                    }

                    if (AttackTimer >= 25) {
                        chargeTarget = target.Center + target.velocity * 10f;
                        Vector2 toTarget = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                        float chargeSpeed = Main.expertMode ? 35f : 28f;
                        NPC.velocity = toTarget * chargeSpeed;

                        SubState = 2;
                        AttackTimer = 0;

                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 0.8f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 20);
                    }
                    break;

                case 2: // 冲刺中
                    // 火焰尾迹
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 5; i++) {
                            Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 40f;
                            dustPos += Main.rand.NextVector2Circular(30, 30);
                            int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                            int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 2.5f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.15f;
                        }
                    }

                    // 冲刺期间发射火弹
                    if (AttackTimer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 perpendicular = NPC.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                        for (int side = -1; side <= 1; side += 2) {
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                perpendicular * side * 4f,
                                ModContent.ProjectileType<AokinFireball>(),
                                NPC.damage / 4,
                                1f
                            );
                        }
                    }

                    if (AttackTimer > 30 || Vector2.Distance(NPC.Center, chargeTarget) < 80) {
                        chargeCount++;
                        if (chargeCount >= maxChargeCount) {
                            TransitionTo(GetRandomPhase2Attack());
                        }
                        else {
                            SubState = 1;
                            AttackTimer = 0;
                            NPC.velocity *= 0.3f;
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 烈焰旋涡 - 在玩家周围召唤旋转火焰旋涡
        /// </summary>
        private void RunPhase2FlameVortex(Player target) {
            NPC.velocity *= 0.95f;
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            NPC.velocity += (hoverPos - NPC.Center) * 0.002f;

            // 召唤旋涡
            if (AttackTimer == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                int vortexCount = Main.expertMode ? 4 : 3;
                for (int i = 0; i < vortexCount; i++) {
                    float angle = MathHelper.TwoPi * i / vortexCount;
                    Vector2 spawnPos = target.Center + angle.ToRotationVector2() * 250f;

                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        spawnPos,
                        Vector2.Zero,
                        ModContent.ProjectileType<AokinFlameVortex>(),
                        NPC.damage / 4,
                        1f
                    );
                }

                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.3f }, NPC.Center);
            }

            // 旋涡粒子
            if (!VaultUtils.isServer && AttackTimer > 30) {
                for (int i = 0; i < 5; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = Main.rand.NextFloat(100, 200);
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * radius;
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Torch, 0, 0, 180, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 4f;
                }
            }

            if (AttackTimer > 180) {
                TransitionTo(GetRandomPhase2Attack());
            }
        }

        /// <summary>
        /// 地狱龙息 - 强化版龙息，更宽更密集
        /// </summary>
        private void RunPhase2InfernoBreath(Player target) {
            NPC.rotation = (target.Center - NPC.Center).ToRotation();

            if (target.Distance(NPC.Center) > 500) {
                NPC.velocity += (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 0.4f;
                NPC.velocity *= 0.97f;
            }
            else {
                NPC.velocity *= 0.98f;
            }

            int fireInterval = Main.expertMode ? 4 : 6;
            if (AttackTimer % fireInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int damage = Main.expertMode ? 40 : 55;

                Vector2 direction = NPC.rotation.ToRotationVector2();
                // 三道龙息
                for (int spread = -1; spread <= 1; spread++) {
                    Vector2 vel = direction.RotatedBy(spread * MathHelper.ToRadians(8)) * 16f;
                    int p = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center + direction * 50f,
                        vel,
                        ModContent.ProjectileType<AokinFireball>(),
                        damage,
                        1f
                    );
                    Main.projectile[p].timeLeft = 80;
                }
            }

            // 龙息火焰粒子 - 更密集
            if (!VaultUtils.isServer) {
                Vector2 breathDir = NPC.rotation.ToRotationVector2();
                for (int i = 0; i < 8; i++) {
                    Vector2 dustPos = NPC.Center + breathDir * (30 + i * 8) + Main.rand.NextVector2Circular(15, 15);
                    int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, breathDir.X * 10, breathDir.Y * 10, 100, default, 3f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (AttackTimer > 200) {
                TransitionTo(GetRandomPhase2Attack());
            }
        }

        /// <summary>
        /// 陨石风暴 - 更密集的陨石雨
        /// </summary>
        private void RunPhase2MeteorStorm(Player target) {
            NPC.velocity *= 0.95f;
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            NPC.velocity += (hoverPos - NPC.Center) * 0.002f;

            int meteorInterval = Main.expertMode ? 8 : 12;
            if (AttackTimer % meteorInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                // 双倍陨石
                for (int m = 0; m < 2; m++) {
                    float offsetX = Main.rand.NextFloat(-500f, 500f);
                    Vector2 spawnPos = target.Center + new Vector2(offsetX, -800);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-3f, 3f), 14f);

                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        spawnPos,
                        vel,
                        ModContent.ProjectileType<AokinMeteor>(),
                        NPC.damage / 3,
                        1f
                    );
                }
            }

            if (AttackTimer == 10) {
                SoundEngine.PlaySound(SoundID.Item45 with { Pitch = -0.6f, Volume = 1.5f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(8, 40);
            }

            if (AttackTimer > 200) {
                TransitionTo(GetRandomPhase2Attack());
            }
        }

        /// <summary>
        /// 烈焰俯冲 - 飞到高空后垂直俯冲
        /// </summary>
        private void RunPhase2Divebomb(Player target) {
            NPC.rotation = NPC.velocity.ToRotation();

            switch ((int)SubState) {
                case 0: // 上升
                    Vector2 skyTarget = new Vector2(target.Center.X, target.Center.Y - 800);
                    NPC.velocity += (skyTarget - NPC.Center).SafeNormalize(Vector2.Zero) * 2f;
                    NPC.velocity *= 0.97f;

                    if (Vector2.Distance(NPC.Center, skyTarget) < 60) {
                        SubState = 1;
                        AttackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 1.2f }, NPC.Center);
                    }
                    break;

                case 1: // 俯冲
                    NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 5f * (1f - (float)NPC.life / NPC.lifeMax);
                    NPC.velocity.Y = 32f;

                    // 俯冲火焰尾迹
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 8; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(40, 40);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.SolarFlare, 0, -8, 100, default, 3f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (NPC.Center.Y > target.Center.Y + 200) {
                        divebombCooldown = 900;
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(20, 40);
                        TransitionTo(GetRandomPhase2Attack());
                    }
                    break;
            }
        }

        /// <summary>
        /// 突袭火球 - 突然后退并发射巨型火球
        /// </summary>
        private void RunPhase2SurpriseFireball(Player target) {
            int damage = Main.expertMode ? 70 : 100;

            NPC.velocity = (NPC.Center - target.Center).SafeNormalize(Vector2.Zero) * 12f;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int p = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    -NPC.velocity * 0.5f + target.velocity,
                    ModContent.ProjectileType<AokinFireball>(),
                    damage,
                    1f
                );
                Main.projectile[p].scale = 3f;
            }

            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);

            // 爆发粒子
            AokinHelper.CreateDragonFireBurst(NPC.Center, 150f, 2, 12);

            TransitionTo(GetRandomPhase2Attack());
        }

        #endregion
    }
}
