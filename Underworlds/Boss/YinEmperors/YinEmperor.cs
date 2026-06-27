using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using AncientChineseMythology.Systems;
using AncientChineseMythology.Underworlds.Boss.YinEmperors.Items;
using AncientChineseMythology.Underworlds.Items.Materials;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors
{
    /// <summary>
    /// 阴天子 - 冥府终局 Boss（T52 · G7 准圣门控）。
    ///
    /// 重做定位（见 docs/BOSS_REDO_PLAN.md §6.4）：这是一场**审判**而非弹幕战。
    /// 不再是随机 5 状态池，而是**三幕脚本化结构**，每一幕改“规则”而非“数值”：
    ///   · 幕一 酆都仪典 (100-66%)：悬浮→龙气横扫→冥谕(列阵) 固定轮替；引入“冥律标记/定魂”。
    ///   · 幕二 镇魂狱 (66-33%)：镇魂封印成为强制阶段——冥眼收缩，必须击破“鬼门关钥”弱点逃脱；冥谕换 2/3 阵型。
    ///   · 幕三 帝裁 (33-0%)：固定循环 帝怒→酆帝诏书(阴阳半场)→终诏(十字激光+弹·一次性 4s 预告)。
    /// G7 联动：~10% 召唤阴帝印幻象，装备酆都套的玩家可在 ≤18% 时处决。
    /// </summary>
    [AutoloadBossHead]
    public class YinEmperor : ModNPC
    {
        #region 常量

        private const int MaxFrames = 3;
        private const int FrameSpeed = 8;
        private const int IntroRiseDuration = 180;
        private const int IntroPauseDuration = 60;
        private const int IntroRoarDuration = 40;
        private const int IntroLightningCount = 6;
        private const float IntroRiseDistance = 900f;
        private const float HoverHeight = 280f;

        // 幕（HP 门）阈值
        private const float Phase2Threshold = 0.66f;
        private const float Phase3Threshold = 0.33f;
        private const float ExecutionThreshold = 0.10f;
        /// <summary>装备酆都套时可处决的血量阈值</summary>
        public const float FengduExecuteThreshold = 0.18f;

        /// <summary>镇魂封印收缩总时长（鬼门关钥与冥眼共用，确保同步）</summary>
        public const int SealContractTime = 360;

        #endregion

        #region 全局审判状态（供 ModPlayer / Sky / 冥眼读取，AI 每帧维护）

        /// <summary>酆帝诏书（阴阳半场）是否生效</summary>
        public static bool YinYangActive;
        /// <summary>阴阳分界 X 坐标（世界坐标）</summary>
        public static float YinYangCenterX;
        /// <summary>安全半场：0=阴(左)，1=阳(右)</summary>
        public static int YinYangSafeSide;
        /// <summary>阴阳即将切换的预警</summary>
        public static bool YinYangWarning;

        /// <summary>终诏十字激光预告进度 0..1（0=未激活）</summary>
        public static float FinalDecreeCharge;

        /// <summary>处决窗口是否开启（阴帝印幻象阶段后常驻）</summary>
        public static bool ExecutionWindowOpen;

        #endregion

        #region AI阶段系统

        private enum AIState
        {
            Intro,

            // 幕一 酆都仪典
            Act1_Hover,
            Act1_DragonSweep,
            Act1_NetherDecree,

            ActTransition2,

            // 幕二 镇魂狱
            Act2_SoulSeal,
            Act2_NetherDecree,

            ActTransition3,

            // 幕三 帝裁
            Act3_ImperialWrath,
            Act3_YinYangEdict,
            Act3_FinalDecree,

            // G7 处决
            ExecutionPhantom
        }

        private AIState CurrentState {
            get => (AIState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        private ref float PhaseTimer => ref NPC.ai[1];
        private ref float AttackTimer => ref NPC.ai[2];
        private ref float ActStep => ref NPC.ai[3];

        // 需要同步的逻辑状态（ai[] 之外）
        private bool didAct2;
        private bool didAct3;
        private bool didExecution;
        private int decreeFormation;

        /// <summary>镇魂封印中心（固定）</summary>
        public Vector2 SealCenter;
        /// <summary>封印状态：0=收缩中，1=已破(逃脱)，2=超时合拢(处决性合击)</summary>
        public int SealState;
        private bool sealWeakSpawned;
        private int sealResolveDelay;

        private float LifeRatio => NPC.lifeMax <= 0 ? 1f : (float)NPC.life / NPC.lifeMax;

        #endregion

        #region 状态变量

        private int seed = -1;
        private Random random;
        private int frameCounter;
        private int currentFrame;

        // 出场演出
        private float introProgress;
        private bool introRoarDone;
        private bool introLightningDone;
        private float introPillarAlpha;
        private float introShakeIntensity;

        // 视觉效果
        private float pulsePhase;
        private float auraRotation;
        private float auraIntensity;
        private float hoverOffset;
        private float[] energyWaveRadius = new float[3];
        private float[] energyWaveAlpha = new float[3];

        // 战斗参数
        private int dashCount;
        private Vector2 dashTarget;
        private int sweepDirection;
        private int yinYangBaseSide;

        // 法环
        private float ringRotation;
        private float ringScale;
        private float ringAlpha;
        private float phantomSealScale;

        // V2 演出层（纯本地视觉）
        /// <summary>阴阳分屏 PaletteLUT 的平滑强度（PostDraw 消费唯一全屏名额）。</summary>
        private float yinYangVisual;
        /// <summary>大节拍泛光脉冲 0..1（出场吼/过场/终诏/处决/死亡触发，逐帧衰减）。</summary>
        private float bloomPulse;
        /// <summary>当前泛光色（RGB）。</summary>
        private Vector3 bloomColorV = Vector3.One;

        #endregion

        #region 目标获取

        private Player Target {
            get {
                if (NPC.target < 0 || NPC.target >= Main.maxPlayers ||
                    Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                    NPC.TargetClosest();
                return Main.player[NPC.target];
            }
        }

        #endregion

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = MaxFrames;
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 12;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 120;
            NPC.height = 160;
            NPC.damage = 220;
            NPC.defense = 95;
            NPC.lifeMax = 12000000;
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = 500000f;
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.aiStyle = -1;
            NPC.npcSlots = 20f;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.75f * balance * bossAdjustment);
            NPC.damage = (int)(NPC.damage * 0.8f);
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<YinImperialSeal>()));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<YinEssence>(), 1, 18, 24));
            npcLoot.Add(ItemDropRule.Common(ItemID.SuperHealingPotion, 1, 25, 40));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<FengduImperialCrown>(),
                ModContent.ItemType<SoulBannerUnderworldRelic>(),
                ModContent.ItemType<GhostGateKey>()
            ));
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            return base.DrawHealthBar(hbPosition, ref scale, ref position);
        }

        public override bool CheckActive() => false;

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(0, 10000);
            random = new Random(seed);
            CurrentState = AIState.Intro;
            PhaseTimer = 0;
            AttackTimer = 0;
            ActStep = 0;
            introProgress = 0f;
            introRoarDone = false;
            introLightningDone = false;
            introPillarAlpha = 0f;
            auraIntensity = 0f;
            yinYangBaseSide = Main.rand.Next(2);

            ResetGlobalState();

            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
            }
        }

        private void ResetGlobalState() {
            YinYangActive = false;
            YinYangWarning = false;
            FinalDecreeCharge = 0f;
            ExecutionWindowOpen = false;
            SealState = 0;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
            writer.Write(introProgress);
            writer.Write(pulsePhase);
            writer.Write(dashCount);
            writer.Write(didAct2);
            writer.Write(didAct3);
            writer.Write(didExecution);
            writer.Write(decreeFormation);
            writer.Write(SealState);
            writer.Write(sealWeakSpawned);
            writer.Write(SealCenter.X);
            writer.Write(SealCenter.Y);
            writer.Write(yinYangBaseSide);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            introProgress = reader.ReadSingle();
            pulsePhase = reader.ReadSingle();
            dashCount = reader.ReadInt32();
            didAct2 = reader.ReadBoolean();
            didAct3 = reader.ReadBoolean();
            didExecution = reader.ReadBoolean();
            decreeFormation = reader.ReadInt32();
            SealState = reader.ReadInt32();
            sealWeakSpawned = reader.ReadBoolean();
            SealCenter.X = reader.ReadSingle();
            SealCenter.Y = reader.ReadSingle();
            yinYangBaseSide = reader.ReadInt32();
            random ??= new Random(seed);
        }

        #region 帧动画

        public override void FindFrame(int frameHeight) {
            frameCounter++;
            if (frameCounter >= FrameSpeed) {
                frameCounter = 0;
                currentFrame++;
                if (currentFrame >= MaxFrames)
                    currentFrame = 0;
            }
            NPC.frame.Y = currentFrame * frameHeight;
        }

        #endregion

        #region 主AI

        public override void AI() {
            UnderworldPlayer.UnderworldEffect = true;
            random ??= new Random(seed);

            // 视觉效果更新
            pulsePhase += 0.06f;
            auraRotation += 0.015f;
            ringRotation += 0.008f;
            hoverOffset = MathF.Sin(pulsePhase * 0.4f) * 8f;
            UpdateEnergyWaves();

            if (CurrentState != AIState.Intro) {
                ringScale = MathHelper.Lerp(ringScale, 2.5f, 0.01f);
                ringAlpha = MathHelper.Lerp(ringAlpha, 0.7f, 0.015f);
            }

            // 目标验证
            NPC.TargetClosest();
            Player target = Target;
            if (!target.active || target.dead) {
                ResetGlobalState();
                NPC.velocity.Y -= 0.5f;
                NPC.alpha += 3;
                if (NPC.alpha > 255 || NPC.timeLeft < 10) {
                    NPC.active = false;
                }
                return;
            }

            float lightIntensity = 0.8f + auraIntensity * 0.4f;
            Lighting.AddLight(NPC.Center, YinEmperorHelper.ImperialGold.ToVector3() * lightIntensity * 0.4f);
            Lighting.AddLight(NPC.Center, YinEmperorHelper.AbyssPurple.ToVector3() * lightIntensity * 0.3f);

            PhaseTimer++;
            AttackTimer++;

            // 幕（HP 门）切换检测：改“规则”，并加入过场无敌节拍
            CheckActTransitions(target);

            // 每帧默认清掉只在特定阶段生效的全局状态（各 Run 内会重新置位）
            if (CurrentState != AIState.Act3_YinYangEdict) {
                YinYangActive = false;
                YinYangWarning = false;
            }
            if (CurrentState != AIState.Act3_FinalDecree)
                FinalDecreeCharge = 0f;

            switch (CurrentState) {
                case AIState.Intro: RunIntro(target); break;

                case AIState.Act1_Hover: RunHover(target); break;
                case AIState.Act1_DragonSweep: RunDragonSweep(target); break;
                case AIState.Act1_NetherDecree: RunNetherDecree(target); break;

                case AIState.ActTransition2: RunActTransition(target, 2); break;

                case AIState.Act2_SoulSeal: RunSoulSeal(target); break;
                case AIState.Act2_NetherDecree: RunNetherDecree(target); break;

                case AIState.ActTransition3: RunActTransition(target, 3); break;

                case AIState.Act3_ImperialWrath: RunImperialWrath(target); break;
                case AIState.Act3_YinYangEdict: RunYinYangEdict(target); break;
                case AIState.Act3_FinalDecree: RunFinalDecree(target); break;

                case AIState.ExecutionPhantom: RunExecutionPhantom(target); break;
            }

            if (CurrentState != AIState.Intro) {
                CreateAmbientParticles();
            }

            UpdateV2Presentation();
        }

        #endregion

        #region V2 演出驱动（纯本地视觉，AI 全端运行故多人客户端亦可见）

        /// <summary>触发一次大节拍泛光（取 max 不累加）。</summary>
        private void TriggerBloom(float intensity, Color color) {
            if (intensity > bloomPulse) {
                bloomPulse = intensity;
                bloomColorV = color.ToVector3();
            }
        }

        /// <summary>每帧推进阴阳分屏强度 / 泛光衰减，并向屏幕系统发布审判演出标量。</summary>
        private void UpdateV2Presentation() {
            bloomPulse = MathHelper.Lerp(bloomPulse, 0f, 0.12f);
            if (bloomPulse < 0.01f)
                bloomPulse = 0f;

            // 阴阳分屏强度：站错半场预警时加深
            float yyTarget = 0f;
            if (YinYangActive) {
                yyTarget = 0.42f + (YinYangWarning ? 0.16f : 0f);
            }
            yinYangVisual = MathHelper.Lerp(yinYangVisual, yyTarget, 0.06f);

            if (Main.dedServ)
                return;

            // decree-vignette 强度（帝裁/处决渐强；其余幕无）
            float vig = CurrentState switch {
                AIState.Act3_ImperialWrath => 0.22f,
                AIState.Act3_YinYangEdict => 0.16f,
                AIState.Act3_FinalDecree => 0.25f + 0.45f * MathHelper.Clamp(FinalDecreeCharge, 0f, 1f),
                AIState.ExecutionPhantom => 0.5f,
                AIState.Act2_SoulSeal => SealState == 0 ? 0.3f : 0.15f,
                _ => 0f
            };
            if (ExecutionWindowOpen)
                vig = System.Math.Max(vig, 0.32f);

            // prison-overlay（镇魂狱收缩牢笼）+ 鬼门关钥弱点高亮
            float prison = 0f;
            Vector2 prisonCenter = SealCenter;
            float prisonRadius = 0f;
            float weak = 0f;
            Vector2 weakPos = SealCenter;
            if (CurrentState == AIState.Act2_SoulSeal && SealState == 0 && PhaseTimer >= 60f) {
                float progress = MathHelper.Clamp((PhaseTimer - 60f) / SealContractTime, 0f, 1f);
                prisonRadius = MathHelper.Lerp(520f, 110f, ACMUtils.SineInOut(progress));
                prison = 1f;
                NPC lock_ = FindGhostGateLock();
                if (lock_ != null) {
                    weak = 1f;
                    weakPos = lock_.Center;
                }
            }

            YinEmperorScreenSystem.Publish((float)Main.GlobalTimeWrappedHourly, vig,
                prison, prisonCenter, prisonRadius, weak, weakPos);
        }

        private static NPC FindGhostGateLock() {
            int lockType = ModContent.NPCType<GhostGateLock>();
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == lockType)
                    return n;
            }
            return null;
        }

        #endregion

        #region 幕切换 / 调度

        private void CheckActTransitions(Player target) {
            bool midCinematic = CurrentState is AIState.Intro or AIState.ActTransition2
                or AIState.ActTransition3 or AIState.ExecutionPhantom;
            if (midCinematic)
                return;

            if (!didExecution && LifeRatio <= ExecutionThreshold) {
                didExecution = true;
                BeginExecutionPhantom();
                return;
            }
            if (!didAct3 && LifeRatio <= Phase3Threshold) {
                didAct3 = true;
                TransitionTo(AIState.ActTransition3);
                return;
            }
            if (!didAct2 && LifeRatio <= Phase2Threshold) {
                didAct2 = true;
                TransitionTo(AIState.ActTransition2);
                return;
            }
        }

        private void TransitionTo(AIState newState) {
            CurrentState = newState;
            PhaseTimer = 0;
            AttackTimer = 0;
            dashCount = 0;
            NPC.dontTakeDamage = false;
            NPC.netUpdate = true;
        }

        // 幕一 固定轮替：悬浮 -> 龙气横扫 -> 冥谕(列阵)
        private void NextAct1() {
            ActStep = (int)ActStep % 3 + 1;
            switch ((int)ActStep % 3) {
                case 1: TransitionTo(AIState.Act1_DragonSweep); break;
                case 2: decreeFormation = 0; TransitionTo(AIState.Act1_NetherDecree); break;
                default: TransitionTo(AIState.Act1_Hover); break;
            }
        }

        // 幕二 固定轮替：镇魂封印(强制) -> 冥谕(环形) -> 镇魂封印 -> 冥谕(十字)
        private void NextAct2() {
            ActStep++;
            switch ((int)ActStep % 4) {
                case 1: decreeFormation = 1; TransitionTo(AIState.Act2_NetherDecree); break;
                case 3: decreeFormation = 2; TransitionTo(AIState.Act2_NetherDecree); break;
                default: TransitionTo(AIState.Act2_SoulSeal); break;
            }
        }

        // 幕三 固定循环：帝怒 -> 酆帝诏书 -> 终诏
        private void NextAct3() {
            ActStep = (int)ActStep % 3 + 1;
            switch ((int)ActStep % 3) {
                case 1: TransitionTo(AIState.Act3_YinYangEdict); break;
                case 2: TransitionTo(AIState.Act3_FinalDecree); break;
                default: TransitionTo(AIState.Act3_ImperialWrath); break;
            }
        }

        private void AdvanceCurrentAct() {
            if (!didAct2) NextAct1();
            else if (!didAct3) NextAct2();
            else NextAct3();
        }

        private static void Speak(string key, Color color) {
            if (Main.dedServ) return;
            string text = Language.GetTextValue(key);
            Main.NewText(text, color);
        }

        private void ClearHostileProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int edge = ModContent.ProjectileType<ArenaEdge>();
            int bolt = ModContent.ProjectileType<YinEmperorBolt>();
            int laser = ModContent.ProjectileType<YinEmperorLaser>();
            foreach (var p in Main.projectile) {
                if (p.active && (p.type == edge || p.type == bolt || p.type == laser))
                    p.Kill();
            }
            int lockType = ModContent.NPCType<GhostGateLock>();
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == lockType) {
                    n.life = 0;
                    n.active = false;
                }
            }
        }

        private void GrantBreatherIFrames() {
            foreach (var p in Main.player) {
                if (p != null && p.active && !p.dead && p.Distance(NPC.Center) < 3000f) {
                    p.immune = true;
                    p.immuneTime = Math.Max(p.immuneTime, 80);
                }
            }
        }

        #endregion

        #region 出场演出

        private void RunIntro(Player target) {
            int totalIntroDuration = IntroRiseDuration + IntroPauseDuration + IntroRoarDuration;

            if (PhaseTimer == 1)
                Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.Intro", YinEmperorHelper.ImperialGold);

            if (PhaseTimer <= IntroRiseDuration) {
                float riseT = MathHelper.Clamp(PhaseTimer / IntroRiseDuration, 0f, 1f);
                introProgress = ACMUtils.SineInOut(riseT);

                Vector2 startPos = target.Center + new Vector2(0, IntroRiseDistance);
                Vector2 endPos = target.Center + new Vector2(0, -HoverHeight);
                Vector2 desired = Vector2.Lerp(startPos, endPos, introProgress);

                NPC.Center += (desired - NPC.Center) * 0.08f;
                NPC.velocity *= 0.85f;
                NPC.alpha = (int)(255 * (1f - introProgress * 0.8f));
                introPillarAlpha = introProgress * 0.8f;

                if (Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0) {
                    YinEmperorHelper.CreateImperialTrail(NPC.Center, NPC.velocity, 1.5f);
                }

                if (Main.netMode != NetmodeID.Server && PhaseTimer % 8 == 0) {
                    for (int i = 0; i < 2; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = 80f + Main.rand.NextFloat(40f);
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Shadowflame;
                        var d = Dust.NewDustPerfect(dustPos, dustType);
                        d.noGravity = true;
                        d.scale = 1.5f + introProgress;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                        d.alpha = 80;
                    }
                }

                if (!introLightningDone && PhaseTimer > 40 && PhaseTimer % 25 == 0 && Main.netMode != NetmodeID.Server) {
                    float lightningX = NPC.Center.X + Main.rand.NextFloat(-400f, 400f);
                    Vector2 lightningTop = new Vector2(lightningX, NPC.Center.Y - 600f);
                    Vector2 lightningBottom = new Vector2(lightningX + Main.rand.NextFloat(-60, 60), NPC.Center.Y + 200f);
                    YinEmperorHelper.CreateNetherLightningPillar(lightningTop, lightningBottom, 0.8f);
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.6f, Volume = 0.8f }, new Vector2(lightningX, NPC.Center.Y));
                }

                if (PhaseTimer == IntroRiseDuration)
                    introLightningDone = true;

                auraIntensity = MathHelper.Lerp(auraIntensity, introProgress * 0.6f, 0.02f);
            }
            else if (PhaseTimer <= IntroRiseDuration + IntroPauseDuration) {
                float pauseT = (PhaseTimer - IntroRiseDuration) / IntroPauseDuration;

                Vector2 hoverPos = target.Center + new Vector2(0, -HoverHeight);
                NPC.Center += (hoverPos - NPC.Center) * 0.05f;
                NPC.velocity *= 0.9f;
                NPC.alpha = (int)(255 * 0.2f * (1f - pauseT));

                if (Main.netMode != NetmodeID.Server) {
                    float chargeRadius = 200f * (1f - pauseT);
                    int chargeCount = (int)(6 * pauseT) + 2;
                    for (int i = 0; i < chargeCount; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * chargeRadius;
                        Vector2 dustVel = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * (6f + pauseT * 8f);
                        int dustType = Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.Shadowflame;
                        var d = Dust.NewDustPerfect(dustPos, dustType);
                        d.noGravity = true;
                        d.scale = 1.8f + pauseT;
                        d.velocity = dustVel;
                    }
                }

                introPillarAlpha = 0.8f + pauseT * 0.2f;
                auraIntensity = MathHelper.Lerp(auraIntensity, 0.8f, 0.03f);

                if (PhaseTimer == IntroRiseDuration + 1)
                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.8f, Volume = 0.6f }, NPC.Center);
            }
            else if (PhaseTimer <= totalIntroDuration) {
                float roarT = (PhaseTimer - IntroRiseDuration - IntroPauseDuration) / (float)IntroRoarDuration;
                NPC.alpha = 0;

                Vector2 hoverPos = target.Center + new Vector2(0, -HoverHeight);
                NPC.Center += (hoverPos - NPC.Center) * 0.03f;
                NPC.velocity *= 0.95f;

                if (!introRoarDone) {
                    introRoarDone = true;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.8f }, NPC.Center);
                    YinEmperorHelper.CreateImperialVortex(NPC.Center, 250f, 2f, 80);
                    YinEmperorHelper.CreateDragonBurst(NPC.Center, 200f, 5, 24);
                    YinEmperorHelper.CreateTalismanBurst(NPC.Center, 300f, 40);
                    for (int i = 0; i < 3; i++) TriggerEnergyWave();
                    ACMScreenShakeSystem.Add(16f);
                    TriggerBloom(0.95f, YinEmperorHelper.DragonVeinGold);
                    YinEmperorHelper.CreateScreenFlash(NPC.Center, YinEmperorHelper.DragonVeinGold, 1.2f);

                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < IntroLightningCount; i++) {
                            float angle = MathHelper.TwoPi * i / IntroLightningCount;
                            float dist = 300f + Main.rand.NextFloat(100f);
                            Vector2 strikePos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                            Vector2 lightningTop = strikePos - new Vector2(0, 800f);
                            YinEmperorHelper.CreateNetherLightningPillar(lightningTop, strikePos, 1.2f);
                        }
                    }
                }

                introShakeIntensity = (1f - roarT) * 8f;
                if (introShakeIntensity > 0.5f)
                    ACMScreenShakeSystem.Add(introShakeIntensity);

                introPillarAlpha = 1f - roarT * 0.8f;
                auraIntensity = MathHelper.Lerp(auraIntensity, 1f, 0.05f);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < (int)(8 * (1f - roarT)); i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(18f, 18f) * (1f - roarT * 0.5f);
                        int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Shadowflame;
                        var d = Dust.NewDustPerfect(NPC.Center, dustType);
                        d.noGravity = true;
                        d.scale = 2.5f * (1f - roarT * 0.5f);
                        d.velocity = vel;
                    }
                }
            }
            else {
                introPillarAlpha = 0f;
                auraIntensity = 1f;
                ActStep = 0;
                Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.ActI", YinEmperorHelper.ImperialGold);
                TransitionTo(AIState.Act1_Hover);
            }
        }

        #endregion

        #region 幕过场（i-frame 节拍 + 规则切换）

        private void RunActTransition(Player target, int act) {
            NPC.dontTakeDamage = true;

            Vector2 hoverPos = target.Center + new Vector2(0, -HoverHeight);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.06f, 0.1f);

            if (PhaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.6f }, NPC.Center);
                YinEmperorHelper.CreateImperialVortex(NPC.Center, 280f, 2.2f, 90);
                YinEmperorHelper.CreateTalismanBurst(NPC.Center, 320f, 45);
                for (int i = 0; i < 3; i++) TriggerEnergyWave();
                ACMScreenShakeSystem.Add(12f);
                TriggerBloom(0.85f, YinEmperorHelper.NetherBloodRed);
                YinEmperorHelper.CreateScreenFlash(NPC.Center, YinEmperorHelper.DragonVeinGold, 1f);
                ClearHostileProjectiles();
                GrantBreatherIFrames();
                ResetGlobalState();

                Speak(act == 2
                        ? "Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.ActII"
                        : "Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.ActIII",
                    YinEmperorHelper.NetherBloodRed);
            }

            auraIntensity = MathHelper.Lerp(auraIntensity, 1.6f, 0.04f);

            if (Main.netMode != NetmodeID.Server && PhaseTimer % 5 == 0) {
                YinEmperorHelper.CreateImperialTrail(NPC.Center, Vector2.Zero, 1.4f);
            }

            if (PhaseTimer >= 110) {
                NPC.dontTakeDamage = false;
                auraIntensity = 1f;
                ActStep = 0;
                if (act == 2)
                    TransitionTo(AIState.Act2_SoulSeal);
                else
                    TransitionTo(AIState.Act3_ImperialWrath);
            }
        }

        #endregion

        #region 幕一 行为

        /// <summary>帝冥悬浮（已削弱常驻喷射）：缓慢悬浮 + 少量可读弹幕 + 一次地面符文预告。</summary>
        private void RunHover(Player target) {
            float swayX = MathF.Sin(PhaseTimer * 0.02f) * 180f;
            Vector2 hoverPos = target.Center + new Vector2(swayX, -HoverHeight + hoverOffset);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.05f, 0.06f);

            // 削弱后的帝冥弹（仅作压制走位，非 DPS 主力）
            if (AttackTimer % 110 == 60) {
                ShootImperialBolts(target, 3);
            }

            // 一次地面符文（telegraph）
            if (PhaseTimer == 130) {
                ShootGroundSeals(target);
            }

            if (PhaseTimer > 230) {
                NextAct1();
            }
        }

        private void RunDragonSweep(Player target) {
            if (PhaseTimer <= 40) {
                NPC.velocity *= 0.9f;
                if (PhaseTimer == 20) {
                    dashTarget = target.Center + target.velocity * 20f;
                    sweepDirection = target.Center.X > NPC.Center.X ? 1 : -1;
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
                    YinEmperorHelper.CreateImperialVortex(NPC.Center, 80f, 0.8f, 25);
                }
                if (PhaseTimer > 20 && Main.netMode != NetmodeID.Server) {
                    float chargeT = (PhaseTimer - 20) / 20f;
                    for (int i = 0; i < 3; i++) {
                        float radius = 120f * (1f - chargeT);
                        Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(radius, radius);
                        Vector2 dustVel = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * (4f + chargeT * 6f);
                        var d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame);
                        d.noGravity = true;
                        d.scale = 1.5f + chargeT;
                        d.velocity = dustVel;
                    }
                }
            }
            else if (PhaseTimer == 41) {
                Vector2 direction = (dashTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                NPC.velocity = direction * 35f;
                dashCount++;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);
                TriggerEnergyWave();
                YinEmperorHelper.CreateDragonBurst(NPC.Center, 60f, 2, 10);
            }
            else if (PhaseTimer <= 65) {
                YinEmperorHelper.CreateImperialTrail(NPC.Center, NPC.velocity, 2f);
                if (PhaseTimer > 55) NPC.velocity *= 0.92f;
            }
            else {
                NPC.velocity *= 0.9f;
                if (dashCount < 3 && PhaseTimer == 80) {
                    PhaseTimer = 0;
                    AttackTimer = 0;
                }
                else if (PhaseTimer > 90) {
                    dashCount = 0;
                    NextAct1();
                }
            }
        }

        /// <summary>冥谕降罚 - 冥眼激光阵列；formation 由各幕指定（幕一只用 0）。</summary>
        private void RunNetherDecree(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -400f + hoverOffset);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.06f);

            if (PhaseTimer < 60) {
                auraIntensity = MathHelper.Lerp(auraIntensity, 1.5f, 0.02f);
                if (Main.netMode != NetmodeID.Server && PhaseTimer % 6 == 0)
                    YinEmperorHelper.CreateImperialTrail(NPC.Center, Vector2.Zero, 1f);
            }

            int formation = decreeFormation % 3;

            if (PhaseTimer == 60) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    switch (formation) {
                        case 0: SpawnEyeLaserFormation(target, -1); break;
                        case 1: SpawnRingLaserFormation(target, 6); break;
                        case 2: SpawnCrossLaserFormation(target, 8); break;
                    }
                }
                if (!Main.dedServ) {
                    switch (formation) {
                        case 0: YinEmperorScreenSystem.AddTelegraph(target.Center + new Vector2(-400f, 0f), 260f, 110, TelegraphColors.NetherViolet); break;
                        case 1: YinEmperorScreenSystem.AddTelegraph(NPC.Center, 450f, 130, TelegraphColors.NetherViolet); break;
                        case 2: YinEmperorScreenSystem.AddTelegraph(NPC.Center, 380f, 130, TelegraphColors.NetherViolet); break;
                    }
                }
            }

            if (PhaseTimer == 130 && formation == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    SpawnEyeLaserFormation(target, 1);
                if (!Main.dedServ)
                    YinEmperorScreenSystem.AddTelegraph(target.Center + new Vector2(400f, 0f), 260f, 110, TelegraphColors.NetherViolet);
            }

            if (PhaseTimer >= 60 && PhaseTimer <= 200 && PhaseTimer % 35 == 0 && Main.netMode != NetmodeID.Server) {
                float lightningX = target.Center.X + Main.rand.NextFloat(-300f, 300f);
                Vector2 top = new Vector2(lightningX, target.Center.Y - 600f);
                Vector2 bottom = new Vector2(lightningX, target.Center.Y + 100f);
                YinEmperorHelper.CreateNetherLightningPillar(top, bottom, 0.8f);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.4f, Volume = 0.8f }, new Vector2(lightningX, target.Center.Y));
            }

            auraIntensity = MathHelper.Lerp(auraIntensity, 1f, 0.01f);

            if (PhaseTimer > 280) {
                if (CurrentState == AIState.Act1_NetherDecree) NextAct1();
                else NextAct2();
            }
        }

        #endregion

        #region 幕二 镇魂狱（强制封印 + 鬼门关钥弱点）

        private void RunSoulSeal(Player target) {
            // 收缩期间 Boss 无敌，强制玩家去破弱点逃脱
            if (SealState == 0)
                NPC.dontTakeDamage = true;

            Vector2 anchor = SealCenter + new Vector2(0, -360f);
            if (PhaseTimer <= 1) anchor = target.Center + new Vector2(0, -360f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (anchor - NPC.Center) * 0.05f, 0.05f);

            // 蓄力预告
            if (PhaseTimer < 60) {
                if (Main.netMode != NetmodeID.Server && PhaseTimer % 5 == 0) {
                    int sealPoints = 8;
                    float sealRadius = 30f + PhaseTimer * 3f;
                    for (int i = 0; i < sealPoints; i++) {
                        float angle = MathHelper.TwoPi * i / sealPoints + PhaseTimer * 0.05f;
                        Vector2 pos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * sealRadius;
                        var d = Dust.NewDustPerfect(pos, DustID.GoldFlame);
                        d.noGravity = true;
                        d.scale = 1.5f;
                        d.velocity = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 2f;
                    }
                }
                if (PhaseTimer == 30) {
                    Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.SoulSeal", YinEmperorHelper.SoulLanternCyan);
                }
            }
            // 封印激活：固定中心 + 冥眼收缩环 + 鬼门关钥弱点
            else if (PhaseTimer == 60) {
                SealCenter = target.Center;
                SealState = 0;
                sealWeakSpawned = false;
                NPC.netUpdate = true;

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    SpawnSealRing(7);
                    SpawnGhostGateLock();
                    sealWeakSpawned = true;
                }

                YinEmperorHelper.CreateDragonBurst(NPC.Center, 120f, 3, 20);
                TriggerEnergyWave();
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 1.3f }, target.Center);
            }
            // 收缩期间：检测弱点是否被击破
            else if (PhaseTimer > 70 && SealState == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient && sealWeakSpawned
                    && !NPC.AnyNPCs(ModContent.NPCType<GhostGateLock>())) {
                    // 鬼门关钥被击破 -> 封印瓦解，开输出窗口
                    SealState = 1;
                    sealResolveDelay = 70;
                    NPC.netUpdate = true;
                }
                else if (PhaseTimer - 60 >= SealContractTime) {
                    // 超时合拢 -> 处决性合击（由冥眼执行），玩家承受重击
                    SealState = 2;
                    sealResolveDelay = 70;
                    NPC.netUpdate = true;
                }
            }

            // 破封反馈
            if (SealState == 1 && PhaseTimer % 60 == 0 && Main.netMode != NetmodeID.Server) {
                YinEmperorHelper.CreateTalismanBurst(SealCenter, 200f, 30);
            }

            if (SealState != 0) {
                NPC.dontTakeDamage = false;
                if (SealState == 1) {
                    // 破封：阴天子短暂破绽（更靠近玩家便于输出）
                    Vector2 lure = target.Center + new Vector2(0, -220f);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (lure - NPC.Center) * 0.06f, 0.08f);
                }
                sealResolveDelay--;
                if (sealResolveDelay <= 0) {
                    if (SealState == 1)
                        Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.SealBroken", YinEmperorHelper.SoulLanternCyan);
                    SealState = 0;
                    NextAct2();
                }
            }
        }

        #endregion

        #region 幕三 帝裁

        /// <summary>帝怒 - 守卫冥眼 + 削弱后的追踪弹（高潮收束，非常驻喷射）。</summary>
        private void RunImperialWrath(Player target) {
            Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            NPC.velocity = Vector2.Lerp(NPC.velocity, toPlayer * 7f, 0.1f);
            YinEmperorHelper.CreateImperialTrail(NPC.Center, NPC.velocity, 1.5f);

            if (PhaseTimer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                SpawnGuardianEyes(4);
            }

            if (AttackTimer % 60 == 0) {
                ShootImperialBolts(target, 3);
            }

            if (AttackTimer % 110 == 55 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = 6;
                int damage = YinEmperorHelper.GetScaledDamage(70);
                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi * i / count + PhaseTimer * 0.01f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, angle.ToRotationVector2() * 9f,
                        ModContent.ProjectileType<YinEmperorBolt>(), damage, 1f, Main.myPlayer);
                }
                TriggerEnergyWave();
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.1f, Volume = 1.1f }, NPC.Center);
            }

            if (Main.rand.NextBool(3) && Main.netMode != NetmodeID.Server) {
                var d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(50f, 50f), DustID.GoldFlame);
                d.noGravity = true;
                d.scale = 1.8f;
                d.velocity = Main.rand.NextVector2Circular(3, 3);
            }

            if (PhaseTimer > 300) {
                NextAct3();
            }
        }

        /// <summary>酆帝诏书 - 阴阳半场：站错半场持续灼魂 DoT；安全半场定期切换（预告）。</summary>
        private void RunYinYangEdict(Player target) {
            Vector2 hoverPos = new Vector2(YinYangCenterX, target.Center.Y - 360f + hoverOffset);
            if (PhaseTimer <= 1) hoverPos = new Vector2(target.Center.X, target.Center.Y - 360f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.05f, 0.06f);

            const int cycle = 165;
            int totalDuration = cycle * 3;

            if (PhaseTimer == 1) {
                YinYangCenterX = target.Center.X;
                Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.YinYang", YinEmperorHelper.DragonVeinGold);
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
                NPC.netUpdate = true;
            }

            YinYangActive = true;
            int cycleIndex = (int)(PhaseTimer / cycle);
            YinYangSafeSide = (yinYangBaseSide + cycleIndex) % 2;
            float intoCycle = PhaseTimer % cycle;
            YinYangWarning = (cycle - intoCycle) < 50f && PhaseTimer > 30;

            // 切换瞬间反馈
            if ((int)PhaseTimer % cycle == 0 && PhaseTimer > 1) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server)
                    YinEmperorHelper.CreateTalismanBurst(NPC.Center, 200f, 24);
            }

            // 适度施压，逼迫走位（非 DPS 主力）
            if (AttackTimer % 75 == 0) {
                ShootImperialBolts(target, 2);
            }

            if (PhaseTimer > totalDuration) {
                YinYangActive = false;
                YinYangWarning = false;
                NextAct3();
            }
        }

        /// <summary>终诏 - 十字激光 + 弹幕，一次性，~4s 预告。终结“无限悬浮喷弹”。</summary>
        private void RunFinalDecree(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -360f + hoverOffset);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.05f, 0.06f);

            const int telegraph = 240;

            if (PhaseTimer == 1) {
                Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.FinalDecree", YinEmperorHelper.NetherBloodRed);
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.6f, Volume = 1f }, NPC.Center);
                if (!Main.dedServ)
                    YinEmperorScreenSystem.AddTelegraph(NPC.Center, 430f, telegraph, TelegraphColors.Execution);
            }

            if (PhaseTimer <= telegraph) {
                FinalDecreeCharge = PhaseTimer / (float)telegraph;
                auraIntensity = MathHelper.Lerp(auraIntensity, 1.4f, 0.02f);
                if (Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0) {
                    float r = 220f * (1f - FinalDecreeCharge);
                    Vector2 dp = NPC.Center + Main.rand.NextVector2Circular(r, r);
                    var d = Dust.NewDustPerfect(dp, Main.rand.NextBool() ? DustID.GoldFlame : DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 1.5f;
                    d.velocity = (NPC.Center - dp).SafeNormalize(Vector2.Zero) * 5f;
                }
                if (PhaseTimer == telegraph - 30)
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f, Volume = 0.9f }, NPC.Center);
            }
            else if (PhaseTimer == telegraph + 1) {
                FinalDecreeCharge = 1f;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int laserDmg = YinEmperorHelper.GetScaledDamage(120);
                    // 十字 + X 八向激光，一次性
                    for (int i = 0; i < 8; i++) {
                        float angle = MathHelper.PiOver4 * i;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<YinEmperorLaser>(), laserDmg, 2f, Main.myPlayer,
                            ai0: angle, ai1: 75);
                    }
                    // 一次弹幕环
                    int boltDmg = YinEmperorHelper.GetScaledDamage(70);
                    for (int i = 0; i < 12; i++) {
                        float a = MathHelper.TwoPi * i / 12 + MathHelper.Pi / 12;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, a.ToRotationVector2() * 8f,
                            ModContent.ProjectileType<YinEmperorBolt>(), boltDmg, 1f, Main.myPlayer);
                    }
                }
                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = -0.6f, Volume = 1.5f }, NPC.Center);
                TriggerEnergyWave();
                ACMScreenShakeSystem.Add(12f);
                TriggerBloom(0.85f, YinEmperorHelper.DragonVeinGold);
            }
            else if (PhaseTimer > telegraph + 110) {
                FinalDecreeCharge = 0f;
                NextAct3();
            }
        }

        #endregion

        #region G7 处决（阴帝印幻象）

        private void BeginExecutionPhantom() {
            ResetGlobalState();
            ClearHostileProjectiles();
            TransitionTo(AIState.ExecutionPhantom);
        }

        private void RunExecutionPhantom(Player target) {
            NPC.dontTakeDamage = true;

            Vector2 hoverPos = target.Center + new Vector2(0, -HoverHeight);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.05f, 0.08f);

            if (PhaseTimer == 1) {
                Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.Phantom", YinEmperorHelper.AbyssPurple);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 1.8f }, NPC.Center);
                YinEmperorHelper.CreateImperialVortex(NPC.Center, 300f, 2.4f, 100);
                for (int i = 0; i < 4; i++) TriggerEnergyWave();
                ACMScreenShakeSystem.Add(16f);
                TriggerBloom(0.9f, YinEmperorHelper.AbyssPurple);
                GrantBreatherIFrames();
            }

            phantomSealScale = MathHelper.Lerp(phantomSealScale, 6f, 0.05f);
            auraIntensity = MathHelper.Lerp(auraIntensity, 1.8f, 0.04f);

            if (Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0) {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = NPC.Center + a.ToRotationVector2() * (260f);
                var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.GoldFlame : DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 2f;
                d.velocity = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 4f;
            }

            // 开窗后提示装备酆都套的玩家可处决
            if (PhaseTimer == 90) {
                ExecutionWindowOpen = true;
                foreach (var p in Main.player) {
                    if (p != null && p.active && !p.dead && p.GetModPlayer<YinJudgmentPlayer>().fengduSetActive) {
                        Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.ExecutePrompt", YinEmperorHelper.SoulLanternCyan);
                        break;
                    }
                }
            }

            if (PhaseTimer >= 170) {
                NPC.dontTakeDamage = false;
                phantomSealScale = MathHelper.Lerp(phantomSealScale, 3.5f, 0.1f);
                ActStep = 0;
                // 处决窗口常驻；未处决则正常以帝裁循环收尾
                TransitionTo(AIState.Act3_ImperialWrath);
            }
        }

        private void ExecuteCinematic() {
            Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.Executed", YinEmperorHelper.NetherBloodRed);
            YinEmperorHelper.CreateImperialVortex(NPC.Center, 380f, 2.8f, 130);
            YinEmperorHelper.CreateTalismanBurst(NPC.Center, 420f, 70);
            YinEmperorHelper.CreateScreenFlash(NPC.Center, YinEmperorHelper.SoulLanternCyan, 2f);
            ACMScreenShakeSystem.Add(16f);
            // G7 处决高光：青魂大泛光定格一击
            TriggerBloom(1f, YinEmperorHelper.SoulLanternCyan);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.8f, Volume = 1.6f }, NPC.Center);
        }

        private void TryExecute(Player player) {
            if (!ExecutionWindowOpen) return;
            if (NPC.life > NPC.lifeMax * FengduExecuteThreshold) return;
            if (player == null || !player.active || player.dead) return;
            if (!player.GetModPlayer<YinJudgmentPlayer>().fengduSetActive) return;

            ExecuteCinematic();
            NPC.life = 0;
            NPC.checkDead();
        }

        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone) {
            if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
                TryExecute(Main.player[projectile.owner]);
        }

        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone) {
            TryExecute(player);
        }

        #endregion

        #region 攻击方法

        private void ShootImperialBolts(Player target, int count) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = YinEmperorHelper.GetScaledDamage(85);
            Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            float spread = 0.2f;

            for (int i = 0; i < count; i++) {
                float angle = (i - (count - 1) / 2f) * spread;
                Vector2 direction = toPlayer.RotatedBy(angle);
                float speed = 13f + Main.rand.NextFloat(-1.5f, 1.5f);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + direction * 60f, direction * speed,
                    ModContent.ProjectileType<YinEmperorBolt>(), damage, 1f, Main.myPlayer);
            }

            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.2f, Volume = 1f }, NPC.Center);
            YinEmperorHelper.CreateDragonBurst(NPC.Center, 40f, 1, 6);
        }

        private void ShootGroundSeals(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = YinEmperorHelper.GetScaledDamage(70);
            int count = 3;
            for (int i = 0; i < count; i++) {
                Vector2 sealPos = target.Center + new Vector2((i - 1) * 200f + Main.rand.NextFloat(-40f, 40f), 50f);
                if (!Main.dedServ)
                    YinEmperorScreenSystem.AddTelegraph(sealPos, 95f, 36, YinEmperorHelper.DragonVeinGold);
                if (Main.netMode != NetmodeID.Server) {
                    for (int j = 0; j < 8; j++) {
                        float angle = MathHelper.TwoPi * j / 8;
                        Vector2 dustPos = sealPos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 40f;
                        var d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame);
                        d.noGravity = true;
                        d.scale = 1.2f;
                        d.velocity = new Vector2(0, -2f);
                    }
                }
                Projectile.NewProjectile(NPC.GetSource_FromAI(), sealPos + new Vector2(0, 300f), new Vector2(0, -12f),
                    ModContent.ProjectileType<YinEmperorBolt>(), damage, 1f, Main.myPlayer);
            }
            SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.2f, Volume = 0.9f }, target.Center);
        }

        #region 冥眼召唤方法

        private void SpawnEyeLaserFormation(Player target, int side) {
            int eyeCount = 4;
            float spacing = 120f;
            float sideOffset = 400f * side;
            int damage = YinEmperorHelper.GetScaledDamage(105);
            for (int i = 0; i < eyeCount; i++) {
                float yOffset = (i - (eyeCount - 1) / 2f) * spacing;
                Vector2 spawnPos = target.Center + new Vector2(sideOffset, yOffset);
                Vector2 toPos = (spawnPos - NPC.Center).SafeNormalize(Vector2.UnitX) * 8f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, toPos,
                    ModContent.ProjectileType<ArenaEdge>(), damage, 2f, Main.myPlayer, ai0: 0, ai1: i);
            }
            SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.1f, Volume = 1f }, NPC.Center);
            YinEmperorHelper.CreateDragonBurst(NPC.Center, 60f, 2, 10);
        }

        /// <summary>镇魂封印环：冥眼绕固定中心收缩（模式6）。</summary>
        private void SpawnSealRing(int count) {
            int damage = YinEmperorHelper.GetScaledDamage(75);
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), SealCenter + angle.ToRotationVector2() * 520f,
                    Vector2.Zero, ModContent.ProjectileType<ArenaEdge>(), damage, 2f, Main.myPlayer,
                    ai0: 6, ai1: angle);
            }
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 1.2f }, SealCenter);
        }

        private void SpawnGhostGateLock() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = SealCenter + angle.ToRotationVector2() * 520f;
            int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)pos.X, (int)pos.Y, ModContent.NPCType<GhostGateLock>(),
                0, SealCenter.X, SealCenter.Y, 0f, angle);
            if (idx >= 0 && idx < Main.maxNPCs && Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
        }

        private void SpawnGuardianEyes(int count) {
            int damage = YinEmperorHelper.GetScaledDamage(60);
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 spawnPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 130f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, Vector2.Zero,
                    ModContent.ProjectileType<ArenaEdge>(), damage, 1f, Main.myPlayer, ai0: 2, ai1: angle);
            }
            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.4f, Volume = 0.9f }, NPC.Center);
        }

        private void SpawnRingLaserFormation(Player target, int count) {
            int damage = YinEmperorHelper.GetScaledDamage(100);
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, angle.ToRotationVector2() * 10f,
                    ModContent.ProjectileType<ArenaEdge>(), damage, 2f, Main.myPlayer, ai0: 3, ai1: angle);
            }
            SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0f, Volume = 1.1f }, NPC.Center);
            YinEmperorHelper.CreateDragonBurst(NPC.Center, 80f, 2, 15);
        }

        private void SpawnCrossLaserFormation(Player target, int totalEyes) {
            int damage = YinEmperorHelper.GetScaledDamage(95);
            int perArm = totalEyes / 4;
            float armLength = 350f;
            for (int arm = 0; arm < 4; arm++) {
                float baseAngle = arm * MathHelper.PiOver2;
                for (int i = 0; i < perArm; i++) {
                    float dist = armLength * (i + 1) / perArm;
                    Vector2 spawnPos = NPC.Center + baseAngle.ToRotationVector2() * dist;
                    Vector2 toPos = (spawnPos - NPC.Center).SafeNormalize(Vector2.UnitX) * 8f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, toPos,
                        ModContent.ProjectileType<ArenaEdge>(), damage, 2f, Main.myPlayer, ai0: 4, ai1: arm * perArm + i);
                }
            }
            SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.2f, Volume = 1.1f }, NPC.Center);
            YinEmperorHelper.CreateImperialVortex(NPC.Center, 100f, 1f, 30);
        }

        #endregion

        #endregion

        #region 视觉效果

        private void TriggerEnergyWave() {
            for (int i = 0; i < energyWaveRadius.Length; i++) {
                if (energyWaveAlpha[i] <= 0.1f) {
                    energyWaveRadius[i] = 0f;
                    energyWaveAlpha[i] = 1f;
                    break;
                }
            }
        }

        private void UpdateEnergyWaves() {
            for (int i = 0; i < energyWaveRadius.Length; i++) {
                if (energyWaveAlpha[i] > 0f) {
                    energyWaveRadius[i] += 14f;
                    energyWaveAlpha[i] -= 0.018f;
                    if (energyWaveAlpha[i] < 0f) energyWaveAlpha[i] = 0f;
                }
            }
        }

        private void CreateAmbientParticles() {
            if (Main.netMode == NetmodeID.Server) return;
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = 70f + Main.rand.NextFloat(30f);
                Vector2 pos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                int dustType = Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.Shadowflame;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 1.3f * auraIntensity;
                d.velocity = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 2f;
                d.alpha = 80;
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            int frameHeight = tex.Height / MaxFrames;
            Rectangle sourceRect = new Rectangle(0, currentFrame * frameHeight, tex.Width, frameHeight);
            Vector2 origin = new Vector2(tex.Width / 2f, frameHeight / 2f);

            DrawEnergyWaves(spriteBatch, screenPos);

            // 阴帝印幻象（处决阶段）— 巨大法环
            if (CurrentState == AIState.ExecutionPhantom && phantomSealScale > 0.1f) {
                YinEmperorHelper.DrawImperialRing(spriteBatch, NPC.Center, phantomSealScale,
                    ringRotation * 2f, pulsePhase, 0.85f);
            }

            if (introPillarAlpha > 0.01f) {
                YinEmperorHelper.DrawDragonPillar(spriteBatch, NPC.Center + new Vector2(0, frameHeight * 0.4f),
                    800f, 60f, pulsePhase, introPillarAlpha);
            }

            if (ringAlpha > 0.01f) {
                YinEmperorHelper.DrawImperialRing(spriteBatch, NPC.Center, ringScale,
                    ringRotation, pulsePhase, ringAlpha * ((255 - NPC.alpha) / 255f));
            }

            YinEmperorHelper.DrawImperialAura(spriteBatch, NPC.Center, 90f * auraIntensity,
                10, auraRotation, pulsePhase, auraIntensity);

            if (auraIntensity > 0.5f) {
                YinEmperorHelper.DrawDragonOrbs(spriteBatch, NPC.Center, 110f, 4, pulsePhase * 0.8f, pulsePhase);
            }

            Color imperialColor = Color.Lerp(drawColor, YinEmperorHelper.ImperialGold, 0.3f);
            imperialColor = Color.Lerp(imperialColor, YinEmperorHelper.AbyssPurple, 0.15f);

            DrawTrail(spriteBatch, screenPos, tex, sourceRect, origin, imperialColor);

            Color glowColor = YinEmperorHelper.ImperialGold;
            glowColor.A = 0;
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.08f;
            for (int i = 3; i >= 0; i--) {
                float glowScale = NPC.scale * (1.15f + i * 0.1f) * pulse * auraIntensity;
                spriteBatch.Draw(tex, NPC.Center - screenPos, sourceRect, glowColor * (0.12f / (i + 1)),
                    NPC.rotation, origin, glowScale, SpriteEffects.None, 0);
            }

            spriteBatch.Draw(tex, NPC.Center - screenPos, sourceRect, imperialColor * ((255 - NPC.alpha) / 255f),
                NPC.rotation, origin, NPC.scale * pulse, SpriteEffects.None, 0);

            return false;
        }

        private void DrawEnergyWaves(SpriteBatch sb, Vector2 screenPos) {
            for (int i = 0; i < energyWaveRadius.Length; i++) {
                if (energyWaveAlpha[i] > 0.05f) {
                    YinEmperorHelper.DrawEnergyWave(sb, NPC.Center, energyWaveRadius[i], 25f,
                        YinEmperorHelper.ImperialGold, energyWaveAlpha[i] * 0.5f);
                }
            }
        }

        private void DrawTrail(SpriteBatch sb, Vector2 screenPos, Texture2D tex,
            Rectangle sourceRect, Vector2 origin, Color baseColor) {
            for (int layer = 0; layer < 2; layer++) {
                for (int i = 0; i < NPC.oldPos.Length; i++) {
                    if (NPC.oldPos[i] == Vector2.Zero) continue;
                    Vector2 pos = NPC.oldPos[i] + NPC.Size / 2 - screenPos;
                    float progress = 1f - i / (float)NPC.oldPos.Length;
                    float fade = progress * (layer == 0 ? 0.25f : 0.4f) * auraIntensity;
                    Color trailColor = layer == 0 ? YinEmperorHelper.AbyssPurple : baseColor;
                    trailColor *= fade;
                    if (layer == 0) trailColor.A = 0;
                    float trailScale = NPC.scale * (layer == 0 ? 1.3f : 1f) * (0.5f + progress * 0.5f);
                    sb.Draw(tex, pos, sourceRect, trailColor, NPC.rotation, origin, trailScale, SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>
        /// 全屏后处理：酆帝诏书阴阳分屏 (PaletteLUT yin-yang-split) + 大节拍泛光 (RadialBloom)。
        /// 二者共抢本帧唯一全屏名额 (§A.7 / 性能契约 ≤1)：阴阳分屏优先消费，泛光在名额空闲时补位。
        /// 危险方向 (站错半场) 被明显染为赤红，随既有安全侧切换而翻转。
        /// </summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || !MythologyConfig.FullscreenShadersEnabled)
                return;

            // 1) 阴阳分屏（消费全屏名额）
            if (yinYangVisual > 0.01f && ACMShaders.RequestFullscreenSlot()) {
                Effect fx = ACMShaders.PaletteLUT;
                if (fx != null) {
                    float aspect = (float)Main.screenWidth / Main.screenHeight;
                    // 竖直分界线（法线 = 屏幕 X 轴），中线落在 YinYangCenterX 的屏幕投影 (§A.7 split-math)
                    float cxUV = (YinYangCenterX - Main.screenPosition.X) / Main.screenWidth;
                    float proj = cxUV * aspect;                      // dir = UnitX → proj = uv.x * aspect
                    float splitPos = proj / ((1f + aspect) * 0.5f);

                    // 危险侧染赤红、安全侧维持阴冷/阳暖；随 YinYangSafeSide 翻转
                    Color yinCalm = TelegraphColors.NetherViolet;   // 阴(左)安全：幽蓝紫
                    Color yangCalm = YinEmperorHelper.ImperialGold;  // 阳(右)安全：帝冥金
                    Color danger = TelegraphColors.Execution;        // 错侧：赤红定罪
                    Color leftTint = YinYangSafeSide == 0 ? yinCalm : danger;   // shadowTint → 阴(左)
                    Color rightTint = YinYangSafeSide == 1 ? yangCalm : danger; // highlightTint → 阳(右)

                    fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                    fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(yinYangVisual, 0f, 1f));
                    fx.Parameters["uAspect"]?.SetValue(aspect);
                    fx.Parameters["uSaturation"]?.SetValue(1.05f);
                    fx.Parameters["uHueShift"]?.SetValue(0f);
                    fx.Parameters["uShadowTint"]?.SetValue(new Vector4(leftTint.ToVector3(), 1f));
                    fx.Parameters["uHighlightTint"]?.SetValue(new Vector4(rightTint.ToVector3(), 1f));
                    fx.Parameters["uSplit"]?.SetValue(1f);
                    fx.Parameters["uSplitDir"]?.SetValue(Vector2.UnitX);
                    fx.Parameters["uSplitPos"]?.SetValue(splitPos);

                    ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
                }
            }

            // 2) 大节拍泛光（名额若被阴阳占用则本帧自动让位）
            if (bloomPulse > 0.02f) {
                float radius = 0.18f + (1f - bloomPulse) * 0.28f;
                ACMShaders.DrawRadialBloomAt(NPC.Center, radius, bloomPulse, new Color(bloomColorV), 12f, 2.4f);
            }
        }

        #endregion

        #region 死亡

        public override void OnKill() {
            ResetGlobalState();

            YinEmperorHelper.CreateImperialVortex(NPC.Center, 350f, 2.5f, 120);
            YinEmperorHelper.CreateDragonBurst(NPC.Center, 300f, 6, 30);
            YinEmperorHelper.CreateTalismanBurst(NPC.Center, 400f, 60);

            for (int i = 0; i < 5; i++) TriggerEnergyWave();

            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8;
                Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                Vector2 strikePos = NPC.Center + dir * 250f;
                YinEmperorHelper.CreateNetherLightningPillar(strikePos - new Vector2(0, 600), strikePos, 1.5f);
            }

            for (int i = 0; i < 250; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(28f, 28f);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Shadowflame;
                var d = Dust.NewDustPerfect(NPC.Center, dustType);
                d.noGravity = true;
                d.scale = 3.5f;
                d.velocity = vel;
            }

            YinEmperorHelper.CreateScreenFlash(NPC.Center, YinEmperorHelper.DragonVeinGold, 2f);
            ACMScreenShakeSystem.Add(16f);
            TriggerBloom(1f, YinEmperorHelper.DragonVeinGold);
            SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.6f, Volume = 2f }, NPC.Center);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                DownedBossSystem.downedYinEmperor = true;
            }
        }

        #endregion
    }
}
