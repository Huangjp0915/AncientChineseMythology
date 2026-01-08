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
        #region AI主循环

        public override void AI() {
            random ??= new Random(seed);
            globalTime += 1f / 60f;
            tailSwayPhase += 0.05f;

            // 检测目标
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    // 没有有效目标，飞向天空离开
                    NPC.velocity.Y -= 0.8f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            // 检查阶段转换
            CheckPhaseTransition();

            // 更新视觉效果
            UpdateVisualEffects();

            PhaseTimer++;
            AttackTimer++;

            // 根据当前阶段执行AI
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
                case BossPhase.Phase1_WaterBarrage:
                    RunPhase1WaterBarrage(target);
                    break;
                case BossPhase.Phase1_VortexSummon:
                    RunPhase1VortexSummon(target);
                    break;
                case BossPhase.Phase1_TidalWave:
                    RunPhase1TidalWave(target);
                    break;
                case BossPhase.Phase1_BubbleStorm:
                    RunPhase1BubbleStorm(target);
                    break;
                case BossPhase.Phase1_CoralSpike:
                    RunPhase1CoralSpike(target);
                    break;
                // 阶段转换
                case BossPhase.PhaseTransition_2:
                    RunPhaseTransition2(target);
                    break;
                // 二阶段
                case BossPhase.Phase2_Charge:
                    RunPhase2Charge(target);
                    break;
                case BossPhase.Phase2_SummonMinions:
                    RunPhase2SummonMinions(target);
                    break;
                case BossPhase.Phase2_Whirlpool:
                    RunPhase2Whirlpool(target);
                    break;
                case BossPhase.Phase2_DragonBreath:
                    RunPhase2DragonBreath(target);
                    break;
                case BossPhase.Phase2_TornadoRush:
                    RunPhase2TornadoRush(target);
                    break;
                case BossPhase.Phase2_TsunamiWall:
                    RunPhase2TsunamiWall(target);
                    break;
                case BossPhase.Phase2_DragonClaw:
                    RunPhase2DragonClaw(target);
                    break;
                // 阶段转换
                case BossPhase.PhaseTransition_3:
                    RunPhaseTransition3(target);
                    break;
                // 三阶段
                case BossPhase.Phase3_FuryCharge:
                    RunPhase3FuryCharge(target);
                    break;
                case BossPhase.Phase3_TridentStorm:
                    RunPhase3TridentStorm(target);
                    break;
                case BossPhase.Phase3_TidalBeam:
                    RunPhase3TidalBeam(target);
                    break;
                case BossPhase.Phase3_DragonCoil:
                    RunPhase3DragonCoil(target);
                    break;
                case BossPhase.Phase3_FinalTsunami:
                    RunPhase3FinalTsunami(target);
                    break;
                case BossPhase.Phase3_SeaDragonDance:
                    RunPhase3SeaDragonDance(target);
                    break;
                case BossPhase.Phase3_AbyssalVortex:
                    RunPhase3AbyssalVortex(target);
                    break;
            }

            // 更新朝向
            UpdateRotation();

            // 水光照明 - 青蓝色
            Lighting.AddLight(NPC.Center, new Vector3(0.3f, 0.7f, 0.9f) * glowIntensity);
        }

        private void UpdateRotation() {
            if (NPC.velocity.LengthSquared() > 1f) {
                float targetRot = NPC.velocity.ToRotation();
                NPC.rotation = MathHelper.Lerp(NPC.rotation, targetRot, 0.1f);
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            }
        }

        private void UpdateVisualEffects() {
            // 水波旋转
            waveRotation += 0.008f;

            // 根据阶段调整效果
            if (IsPhase3) {
                waveScale = 1.5f + MathF.Sin(globalTime * 3.5f) * 0.2f;
                glowIntensity = 1.6f;
                waterAuraAlpha = MathHelper.Lerp(waterAuraAlpha, 0.85f, 0.04f);
            }
            else if (IsPhase2) {
                waveScale = 1.25f + MathF.Sin(globalTime * 2.5f) * 0.12f;
                glowIntensity = 1.3f;
                waterAuraAlpha = MathHelper.Lerp(waterAuraAlpha, 0.55f, 0.04f);
            }
            else {
                waveScale = 1f + MathF.Sin(globalTime * 1.8f) * 0.06f;
                glowIntensity = 1f;
                waterAuraAlpha = MathHelper.Lerp(waterAuraAlpha, 0.35f, 0.04f);
            }
        }

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 && !IsPhase3 &&
                Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.PhaseTransition_2);
                didPhase2Transition = true;
            }

            if (!didPhase3Transition && IsPhase3 &&
                Phase != BossPhase.PhaseTransition_3 && Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.PhaseTransition_3);
                didPhase3Transition = true;
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
            return (BossPhase)(Main.rand.Next(5) switch {
                0 => (int)BossPhase.Phase1_WaterBarrage,
                1 => (int)BossPhase.Phase1_VortexSummon,
                2 => (int)BossPhase.Phase1_TidalWave,
                3 => (int)BossPhase.Phase1_BubbleStorm,
                _ => (int)BossPhase.Phase1_CoralSpike
            });
        }

        private BossPhase GetRandomPhase2Attack() {
            return (BossPhase)(Main.rand.Next(7) switch {
                0 => (int)BossPhase.Phase2_Charge,
                1 => (int)BossPhase.Phase2_SummonMinions,
                2 => (int)BossPhase.Phase2_Whirlpool,
                3 => (int)BossPhase.Phase2_DragonBreath,
                4 => (int)BossPhase.Phase2_TornadoRush,
                5 => (int)BossPhase.Phase2_TsunamiWall,
                _ => (int)BossPhase.Phase2_DragonClaw
            });
        }

        private BossPhase GetRandomPhase3Attack() {
            return (BossPhase)(Main.rand.Next(7) switch {
                0 => (int)BossPhase.Phase3_FuryCharge,
                1 => (int)BossPhase.Phase3_TridentStorm,
                2 => (int)BossPhase.Phase3_TidalBeam,
                3 => (int)BossPhase.Phase3_DragonCoil,
                4 => (int)BossPhase.Phase3_FinalTsunami,
                5 => (int)BossPhase.Phase3_SeaDragonDance,
                _ => (int)BossPhase.Phase3_AbyssalVortex
            });
        }

        #endregion

        #region 出场演出

        private void RunIntro(Player target) {
            introProgress = MathHelper.Clamp(PhaseTimer / 180f, 0f, 1f);

            // 从水中升起的效果
            Vector2 introOffset = new Vector2(0, 400) * (1f - ACMUtils.SineInOut(introProgress));
            Vector2 desiredPos = target.Center + new Vector2(0, -300) + introOffset;

            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, 0.03f);
            NPC.velocity *= 0.9f;

            // 水花粒子效果
            if (!VaultUtils.isServer && PhaseTimer % 2 == 0) {
                for (int i = 0; i < 5; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(180, 180) * (1f - introProgress);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Water, 0, -2f, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                }

                // 气泡粒子
                for (int i = 0; i < 2; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(100, 100);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Wet, 0, -1.5f, 150, default, 1.3f);
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
            }

            if (PhaseTimer > 180) {
                TransitionTo(BossPhase.Intro_SummonBarriers);
            }
        }

        /// <summary>
        /// 开场召唤两侧封路龙卷
        /// </summary>
        private void RunIntroSummonBarriers(Player target) {
            NPC.velocity *= 0.9f;

            // 保持在玩家上方
            Vector2 hoverPos = target.Center + new Vector2(0, -350);
            NPC.velocity += (hoverPos - NPC.Center) * 0.003f;

            // 召唤两侧巨型水龙卷作为战场边界
            if (PhaseTimer == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                hasSpawnedBarriers = true;
                barrierTornadoIds = new int[2];

                // 左侧龙卷
                int leftTornado = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    target.Center + new Vector2(-800, 0),
                    Vector2.Zero,
                    ModContent.ProjectileType<BarrierWaterTornado>(),
                    NPC.damage / 4,
                    0f,
                    ai0: NPC.whoAmI,
                    ai1: -1 // 左侧
                );
                barrierTornadoIds[0] = leftTornado;

                // 右侧龙卷
                int rightTornado = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    target.Center + new Vector2(800, 0),
                    Vector2.Zero,
                    ModContent.ProjectileType<BarrierWaterTornado>(),
                    NPC.damage / 4,
                    0f,
                    ai0: NPC.whoAmI,
                    ai1: 1 // 右侧
                );
                barrierTornadoIds[1] = rightTornado;

                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(20, 60);
            }

            // 龙王威压粒子
            if (!VaultUtils.isServer && PhaseTimer > 30) {
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi * i / 8 + PhaseTimer * 0.03f;
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * (120 + MathF.Sin(PhaseTimer * 0.1f) * 30);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 150, default, 2f);
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

            // 保持在玩家上方
            Vector2 hoverPos = target.Center + new Vector2(0, -350);
            NPC.velocity += (hoverPos - NPC.Center) * 0.002f;

            // 阶段转换特效 - 水龙卷
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 12; i++) {
                    float angle = MathHelper.TwoPi * i / 12 + PhaseTimer * 0.05f;
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * (100 + PhaseTimer);
                    int dustType = Main.rand.NextBool() ? DustID.Water : DustID.Wet;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 8f;
                }
            }

            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f, Volume = 1.5f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(20, 60);

                // 爆发水花
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 50; i++) {
                        float angle = MathHelper.TwoPi * i / 50;
                        Vector2 vel = angle.ToRotationVector2() * 10f;
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Water, vel.X, vel.Y, 100, default, 3f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (PhaseTimer > 100) {
                NPC.dontTakeDamage = false;
                TransitionTo(BossPhase.Phase2_Charge);
            }
        }

        private void RunPhaseTransition3(Player target) {
            NPC.velocity *= 0.9f;
            NPC.dontTakeDamage = true;

            // 保持位置
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            NPC.velocity += (hoverPos - NPC.Center) * 0.003f;

            // 更强烈的水龙卷效果
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 20; i++) {
                    float angle = MathHelper.TwoPi * i / 20 + PhaseTimer * 0.08f;
                    float radius = 80 + PhaseTimer * 1.5f;
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * radius;
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Water,
                        1 => DustID.Wet,
                        _ => DustID.BlueTorch
                    };
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 120, default, 3f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 12f;
                }
            }

            if (PhaseTimer == 70) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 2f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(25, 80);

                // 爆发海啸
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 80; i++) {
                        float angle = MathHelper.TwoPi * i / 80;
                        Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(8, 15);
                        int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                        int dust = Dust.NewDust(NPC.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 3.5f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (PhaseTimer > 120) {
                NPC.dontTakeDamage = false;
                TransitionTo(BossPhase.Phase3_FuryCharge);
            }
        }

        #endregion
    }
}
