using AncientChineseMythology.Celestias.Boss.CelestialOverseers.Items;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialOverseers
{
    /// <summary>
    /// 天庭观察者 Celestial Overseer — 天庭入侵终局 Boss（信息/预判身份）。
    ///
    /// 与毗沙门彻底分家，自包含、不依赖任何共享攻击代码路径。签名机制：
    ///  - 监视槽 Surveillance Meter：处于本体核心或 ≥4 只天眼的直视线内时上升，破除视线/击破临时眼泡时下降；满槽触发"审判标记"高伤预告射线。
    ///  - 窥视相位 Scrying：本体静止 + 无敌约 5 秒，6 只天眼脱离投射"假预告"（纯尘），随后两次真实攻击；击破眼泡减少真实攻击数。
    ///  - 入侵终局事件：50% / 25% 召唤"天庭陪审团"（每名玩家 1 个），20 秒内未清除则获得永久"裁决叠层"（+1 攻击强度/节拍）。
    ///  - 三阶段"全知循环"Omniscient Cycle：固定 4 节拍循环（地标光柱阵 → 带安全扇区的旋转凝视扫描 → 有仆从则同步激光否则惩戒输出窗 → 休整），取代旧的喷弹 hub。
    ///  - 签名十字激光：蓄力在地面显示完整十字预告，开火时天眼缓慢旋转激光面（靠横穿旋转方向闪避）。
    /// </summary>
    [AutoloadBossHead]
    internal class CelestialOverseer : ModNPC
    {
        #region 常量定义

        /// <summary>二阶段血量百分比阈值（规则改变：召唤持久天眼 + 加入位移/连射攻击族）</summary>
        public const float Phase2Threshold = 0.65f;

        /// <summary>三阶段血量百分比阈值（规则改变：进入全知循环，废除随机 hub）</summary>
        public const float Phase3Threshold = 0.30f;

        /// <summary>天眼环绕数量</summary>
        public const int CelestialEyeCount = 6;

        /// <summary>陪审团事件持续时间（帧）</summary>
        public const int JuryDuration = 1200;

        /// <summary>裁决叠层上限</summary>
        public const int MaxVerdictStacks = 3;

        #endregion

        #region 阶段枚举

        public enum BossPhase
        {
            Intro,
            Observe,                // 短促重定位/休整节拍（无喷弹），选下一攻击
            Attack_CrossLaser,      // 签名：地面十字预告 + 旋转激光面
            Attack_PillarGrid,      // 地标光柱阵
            Attack_GazeSweep,       // 带安全扇区的旋转凝视扫描
            Attack_StarVolley,      // 预判星陨（提前量）
            Attack_EyeBarrage,      // 二阶段：天眼序列连射（带注视线预告）
            Attack_DivineDash,      // 二阶段：预告冲刺
            Scrying,                // 窥视相位（静止无敌 + 假预告 + 真实攻击）
            MarkedForJudgment,      // 监视满槽中断：单发高伤预告射线
            JuryTrial,              // 入侵终局事件：天庭陪审团
            PhaseTransition,        // 阶段过渡（无敌过渡帧）
            P3_OmniscientCycle      // 三阶段：全知循环
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

        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;
        public bool IsPhase3 => NPC.life < NPC.lifeMax * Phase3Threshold;

        // 私有状态
        private float globalTime;
        private int seed;
        private Random random;
        private float introProgress;
        private bool didPhase2Transition;
        private bool didPhase3Transition;

        // 监视槽机制
        private float surveillanceMeter;     // 0..100
        private bool judgmentQueued;         // 满槽后排队，于安全边界触发
        private int judgmentCooldown;        // 触发后冷却（帧）
        private bool[] eyeHasLOS;            // 每只天眼的视线状态（绘制用）
        private bool coreHasLOS;

        // 天眼轨道
        private float[] eyeAngles;
        private float[] eyeDistances;
        private float eyeOrbitSpeed;
        private bool scryActive;            // 窥视中：本体天眼隐藏，改由眼泡 NPC 表现

        // 窥视
        private int scryRealRemaining;
        private int eyesPopped;

        // 陪审团事件
        private bool didJury50;
        private bool didJury25;
        private int verdictStacks;
        private int[] jurorIds = new int[8];
        private int jurorCount;
        private int juryTimer;

        // 攻击序列
        private int attackIndex;
        private int attacksSinceScry;

        // 十字激光
        private float crossAngle;

        // 光柱/凝视
        private Vector2[] markerPositions = new Vector2[16];
        private int markerCount;
        private float gazeAngle;
        private float gazeDir;
        private float safeWedgeAngle;
        private const float SafeWedgeHalf = 0.55f; // 安全扇区半角（弧度）

        // 冲刺
        private Vector2 dashTarget;
        private Vector2 dashVelocity;
        private int dashCount;
        private int maxDashCount;

        // 全知循环
        private int cycleBeat;
        private bool beatFired;
        private bool beatFired2;

        // 视觉
        private float haloRotation;
        private float haloScale = 1f;
        private float glowIntensity = 1f;
        private float divineAuraAlpha;

        // V2 演出叠加层（纯本地视觉, 由同步的 surveillanceMeter/Phase 推导, 无需额外 net 同步）
        private float surveillanceWarp;   // GenericWarp(rift) 全屏折射扭曲强度 0~1
        private float vignettePublish;    // 监视压迫暗角(平滑) 0~1
        private float runicPublish;       // 全视眼穹/审判庭法阵(平滑) 0~1
        private float bloomPulse;         // 处决/十字开火加性泛光脉冲 0~1
        /// <summary>监视折射主题蓝(冷钢监视色)。</summary>
        private static readonly Color SurveillanceBlue = new(90, 150, 215);

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
            NPC.lifeMax = 1200000;
            NPC.HitSound = SoundID.NPCHit5;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = Item.buyPrice(platinum: 2);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 20f;
            NPC.aiStyle = -1;

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

            InitializeEyes();
            eyeOrbitSpeed = 0.02f;

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
            writer.Write(globalTime);
            writer.Write(surveillanceMeter);
            writer.Write(judgmentQueued);
            writer.Write(judgmentCooldown);
            writer.Write(verdictStacks);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(didJury50);
            writer.Write(didJury25);
            writer.Write(attackIndex);
            writer.Write(attacksSinceScry);
            writer.Write(cycleBeat);
            writer.Write(crossAngle);
            writer.Write(gazeAngle);
            writer.Write(safeWedgeAngle);
            writer.WriteVector2(dashVelocity);
            writer.Write(dashCount);
            writer.Write(scryActive);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            globalTime = reader.ReadSingle();
            surveillanceMeter = reader.ReadSingle();
            judgmentQueued = reader.ReadBoolean();
            judgmentCooldown = reader.ReadInt32();
            verdictStacks = reader.ReadInt32();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            didJury50 = reader.ReadBoolean();
            didJury25 = reader.ReadBoolean();
            attackIndex = reader.ReadInt32();
            attacksSinceScry = reader.ReadInt32();
            cycleBeat = reader.ReadInt32();
            crossAngle = reader.ReadSingle();
            gazeAngle = reader.ReadSingle();
            safeWedgeAngle = reader.ReadSingle();
            dashVelocity = reader.ReadVector2();
            dashCount = reader.ReadInt32();
            scryActive = reader.ReadBoolean();

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
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<OverseersEye>(), 1, 8, 12));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<CelestialWatcherStaff>(),
                ModContent.ItemType<AllSeeingJadeTome>(),
                ModContent.ItemType<CelestialGearGreatsword>(),
                ModContent.ItemType<CelestialMechanismBow>(),
                ModContent.ItemType<CelestialJudgmentChakram>(),
                ModContent.ItemType<GoldenPhoenixSummonStaff>(),
                ModContent.ItemType<ClockworkPhoenixSpear>(),
                ModContent.ItemType<JadeDragonCloudDao>()
            ));
        }

        public override void OnKill() {
            DownedBossSystem.downedCelestialOverseer = true;
            if (Main.netMode != NetmodeID.Server) {
                PunchCameraModifier mod = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 25f, 12f, 60, 2000f, FullName);
                Main.instance.CameraModifiers.Add(mod);
            }
        }

        /// <summary>窥视眼泡被击破时调用（降低监视槽 + 减少本轮真实攻击数）。</summary>
        public void OnScryingEyePopped() {
            surveillanceMeter = Math.Max(0f, surveillanceMeter - 30f);
            eyesPopped++;
            if (scryRealRemaining > 1) scryRealRemaining--;
            NPC.netUpdate = true;
        }

        #endregion

        #region AI主循环

        public override void AI() {
            random ??= new Random(seed);
            globalTime += 1f / 60f;

            if (eyeAngles == null) InitializeEyes();

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    NPC.velocity.Y -= 0.8f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            // 默认可受击；无敌状态由各相位自行开启
            NPC.dontTakeDamage = false;

            UpdateVisualEffects();
            UpdateCelestialEyes();
            UpdateSurveillance(target);
            CheckPhaseTransition();

            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.Observe: RunObserve(target); break;
                case BossPhase.Attack_CrossLaser: RunCrossLaser(target); break;
                case BossPhase.Attack_PillarGrid: RunPillarGrid(target); break;
                case BossPhase.Attack_GazeSweep: RunGazeSweep(target); break;
                case BossPhase.Attack_StarVolley: RunStarVolley(target); break;
                case BossPhase.Attack_EyeBarrage: RunEyeBarrage(target); break;
                case BossPhase.Attack_DivineDash: RunDivineDash(target); break;
                case BossPhase.Scrying: RunScrying(target); break;
                case BossPhase.MarkedForJudgment: RunMarkedForJudgment(target); break;
                case BossPhase.JuryTrial: RunJuryTrial(target); break;
                case BossPhase.PhaseTransition: RunPhaseTransition(target); break;
                case BossPhase.P3_OmniscientCycle: RunOmniscientCycle(target); break;
            }

            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.95f, 0.7f) * glowIntensity);
            for (int i = 0; i < CelestialEyeCount; i++) {
                Lighting.AddLight(GetEyePosition(i), new Vector3(0.8f, 0.9f, 1f) * 0.5f);
            }

            UpdatePresentation();
        }

        /// <summary>
        /// V2 演出叠加：由监视槽/相位推导屏幕折射、监视暗角、全视法阵、泛光脉冲, 并发布给
        /// <see cref="OverseerSurveillanceScreenSystem"/>。纯本地视觉, 全部从已同步状态派生, 不新增 net 同步。
        /// </summary>
        private void UpdatePresentation() {
            float meterFrac = surveillanceMeter / 100f;

            // —— 监视压迫暗角: 随槽收紧; 窥视/终局相位额外加压 ——
            float targetVig = meterFrac * 0.7f;
            bool finale = Phase == BossPhase.JuryTrial || Phase == BossPhase.MarkedForJudgment;
            if (Phase == BossPhase.Scrying) targetVig = System.Math.Max(targetVig, 0.55f);
            if (finale) targetVig = System.Math.Max(targetVig, 0.85f);
            if (IsPhase3) targetVig = System.Math.Max(targetVig, 0.4f);
            vignettePublish = MathHelper.Lerp(vignettePublish, targetVig, 0.06f);

            // —— GenericWarp(rift) 全屏折射: "被扫描"感, 窥视/审判达峰 (走单一全屏名额, 见 PostDraw) ——
            float targetWarp = meterFrac * 0.35f;
            if (Phase == BossPhase.Scrying) targetWarp = System.Math.Max(targetWarp, 0.7f);
            if (Phase == BossPhase.MarkedForJudgment) targetWarp = System.Math.Max(targetWarp, 0.9f);
            surveillanceWarp = MathHelper.Lerp(surveillanceWarp, targetWarp, 0.08f);

            // —— 全视眼穹/审判庭法阵 ——
            float runic = 0f;
            float runicRadius = 360f;
            bool dome = false;
            if (Phase == BossPhase.Scrying) { runic = 0.6f; runicRadius = 380f; }
            else if (Phase == BossPhase.JuryTrial) { runic = 0.9f; runicRadius = 460f; dome = true; }
            else if (Phase == BossPhase.P3_OmniscientCycle) { runic = 0.35f; runicRadius = 420f; }
            runicPublish = MathHelper.Lerp(runicPublish, runic, 0.08f);

            // —— 泛光脉冲衰减 ——
            bloomPulse *= 0.9f;

            float warm = MathHelper.Clamp(meterFrac, 0f, 1f);
            if (finale) warm = 1f;

            OverseerSurveillanceScreenSystem.Publish(NPC.Center, globalTime, vignettePublish, warm, runicPublish, runicRadius, dome);
        }

        private void InitializeEyes() {
            eyeAngles = new float[CelestialEyeCount];
            eyeDistances = new float[CelestialEyeCount];
            eyeHasLOS = new bool[CelestialEyeCount];
            for (int i = 0; i < CelestialEyeCount; i++) {
                eyeAngles[i] = MathHelper.TwoPi * i / CelestialEyeCount;
                eyeDistances[i] = 150f;
            }
        }

        private void UpdateCelestialEyes() {
            for (int i = 0; i < CelestialEyeCount; i++) {
                eyeAngles[i] += eyeOrbitSpeed;
                float baseDistance = 150f;
                if (IsPhase2) baseDistance = 180f;
                if (IsPhase3) baseDistance = 200f;
                if (scryActive) baseDistance = 320f; // 窥视时眼睛外扩（脱离）
                eyeDistances[i] = MathHelper.Lerp(eyeDistances[i], baseDistance + MathF.Sin(globalTime * 2f + i * 0.5f) * 15f, 0.1f);
            }
        }

        private Vector2 GetEyePosition(int index) {
            if (eyeAngles == null || eyeDistances == null) return NPC.Center;
            float angle = eyeAngles[index];
            float distance = eyeDistances[index];
            return NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
        }

        private void UpdateVisualEffects() {
            haloRotation += 0.01f;
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

        /// <summary>监视槽：处于核心或 ≥4 只天眼的直视线内时上升；满槽排队审判。</summary>
        private void UpdateSurveillance(Player target) {
            if (judgmentCooldown > 0) judgmentCooldown--;

            coreHasLOS = Collision.CanHitLine(NPC.Center, 1, 1, target.Center, 1, 1);
            int eyesWithLOS = 0;
            for (int i = 0; i < CelestialEyeCount; i++) {
                Vector2 eyePos = GetEyePosition(i);
                bool los = Collision.CanHitLine(eyePos, 1, 1, target.Center, 1, 1);
                eyeHasLOS[i] = los;
                if (los) eyesWithLOS++;
            }

            bool watched = coreHasLOS || eyesWithLOS >= 4;

            // 窥视/陪审/过渡/审判时不积累监视
            bool accumulate = Phase != BossPhase.Scrying && Phase != BossPhase.JuryTrial
                && Phase != BossPhase.PhaseTransition && Phase != BossPhase.MarkedForJudgment
                && Phase != BossPhase.Intro;

            if (accumulate && judgmentCooldown <= 0) {
                float rise = 0.55f;
                if (IsPhase2) rise += 0.15f;
                if (IsPhase3) rise += 0.20f;
                rise += verdictStacks * 0.10f;

                if (watched) surveillanceMeter += rise;
                else surveillanceMeter -= 1.25f; // 破除视线快速回落，奖励走位
            }

            surveillanceMeter = MathHelper.Clamp(surveillanceMeter, 0f, 100f);

            if (surveillanceMeter >= 100f && !judgmentQueued && judgmentCooldown <= 0) {
                judgmentQueued = true;
                if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
            }
        }

        private bool IsInterruptible() {
            return Phase == BossPhase.Observe
                || Phase == BossPhase.Attack_CrossLaser
                || Phase == BossPhase.Attack_PillarGrid
                || Phase == BossPhase.Attack_GazeSweep
                || Phase == BossPhase.Attack_StarVolley
                || Phase == BossPhase.Attack_EyeBarrage
                || Phase == BossPhase.Attack_DivineDash;
        }

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 && !IsPhase3 && IsInterruptible()) {
                didPhase2Transition = true;
                TransitionTo(BossPhase.PhaseTransition);
            }
            else if (!didPhase3Transition && IsPhase3 && IsInterruptible()) {
                didPhase3Transition = true;
                TransitionTo(BossPhase.PhaseTransition);
            }
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            beatFired = false;
            beatFired2 = false;
            if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
        }

        /// <summary>返回主循环：三阶段进入全知循环，否则进入观测节拍。</summary>
        private void ReturnToHub() {
            if (IsPhase3) {
                cycleBeat = 0;
                TransitionTo(BossPhase.P3_OmniscientCycle);
            }
            else {
                TransitionTo(BossPhase.Observe);
            }
        }

        #endregion

        #region 出场演出

        private void RunIntro(Player target) {
            introProgress = MathHelper.Clamp(PhaseTimer / 180f, 0f, 1f);

            Vector2 introOffset = new Vector2(0, -600) * (1f - ACMUtils.SineInOut(introProgress));
            Vector2 desiredPos = target.Center + new Vector2(0, -350) + introOffset;
            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, 0.03f);
            NPC.velocity *= 0.9f;

            if (Main.netMode != NetmodeID.Server && PhaseTimer % 2 == 0) {
                for (int i = 0; i < 4; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(150, 150) * (1f - introProgress);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                }
                for (int i = 0; i < 2; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(100, 100);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.YellowStarDust, 0, -2f, 150, default, 1.2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (PhaseTimer == 60)
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
            if (PhaseTimer == 120) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f }, NPC.Center);
                ACMScreenShakeSystem.Add(14f);
            }

            if (PhaseTimer > 180) TransitionTo(BossPhase.Observe);
        }

        #endregion

        #region 观测/选招

        private void RunObserve(Player target) {
            // 短促重定位，无喷弹；天眼悬于玩家上方"注视"
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime * 1.2f) * 60f, -400 + MathF.Sin(globalTime * 1.8f) * 25f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.05f, 0.1f);
            eyeOrbitSpeed = MathHelper.Lerp(eyeOrbitSpeed, 0.018f, 0.1f);

            // 注视线尘（Argus 式预告氛围）
            if (Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0) {
                int i = (int)(PhaseTimer / 4) % CelestialEyeCount;
                Vector2 eyePos = GetEyePosition(i);
                Vector2 dir = (target.Center - eyePos).SafeNormalize(Vector2.Zero);
                Vector2 dp = eyePos + dir * Main.rand.NextFloat(0, 400);
                int dust = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 0, 180, default, 0.7f);
                Main.dust[dust].noGravity = true;
            }

            if (PhaseTimer >= 40) {
                SelectNextAction();
            }
        }

        /// <summary>在安全边界选下一步：优先审判/陪审，否则确定性轮换攻击 + 周期窥视。</summary>
        private void SelectNextAction() {
            if (judgmentQueued) { TransitionTo(BossPhase.MarkedForJudgment); return; }
            if (TryStartJury()) return;

            attacksSinceScry++;
            int scryInterval = IsPhase2 ? 2 : 3;
            if (attacksSinceScry >= scryInterval) {
                attacksSinceScry = 0;
                TransitionTo(BossPhase.Scrying);
                return;
            }

            BossPhase[] list = IsPhase2
                ? new[] { BossPhase.Attack_CrossLaser, BossPhase.Attack_EyeBarrage, BossPhase.Attack_PillarGrid, BossPhase.Attack_GazeSweep, BossPhase.Attack_StarVolley, BossPhase.Attack_DivineDash }
                : new[] { BossPhase.Attack_CrossLaser, BossPhase.Attack_PillarGrid, BossPhase.Attack_GazeSweep, BossPhase.Attack_StarVolley };

            BossPhase next = list[attackIndex % list.Length];
            attackIndex++;
            TransitionTo(next);
        }

        /// <summary>50%/25% 触发陪审团事件。</summary>
        private bool TryStartJury() {
            if (!didJury50 && NPC.life < NPC.lifeMax * 0.5f) { didJury50 = true; TransitionTo(BossPhase.JuryTrial); return true; }
            if (!didJury25 && NPC.life < NPC.lifeMax * 0.25f) { didJury25 = true; TransitionTo(BossPhase.JuryTrial); return true; }
            return false;
        }

        #endregion

        #region 签名：十字激光

        private void RunCrossLaser(Player target) {
            switch ((int)SubState) {
                case 0: // 蓄力 + 地面十字预告
                    NPC.velocity *= 0.9f;
                    Vector2 hover = target.Center + new Vector2(0, -300);
                    NPC.Center = Vector2.Lerp(NPC.Center, hover, 0.04f);

                    if (PhaseTimer == 1) {
                        crossAngle = Main.rand.NextFloat(MathHelper.PiOver4);
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.6f }, NPC.Center);
                        SpawnCrossTelegraph(crossAngle, 1500f, 60);
                    }

                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 4; i++) {
                            float a = crossAngle + MathHelper.PiOver2 * i;
                            Vector2 d = a.ToRotationVector2();
                            Vector2 dp = NPC.Center + d * Main.rand.NextFloat(0, 700);
                            int dust = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 0, 120, default, 1.2f);
                            Main.dust[dust].noGravity = true;
                        }
                    }

                    if (PhaseTimer >= 60) {
                        SubState = 1; PhaseTimer = 0;
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < 4; i++) {
                                float a = crossAngle + MathHelper.PiOver2 * i;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                                    ModContent.ProjectileType<CrossLaserBeam>(), NPC.damage / 2, 0f, Main.myPlayer,
                                    ai0: NPC.whoAmI, ai1: a);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.3f }, NPC.Center);
                        ACMScreenShakeSystem.Add(10f);
                        bloomPulse = 1f;
                    }
                    break;

                case 1: // 开火（激光自旋，靠横穿旋转方向闪避）
                    NPC.velocity *= 0.96f;
                    if (PhaseTimer > 130) ReturnToHub();
                    break;
            }
        }

        #endregion

        #region 地标光柱阵

        private void RunPillarGrid(Player target) {
            switch ((int)SubState) {
                case 0: // 地标预告
                    NPC.velocity *= 0.92f;
                    if (PhaseTimer == 1) {
                        markerCount = (Main.expertMode ? 6 : 4) + verdictStacks;
                        if (markerCount > markerPositions.Length) markerCount = markerPositions.Length;
                        for (int i = 0; i < markerCount; i++) {
                            float offsetX = (i - (markerCount - 1) / 2f) * 200f;
                            markerPositions[i] = new Vector2(target.Center.X + offsetX, target.Center.Y + 50);
                            SpawnPillarTelegraph(markerPositions[i], 64);
                        }
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f }, NPC.Center);
                    }
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < markerCount; i++) {
                            int dust = Dust.NewDust(markerPositions[i] + new Vector2(-20, -500), 40, 500, DustID.GoldCoin, 0, 2f, 100, default, 0.8f);
                            Main.dust[dust].noGravity = true;
                        }
                    }
                    if (PhaseTimer >= 64) { SubState = 1; PhaseTimer = 0; }
                    break;

                case 1: // 落柱
                    if (PhaseTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < markerCount; i++) {
                                Projectile.NewProjectile(NPC.GetSource_FromAI(),
                                    new Vector2(markerPositions[i].X, markerPositions[i].Y - 800), new Vector2(0, 25f),
                                    ModContent.ProjectileType<DivineLightPillar>(), NPC.damage, 5f, Main.myPlayer);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f }, NPC.Center);
                        ACMScreenShakeSystem.Add(8f);
                    }
                    if (PhaseTimer > 60) ReturnToHub();
                    break;
            }
        }

        #endregion

        #region 带安全扇区的旋转凝视扫描

        private void RunGazeSweep(Player target) {
            switch ((int)SubState) {
                case 0: // 预告：确定安全扇区方向
                    NPC.velocity *= 0.9f;
                    Vector2 hover = target.Center + new Vector2(0, -360);
                    NPC.Center = Vector2.Lerp(NPC.Center, hover, 0.03f);
                    if (PhaseTimer == 1) {
                        gazeAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                        gazeDir = Main.rand.NextBool() ? 1f : -1f;
                        // 安全扇区朝向玩家当前侧，给一个可站位的缝
                        safeWedgeAngle = (target.Center - NPC.Center).ToRotation();
                        SpawnSafeWedgeTelegraph(safeWedgeAngle, 1400f, 50);
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.2f }, NPC.Center);
                    }
                    if (PhaseTimer >= 50) { SubState = 1; PhaseTimer = 0; }
                    break;

                case 1: // 扫描：四周喷弹，仅保留安全扇区缺口
                    NPC.velocity *= 0.95f;
                    gazeAngle += gazeDir * 0.05f;

                    int interval = IsPhase3 ? 5 : 7;
                    if (PhaseTimer % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        int segments = 16;
                        for (int k = 0; k < segments; k++) {
                            float a = gazeAngle + MathHelper.TwoPi * k / segments;
                            float diff = MathHelper.WrapAngle(a - safeWedgeAngle);
                            if (Math.Abs(diff) < SafeWedgeHalf) continue; // 安全扇区缺口
                            Vector2 vel = a.ToRotationVector2() * 7.5f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                ModContent.ProjectileType<HolyOrb>(), NPC.damage / 3, 1f, Main.myPlayer);
                        }
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.2f }, NPC.Center);
                    }

                    // 持续标示安全扇区缝
                    if (Main.netMode != NetmodeID.Server) {
                        Vector2 d = safeWedgeAngle.ToRotationVector2();
                        Vector2 dp = NPC.Center + d * Main.rand.NextFloat(0, 800);
                        int dust = Dust.NewDust(dp, 0, 0, DustID.BlueTorch, 0, 0, 150, new Color(180, 220, 255), 0.9f);
                        Main.dust[dust].noGravity = true;
                    }

                    if (PhaseTimer > 170) ReturnToHub();
                    break;
            }
        }

        #endregion

        #region 预判星陨

        private void RunStarVolley(Player target) {
            switch ((int)SubState) {
                case 0: // 预告（提前量）
                    NPC.velocity *= 0.9f;
                    if (PhaseTimer == 1) {
                        int count = (Main.expertMode ? 7 : 5) + verdictStacks;
                        if (count > markerPositions.Length) count = markerPositions.Length;
                        markerCount = count;
                        for (int i = 0; i < count; i++) {
                            float angle = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.2f, 0.2f);
                            float dist = 450f + Main.rand.NextFloat(-50f, 50f);
                            markerPositions[i] = target.Center + angle.ToRotationVector2() * dist;
                        }
                        SoundEngine.PlaySound(SoundID.Item25 with { Pitch = 0.3f }, NPC.Center);
                    }
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < markerCount; i++) {
                            int dust = Dust.NewDust(markerPositions[i], 0, 0, DustID.YellowStarDust, 0, 0, 100, default, 2f * (PhaseTimer / 55f));
                            Main.dust[dust].noGravity = true;
                        }
                    }
                    if (PhaseTimer >= 55) { SubState = 1; PhaseTimer = 0; }
                    break;

                case 1: // 发射（按预判提前量瞄准）
                    if (PhaseTimer == 1) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < markerCount; i++) {
                                Vector2 dir = ACMUtils.LeadTarget(markerPositions[i], target.Center, target.velocity, 13f);
                                Vector2 vel = dir * 13f;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), markerPositions[i], vel,
                                    ModContent.ProjectileType<CelestialStar>(), NPC.damage / 2, 3f, Main.myPlayer);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item92, NPC.Center);
                    }
                    if (PhaseTimer > 70) ReturnToHub();
                    break;
            }
        }

        #endregion

        #region 二阶段：天眼连射 + 冲刺

        private void RunEyeBarrage(Player target) {
            NPC.velocity *= 0.94f;
            Vector2 hover = target.Center + new Vector2(0, -340);
            NPC.Center = Vector2.Lerp(NPC.Center, hover, 0.02f);
            eyeOrbitSpeed = MathHelper.Lerp(eyeOrbitSpeed, 0.03f, 0.05f);

            // 每只眼依次开火，开火前有注视线预告
            int perEye = 14;
            int total = CelestialEyeCount * perEye;
            int idx = (int)PhaseTimer / perEye;
            int localT = (int)PhaseTimer % perEye;

            if (idx < CelestialEyeCount) {
                Vector2 eyePos = GetEyePosition(idx);
                if (Main.netMode != NetmodeID.Server && localT < perEye - 4) {
                    Vector2 dir = (target.Center - eyePos).SafeNormalize(Vector2.Zero);
                    Vector2 dp = eyePos + dir * Main.rand.NextFloat(0, 350);
                    int dust = Dust.NewDust(dp, 0, 0, DustID.BlueTorch, 0, 0, 150, new Color(200, 220, 255), 1f);
                    Main.dust[dust].noGravity = true;
                }
                if (localT == perEye - 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 toT = (target.Center - eyePos).SafeNormalize(Vector2.UnitY);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), eyePos, toT * 9f,
                        ModContent.ProjectileType<CelestialEyeBeam>(), NPC.damage / 3, 1f, Main.myPlayer);
                    SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.3f }, eyePos);
                }
            }

            if (PhaseTimer > total + 30) ReturnToHub();
        }

        private void RunDivineDash(Player target) {
            switch ((int)SubState) {
                case 0:
                    dashCount = 0;
                    maxDashCount = Main.expertMode ? 4 : 3;
                    SubState = 1; PhaseTimer = 0;
                    break;

                case 1: // 蓄力 + 冲刺预告线
                    NPC.velocity *= 0.9f;
                    if (PhaseTimer == 1) {
                        dashTarget = target.Center;
                        SpawnDashTelegraph(dashTarget, 25);
                    }
                    if (Main.netMode != NetmodeID.Server) {
                        Vector2 dir = (dashTarget - NPC.Center).SafeNormalize(Vector2.Zero);
                        Vector2 dp = NPC.Center + dir * Main.rand.NextFloat(0, 500);
                        int dust = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 0, 120, default, 1f);
                        Main.dust[dust].noGravity = true;
                    }
                    if (PhaseTimer >= 25) {
                        dashVelocity = (dashTarget - NPC.Center).SafeNormalize(Vector2.Zero) * 30f;
                        SubState = 2; PhaseTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.5f }, NPC.Center);
                    }
                    break;

                case 2: // 冲刺
                    NPC.velocity = dashVelocity;
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 dp = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 30f * i;
                            int dust = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 2f);
                            Main.dust[dust].noGravity = true;
                            Main.dust[dust].velocity = -NPC.velocity * 0.1f;
                        }
                    }
                    if (PhaseTimer >= 22) {
                        dashCount++;
                        if (dashCount >= maxDashCount) ReturnToHub();
                        else { SubState = 1; PhaseTimer = 0; }
                    }
                    break;
            }
        }

        #endregion

        #region 窥视相位（签名）

        private void RunScrying(Player target) {
            NPC.dontTakeDamage = true; // 静止无敌
            NPC.velocity *= 0.85f;
            eyeOrbitSpeed = MathHelper.Lerp(eyeOrbitSpeed, 0.06f, 0.05f);

            switch ((int)SubState) {
                case 0: // 脱离：眼睛外扩并生成可击破眼泡
                    scryActive = true;
                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item119 with { Pitch = 0.3f }, NPC.Center);
                        eyesPopped = 0;
                        scryRealRemaining = 2;
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < CelestialEyeCount; i++) {
                                Vector2 sp = GetEyePosition(i);
                                NPC.NewNPC(NPC.GetSource_FromAI(), (int)sp.X, (int)sp.Y,
                                    ModContent.NPCType<OverseerScryingEye>(), ai0: NPC.whoAmI, ai1: i);
                            }
                        }
                    }
                    if (PhaseTimer >= 30) { SubState = 1; PhaseTimer = 0; }
                    break;

                case 1: // 假预告（纯尘，无伤）—— 学会后可读
                    if (Main.netMode != NetmodeID.Server) {
                        // 随机闪现各种攻击的"假"地面标线
                        if (PhaseTimer % 8 == 0) {
                            int fake = (int)(PhaseTimer / 8) % 3;
                            switch (fake) {
                                case 0: // 假十字
                                    for (int i = 0; i < 4; i++) {
                                        float a = globalTime + MathHelper.PiOver2 * i;
                                        for (int j = 0; j < 6; j++) {
                                            Vector2 dp = target.Center + a.ToRotationVector2() * (j * 90);
                                            int d = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 0, 200, default, 0.7f);
                                            Main.dust[d].noGravity = true;
                                        }
                                    }
                                    break;
                                case 1: // 假光柱
                                    for (int i = -2; i <= 2; i++) {
                                        Vector2 dp = target.Center + new Vector2(i * 160, Main.rand.NextFloat(-250, 0));
                                        int d = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 2f, 200, default, 0.7f);
                                        Main.dust[d].noGravity = true;
                                    }
                                    break;
                                case 2: // 假星陨标记
                                    for (int i = 0; i < 5; i++) {
                                        Vector2 dp = target.Center + Main.rand.NextVector2Circular(300, 200);
                                        int d = Dust.NewDust(dp, 0, 0, DustID.YellowStarDust, 0, 0, 200, default, 0.8f);
                                        Main.dust[d].noGravity = true;
                                    }
                                    break;
                            }
                        }
                    }
                    if (PhaseTimer >= 120) { SubState = 2; PhaseTimer = 0; }
                    break;

                case 2: // 真实攻击：按 scryRealRemaining 依次释放（被击破眼泡会减少）
                    if (PhaseTimer == 1 || (PhaseTimer % 70 == 1 && scryRealRemaining > 0)) {
                        if (scryRealRemaining > 0) {
                            DoScryRealAttack(target, scryRealRemaining);
                            scryRealRemaining--;
                        }
                    }
                    if (scryRealRemaining <= 0 && PhaseTimer % 70 >= 50) { SubState = 3; PhaseTimer = 0; }
                    else if (PhaseTimer > 220) { SubState = 3; PhaseTimer = 0; }
                    break;

                case 3: // 收回眼睛
                    scryActive = false;
                    if (PhaseTimer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                        KillAllScryingEyes();
                    }
                    if (PhaseTimer >= 25) ReturnToHub();
                    break;
            }
        }

        private void DoScryRealAttack(Player target, int variant) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f }, NPC.Center);
            if (variant % 2 == 0) {
                // 光柱阵
                int count = Main.expertMode ? 6 : 4;
                for (int i = 0; i < count; i++) {
                    float offsetX = (i - (count - 1) / 2f) * 200f;
                    Vector2 pos = new Vector2(target.Center.X + offsetX, target.Center.Y - 800);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, new Vector2(0, 25f),
                        ModContent.ProjectileType<DivineLightPillar>(), NPC.damage, 5f, Main.myPlayer);
                }
            }
            else {
                // 旋转十字
                float baseA = Main.rand.NextFloat(MathHelper.PiOver4);
                for (int i = 0; i < 4; i++) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<CrossLaserBeam>(), NPC.damage / 2, 0f, Main.myPlayer,
                        ai0: NPC.whoAmI, ai1: baseA + MathHelper.PiOver2 * i);
                }
            }
        }

        private void KillAllScryingEyes() {
            int type = ModContent.NPCType<OverseerScryingEye>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.type == type && (int)n.ai[0] == NPC.whoAmI) {
                    n.life = 0; n.HitEffect(); n.active = false;
                    if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: i);
                }
            }
        }

        #endregion

        #region 监视满槽：审判标记

        private void RunMarkedForJudgment(Player target) {
            NPC.velocity *= 0.9f;
            switch ((int)SubState) {
                case 0: // 锁定 + 预告
                    if (PhaseTimer == 1) {
                        crossAngle = (target.Center - NPC.Center).ToRotation(); // 锁定方向（不追踪）
                        SpawnJudgmentTelegraph(crossAngle, 2400f, 55); // 致命锁定线（唯一红，固定方向）
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);
                    }
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 dp = NPC.Center + crossAngle.ToRotationVector2() * Main.rand.NextFloat(0, 1200);
                            int d = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 0, 60, default, 1.6f);
                            Main.dust[d].noGravity = true;
                        }
                        if (PhaseTimer % 8 == 0) ACMScreenShakeSystem.Add(MathHelper.Clamp(PhaseTimer / 12f, 0f, 12f)); // 渐强震屏
                    }
                    if (PhaseTimer >= 55) {
                        SubState = 1; PhaseTimer = 0;
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                                ModContent.ProjectileType<JudgmentBeam>(), (int)(NPC.damage * 1.8f), 0f, Main.myPlayer,
                                ai0: NPC.whoAmI, ai1: crossAngle);
                        }
                        SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.3f, Volume = 2f }, NPC.Center);
                        ACMScreenShakeSystem.Add(12f); // 处决级一次性
                        bloomPulse = 1f;               // 全视看穿你的处决泛光
                    }
                    break;

                case 1: // 射线持续后复位
                    if (PhaseTimer > 80) {
                        judgmentQueued = false;
                        surveillanceMeter = 0f;
                        judgmentCooldown = 180;
                        ReturnToHub();
                    }
                    break;
            }
        }

        #endregion

        #region 陪审团事件

        private void RunJuryTrial(Player target) {
            switch ((int)SubState) {
                case 0: // 召唤
                    NPC.dontTakeDamage = true;
                    NPC.velocity *= 0.9f;
                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item119 with { Pitch = -0.2f, Volume = 1.4f }, NPC.Center);
                        ACMScreenShakeSystem.Add(12f);

                        int players = CountActivePlayers();
                        int extra = (didJury25 ? 1 : 0);
                        jurorCount = Math.Min(jurorIds.Length, players + extra);
                        juryTimer = JuryDuration;

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < jurorCount; i++) {
                                float angle = MathHelper.TwoPi * i / Math.Max(1, jurorCount);
                                Vector2 sp = NPC.Center + angle.ToRotationVector2() * 260f;
                                jurorIds[i] = NPC.NewNPC(NPC.GetSource_FromAI(), (int)sp.X, (int)sp.Y,
                                    ModContent.NPCType<HeavenlyJuror>(), ai0: NPC.whoAmI);
                            }
                        }
                    }
                    if (PhaseTimer >= 50) { SubState = 1; PhaseTimer = 0; }
                    break;

                case 1: // 审判中：本体半被动且无敌，玩家须清陪审团
                    NPC.dontTakeDamage = true;
                    Vector2 hover = target.Center + new Vector2(MathF.Sin(globalTime) * 120f, -380);
                    NPC.Center = Vector2.Lerp(NPC.Center, hover, 0.015f);
                    juryTimer--;

                    // 偶发预告光柱（保持压力，但稀疏）
                    if (PhaseTimer % 90 == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 pos = new Vector2(target.Center.X + Main.rand.NextFloat(-200, 200), target.Center.Y - 800);
                        SpawnPillarTelegraph(new Vector2(pos.X, target.Center.Y + 50), 50);
                    }
                    if (PhaseTimer % 90 == 55 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 pos = new Vector2(target.Center.X + Main.rand.NextFloat(-200, 200), target.Center.Y - 800);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, new Vector2(0, 22f),
                            ModContent.ProjectileType<DivineLightPillar>(), NPC.damage, 5f, Main.myPlayer);
                    }

                    bool allDead = !AnyJurorsAlive();
                    if (allDead) {
                        // 成功：进入惩戒输出窗
                        SubState = 2; PhaseTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item4, NPC.Center);
                    }
                    else if (juryTimer <= 0) {
                        // 失败：获得永久裁决叠层
                        if (verdictStacks < MaxVerdictStacks) verdictStacks++;
                        KillAllJurors();
                        SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.3f }, NPC.Center);
                        ACMScreenShakeSystem.Add(12f);
                        if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
                        SubState = 3; PhaseTimer = 0;
                    }
                    break;

                case 2: // 惩戒输出窗（清光奖励）：本体可受击且减速
                    NPC.velocity *= 0.92f;
                    if (Main.netMode != NetmodeID.Server && PhaseTimer % 3 == 0) {
                        Vector2 dp = NPC.Center + Main.rand.NextVector2CircularEdge(140, 140);
                        int d = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 0, 120, default, 1.5f);
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity = (NPC.Center - dp).SafeNormalize(Vector2.Zero) * 3f;
                    }
                    if (PhaseTimer > 150) ReturnToHub();
                    break;

                case 3: // 失败收场（短）
                    NPC.velocity *= 0.92f;
                    if (PhaseTimer > 40) ReturnToHub();
                    break;
            }
        }

        private int CountActivePlayers() {
            int c = 0;
            for (int i = 0; i < Main.maxPlayers; i++)
                if (Main.player[i].active && !Main.player[i].dead) c++;
            return Math.Max(1, c);
        }

        private bool AnyJurorsAlive() {
            int type = ModContent.NPCType<HeavenlyJuror>();
            for (int i = 0; i < jurorCount; i++) {
                int id = jurorIds[i];
                if (id >= 0 && id < Main.maxNPCs && Main.npc[id].active && Main.npc[id].type == type)
                    return true;
            }
            return false;
        }

        private void KillAllJurors() {
            int type = ModContent.NPCType<HeavenlyJuror>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.type == type && (int)n.ai[0] == NPC.whoAmI) {
                    n.life = 0; n.HitEffect(); n.active = false;
                    if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: i);
                }
            }
        }

        #endregion

        #region 阶段过渡

        private void RunPhaseTransition(Player target) {
            NPC.dontTakeDamage = true; // 过渡无敌帧
            NPC.velocity *= 0.93f;
            eyeOrbitSpeed = 0.06f + PhaseTimer * 0.0015f;

            if (Main.netMode != NetmodeID.Server) {
                int n = IsPhase3 ? 12 : 8;
                for (int i = 0; i < n; i++) {
                    Vector2 dp = NPC.Center + Main.rand.NextVector2CircularEdge(230, 230);
                    int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.YellowStarDust;
                    int d = Dust.NewDust(dp, 0, 0, dustType, 0, 0, 50, default, 2.2f);
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity = (NPC.Center - dp).SafeNormalize(Vector2.Zero) * 10f;
                }
            }

            if (PhaseTimer == 40)
                SoundEngine.PlaySound(SoundID.Item119 with { Volume = 1.4f }, NPC.Center);
            if (PhaseTimer == 70) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = IsPhase3 ? -0.3f : 0.1f }, NPC.Center);
                ACMScreenShakeSystem.Add(IsPhase3 ? 12f : 11f);
            }

            int dur = IsPhase3 ? 120 : 100;
            if (PhaseTimer > dur) {
                eyeOrbitSpeed = 0.03f;
                ReturnToHub();
            }
        }

        #endregion

        #region 三阶段：全知循环

        private void RunOmniscientCycle(Player target) {
            eyeOrbitSpeed = MathHelper.Lerp(eyeOrbitSpeed, 0.035f, 0.05f);

            switch (cycleBeat) {
                case 0: BeatPillarGrid(target); break;
                case 1: BeatGazeSweep(target); break;
                case 2: BeatSyncOrPunish(target); break;
                case 3: BeatRest(target); break;
            }
        }

        private void NextBeat() {
            cycleBeat = (cycleBeat + 1) % 4;
            PhaseTimer = 0;
            beatFired = false;
            beatFired2 = false;
            if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
        }

        // 节拍0：地标光柱阵
        private void BeatPillarGrid(Player target) {
            NPC.velocity *= 0.92f;
            if (PhaseTimer == 1) {
                markerCount = (Main.expertMode ? 7 : 5) + verdictStacks;
                if (markerCount > markerPositions.Length) markerCount = markerPositions.Length;
                for (int i = 0; i < markerCount; i++) {
                    float offsetX = (i - (markerCount - 1) / 2f) * 180f;
                    markerPositions[i] = new Vector2(target.Center.X + offsetX, target.Center.Y + 50);
                    SpawnPillarTelegraph(markerPositions[i], 60);
                }
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f }, NPC.Center);
            }
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < markerCount; i++) {
                    int d = Dust.NewDust(markerPositions[i] + new Vector2(-20, -500), 40, 500, DustID.GoldCoin, 0, 2f, 100, default, 0.8f);
                    Main.dust[d].noGravity = true;
                }
            }
            if (!beatFired && PhaseTimer >= 60) {
                beatFired = true;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < markerCount; i++) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(),
                            new Vector2(markerPositions[i].X, markerPositions[i].Y - 800), new Vector2(0, 26f),
                            ModContent.ProjectileType<DivineLightPillar>(), NPC.damage, 5f, Main.myPlayer);
                    }
                }
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f }, NPC.Center);
                ACMScreenShakeSystem.Add(8f);
            }
            if (PhaseTimer > 130) NextBeat();
        }

        // 节拍1：带安全扇区的旋转凝视扫描
        private void BeatGazeSweep(Player target) {
            NPC.velocity *= 0.95f;
            if (PhaseTimer == 1) {
                gazeAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                gazeDir = Main.rand.NextBool() ? 1f : -1f;
                safeWedgeAngle = (target.Center - NPC.Center).ToRotation();
                SpawnSafeWedgeTelegraph(safeWedgeAngle, 1400f, 50);
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.2f }, NPC.Center);
            }
            if (PhaseTimer > 50) {
                gazeAngle += gazeDir * 0.055f;
                if (PhaseTimer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    int segments = 18;
                    for (int k = 0; k < segments; k++) {
                        float a = gazeAngle + MathHelper.TwoPi * k / segments;
                        if (Math.Abs(MathHelper.WrapAngle(a - safeWedgeAngle)) < SafeWedgeHalf) continue;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, a.ToRotationVector2() * 8f,
                            ModContent.ProjectileType<HolyOrb>(), NPC.damage / 3, 1f, Main.myPlayer);
                    }
                }
                if (Main.netMode != NetmodeID.Server) {
                    Vector2 dp = NPC.Center + safeWedgeAngle.ToRotationVector2() * Main.rand.NextFloat(0, 800);
                    int d = Dust.NewDust(dp, 0, 0, DustID.BlueTorch, 0, 0, 150, new Color(180, 220, 255), 0.9f);
                    Main.dust[d].noGravity = true;
                }
            }
            if (PhaseTimer > 50 + 170) NextBeat();
        }

        // 节拍2：有仆从则同步激光，否则惩戒输出窗
        private void BeatSyncOrPunish(Player target) {
            bool minionsAlive = AnyScryingEyesAlive() || AnyJurorsAlive();
            // 全知循环不依赖固定仆从，这里用天眼连射模拟"同步"；否则给一个减速输出窗
            if (minionsAlive) {
                // 同步激光：天眼齐射
                NPC.velocity *= 0.9f;
                if (PhaseTimer == 1) SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f }, NPC.Center);
                if (!beatFired && PhaseTimer >= 50) {
                    beatFired = true;
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int i = 0; i < CelestialEyeCount; i++) {
                            Vector2 eyePos = GetEyePosition(i);
                            Vector2 toT = (target.Center - eyePos).SafeNormalize(Vector2.Zero);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), eyePos, toT * 13f,
                                ModContent.ProjectileType<MinionSyncLaser>(), NPC.damage / 2, 2f, Main.myPlayer);
                        }
                    }
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.4f }, NPC.Center);
                    ACMScreenShakeSystem.Add(10f);
                }
                if (PhaseTimer > 110) NextBeat();
            }
            else {
                // 惩戒输出窗：本体减速逼近，稀疏单发，留给玩家集火
                Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 4f, 0.04f);
                if (PhaseTimer % 40 == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, toTarget * 9f,
                        ModContent.ProjectileType<CelestialEyeBeam>(), NPC.damage / 3, 1f, Main.myPlayer);
                }
                if (Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0) {
                    Vector2 dp = NPC.Center + Main.rand.NextVector2CircularEdge(120, 120);
                    int d = Dust.NewDust(dp, 0, 0, DustID.GoldCoin, 0, 0, 120, default, 1.3f);
                    Main.dust[d].noGravity = true;
                }
                if (PhaseTimer > 120) NextBeat();
            }
        }

        // 节拍3：休整（无喷弹），在此检查审判/陪审中断
        private void BeatRest(Player target) {
            NPC.velocity *= 0.9f;
            Vector2 hover = target.Center + new Vector2(MathF.Sin(globalTime) * 60f, -360);
            NPC.Center = Vector2.Lerp(NPC.Center, hover, 0.03f);

            if (PhaseTimer >= 60) {
                if (judgmentQueued) { TransitionTo(BossPhase.MarkedForJudgment); return; }
                if (TryStartJury()) return;
                NextBeat();
            }
        }

        private bool AnyScryingEyesAlive() {
            int type = ModContent.NPCType<OverseerScryingEye>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && n.type == type && (int)n.ai[0] == NPC.whoAmI) return true;
            }
            return false;
        }

        #endregion

        #region 预告生成辅助

        private void SpawnCrossTelegraph(float baseAngle, float length, int life) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            for (int i = 0; i < 4; i++) {
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                    ModContent.ProjectileType<OverseerGroundTelegraph>(), 0, 0f, Main.myPlayer,
                    ai0: length, ai1: baseAngle + MathHelper.PiOver2 * i, ai2: NPC.whoAmI);
                if (p >= 0 && p < Main.maxProjectiles) {
                    Main.projectile[p].timeLeft = life;
                    Main.projectile[p].localAI[0] = 0f; // style: 线
                }
            }
        }

        private void SpawnSafeWedgeTelegraph(float safeAngle, float length, int life) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                ModContent.ProjectileType<OverseerGroundTelegraph>(), 0, 0f, Main.myPlayer,
                ai0: length, ai1: safeAngle, ai2: NPC.whoAmI);
            if (p >= 0 && p < Main.maxProjectiles) {
                Main.projectile[p].timeLeft = life;
                Main.projectile[p].localAI[0] = 2f; // style: 安全扇区
            }
        }

        private void SpawnPillarTelegraph(Vector2 groundPos, int life) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), groundPos, Vector2.Zero,
                ModContent.ProjectileType<OverseerGroundTelegraph>(), 0, 0f, Main.myPlayer,
                ai0: 800f, ai1: -MathHelper.PiOver2, ai2: -1f);
            if (p >= 0 && p < Main.maxProjectiles) {
                Main.projectile[p].timeLeft = life;
                Main.projectile[p].localAI[0] = 1f; // style: 光柱列
            }
        }

        /// <summary>致命审判锁定线（唯一红, style=3）：单发固定方向, 取代旧的四线十字预告以匹配单发审判射线。</summary>
        private void SpawnJudgmentTelegraph(float angle, float length, int life) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                ModContent.ProjectileType<OverseerGroundTelegraph>(), 0, 0f, Main.myPlayer,
                ai0: length, ai1: angle, ai2: NPC.whoAmI);
            if (p >= 0 && p < Main.maxProjectiles) {
                Main.projectile[p].timeLeft = life;
                Main.projectile[p].localAI[0] = 3f; // style: 致命审判线
            }
        }

        private void SpawnDashTelegraph(Vector2 toPos, int life) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            float a = (toPos - NPC.Center).ToRotation();
            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                ModContent.ProjectileType<OverseerGroundTelegraph>(), 0, 0f, Main.myPlayer,
                ai0: 700f, ai1: a, ai2: NPC.whoAmI);
            if (p >= 0 && p < Main.maxProjectiles) {
                Main.projectile[p].timeLeft = life;
                Main.projectile[p].localAI[0] = 0f;
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            DrawDivineAura(spriteBatch, screenPos);
            DrawTrail(spriteBatch, screenPos);
            if (!scryActive) DrawCelestialEyes(spriteBatch, screenPos, drawColor);
            DrawHalo(spriteBatch, screenPos);
            DrawMainBody(spriteBatch, screenPos, drawColor);
            DrawOuterGlow(spriteBatch, screenPos);
            DrawSurveillanceMeter(spriteBatch, screenPos);

            // 处决/十字开火加性泛光（金白权柄）。DrawRadialBloomAt 内部申请全屏名额 —— PreDraw 先于
            // PostDraw 执行, 故开火帧泛光优先取得名额, GenericWarp 折射当帧让位 (§全屏名额仲裁)。
            if (bloomPulse > 0.02f)
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.32f, MathHelper.Clamp(bloomPulse, 0f, 1f), TelegraphColors.Holy, 12f, 2.4f);

            return false;
        }

        /// <summary>
        /// V2 监视/窥视的全屏折射扭曲（GenericWarp · rift 主题 uMode=3）。喂 <see cref="Main.screenTarget"/> 的昂贵
        /// 后处理, 受单一全屏名额约束: 窥视/审判时拉满, 平时随监视槽渐显; 强度过低或名额被泛光占用时直接早退。
        /// 监视暗角 / 全视法阵由 <see cref="OverseerSurveillanceScreenSystem"/> 单独承担。
        /// </summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || surveillanceWarp <= 0.02f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;

            Vector2 centerUV = (NPC.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;

            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(surveillanceWarp, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(0.95f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uWarpScale"]?.SetValue(1.15f);
            fx.Parameters["uChroma"]?.SetValue(0.5f);
            fx.Parameters["uRadialPull"]?.SetValue(0.25f);   // 轻微向内吸 = 被扫描/窥视收束
            fx.Parameters["uMode"]?.SetValue(3f);            // 3 = rift/distort
            fx.Parameters["uTint"]?.SetValue(new Vector4(SurveillanceBlue.ToVector3(), 0.3f));

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        private void DrawSurveillanceMeter(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.BlankStar == null || surveillanceMeter <= 1f) return;
            Texture2D tex = ACMAsset.BlankStar;
            Vector2 center = NPC.Center - screenPos + new Vector2(0, -130);
            int dots = 12;
            int lit = (int)(surveillanceMeter / 100f * dots);
            float t = surveillanceMeter / 100f;
            Color full = Color.Lerp(new Color(255, 240, 160), new Color(255, 70, 60), t);
            for (int i = 0; i < dots; i++) {
                float a = MathHelper.Pi + MathHelper.Pi * i / (dots - 1f);
                Vector2 pos = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 70f;
                Color c = (i < lit ? full : new Color(60, 60, 70)) * 0.9f;
                c.A = 0;
                spriteBatch.Draw(tex, pos, null, c, 0f, tex.Size() / 2f, 0.12f, SpriteEffects.None, 0f);
            }
        }

        private void DrawDivineAura(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.LightShot == null) return;
            Texture2D auraTexture = ACMAsset.LightShot;
            Vector2 drawPos = NPC.Center - screenPos;
            Color auraColor = new Color(255, 240, 180) * divineAuraAlpha;
            auraColor.A = 0;
            float auraScale = 8f * haloScale;
            spriteBatch.Draw(auraTexture, drawPos, null, auraColor, MathHelper.PiOver2, auraTexture.Size() / 2f, auraScale, SpriteEffects.None, 0f);
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
                spriteBatch.Draw(texture, drawPos, null, trailColor, NPC.rotation, texture.Size() / 2f, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawCelestialEyes(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (eyeAngles == null) return;
            Texture2D eyeTexture = CelestialEyeMinion.CelestialOverseerEye ?? ACMAsset.BlankStar;
            if (eyeTexture == null) return;
            for (int i = 0; i < CelestialEyeCount; i++) {
                Vector2 eyePos = GetEyePosition(i) - screenPos;
                // 视线高亮：有视线时偏暖（危险），无视线时偏冷
                Color outerGlow = (eyeHasLOS != null && eyeHasLOS[i]) ? new Color(255, 180, 120) * 0.7f : new Color(160, 200, 255) * 0.5f;
                outerGlow.A = 0;
                spriteBatch.Draw(eyeTexture, eyePos, null, outerGlow, globalTime + i * 0.5f, eyeTexture.Size() / 2f, 0.6f, SpriteEffects.None, 0f);
                Color coreColor = new Color(255, 255, 220);
                coreColor.A = 0;
                spriteBatch.Draw(eyeTexture, eyePos, null, coreColor, -globalTime * 0.5f + i * 0.3f, eyeTexture.Size() / 2f, 0.4f, SpriteEffects.None, 0f);
                if (CelestialEyeMinion.CelestialOverseerEye != null) {
                    spriteBatch.Draw(eyeTexture, eyePos, null, Color.White, 0f, eyeTexture.Size() / 2f, 0.35f, SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawHalo(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.BlankStar == null) return;
            Texture2D haloTexture = ACMAsset.BlankStar;
            Vector2 drawPos = NPC.Center - screenPos;
            for (int i = 0; i < 3; i++) {
                float layerRotation = haloRotation + i * MathHelper.TwoPi / 3f;
                float layerScale = (1.5f + i * 0.3f) * haloScale;
                Color layerColor = new Color(255, 245, 200) * (0.4f - i * 0.1f);
                layerColor.A = 0;
                spriteBatch.Draw(haloTexture, drawPos, null, layerColor, layerRotation, haloTexture.Size() / 2f, layerScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawMainBody(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;
            Color glowColor = new Color(255, 240, 180) * 0.4f * NPC.Opacity;
            glowColor.A = 0;
            for (int i = 0; i < 4; i++) {
                float angle = globalTime * 2f + i * MathHelper.PiOver2;
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 4f;
                spriteBatch.Draw(texture, drawPos + offset, null, glowColor, NPC.rotation, texture.Size() / 2f, NPC.scale * 1.05f, SpriteEffects.None, 0f);
            }
            Color bodyColor = drawColor * NPC.Opacity;
            spriteBatch.Draw(texture, drawPos, null, bodyColor, NPC.rotation, texture.Size() / 2f, NPC.scale, SpriteEffects.None, 0f);
        }

        private void DrawOuterGlow(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.Sparkle == null) return;
            Texture2D sparkleTexture = ACMAsset.Sparkle;
            Vector2 drawPos = NPC.Center - screenPos;
            Color sparkleColor = new Color(255, 250, 220) * 0.3f * glowIntensity;
            sparkleColor.A = 0;
            spriteBatch.Draw(sparkleTexture, drawPos, null, sparkleColor, globalTime * 0.5f, sparkleTexture.Size() / 2f, 2f * haloScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(sparkleTexture, drawPos, null, sparkleColor * 0.5f, -globalTime * 0.3f, sparkleTexture.Size() / 2f, 2.5f * haloScale, SpriteEffects.None, 0f);
        }

        #endregion
    }
}
