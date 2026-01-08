using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    internal partial class AoGuang
    {
        #region 二阶段攻击

        /// <summary>
        /// 猛烈冲刺 - 类似猪鲨的多次冲刺攻击
        /// </summary>
        private void RunPhase2Charge(Player target) {
            switch ((int)SubState) {
                case 0: // 初始化
                    chargeCount = 0;
                    maxChargeCount = Main.expertMode ? 4 : 3;
                    SubState = 1;
                    AttackTimer = 0;
                    break;

                case 1: // 蓄力瞄准
                    NPC.velocity *= 0.85f;

                    // 瞄准指示
                    if (!VaultUtils.isServer) {
                        Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                        for (int i = 0; i < 3; i++) {
                            Vector2 dustPos = NPC.Center + toPlayer * (50 + i * 30);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 100, default, 1.5f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = Vector2.Zero;
                        }
                    }

                    if (AttackTimer >= 25) {
                        // 计算预判位置
                        chargeTarget = target.Center + target.velocity * 10f;
                        Vector2 toTarget = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                        float chargeSpeed = Main.expertMode ? 35f : 28f;
                        NPC.velocity = toTarget * chargeSpeed;

                        SubState = 2;
                        AttackTimer = 0;

                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 0.8f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 20);
                    }
                    break;

                case 2: // 冲刺中
                    // 水花拖尾
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 5; i++) {
                            Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 40f;
                            dustPos += Main.rand.NextVector2Circular(30, 30);
                            int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                            int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 2.5f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.15f;
                        }
                    }

                    // 冲刺期间发射水弹
                    if (AttackTimer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 perpendicular = NPC.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                        for (int side = -1; side <= 1; side += 2) {
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                perpendicular * side * 4f,
                                ModContent.ProjectileType<DragonWaterBolt>(),
                                NPC.damage / 4,
                                1f
                            );
                        }
                    }

                    if (AttackTimer >= 25) {
                        chargeCount++;
                        if (chargeCount >= maxChargeCount) {
                            TransitionTo(GetRandomPhase2Attack());
                        }
                        else {
                            SubState = 1;
                            AttackTimer = 0;
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 召唤虾兵蟹将
        /// </summary>
        private void RunPhase2SummonMinions(Player target) {
            NPC.velocity *= 0.95f;

            // 保持在玩家上方
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            NPC.velocity += (hoverPos - NPC.Center) * 0.002f;

            // 召唤小怪
            if (AttackTimer == 60 && Main.netMode != NetmodeID.MultiplayerClient) {
                int minionCount = Main.expertMode ? 4 : 3;
                for (int i = 0; i < minionCount; i++) {
                    float angle = MathHelper.TwoPi * i / minionCount + Main.rand.NextFloat(-0.2f, 0.2f);
                    Vector2 spawnPos = NPC.Center + angle.ToRotationVector2() * 150f;

                    // 召唤虾兵或蟹将
                    int minionType = ModContent.ProjectileType<DragonMinion>();
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        spawnPos,
                        (target.Center - spawnPos).SafeNormalize(Vector2.Zero) * 5f,
                        minionType,
                        NPC.damage / 4,
                        1f,
                        ai0: NPC.whoAmI
                    );
                }

                SoundEngine.PlaySound(SoundID.Item96 with { Pitch = -0.2f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(8, 20);

                // 召唤粒子
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 30; i++) {
                        float angle = MathHelper.TwoPi * i / 30;
                        Vector2 vel = angle.ToRotationVector2() * 6f;
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Water, vel.X, vel.Y, 100, default, 2f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (AttackTimer > 120) {
                TransitionTo(GetRandomPhase2Attack());
            }
        }

        /// <summary>
        /// 大漩涡 - 生成吸引玩家的大型漩涡
        /// </summary>
        private void RunPhase2Whirlpool(Player target) {
            NPC.velocity *= 0.95f;

            // 悬停在远处
            Vector2 hoverPos = target.Center + new Vector2(target.Center.X > NPC.Center.X ? -500 : 500, -200);
            NPC.velocity += (hoverPos - NPC.Center) * 0.003f;

            // 生成漩涡
            if (AttackTimer == 40 && Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    target.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<GiantWhirlpool>(),
                    NPC.damage / 3,
                    1f
                );

                SoundEngine.PlaySound(SoundID.Item66 with { Pitch = -0.5f, Volume = 1.2f }, target.Center);
            }

            // 在漩涡期间发射水弹干扰玩家
            if (AttackTimer > 60 && AttackTimer % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                for (int i = -2; i <= 2; i++) {
                    Vector2 vel = toPlayer.RotatedBy(MathHelper.ToRadians(10 * i)) * 12f;
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center,
                        vel,
                        ModContent.ProjectileType<DragonWaterBolt>(),
                        NPC.damage / 4,
                        1f
                    );
                }
                SoundEngine.PlaySound(SoundID.Item21 with { Pitch = 0.3f, Volume = 0.6f }, NPC.Center);
            }

            if (AttackTimer > 200) {
                TransitionTo(GetRandomPhase2Attack());
            }
        }

        /// <summary>
        /// 龙息攻击 - 水柱扫射
        /// </summary>
        private void RunPhase2DragonBreath(Player target) {
            switch ((int)SubState) {
                case 0: // 蓄力
                    NPC.velocity *= 0.9f;

                    if (AttackTimer == 1) {
                        breathAngle = (target.Center - NPC.Center).ToRotation();
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                    }

                    // 蓄力粒子
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 8; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(120, 120);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.Water, 0, 0, 100, default, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 10f;
                        }
                    }

                    // 震动
                    if (AttackTimer % 10 == 0) {
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(AttackTimer / 10f, 10);
                    }

                    if (AttackTimer >= 60) {
                        SubState = 1;
                        AttackTimer = 0;
                        isBreathActive = true;

                        // 发射龙息
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                Vector2.Zero,
                                ModContent.ProjectileType<DragonBreathBeam>(),
                                NPC.damage / 2,
                                0f,
                                ai0: NPC.whoAmI,
                                ai1: breathAngle
                            );
                        }

                        SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(15, 100);
                    }
                    break;

                case 1: // 龙息扫射
                    NPC.velocity *= 0.95f;

                    // 缓慢追踪玩家
                    float targetAngle = (target.Center - NPC.Center).ToRotation();
                    breathAngle = MathHelper.Lerp(breathAngle, targetAngle, 0.02f);

                    if (AttackTimer > 90) {
                        isBreathActive = false;
                        TransitionTo(GetRandomPhase2Attack());
                    }
                    break;
            }
        }

        /// <summary>
        /// 龙卷风冲刺 - 高速旋转冲刺
        /// </summary>
        private void RunPhase2TornadoRush(Player target) {
            switch ((int)SubState) {
                case 0: // 升起准备
                    Vector2 risePos = target.Center + new Vector2(0, -500);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (risePos - NPC.Center) * 0.05f, 0.1f);

                    // 旋转粒子
                    if (!VaultUtils.isServer) {
                        float angle = AttackTimer * 0.2f;
                        Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * 80f;
                        int dust = Dust.NewDust(dustPos, 0, 0, DustID.Water, 0, 0, 150, default, 2f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 5f;
                    }

                    if (AttackTimer >= 50 || Vector2.Distance(NPC.Center, risePos) < 100f) {
                        SubState = 1;
                        AttackTimer = 0;
                        vortexAngle = (target.Center - NPC.Center).ToRotation();
                    }
                    break;

                case 1: // 龙卷俯冲
                    vortexAngle += 0.15f; // 旋转效果
                    float speed = 30f;
                    Vector2 direction = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                    NPC.velocity = direction * speed;

                    // 龙卷粒子
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 8; i++) {
                            float dustAngle = vortexAngle + MathHelper.TwoPi * i / 8;
                            Vector2 dustOffset = dustAngle.ToRotationVector2() * 60f;
                            int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                            int dust = Dust.NewDust(NPC.Center + dustOffset, 0, 0, dustType, 0, 0, 100, default, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (dustAngle + MathHelper.PiOver2).ToRotationVector2() * 6f;
                        }
                    }

                    // 发射水弹
                    if (AttackTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 shotDir = vortexAngle.ToRotationVector2();
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            shotDir * 8f,
                            ModContent.ProjectileType<DragonWaterBolt>(),
                            NPC.damage / 4,
                            1f
                        );
                    }

                    // 接近玩家或超时后结束
                    if (AttackTimer > 60 || Vector2.Distance(NPC.Center, target.Center) < 80f) {
                        TransitionTo(GetRandomPhase2Attack());
                    }
                    break;
            }
        }

        #endregion
    }
}
