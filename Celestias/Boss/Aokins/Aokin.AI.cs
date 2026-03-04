using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    internal partial class Aokin
    {
        #region AI主循环

        public override void AI() {
            random ??= new Random(seed);
            globalTime += 1f / 60f;

            if (divebombCooldown > 0)
                divebombCooldown--;

            // 激活天空背景
            if (!VaultUtils.isServer && AokinSky.name != null) {
                if (!SkyManager.Instance[AokinSky.name].IsActive())
                    SkyManager.Instance.Activate(AokinSky.name, NPC.Center);
            }

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    // 关闭天空背景
                    if (!VaultUtils.isServer && AokinSky.name != null) {
                        SkyManager.Instance.Deactivate(AokinSky.name);
                    }
                    NPC.velocity.Y -= 0.8f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            // 阶段转换检测
            CheckPhaseTransition();

            // 更新视觉效果
            UpdateVisualEffects();

            // 更新蛇形身体
            UpdateSegments();

            // 身体段碰撞伤害
            ApplySegmentContactDamage(target);

            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro:
                    RunIntro(target);
                    break;
                case BossPhase.Intro_SummonBarriers:
                    RunIntroSummonBarriers(target);
                    break;
                // 一阶段
                case BossPhase.Phase1_Patrol:
                    RunPhase1Patrol(target);
                    break;
                case BossPhase.Phase1_FireBarrage:
                    RunPhase1FireBarrage(target);
                    break;
                case BossPhase.Phase1_DragonBreath:
                    RunPhase1DragonBreath(target);
                    break;
                case BossPhase.Phase1_TailWhip:
                    RunPhase1TailWhip(target);
                    break;
                case BossPhase.Phase1_MeteorRain:
                    RunPhase1MeteorRain(target);
                    break;
                // 阶段转换
                case BossPhase.PhaseTransition_2:
                    RunPhaseTransition2(target);
                    break;
                // 二阶段
                case BossPhase.Phase2_FuryCharge:
                    RunPhase2FuryCharge(target);
                    break;
                case BossPhase.Phase2_FlameVortex:
                    RunPhase2FlameVortex(target);
                    break;
                case BossPhase.Phase2_InfernoBreath:
                    RunPhase2InfernoBreath(target);
                    break;
                case BossPhase.Phase2_MeteorStorm:
                    RunPhase2MeteorStorm(target);
                    break;
                case BossPhase.Phase2_Divebomb:
                    RunPhase2Divebomb(target);
                    break;
                case BossPhase.Phase2_SurpriseFireball:
                    RunPhase2SurpriseFireball(target);
                    break;
            }

            // 更新朝向
            UpdateRotation();

            // 火焰光照
            Lighting.AddLight(NPC.Center, new Vector3(0.9f, 0.4f, 0.2f) * glowIntensity);
        }

        #endregion

        #region 蛇形身体更新

        private void UpdateSegments() {
            if (Main.gamePaused) return;

            int gap = (int)(SegmentGap * NPC.scale);

            for (int i = 0; i < SegmentCount; i++) {
                Vector2 previousSegment;
                float previousRot;
                if (i == 0) {
                    previousSegment = NPC.Center;
                    previousRot = NPC.rotation;
                }
                else {
                    previousSegment = segmentPos[i - 1];
                    previousRot = segmentRot[i - 1];
                }

                Vector2 targetPos = previousSegment - previousRot.ToRotationVector2() * gap;
                segmentPos[i] += (targetPos - segmentPos[i]) * 0.3f;

                // 保持固定距离
                Vector2 diff = previousSegment - segmentPos[i];
                if (diff.LengthSquared() > 0.01f) {
                    segmentPos[i] = previousSegment - diff.SafeNormalize(Vector2.Zero) * gap;
                }

                segmentRot[i] = (previousSegment - segmentPos[i]).ToRotation();
            }
        }

        private void ApplySegmentContactDamage(Player target) {
            if (Main.netMode == NetmodeID.Server) return;

            Rectangle playerBox = new Rectangle(
                (int)target.position.X, (int)target.position.Y,
                target.width, target.height);

            for (int i = 0; i < SegmentCount; i++) {
                Rectangle segBox = new Rectangle(
                    (int)segmentPos[i].X - 20, (int)segmentPos[i].Y - 20, 40, 40);

                if (playerBox.Intersects(segBox)) {
                    int direction = NPC.velocity.X > 0 ? 1 : -1;
                    target.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), NPC.damage / 2, direction);
                    break;
                }
            }
        }

        #endregion

        #region 辅助方法

        private void UpdateRotation() {
            if (NPC.velocity.LengthSquared() > 1f) {
                float targetRot = NPC.velocity.ToRotation();
                NPC.rotation = MathHelper.Lerp(NPC.rotation, targetRot, 0.1f);
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            }
        }

        private void UpdateVisualEffects() {
            flameRotation += 0.01f;

            if (IsPhase2) {
                flameScale = 1.4f + MathF.Sin(globalTime * 3f) * 0.15f;
                glowIntensity = 1.5f;
                flameAuraAlpha = MathHelper.Lerp(flameAuraAlpha, 0.75f, 0.04f);
                tailTurnSpeed = 18f;
            }
            else {
                flameScale = 1f + MathF.Sin(globalTime * 2f) * 0.08f;
                glowIntensity = 1f;
                flameAuraAlpha = MathHelper.Lerp(flameAuraAlpha, 0.35f, 0.04f);
                tailTurnSpeed = 12f;
            }
        }

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 &&
                Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.PhaseTransition_2);
                didPhase2Transition = true;
            }
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            NPC.netUpdate = true;
        }

        private BossPhase GetRandomPhase1Attack() {
            return (BossPhase)(Main.rand.Next(4) switch {
                0 => (int)BossPhase.Phase1_FireBarrage,
                1 => (int)BossPhase.Phase1_DragonBreath,
                2 => (int)BossPhase.Phase1_TailWhip,
                _ => (int)BossPhase.Phase1_MeteorRain
            });
        }

        private BossPhase GetRandomPhase2Attack() {
            // 俯冲需要冷却
            if (divebombCooldown <= 0 && Main.rand.NextBool(5))
                return BossPhase.Phase2_Divebomb;

            return (BossPhase)(Main.rand.Next(5) switch {
                0 => (int)BossPhase.Phase2_FuryCharge,
                1 => (int)BossPhase.Phase2_FlameVortex,
                2 => (int)BossPhase.Phase2_InfernoBreath,
                3 => (int)BossPhase.Phase2_MeteorStorm,
                _ => (int)BossPhase.Phase2_SurpriseFireball
            });
        }

        #endregion

        #region 出场演出

        private void RunIntro(Player target) {
            introProgress = MathHelper.Clamp(PhaseTimer / 180f, 0f, 1f);

            // 从天空降下
            Vector2 introOffset = new Vector2(0, 400) * (1f - ACMUtils.SineInOut(introProgress));
            Vector2 desiredPos = target.Center + new Vector2(0, -300) + introOffset;

            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, 0.03f);
            NPC.velocity *= 0.9f;

            // 火焰粒子效果
            if (!VaultUtils.isServer && PhaseTimer % 2 == 0) {
                for (int i = 0; i < 5; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(180, 180) * (1f - introProgress);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Torch, 0, -2f, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                }

                for (int i = 0; i < 2; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(100, 100);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.SolarFlare, 0, -1.5f, 150, default, 1.3f);
                    Main.dust[dust].noGravity = true;
                }
            }

            // 龙吼音效
            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.Zombie20 with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
            }

            if (PhaseTimer == 120) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(15, 50);

                // 火焰爆发
                AokinHelper.CreateDragonFireBurst(NPC.Center, 200f, 3, 16);
            }

            if (PhaseTimer > 180) {
                TransitionTo(BossPhase.Intro_SummonBarriers);
            }
        }

        /// <summary>
        /// 出场后召唤两侧火龙卷封路
        /// </summary>
        private void RunIntroSummonBarriers(Player target) {
            NPC.velocity *= 0.9f;

            Vector2 hoverPos = target.Center + new Vector2(0, -350);
            NPC.velocity += (hoverPos - NPC.Center) * 0.003f;

            // 召唤两侧火焰龙卷
            if (PhaseTimer == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                hasSpawnedBarriers = true;
                barrierTornadoIds = new int[2];

                // 左侧龙卷
                int leftTornado = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    target.Center + new Vector2(-800, 0),
                    Vector2.Zero,
                    ModContent.ProjectileType<AokinBarrierTornado>(),
                    NPC.damage / 4,
                    0f,
                    ai0: NPC.whoAmI,
                    ai1: -1
                );
                barrierTornadoIds[0] = leftTornado;

                // 右侧龙卷
                int rightTornado = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    target.Center + new Vector2(800, 0),
                    Vector2.Zero,
                    ModContent.ProjectileType<AokinBarrierTornado>(),
                    NPC.damage / 4,
                    0f,
                    ai0: NPC.whoAmI,
                    ai1: 1
                );
                barrierTornadoIds[1] = rightTornado;

                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(15, 40);
            }

            // 火焰压迫粒子
            if (!VaultUtils.isServer && PhaseTimer > 30) {
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi * i / 8 + PhaseTimer * 0.03f;
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * (120 + MathF.Sin(PhaseTimer * 0.1f) * 30);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Torch, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 4f;
                }
            }

            if (PhaseTimer > 90) {
                TransitionTo(BossPhase.Phase1_Patrol);
            }
        }

        #endregion

        #region 阶段转换

        private void RunPhaseTransition2(Player target) {
            NPC.velocity *= 0.92f;
            NPC.dontTakeDamage = true;

            Vector2 hoverPos = target.Center + new Vector2(0, -350);
            NPC.velocity += (hoverPos - NPC.Center) * 0.002f;

            // 火焰旋涡特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 12; i++) {
                    float angle = MathHelper.TwoPi * i / 12 + PhaseTimer * 0.05f;
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * (100 + PhaseTimer);
                    int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 8f;
                }
            }

            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f, Volume = 1.5f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(20, 60);
                AokinHelper.CreateFlameVortex(NPC.Center, 300f, 2f, 60);
            }

            if (PhaseTimer > 120) {
                NPC.dontTakeDamage = false;
                TransitionTo(GetRandomPhase2Attack());
            }
        }

        #endregion
    }
}
