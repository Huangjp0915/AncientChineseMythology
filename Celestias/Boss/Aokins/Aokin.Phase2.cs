using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    internal partial class Aokin
    {
        #region 狂怒冲刺

        /// <summary>狂怒冲刺 - 多次高速冲向玩家。</summary>
        private bool RunFuryCharge(Player target) {
            switch (subState) {
                case 0:
                    chargeCount = 0;
                    maxChargeCount = IsPhase3 ? 5 : (Main.expertMode ? 4 : 3);
                    subState = 1;
                    attackTimer = 0;
                    break;

                case 1: // 蓄力瞄准（冲刺线预告）
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
                    if (attackTimer >= 25) {
                        chargeTarget = target.Center + target.velocity * 10f;
                        Vector2 toTarget = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                        float chargeSpeed = IsPhase3 ? 38f : (Main.expertMode ? 35f : 28f);
                        NPC.velocity = toTarget * chargeSpeed;
                        subState = 2;
                        attackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 0.8f }, NPC.Center);
                        ACMUtils.AddScreenShake(8f);
                    }
                    break;

                case 2: // 冲刺中
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

                    if (attackTimer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 perpendicular = NPC.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                        for (int side = -1; side <= 1; side += 2) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                                perpendicular * side * 4f,
                                ModContent.ProjectileType<AokinFireball>(), NPC.damage / 4, 1f);
                        }
                    }

                    if (attackTimer > 30 || Vector2.Distance(NPC.Center, chargeTarget) < 80) {
                        chargeCount++;
                        if (chargeCount >= maxChargeCount) {
                            AddHeat(20f);
                            return true;
                        }
                        subState = 1;
                        attackTimer = 0;
                        NPC.velocity *= 0.3f;
                    }
                    break;
            }
            return false;
        }

        #endregion

        #region 烈焰旋涡

        /// <summary>烈焰旋涡 - 在玩家周围召唤旋转火焰旋涡。</summary>
        private bool RunFlameVortex(Player target) {
            NPC.velocity *= 0.95f;
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            NPC.velocity += (hoverPos - NPC.Center) * 0.002f;

            if (attackTimer == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                int vortexCount = Main.expertMode ? 4 : 3;
                for (int i = 0; i < vortexCount; i++) {
                    float angle = MathHelper.TwoPi * i / vortexCount;
                    Vector2 spawnPos = target.Center + angle.ToRotationVector2() * 250f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, Vector2.Zero,
                        ModContent.ProjectileType<AokinFlameVortex>(), NPC.damage / 4, 1f);
                }
                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.3f }, NPC.Center);
            }

            if (!VaultUtils.isServer && attackTimer > 30) {
                for (int i = 0; i < 5; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = Main.rand.NextFloat(100, 200);
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * radius;
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Torch, 0, 0, 180, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 4f;
                }
            }

            if (attackTimer > 180) {
                AddHeat(16f);
                return true;
            }
            return false;
        }

        #endregion

        #region 地狱龙息

        /// <summary>地狱龙息 - 强化版龙息，更宽更密集。</summary>
        private bool RunInfernoBreath(Player target) {
            NPC.rotation = (target.Center - NPC.Center).ToRotation();

            if (target.Distance(NPC.Center) > 500) {
                NPC.velocity += (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 0.4f;
                NPC.velocity *= 0.97f;
            }
            else {
                NPC.velocity *= 0.98f;
            }

            int fireInterval = Main.expertMode ? 4 : 6;
            if (attackTimer % fireInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int damage = Main.expertMode ? 40 : 55;
                Vector2 direction = NPC.rotation.ToRotationVector2();
                for (int spread = -1; spread <= 1; spread++) {
                    Vector2 vel = direction.RotatedBy(spread * MathHelper.ToRadians(8)) * 16f;
                    int p = Projectile.NewProjectile(NPC.GetSource_FromAI(),
                        NPC.Center + direction * 50f, vel,
                        ModContent.ProjectileType<AokinFireball>(), damage, 1f);
                    Main.projectile[p].timeLeft = 80;
                }
            }

            if (!VaultUtils.isServer) {
                Vector2 breathDir = NPC.rotation.ToRotationVector2();
                for (int i = 0; i < 8; i++) {
                    Vector2 dustPos = NPC.Center + breathDir * (30 + i * 8) + Main.rand.NextVector2Circular(15, 15);
                    int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, breathDir.X * 10, breathDir.Y * 10, 100, default, 3f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (attackTimer > 200) {
                AddHeat(22f);
                return true;
            }
            return false;
        }

        #endregion

        #region 烈焰俯冲（带冷却）

        /// <summary>烈焰俯冲 - 飞到高空后垂直俯冲。</summary>
        private bool RunDivebomb(Player target) {
            NPC.rotation = NPC.velocity.ToRotation();

            switch (subState) {
                case 0: { // 上升
                    Vector2 skyTarget = new Vector2(target.Center.X, target.Center.Y - 800);
                    NPC.velocity += (skyTarget - NPC.Center).SafeNormalize(Vector2.Zero) * 2f;
                    NPC.velocity *= 0.97f;

                    if (Vector2.Distance(NPC.Center, skyTarget) < 60) {
                        subState = 1;
                        attackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 1.2f }, NPC.Center);
                    }
                    break;
                }
                case 1: { // 俯冲
                    NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 5f;
                    NPC.velocity.Y = IsPhase3 ? 38f : 32f;

                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 8; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(40, 40);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.SolarFlare, 0, -8, 100, default, 3f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (NPC.Center.Y > target.Center.Y + 200) {
                        divebombCooldown = 900;
                        ACMUtils.AddScreenShake(12f);
                        lavaBloom = 0.6f;
                        AddHeat(18f);
                        return true;
                    }
                    break;
                }
            }
            return false;
        }

        #endregion

        #region 炼狱茧 — 满温泄压（无敌帧 + 带缺口扩张火环, 有反制）

        /// <summary>
        /// 炼狱茧 Inferno Cocoon（满温泄压 set-piece）：
        ///   蓄力（runic 向心收口 + 渐强泛光/震屏, 无敌帧）→ 释放一道扩张火环，环上有一道随机缺口（telegraph 金芒），
        ///   玩家须朝缺口冲出（反制）。释放清空温度。把"你把房间烧热了"的因果收束为一次可读的泄压。
        /// </summary>
        private bool RunInfernoCocoon(Player target) {
            NPC.dontTakeDamage = true; // i-frame beat

            switch (subState) {
                case 0: { // 锚定 + 蓄力
                    Vector2 anchor = target.Center + new Vector2(0, -260);
                    NPC.velocity = (anchor - NPC.Center) * 0.12f;

                    if (attackTimer == 1)
                        SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);

                    // 渐强蓄力泛光 + 震屏（处决级预警）
                    float chargeT = MathHelper.Clamp(attackTimer / 90f, 0f, 1f);
                    lavaBloom = Math.Max(lavaBloom, chargeT * 0.7f);
                    if (chargeT > 0.6f)
                        ACMUtils.AddScreenShake((chargeT - 0.6f) / 0.4f * 7f);

                    if (!VaultUtils.isServer && attackTimer % 3 == 0)
                        AokinHelper.CreateFlameVortex(NPC.Center, 60f + attackTimer * 0.8f, 0.6f, 12);

                    if (attackTimer >= 90) {
                        subState = 1;
                        attackTimer = 0;
                        lavaBloom = 1f;
                        ACMUtils.AddScreenShake(11f);
                        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.4f, Volume = 1.5f }, NPC.Center);

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // 缺口角度（server 决策并同步）
                            float gapAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                            int rings = IsPhase3 ? 2 : 1;
                            for (int r = 0; r < rings; r++) {
                                Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                    NPC.Center, Vector2.Zero,
                                    ModContent.ProjectileType<AokinInfernoRing>(),
                                    Main.expertMode ? 55 : 70, 4f, Main.myPlayer,
                                    ai0: gapAngle + r * 0.6f, ai1: r);
                            }
                        }
                        VentHeat();
                    }
                    break;
                }
                case 1: { // 释放余波
                    NPC.velocity *= 0.92f;
                    if (attackTimer > 70) {
                        NPC.dontTakeDamage = false;
                        return true;
                    }
                    break;
                }
            }
            return false;
        }

        #endregion
    }
}
