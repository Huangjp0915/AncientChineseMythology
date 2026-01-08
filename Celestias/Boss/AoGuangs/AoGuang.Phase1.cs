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
        #region 一阶段攻击

        /// <summary>
        /// 一阶段巡游 - 优雅环绕玩家
        /// </summary>
        private void RunPhase1Patrol(Player target) {
            // 环绕飞行
            float orbitSpeed = 0.02f;
            float orbitRadius = 400f;

            NPC.localAI[1] += orbitSpeed;
            if (NPC.localAI[1] > MathHelper.TwoPi)
                NPC.localAI[1] -= MathHelper.TwoPi;

            Vector2 targetPos = target.Center + new Vector2(
                MathF.Cos(NPC.localAI[1]) * orbitRadius,
                MathF.Sin(NPC.localAI[1]) * orbitRadius * 0.5f - 200f
            );

            // 波动效果
            targetPos.Y += MathF.Sin(globalTime * 2f) * 30f;

            Vector2 toTarget = targetPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 0.06f, 0.08f);

            // 水粒子拖尾
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(NPC.Center - NPC.velocity * 2f, 0, 0, DustID.Water, 0, 0, 150, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -NPC.velocity * 0.2f;
            }

            // 巡游持续时间后切换攻击
            if (PhaseTimer > 180) {
                TransitionTo(GetRandomPhase1Attack());
            }
        }

        /// <summary>
        /// 水弹幕攻击 - 发射多方向水弹（类似猪鲨的泡泡弹幕）
        /// </summary>
        private void RunPhase1WaterBarrage(Player target) {
            // 悬停
            Vector2 hoverPos = target.Center + new Vector2(0, -350);
            hoverPos.X += MathF.Sin(globalTime * 1.5f) * 100f;

            Vector2 toHover = hoverPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.04f, 0.1f);

            // 发射水弹
            int fireInterval = Main.expertMode ? 12 : 15;
            if (AttackTimer % fireInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                int bulletCount = 3;
                float spreadAngle = MathHelper.ToRadians(15f);

                for (int i = -bulletCount / 2; i <= bulletCount / 2; i++) {
                    Vector2 vel = toPlayer.RotatedBy(i * spreadAngle) * 10f;
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center + toPlayer * 50f,
                        vel,
                        ModContent.ProjectileType<DragonWaterBolt>(),
                        NPC.damage / 3,
                        1f
                    );
                }

                SoundEngine.PlaySound(SoundID.Item21 with { Pitch = 0.2f, Volume = 0.8f }, NPC.Center);
            }

            // 水雾效果
            if (!VaultUtils.isServer && AttackTimer % 5 == 0) {
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                for (int i = 0; i < 3; i++) {
                    Vector2 dustVel = toPlayer.RotatedByRandom(0.5f) * Main.rand.NextFloat(3, 6);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Water, dustVel.X, dustVel.Y, 180, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (AttackTimer > 150) {
                TransitionTo(BossPhase.Phase1_Patrol);
            }
        }

        /// <summary>
        /// 旋涡召唤 - 在玩家周围生成追踪旋涡
        /// </summary>
        private void RunPhase1VortexSummon(Player target) {
            NPC.velocity *= 0.95f;

            // 保持在玩家上方
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            NPC.velocity += (hoverPos - NPC.Center) * 0.002f;

            // 召唤旋涡
            if (AttackTimer == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                int vortexCount = Main.expertMode ? 4 : 3;
                for (int i = 0; i < vortexCount; i++) {
                    float angle = MathHelper.TwoPi * i / vortexCount;
                    Vector2 spawnPos = target.Center + angle.ToRotationVector2() * 300f;

                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        spawnPos,
                        Vector2.Zero,
                        ModContent.ProjectileType<WaterVortex>(),
                        NPC.damage / 4,
                        1f,
                        ai0: target.whoAmI
                    );
                }

                SoundEngine.PlaySound(SoundID.Item66 with { Pitch = -0.3f }, NPC.Center);
            }

            // 旋涡粒子
            if (!VaultUtils.isServer && AttackTimer > 30) {
                for (int i = 0; i < 5; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = Main.rand.NextFloat(100, 200);
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * radius;
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Water, 0, 0, 180, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 4f;
                }
            }

            if (AttackTimer > 180) {
                TransitionTo(BossPhase.Phase1_Patrol);
            }
        }

        /// <summary>
        /// 潮汐波攻击 - 扩散水环（类似猪鲨的冲击波）
        /// </summary>
        private void RunPhase1TidalWave(Player target) {
            switch ((int)SubState) {
                case 0: // 蓄力
                    NPC.velocity *= 0.9f;

                    // 吸收水粒子
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 5; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(150, 150);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.Water, 0, 0, 100, default, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 8f;
                        }
                    }

                    if (AttackTimer == 30) {
                        SoundEngine.PlaySound(SoundID.Item21 with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                    }

                    if (AttackTimer >= 60) {
                        SubState = 1;
                        AttackTimer = 0;

                        // 发射潮汐波
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                Vector2.Zero,
                                ModContent.ProjectileType<TidalWave>(),
                                NPC.damage / 3,
                                1f
                            );
                        }

                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(10, 30);
                    }
                    break;

                case 1: // 释放后恢复
                    NPC.velocity *= 0.95f;

                    if (AttackTimer > 60) {
                        TransitionTo(BossPhase.Phase1_Patrol);
                    }
                    break;
            }
        }

        #endregion
    }
}
