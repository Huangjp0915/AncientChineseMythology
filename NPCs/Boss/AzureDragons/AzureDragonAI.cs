using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 青龙头部 - AI招式实现
    /// </summary>
    public partial class AzureDragonHead
    {
        #region 出场演出

        private void RunIntro(Player target) {
            introProgress = MathHelper.Clamp(StateTimer / 180f, 0f, 1f);
            NPC.dontTakeDamage = true;

            // 从天而降，雷光环绕
            Vector2 introOffset = new Vector2(0, -800) * (1f - ACMUtils.SineInOut(introProgress));
            Vector2 desiredPos = target.Center + new Vector2(0, -400) + introOffset;
            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, 0.03f);
            NPC.velocity *= 0.9f;

            // 青蓝色雷光粒子
            if (!VaultUtils.isServer && StateTimer % 2 == 0) {
                for (int i = 0; i < 6; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(200, 200) * (1f - introProgress);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 6f;
                }

                // 闪电火花
                if (Main.rand.NextBool(3)) {
                    Vector2 sparkPos = NPC.Center + Main.rand.NextVector2Circular(150, 150);
                    int spark = Dust.NewDust(sparkPos, 0, 0, DustID.Electric, 0, 0, 150, default, 1.5f);
                    Main.dust[spark].noGravity = true;
                }
            }

            if (StateTimer == 60) {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
            }

            if (StateTimer == 120) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 1.8f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(18, 60);

                // 出场雷暴爆发
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 60; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(12, 12);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.BlueTorch, vel.X, vel.Y, 80, default, 2.5f);
                        Main.dust[dust].noGravity = true;
                    }
                    for (int i = 0; i < 20; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(8, 8);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric, vel.X, vel.Y, 50, default, 2f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (StateTimer > 180) {
                NPC.dontTakeDamage = false;
                TransitionTo(AIState.Phase1_Orbit);
            }
        }

        #endregion

        #region 一阶段 - 苍龙出海

        /// <summary>
        /// 盘旋巡弋 - 在玩家周围做8字盘旋，一段时间后切换到攻击招式
        /// </summary>
        private void RunPhase1Orbit(Player target) {
            orbitAngle += orbitSpeed;

            float R = 500f;
            float r = 180f;
            float h = 300f;
            float offsetX = R * MathF.Cos(orbitAngle);
            float offsetY = r * MathF.Sin(orbitAngle * 2f);

            Vector2 desiredPos = target.Center + new Vector2(offsetX, -h + offsetY);
            SmoothOrbit(desiredPos, 80f);

            // 盘旋时青色拖尾粒子
            if (!VaultUtils.isServer && StateTimer % 4 == 0) {
                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.BlueTorch, 0, 0, 120, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -NPC.velocity * 0.2f;
            }

            // 盘旋一定时间后切换攻击
            if (StateTimer > 180) {
                TransitionTo(PickPhase1Attack());
            }
        }

        /// <summary>
        /// 龙息吐息 - 朝玩家方向释放扇形青色能量弹幕
        /// </summary>
        private void RunPhase1DragonBreath(Player target) {
            switch ((int)SubState) {
                case 0: // 蓄力阶段
                    NPC.velocity *= 0.92f;

                    // 蓄力粒子向口部汇聚
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(120, 120);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 100, default, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 8f;
                        }
                    }

                    if (AttackTimer >= 60) {
                        SubState = 1;
                        AttackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(10, 30);
                    }
                    break;

                case 1: // 吐息阶段 - 连续发射能量弹
                    NPC.velocity *= 0.95f;

                    // 缓缓追踪玩家
                    Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    NPC.velocity += toTarget * 0.3f;

                    // 每隔一定帧发射龙息弹
                    int fireInterval = Main.expertMode ? 6 : 8;
                    if (AttackTimer % fireInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        float baseAngle = toTarget.ToRotation();
                        int bulletCount = 3;
                        float spread = MathHelper.ToRadians(25f);

                        for (int i = 0; i < bulletCount; i++) {
                            float angle = baseAngle + MathHelper.Lerp(-spread, spread, i / (float)(bulletCount - 1));
                            Vector2 vel = angle.ToRotationVector2() * 14f;
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(), NPC.Center + toTarget * 40f, vel,
                                ProjectileID.CultistBossLightningOrb, NPC.damage / 4, 2f
                            );
                        }
                    }

                    // 吐息时青色烟雾粒子
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 5; i++) {
                            Vector2 dustVel = toTarget.RotatedByRandom(0.4f) * Main.rand.NextFloat(4, 10);
                            int dust = Dust.NewDust(NPC.Center + toTarget * 30f, 0, 0, DustID.BlueTorch,
                                dustVel.X, dustVel.Y, 80, default, Main.rand.NextFloat(1.5f, 2.5f));
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (AttackTimer >= 90) {
                        TransitionTo(AIState.Phase1_Orbit);
                    }
                    break;
            }
        }

        /// <summary>
        /// 雷球轰击 - 在玩家周围生成多个雷球，短暂延迟后爆炸
        /// </summary>
        private void RunPhase1ThunderOrbs(Player target) {
            switch ((int)SubState) {
                case 0: // 悬停蓄力
                    Vector2 hoverPos = target.Center + new Vector2(0, -450);
                    SmoothOrbit(hoverPos, 50f);

                    if (!VaultUtils.isServer && AttackTimer % 3 == 0) {
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric, 0, 0, 150, default, 2f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].velocity = Main.rand.NextVector2Circular(3, 3);
                    }

                    if (AttackTimer >= 60) {
                        SubState = 1;
                        AttackTimer = 0;

                        SoundEngine.PlaySound(SoundID.Item93 with { Pitch = -0.2f, Volume = 1.3f }, NPC.Center);

                        // 在玩家周围生成雷球
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int orbCount = Main.expertMode ? 8 : 6;
                            for (int i = 0; i < orbCount; i++) {
                                float angle = MathHelper.TwoPi * i / orbCount;
                                float radius = 300f + Main.rand.NextFloat(-50f, 50f);
                                Vector2 spawnPos = target.Center + angle.ToRotationVector2() * radius;
                                Vector2 vel = (target.Center - spawnPos).SafeNormalize(Vector2.Zero) * 8f;

                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(), spawnPos, vel,
                                    ProjectileID.CultistBossLightningOrbArc, NPC.damage / 4, 2f
                                );
                            }
                        }
                    }
                    break;

                case 1: // 等待雷球爆炸
                    NPC.velocity *= 0.9f;

                    // 电弧粒子效果
                    if (!VaultUtils.isServer && AttackTimer % 5 == 0) {
                        for (int i = 0; i < 8; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(100, 100);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.Electric, 0, 0, 100, default, 1.5f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (AttackTimer >= 80) {
                        TransitionTo(AIState.Phase1_Orbit);
                    }
                    break;
            }
        }

        /// <summary>
        /// 苍龙冲刺 - 向玩家高速冲刺，带有青色拖尾
        /// </summary>
        private void RunPhase1Charge(Player target) {
            switch ((int)SubState) {
                case 0: // 锁定准备
                    chargeCount = 0;
                    maxCharges = Main.expertMode ? 4 : 3;
                    SubState = 1;
                    AttackTimer = 0;
                    break;

                case 1: // 预告
                    NPC.velocity *= 0.85f;

                    // 冲刺方向指示粒子
                    if (!VaultUtils.isServer) {
                        Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                        for (int i = 0; i < 4; i++) {
                            Vector2 dustPos = NPC.Center + toPlayer * (40 + i * 30);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 100, default, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = Vector2.Zero;
                        }
                    }

                    if (AttackTimer >= 25) {
                        chargeTarget = target.Center + target.velocity * 10f;
                        chargeDirection = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                        float chargeSpeed = Main.expertMode ? 38f : 30f;
                        NPC.velocity = chargeDirection * chargeSpeed;

                        SubState = 2;
                        AttackTimer = 0;

                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 1f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 20);
                    }
                    break;

                case 2: // 冲刺中
                    // 青色冲刺拖尾
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 40f;
                            dustPos += Main.rand.NextVector2Circular(30, 30);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch,
                                0, 0, 60, default, Main.rand.NextFloat(2f, 3f));
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.1f;
                        }
                        // 电弧火花
                        if (Main.rand.NextBool(2)) {
                            int spark = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric, 0, 0, 50, default, 1.5f);
                            Main.dust[spark].noGravity = true;
                        }
                    }

                    if (AttackTimer >= 22) {
                        chargeCount++;
                        if (chargeCount >= maxCharges) {
                            TransitionTo(AIState.Phase1_Orbit);
                        }
                        else {
                            SubState = 1;
                            AttackTimer = 0;
                        }
                    }
                    break;
            }
        }

        #endregion

        #region 阶段转换演出

        private void RunPhaseTransition2(Player target) {
            NPC.velocity *= 0.92f;
            NPC.dontTakeDamage = true;

            // 雷暴聚拢特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(250, 250);
                    int dustType = Main.rand.NextBool() ? DustID.BlueTorch : DustID.Electric;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 12f;
                }
            }

            if (StateTimer == 50) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0f, Volume = 2f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(22, 60);

                // 爆发电弧
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 80; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(15, 15);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric,
                            vel.X, vel.Y, 50, default, Main.rand.NextFloat(2f, 3.5f));
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (StateTimer > 90) {
                NPC.dontTakeDamage = false;
                TransitionTo(AIState.Phase2_StormChase);
            }
        }

        private void RunPhaseTransition3(Player target) {
            NPC.velocity *= 0.9f;
            NPC.dontTakeDamage = true;

            // 天威降临 - 更强烈的雷暴特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 12; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(300, 300);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Electric, 0, 0, 50, default, 3f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 15f;
                }
                for (int i = 0; i < 5; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(150, 150);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 80, default, 3f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 10f;
                }
            }

            if (StateTimer == 40) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 2.5f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(30, 80);
            }

            if (StateTimer == 70) {
                // 全方位电弧爆发
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 120; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(18, 18);
                        int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.BlueTorch;
                        int dust = Dust.NewDust(NPC.Center, 0, 0, dustType,
                            vel.X, vel.Y, 40, default, Main.rand.NextFloat(2.5f, 4f));
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (StateTimer > 100) {
                NPC.dontTakeDamage = false;
                TransitionTo(AIState.Phase3_ThunderJudgment);
            }
        }

        #endregion

        #region 二阶段 - 雷霆震怒

        /// <summary>
        /// 风暴追击 - 快速追踪玩家，同时释放闪电火花
        /// </summary>
        private void RunPhase2StormChase(Player target) {
            // 高速追踪
            Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
            float chaseSpeed = Main.expertMode ? 18f : 14f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * chaseSpeed, 0.06f);

            // 追踪时释放电弧粒子
            if (!VaultUtils.isServer && StateTimer % 3 == 0) {
                for (int i = 0; i < 4; i++) {
                    Vector2 offset = Main.rand.NextVector2Circular(40, 40);
                    int dust = Dust.NewDust(NPC.Center + offset, 0, 0, DustID.Electric,
                        0, 0, 80, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -NPC.velocity * 0.15f + Main.rand.NextVector2Circular(2, 2);
                }
            }

            // 追踪过程中周期性释放雷电弹
            int shootInterval = Main.expertMode ? 20 : 30;
            if (StateTimer % shootInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = toTarget.RotatedByRandom(0.3f) * 12f;
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(), NPC.Center, vel,
                    ProjectileID.CultistBossLightningOrbArc, NPC.damage / 4, 2f
                );
            }

            if (StateTimer > 200) {
                TransitionTo(PickPhase2Attack());
            }
        }

        /// <summary>
        /// 闪电矩阵 - 在战场上布下电弧网格
        /// </summary>
        private void RunPhase2LightningMatrix(Player target) {
            switch ((int)SubState) {
                case 0: // 升空蓄力
                    Vector2 highPos = target.Center + new Vector2(0, -600);
                    SmoothOrbit(highPos, 40f);

                    if (!VaultUtils.isServer && AttackTimer % 2 == 0) {
                        Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(80, 80);
                        int dust = Dust.NewDust(dustPos, 0, 0, DustID.Electric, 0, 0, 50, default, 2f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                    }

                    if (AttackTimer >= 50) {
                        SubState = 1;
                        AttackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item93 with { Pitch = 0.2f, Volume = 1.5f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(15, 40);
                    }
                    break;

                case 1: // 释放闪电阵
                    NPC.velocity *= 0.9f;

                    if (AttackTimer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                        // 纵横交错的闪电
                        int lines = Main.expertMode ? 6 : 4;
                        for (int i = 0; i < lines; i++) {
                            float angle = MathHelper.TwoPi * i / lines;
                            Vector2 dir = angle.ToRotationVector2();
                            for (int j = 1; j <= 3; j++) {
                                Vector2 spawnPos = target.Center + dir * (j * 200);
                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(), spawnPos, Vector2.Zero,
                                    ProjectileID.CultistBossLightningOrbArc, NPC.damage / 5, 1f
                                );
                            }
                        }
                    }

                    // 矩阵闪烁特效
                    if (!VaultUtils.isServer && AttackTimer % 4 == 0) {
                        for (int i = 0; i < 10; i++) {
                            Vector2 dustPos = target.Center + Main.rand.NextVector2Circular(400, 400);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.Electric, 0, 0, 80, default, 1.5f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (AttackTimer >= 80) {
                        TransitionTo(AIState.Phase2_StormChase);
                    }
                    break;
            }
        }

        /// <summary>
        /// 龙卷风暴 - 高速螺旋盘旋逼近玩家，留下电弧旋风
        /// </summary>
        private void RunPhase2TornadoSweep(Player target) {
            orbitAngle += 0.08f; // 极快的旋转

            float radius = MathHelper.Lerp(500f, 100f, MathHelper.Clamp(StateTimer / 180f, 0, 1));
            Vector2 desiredPos = target.Center + orbitAngle.ToRotationVector2() * radius;
            NPC.velocity = (desiredPos - NPC.Center) * 0.12f;

            // 旋风拖尾
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 5; i++) {
                    Vector2 dustVel = (-NPC.velocity).SafeNormalize(Vector2.Zero).RotatedByRandom(1f) * Main.rand.NextFloat(3, 8);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.BlueTorch,
                        dustVel.X, dustVel.Y, 80, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
                if (Main.rand.NextBool(3)) {
                    int spark = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric, 0, 0, 50, default, 1.5f);
                    Main.dust[spark].noGravity = true;
                }
            }

            // 旋风过程中释放弹幕
            if (StateTimer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = NPC.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * 10f;
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(), NPC.Center, vel,
                    ProjectileID.CultistBossLightningOrb, NPC.damage / 5, 1f
                );
            }

            if (StateTimer > 200) {
                TransitionTo(AIState.Phase2_StormChase);
            }
        }

        /// <summary>
        /// 急速连冲 - 多次极速冲刺，每次改变方向
        /// </summary>
        private void RunPhase2RapidCharge(Player target) {
            switch ((int)SubState) {
                case 0:
                    chargeCount = 0;
                    maxCharges = Main.expertMode ? 6 : 5;
                    SubState = 1;
                    AttackTimer = 0;
                    break;

                case 1: // 短暂锁定
                    NPC.velocity *= 0.8f;

                    if (!VaultUtils.isServer) {
                        Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                        for (int i = 0; i < 5; i++) {
                            Vector2 dustPos = NPC.Center + toPlayer * (30 + i * 25);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.Electric, 0, 0, 100, default, 2.2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = Vector2.Zero;
                        }
                    }

                    if (AttackTimer >= 16) {
                        chargeTarget = target.Center + target.velocity * 8f;
                        Vector2 toTargetVec = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                        float speed = Main.expertMode ? 45f : 36f;
                        NPC.velocity = toTargetVec * speed;

                        SubState = 2;
                        AttackTimer = 0;

                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.8f, Volume = 0.9f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(14, 20);
                    }
                    break;

                case 2: // 冲刺
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 8; i++) {
                            Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 50f;
                            dustPos += Main.rand.NextVector2Circular(35, 35);
                            int dustType = Main.rand.NextBool() ? DustID.BlueTorch : DustID.Electric;
                            int dust = Dust.NewDust(dustPos, 0, 0, dustType,
                                0, 0, 50, default, Main.rand.NextFloat(2f, 3.5f));
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.08f;
                        }
                    }

                    if (AttackTimer >= 18) {
                        chargeCount++;
                        if (chargeCount >= maxCharges) {
                            TransitionTo(AIState.Phase2_StormChase);
                        }
                        else {
                            SubState = 1;
                            AttackTimer = 0;
                        }
                    }
                    break;
            }
        }

        #endregion

        #region 三阶段 - 天威降世

        /// <summary>
        /// 雷霆审判 - 全屏范围连续落雷+能量弹幕倾泻
        /// </summary>
        private void RunPhase3ThunderJudgment(Player target) {
            // 居高临下
            Vector2 highPos = target.Center + new Vector2(MathF.Sin(globalTime) * 200, -500);
            SmoothOrbit(highPos, 40f);

            // 持续落雷
            int strikeInterval = Main.expertMode ? 8 : 12;
            if (StateTimer % strikeInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                // 在玩家周围随机位置生成向下的闪电弹幕
                Vector2 strikePos = target.Center + Main.rand.NextVector2Circular(500, 200);
                strikePos.Y -= 800;
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), 18f);

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(), strikePos, vel,
                    ProjectileID.CultistBossLightningOrbArc, NPC.damage / 4, 2f
                );
            }

            // 同时释放追踪能量弹
            if (StateTimer % 30 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 3; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 vel = angle.ToRotationVector2() * 10f;
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(), NPC.Center, vel,
                        ProjectileID.CultistBossLightningOrb, NPC.damage / 5, 1f
                    );
                }
            }

            // 雷暴视觉效果
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 6; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(150, 150);
                    int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.BlueTorch;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 50, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (StateTimer > 300) {
                TransitionTo(PickPhase3Attack());
            }
        }

        /// <summary>
        /// 天怒龙卷 - 极速螺旋逼近+爆发电弧洪流
        /// </summary>
        private void RunPhase3CelestialFury(Player target) {
            float progress = MathHelper.Clamp(StateTimer / 240f, 0, 1);
            orbitAngle += 0.1f + progress * 0.05f;

            float radius = MathHelper.Lerp(600f, 60f, ACMUtils.SineInOut(progress));
            Vector2 desiredPos = target.Center + orbitAngle.ToRotationVector2() * radius;
            NPC.velocity = (desiredPos - NPC.Center) * 0.15f;

            // 极致旋风特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    Vector2 dustVel = (-NPC.velocity).SafeNormalize(Vector2.Zero).RotatedByRandom(1.2f) *
                        Main.rand.NextFloat(4, 12);
                    int dustType = Main.rand.NextBool() ? DustID.BlueTorch : DustID.Electric;
                    int dust = Dust.NewDust(NPC.Center, 0, 0, dustType,
                        dustVel.X, dustVel.Y, 40, default, Main.rand.NextFloat(2f, 3.5f));
                    Main.dust[dust].noGravity = true;
                }
            }

            // 高频弹幕释放
            if (StateTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = NPC.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * 12f;
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(), NPC.Center, vel,
                    ProjectileID.CultistBossLightningOrbArc, NPC.damage / 5, 1f
                );
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(), NPC.Center, -vel,
                    ProjectileID.CultistBossLightningOrbArc, NPC.damage / 5, 1f
                );
            }

            // 到达最内圈时爆发
            if (progress > 0.9f && SubState == 0) {
                SubState = 1;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 2f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(25, 60);

                // 爆发电弧
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 12; i++) {
                        float angle = MathHelper.TwoPi * i / 12f;
                        Vector2 vel = angle.ToRotationVector2() * 15f;
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(), NPC.Center, vel,
                            ProjectileID.CultistBossLightningOrb, NPC.damage / 4, 2f
                        );
                    }
                }

                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 100; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(20, 20);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric,
                            vel.X, vel.Y, 30, default, Main.rand.NextFloat(2.5f, 4f));
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (StateTimer > 280) {
                TransitionTo(PickPhase3Attack());
            }
        }

        /// <summary>
        /// 龙升天击 - 极速升天后俯冲轰击，沿途留下电弧地带
        /// </summary>
        private void RunPhase3DragonAscent(Player target) {
            switch ((int)SubState) {
                case 0: // 蓄力升天
                    NPC.velocity.Y -= 1.5f;
                    NPC.velocity.X *= 0.95f;

                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 6; i++) {
                            int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.BlueTorch,
                                Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(2, 6), 80, default, 2.5f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (AttackTimer >= 50) {
                        SubState = 1;
                        AttackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 1.8f }, NPC.Center);
                    }
                    break;

                case 1: // 锁定俯冲
                    NPC.velocity *= 0.7f;

                    if (AttackTimer >= 15) {
                        chargeTarget = target.Center + target.velocity * 5f;
                        chargeDirection = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                        NPC.velocity = chargeDirection * (Main.expertMode ? 55f : 45f);

                        SubState = 2;
                        AttackTimer = 0;
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(20, 40);
                    }
                    break;

                case 2: // 俯冲轰击
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 10; i++) {
                            Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 60f;
                            dustPos += Main.rand.NextVector2Circular(40, 40);
                            int dustType = Main.rand.Next(3) switch {
                                0 => DustID.Electric,
                                1 => DustID.BlueTorch,
                                _ => DustID.IceTorch
                            };
                            int dust = Dust.NewDust(dustPos, 0, 0, dustType,
                                0, 0, 30, default, Main.rand.NextFloat(2.5f, 4f));
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.05f;
                        }
                    }

                    // 俯冲路径上释放电弧
                    if (AttackTimer % 4 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 perpendicular = NPC.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(), NPC.Center, perpendicular * 8f,
                            ProjectileID.CultistBossLightningOrbArc, NPC.damage / 5, 1f
                        );
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(), NPC.Center, -perpendicular * 8f,
                            ProjectileID.CultistBossLightningOrbArc, NPC.damage / 5, 1f
                        );
                    }

                    if (AttackTimer >= 25) {
                        // 着陆冲击
                        if (!VaultUtils.isServer) {
                            for (int i = 0; i < 60; i++) {
                                Vector2 vel = Main.rand.NextVector2CircularEdge(15, 15);
                                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Electric,
                                    vel.X, vel.Y, 30, default, Main.rand.NextFloat(2f, 3.5f));
                                Main.dust[dust].noGravity = true;
                            }
                        }
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(18, 30);

                        TransitionTo(PickPhase3Attack());
                    }
                    break;
            }
        }

        #endregion
    }
}
