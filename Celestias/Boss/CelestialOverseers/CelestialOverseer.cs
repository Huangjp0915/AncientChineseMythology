using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialOverseers
{
    /// <summary>
    /// 天庭观察者 - 月后大后期Boss
    /// 天庭风格，神圣仙气的视觉效果
    /// 一阶段：天眼注视，发射神圣光弹和光柱审判
    /// 二阶段：天威降临，召唤星辰，神圣冲刺
    /// 三阶段：天罚模式，终极神威
    /// </summary>
    [AutoloadBossHead]
    internal class CelestialOverseer : ModNPC
    {
        #region 常量定义

        /// <summary>二阶段血量百分比阈值</summary>
        public const float Phase2Threshold = 0.65f;

        /// <summary>三阶段血量百分比阈值</summary>
        public const float Phase3Threshold = 0.30f;

        /// <summary>天眼环绕数量</summary>
        public const int CelestialEyeCount = 6;

        #endregion

        #region 阶段枚举

        public enum BossPhase
        {
            Intro,                  // 出场演出
            Phase1_Observe,         // 一阶段：神圣观测，悬浮注视
            Phase1_LightPillar,     // 一阶段：光柱审判
            Phase1_HolyBarrage,     // 一阶段：神圣弹幕齐射
            Phase1_DeathRay,        // 一阶段：天眼死光
            Phase1_SweepingBeam,    // 一阶段：扫射光束
            PhaseTransition_2,      // 一阶段到二阶段转换
            Phase2_Descend,         // 二阶段：天威降临
            Phase2_StarSummon,      // 二阶段：召唤星辰
            Phase2_DivineDash,      // 二阶段：神圣冲刺
            Phase2_HaloStorm,       // 二阶段：光环风暴
            Phase2_EyeMinions,      // 二阶段：召唤天眼仆从
            Phase2_CrossLaser,      // 二阶段：交叉激光
            PhaseTransition_3,      // 二阶段到三阶段转换
            Phase3_Punishment,      // 三阶段：天罚模式
            Phase3_UltimateWrath,   // 三阶段：终极神威
            Phase3_FinalJudgment,   // 三阶段：最终审判
            Phase3_OmegaLaser,      // 三阶段：终极激光
            Phase3_MinionSync       // 三阶段：仆从同步激光
        }

        #endregion

        #region 状态属性

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float PhaseTimer => ref NPC.ai[1];
        public ref float AttackTimer => ref NPC.ai[2];
        public ref float SubState => ref NPC.ai[3];

        /// <summary>是否处于二阶段</summary>
        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;

        /// <summary>是否处于三阶段</summary>
        public bool IsPhase3 => NPC.life < NPC.lifeMax * Phase3Threshold;

        // 私有状态
        private float globalTime;
        private int seed;
        private Random random;
        private float introProgress;
        private bool didPhase2Transition;
        private bool didPhase3Transition;

        // 天眼状态
        private float[] eyeAngles;
        private float[] eyeDistances;
        private float eyeOrbitSpeed;

        // 攻击控制
        private Vector2 dashTarget;
        private Vector2 dashVelocity;
        private int dashCount;
        private int maxDashCount;

        // 光柱控制
        private Vector2[] pillarPositions;
        private float pillarChargeProgress;

        // 星辰控制
        private int starCount;
        private Vector2[] starPositions;

        // 激光控制
        private float laserAngle;
        private float laserSweepDirection;
        private int laserChargeTime;

        // 仆从控制
        private int[] eyeMinionIds;
        private bool hasSpawnedMinions;

        // 视觉效果
        private float haloRotation;
        private float haloScale;
        private float glowIntensity;
        private float divineAuraAlpha;

        #endregion

        #region ModNPC重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 120;
            NPC.height = 120;
            NPC.damage = 150;
            NPC.defense = 80;
            NPC.lifeMax = 1200000; // 月后级别血量
            NPC.HitSound = SoundID.NPCHit5;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = Item.buyPrice(platinum: 2);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 20f;
            NPC.aiStyle = -1;

            // 调整难度
            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.35f);
                NPC.damage = (int)(NPC.damage * 1.25f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.35f);
            }

            Music = MusicID.LunarBoss; // 可替换为自定义音乐
        }

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(10000);
            random = new Random(seed);

            // 初始化天眼
            eyeAngles = new float[CelestialEyeCount];
            eyeDistances = new float[CelestialEyeCount];
            for (int i = 0; i < CelestialEyeCount; i++) {
                eyeAngles[i] = MathHelper.TwoPi * i / CelestialEyeCount;
                eyeDistances[i] = 150f + Main.rand.NextFloat(-20f, 20f);
            }
            eyeOrbitSpeed = 0.02f;

            // 初始化光柱
            pillarPositions = new Vector2[8];

            // 初始化星辰
            starPositions = new Vector2[12];

            // 初始化仆从
            eyeMinionIds = new int[4];
            hasSpawnedMinions = false;

            // 初始化视觉效果
            haloRotation = 0f;
            haloScale = 1f;
            glowIntensity = 1f;
            divineAuraAlpha = 0f;

            Phase = BossPhase.Intro;
            PhaseTimer = 0;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.netUpdate = true;
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
            writer.Write((int)Phase);
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.WriteVector2(dashTarget);
            writer.Write(dashCount);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            dashTarget = reader.ReadVector2();
            dashCount = reader.ReadInt32();

            random ??= new Random(seed);
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        #endregion

        #region AI主循环

        public override void AI() {
            random ??= new Random(seed);
            globalTime += 1f / 60f;

            // 初始化天眼（如果需要）
            if (eyeAngles == null) {
                InitializeEyes();
            }

            // 检测目标
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    // 没有有效目标，升天离开
                    NPC.velocity.Y -= 0.8f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            // 检查阶段转换
            CheckPhaseTransition();

            // 更新视觉效果
            UpdateVisualEffects();

            // 更新天眼轨道
            UpdateCelestialEyes();

            PhaseTimer++;
            AttackTimer++;

            // 根据当前阶段执行AI
            switch (Phase) {
                case BossPhase.Intro:
                    RunIntro(target);
                    break;
                case BossPhase.Phase1_Observe:
                    RunPhase1Observe(target);
                    break;
                case BossPhase.Phase1_LightPillar:
                    RunPhase1LightPillar(target);
                    break;
                case BossPhase.Phase1_HolyBarrage:
                    RunPhase1HolyBarrage(target);
                    break;
                case BossPhase.Phase1_DeathRay:
                    RunPhase1DeathRay(target);
                    break;
                case BossPhase.Phase1_SweepingBeam:
                    RunPhase1SweepingBeam(target);
                    break;
                case BossPhase.PhaseTransition_2:
                    RunPhaseTransition2(target);
                    break;
                case BossPhase.Phase2_Descend:
                    RunPhase2Descend(target);
                    break;
                case BossPhase.Phase2_StarSummon:
                    RunPhase2StarSummon(target);
                    break;
                case BossPhase.Phase2_DivineDash:
                    RunPhase2DivineDash(target);
                    break;
                case BossPhase.Phase2_HaloStorm:
                    RunPhase2HaloStorm(target);
                    break;
                case BossPhase.Phase2_EyeMinions:
                    RunPhase2EyeMinions(target);
                    break;
                case BossPhase.Phase2_CrossLaser:
                    RunPhase2CrossLaser(target);
                    break;
                case BossPhase.PhaseTransition_3:
                    RunPhaseTransition3(target);
                    break;
                case BossPhase.Phase3_Punishment:
                    RunPhase3Punishment(target);
                    break;
                case BossPhase.Phase3_UltimateWrath:
                    RunPhase3UltimateWrath(target);
                    break;
                case BossPhase.Phase3_FinalJudgment:
                    RunPhase3FinalJudgment(target);
                    break;
                case BossPhase.Phase3_OmegaLaser:
                    RunPhase3OmegaLaser(target);
                    break;
                case BossPhase.Phase3_MinionSync:
                    RunPhase3MinionSync(target);
                    break;
            }

            // 神圣光照
            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.95f, 0.7f) * glowIntensity);

            // 天眼光照
            for (int i = 0; i < CelestialEyeCount; i++) {
                Vector2 eyePos = GetEyePosition(i);
                Lighting.AddLight(eyePos, new Vector3(0.8f, 0.9f, 1f) * 0.5f);
            }
        }

        private void InitializeEyes() {
            eyeAngles = new float[CelestialEyeCount];
            eyeDistances = new float[CelestialEyeCount];
            for (int i = 0; i < CelestialEyeCount; i++) {
                eyeAngles[i] = MathHelper.TwoPi * i / CelestialEyeCount;
                eyeDistances[i] = 150f;
            }
        }

        private void UpdateCelestialEyes() {
            for (int i = 0; i < CelestialEyeCount; i++) {
                eyeAngles[i] += eyeOrbitSpeed;

                // 轻微的距离波动
                float baseDistance = 150f;
                if (IsPhase2) baseDistance = 180f;
                if (IsPhase3) baseDistance = 200f;

                eyeDistances[i] = baseDistance + MathF.Sin(globalTime * 2f + i * 0.5f) * 15f;
            }
        }

        private Vector2 GetEyePosition(int index) {
            if (eyeAngles == null || eyeDistances == null) return NPC.Center;
            float angle = eyeAngles[index];
            float distance = eyeDistances[index];
            return NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
        }

        private void UpdateVisualEffects() {
            // 光环旋转
            haloRotation += 0.01f;

            // 根据阶段调整光环
            if (IsPhase3) {
                haloScale = 1.5f + MathF.Sin(globalTime * 4f) * 0.2f;
                glowIntensity = 1.5f;
                divineAuraAlpha = MathHelper.Lerp(divineAuraAlpha, 0.8f, 0.05f);
            }
            else if (IsPhase2) {
                haloScale = 1.2f + MathF.Sin(globalTime * 3f) * 0.1f;
                glowIntensity = 1.2f;
                divineAuraAlpha = MathHelper.Lerp(divineAuraAlpha, 0.5f, 0.05f);
            }
            else {
                haloScale = 1f + MathF.Sin(globalTime * 2f) * 0.05f;
                glowIntensity = 1f;
                divineAuraAlpha = MathHelper.Lerp(divineAuraAlpha, 0.3f, 0.05f);
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

        #endregion

        #region 出场演出

        private void RunIntro(Player target) {
            introProgress = MathHelper.Clamp(PhaseTimer / 180f, 0f, 1f);

            // 从天而降，带有神圣光芒
            Vector2 introOffset = new Vector2(0, -600) * (1f - ACMUtils.SineInOut(introProgress));
            Vector2 desiredPos = target.Center + new Vector2(0, -350) + introOffset;

            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, 0.03f);
            NPC.velocity *= 0.9f;

            // 神圣粒子效果
            if (Main.netMode != NetmodeID.Server && PhaseTimer % 2 == 0) {
                // 金色神圣光粒
                for (int i = 0; i < 4; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(150, 150) * (1f - introProgress);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                }

                // 星光粒子
                for (int i = 0; i < 2; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(100, 100);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.YellowStarDust, 0, -2f, 150, default, 1.2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            // 神圣音效
            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
            }

            if (PhaseTimer == 120) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(15, 50);

                // 激活天空效果
                if (!VaultUtils.isServer && !SkyManager.Instance[CelestialOverseerSky.SkyName].IsActive()) {
                    SkyManager.Instance.Activate(CelestialOverseerSky.SkyName);
                }
            }

            if (PhaseTimer > 180) {
                TransitionTo(BossPhase.Phase1_Observe);
            }
        }

        #endregion

        #region 一阶段AI

        private void RunPhase1Observe(Player target) {
            // 神圣悬浮，保持在玩家上方
            Vector2 hoverPos = target.Center + new Vector2(0, -400);

            // 添加优雅的悬浮晃动
            hoverPos.X += MathF.Sin(globalTime * 1.2f) * 60f;
            hoverPos.Y += MathF.Sin(globalTime * 1.8f) * 25f;

            Vector2 toHover = hoverPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.025f, 0.1f);

            // 天眼缓慢观测旋转
            eyeOrbitSpeed = 0.015f;

            // 定期发射神圣光弹
            float shotCooldown = Main.expertMode ? 35f : 45f;
            if (AttackTimer % shotCooldown == 0) {
                FireHolyOrbs(target);
            }

            // 定期从天眼发射追踪弹
            if (AttackTimer % 80 == 0) {
                FireEyeBeams(target);
            }

            // 随机切换到其他攻击
            if (PhaseTimer > 300) {
                int nextAction = Main.rand.Next(5);
                switch (nextAction) {
                    case 0:
                        TransitionTo(BossPhase.Phase1_LightPillar);
                        break;
                    case 1:
                        TransitionTo(BossPhase.Phase1_HolyBarrage);
                        break;
                    case 2:
                        TransitionTo(BossPhase.Phase1_DeathRay);
                        break;
                    case 3:
                        TransitionTo(BossPhase.Phase1_SweepingBeam);
                        break;
                    default:
                        PhaseTimer = 0; // 继续观测
                        break;
                }
            }
        }

        private void FireHolyOrbs(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int orbCount = Main.expertMode ? 5 : 3;
            float spread = MathHelper.ToRadians(30);
            Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            float baseAngle = toTarget.ToRotation();

            for (int i = 0; i < orbCount; i++) {
                float angle = baseAngle + spread * (i - (orbCount - 1) / 2f) / (orbCount - 1);
                Vector2 velocity = angle.ToRotationVector2() * 10f;

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    velocity,
                    ModContent.ProjectileType<HolyOrb>(),
                    NPC.damage / 2,
                    2f,
                    Main.myPlayer
                );
            }

            SoundEngine.PlaySound(SoundID.Item29, NPC.Center);
        }

        private void FireEyeBeams(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // 从随机天眼发射追踪光束
            int eyeIndex = Main.rand.Next(CelestialEyeCount);
            Vector2 eyePos = GetEyePosition(eyeIndex);
            Vector2 toTarget = (target.Center - eyePos).SafeNormalize(Vector2.UnitY);

            Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                eyePos,
                toTarget * 8f,
                ModContent.ProjectileType<CelestialEyeBeam>(),
                NPC.damage / 3,
                1f,
                Main.myPlayer
            );

            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.3f }, eyePos);
        }

        private void RunPhase1LightPillar(Player target) {
            switch ((int)SubState) {
                case 0: // 准备阶段 - 悬停并蓄力
                    NPC.velocity *= 0.92f;

                    if (PhaseTimer == 1) {
                        // 初始化光柱位置
                        int pillarCount = Main.expertMode ? 6 : 4;
                        pillarPositions = new Vector2[pillarCount];
                        for (int i = 0; i < pillarCount; i++) {
                            float offsetX = (i - (pillarCount - 1) / 2f) * 200f;
                            pillarPositions[i] = new Vector2(target.Center.X + offsetX, target.Center.Y + 50);
                        }

                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f }, NPC.Center);
                    }

                    pillarChargeProgress = PhaseTimer / 60f;

                    // 预警粒子
                    if (Main.netMode != NetmodeID.Server) {
                        foreach (var pos in pillarPositions) {
                            if (pos == Vector2.Zero) continue;
                            int dust = Dust.NewDust(pos + new Vector2(-20, -500), 40, 500, DustID.GoldCoin, 0, 0, 100, default, 0.8f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = new Vector2(0, 2f);
                        }
                    }

                    if (PhaseTimer >= 60) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 释放光柱
                    if (PhaseTimer == 1) {
                        // 生成光柱弹幕
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            foreach (var pos in pillarPositions) {
                                if (pos == Vector2.Zero) continue;
                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(),
                                    new Vector2(pos.X, pos.Y - 800),
                                    new Vector2(0, 25f),
                                    ModContent.ProjectileType<DivineLightPillar>(),
                                    NPC.damage,
                                    5f,
                                    Main.myPlayer
                                );
                            }
                        }

                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 30);
                    }

                    if (PhaseTimer > 60) {
                        TransitionTo(BossPhase.Phase1_Observe);
                    }
                    break;
            }
        }

        private void RunPhase1HolyBarrage(Player target) {
            // 快速向各方向发射神圣光弹
            NPC.velocity *= 0.95f;

            // 追踪玩家位置
            Vector2 hoverPos = target.Center + new Vector2(0, -300);
            NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.02f);

            // 环形射击
            if (PhaseTimer % 15 == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int count = 8;
                    float baseAngle = PhaseTimer * 0.1f;
                    for (int i = 0; i < count; i++) {
                        float angle = baseAngle + MathHelper.TwoPi * i / count;
                        Vector2 velocity = angle.ToRotationVector2() * 8f;

                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            velocity,
                            ModContent.ProjectileType<HolyOrb>(),
                            NPC.damage / 3,
                            1f,
                            Main.myPlayer
                        );
                    }
                }

                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.2f }, NPC.Center);
            }

            if (PhaseTimer > 180) {
                TransitionTo(BossPhase.Phase1_Observe);
            }
        }

        private void RunPhase1DeathRay(Player target) {
            // 天眼死光 - 从本体发射追踪玩家的大激光
            switch ((int)SubState) {
                case 0: // 蓄力阶段
                    NPC.velocity *= 0.9f;

                    // 保持在玩家上方
                    Vector2 hoverPos = target.Center + new Vector2(0, -350);
                    NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.02f);

                    // 计算激光初始角度
                    if (PhaseTimer == 1) {
                        laserAngle = (target.Center - NPC.Center).ToRotation();
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.8f, Volume = 1.2f }, NPC.Center);
                    }

                    // 蓄力粒子效果
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 5; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(100, 100);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 8f;
                        }
                    }

                    laserChargeTime = Main.expertMode ? 50 : 60;
                    if (PhaseTimer >= laserChargeTime) {
                        SubState = 1;
                        PhaseTimer = 0;

                        // 发射激光
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                Vector2.Zero,
                                ModContent.ProjectileType<DivineDeathRay>(),
                                NPC.damage,
                                0f,
                                Main.myPlayer,
                                ai0: NPC.whoAmI,
                                ai1: laserAngle
                            );
                        }

                        SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = 0.5f, Volume = 1.5f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(15, 60);
                    }
                    break;

                case 1: // 激光发射中
                    NPC.velocity *= 0.95f;

                    // 激光持续时间
                    if (PhaseTimer > 90) {
                        TransitionTo(BossPhase.Phase1_Observe);
                    }
                    break;
            }
        }

        private void RunPhase1SweepingBeam(Player target) {
            // 扫射光束 - 多道光束从左到右或从右到左扫射
            switch ((int)SubState) {
                case 0: // 准备阶段
                    NPC.velocity *= 0.9f;

                    Vector2 sweepHoverPos = target.Center + new Vector2(0, -400);
                    NPC.Center = Vector2.Lerp(NPC.Center, sweepHoverPos, 0.03f);

                    if (PhaseTimer == 1) {
                        laserSweepDirection = Main.rand.NextBool() ? 1f : -1f;
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.3f }, NPC.Center);
                    }

                    // 预警线
                    if (Main.netMode != NetmodeID.Server) {
                        float sweepAngle = MathHelper.PiOver4 * laserSweepDirection;
                        Vector2 lineDir = sweepAngle.ToRotationVector2();
                        for (int i = 0; i < 10; i++) {
                            Vector2 dustPos = NPC.Center + lineDir * (i * 80);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 150, default, 1f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (PhaseTimer >= 40) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 扫射阶段
                    // 发射多道快速光束
                    if (PhaseTimer % 8 == 0 && PhaseTimer <= 80) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            float progress = PhaseTimer / 80f;
                            float startAngle = laserSweepDirection > 0 ? -MathHelper.PiOver4 : MathHelper.PiOver4;
                            float endAngle = laserSweepDirection > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4;
                            float currentAngle = MathHelper.Lerp(startAngle, endAngle, progress) + MathHelper.PiOver2;

                            Vector2 velocity = currentAngle.ToRotationVector2() * 18f;
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                velocity,
                                ModContent.ProjectileType<SweepingLaserBolt>(),
                                NPC.damage / 2,
                                2f,
                                Main.myPlayer
                            );
                        }

                        SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.5f }, NPC.Center);
                    }

                    if (PhaseTimer > 100) {
                        TransitionTo(BossPhase.Phase1_Observe);
                    }
                    break;
            }
        }

        #endregion

        #region 阶段转换

        private void RunPhaseTransition2(Player target) {
            NPC.velocity *= 0.95f;

            // 天眼加速旋转
            eyeOrbitSpeed = 0.05f + PhaseTimer * 0.001f;

            // 能量聚集效果
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 8; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(200, 200);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 50, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 8f;
                }

                // 星光爆发
                if (PhaseTimer % 10 == 0) {
                    for (int i = 0; i < 5; i++) {
                        Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(50, 50);
                        int dust = Dust.NewDust(dustPos, 0, 0, DustID.YellowStarDust, 0, 0, 100, default, 2f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }

            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(20, 60);
            }

            if (PhaseTimer > 100) {
                eyeOrbitSpeed = 0.025f;
                TransitionTo(BossPhase.Phase2_Descend);
            }
        }

        private void RunPhaseTransition3(Player target) {
            NPC.velocity *= 0.93f;

            // 极速天眼旋转
            eyeOrbitSpeed = 0.08f + PhaseTimer * 0.002f;

            // 神圣能量风暴
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 12; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(250, 250);
                    int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.YellowStarDust;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 50, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 12f;
                }
            }

            if (PhaseTimer == 40) {
                SoundEngine.PlaySound(SoundID.Item119 with { Volume = 1.5f }, NPC.Center);
            }

            if (PhaseTimer == 80) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.2f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(30, 80);
            }

            if (PhaseTimer > 120) {
                eyeOrbitSpeed = 0.035f;
                TransitionTo(BossPhase.Phase3_Punishment);
            }
        }

        #endregion

        #region 二阶段AI

        private void RunPhase2Descend(Player target) {
            // 天威降临 - 缓慢下压并释放环形光波
            Vector2 descendPos = target.Center + new Vector2(0, -200);
            NPC.Center = Vector2.Lerp(NPC.Center, descendPos, 0.03f);

            // 释放环形光波
            if (PhaseTimer % 40 == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int waveCount = 12;
                    for (int i = 0; i < waveCount; i++) {
                        float angle = MathHelper.TwoPi * i / waveCount;
                        Vector2 velocity = angle.ToRotationVector2() * 6f;

                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            velocity,
                            ModContent.ProjectileType<HolyOrb>(),
                            NPC.damage / 3,
                            2f,
                            Main.myPlayer
                        );
                    }
                }

                SoundEngine.PlaySound(SoundID.Item29, NPC.Center);
            }

            // 压迫感粒子
            if (Main.netMode != NetmodeID.Server && PhaseTimer % 3 == 0) {
                Vector2 dustPos = target.Center + Main.rand.NextVector2Circular(300, 50) + new Vector2(0, -100);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 3f, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            if (PhaseTimer > 200) {
                int nextAction = Main.rand.Next(5);
                switch (nextAction) {
                    case 0:
                        TransitionTo(BossPhase.Phase2_StarSummon);
                        break;
                    case 1:
                        TransitionTo(BossPhase.Phase2_DivineDash);
                        break;
                    case 2:
                        TransitionTo(BossPhase.Phase2_HaloStorm);
                        break;
                    case 3:
                        TransitionTo(BossPhase.Phase2_EyeMinions);
                        break;
                    case 4:
                        TransitionTo(BossPhase.Phase2_CrossLaser);
                        break;
                }
            }
        }

        private void RunPhase2StarSummon(Player target) {
            switch ((int)SubState) {
                case 0: // 准备召唤
                    NPC.velocity *= 0.9f;

                    if (PhaseTimer == 1) {
                        starCount = Main.expertMode ? 8 : 5;
                        for (int i = 0; i < starCount; i++) {
                            float angle = MathHelper.TwoPi * i / starCount + Main.rand.NextFloat(-0.2f, 0.2f);
                            float distance = 400f + Main.rand.NextFloat(-50f, 50f);
                            starPositions[i] = target.Center + angle.ToRotationVector2() * distance;
                        }

                        SoundEngine.PlaySound(SoundID.Item25 with { Pitch = 0.3f }, NPC.Center);
                    }

                    // 星辰预警
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < starCount; i++) {
                            float alpha = PhaseTimer / 60f;
                            Vector2 pos = starPositions[i];
                            int dust = Dust.NewDust(pos, 0, 0, DustID.YellowStarDust, 0, 0, 100, default, 2f * alpha);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (PhaseTimer >= 60) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 星辰坠落
                    if (PhaseTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < starCount; i++) {
                                Vector2 toTarget = (target.Center - starPositions[i]).SafeNormalize(Vector2.Zero);
                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(),
                                    starPositions[i],
                                    toTarget * 12f,
                                    ModContent.ProjectileType<CelestialStar>(),
                                    NPC.damage / 2,
                                    3f,
                                    Main.myPlayer
                                );
                            }
                        }

                        SoundEngine.PlaySound(SoundID.Item92, NPC.Center);
                    }

                    if (PhaseTimer > 80) {
                        TransitionTo(BossPhase.Phase2_Descend);
                    }
                    break;
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
                    if (Main.netMode != NetmodeID.Server) {
                        Vector2 dustVel = Main.rand.NextVector2CircularEdge(5, 5);
                        int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldCoin, dustVel.X, dustVel.Y, 100, default, 1.5f);
                        Main.dust[dust].noGravity = true;
                    }

                    if (PhaseTimer >= 25) {
                        // 设置冲刺方向
                        dashTarget = target.Center;
                        dashVelocity = (dashTarget - NPC.Center).SafeNormalize(Vector2.Zero) * 30f;
                        SubState = 2;
                        PhaseTimer = 0;

                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.5f }, NPC.Center);
                    }
                    break;

                case 2: // 冲刺
                    NPC.velocity = dashVelocity;

                    // 冲刺拖尾粒子
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 dustPos = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 30f * i;
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.1f;
                        }
                    }

                    if (PhaseTimer >= 25) {
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
            // 光环风暴 - 释放旋转的光环
            Vector2 hoverPos = target.Center + new Vector2(0, -300);
            NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.02f);

            // 释放旋转光环
            if (PhaseTimer % 25 == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    float baseAngle = PhaseTimer * 0.15f;
                    int ringCount = 6;
                    for (int i = 0; i < ringCount; i++) {
                        float angle = baseAngle + MathHelper.TwoPi * i / ringCount;
                        Vector2 velocity = angle.ToRotationVector2() * 7f;

                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            velocity,
                            ModContent.ProjectileType<HolyHaloRing>(),
                            NPC.damage / 3,
                            2f,
                            Main.myPlayer,
                            ai0: angle
                        );
                    }
                }

                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.5f }, NPC.Center);
            }

            if (PhaseTimer > 200) {
                TransitionTo(BossPhase.Phase2_Descend);
            }
        }

        private void RunPhase2EyeMinions(Player target) {
            // 召唤天眼仆从
            switch ((int)SubState) {
                case 0: // 召唤阶段
                    NPC.velocity *= 0.9f;

                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item119 with { Pitch = 0.2f }, NPC.Center);

                        // 召唤天眼仆从
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int minionCount = Main.expertMode ? 4 : 3;
                            eyeMinionIds = new int[minionCount];

                            for (int i = 0; i < minionCount; i++) {
                                float angle = MathHelper.TwoPi * i / minionCount;
                                Vector2 spawnPos = NPC.Center + angle.ToRotationVector2() * 100f;

                                int npcId = NPC.NewNPC(
                                    NPC.GetSource_FromAI(),
                                    (int)spawnPos.X,
                                    (int)spawnPos.Y,
                                    ModContent.NPCType<CelestialEyeMinion>(),
                                    ai0: NPC.whoAmI,
                                    ai1: i
                                );
                                eyeMinionIds[i] = npcId;
                            }

                            hasSpawnedMinions = true;
                        }
                    }

                    // 召唤粒子效果
                    if (Main.netMode != NetmodeID.Server && PhaseTimer <= 30) {
                        for (int i = 0; i < 8; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(80, 80);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.YellowStarDust, 0, 0, 100, default, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = Main.rand.NextVector2Circular(3, 3);
                        }
                    }

                    if (PhaseTimer >= 60) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 等待仆从行动
                    // 缓慢移动
                    Vector2 hoverPos = target.Center + new Vector2(0, -350);
                    NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.015f);

                    if (PhaseTimer > 180) {
                        TransitionTo(BossPhase.Phase2_Descend);
                    }
                    break;
            }
        }

        private void RunPhase2CrossLaser(Player target) {
            // 交叉激光 - 从四个方向发射交叉激光
            switch ((int)SubState) {
                case 0: // 准备阶段
                    NPC.velocity *= 0.9f;

                    Vector2 crossHoverPos = target.Center + new Vector2(0, -300);
                    NPC.Center = Vector2.Lerp(NPC.Center, crossHoverPos, 0.03f);

                    if (PhaseTimer == 1) {
                        laserAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.6f }, NPC.Center);
                    }

                    // 预警线
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 4; i++) {
                            float angle = laserAngle + MathHelper.PiOver2 * i;
                            Vector2 dir = angle.ToRotationVector2();
                            for (int j = 0; j < 8; j++) {
                                Vector2 dustPos = NPC.Center + dir * (j * 100);
                                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 150, default, 1.2f);
                                Main.dust[dust].noGravity = true;
                            }
                        }
                    }

                    if (PhaseTimer >= 50) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 发射交叉激光
                    if (PhaseTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < 4; i++) {
                                float angle = laserAngle + MathHelper.PiOver2 * i;
                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(),
                                    NPC.Center,
                                    Vector2.Zero,
                                    ModContent.ProjectileType<CrossLaserBeam>(),
                                    NPC.damage / 2,
                                    0f,
                                    Main.myPlayer,
                                    ai0: NPC.whoAmI,
                                    ai1: angle
                                );
                            }
                        }

                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.3f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 40);
                    }

                    // 激光旋转
                    laserAngle += 0.015f;

                    if (PhaseTimer > 120) {
                        TransitionTo(BossPhase.Phase2_Descend);
                    }
                    break;
            }
        }

        #endregion

        #region 三阶段AI

        private void RunPhase3Punishment(Player target) {
            // 天罚模式 - 持续追击并释放密集弹幕
            Vector2 toTarget = target.Center - NPC.Center;
            Vector2 desiredVelocity = toTarget.SafeNormalize(Vector2.Zero) * 10f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVelocity, 0.08f);

            // 高速天眼旋转
            eyeOrbitSpeed = 0.04f;

            // 密集神圣光弹
            if (PhaseTimer % 12 == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                    float spread = MathHelper.ToRadians(15);

                    for (int i = -1; i <= 1; i++) {
                        Vector2 velocity = toPlayer.RotatedBy(spread * i) * 14f;
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            velocity,
                            ModContent.ProjectileType<HolyOrb>(),
                            NPC.damage / 3,
                            2f,
                            Main.myPlayer
                        );
                    }
                }
            }

            // 天眼同步射击
            if (PhaseTimer % 30 == 0) {
                FireAllEyeBeams(target);
            }

            if (PhaseTimer > 300) {
                int nextAction = Main.rand.Next(4);
                switch (nextAction) {
                    case 0:
                        TransitionTo(BossPhase.Phase3_UltimateWrath);
                        break;
                    case 1:
                        TransitionTo(BossPhase.Phase3_FinalJudgment);
                        break;
                    case 2:
                        TransitionTo(BossPhase.Phase3_OmegaLaser);
                        break;
                    case 3:
                        TransitionTo(BossPhase.Phase3_MinionSync);
                        break;
                }
            }
        }

        private void FireAllEyeBeams(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            for (int i = 0; i < CelestialEyeCount; i++) {
                Vector2 eyePos = GetEyePosition(i);
                Vector2 toTarget = (target.Center - eyePos).SafeNormalize(Vector2.Zero);

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    eyePos,
                    toTarget * 10f,
                    ModContent.ProjectileType<CelestialEyeBeam>(),
                    NPC.damage / 4,
                    1f,
                    Main.myPlayer
                );
            }

            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.5f, Volume = 1.2f }, NPC.Center);
        }

        private void RunPhase3UltimateWrath(Player target) {
            // 终极神威 - 巨大能量爆发
            switch ((int)SubState) {
                case 0: // 蓄力
                    NPC.velocity *= 0.9f;

                    // 能量聚集
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 10; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(300, 300);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 50, default, 2.5f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 15f;
                        }
                    }

                    if (PhaseTimer == 30) {
                        SoundEngine.PlaySound(SoundID.Item119 with { Pitch = -0.3f }, NPC.Center);
                    }

                    if (PhaseTimer >= 60) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 释放
                    if (PhaseTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // 巨大的环形弹幕爆发
                            int waves = 3;
                            for (int w = 0; w < waves; w++) {
                                int count = 16;
                                float baseAngle = w * MathHelper.ToRadians(15);
                                for (int i = 0; i < count; i++) {
                                    float angle = baseAngle + MathHelper.TwoPi * i / count;
                                    Vector2 velocity = angle.ToRotationVector2() * (8f + w * 2f);

                                    Projectile.NewProjectile(
                                        NPC.GetSource_FromAI(),
                                        NPC.Center,
                                        velocity,
                                        ModContent.ProjectileType<HolyOrb>(),
                                        NPC.damage / 3,
                                        2f,
                                        Main.myPlayer
                                    );
                                }
                            }
                        }

                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.5f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(20, 40);
                    }

                    if (PhaseTimer > 80) {
                        TransitionTo(BossPhase.Phase3_Punishment);
                    }
                    break;
            }
        }

        private void RunPhase3FinalJudgment(Player target) {
            // 最终审判 - 大量光柱从天而降
            switch ((int)SubState) {
                case 0: // 准备
                    NPC.velocity *= 0.92f;

                    if (PhaseTimer == 1) {
                        int pillarCount = Main.expertMode ? 10 : 7;
                        pillarPositions = new Vector2[pillarCount];
                        for (int i = 0; i < pillarCount; i++) {
                            Vector2 offset = Main.rand.NextVector2Circular(400, 100);
                            pillarPositions[i] = target.Center + offset;
                        }

                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.7f }, NPC.Center);
                    }

                    // 预警
                    if (Main.netMode != NetmodeID.Server) {
                        foreach (var pos in pillarPositions) {
                            if (pos == Vector2.Zero) continue;
                            int dust = Dust.NewDust(pos + new Vector2(-30, -600), 60, 600, DustID.GoldCoin, 0, 0, 100, default, 1f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (PhaseTimer >= 50) {
                        SubState = 1;
                        PhaseTimer = 0;
                    }
                    break;

                case 1: // 释放光柱
                    if (PhaseTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            foreach (var pos in pillarPositions) {
                                if (pos == Vector2.Zero) continue;
                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(),
                                    new Vector2(pos.X, pos.Y - 900),
                                    new Vector2(0, 30f),
                                    ModContent.ProjectileType<DivineLightPillar>(),
                                    NPC.damage,
                                    5f,
                                    Main.myPlayer
                                );
                            }
                        }

                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.3f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(18, 40);
                    }

                    if (PhaseTimer > 60) {
                        TransitionTo(BossPhase.Phase3_Punishment);
                    }
                    break;
            }
        }

        private void RunPhase3OmegaLaser(Player target) {
            // 终极激光 - 超大范围追踪激光
            switch ((int)SubState) {
                case 0: // 蓄力阶段
                    NPC.velocity *= 0.85f;

                    if (PhaseTimer == 1) {
                        laserAngle = (target.Center - NPC.Center).ToRotation();
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);
                    }

                    // 巨大的能量聚集效果
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 12; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(200, 200);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 50, default, 2.5f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 12f;
                        }

                        // 核心聚能
                        for (int i = 0; i < 5; i++) {
                            Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(30, 30);
                            int dust = Dust.NewDust(dustPos, 0, 0, DustID.YellowStarDust, 0, 0, 100, default, 3f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    // 震动逐渐增强
                    if (PhaseTimer % 10 == 0) {
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(PhaseTimer / 10f, 10);
                    }

                    if (PhaseTimer >= 80) {
                        SubState = 1;
                        PhaseTimer = 0;

                        // 发射终极激光
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                Vector2.Zero,
                                ModContent.ProjectileType<OmegaCelestialLaser>(),
                                (int)(NPC.damage * 1.5f),
                                0f,
                                Main.myPlayer,
                                ai0: NPC.whoAmI,
                                ai1: laserAngle
                            );
                        }

                        SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.3f, Volume = 2f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(25, 120);
                    }
                    break;

                case 1: // 激光持续
                    NPC.velocity *= 0.9f;

                    // 追踪玩家
                    float targetAngle = (target.Center - NPC.Center).ToRotation();
                    laserAngle = MathHelper.Lerp(laserAngle, targetAngle, 0.02f);

                    if (PhaseTimer > 150) {
                        TransitionTo(BossPhase.Phase3_Punishment);
                    }
                    break;
            }
        }

        private void RunPhase3MinionSync(Player target) {
            // 仆从同步激光 - 所有天眼仆从同时发射激光
            switch ((int)SubState) {
                case 0: // 确保有仆从
                    if (!hasSpawnedMinions || !AnyMinionsAlive()) {
                        // 召唤新仆从
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int minionCount = Main.expertMode ? 4 : 3;
                            eyeMinionIds = new int[minionCount];

                            for (int i = 0; i < minionCount; i++) {
                                float angle = MathHelper.TwoPi * i / minionCount;
                                Vector2 spawnPos = NPC.Center + angle.ToRotationVector2() * 150f;

                                int npcId = NPC.NewNPC(
                                    NPC.GetSource_FromAI(),
                                    (int)spawnPos.X,
                                    (int)spawnPos.Y,
                                    ModContent.NPCType<CelestialEyeMinion>(),
                                    ai0: NPC.whoAmI,
                                    ai1: i
                                );
                                eyeMinionIds[i] = npcId;
                            }
                            hasSpawnedMinions = true;
                        }
                    }

                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: // 准备同步激光
                    NPC.velocity *= 0.9f;

                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f, Volume = 1.2f }, NPC.Center);

                        // 通知所有仆从准备激光
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            foreach (int minionId in eyeMinionIds) {
                                if (minionId >= 0 && minionId < Main.maxNPCs && Main.npc[minionId].active) {
                                    Main.npc[minionId].ai[2] = 1; // 触发激光模式
                                }
                            }
                        }
                    }

                    // 预警效果
                    if (Main.netMode != NetmodeID.Server) {
                        foreach (int minionId in eyeMinionIds) {
                            if (minionId >= 0 && minionId < Main.maxNPCs && Main.npc[minionId].active) {
                                Vector2 minionPos = Main.npc[minionId].Center;
                                Vector2 toTarget = (target.Center - minionPos).SafeNormalize(Vector2.Zero);
                                for (int i = 0; i < 5; i++) {
                                    Vector2 dustPos = minionPos + toTarget * (i * 100);
                                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 150, default, 1f);
                                    Main.dust[dust].noGravity = true;
                                }
                            }
                        }
                    }

                    if (PhaseTimer >= 60) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2: // 同步发射
                    if (PhaseTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            foreach (int minionId in eyeMinionIds) {
                                if (minionId >= 0 && minionId < Main.maxNPCs && Main.npc[minionId].active) {
                                    NPC minion = Main.npc[minionId];
                                    Vector2 toTarget = (target.Center - minion.Center).SafeNormalize(Vector2.Zero);

                                    Projectile.NewProjectile(
                                        NPC.GetSource_FromAI(),
                                        minion.Center,
                                        toTarget * 15f,
                                        ModContent.ProjectileType<MinionSyncLaser>(),
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
                                bossToTarget * 15f,
                                ModContent.ProjectileType<MinionSyncLaser>(),
                                NPC.damage / 2,
                                2f,
                                Main.myPlayer
                            );
                        }

                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.5f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(15, 30);
                    }

                    if (PhaseTimer > 60) {
                        TransitionTo(BossPhase.Phase3_Punishment);
                    }
                    break;
            }
        }

        private bool AnyMinionsAlive() {
            if (eyeMinionIds == null) return false;
            foreach (int id in eyeMinionIds) {
                if (id >= 0 && id < Main.maxNPCs && Main.npc[id].active &&
                    Main.npc[id].type == ModContent.NPCType<CelestialEyeMinion>()) {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 绘制神圣光环（底层）
            DrawDivineAura(spriteBatch, screenPos);

            // 绘制拖尾
            DrawTrail(spriteBatch, screenPos);

            // 绘制天眼
            DrawCelestialEyes(spriteBatch, screenPos, drawColor);

            // 绘制光晕（在本体之前）
            DrawHalo(spriteBatch, screenPos);

            // 绘制本体
            DrawMainBody(spriteBatch, screenPos, drawColor);

            // 绘制外层光效
            DrawOuterGlow(spriteBatch, screenPos);

            return false;
        }

        private void DrawDivineAura(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.LightShot == null) return;

            // 使用 LightShot 创建大范围神圣光环
            Texture2D auraTexture = ACMAsset.LightShot;
            Vector2 drawPos = NPC.Center - screenPos;

            Color auraColor = new Color(255, 240, 180) * divineAuraAlpha;
            auraColor.A = 0;

            float auraScale = 8f * haloScale;

            spriteBatch.Draw(
                auraTexture,
                drawPos,
                null,
                auraColor,
                MathHelper.PiOver2,
                auraTexture.Size() / 2f,
                auraScale,
                SpriteEffects.None,
                0f
            );
        }

        private void DrawTrail(SpriteBatch spriteBatch, Vector2 screenPos) {
            Texture2D texture = TextureAssets.Npc[Type].Value;

            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / NPC.oldPos.Length;
                Color trailColor = new Color(255, 230, 150) * progress * 0.25f * NPC.Opacity;
                trailColor.A = 0;
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float scale = NPC.scale * progress * 0.9f;

                spriteBatch.Draw(
                    texture,
                    drawPos,
                    null,
                    trailColor,
                    NPC.rotation,
                    texture.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawCelestialEyes(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (eyeAngles == null) return;

            // 优先使用专用天眼纹理，否则使用BlankStar
            Texture2D eyeTexture = CelestialEyeMinion.CelestialOverseerEye ?? ACMAsset.BlankStar;
            if (eyeTexture == null) return;

            for (int i = 0; i < CelestialEyeCount; i++) {
                Vector2 eyePos = GetEyePosition(i) - screenPos;

                // 外层光晕
                Color outerGlow = new Color(200, 220, 255) * 0.6f;
                outerGlow.A = 0;
                spriteBatch.Draw(
                    eyeTexture,
                    eyePos,
                    null,
                    outerGlow,
                    globalTime + i * 0.5f,
                    eyeTexture.Size() / 2f,
                    0.6f,
                    SpriteEffects.None,
                    0f
                );

                // 核心
                Color coreColor = new Color(255, 255, 220);
                coreColor.A = 0;
                spriteBatch.Draw(
                    eyeTexture,
                    eyePos,
                    null,
                    coreColor,
                    -globalTime * 0.5f + i * 0.3f,
                    eyeTexture.Size() / 2f,
                    0.4f,
                    SpriteEffects.None,
                    0f
                );

                // 瞳孔效果（如果使用专用纹理）
                if (CelestialEyeMinion.CelestialOverseerEye != null) {
                    Color pupilColor = Color.White;
                    spriteBatch.Draw(
                        eyeTexture,
                        eyePos,
                        null,
                        pupilColor,
                        0f,
                        eyeTexture.Size() / 2f,
                        0.35f,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        private void DrawHalo(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.BlankStar == null) return;

            Texture2D haloTexture = ACMAsset.BlankStar;
            Vector2 drawPos = NPC.Center - screenPos;

            // 多层光环
            for (int i = 0; i < 3; i++) {
                float layerRotation = haloRotation + i * MathHelper.TwoPi / 3f;
                float layerScale = (1.5f + i * 0.3f) * haloScale;
                Color layerColor = new Color(255, 245, 200) * (0.4f - i * 0.1f);
                layerColor.A = 0;

                spriteBatch.Draw(
                    haloTexture,
                    drawPos,
                    null,
                    layerColor,
                    layerRotation,
                    haloTexture.Size() / 2f,
                    layerScale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawMainBody(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;

            // 内层发光
            Color glowColor = new Color(255, 240, 180) * 0.4f * NPC.Opacity;
            glowColor.A = 0;

            for (int i = 0; i < 4; i++) {
                float angle = globalTime * 2f + i * MathHelper.PiOver2;
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 4f;
                spriteBatch.Draw(
                    texture,
                    drawPos + offset,
                    null,
                    glowColor,
                    NPC.rotation,
                    texture.Size() / 2f,
                    NPC.scale * 1.05f,
                    SpriteEffects.None,
                    0f
                );
            }

            // 本体
            Color bodyColor = drawColor * NPC.Opacity;
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                bodyColor,
                NPC.rotation,
                texture.Size() / 2f,
                NPC.scale,
                SpriteEffects.None,
                0f
            );
        }

        private void DrawOuterGlow(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.Sparkle == null) return;

            // 使用 Sparkle 创建星芒效果
            Texture2D sparkleTexture = ACMAsset.Sparkle;
            Vector2 drawPos = NPC.Center - screenPos;

            Color sparkleColor = new Color(255, 250, 220) * 0.3f * glowIntensity;
            sparkleColor.A = 0;

            // 旋转的星芒
            spriteBatch.Draw(
                sparkleTexture,
                drawPos,
                null,
                sparkleColor,
                globalTime * 0.5f,
                sparkleTexture.Size() / 2f,
                2f * haloScale,
                SpriteEffects.None,
                0f
            );

            // 反向旋转的星芒
            spriteBatch.Draw(
                sparkleTexture,
                drawPos,
                null,
                sparkleColor * 0.5f,
                -globalTime * 0.3f,
                sparkleTexture.Size() / 2f,
                2.5f * haloScale,
                SpriteEffects.None,
                0f
            );
        }

        #endregion
    }
}