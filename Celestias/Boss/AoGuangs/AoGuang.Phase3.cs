using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    internal partial class AoGuang
    {
        #region 三阶段攻击

        /// <summary>
        /// 狂怒冲刺 - 更快更多次的冲刺
        /// </summary>
        private void RunPhase3FuryCharge(Player target) {
            switch ((int)SubState) {
                case 0: // 初始化
                    chargeCount = 0;
                    // V2 抛光: 降低连冲次数(原 6/5), 配合每次冲刺后的恢复拍, 减弱 spam 感
                    maxChargeCount = Main.expertMode ? 4 : 3;
                    SubState = 1;
                    AttackTimer = 0;
                    break;

                case 1: // 蓄力 (致命冲刺线预警, 略延长可读)
                    NPC.velocity *= 0.8f;

                    // 致命冲刺线预警 (红=致命, 渐强)
                    if (!VaultUtils.isServer) {
                        Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                        int count = 6 + (int)(AttackTimer / 5f);
                        for (int i = 0; i < count; i++) {
                            Vector2 dustPos = NPC.Center + toPlayer * (40 + i * 40);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.RedTorch, 0, 0, 100,
                                TelegraphColors.Lethal, 1.5f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = Vector2.Zero;
                        }
                    }

                    if (AttackTimer >= 24) {
                        chargeTarget = target.Center + target.velocity * 8f;
                        Vector2 toTarget = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                        float chargeSpeed = Main.expertMode ? 42f : 35f;
                        NPC.velocity = toTarget * chargeSpeed;

                        SubState = 2;
                        AttackTimer = 0;

                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.7f, Volume = 1f }, NPC.Center);
                        ACMUtils.AddScreenShake(11f);
                    }
                    break;

                case 2: // 冲刺
                    // 更密集的水花拖尾
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 8; i++) {
                            Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 50f;
                            dustPos += Main.rand.NextVector2Circular(40, 40);
                            int dustType = Main.rand.Next(3) switch {
                                0 => DustID.Water,
                                1 => DustID.BlueTorch,
                                _ => DustID.Wet
                            };
                            int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 80, default, 3f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.2f;
                        }
                    }

                    // 冲刺发射更多水弹
                    if (AttackTimer % 3 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 perpendicular = NPC.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                        for (int side = -1; side <= 1; side += 2) {
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                perpendicular * side * 6f + Main.rand.NextVector2Circular(2, 2),
                                ModContent.ProjectileType<DragonWaterBolt>(),
                                NPC.damage / 4,
                                1f
                            );
                        }
                    }

                    if (AttackTimer >= 22) {
                        chargeCount++;
                        if (chargeCount >= maxChargeCount) {
                            TransitionTo(GetRandomPhase3Attack());
                        }
                        else {
                            SubState = 3; // V2: 冲刺后短恢复拍, 给玩家可读窗口
                            AttackTimer = 0;
                        }
                    }
                    break;

                case 3: // 恢复 (冲刺间窗口, 缓速漂移)
                    NPC.velocity *= 0.9f;
                    if (AttackTimer >= 16) {
                        SubState = 1;
                        AttackTimer = 0;
                    }
                    break;
            }
        }

        /// <summary>
        /// 三叉戟风暴 - 全屏三叉戟弹幕
        /// </summary>
        private void RunPhase3TridentStorm(Player target) {
            NPC.velocity *= 0.93f;

            // 悬停
            Vector2 hoverPos = target.Center + new Vector2(0, -380);
            NPC.velocity += (hoverPos - NPC.Center) * 0.003f;

            // 发射三叉戟弹幕
            int fireInterval = Main.expertMode ? 6 : 8;
            if (AttackTimer % fireInterval == 0 && AttackTimer > 20 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = 8;
                float baseAngle = AttackTimer * 0.1f;
                for (int i = 0; i < count; i++) {
                    float angle = baseAngle + MathHelper.TwoPi * i / count;
                    Vector2 vel = angle.ToRotationVector2() * 12f;
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center,
                        vel,
                        ModContent.ProjectileType<TridentProjectile>(),
                        NPC.damage / 3,
                        1f
                    );
                }

                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f, Volume = 0.7f }, NPC.Center);
            }

            // 风暴粒子
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * Main.rand.NextFloat(80, 150);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 5f;
                }
            }

            if (AttackTimer > 150) {
                TransitionTo(GetRandomPhase3Attack());
            }
        }

        /// <summary>
        /// 潮汐光束 - 强力追踪水柱激光
        /// </summary>
        private void RunPhase3TidalBeam(Player target) {
            switch ((int)SubState) {
                case 0: // 蓄力
                    NPC.velocity *= 0.85f;

                    if (AttackTimer == 1) {
                        breathAngle = (target.Center - NPC.Center).ToRotation();
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.2f, Volume = 1.5f }, NPC.Center);
                    }

                    // 更强的蓄力效果
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 12; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(180, 180);
                            int dustType = Main.rand.NextBool() ? DustID.BlueTorch : DustID.Water;
                            int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 80, default, 2.5f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 15f;
                        }

                        // 致命激光路径预警 (红=致命, 处决级渐强)
                        if (AttackTimer > 12) {
                            Vector2 beamDir = breathAngle.ToRotationVector2();
                            for (int i = 0; i < 16; i++) {
                                Vector2 lp = NPC.Center + beamDir * (70 + i * 160);
                                int d = Dust.NewDust(lp, 0, 0, DustID.RedTorch, 0, 0, 110, TelegraphColors.Lethal, 1.5f);
                                Main.dust[d].noGravity = true;
                                Main.dust[d].velocity = beamDir * 2f;
                            }
                        }
                    }

                    // 震动增强 (取 max 不累加)
                    if (AttackTimer % 8 == 0) {
                        ACMUtils.AddScreenShake(MathHelper.Clamp(AttackTimer / 8f, 0f, 12f));
                    }

                    if (AttackTimer >= 50) {
                        SubState = 1;
                        AttackTimer = 0;

                        // 发射强力激光
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                Vector2.Zero,
                                ModContent.ProjectileType<TidalBeam>(),
                                NPC.damage,
                                0f,
                                ai0: NPC.whoAmI,
                                ai1: breathAngle
                            );
                        }

                        SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = 0.3f, Volume = 1.8f }, NPC.Center);
                        ACMUtils.AddScreenShake(12f);
                        waterBloom = 1f; // 潮汐激光释放·水爆泛光
                    }
                    break;

                case 1: // 激光扫射
                    NPC.velocity *= 0.9f;

                    // 更快的追踪
                    float targetAngle = (target.Center - NPC.Center).ToRotation();
                    breathAngle = MathHelper.Lerp(breathAngle, targetAngle, 0.025f);

                    if (AttackTimer > 100) {
                        TransitionTo(GetRandomPhase3Attack());
                    }
                    break;
            }
        }

        /// <summary>
        /// 龙王盘绕 - 环绕玩家收缩攻击
        /// </summary>
        private void RunPhase3DragonCoil(Player target) {
            switch ((int)SubState) {
                case 0: // 初始化
                    vortexAngle = (NPC.Center - target.Center).ToRotation();
                    vortexRadius = 400f;
                    SubState = 1;
                    AttackTimer = 0;
                    break;

                case 1: // 盘旋收缩
                    float coilSpeed = 0.06f + AttackTimer * 0.0003f;
                    vortexAngle += coilSpeed;

                    float targetRadius = 400f - AttackTimer * 2f;
                    if (targetRadius < 120f) targetRadius = 120f;
                    vortexRadius = MathHelper.Lerp(vortexRadius, targetRadius, 0.1f);

                    Vector2 coilTarget = target.Center + vortexAngle.ToRotationVector2() * vortexRadius;
                    Vector2 toCoil = coilTarget - NPC.Center;
                    NPC.velocity = toCoil * 0.15f;

                    // 盘绕粒子
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(40, 40);
                            int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                            int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (vortexAngle + MathHelper.PiOver2).ToRotationVector2() * 4f;
                        }
                    }

                    // 发射追踪水弹
                    if (AttackTimer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            toPlayer * 8f,
                            ModContent.ProjectileType<HomingWaterOrb>(),
                            NPC.damage / 4,
                            1f
                        );
                    }

                    if (AttackTimer > 150 || vortexRadius <= 120f) {
                        SubState = 2;
                        AttackTimer = 0;
                    }
                    break;

                case 2: // 爆发冲刺
                    if (AttackTimer == 1) {
                        Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                        NPC.velocity = toPlayer * 35f;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.4f }, NPC.Center);
                        ACMUtils.AddScreenShake(11f);

                        // 爆发水花
                        if (!VaultUtils.isServer) {
                            for (int i = 0; i < 40; i++) {
                                float angle = MathHelper.TwoPi * i / 40;
                                Vector2 vel = angle.ToRotationVector2() * 8f;
                                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Water, vel.X, vel.Y, 100, default, 3f);
                                Main.dust[dust].noGravity = true;
                            }
                        }
                    }

                    NPC.velocity *= 0.97f;

                    if (AttackTimer > 35) {
                        TransitionTo(GetRandomPhase3Attack());
                    }
                    break;
            }
        }

        /// <summary>
        /// 终极海啸 - 多波次全方位攻击
        /// </summary>
        private void RunPhase3FinalTsunami(Player target) {
            switch ((int)SubState) {
                case 0: // 第一波：环形水弹爆发
                    NPC.velocity *= 0.88f;

                    if (AttackTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 1.5f }, NPC.Center);
                    }

                    if (AttackTimer % 12 == 0 && AttackTimer <= 72 && Main.netMode != NetmodeID.MultiplayerClient) {
                        int count = 12;
                        float baseAngle = AttackTimer * 0.12f;
                        for (int i = 0; i < count; i++) {
                            float angle = baseAngle + MathHelper.TwoPi * i / count;
                            Vector2 vel = angle.ToRotationVector2() * 10f;
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

                    if (AttackTimer > 80) {
                        SubState = 1;
                        AttackTimer = 0;
                    }
                    break;

                case 1: // 第二波：多方向冲刺
                    chargeCount = 0;
                    maxChargeCount = 4;
                    SubState = 2;
                    AttackTimer = 0;
                    break;

                case 2: // 冲刺蓄力
                    NPC.velocity *= 0.8f;

                    if (AttackTimer >= 15) {
                        Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                        NPC.velocity = toPlayer * 38f;
                        SubState = 3;
                        AttackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.6f }, NPC.Center);
                    }
                    break;

                case 3: // 冲刺中
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 4; i++) {
                            Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 40f;
                            int dust = Dust.NewDust(dustPos + Main.rand.NextVector2Circular(30, 30), 0, 0, DustID.Water, 0, 0, 100, default, 3f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.15f;
                        }
                    }

                    if (AttackTimer >= 18) {
                        chargeCount++;
                        if (chargeCount >= maxChargeCount) {
                            SubState = 4;
                            AttackTimer = 0;
                        }
                        else {
                            SubState = 2;
                            AttackTimer = 0;
                        }
                    }
                    break;

                case 4: // 第三波：巨型海啸
                    NPC.velocity *= 0.85f;

                    if (AttackTimer == 25 && Main.netMode != NetmodeID.MultiplayerClient) {
                        // 多方向潮汐波
                        for (int i = 0; i < 4; i++) {
                            float angle = MathHelper.PiOver2 * i;
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                angle.ToRotationVector2() * 2f,
                                ModContent.ProjectileType<TidalWave>(),
                                NPC.damage / 3,
                                0f
                            );
                        }
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0f, Volume = 2f }, NPC.Center);
                        ACMUtils.AddScreenShake(12f);
                        waterBloom = 1f; // 终极海啸·水爆泛光
                    }

                    if (AttackTimer > 120) {
                        TransitionTo(GetRandomPhase3Attack());
                    }
                    break;
            }
        }

        /// <summary>
        /// 海龙狂舞 - 高速S形蛇行移动并发射弹幕
        /// </summary>
        private void RunPhase3SeaDragonDance(Player target) {
            // S形蛇行移动
            float baseAngle = (target.Center - NPC.Center).ToRotation();
            float waveOffset = MathF.Sin(AttackTimer * 0.1f) * 0.8f;
            float currentAngle = baseAngle + waveOffset;

            float speed = 25f;
            Vector2 targetVelocity = currentAngle.ToRotationVector2() * speed;
            NPC.velocity = Vector2.Lerp(NPC.velocity, targetVelocity, 0.15f);

            // 蛇行粒子拖尾
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 6; i++) {
                    Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * (20 + i * 15);
                    dustPos += Main.rand.NextVector2Circular(20, 20);
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Water,
                        1 => DustID.BlueTorch,
                        _ => DustID.Wet
                    };
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 120, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -NPC.velocity * 0.1f;
                }
            }

            // 发射水弹
            if (AttackTimer % 6 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                // 向两侧发射
                Vector2 perpendicular = NPC.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                for (int side = -1; side <= 1; side += 2) {
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center,
                        perpendicular * side * 8f,
                        ModContent.ProjectileType<DragonWaterBolt>(),
                        NPC.damage / 4,
                        1f
                    );
                }
            }

            // 每隔一段时间发射追踪水球
            if (AttackTimer % 30 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<HomingWaterOrb>(),
                    NPC.damage / 4,
                    1f
                );
            }

            if (AttackTimer > 180) {
                TransitionTo(GetRandomPhase3Attack());
            }
        }

        /// <summary>
        /// 深渊漩涡 - 在场地中央生成巨大漩涡并召唤水柱
        /// </summary>
        private void RunPhase3AbyssalVortex(Player target) {
            switch ((int)SubState) {
                case 0: // 飞到上方
                    Vector2 risePos = target.Center + new Vector2(0, -500);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (risePos - NPC.Center) * 0.06f, 0.12f);

                    if (AttackTimer >= 40 || Vector2.Distance(NPC.Center, risePos) < 80f) {
                        SubState = 1;
                        AttackTimer = 0;
                    }
                    break;

                case 1: // 召唤深渊漩涡
                    NPC.velocity *= 0.9f;

                    if (AttackTimer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            target.Center,
                            Vector2.Zero,
                            ModContent.ProjectileType<AbyssalVortex>(),
                            NPC.damage / 3,
                            0f,
                            ai0: NPC.whoAmI
                        );

                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 1.5f }, target.Center);
                        ACMUtils.AddScreenShake(12f);
                        waterBloom = 1f; // 深渊漩涡降临·水爆泛光
                    }

                    // 从上方发射水柱
                    if (AttackTimer > 30 && AttackTimer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 spikePos = target.Center + new Vector2(Main.rand.NextFloat(-400, 400), -600);
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            spikePos,
                            new Vector2(0, 15f),
                            ModContent.ProjectileType<FallingWaterSpear>(),
                            NPC.damage / 3,
                            1f
                        );
                    }

                    // 深渊粒子
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 5; i++) {
                            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                            float radius = Main.rand.NextFloat(150, 300);
                            Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * radius;
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 180, default, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 6f;
                        }
                    }

                    if (AttackTimer > 180) {
                        TransitionTo(GetRandomPhase3Attack());
                    }
                    break;
            }
        }

        #endregion
    }
}
