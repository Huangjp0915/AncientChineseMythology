using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas
{
    /// <summary>
    /// 毗沙门天王 - 阶段AI定义（分离文件）
    /// </summary>
    internal partial class Vaisravana
    {
        #region 阶段枚举

        public enum BossPhase
        {
            Intro,                      // 出场演出
            Phase1_TowerGlory,          // 一阶段：宝塔威光，悬浮观测
            Phase1_TowerBeam,           // 一阶段：宝塔光束
            Phase1_HolyBarrage,         // 一阶段：神圣弹幕齐射
            Phase1_SweepingLight,       // 一阶段：扫射光芒
            Phase1_StarRain,            // 一阶段：星辰雨
            PhaseTransition_2,          // 一阶段到二阶段转换
            Phase2_Descend,             // 二阶段：天王降临
            Phase2_YakshaSummon,        // 二阶段：召唤夜叉
            Phase2_QuadrantRay,         // 二阶段：四方圣光
            Phase2_ImmortalWave,        // 二阶段：仙气波动
            Phase2_DivineDash,          // 二阶段：神圣冲刺
            Phase2_HaloStorm,           // 二阶段：光环风暴
            PhaseTransition_3,          // 二阶段到三阶段转换
            Phase3_FourKingsWrath,      // 三阶段：四天王威
            Phase3_TowerJudgment,       // 三阶段：宝塔审判
            Phase3_UltimateTower,       // 三阶段：终极宝塔光
            Phase3_YakshaSync,          // 三阶段：夜叉同步攻击
            Phase3_FinalRadiance        // 三阶段：最终光辉
        }

        #endregion

        #region 一阶段AI

        private void RunPhase1TowerGlory(Player target) {
            // 宝塔威光 - 神圣悬浮，保持在玩家上方
            Vector2 hoverPos = target.Center + new Vector2(0, -380);
            hoverPos.X += MathF.Sin(globalTime * 1.0f) * 50f;
            hoverPos.Y += MathF.Sin(globalTime * 1.5f) * 20f;

            Vector2 toHover = hoverPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.022f, 0.08f);

            towerOrbitSpeed = 0.012f;

            // 定期发射宝塔神光弹
            float shotCooldown = Main.expertMode ? 30f : 40f;
            if (AttackTimer % shotCooldown == 0) {
                FireTowerOrbs(target);
            }

            // 定期发射追踪光束
            if (AttackTimer % 70 == 0) {
                FireTowerBeams(target);
            }

            // 随机切换攻击
            if (PhaseTimer > 280) {
                int nextAction = Main.rand.Next(5);
                switch (nextAction) {
                    case 0:
                        TransitionTo(BossPhase.Phase1_TowerBeam);
                        break;
                    case 1:
                        TransitionTo(BossPhase.Phase1_HolyBarrage);
                        break;
                    case 2:
                        TransitionTo(BossPhase.Phase1_SweepingLight);
                        break;
                    case 3:
                        TransitionTo(BossPhase.Phase1_StarRain);
                        break;
                    default:
                        PhaseTimer = 0;
                        break;
                }
            }
        }

        private void FireTowerOrbs(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int orbCount = Main.expertMode ? 5 : 3;
            float spread = MathHelper.ToRadians(25);
            Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            float baseAngle = toTarget.ToRotation();

            for (int i = 0; i < orbCount; i++) {
                float angle = baseAngle + spread * (i - (orbCount - 1) / 2f) / (orbCount - 1);
                Vector2 velocity = angle.ToRotationVector2() * 11f;

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    velocity,
                    ModContent.ProjectileType<TreasureTowerOrb>(),
                    NPC.damage / 2,
                    2f,
                    Main.myPlayer
                );
            }

            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.2f }, NPC.Center);
        }

        private void FireTowerBeams(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // 从随机宝塔位置发射
            int towerIndex = Main.rand.Next(TowerCount);
            Vector2 towerPos = GetTowerPosition(towerIndex);
            Vector2 toTarget = (target.Center - towerPos).SafeNormalize(Vector2.UnitY);

            Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                towerPos,
                toTarget * 9f,
                ModContent.ProjectileType<TowerBeam>(),
                NPC.damage / 3,
                1f,
                Main.myPlayer
            );

            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.4f }, towerPos);
        }

        private void RunPhase1TowerBeam(Player target) {
            switch ((int)SubState) {
                case 0: // 蓄力
                    NPC.velocity *= 0.92f;

                    if (PhaseTimer == 1) {
                        laserAngle = (target.Center - NPC.Center).ToRotation();
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.6f, Volume = 1.2f }, NPC.Center);
                    }

                    // 蓄力粒子
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(120, 120);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 100, default, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 8f;
                        }
                    }

                    laserChargeTime = Main.expertMode ? 45 : 55;
                    if (PhaseTimer >= laserChargeTime) {
                        SubState = 1;
                        PhaseTimer = 0;

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                Vector2.Zero,
                                ModContent.ProjectileType<TreasureTowerRay>(),
                                NPC.damage,
                                0f,
                                Main.myPlayer,
                                ai0: NPC.whoAmI,
                                ai1: laserAngle
                            );
                        }

                        SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = 0.3f, Volume = 1.5f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(15, 60);
                    }
                    break;

                case 1: // 激光发射中
                    NPC.velocity *= 0.95f;

                    if (PhaseTimer > 100) {
                        TransitionTo(BossPhase.Phase1_TowerGlory);
                    }
                    break;
            }
        }

        private void RunPhase1HolyBarrage(Player target) {
            NPC.velocity *= 0.95f;

            Vector2 hoverPos = target.Center + new Vector2(0, -320);
            NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.02f);

            // 环形射击
            if (PhaseTimer % 12 == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int count = 10;
                    float baseAngle = PhaseTimer * 0.08f;
                    for (int i = 0; i < count; i++) {
                        float angle = baseAngle + MathHelper.TwoPi * i / count;
                        Vector2 velocity = angle.ToRotationVector2() * 9f;

                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            velocity,
                            ModContent.ProjectileType<TreasureTowerOrb>(),
                            NPC.damage / 3,
                            1f,
                            Main.myPlayer
                        );
                    }
                }

                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.3f }, NPC.Center);
            }

            if (PhaseTimer > 180) {
                TransitionTo(BossPhase.Phase1_TowerGlory);
            }
        }

        private void RunPhase1SweepingLight(Player target) {
            switch ((int)SubState) {
                case 0: // 准备
                    NPC.velocity *= 0.9f;

                    Vector2 sweepHoverPos = target.Center + new Vector2(0, -380);
                    NPC.Center = Vector2.Lerp(NPC.Center, sweepHoverPos, 0.03f);

                    if (PhaseTimer == 1) {
                        laserSweepDirection = Main.rand.NextBool() ? 1f : -1f;
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.4f }, NPC.Center);
                    }

                    // 预警线
                    if (!VaultUtils.isServer) {
                        float sweepAngle = MathHelper.PiOver4 * laserSweepDirection;
                        Vector2 lineDir = sweepAngle.ToRotationVector2();
                        for (int i = 0; i < 10; i++) {
                            Vector2 dustPos = NPC.Center + lineDir * (i * 80);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 150, default, 0.8f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (PhaseTimer >= 35) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 扫射
                    if (PhaseTimer % 6 == 0 && PhaseTimer <= 72) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            float progress = PhaseTimer / 72f;
                            float startAngle = laserSweepDirection > 0 ? -MathHelper.PiOver4 : MathHelper.PiOver4;
                            float endAngle = laserSweepDirection > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4;
                            float currentAngle = MathHelper.Lerp(startAngle, endAngle, progress) + MathHelper.PiOver2;

                            Vector2 velocity = currentAngle.ToRotationVector2() * 20f;
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                velocity,
                                ModContent.ProjectileType<SweepingLightBolt>(),
                                NPC.damage / 2,
                                2f,
                                Main.myPlayer
                            );
                        }

                        SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.6f }, NPC.Center);
                    }

                    if (PhaseTimer > 90) {
                        TransitionTo(BossPhase.Phase1_TowerGlory);
                    }
                    break;
            }
        }

        private void RunPhase1StarRain(Player target) {
            switch ((int)SubState) {
                case 0: // 准备召唤
                    NPC.velocity *= 0.9f;

                    if (PhaseTimer == 1) {
                        starCount = Main.expertMode ? 10 : 6;
                        starPositions = new Vector2[starCount];
                        for (int i = 0; i < starCount; i++) {
                            float angle = MathHelper.TwoPi * i / starCount + Main.rand.NextFloat(-0.15f, 0.15f);
                            float distance = 450f + Main.rand.NextFloat(-60f, 60f);
                            starPositions[i] = target.Center + angle.ToRotationVector2() * distance;
                        }

                        SoundEngine.PlaySound(SoundID.Item25 with { Pitch = 0.4f }, NPC.Center);
                    }

                    // 星辰预警
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < starCount; i++) {
                            if (starPositions[i] == Vector2.Zero) continue;
                            float alpha = PhaseTimer / 50f;
                            Vector2 pos = starPositions[i];
                            int dust = Dust.NewDust(pos, 0, 0, DustID.WhiteTorch, 0, 0, 100, default, 1.8f * alpha);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (PhaseTimer >= 50) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 星辰坠落
                    if (PhaseTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < starCount; i++) {
                                if (starPositions[i] == Vector2.Zero) continue;
                                Vector2 toTarget = (target.Center - starPositions[i]).SafeNormalize(Vector2.Zero);
                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(),
                                    starPositions[i],
                                    toTarget * 14f,
                                    ModContent.ProjectileType<VaisravanaStar>(),
                                    NPC.damage / 2,
                                    3f,
                                    Main.myPlayer
                                );
                            }
                        }

                        SoundEngine.PlaySound(SoundID.Item92 with { Pitch = 0.2f }, NPC.Center);
                    }

                    if (PhaseTimer > 70) {
                        TransitionTo(BossPhase.Phase1_TowerGlory);
                    }
                    break;
            }
        }

        #endregion

        #region 阶段转换

        private void RunPhaseTransition2(Player target) {
            NPC.velocity *= 0.95f;

            // 宝塔加速旋转
            towerOrbitSpeed = 0.04f + PhaseTimer * 0.0008f;

            // 能量聚集效果
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 10; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(220, 220);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 50, default, 2.2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 10f;
                }

                if (PhaseTimer % 8 == 0) {
                    for (int i = 0; i < 6; i++) {
                        Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(60, 60);
                        int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 100, default, 2f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (PhaseTimer == 50) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(18, 50);
            }

            if (PhaseTimer > 90) {
                towerOrbitSpeed = 0.02f;
                TransitionTo(BossPhase.Phase2_Descend);
            }
        }

        private void RunPhaseTransition3(Player target) {
            NPC.velocity *= 0.93f;

            // 极速宝塔旋转
            towerOrbitSpeed = 0.07f + PhaseTimer * 0.0015f;

            // 神圣能量风暴
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 15; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(280, 280);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 50, default, 2.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 14f;
                }
            }

            if (PhaseTimer == 35) {
                SoundEngine.PlaySound(SoundID.Item119 with { Volume = 1.5f }, NPC.Center);
            }

            if (PhaseTimer == 70) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(28, 70);
            }

            if (PhaseTimer > 110) {
                towerOrbitSpeed = 0.03f;
                TransitionTo(BossPhase.Phase3_FourKingsWrath);
            }
        }

        #endregion
    }
}
