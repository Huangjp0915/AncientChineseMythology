using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas
{
    /// <summary>
    /// 毗沙门天王 - 二阶段和三阶段AI（分离文件）
    /// </summary>
    internal partial class Vaisravana
    {
        #region 二阶段AI

        private void RunPhase2Descend(Player target) {
            // 天王降临 - 下压并释放环形仙气波
            Vector2 descendPos = target.Center + new Vector2(0, -220);
            NPC.Center = Vector2.Lerp(NPC.Center, descendPos, 0.025f);

            // 释放仙气波
            if (PhaseTimer % 50 == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<ImmortalWave>(),
                        NPC.damage / 3,
                        0f,
                        Main.myPlayer
                    );
                }

                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.1f }, NPC.Center);
            }

            // 环形光弹
            if (PhaseTimer % 35 == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int waveCount = 14;
                    for (int i = 0; i < waveCount; i++) {
                        float angle = MathHelper.TwoPi * i / waveCount;
                        Vector2 velocity = angle.ToRotationVector2() * 7f;

                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            velocity,
                            ModContent.ProjectileType<TreasureTowerOrb>(),
                            NPC.damage / 3,
                            2f,
                            Main.myPlayer
                        );
                    }
                }
            }

            // 仙气粒子
            if (!VaultUtils.isServer && PhaseTimer % 3 == 0) {
                Vector2 dustPos = target.Center + Main.rand.NextVector2Circular(350, 60) + new Vector2(0, -80);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 2.5f, 100, default, 1.3f);
                Main.dust[dust].noGravity = true;
            }

            if (PhaseTimer > 200) {
                int nextAction = Main.rand.Next(5);
                switch (nextAction) {
                    case 0:
                        TransitionTo(BossPhase.Phase2_YakshaSummon);
                        break;
                    case 1:
                        TransitionTo(BossPhase.Phase2_QuadrantRay);
                        break;
                    case 2:
                        TransitionTo(BossPhase.Phase2_ImmortalWave);
                        break;
                    case 3:
                        TransitionTo(BossPhase.Phase2_DivineDash);
                        break;
                    case 4:
                        TransitionTo(BossPhase.Phase2_HaloStorm);
                        break;
                }
            }
        }

        private void RunPhase2YakshaSummon(Player target) {
            switch ((int)SubState) {
                case 0: // 召唤阶段
                    NPC.velocity *= 0.9f;

                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item119 with { Pitch = 0.3f }, NPC.Center);

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int minionCount = Main.expertMode ? 4 : 3;
                            yakshaMinionIds = new int[minionCount];

                            for (int i = 0; i < minionCount; i++) {
                                float angle = MathHelper.TwoPi * i / minionCount;
                                Vector2 spawnPos = NPC.Center + angle.ToRotationVector2() * 120f;

                                int npcId = NPC.NewNPC(
                                    NPC.GetSource_FromAI(),
                                    (int)spawnPos.X,
                                    (int)spawnPos.Y,
                                    ModContent.NPCType<YakshaMinion>(),
                                    ai0: NPC.whoAmI,
                                    ai1: i
                                );
                                yakshaMinionIds[i] = npcId;
                            }

                            hasSpawnedMinions = true;
                        }
                    }

                    // 召唤粒子效果
                    if (!VaultUtils.isServer && PhaseTimer <= 30) {
                        for (int i = 0; i < 10; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(100, 100);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 100, default, 2.2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = Main.rand.NextVector2Circular(4, 4);
                        }
                    }

                    if (PhaseTimer >= 60) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 等待仆从行动
                    Vector2 hoverPos = target.Center + new Vector2(0, -340);
                    NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.012f);

                    if (PhaseTimer > 160) {
                        TransitionTo(BossPhase.Phase2_Descend);
                    }
                    break;
            }
        }

        private void RunPhase2QuadrantRay(Player target) {
            switch ((int)SubState) {
                case 0: // 准备
                    NPC.velocity *= 0.9f;

                    Vector2 quadHoverPos = target.Center + new Vector2(0, -300);
                    NPC.Center = Vector2.Lerp(NPC.Center, quadHoverPos, 0.03f);

                    if (PhaseTimer == 1) {
                        laserAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f }, NPC.Center);
                    }

                    // 预警线
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 4; i++) {
                            float angle = laserAngle + MathHelper.PiOver2 * i;
                            Vector2 dir = angle.ToRotationVector2();
                            for (int j = 0; j < 10; j++) {
                                Vector2 dustPos = NPC.Center + dir * (j * 100);
                                int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 150, default, 1f);
                                Main.dust[dust].noGravity = true;
                            }
                        }
                    }

                    if (PhaseTimer >= 45) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 发射四方激光
                    if (PhaseTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < 4; i++) {
                                float angle = laserAngle + MathHelper.PiOver2 * i;
                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(),
                                    NPC.Center,
                                    Vector2.Zero,
                                    ModContent.ProjectileType<QuadrantRay>(),
                                    NPC.damage / 2,
                                    0f,
                                    Main.myPlayer,
                                    ai0: NPC.whoAmI,
                                    ai1: angle
                                );
                            }
                        }

                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.3f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 35);
                    }

                    laserAngle += 0.012f;

                    if (PhaseTimer > 100) {
                        TransitionTo(BossPhase.Phase2_Descend);
                    }
                    break;
            }
        }

        private void RunPhase2ImmortalWave(Player target) {
            NPC.velocity *= 0.92f;

            Vector2 hoverPos = target.Center + new Vector2(0, -280);
            NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.02f);

            // 多次释放仙气波
            if (PhaseTimer % 30 == 0 && PhaseTimer <= 120) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<ImmortalWave>(),
                        NPC.damage / 3,
                        0f,
                        Main.myPlayer
                    );
                }

                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.1f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(8, 20);
            }

            if (PhaseTimer > 150) {
                TransitionTo(BossPhase.Phase2_Descend);
            }
        }

        private void RunPhase2DivineDash(Player target) {
            switch ((int)SubState) {
                case 0: // 准备冲刺
                    dashCount = 0;
                    maxDashCount = Main.expertMode ? 4 : 3;
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: // 蓄力
                    NPC.velocity *= 0.9f;

                    // 蓄力粒子
                    if (!VaultUtils.isServer) {
                        Vector2 dustVel = Main.rand.NextVector2CircularEdge(6, 6);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.WhiteTorch, dustVel.X, dustVel.Y, 100, default, 1.6f);
                        Main.dust[dust].noGravity = true;
                    }

                    if (PhaseTimer >= 22) {
                        dashTarget = target.Center;
                        dashVelocity = (dashTarget - NPC.Center).SafeNormalize(Vector2.Zero) * 32f;
                        SubState = 2;
                        PhaseTimer = 0;

                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.4f }, NPC.Center);
                    }
                    break;

                case 2: // 冲刺
                    NPC.velocity = dashVelocity;

                    // 冲刺拖尾粒子
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 4; i++) {
                            Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 35f * i;
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 100, default, 2.2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.1f;
                        }
                    }

                    if (PhaseTimer >= 22) {
                        dashCount++;
                        if (dashCount >= maxDashCount) {
                            TransitionTo(BossPhase.Phase2_Descend);
                        }
                        else {
                            SubState = 1;
                            PhaseTimer = 0;
                        }
                    }
                    break;
            }
        }

        private void RunPhase2HaloStorm(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -300);
            NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.018f);

            // 释放旋转光环
            if (PhaseTimer % 22 == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    float baseAngle = PhaseTimer * 0.12f;
                    int ringCount = 8;
                    for (int i = 0; i < ringCount; i++) {
                        float angle = baseAngle + MathHelper.TwoPi * i / ringCount;
                        Vector2 velocity = angle.ToRotationVector2() * 8f;

                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            velocity,
                            ModContent.ProjectileType<ImmortalHaloRing>(),
                            NPC.damage / 3,
                            2f,
                            Main.myPlayer,
                            ai0: angle
                        );
                    }
                }

                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.4f }, NPC.Center);
            }

            if (PhaseTimer > 180) {
                TransitionTo(BossPhase.Phase2_Descend);
            }
        }

        #endregion

        #region 三阶段AI

        private void RunPhase3FourKingsWrath(Player target) {
            // 四天王威 - 持续追击并释放密集弹幕
            Vector2 toTarget = target.Center - NPC.Center;
            Vector2 desiredVelocity = toTarget.SafeNormalize(Vector2.Zero) * 11f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVelocity, 0.07f);

            // 高速宝塔旋转
            towerOrbitSpeed = 0.035f;

            // 密集神圣光弹
            if (PhaseTimer % 10 == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                    float spread = MathHelper.ToRadians(12);

                    for (int i = -1; i <= 1; i++) {
                        Vector2 velocity = toPlayer.RotatedBy(spread * i) * 15f;
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            velocity,
                            ModContent.ProjectileType<TreasureTowerOrb>(),
                            NPC.damage / 3,
                            2f,
                            Main.myPlayer
                        );
                    }
                }
            }

            // 宝塔同步射击
            if (PhaseTimer % 25 == 0) {
                FireAllTowerBeams(target);
            }

            if (PhaseTimer > 280) {
                int nextAction = Main.rand.Next(4);
                switch (nextAction) {
                    case 0:
                        TransitionTo(BossPhase.Phase3_TowerJudgment);
                        break;
                    case 1:
                        TransitionTo(BossPhase.Phase3_UltimateTower);
                        break;
                    case 2:
                        TransitionTo(BossPhase.Phase3_YakshaSync);
                        break;
                    case 3:
                        TransitionTo(BossPhase.Phase3_FinalRadiance);
                        break;
                }
            }
        }

        private void FireAllTowerBeams(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            for (int i = 0; i < TowerCount; i++) {
                Vector2 towerPos = GetTowerPosition(i);
                Vector2 toTarget = (target.Center - towerPos).SafeNormalize(Vector2.Zero);

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    towerPos,
                    toTarget * 11f,
                    ModContent.ProjectileType<TowerBeam>(),
                    NPC.damage / 4,
                    1f,
                    Main.myPlayer
                );
            }

            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.5f, Volume = 1.2f }, NPC.Center);
        }

        private void RunPhase3TowerJudgment(Player target) {
            switch ((int)SubState) {
                case 0: // 准备
                    NPC.velocity *= 0.92f;

                    if (PhaseTimer == 1) {
                        int pillarCount = Main.expertMode ? 12 : 8;
                        pillarPositions = new Vector2[pillarCount];
                        for (int i = 0; i < pillarCount; i++) {
                            Vector2 offset = Main.rand.NextVector2Circular(450, 120);
                            pillarPositions[i] = target.Center + offset;
                        }

                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.6f }, NPC.Center);
                    }

                    // 预警
                    if (!VaultUtils.isServer) {
                        foreach (var pos in pillarPositions) {
                            if (pos == Vector2.Zero) continue;
                            int dust = Dust.NewDust(pos + new Vector2(-25, -600), 50, 600, DustID.WhiteTorch, 0, 0, 100, default, 1f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (PhaseTimer >= 45) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 释放光柱
                    if (PhaseTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // 使用星辰弹幕模拟光柱效果
                            foreach (var pos in pillarPositions) {
                                if (pos == Vector2.Zero) continue;
                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(),
                                    new Vector2(pos.X, pos.Y - 600),
                                    new Vector2(0, 25f),
                                    ModContent.ProjectileType<VaisravanaStar>(),
                                    NPC.damage / 2,
                                    5f,
                                    Main.myPlayer
                                );
                            }
                        }

                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.3f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(16, 35);
                    }

                    if (PhaseTimer > 50) {
                        TransitionTo(BossPhase.Phase3_FourKingsWrath);
                    }
                    break;
            }
        }

        private void RunPhase3UltimateTower(Player target) {
            switch ((int)SubState) {
                case 0: // 蓄力
                    NPC.velocity *= 0.85f;

                    if (PhaseTimer == 1) {
                        laserAngle = (target.Center - NPC.Center).ToRotation();
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
                    }

                    // 巨大能量聚集效果
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 14; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(220, 220);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 50, default, 2.8f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 14f;
                        }

                        for (int i = 0; i < 6; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(35, 35);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 100, default, 3.2f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (PhaseTimer % 8 == 0) {
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(PhaseTimer / 8f, 8);
                    }

                    if (PhaseTimer >= 70) {
                        SubState = 1;
                        PhaseTimer = 0;

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                Vector2.Zero,
                                ModContent.ProjectileType<TreasureTowerRay>(),
                                (int)(NPC.damage * 1.5f),
                                0f,
                                Main.myPlayer,
                                ai0: NPC.whoAmI,
                                ai1: laserAngle
                            );
                        }

                        SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.2f, Volume = 2f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(25, 100);
                    }
                    break;

                case 1: // 激光持续
                    NPC.velocity *= 0.9f;

                    // 追踪玩家
                    float targetAngle = (target.Center - NPC.Center).ToRotation();
                    laserAngle = MathHelper.Lerp(laserAngle, targetAngle, 0.018f);

                    if (PhaseTimer > 130) {
                        TransitionTo(BossPhase.Phase3_FourKingsWrath);
                    }
                    break;
            }
        }

        private void RunPhase3YakshaSync(Player target) {
            switch ((int)SubState) {
                case 0: // 确保有仆从
                    if (!hasSpawnedMinions || !AnyMinionsAlive()) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int minionCount = Main.expertMode ? 4 : 3;
                            yakshaMinionIds = new int[minionCount];

                            for (int i = 0; i < minionCount; i++) {
                                float angle = MathHelper.TwoPi * i / minionCount;
                                Vector2 spawnPos = NPC.Center + angle.ToRotationVector2() * 160f;

                                int npcId = NPC.NewNPC(
                                    NPC.GetSource_FromAI(),
                                    (int)spawnPos.X,
                                    (int)spawnPos.Y,
                                    ModContent.NPCType<YakshaMinion>(),
                                    ai0: NPC.whoAmI,
                                    ai1: i
                                );
                                yakshaMinionIds[i] = npcId;
                            }
                            hasSpawnedMinions = true;
                        }
                    }

                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: // 准备同步攻击
                    NPC.velocity *= 0.9f;

                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.4f, Volume = 1.2f }, NPC.Center);

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            foreach (int minionId in yakshaMinionIds) {
                                if (minionId >= 0 && minionId < Main.maxNPCs && Main.npc[minionId].active) {
                                    Main.npc[minionId].ai[2] = 1;
                                }
                            }
                        }
                    }

                    // 预警效果
                    if (!VaultUtils.isServer) {
                        foreach (int minionId in yakshaMinionIds) {
                            if (minionId >= 0 && minionId < Main.maxNPCs && Main.npc[minionId].active) {
                                Vector2 minionPos = Main.npc[minionId].Center;
                                Vector2 toTarget = (target.Center - minionPos).SafeNormalize(Vector2.Zero);
                                for (int i = 0; i < 6; i++) {
                                    Vector2 dustPos = minionPos + toTarget * (i * 100);
                                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 150, default, 0.9f);
                                    Main.dust[dust].noGravity = true;
                                }
                            }
                        }
                    }

                    if (PhaseTimer >= 55) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2: // 同步发射
                    if (PhaseTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            foreach (int minionId in yakshaMinionIds) {
                                if (minionId >= 0 && minionId < Main.maxNPCs && Main.npc[minionId].active) {
                                    NPC minion = Main.npc[minionId];
                                    Vector2 toTarget = (target.Center - minion.Center).SafeNormalize(Vector2.Zero);

                                    Projectile.NewProjectile(
                                        NPC.GetSource_FromAI(),
                                        minion.Center,
                                        toTarget * 16f,
                                        ModContent.ProjectileType<TowerBeam>(),
                                        NPC.damage / 2,
                                        2f,
                                        Main.myPlayer
                                    );
                                }
                            }

                            // Boss也发射
                            Vector2 bossToTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                bossToTarget * 16f,
                                ModContent.ProjectileType<TowerBeam>(),
                                NPC.damage / 2,
                                2f,
                                Main.myPlayer
                            );
                        }

                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.5f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(14, 28);
                    }

                    if (PhaseTimer > 55) {
                        TransitionTo(BossPhase.Phase3_FourKingsWrath);
                    }
                    break;
            }
        }

        private void RunPhase3FinalRadiance(Player target) {
            switch ((int)SubState) {
                case 0: // 蓄力
                    NPC.velocity *= 0.9f;

                    // 能量聚集
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < 12; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(320, 320);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 50, default, 2.8f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 16f;
                        }
                    }

                    if (PhaseTimer == 28) {
                        SoundEngine.PlaySound(SoundID.Item119 with { Pitch = -0.2f }, NPC.Center);
                    }

                    if (PhaseTimer >= 55) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 释放
                    if (PhaseTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // 巨大的环形弹幕爆发
                            int waves = 4;
                            for (int w = 0; w < waves; w++) {
                                int count = 18;
                                float baseAngle = w * MathHelper.ToRadians(12);
                                for (int i = 0; i < count; i++) {
                                    float angle = baseAngle + MathHelper.TwoPi * i / count;
                                    Vector2 velocity = angle.ToRotationVector2() * (9f + w * 2.5f);

                                    Projectile.NewProjectile(
                                        NPC.GetSource_FromAI(),
                                        NPC.Center,
                                        velocity,
                                        ModContent.ProjectileType<TreasureTowerOrb>(),
                                        NPC.damage / 3,
                                        2f,
                                        Main.myPlayer
                                    );
                                }
                            }

                            // 同时释放仙气波
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                Vector2.Zero,
                                ModContent.ProjectileType<ImmortalWave>(),
                                NPC.damage / 3,
                                0f,
                                Main.myPlayer
                            );
                        }

                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.6f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(22, 40);
                    }

                    if (PhaseTimer > 70) {
                        TransitionTo(BossPhase.Phase3_FourKingsWrath);
                    }
                    break;
            }
        }

        private bool AnyMinionsAlive() {
            if (yakshaMinionIds == null) return false;
            foreach (int id in yakshaMinionIds) {
                if (id >= 0 && id < Main.maxNPCs && Main.npc[id].active &&
                    Main.npc[id].type == ModContent.NPCType<YakshaMinion>()) {
                    return true;
                }
            }
            return false;
        }

        #endregion
    }
}
