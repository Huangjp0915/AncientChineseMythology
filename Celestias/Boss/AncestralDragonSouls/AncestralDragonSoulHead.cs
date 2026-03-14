using AncientChineseMythology.Celestias.Boss.AncestralDragonSouls.Items;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls
{
    /// <summary>
    /// 祖龙残魂头部 - 大后期超级Boss
    /// 迷幻仙气风格，白色雾气视觉效果
    /// 多阶段AI：空灵巡游、龙息吐纳、虚空穿梭、灵魂风暴、终极轮回
    /// </summary>
    [AutoloadBossHead]
    public class AncestralDragonSoulHead : AncestralDragonSoul
    {
        public override WormType NPCWormType => WormType.Head;

        #region AI状态枚举

        private enum AIState
        {
            Intro,              // 出场演出
            EtherealGlide,      // 空灵滑翔 - 优雅环绕
            DragonBreath,       // 龙息吐纳 - 发射雾气弹幕
            VoidPhase,          // 虚空相位 - 穿梭攻击
            SpiralDescent,      // 螺旋俯冲
            SoulStorm,          // 灵魂风暴 - 召唤龙魂碎片
            CoilingStrike,      // 盘龙缠绕 - 包围玩家
            AncestralBeam,      // 祖龙吐息 - 大激光
            EternalCycle,       // 终极轮回 - 三阶段终极攻击
            PhaseTransition     // 阶段转换
        }

        #endregion

        #region 状态变量

        private AIState CurrentState {
            get => (AIState)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        private ref float StateTimer => ref NPC.ai[1];
        private ref float AttackTimer => ref NPC.ai[2];
        private ref float SubState => ref NPC.ai[3];

        // 阶段控制
        private int currentPhase = 1;
        private bool didPhase2Transition = false;
        private bool didPhase3Transition = false;

        private const float Phase2Threshold = 0.6f;
        private const float Phase3Threshold = 0.3f;

        // 攻击相关
        private int breathTimer = 0;
        private const int BreathInterval = 100;

        private Vector2 chargeTarget;
        private int chargeCount;
        private int maxCharges;

        private float spiralAngle;
        private float spiralRadius;

        private float coilAngle;
        private int coilDirection;

        private float beamAngle;
        private bool isBeamActive;

        #endregion

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<AncestralDragonSoulBody>();
        }

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 15;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.boss = true;
            NPC.width = 90;
            NPC.height = 90;
            NPC.lifeMax = 9500000;
            NPC.damage = 360;
            NPC.defense = 130;

            Music = MusicID.LunarBoss; // 可替换为自定义音乐
        }

        public override void OnSpawn(IEntitySource source) {
            base.OnSpawn(source);

            CurrentState = AIState.Intro;
            StateTimer = 0;
            currentPhase = 1;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.netUpdate = true;
            }
        }

        public override void AI() {
            base.AI();

            if (!NPC.HasValidTarget)
                NPC.TargetClosest(true);

            // 初始化
            if (NPC.localAI[0] == 0f) {
                breathTimer = BreathInterval;
                NPC.localAI[0] = 1f;
            }

            // 检查阶段转换
            CheckPhaseTransition();

            StateTimer++;
            AttackTimer++;

            // 龙息计时
            if (--breathTimer <= 0 && CurrentState != AIState.Intro && CurrentState != AIState.PhaseTransition) {
                breathTimer = BreathInterval - (currentPhase - 1) * 15;
                FireDragonBreath();
            }

            // 状态机
            switch (CurrentState) {
                case AIState.Intro:
                    RunIntro();
                    break;
                case AIState.EtherealGlide:
                    RunEtherealGlide();
                    break;
                case AIState.DragonBreath:
                    RunDragonBreath();
                    break;
                case AIState.VoidPhase:
                    RunVoidPhase();
                    break;
                case AIState.SpiralDescent:
                    RunSpiralDescent();
                    break;
                case AIState.SoulStorm:
                    RunSoulStorm();
                    break;
                case AIState.CoilingStrike:
                    RunCoilingStrike();
                    break;
                case AIState.AncestralBeam:
                    RunAncestralBeam();
                    break;
                case AIState.EternalCycle:
                    RunEternalCycle();
                    break;
                case AIState.PhaseTransition:
                    RunPhaseTransition();
                    break;
            }

            // 更新旋转和朝向
            UpdateRotation();

            // 空灵粒子效果
            SpawnEtherealParticles();
        }

        #region 辅助方法

        private void UpdateRotation() {
            if (NPC.velocity.LengthSquared() > 0.1f) {
                NPC.rotation = NPC.velocity.ToRotation();
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
                if (NPC.spriteDirection == -1)
                    NPC.rotation += MathHelper.Pi;
            }
        }

        private void TransitionTo(AIState newState) {
            CurrentState = newState;
            StateTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            NPC.netUpdate = true;
        }

        private void CheckPhaseTransition() {
            float lifePercent = (float)NPC.life / NPC.lifeMax;

            if (!didPhase2Transition && lifePercent <= Phase2Threshold && lifePercent > Phase3Threshold) {
                didPhase2Transition = true;
                currentPhase = 2;
                TransitionTo(AIState.PhaseTransition);
            }

            if (!didPhase3Transition && lifePercent <= Phase3Threshold) {
                didPhase3Transition = true;
                currentPhase = 3;
                TransitionTo(AIState.PhaseTransition);
            }
        }

        private AIState GetRandomAttackState() {
            int[] phase1States = [(int)AIState.EtherealGlide, (int)AIState.DragonBreath, (int)AIState.SpiralDescent];
            int[] phase2States = [(int)AIState.EtherealGlide, (int)AIState.DragonBreath, (int)AIState.VoidPhase, (int)AIState.SpiralDescent, (int)AIState.SoulStorm];
            int[] phase3States = [(int)AIState.DragonBreath, (int)AIState.VoidPhase, (int)AIState.SoulStorm, (int)AIState.CoilingStrike, (int)AIState.AncestralBeam, (int)AIState.EternalCycle];

            int[] states = currentPhase switch {
                1 => phase1States,
                2 => phase2States,
                _ => phase3States
            };

            return (AIState)states[Main.rand.Next(states.Length)];
        }

        private void SpawnEtherealParticles() {
            if (Main.netMode == NetmodeID.Server) return;

            // 头部周围的仙气粒子
            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(50, 50);
                int dustType = Main.rand.Next(4) switch {
                    0 => DustID.Cloud,
                    1 => DustID.WhiteTorch,
                    2 => DustID.Clentaminator_Cyan,
                    _ => DustID.Frost
                };

                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 200, new Color(240, 248, 255), 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(2, 2) - NPC.velocity * 0.1f;
                Main.dust[dust].fadeIn = 1.5f;
            }

            // 龙眼光芒
            if (Main.rand.NextBool(5)) {
                Vector2 eyeOffset = NPC.rotation.ToRotationVector2() * 30f;
                int dust = Dust.NewDust(NPC.Center + eyeOffset, 0, 0, DustID.WhiteTorch, 0, 0, 100, Color.White, 0.6f);
                Main.dust[dust].noGravity = true;
            }
        }

        #endregion

        #region 攻击方法

        private void FireDragonBreath() {
            if (!NPC.HasValidTarget || Main.netMode == NetmodeID.MultiplayerClient) return;

            int damage = NPC.damage / 3;
            int count = 8 + currentPhase * 3;
            Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);

            for (int i = 0; i < count; i++) {
                float angleOffset = MathHelper.ToRadians(Main.rand.NextFloat(-12f, 12f));
                Vector2 direction = toPlayer.RotatedBy(angleOffset);
                float speed = 10f + Main.rand.NextFloat(-2f, 4f);

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center + direction * 50f,
                    direction * speed,
                    ModContent.ProjectileType<AncestralMistBolt>(),
                    damage,
                    1f
                );
            }

            SoundEngine.PlaySound(SoundID.Item20 with { Pitch = 0.3f }, NPC.Center);

            // 吐息雾气效果
            for (int i = 0; i < 15; i++) {
                Vector2 dustVel = toPlayer.RotatedByRandom(0.5f) * Main.rand.NextFloat(4, 8);
                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Cloud, dustVel.X, dustVel.Y, 180, Color.White, 2f);
                Main.dust[dust].noGravity = true;
            }
        }

        #endregion

        #region AI状态实现

        private void RunIntro() {
            float introProgress = MathHelper.Clamp(StateTimer / 180f, 0f, 1f);

            // 从天空盘旋而下
            Vector2 introOffset = new Vector2(0, -800) * (1f - ACMUtils.SineInOut(introProgress));
            Vector2 desiredPos = Target.Center + new Vector2(MathF.Sin(StateTimer * 0.03f) * 200f, -400) + introOffset;

            Vector2 toDesired = desiredPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toDesired * 0.04f, 0.08f);

            // 仙雾粒子效果
            if (Main.netMode != NetmodeID.Server && StateTimer % 2 == 0) {
                for (int i = 0; i < 5; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(200, 200) * (1f - introProgress);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Cloud, 0, 0, 200, Color.White, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                }
            }

            if (StateTimer == 100) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 1.5f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(15, 60);
            }

            if (StateTimer > 180) {
                TransitionTo(AIState.EtherealGlide);
            }
        }

        private void RunEtherealGlide() {
            // 优雅的环绕飞行
            float orbitSpeed = 0.025f + currentPhase * 0.005f;
            float orbitRadius = 450f - currentPhase * 30f;

            NPC.localAI[1] += orbitSpeed;
            if (NPC.localAI[1] > MathHelper.TwoPi)
                NPC.localAI[1] -= MathHelper.TwoPi;

            Vector2 targetPos = Target.Center + new Vector2(
                MathF.Cos(NPC.localAI[1]) * orbitRadius,
                MathF.Sin(NPC.localAI[1]) * orbitRadius * 0.6f - 150f
            );

            // 添加波动
            targetPos.Y += MathF.Sin(globalTime * 2f) * 40f;

            Vector2 toTarget = targetPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 0.06f, 0.08f);

            // 状态持续时间
            if (StateTimer > 300 - currentPhase * 30) {
                TransitionTo(GetRandomAttackState());
            }
        }

        private void RunDragonBreath() {
            // 悬停并连续吐息
            Vector2 hoverPos = Target.Center + new Vector2(0, -350);
            hoverPos.X += MathF.Sin(globalTime * 1.5f) * 80f;

            Vector2 toHover = hoverPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.04f, 0.1f);

            // 快速发射雾气弹
            if (AttackTimer % (20 - currentPhase * 3) == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                    int spread = 3 + currentPhase;

                    for (int i = -spread / 2; i <= spread / 2; i++) {
                        float angle = MathHelper.ToRadians(i * 8);
                        Vector2 vel = toPlayer.RotatedBy(angle) * 12f;

                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            vel,
                            ModContent.ProjectileType<AncestralMistBolt>(),
                            NPC.damage / 3,
                            1f
                        );
                    }

                    SoundEngine.PlaySound(SoundID.Item13 with { Pitch = 0.4f, Volume = 0.6f }, NPC.Center);
                }
            }

            if (StateTimer > 200) {
                TransitionTo(AIState.EtherealGlide);
            }
        }

        private void RunVoidPhase() {
            // 虚空穿梭 - 快速冲刺穿过玩家
            switch ((int)SubState) {
                case 0: // 准备
                    chargeCount = 0;
                    maxCharges = 2 + currentPhase;
                    SubState = 1;
                    StateTimer = 0;
                    break;

                case 1: // 蓄力
                    NPC.velocity *= 0.9f;

                    // 虚空蓄力粒子
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(100, 100);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 150, Color.White, 1.5f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 6f;
                        }
                    }

                    if (StateTimer >= 30) {
                        chargeTarget = Target.Center + Target.velocity * 15f;
                        Vector2 toTarget = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                        NPC.velocity = toTarget * 35f;
                        SubState = 2;
                        StateTimer = 0;

                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f }, NPC.Center);

                        // 虚空涟漪
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(10, 20);
                    }
                    break;

                case 2: // 冲刺
                    // 虚空拖尾
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 5; i++) {
                            Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 30f * i;
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.Cloud, 0, 0, 200, Color.White, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.05f;
                        }
                    }

                    if (StateTimer >= 25) {
                        chargeCount++;
                        if (chargeCount >= maxCharges) {
                            TransitionTo(AIState.EtherealGlide);
                        }
                        else {
                            SubState = 1;
                            StateTimer = 0;
                        }
                    }
                    break;
            }
        }

        private void RunSpiralDescent() {
            // 螺旋俯冲攻击
            switch ((int)SubState) {
                case 0: // 上升
                    Vector2 risePos = Target.Center + new Vector2(0, -500);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (risePos - NPC.Center) * 0.05f, 0.1f);

                    if (StateTimer >= 60 || Vector2.Distance(NPC.Center, risePos) < 100f) {
                        spiralAngle = (NPC.Center - Target.Center).ToRotation();
                        spiralRadius = 400f;
                        SubState = 1;
                        StateTimer = 0;
                    }
                    break;

                case 1: // 螺旋下降
                    spiralAngle += 0.1f;
                    spiralRadius -= 3f;

                    if (spiralRadius < 80f) spiralRadius = 80f;

                    Vector2 spiralTarget = Target.Center + spiralAngle.ToRotationVector2() * spiralRadius;
                    spiralTarget.Y -= 50f + spiralRadius * 0.3f;

                    Vector2 toSpiral = spiralTarget - NPC.Center;
                    NPC.velocity = toSpiral * 0.15f;

                    // 发射螺旋弹幕
                    if (StateTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 shotDir = -spiralAngle.ToRotationVector2();
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            shotDir * 8f,
                            ModContent.ProjectileType<SpiralSoulFragment>(),
                            NPC.damage / 4,
                            1f
                        );
                    }

                    if (StateTimer > 180 || spiralRadius <= 80f) {
                        TransitionTo(AIState.EtherealGlide);
                    }
                    break;
            }
        }

        private void RunSoulStorm() {
            // 灵魂风暴 - 召唤龙魂碎片环绕攻击
            NPC.velocity *= 0.95f;

            // 保持在玩家上方
            Vector2 hoverPos = Target.Center + new Vector2(0, -300);
            NPC.velocity += (hoverPos - NPC.Center) * 0.002f;

            // 周期性召唤龙魂碎片
            if (StateTimer % 30 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int fragmentCount = 6 + currentPhase * 2;
                for (int i = 0; i < fragmentCount; i++) {
                    float angle = MathHelper.TwoPi * i / fragmentCount + StateTimer * 0.02f;
                    Vector2 spawnPos = NPC.Center + angle.ToRotationVector2() * 150f;

                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        spawnPos,
                        Vector2.Zero,
                        ModContent.ProjectileType<AncestralSoulFragment>(),
                        NPC.damage / 4,
                        1f,
                        ai0: NPC.whoAmI,
                        ai1: angle
                    );
                }

                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f }, NPC.Center);
            }

            // 风暴粒子
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 3; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = Main.rand.NextFloat(100, 200);
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * radius;
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Cloud, 0, 0, 200, Color.White, 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 3f;
                }
            }

            if (StateTimer > 180) {
                TransitionTo(AIState.EtherealGlide);
            }
        }

        private void RunCoilingStrike() {
            // 盘龙缠绕 - 绕玩家盘旋并收缩
            switch ((int)SubState) {
                case 0: // 初始化
                    coilAngle = (NPC.Center - Target.Center).ToRotation();
                    coilDirection = Main.rand.NextBool() ? 1 : -1;
                    SubState = 1;
                    StateTimer = 0;
                    break;

                case 1: // 盘旋收缩
                    float coilSpeed = 0.05f + StateTimer * 0.0005f;
                    coilAngle += coilSpeed * coilDirection;

                    float targetRadius = 350f - StateTimer * 1.5f;
                    if (targetRadius < 100f) targetRadius = 100f;

                    Vector2 coilTarget = Target.Center + coilAngle.ToRotationVector2() * targetRadius;
                    Vector2 toCoil = coilTarget - NPC.Center;
                    NPC.velocity = toCoil * 0.12f;

                    // 收缩时发射追踪弹
                    if (StateTimer % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            toPlayer * 6f,
                            ModContent.ProjectileType<HomingSoulOrb>(),
                            NPC.damage / 4,
                            1f
                        );
                    }

                    if (StateTimer > 200 || targetRadius <= 100f) {
                        SubState = 2;
                        StateTimer = 0;
                    }
                    break;

                case 2: // 爆发冲刺
                    if (StateTimer == 1) {
                        Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                        NPC.velocity = toPlayer * 30f;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(15, 30);
                    }

                    NPC.velocity *= 0.98f;

                    if (StateTimer > 40) {
                        TransitionTo(AIState.EtherealGlide);
                    }
                    break;
            }
        }

        private void RunAncestralBeam() {
            // 祖龙吐息 - 大激光攻击
            switch ((int)SubState) {
                case 0: // 蓄力
                    NPC.velocity *= 0.9f;

                    if (StateTimer == 1) {
                        beamAngle = (Target.Center - NPC.Center).ToRotation();
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
                    }

                    // 蓄力粒子
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 8; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(150, 150);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 100, Color.White, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 10f;
                        }
                    }

                    // 震动
                    if (StateTimer % 10 == 0) {
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(StateTimer / 10f, 10);
                    }

                    if (StateTimer >= 80) {
                        SubState = 1;
                        StateTimer = 0;
                        isBeamActive = true;

                        // 发射激光
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                Vector2.Zero,
                                ModContent.ProjectileType<AncestralDragonBeam>(),
                                NPC.damage,
                                0f,
                                ai0: NPC.whoAmI,
                                ai1: beamAngle
                            );
                        }

                        SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = 0.2f, Volume = 1.5f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(20, 120);
                    }
                    break;

                case 1: // 激光扫射
                    NPC.velocity *= 0.95f;

                    // 缓慢追踪
                    float targetAngle = (Target.Center - NPC.Center).ToRotation();
                    beamAngle = MathHelper.Lerp(beamAngle, targetAngle, 0.015f);

                    if (StateTimer > 120) {
                        isBeamActive = false;
                        TransitionTo(AIState.EtherealGlide);
                    }
                    break;
            }
        }

        private void RunEternalCycle() {
            // 终极轮回 - 三阶段终极攻击组合
            switch ((int)SubState) {
                case 0: // 第一波：环形弹幕爆发
                    NPC.velocity *= 0.9f;

                    if (StateTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 1.3f }, NPC.Center);
                    }

                    if (StateTimer % 15 == 0 && StateTimer <= 90 && Main.netMode != NetmodeID.MultiplayerClient) {
                        int count = 12;
                        float baseAngle = StateTimer * 0.15f;
                        for (int i = 0; i < count; i++) {
                            float angle = baseAngle + MathHelper.TwoPi * i / count;
                            Vector2 vel = angle.ToRotationVector2() * 10f;
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                vel,
                                ModContent.ProjectileType<AncestralMistBolt>(),
                                NPC.damage / 4,
                                1f
                            );
                        }
                    }

                    if (StateTimer > 100) {
                        SubState = 1;
                        StateTimer = 0;
                    }
                    break;

                case 1: // 第二波：多方向冲刺
                    chargeCount = 0;
                    maxCharges = 5;
                    SubState = 2;
                    StateTimer = 0;
                    break;

                case 2: // 冲刺蓄力
                    NPC.velocity *= 0.85f;

                    if (StateTimer >= 20) {
                        Vector2 toPlayer = (Target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                        NPC.velocity = toPlayer * 40f;
                        SubState = 3;
                        StateTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.5f }, NPC.Center);
                    }
                    break;

                case 3: // 冲刺中
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 3; i++) {
                            int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Cloud, 0, 0, 200, Color.White, 2.5f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.1f;
                        }
                    }

                    if (StateTimer >= 20) {
                        chargeCount++;
                        if (chargeCount >= maxCharges) {
                            SubState = 4;
                            StateTimer = 0;
                        }
                        else {
                            SubState = 2;
                            StateTimer = 0;
                        }
                    }
                    break;

                case 4: // 第三波：终极激光
                    NPC.velocity *= 0.9f;

                    if (StateTimer == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                        // 多方向激光
                        for (int i = 0; i < 4; i++) {
                            float angle = MathHelper.PiOver2 * i + StateTimer * 0.01f;
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                Vector2.Zero,
                                ModContent.ProjectileType<AncestralDragonBeam>(),
                                NPC.damage / 2,
                                0f,
                                ai0: NPC.whoAmI,
                                ai1: angle
                            );
                        }
                        SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = 0f, Volume = 1.8f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(25, 80);
                    }

                    if (StateTimer > 150) {
                        TransitionTo(AIState.EtherealGlide);
                    }
                    break;
            }
        }

        private void RunPhaseTransition() {
            NPC.velocity *= 0.92f;
            NPC.dontTakeDamage = true;

            // 阶段转换特效
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 10; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(200, 200);
                    int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, Color.White, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 10f;
                }
            }

            if (StateTimer == 60) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = currentPhase * 0.2f, Volume = 1.5f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(20, 60);

                // 阶段转换爆发
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 50; i++) {
                        float angle = MathHelper.TwoPi * i / 50;
                        Vector2 vel = angle.ToRotationVector2() * 8f;
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.WhiteTorch, vel.X, vel.Y, 100, Color.White, 3f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (StateTimer > 100) {
                NPC.dontTakeDamage = false;
                TransitionTo(AIState.EtherealGlide);
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            float soulPulse = 1f + MathF.Sin(soulPulsePhase) * 0.1f;

            // 迷幻光晕
            DrawMysticalGlow(spriteBatch, screenPos, tex, origin, soulPulse);

            // 拖尾
            DrawEtherealTrail(spriteBatch, screenPos, tex, origin);

            // 主体
            Color mistColor = Color.Lerp(drawColor, new Color(240, 248, 255), 0.5f);
            mistColor = Color.Lerp(mistColor, Color.White, 0.35f);

            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            spriteBatch.Draw(tex, NPC.Center - screenPos, null, mistColor * NPC.Opacity,
                NPC.rotation, origin, NPC.scale * soulPulse, effects, 0f);

            // 内层发光
            Color innerGlow = new Color(255, 255, 255) * 0.35f * soulPulse;
            innerGlow.A = 0;
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, innerGlow,
                NPC.rotation, origin, NPC.scale * 0.85f, effects, 0f);

            // 龙眼光效
            DrawDragonEyes(spriteBatch, screenPos);

            return false;
        }

        private void DrawDragonEyes(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.LightShot == null) return;

            Vector2 eyeOffset = NPC.rotation.ToRotationVector2() * 25f;
            Vector2 eyePos = NPC.Center + eyeOffset - screenPos;

            float eyePulse = 0.8f + MathF.Sin(globalTime * 4f) * 0.2f;
            Color eyeColor = new Color(255, 255, 255) * eyePulse * 0.6f;
            eyeColor.A = 0;

            spriteBatch.Draw(ACMAsset.LightShot, eyePos, null, eyeColor, 0f,
                ACMAsset.LightShot.Size() / 2f, 0.6f * eyePulse, SpriteEffects.None, 0f);
        }

        #endregion

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            // 祖龙残魂掉落：近战/远程/魔法 三选一
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<ArchosaurFerrara>(),
                ModContent.ItemType<ArchosaurBow>(),
                ModContent.ItemType<ArchosaurStaff>()
            ));
        }

        public override void BossLoot(ref string name, ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override void OnKill() {
            base.OnKill();

            // 死亡粒子爆发
            for (int i = 0; i < 100; i++) {
                float angle = MathHelper.TwoPi * i / 100;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5, 15);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Cloud,
                    1 => DustID.WhiteTorch,
                    _ => DustID.Clentaminator_Cyan
                };
                int dust = Dust.NewDust(NPC.Center, 0, 0, dustType, vel.X, vel.Y, 150, Color.White, 3f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 1.5f }, NPC.Center);
        }
    }
}
