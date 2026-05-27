using System;
using System.IO;
using AncientChineseMythology.Celestias.Boss.Vaisravanas.Items;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Systems;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas
{
    /// <summary>
    /// 毗沙门天王 - 北方多闻天王，月后大后期Boss
    /// 仙气类白色主题，持宝塔的护法天王
    /// 一阶段：宝塔威光，神圣光弹和光束
    /// 二阶段：天王降临，召唤夜叉，四方圣光
    /// 三阶段：四天王威，终极宝塔光
    /// </summary>
    [AutoloadBossHead]
    internal partial class Vaisravana : ModNPC
    {
        #region 常量定义

        /// <summary>二阶段血量百分比阈值</summary>
        public const float Phase2Threshold = 0.65f;

        /// <summary>三阶段血量百分比阈值</summary>
        public const float Phase3Threshold = 0.30f;

        /// <summary>宝塔环绕数量</summary>
        public const int TowerCount = 4;

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

        // 宝塔状态
        private float[] towerAngles;
        private float[] towerDistances;
        private float towerOrbitSpeed;

        // 攻击控制
        private Vector2 dashTarget;
        private Vector2 dashVelocity;
        private int dashCount;
        private int maxDashCount;

        // 光柱控制
        private Vector2[] pillarPositions;

        // 星辰控制
        private int starCount;
        private Vector2[] starPositions;

        // 激光控制
        private float laserAngle;
        private float laserSweepDirection;
        private int laserChargeTime;

        // 夜叉仆从控制
        private int[] yakshaMinionIds;
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
            NPC.width = 130;
            NPC.height = 130;
            NPC.damage = 160;
            NPC.defense = 85;
            NPC.lifeMax = 1350000; // 月后级别血量
            NPC.HitSound = SoundID.NPCHit5;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = Item.buyPrice(platinum: 2, gold: 50);
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

            Music = MusicID.LunarBoss;
        }

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(10000);
            random = new Random(seed);

            // 初始化宝塔
            towerAngles = new float[TowerCount];
            towerDistances = new float[TowerCount];
            for (int i = 0; i < TowerCount; i++) {
                towerAngles[i] = MathHelper.TwoPi * i / TowerCount;
                towerDistances[i] = 180f + Main.rand.NextFloat(-20f, 20f);
            }
            towerOrbitSpeed = 0.015f;

            // 初始化光柱
            pillarPositions = new Vector2[12];

            // 初始化星辰
            starPositions = new Vector2[16];

            // 初始化仆从
            yakshaMinionIds = new int[4];
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

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<GeneralOrder>(), 1, 1, 3));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<TreasurePagodaStaff>(),
                ModContent.ItemType<VaultshadeVoidshot>(),
                ModContent.ItemType<CelestialCircletScepter>()
            ));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<TreasurePagodaCharm>(), 4));
        }

        public override void OnKill() {
            DownedBossSystem.downedVaisravana = true;
            if (Main.netMode != NetmodeID.Server) {
                PunchCameraModifier mod = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 25f, 12f, 60, 2000f, FullName);
                Main.instance.CameraModifiers.Add(mod);
            }
        }

        #endregion

        #region AI主循环

        public override void AI() {
            random ??= new Random(seed);
            globalTime += 1f / 60f;

            // 初始化宝塔（如果需要）
            if (towerAngles == null) {
                InitializeTowers();
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

            // 更新宝塔轨道
            UpdateTowers();

            PhaseTimer++;
            AttackTimer++;

            // 根据当前阶段执行AI
            switch (Phase) {
                case BossPhase.Intro:
                    RunIntro(target);
                    break;
                case BossPhase.Phase1_TowerGlory:
                    RunPhase1TowerGlory(target);
                    break;
                case BossPhase.Phase1_TowerBeam:
                    RunPhase1TowerBeam(target);
                    break;
                case BossPhase.Phase1_HolyBarrage:
                    RunPhase1HolyBarrage(target);
                    break;
                case BossPhase.Phase1_SweepingLight:
                    RunPhase1SweepingLight(target);
                    break;
                case BossPhase.Phase1_StarRain:
                    RunPhase1StarRain(target);
                    break;
                case BossPhase.PhaseTransition_2:
                    RunPhaseTransition2(target);
                    break;
                case BossPhase.Phase2_Descend:
                    RunPhase2Descend(target);
                    break;
                case BossPhase.Phase2_YakshaSummon:
                    RunPhase2YakshaSummon(target);
                    break;
                case BossPhase.Phase2_QuadrantRay:
                    RunPhase2QuadrantRay(target);
                    break;
                case BossPhase.Phase2_ImmortalWave:
                    RunPhase2ImmortalWave(target);
                    break;
                case BossPhase.Phase2_DivineDash:
                    RunPhase2DivineDash(target);
                    break;
                case BossPhase.Phase2_HaloStorm:
                    RunPhase2HaloStorm(target);
                    break;
                case BossPhase.PhaseTransition_3:
                    RunPhaseTransition3(target);
                    break;
                case BossPhase.Phase3_FourKingsWrath:
                    RunPhase3FourKingsWrath(target);
                    break;
                case BossPhase.Phase3_TowerJudgment:
                    RunPhase3TowerJudgment(target);
                    break;
                case BossPhase.Phase3_UltimateTower:
                    RunPhase3UltimateTower(target);
                    break;
                case BossPhase.Phase3_YakshaSync:
                    RunPhase3YakshaSync(target);
                    break;
                case BossPhase.Phase3_FinalRadiance:
                    RunPhase3FinalRadiance(target);
                    break;
            }

            // 仙气白光照明
            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.98f, 0.95f) * glowIntensity);

            // 宝塔光照
            for (int i = 0; i < TowerCount; i++) {
                Vector2 towerPos = GetTowerPosition(i);
                Lighting.AddLight(towerPos, new Vector3(1f, 0.95f, 0.85f) * 0.6f);
            }
        }

        private void InitializeTowers() {
            towerAngles = new float[TowerCount];
            towerDistances = new float[TowerCount];
            for (int i = 0; i < TowerCount; i++) {
                towerAngles[i] = MathHelper.TwoPi * i / TowerCount;
                towerDistances[i] = 180f;
            }
        }

        private void UpdateTowers() {
            for (int i = 0; i < TowerCount; i++) {
                towerAngles[i] += towerOrbitSpeed;

                // 轻微的距离波动
                float baseDistance = 180f;
                if (IsPhase2) baseDistance = 210f;
                if (IsPhase3) baseDistance = 240f;

                towerDistances[i] = baseDistance + MathF.Sin(globalTime * 1.8f + i * 0.6f) * 18f;
            }
        }

        private Vector2 GetTowerPosition(int index) {
            if (towerAngles == null || towerDistances == null) return NPC.Center;
            float angle = towerAngles[index];
            float distance = towerDistances[index];
            return NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
        }

        private void UpdateVisualEffects() {
            // 光环旋转
            haloRotation += 0.008f;

            // 根据阶段调整光环
            if (IsPhase3) {
                haloScale = 1.5f + MathF.Sin(globalTime * 3.5f) * 0.2f;
                glowIntensity = 1.6f;
                divineAuraAlpha = MathHelper.Lerp(divineAuraAlpha, 0.85f, 0.04f);
            }
            else if (IsPhase2) {
                haloScale = 1.25f + MathF.Sin(globalTime * 2.5f) * 0.12f;
                glowIntensity = 1.3f;
                divineAuraAlpha = MathHelper.Lerp(divineAuraAlpha, 0.55f, 0.04f);
            }
            else {
                haloScale = 1f + MathF.Sin(globalTime * 1.8f) * 0.06f;
                glowIntensity = 1f;
                divineAuraAlpha = MathHelper.Lerp(divineAuraAlpha, 0.35f, 0.04f);
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

            // 从天而降，带有神圣仙光
            Vector2 introOffset = new Vector2(0, -650) * (1f - ACMUtils.SineInOut(introProgress));
            Vector2 desiredPos = target.Center + new Vector2(0, -360) + introOffset;

            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, 0.025f);
            NPC.velocity *= 0.9f;

            // 仙气粒子效果
            if (!VaultUtils.isServer && PhaseTimer % 2 == 0) {
                // 白色仙光粒
                for (int i = 0; i < 5; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(170, 170) * (1f - introProgress);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                }

                // 星光粒子
                for (int i = 0; i < 2; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(120, 120);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, -1.5f, 150, default, 1.3f);
                    Main.dust[dust].noGravity = true;
                }
            }

            // 神圣音效
            if (PhaseTimer == 55) {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f, Volume = 1.5f }, NPC.Center);
            }

            if (PhaseTimer == 115) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(14, 45);
            }

            if (PhaseTimer > 180) {
                TransitionTo(BossPhase.Phase1_TowerGlory);
            }
        }

        #endregion
    }
}
