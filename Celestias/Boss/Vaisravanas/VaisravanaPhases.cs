using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas
{
    /// <summary>
    /// 毗沙门天王 - 阶段枚举 + 一阶段（宝塔威光）+ 阶段转换
    /// </summary>
    internal partial class Vaisravana
    {
        #region 阶段枚举

        public enum BossPhase
        {
            Intro,                      // 出场演出

            // 一阶段 · 宝塔威光：宝塔充能驱动的控场弹幕，教学赐福窃取
            Phase1_Hub,                 // 悬浮枢纽，固定可读轮替
            Phase1_TowerVolley,         // 宝塔齐射（被赐福→延迟安全光束）
            Phase1_SweepingLight,       // 扫射光芒（预告）
            Phase1_StarRain,            // 星辰雨（落点预告）

            PhaseTransition_2,          // 一→二 i 帧转换

            // 二阶段 · 天王降临：四方夜叉锚点 + 地形仙气波 + 守护反击
            Phase2_Hub,                 // 降临枢纽
            Phase2_YakshaSummon,        // 召唤四方夜叉
            Phase2_QuadrantRay,         // 四象射线（夜叉锚定安全道）
            Phase2_ImmortalWave,        // 仙气地波（随地形起伏，迫使纵向走位）
            Phase2_GuardianStance,      // 守护姿态（绝对防御式反击窗口）

            PhaseTransition_3,          // 二→三 i 帧转换

            // 三阶段 · 库藏封印：脚本化 A/B/C 三幕轮替
            Phase3_SealRings,           // A 金环收束（标记安全道）
            Phase3_YakshaMirror,        // B 夜叉镜射（仅反射角可躲）
            Phase3_UltimateTower,       // C 终极宝塔（70 tick 蓄力 + 地纹预告）
            Phase3_SealBeat             // 幕间守护节拍
        }

        #endregion

        #region 一阶段轮替状态

        private int p1Index;        // 一阶段攻击轮替索引
        private bool volleyBlessed; // 本次齐射是否被赐福（安全变体）

        #endregion

        #region 一阶段AI

        private void RunPhase1Hub(Player target) {
            // 宝塔威光 - 神圣悬浮，保持在玩家上方
            Vector2 hoverPos = target.Center + new Vector2(0, -380);
            hoverPos.X += MathF.Sin(globalTime * 1.0f) * 60f;
            hoverPos.Y += MathF.Sin(globalTime * 1.5f) * 20f;

            Vector2 toHover = hoverPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.022f, 0.08f);

            towerOrbitSpeed = 0.012f;

            // 枢纽期间从带充能的宝塔点射，提供可读的低压力压制（不喷射）
            if (AttackTimer % 46 == 0) {
                FireTowerTap(target);
            }

            // 固定可读轮替：齐射 → 扫射 → 星雨（非随机喷射）
            if (PhaseTimer > 150) {
                BossPhase[] rotation = {
                    BossPhase.Phase1_TowerVolley,
                    BossPhase.Phase1_SweepingLight,
                    BossPhase.Phase1_TowerVolley,
                    BossPhase.Phase1_StarRain
                };
                BossPhase next = rotation[p1Index % rotation.Length];
                p1Index++;
                TransitionTo(next);
            }
        }

        /// <summary>枢纽点射：从一座带充能的宝塔射出单发可读慢弹（消耗该塔一点充能）。</summary>
        private void FireTowerTap(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int towerIndex = -1;
            for (int i = 0; i < TowerCount; i++) {
                if (towerCharges[i] > 0) { towerIndex = i; break; }
            }
            if (towerIndex < 0) return; // 充能被偷光 → 不再点射（奖励玩家窃取）

            ConsumeTowerCharge(towerIndex);
            Vector2 towerPos = GetTowerPosition(towerIndex);
            Vector2 toTarget = (target.Center - towerPos).SafeNormalize(Vector2.UnitY);

            Projectile.NewProjectile(NPC.GetSource_FromAI(), towerPos, toTarget * 8f,
                ModContent.ProjectileType<TowerBeam>(), NPC.damage / 3, 1f, Main.myPlayer);

            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.4f }, towerPos);
        }

        /// <summary>
        /// 宝塔齐射：核心赐福机制示范。
        /// 未赐福 → 瞬发，每座带充能的宝塔向玩家喷出扇形光弹（消耗充能）。
        /// 被赐福 → 转化为长预告的延迟安全光束墙，在玩家定身处留出明显安全缝。
        /// </summary>
        private void RunPhase1TowerVolley(Player target) {
            switch ((int)SubState) {
                case 0: { // 蓄力 / 预告
                    NPC.velocity *= 0.9f;
                    Vector2 hoverPos = target.Center + new Vector2(0, -360);
                    NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.03f);

                    if (PhaseTimer == 1) {
                        volleyBlessed = ConsumeBlessing();
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = volleyBlessed ? 0.1f : 0.6f }, NPC.Center);
                    }

                    // 预告：从带充能宝塔向玩家拉出标线
                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < TowerCount; i++) {
                            if (towerCharges[i] <= 0) continue;
                            Vector2 from = GetTowerPosition(i);
                            Vector2 dir = (target.Center - from).SafeNormalize(Vector2.UnitY);
                            TelegraphLine(from, dir, 6, volleyBlessed ? DustID.GoldFlame : DustID.WhiteTorch);
                        }
                    }

                    int windup = volleyBlessed ? 55 : 26;
                    if (PhaseTimer >= windup) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;
                }

                case 1: { // 释放
                    NPC.velocity *= 0.95f;

                    if (PhaseTimer == 1) {
                        if (volleyBlessed) FireBlessedSafeBeam(target);
                        else FireTowerOrbFans(target);
                    }

                    if (PhaseTimer > (volleyBlessed ? 70 : 55)) {
                        TransitionTo(BossPhase.Phase1_Hub);
                    }
                    break;
                }
            }
        }

        /// <summary>未赐福变体：每座带充能宝塔瞬发扇形光弹（消耗充能）。</summary>
        private void FireTowerOrbFans(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int fired = 0;
            for (int i = 0; i < TowerCount; i++) {
                if (towerCharges[i] <= 0) continue;
                ConsumeTowerCharge(i);
                fired++;

                Vector2 from = GetTowerPosition(i);
                Vector2 toTarget = (target.Center - from).SafeNormalize(Vector2.UnitY);
                float baseAngle = toTarget.ToRotation();
                int orbCount = Main.expertMode ? 5 : 3;
                float spread = MathHelper.ToRadians(28);
                for (int j = 0; j < orbCount; j++) {
                    float angle = baseAngle + spread * (j - (orbCount - 1) / 2f) / (orbCount - 1);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), from, angle.ToRotationVector2() * 11f,
                        ModContent.ProjectileType<TreasureTowerOrb>(), NPC.damage / 3, 2f, Main.myPlayer);
                }
            }

            if (fired == 0) {
                // 充能被偷光：完全无害（奖励满额窃取）
                if (Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.6f }, NPC.Center);
            }
            else {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.2f }, NPC.Center);
            }
        }

        /// <summary>被赐福变体：延迟安全光束墙，在玩家方向留安全缝。</summary>
        private void FireBlessedSafeBeam(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // 在 360° 上发慢速光弹环，但留出朝向玩家当前位置的安全扇区
            float safeAngle = (target.Center - NPC.Center).ToRotation();
            int count = 24;
            float safeHalfWidth = MathHelper.ToRadians(34);
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                float delta = MathHelper.WrapAngle(angle - safeAngle);
                if (MathF.Abs(delta) < safeHalfWidth) continue; // 安全缝
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, angle.ToRotationVector2() * 6.5f,
                    ModContent.ProjectileType<TreasureTowerOrb>(), NPC.damage / 4, 1f, Main.myPlayer);
            }

            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f, Volume = 0.9f }, NPC.Center);
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

                    if (!VaultUtils.isServer) {
                        float sweepAngle = MathHelper.PiOver4 * laserSweepDirection;
                        TelegraphLine(NPC.Center, sweepAngle.ToRotationVector2(), 10, DustID.WhiteTorch);
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

                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                                currentAngle.ToRotationVector2() * 20f,
                                ModContent.ProjectileType<SweepingLightBolt>(), NPC.damage / 2, 2f, Main.myPlayer);
                        }
                        SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.6f }, NPC.Center);
                    }

                    if (PhaseTimer > 90) {
                        TransitionTo(BossPhase.Phase1_Hub);
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

                    if (!VaultUtils.isServer) {
                        for (int i = 0; i < starCount; i++) {
                            if (starPositions[i] == Vector2.Zero) continue;
                            float alpha = PhaseTimer / 50f;
                            int dust = Dust.NewDust(starPositions[i], 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.8f * alpha);
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
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), starPositions[i], toTarget * 14f,
                                    ModContent.ProjectileType<VaisravanaStar>(), NPC.damage / 2, 3f, Main.myPlayer);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item92 with { Pitch = 0.2f }, NPC.Center);
                    }

                    if (PhaseTimer > 70) {
                        TransitionTo(BossPhase.Phase1_Hub);
                    }
                    break;
            }
        }

        /// <summary>沿方向画一条点状预告线。</summary>
        private void TelegraphLine(Vector2 from, Vector2 dir, int segments, int dustType) {
            for (int i = 0; i < segments; i++) {
                Vector2 pos = from + dir * (i * 80f);
                int dust = Dust.NewDust(pos, 0, 0, dustType, 0, 0, 150, default, 0.9f);
                Main.dust[dust].noGravity = true;
            }
        }

        #endregion

        #region 阶段转换

        private void RunPhaseTransition2(Player target) {
            NPC.velocity *= 0.95f;
            NPC.dontTakeDamage = true; // i 帧转换节拍

            // 宝塔加速旋转并重置为满充能
            towerOrbitSpeed = 0.04f + PhaseTimer * 0.0008f;
            if (PhaseTimer == 1) {
                for (int i = 0; i < TowerCount; i++) towerCharges[i] = MaxTowerCharge;
                pendingBlessing = 0;
            }

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 10; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(220, 220);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 50, default, 2.2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 10f;
                }
            }

            if (PhaseTimer == 50) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f }, NPC.Center);
                if (!VaultUtils.isServer) {
                    ACMScreenShakeSystem.Add(12f);
                    VaisravanaTreasureScreenSystem.PulseBloom(0.55f);
                }
            }

            if (PhaseTimer > 90) {
                towerOrbitSpeed = 0.02f;
                TransitionTo(BossPhase.Phase2_YakshaSummon);
            }
        }

        private void RunPhaseTransition3(Player target) {
            NPC.velocity *= 0.93f;
            NPC.dontTakeDamage = true; // i 帧转换节拍

            towerOrbitSpeed = 0.07f + PhaseTimer * 0.0015f;
            if (PhaseTimer == 1) {
                for (int i = 0; i < TowerCount; i++) towerCharges[i] = MaxTowerCharge;
                pendingBlessing = 0;
                sealCycle = 0;
            }

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 15; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(280, 280);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 50, default, 2.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 14f;
                }
            }

            if (PhaseTimer == 35) {
                SoundEngine.PlaySound(SoundID.Item119 with { Volume = 1.5f }, NPC.Center);
            }

            if (PhaseTimer == 70) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0f }, NPC.Center);
                if (!VaultUtils.isServer) {
                    ACMScreenShakeSystem.Add(12f);
                    VaisravanaTreasureScreenSystem.PulseBloom(0.8f);
                }
            }

            if (PhaseTimer > 110) {
                towerOrbitSpeed = 0.03f;
                TransitionTo(BossPhase.Phase3_SealRings);
            }
        }

        #endregion
    }
}
