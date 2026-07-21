using AncientChineseMythology.Helpers;
using AncientChineseMythology.Systems;
using AncientChineseMythology.Underworlds.Boss.YinEmperors.Items;
using AncientChineseMythology.Underworlds.Items.Materials;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
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
    /// 阴天子（酆都大帝） - 冥府终局 Boss（T52 · G7 准圣门控）。
    ///
    /// V3 重做（见 Docs/BossRedo/YinEmperor.md）：主题是"帝王的审判"——威仪而非狂暴。
    ///   · 帝王以诏令统治法庭：冥诏点名（标记→宣判倒计时→执行，离开执行圈即安全）、
    ///     鬼门开阖召阴兵、酆都法庭结界（YinEmperorCourtBarrier + 六杆冥幡围场）。
    ///   · 亲自出手只有两式：帝袖横扫（极长蓄势换 9 帧毁灭直线）与玉玺压顶（幕三天坠强击），
    ///     接触伤害窗口与视觉爆发严格对齐（CanHitPlayer 门控）。
    ///   · 三幕骨架保留：幕一酆都仪典 / 幕二镇魂狱（鬼门关钥弱点强制阶段）/ 幕三帝裁
    ///     （阴阳诏书 + 终诏），G7 阴帝印幻象与酆都套处决保留。
    ///   · 三大演出：仪式入场（幡旗仪仗→鬼门显形→镇尺落界）、幕过场规则预览、
    ///     CheckDead 拦截的 ~5.5s 死亡弧线（幡旗逐杆熄灭→法环崩解→静默→终爆）。
    /// </summary>
    [AutoloadBossHead]
    public class YinEmperor : ModNPC
    {
        #region 常量

        private const int MaxFrames = 3;
        private const int FrameSpeed = 8;
        private const float HoverHeight = 300f;

        // 幕（HP 门）阈值
        private const float Phase2Threshold = 0.66f;
        private const float Phase3Threshold = 0.33f;
        private const float ExecutionThreshold = 0.10f;
        /// <summary>装备酆都套时可处决的血量阈值</summary>
        public const float FengduExecuteThreshold = 0.18f;

        /// <summary>镇魂封印收缩总时长（鬼门关钥与冥眼共用，确保同步）</summary>
        public const int SealContractTime = 360;

        /// <summary>酆都法庭基础半径（世界像素）</summary>
        public const float CourtBaseRadius = 1250f;
        private const int BannerCount = 6;

        // 入场时间轴（帧）
        private const int IntroTotal = 345;
        // 死亡弧线时间轴（帧）
        private const int DeathTotal = 330;

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
            /// <summary>换手势连接节拍（24 帧段落感，queuedState 指定下一招）</summary>
            Connector,

            // 幕一 酆都仪典（DecreeCall / GhostGates 为跨幕共用招，强度随幕递进）
            Act1_SleeveSweep,
            Act1_DecreeCall,
            Act1_GhostGates,

            ActTransition2,

            // 幕二 镇魂狱
            Act2_SoulSeal,
            Act2_NetherDecree,

            ActTransition3,

            // 幕三 帝裁
            Act3_SealSlam,
            Act3_YinYangEdict,
            Act3_FinalDecree,

            // G7 处决
            ExecutionPhantom,

            // 死亡弧线（CheckDead 拦截）
            DeathCinematic
        }

        private AIState CurrentState {
            get => (AIState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        private ref float PhaseTimer => ref NPC.ai[1];
        private ref float AttackTimer => ref NPC.ai[2];
        /// <summary>当前幕内循环表索引</summary>
        private ref float ActStep => ref NPC.ai[3];

        // 手写循环表（PACING §2：攻击序列本身就是编排——压制与呼吸交替）
        private static readonly AIState[] Act1Cycle = {
            AIState.Act1_SleeveSweep, AIState.Act1_DecreeCall,
            AIState.Act1_SleeveSweep, AIState.Act1_GhostGates
        };
        private static readonly AIState[] Act2Cycle = {
            AIState.Act2_SoulSeal, AIState.Act2_NetherDecree,
            AIState.Act1_DecreeCall, AIState.Act1_GhostGates, AIState.Act2_NetherDecree
        };
        private static readonly AIState[] Act3Cycle = {
            AIState.Act3_SealSlam, AIState.Act3_YinYangEdict,
            AIState.Act1_DecreeCall, AIState.Act3_SealSlam, AIState.Act3_FinalDecree
        };

        // 需要同步的逻辑状态（ai[] 之外）
        private int seed;
        private bool didAct2;
        private bool didAct3;
        private bool didExecution;
        private int decreeFormation;
        private int queuedState;
        private bool dying;
        private int sweepIndex;
        private Vector2 dashTarget;
        private float slamY;
        private bool courtSpawned;

        /// <summary>酆都法庭中心（召唤时锁定，幡旗/结界/鬼门以此为锚）</summary>
        public Vector2 ArenaCenter;

        /// <summary>镇魂封印中心（固定）</summary>
        public Vector2 SealCenter;
        /// <summary>封印状态：0=收缩中，1=已破(逃脱)，2=超时合拢(处决性合击)</summary>
        public int SealState;
        private bool sealWeakSpawned;
        private int sealResolveDelay;
        /// <summary>上一帧封印状态（各端本地，用于捕捉破封翻转帧播放失仪演出）</summary>
        private int prevSealState;
        private int yinYangBaseSide;

        private float LifeRatio => NPC.lifeMax <= 0 ? 1f : (float)NPC.life / NPC.lifeMax;

        #endregion

        #region 法庭结界状态（供 YinEmperorCourtBarrier 读取）

        /// <summary>结界当前目标半径（幕递进收窄：审判逐步收拢）</summary>
        public float CourtRadius => didAct3 ? 1050f : didAct2 ? 1150f : CourtBaseRadius;
        /// <summary>结界整体强度（入场落界前 0，死亡终爆后归 0）</summary>
        public float CourtIntensity { get; private set; }
        /// <summary>结界收缩压迫 0..1（镇魂狱收缩期转赤）</summary>
        public float CourtCollapse { get; private set; }
        /// <summary>结界大节拍闪光（逐帧衰减）</summary>
        public float CourtFlash { get; private set; }
        /// <summary>死亡弧线进度 0..1（Sky 读取做骤暗）</summary>
        public float DeathDarken { get; private set; }

        #endregion

        #region 状态变量（纯本地视觉）

        private int frameCounter;
        private int currentFrame;

        // 接触伤害窗口（每帧由招式显式开启；CanHitPlayer 门控 → 伤害与视觉严格对齐）
        private bool contactWindow;

        // 入场演出
        private float introGateOpen;
        private float bodyMaterialize;   // 0=未显形 1=完全显形

        // 死亡演出
        private float deathDissolve;     // 0=完好 1=完全崩解

        // 冥幡（六杆，世界固定位，客户端演出标量）
        private readonly float[] bannerRaise = new float[BannerCount];
        private readonly float[] bannerBurn = new float[BannerCount];
        private float bannerWave = 1f;

        // 视觉效果
        private float pulsePhase;
        private float auraRotation;
        private float auraIntensity;
        private float hoverOffset;
        private readonly float[] energyWaveRadius = new float[3];
        private readonly float[] energyWaveAlpha = new float[3];

        // 法环
        private float ringRotation;
        private float ringSpin = 0.008f;
        private float ringScale;
        private float ringAlpha;
        private float phantomSealScale;

        // V2 演出层
        /// <summary>阴阳分屏 PaletteLUT 的平滑强度（PostDraw 消费唯一全屏名额）。</summary>
        private float yinYangVisual;
        /// <summary>大节拍泛光脉冲 0..1（逐帧衰减）。</summary>
        private float bloomPulse;
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

        /// <summary>接触伤害门控：只在帝袖横扫爆发窗口与玉玺下砸窗口造成接触伤害。</summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => contactWindow;

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(0, 10000);
            CurrentState = AIState.Intro;
            PhaseTimer = 0;
            AttackTimer = 0;
            ActStep = 0;
            yinYangBaseSide = Main.rand.Next(2);
            dying = false;
            courtSpawned = false;

            // 法庭中心锁定在召唤者位置（结界/幡旗/鬼门以此为锚）
            int closest = Player.FindClosest(NPC.position, NPC.width, NPC.height);
            if (closest >= 0)
                ArenaCenter = Main.player[closest].Center;
            else
                ArenaCenter = NPC.Center;
            NPC.Center = ArenaCenter + new Vector2(0, -260f);
            NPC.alpha = 255;

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
            writer.Write(didAct2);
            writer.Write(didAct3);
            writer.Write(didExecution);
            writer.Write(decreeFormation);
            writer.Write(queuedState);
            writer.Write(dying);
            writer.Write(sweepIndex);
            writer.Write(dashTarget.X);
            writer.Write(dashTarget.Y);
            writer.Write(slamY);
            writer.Write(courtSpawned);
            writer.Write(ArenaCenter.X);
            writer.Write(ArenaCenter.Y);
            writer.Write(SealState);
            writer.Write(sealWeakSpawned);
            writer.Write(SealCenter.X);
            writer.Write(SealCenter.Y);
            writer.Write(yinYangBaseSide);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            didAct2 = reader.ReadBoolean();
            didAct3 = reader.ReadBoolean();
            didExecution = reader.ReadBoolean();
            decreeFormation = reader.ReadInt32();
            queuedState = reader.ReadInt32();
            dying = reader.ReadBoolean();
            sweepIndex = reader.ReadInt32();
            dashTarget.X = reader.ReadSingle();
            dashTarget.Y = reader.ReadSingle();
            slamY = reader.ReadSingle();
            courtSpawned = reader.ReadBoolean();
            ArenaCenter.X = reader.ReadSingle();
            ArenaCenter.Y = reader.ReadSingle();
            SealState = reader.ReadInt32();
            sealWeakSpawned = reader.ReadBoolean();
            SealCenter.X = reader.ReadSingle();
            SealCenter.Y = reader.ReadSingle();
            yinYangBaseSide = reader.ReadInt32();
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

            // 视觉效果更新
            pulsePhase += 0.06f;
            auraRotation += 0.015f;
            ringSpin = MathHelper.Lerp(ringSpin, 0.008f, 0.04f);
            ringRotation += ringSpin;
            hoverOffset = MathF.Sin(pulsePhase * 0.4f) * 8f;
            UpdateEnergyWaves();
            contactWindow = false;

            if (CurrentState != AIState.Intro) {
                ringScale = MathHelper.Lerp(ringScale, 2.5f, 0.01f);
                ringAlpha = MathHelper.Lerp(ringAlpha, 0.7f, 0.015f);
            }

            // 目标验证
            NPC.TargetClosest();
            Player target = Target;
            if ((!target.active || target.dead) && CurrentState != AIState.DeathCinematic) {
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

            // 幕（HP 门）切换检测
            if (!dying)
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
                case AIState.Connector: RunConnector(target); break;

                case AIState.Act1_SleeveSweep: RunSleeveSweep(target); break;
                case AIState.Act1_DecreeCall: RunDecreeCall(target); break;
                case AIState.Act1_GhostGates: RunGhostGates(target); break;

                case AIState.ActTransition2: RunActTransition(target, 2); break;

                case AIState.Act2_SoulSeal: RunSoulSeal(target); break;
                case AIState.Act2_NetherDecree: RunNetherDecree(target); break;

                case AIState.ActTransition3: RunActTransition(target, 3); break;

                case AIState.Act3_SealSlam: RunSealSlam(target); break;
                case AIState.Act3_YinYangEdict: RunYinYangEdict(target); break;
                case AIState.Act3_FinalDecree: RunFinalDecree(target); break;

                case AIState.ExecutionPhantom: RunExecutionPhantom(target); break;
                case AIState.DeathCinematic: RunDeathCinematic(target); break;
            }

            // 距离栓绳：任何状态都不允许飘出法庭太远（失败模式：Boss 飞出屏幕绕圈）
            if (CurrentState != AIState.Intro && CurrentState != AIState.DeathCinematic) {
                float maxDist = CourtRadius * 1.05f;
                Vector2 fromCenter = NPC.Center - ArenaCenter;
                if (fromCenter.Length() > maxDist) {
                    NPC.Center = ArenaCenter + fromCenter.SafeNormalize(Vector2.Zero) * maxDist;
                    if (Vector2.Dot(NPC.velocity, fromCenter) > 0)
                        NPC.velocity *= 0.5f;
                }
            }

            if (CurrentState != AIState.Intro && CurrentState != AIState.DeathCinematic) {
                CreateAmbientParticles();
            }

            UpdateBanners();
            UpdateCourtScalars();
            UpdateV2Presentation();
        }

        /// <summary>法庭内的悬浮锚点：跟随玩家但被限制在结界内侧。</summary>
        private Vector2 CourtAnchor(Player target, float height = HoverHeight) {
            Vector2 desired = target.Center + new Vector2(0, -height + hoverOffset);
            Vector2 fromCenter = desired - ArenaCenter;
            float maxR = CourtRadius * 0.78f;
            if (fromCenter.Length() > maxR)
                desired = ArenaCenter + fromCenter.SafeNormalize(Vector2.Zero) * maxR;
            return desired;
        }

        #endregion

        #region 法庭/幡旗/演出标量

        /// <summary>结界标量每帧推进（Flash 衰减、Collapse 平滑、Intensity 状态机）。</summary>
        private void UpdateCourtScalars() {
            CourtFlash = MathHelper.Lerp(CourtFlash, 0f, 0.08f);
            if (CourtFlash < 0.01f) CourtFlash = 0f;

            float collapseTarget = 0f;
            if (CurrentState == AIState.Act2_SoulSeal && SealState == 0 && PhaseTimer >= 60f)
                collapseTarget = MathHelper.Clamp((PhaseTimer - 60f) / SealContractTime, 0f, 1f);
            CourtCollapse = MathHelper.Lerp(CourtCollapse, collapseTarget, 0.05f);

            float intensityTarget = courtSpawned ? 1f : 0f;
            if (dying && PhaseTimer > 225f)
                intensityTarget = 0f;
            CourtIntensity = MathHelper.Lerp(CourtIntensity, intensityTarget, 0.03f);
        }

        /// <summary>幡旗演出标量（客户端确定性时间轴，全端一致）。</summary>
        private void UpdateBanners() {
            // 升起：入场 30~150 帧依次；再战精简版已在 RunIntro 中直接置满
            if (CurrentState == AIState.Intro) {
                for (int i = 0; i < BannerCount; i++) {
                    float t = (PhaseTimer - 30f - i * 20f) / 40f;
                    bannerRaise[i] = Math.Max(bannerRaise[i], ACMUtils.SineInOut(MathHelper.Clamp(t, 0f, 1f)));
                }
            }
            else if (!dying) {
                for (int i = 0; i < BannerCount; i++)
                    bannerRaise[i] = MathHelper.Lerp(bannerRaise[i], 1f, 0.02f);
            }

            // 熄灭：死亡弧线 40~160 帧逐杆
            if (dying) {
                for (int i = 0; i < BannerCount; i++) {
                    float t = (PhaseTimer - 40f - i * 20f) / 30f;
                    bannerBurn[i] = Math.Max(bannerBurn[i], MathHelper.Clamp(t, 0f, 1f));
                }
            }

            // 风力随战斗烈度
            float waveTarget = CurrentState switch {
                AIState.Act3_FinalDecree => 1.8f,
                AIState.ExecutionPhantom => 1.7f,
                AIState.DeathCinematic => 0.5f,
                _ => 1f + (didAct3 ? 0.4f : didAct2 ? 0.2f : 0f)
            };
            bannerWave = MathHelper.Lerp(bannerWave, waveTarget, 0.02f);
        }

        /// <summary>第 i 杆冥幡的布幔顶端挂点（世界坐标）。</summary>
        private Vector2 BannerTop(int i) {
            float angle = -MathHelper.PiOver2 + MathHelper.TwoPi * i / BannerCount;
            Vector2 basePoint = ArenaCenter + angle.ToRotationVector2() * CourtRadius * 0.97f;
            // 未升起时藏在下方，升起时滑到位
            return basePoint + new Vector2(0, (1f - bannerRaise[i]) * 260f - 150f);
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

            float yyTarget = 0f;
            if (YinYangActive) {
                yyTarget = 0.42f + (YinYangWarning ? 0.16f : 0f);
            }
            yinYangVisual = MathHelper.Lerp(yinYangVisual, yyTarget, 0.06f);

            if (Main.dedServ)
                return;

            // decree-vignette 强度（帝裁/处决/死亡渐强）
            float vig = CurrentState switch {
                AIState.Act3_SealSlam => 0.22f,
                AIState.Act3_YinYangEdict => 0.16f,
                AIState.Act3_FinalDecree => 0.25f + 0.45f * MathHelper.Clamp(FinalDecreeCharge, 0f, 1f),
                AIState.ExecutionPhantom => 0.5f,
                AIState.Act2_SoulSeal => SealState == 0 ? 0.3f : 0.15f,
                AIState.DeathCinematic => 0.4f,
                _ => 0f
            };
            if (ExecutionWindowOpen)
                vig = Math.Max(vig, 0.32f);

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
                or AIState.ActTransition3 or AIState.ExecutionPhantom or AIState.DeathCinematic;
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
            sweepIndex = 0;
            NPC.dontTakeDamage = false;
            NPC.netUpdate = true;
        }

        /// <summary>推进幕内手写循环：先进 24 帧 connector，再进入表中下一招。</summary>
        private void AdvanceCycle() {
            AIState[] table = !didAct2 ? Act1Cycle : !didAct3 ? Act2Cycle : Act3Cycle;
            ActStep++;
            AIState next = table[(int)ActStep % table.Length];
            if (next == AIState.Act2_NetherDecree)
                decreeFormation = (decreeFormation + 1) % 3;
            queuedState = (int)next;
            TransitionTo(AIState.Connector);
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
            int seal = ModContent.ProjectileType<YinEmperorDecreeSeal>();
            int column = ModContent.ProjectileType<YinEmperorJudgmentColumn>();
            int gate = ModContent.ProjectileType<YinEmperorGhostGate>();
            foreach (var p in Main.projectile) {
                if (p.active && (p.type == edge || p.type == bolt || p.type == laser
                    || p.type == seal || p.type == column || p.type == gate))
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

        #region 出场演出（仪式入场：幡旗仪仗 → 鬼门显形 → 静止凝视 → 镇尺落界）

        private void RunIntro(Player target) {
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.8f;
            NPC.Center = Vector2.Lerp(NPC.Center, ArenaCenter + new Vector2(0, -260f), 0.2f);

            float t = PhaseTimer;

            // 再战精简版：跳过仪仗前半（尊重玩家时间）
            if (t == 2 && DownedBossSystem.downedYinEmperor) {
                PhaseTimer = 145;
                for (int i = 0; i < BannerCount; i++)
                    bannerRaise[i] = 1f;
                return;
            }

            if (t == 1) {
                Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.Intro", YinEmperorHelper.ImperialGold);
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.9f, Volume = 1.2f }, ArenaCenter);
            }

            // 法庭法阵预告
            if (t == 10 && !Main.dedServ)
                YinEmperorScreenSystem.AddTelegraph(ArenaCenter, CourtBaseRadius * 0.5f, 130, TelegraphColors.NetherViolet);

            // 幡旗升起音阶（升起本体在 UpdateBanners 中推进）
            for (int i = 0; i < BannerCount; i++) {
                if ((int)t == 30 + i * 20) {
                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.5f + i * 0.12f, Volume = 0.8f },
                        BannerTop(i));
                    if (Main.netMode != NetmodeID.Server) {
                        for (int j = 0; j < 10; j++) {
                            var d = Dust.NewDustPerfect(BannerTop(i) + new Vector2(Main.rand.NextFloat(-40f, 40f), 120f), DustID.GoldFlame);
                            d.noGravity = true;
                            d.scale = 1.5f;
                            d.velocity = new Vector2(0, -Main.rand.NextFloat(3f, 7f));
                        }
                    }
                }
            }

            // 帝钟第二响
            if (t == 140)
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.7f, Volume = 1.3f }, ArenaCenter);

            // 鬼门开阖：140 开 → 235 起阖
            introGateOpen = t < 140 ? 0f
                : t <= 200 ? ACMUtils.SineInOut((t - 140f) / 60f)
                : t <= 235 ? 1f
                : MathHelper.Clamp(1f - (t - 235f) / 30f, 0f, 1f);

            // 显形：200~255 溶解显形（DissolveBurn 由 PreDraw 消费 bodyMaterialize）
            if (t >= 200) {
                bodyMaterialize = MathHelper.Clamp((t - 200f) / 55f, 0f, 1f);
                NPC.alpha = 0;
                if (t == 200) {
                    SoundEngine.PlaySound(SoundID.Item100 with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);
                    YinEmperorHelper.CreateImperialVortex(NPC.Center, 220f, 1.6f, 60);
                }
                auraIntensity = MathHelper.Lerp(auraIntensity, bodyMaterialize, 0.05f);
            }

            // 静止凝视期的低鸣与法环缓现
            if (t > 265 && t < 330) {
                ringScale = MathHelper.Lerp(ringScale, 2.5f, 0.03f);
                ringAlpha = MathHelper.Lerp(ringAlpha, 0.7f, 0.03f);
                if (t == 310)
                    Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.ActI", YinEmperorHelper.ImperialGold);
            }

            // 镇尺拍案：落界
            if (t == 330) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.8f, Volume = 1.6f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.4f }, NPC.Center);
                ACMScreenShakeSystem.Add(14f);
                TriggerBloom(0.95f, YinEmperorHelper.DragonVeinGold);
                CourtFlash = 1f;
                for (int i = 0; i < 3; i++) TriggerEnergyWave();
                YinEmperorHelper.CreateTalismanBurst(NPC.Center, 320f, 40);
                YinEmperorHelper.CreateScreenFlash(NPC.Center, YinEmperorHelper.DragonVeinGold, 1.2f);

                if (Main.netMode != NetmodeID.MultiplayerClient && !courtSpawned) {
                    courtSpawned = true;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), ArenaCenter, Vector2.Zero,
                        ModContent.ProjectileType<YinEmperorCourtBarrier>(), 0, 0f, Main.myPlayer, ai0: NPC.whoAmI);
                    NPC.netUpdate = true;
                }
            }

            if (t >= IntroTotal) {
                bodyMaterialize = 1f;
                NPC.alpha = 0;
                auraIntensity = 1f;
                ActStep = -1;
                AdvanceCycle();
            }
        }

        #endregion

        #region 连接节拍（换手势）

        /// <summary>招式间 24 帧"换手势"：法环短暂提速 + 一声木磬，屏幕得到段落感。</summary>
        private void RunConnector(Player target) {
            Vector2 anchor = CourtAnchor(target);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (anchor - NPC.Center) * 0.05f, 0.08f);

            if (PhaseTimer == 1) {
                ringSpin = 0.05f;
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.3f, Volume = 0.45f }, NPC.Center);
            }

            if (PhaseTimer >= 24) {
                // 容错：队列状态非法（如异常同步）时直接重新调度，绝不回退到 Intro
                var next = (AIState)queuedState;
                if (next is AIState.Intro or AIState.Connector or AIState.DeathCinematic)
                    AdvanceCycle();
                else
                    TransitionTo(next);
            }
        }

        #endregion

        #region 幕过场（i-frame 节拍 + 规则预览）

        private void RunActTransition(Player target, int act) {
            NPC.dontTakeDamage = true;

            Vector2 hoverPos = CourtAnchor(target);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.06f, 0.1f);

            if (PhaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.6f }, NPC.Center);
                YinEmperorHelper.CreateImperialVortex(NPC.Center, 280f, 2.2f, 90);
                YinEmperorHelper.CreateTalismanBurst(NPC.Center, 320f, 45);
                for (int i = 0; i < 3; i++) TriggerEnergyWave();
                ACMScreenShakeSystem.Add(12f);
                TriggerBloom(0.85f, YinEmperorHelper.NetherBloodRed);
                CourtFlash = 1f;
                YinEmperorHelper.CreateScreenFlash(NPC.Center, YinEmperorHelper.DragonVeinGold, 1f);
                ClearHostileProjectiles();
                GrantBreatherIFrames();
                ResetGlobalState();

                Speak(act == 2
                        ? "Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.ActII"
                        : "Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.ActIII",
                    YinEmperorHelper.NetherBloodRed);
            }

            // 规则预览：幕二 = 牢笼收缩预告；幕三 = 阴阳对转一周
            if (act == 2) {
                CourtCollapse = MathHelper.Lerp(CourtCollapse, PhaseTimer < 70 ? 0.5f : 0f, 0.08f);
            }
            else if (PhaseTimer is > 20 and < 90 && Main.netMode != NetmodeID.Server && PhaseTimer % 6 == 0) {
                // 结界环上的阴阳双点对转（金/紫粒子沿环相向而行）
                float a = (PhaseTimer - 20f) / 70f * MathHelper.TwoPi;
                for (int s = 0; s < 2; s++) {
                    float angle = a * (s == 0 ? 1f : -1f) + s * MathHelper.Pi;
                    Vector2 pos = ArenaCenter + angle.ToRotationVector2() * CourtRadius * 0.97f;
                    var d = Dust.NewDustPerfect(pos, s == 0 ? DustID.GoldFlame : DustID.PurpleTorch);
                    d.noGravity = true;
                    d.scale = 2.2f;
                    d.velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 4f * (s == 0 ? 1f : -1f);
                }
            }

            auraIntensity = MathHelper.Lerp(auraIntensity, 1.6f, 0.04f);

            if (Main.netMode != NetmodeID.Server && PhaseTimer % 5 == 0) {
                YinEmperorHelper.CreateImperialTrail(NPC.Center, Vector2.Zero, 1.4f);
            }

            if (PhaseTimer >= 110) {
                NPC.dontTakeDamage = false;
                auraIntensity = 1f;
                ActStep = -1;
                AdvanceCycle();
            }
        }

        #endregion

        #region 帝袖横扫（长蓄势 → 9 帧毁灭直线 → 硬刹）

        private void RunSleeveSweep(Player target) {
            int anticipation = sweepIndex == 0 ? 50 : 38;
            float t = AttackTimer;

            if (t < anticipation) {
                // 前摇：减速悬停，末 20 帧 pow8 反向吸气（late-snap reel-back）
                NPC.velocity *= 0.9f;
                float reelT = MathHelper.Clamp((t - (anticipation - 20)) / 20f, 0f, 1f);
                if (reelT > 0f) {
                    Vector2 away = (NPC.Center - target.Center).SafeNormalize(-Vector2.UnitY);
                    NPC.velocity += away * MathF.Pow(reelT, 8f) * 13f;
                }

                // 固定 36 帧预警钟音（威胁级别常数，玩家可内化）
                if (t == anticipation - 36)
                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.2f, Volume = 1.1f }, NPC.Center);

                // 锁定预测拦截点
                if (t == anticipation - 8) {
                    dashTarget = target.Center + target.velocity * 14f;
                    NPC.netUpdate = true;
                }

                // 蓄力粒子向袖口收敛
                if (Main.netMode != NetmodeID.Server && reelT > 0f && t % 2 == 0) {
                    float radius = 130f * (1f - reelT);
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(radius + 30f, radius + 30f);
                    var d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame);
                    d.noGravity = true;
                    d.scale = 1.4f + reelT;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * (4f + reelT * 8f);
                }
            }
            else if (t == anticipation) {
                // 爆发：一帧 set，直线无转向
                Vector2 dir = (dashTarget - NPC.Center).SafeNormalize(Vector2.UnitX);
                NPC.velocity = dir * 118f;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.4f, Volume = 1f }, NPC.Center);
                ACMScreenShakeSystem.Add(6f);
                TriggerEnergyWave();
                YinEmperorHelper.CreateDragonBurst(NPC.Center, 70f, 2, 12);

                // 帝袖罡风：垂直于冲线的两道可躲弧波
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                    int dmg = YinEmperorHelper.GetScaledDamage(75);
                    for (int s = -1; s <= 1; s += 2) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perp * s * 6.5f,
                            ModContent.ProjectileType<YinEmperorBolt>(), dmg, 1f, Main.myPlayer, ai0: 1f, ai1: s * 2f);
                    }
                }
            }
            else if (t <= anticipation + 9) {
                // 冲刺中：接触伤害仅此窗口
                contactWindow = true;
                YinEmperorHelper.CreateImperialTrail(NPC.Center, NPC.velocity, 2.2f);
                // 冲过法庭边缘则提前进入刹车（距离栓绳）
                if (Vector2.Distance(NPC.Center, ArenaCenter) > CourtRadius * 0.92f)
                    AttackTimer = anticipation + 10;
            }
            else if (t <= anticipation + 23) {
                // 硬刹：×0.68/f 的"砸进位置"读感
                NPC.velocity *= 0.68f;
                if (t == anticipation + 11)
                    ACMScreenShakeSystem.Add(5f);
            }
            else if (t <= anticipation + 46) {
                NPC.velocity *= 0.9f;
                NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;
            }
            else {
                sweepIndex++;
                int maxReps = LifeRatio < 0.8f ? 3 : 2;
                if (sweepIndex < maxReps) {
                    AttackTimer = 0;
                }
                else {
                    sweepIndex = 0;
                    AdvanceCycle();
                }
            }
        }

        #endregion

        #region 冥诏点名（标记 → 宣判倒计时 → 执行；幕二双印 / 幕三三印）

        private void RunDecreeCall(Player target) {
            int salvos = didAct3 ? 3 : didAct2 ? 2 : 1;
            Vector2 anchor = CourtAnchor(target, 360f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (anchor - NPC.Center) * 0.04f, 0.06f);

            if (PhaseTimer == 1) {
                Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.Judgment", YinEmperorHelper.ImperialGold);
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.5f, Volume = 1.1f }, NPC.Center);
            }

            // 抬手蓄力：金光向袖口汇聚
            if (PhaseTimer < 30 && Main.netMode != NetmodeID.Server && PhaseTimer % 2 == 0) {
                Vector2 hand = NPC.Center + new Vector2(NPC.spriteDirection * 40f, -50f);
                Vector2 dustPos = hand + Main.rand.NextVector2Circular(120f, 120f);
                var d = Dust.NewDustPerfect(dustPos, DustID.GoldFlame);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = (hand - dustPos).SafeNormalize(Vector2.Zero) * 5f;
            }

            // 落印（每波对每名存活玩家当时位置各一印）
            bool isSalvoFrame = PhaseTimer == 30 || (salvos >= 2 && PhaseTimer == 75) || (salvos >= 3 && PhaseTimer == 120);
            if (isSalvoFrame) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 1f }, NPC.Center);
                CourtFlash = Math.Max(CourtFlash, 0.4f);
                TriggerEnergyWave();
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    foreach (var p in Main.player) {
                        if (p != null && p.active && !p.dead && p.Distance(NPC.Center) < 3200f) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), p.Center, Vector2.Zero,
                                ModContent.ProjectileType<YinEmperorDecreeSeal>(), 0, 0f, Main.myPlayer,
                                ai0: 0f, ai1: 140f);
                        }
                    }
                }
            }

            int total = 30 + (salvos - 1) * 45 + 170;
            if (PhaseTimer > total)
                AdvanceCycle();
        }

        #endregion

        #region 鬼门开阖（召阴兵；幕二起三门）

        private void RunGhostGates(Player target) {
            bool triple = didAct2;
            Vector2 anchor = CourtAnchor(target, 380f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (anchor - NPC.Center) * 0.04f, 0.06f);

            if (PhaseTimer == 1) {
                Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.GhostGate", YinEmperorHelper.AbyssPurple);
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.6f, Volume = 1.1f }, NPC.Center);
            }

            // 挥袖：侧身摆动
            if (PhaseTimer < 30) {
                NPC.velocity.X += MathF.Sin(PhaseTimer * 0.4f) * 0.6f;
                if (Main.netMode != NetmodeID.Server && PhaseTimer % 3 == 0) {
                    var d = Dust.NewDustPerfect(NPC.Center + new Vector2(NPC.spriteDirection * 60f, 0f), DustID.PurpleTorch);
                    d.noGravity = true;
                    d.scale = 1.6f;
                    d.velocity = new Vector2(NPC.spriteDirection * 4f, Main.rand.NextFloat(-2f, 2f));
                }
            }

            if (PhaseTimer == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                float maxOff = CourtRadius - 260f;
                Vector2 basePos = target.Center;
                int gateType = ModContent.ProjectileType<YinEmperorGhostGate>();

                // 两侧门（出弹朝内），位置钳制在法庭内
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 pos = basePos + new Vector2(s * 760f, -60f);
                    pos.X = MathHelper.Clamp(pos.X, ArenaCenter.X - maxOff, ArenaCenter.X + maxOff);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero, gateType,
                        0, 0f, Main.myPlayer, ai0: -s, ai1: Main.rand.NextFloat(1f));
                }
                // 幕二起：头顶门（壁帘下压）
                if (triple) {
                    Vector2 top = basePos + new Vector2(0f, -520f);
                    top.Y = Math.Max(top.Y, ArenaCenter.Y - maxOff);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), top, Vector2.Zero, gateType,
                        0, 0f, Main.myPlayer, ai0: 0f, ai1: Main.rand.NextFloat(1f));
                }
            }

            if (PhaseTimer > 30 + 230 + 20)
                AdvanceCycle();
        }

        #endregion

        #region 幕二 冥谕列阵（冥眼阵列，保留强化）

        /// <summary>冥谕降罚 - 冥眼激光阵列；decreeFormation 由调度轮转（0=列阵 1=环形 2=十字）。</summary>
        private void RunNetherDecree(Player target) {
            Vector2 hoverPos = CourtAnchor(target, 400f);
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

            if (PhaseTimer >= 60 && PhaseTimer <= 200 && PhaseTimer % 45 == 0 && Main.netMode != NetmodeID.Server) {
                float lightningX = target.Center.X + Main.rand.NextFloat(-300f, 300f);
                Vector2 top = new Vector2(lightningX, target.Center.Y - 600f);
                Vector2 bottom = new Vector2(lightningX, target.Center.Y + 100f);
                YinEmperorHelper.CreateNetherLightningPillar(top, bottom, 0.8f);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.4f, Volume = 0.7f }, new Vector2(lightningX, target.Center.Y));
            }

            auraIntensity = MathHelper.Lerp(auraIntensity, 1f, 0.01f);

            if (PhaseTimer > 280)
                AdvanceCycle();
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
                        Vector2 pos = NPC.Center + angle.ToRotationVector2() * sealRadius;
                        var d = Dust.NewDustPerfect(pos, DustID.GoldFlame);
                        d.noGravity = true;
                        d.scale = 1.5f;
                        d.velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2f;
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
                CourtFlash = 0.6f;
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 1.3f }, target.Center);
            }
            // 收缩期间：检测弱点是否被击破
            else if (PhaseTimer > 70 && SealState == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient && sealWeakSpawned
                    && !NPC.AnyNPCs(ModContent.NPCType<GhostGateLock>())) {
                    // 鬼门关钥被击破 -> 封印瓦解，帝王失仪 + 长输出窗口
                    SealState = 1;
                    sealResolveDelay = 130;
                    NPC.netUpdate = true;
                }
                else if (PhaseTimer - 60 >= SealContractTime) {
                    // 超时合拢 -> 处决性合击（由冥眼执行）
                    SealState = 2;
                    sealResolveDelay = 70;
                    NPC.netUpdate = true;
                }
            }

            // 破封反馈：帝王失仪（被震退半跪，加大输出窗口）。触发帧凭 SealState 翻转在各端各自播放。
            if (SealState == 1) {
                if (prevSealState == 0) {
                    NPC.velocity = (NPC.Center - SealCenter).SafeNormalize(-Vector2.UnitY) * 9f;
                    ACMScreenShakeSystem.Add(9f);
                    TriggerBloom(0.7f, YinEmperorHelper.SoulLanternCyan);
                    CourtFlash = 1f;
                    Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.SealBroken", YinEmperorHelper.SoulLanternCyan);
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.6f, Volume = 1.3f }, NPC.Center);
                }
                // 失仪期：跌落到低位（近身输出奖励）
                Vector2 lure = target.Center + new Vector2(0, -150f);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (lure - NPC.Center) * 0.05f, 0.07f);
                if (PhaseTimer % 50 == 0 && Main.netMode != NetmodeID.Server)
                    YinEmperorHelper.CreateTalismanBurst(SealCenter, 200f, 24);
            }
            prevSealState = SealState;

            if (SealState != 0) {
                NPC.dontTakeDamage = false;
                // 倒计时只在服务器推进，状态经 netUpdate 下发，避免客户端各自提前收招
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    sealResolveDelay--;
                    if (sealResolveDelay <= 0) {
                        SealState = 0;
                        AdvanceCycle();
                    }
                }
            }
        }

        #endregion

        #region 幕三 玉玺压顶（升天 → 悬印 → 一帧下砸 → 半跪窗口）

        private void RunSealSlam(Player target) {
            float t = PhaseTimer;

            if (t == 1) {
                Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.SealSlam", YinEmperorHelper.NetherBloodRed);
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.7f, Volume = 1.2f }, NPC.Center);
            }

            if (t < 22) {
                // 升天：一帧 set 冲出视野上沿
                if (t == 2)
                    NPC.velocity = new Vector2(0, -26f);
                YinEmperorHelper.CreateImperialTrail(NPC.Center, NPC.velocity, 1.8f);
            }
            else if (t == 22) {
                // 顶点显形于玩家上方
                NPC.velocity = Vector2.Zero;
                NPC.Center = target.Center + new Vector2(0, -380f);
                NPC.netUpdate = true;
                SoundEngine.PlaySound(SoundID.Item100 with { Pitch = -0.4f, Volume = 1f }, NPC.Center);
                YinEmperorHelper.CreateImperialVortex(NPC.Center, 160f, 1.4f, 40);
            }
            else if (t < 53) {
                // 软跟踪玩家 X（给走位博弈），Y 锁定
                float targetX = target.Center.X;
                NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, (targetX - NPC.Center.X) * 0.06f, 0.15f);
                NPC.velocity.Y = (target.Center.Y - 380f - NPC.Center.Y) * 0.05f;
            }
            else if (t == 53) {
                // 锁定落点 + 预警投影（预警形状 = 伤害路径）
                NPC.velocity = Vector2.Zero;
                slamY = MathHelper.Clamp(target.Bottom.Y, ArenaCenter.Y - CourtRadius + 200f, ArenaCenter.Y + CourtRadius - 100f);
                NPC.netUpdate = true;
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.2f, Volume = 1.2f }, NPC.Center);
                if (!Main.dedServ)
                    YinEmperorScreenSystem.AddTelegraph(new Vector2(NPC.Center.X, slamY), 300f, 55, TelegraphColors.Lethal);
            }
            else if (t < 98) {
                // 悬印蓄势：pow8 上抬吸气 + 汇聚粒子（末段收声 = 爆发前的静默）
                float riseT = MathHelper.Clamp((t - 53f) / 45f, 0f, 1f);
                NPC.velocity = new Vector2(0, -MathF.Pow(riseT, 8f) * 10f);
                if (Main.netMode != NetmodeID.Server && riseT < 0.72f && t % 2 == 0) {
                    Vector2 below = NPC.Center + new Vector2(Main.rand.NextFloat(-140f, 140f), 160f);
                    var d = Dust.NewDustPerfect(below, DustID.GoldFlame);
                    d.noGravity = true;
                    d.scale = 1.6f;
                    d.velocity = (NPC.Center - below).SafeNormalize(Vector2.Zero) * 7f;
                }
            }
            else if (t == 98) {
                // 一帧下砸
                NPC.velocity = new Vector2(0, 78f);
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = -0.8f, Volume = 1.6f }, NPC.Center);
            }
            else if (t < 199) {
                // 坠落中：接触伤害窗口；服务器权威检测触地，跳表到 199 → 全端在 200 播触地演出
                contactWindow = NPC.velocity.Y > 20f;
                YinEmperorHelper.CreateImperialTrail(NPC.Center, NPC.velocity, 2.4f);
                bool hitGround = NPC.Center.Y >= slamY - 30f;
                bool bailout = t > 98 + 80;
                if ((hitGround || bailout) && Main.netMode != NetmodeID.MultiplayerClient) {
                    PhaseTimer = 199;
                    NPC.velocity = Vector2.Zero;
                    NPC.netUpdate = true;
                }
            }
            else if (t == 200) {
                // 触地帧（服务器与所有客户端凭同步后的计时各自播放）
                NPC.velocity = Vector2.Zero;
                SlamImpact(target);
            }
            else if (t < 245) {
                // 半跪：唯一近身输出窗口（fairness reward）
                NPC.velocity *= 0.8f;
                auraIntensity = MathHelper.Lerp(auraIntensity, 0.55f, 0.06f);
            }
            else if (t == 245) {
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.3f, Volume = 0.9f }, NPC.Center);
            }
            else if (t > 265) {
                auraIntensity = 1f;
                AdvanceCycle();
            }
        }

        /// <summary>玉玺触地：震屏 + 左右地面冲击波 + 金尘喷泉（服务器触发，特效各端由音效/弹幕承载）。</summary>
        private void SlamImpact(Player target) {
            ACMScreenShakeSystem.Add(10f);
            CourtFlash = 0.8f;
            TriggerBloom(0.7f, YinEmperorHelper.DragonVeinGold);
            TriggerEnergyWave();
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.7f, Volume = 1.4f }, NPC.Center);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int dmg = YinEmperorHelper.GetScaledDamage(85);
                for (int s = -1; s <= 1; s += 2) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(),
                        new Vector2(NPC.Center.X + s * 60f, slamY - 26f), new Vector2(s * 13f, 0f),
                        ModContent.ProjectileType<YinEmperorBolt>(), dmg, 1f, Main.myPlayer, ai0: 1f, ai1: s * 3f);
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 40; i++) {
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-9f, 9f), Main.rand.NextFloat(-13f, -4f));
                    var d = Dust.NewDustPerfect(new Vector2(NPC.Center.X + Main.rand.NextFloat(-100f, 100f), slamY - 10f),
                        Main.rand.NextBool(3) ? DustID.Shadowflame : DustID.GoldFlame);
                    d.noGravity = true;
                    d.scale = 1.8f + Main.rand.NextFloat(0.8f);
                    d.velocity = vel;
                }
            }
        }

        #endregion

        #region 幕三 酆帝诏书（阴阳半场）

        /// <summary>酆帝诏书 - 阴阳半场：站错半场持续灼魂 DoT；危险半场金符雨压力。</summary>
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

            // 切换瞬间反馈（翻页）
            if ((int)PhaseTimer % cycle == 0 && PhaseTimer > 1) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1f }, NPC.Center);
                CourtFlash = Math.Max(CourtFlash, 0.5f);
                if (Main.netMode != NetmodeID.Server)
                    YinEmperorHelper.CreateTalismanBurst(NPC.Center, 200f, 24);
            }

            // 危险半场金符雨（视觉压力为主，低伤）
            if (AttackTimer % 50 == 0 && PhaseTimer > 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                int dangerSign = YinYangSafeSide == 0 ? 1 : -1;
                int dmg = YinEmperorHelper.GetScaledDamage(60);
                for (int i = 0; i < 3; i++) {
                    float x = YinYangCenterX + dangerSign * Main.rand.NextFloat(140f, CourtRadius * 0.75f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(),
                        new Vector2(x, target.Center.Y - 720f), new Vector2(0, 2.5f),
                        ModContent.ProjectileType<YinEmperorBolt>(), dmg, 0.5f, Main.myPlayer,
                        ai0: 2f, ai1: Main.rand.NextFloat(6f));
                }
            }

            if (PhaseTimer > totalDuration) {
                YinYangActive = false;
                YinYangWarning = false;
                AdvanceCycle();
            }
        }

        #endregion

        #region 幕三 终诏（一次性 4s 预告十字激光）

        private void RunFinalDecree(Player target) {
            Vector2 hoverPos = CourtAnchor(target, 360f);
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
                // 汇聚粒子：72% 后收声（爆发前的静默）
                if (Main.netMode != NetmodeID.Server && FinalDecreeCharge < 0.72f && PhaseTimer % 4 == 0) {
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
                    for (int i = 0; i < 8; i++) {
                        float angle = MathHelper.PiOver4 * i;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                            ModContent.ProjectileType<YinEmperorLaser>(), laserDmg, 2f, Main.myPlayer,
                            ai0: angle, ai1: 75);
                    }
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
                CourtFlash = 1f;
                TriggerBloom(0.85f, YinEmperorHelper.DragonVeinGold);
            }
            else if (PhaseTimer > telegraph + 110) {
                FinalDecreeCharge = 0f;
                AdvanceCycle();
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

            Vector2 hoverPos = CourtAnchor(target);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.05f, 0.08f);

            if (PhaseTimer == 1) {
                Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.Phantom", YinEmperorHelper.AbyssPurple);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 1.8f }, NPC.Center);
                YinEmperorHelper.CreateImperialVortex(NPC.Center, 300f, 2.4f, 100);
                for (int i = 0; i < 4; i++) TriggerEnergyWave();
                ACMScreenShakeSystem.Add(16f);
                TriggerBloom(0.9f, YinEmperorHelper.AbyssPurple);
                CourtFlash = 1f;
                GrantBreatherIFrames();
            }

            phantomSealScale = MathHelper.Lerp(phantomSealScale, 6f, 0.05f);
            auraIntensity = MathHelper.Lerp(auraIntensity, 1.8f, 0.04f);

            if (Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0) {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = NPC.Center + a.ToRotationVector2() * 260f;
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
                ActStep = -1;
                AdvanceCycle();
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
            if (dying) return;
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

        #region 死亡弧线（CheckDead 拦截 → 幡旗熄灭 → 崩解 → 静默 → 终爆）

        public override bool CheckDead() {
            // 弧线播完才允许真正死亡；期间任何再致死都被拦截回 1 HP
            if (dying && PhaseTimer >= DeathTotal)
                return true;
            if (!dying) {
                dying = true;
                TransitionTo(AIState.DeathCinematic);
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    ClearHostileProjectiles();
                ResetGlobalState();
            }
            NPC.life = 1;
            NPC.dontTakeDamage = true;
            return false;
        }

        private void RunDeathCinematic(Player target) {
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.9f;
            float t = PhaseTimer;

            if (t == 1) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.9f, Volume = 1.5f }, NPC.Center);
                Speak("Mods.AncientChineseMythology.NPCs.YinEmperor.Dialog.Death", YinEmperorHelper.AbyssPurple);
            }

            // 幡旗逐杆熄灭音阶（熄灭本体在 UpdateBanners 推进）
            for (int i = 0; i < BannerCount; i++) {
                if ((int)t == 40 + i * 20)
                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = 0.4f - i * 0.16f, Volume = 0.9f }, BannerTop(i));
            }

            // 40~225：本体边缘剥落 + 金屑逆升
            if (t is > 40 and <= 225) {
                deathDissolve = MathHelper.Clamp((t - 40f) / 185f, 0f, 1f) * 0.55f;
                if (Main.netMode != NetmodeID.Server && t % 3 == 0) {
                    Vector2 pos = NPC.Center + Main.rand.NextVector2Circular(70f, 90f);
                    var d = Dust.NewDustPerfect(pos, DustID.GoldFlame);
                    d.noGravity = true;
                    d.scale = 1.4f;
                    d.velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(2f, 5f));
                }
            }

            // 160~225：法环崩解（转速失控 + 忽明忽暗），本体收缩增亮（pre-collapse）
            if (t is > 160 and <= 225) {
                ringSpin = MathHelper.Lerp(ringSpin, 0.11f, 0.05f);
                ringAlpha = 0.7f * (0.6f + 0.4f * MathF.Sin(t * 0.7f));
                NPC.scale = MathHelper.Lerp(NPC.scale, 0.88f, 0.02f);
                auraIntensity = MathHelper.Lerp(auraIntensity, 1.9f, 0.03f);
                if ((int)t % 22 == 0)
                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.4f, Volume = 0.8f }, NPC.Center);
            }

            // 225~250：静默（一切收声，天穹骤暗）
            if (t is > 225 and < 250) {
                ringAlpha = MathHelper.Lerp(ringAlpha, 0f, 0.2f);
                auraIntensity = MathHelper.Lerp(auraIntensity, 0.2f, 0.15f);
                DeathDarken = MathHelper.Clamp((t - 225f) / 20f, 0f, 1f);
            }

            // 250：终爆（全场唯一一次顶格演出）
            if (t == 250) {
                ACMScreenShakeSystem.Add(16f);
                TriggerBloom(1f, YinEmperorHelper.DragonVeinGold);
                CourtFlash = 1f;
                YinEmperorHelper.CreateImperialVortex(NPC.Center, 380f, 2.8f, 120);
                YinEmperorHelper.CreateTalismanBurst(NPC.Center, 420f, 60);
                YinEmperorHelper.CreateScreenFlash(NPC.Center, YinEmperorHelper.DragonVeinGold, 2f);
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.6f, Volume = 2f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.9f, Volume = 1.5f }, NPC.Center);
                if (!Main.dedServ)
                    YinEmperorScreenSystem.AddTelegraph(NPC.Center, 420f, 70, TelegraphColors.Holy);
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 8; i++) {
                        float angle = MathHelper.TwoPi * i / 8;
                        Vector2 strikePos = NPC.Center + angle.ToRotationVector2() * 250f;
                        YinEmperorHelper.CreateNetherLightningPillar(strikePos - new Vector2(0, 600), strikePos, 1.5f);
                    }
                }
            }

            // 250~330：余烬（魂灯青雨）+ 本体加速崩解
            if (t > 250) {
                deathDissolve = MathHelper.Clamp(0.55f + (t - 250f) / 80f * 0.45f, 0f, 1f);
                DeathDarken = MathHelper.Lerp(DeathDarken, 0.4f, 0.05f);
                if (Main.netMode != NetmodeID.Server && t % 2 == 0) {
                    Vector2 pos = NPC.Center + new Vector2(Main.rand.NextFloat(-320f, 320f), -Main.rand.NextFloat(200f, 380f));
                    var d = Dust.NewDustPerfect(pos, DustID.IceTorch);
                    d.noGravity = true;
                    d.scale = 1.3f;
                    d.velocity = new Vector2(0, Main.rand.NextFloat(1.5f, 3.5f));
                }
            }

            if (t >= DeathTotal) {
                NPC.life = 0;
                NPC.HitEffect();
                NPC.checkDead();
            }
        }

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
                Vector2 pos = NPC.Center + angle.ToRotationVector2() * dist;
                int dustType = Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.Shadowflame;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 1.3f * auraIntensity;
                d.velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2f;
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

            // 六杆冥幡（法庭仪仗，位于本体之下）
            DrawBanners(spriteBatch);

            // 入场鬼门
            if (introGateOpen > 0.01f && CurrentState == AIState.Intro) {
                YinEmperorHelper.DrawGate(spriteBatch, ArenaCenter + new Vector2(0, -250f),
                    new Vector2(340f, 560f), introGateOpen, 1f, 0.37f);
            }

            DrawEnergyWaves(spriteBatch, screenPos);

            // 阴帝印幻象（处决阶段）— 巨大法环
            if (CurrentState == AIState.ExecutionPhantom && phantomSealScale > 0.1f) {
                YinEmperorHelper.DrawImperialRing(spriteBatch, NPC.Center, phantomSealScale,
                    ringRotation * 2f, pulsePhase, 0.85f);
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

            // 溶解绘制路径：入场显形 / 死亡崩解
            float dissolveThreshold = Math.Max(1f - bodyMaterialize, deathDissolve);
            if (dissolveThreshold > 0.002f) {
                if (bodyMaterialize > 0.02f || deathDissolve > 0f) {
                    Color edge = deathDissolve > 0f ? YinEmperorHelper.SoulLanternCyan : YinEmperorHelper.DragonVeinGold;
                    edge.A = 220;
                    WeaponVFX.ApplyDissolveBurn(tex, NPC.Center, sourceRect, imperialColor,
                        NPC.rotation, origin, NPC.scale, dissolveThreshold, 1f,
                        edge, 0.1f, 2.4f);
                }
                return false;
            }

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

        private YinEmperorHelper.BannerDraw[] bannerDrawCache;

        private void DrawBanners(SpriteBatch sb) {
            if (Main.dedServ || CourtIntensity <= 0.01f && CurrentState != AIState.Intro)
                return;

            bannerDrawCache ??= new YinEmperorHelper.BannerDraw[BannerCount];
            int count = 0;
            for (int i = 0; i < BannerCount; i++) {
                if (bannerRaise[i] <= 0.01f || bannerBurn[i] >= 0.999f)
                    continue;
                bannerDrawCache[count++] = new YinEmperorHelper.BannerDraw {
                    Top = BannerTop(i),
                    Width = 116f,
                    Height = 340f,
                    Wave = bannerWave,
                    Burn = bannerBurn[i],
                    Intensity = bannerRaise[i],
                    Seed = seed * 0.01f + i * 1.7f
                };
            }
            if (count > 0)
                YinEmperorHelper.DrawBannerSet(sb, bannerDrawCache, count);
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
        /// 二者共抢本帧唯一全屏名额 (性能契约 ≤1)：阴阳分屏优先消费，泛光在名额空闲时补位。
        /// </summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || !MythologyConfig.FullscreenShadersEnabled)
                return;

            // 1) 阴阳分屏（消费全屏名额）
            if (yinYangVisual > 0.01f && ACMShaders.RequestFullscreenSlot()) {
                Effect fx = ACMShaders.PaletteLUT;
                if (fx != null) {
                    float aspect = (float)Main.screenWidth / Main.screenHeight;
                    float cxUV = (YinYangCenterX - Main.screenPosition.X) / Main.screenWidth;
                    float proj = cxUV * aspect;
                    float splitPos = proj / ((1f + aspect) * 0.5f);

                    Color yinCalm = TelegraphColors.NetherViolet;
                    Color yangCalm = YinEmperorHelper.ImperialGold;
                    Color danger = TelegraphColors.Execution;
                    Color leftTint = YinYangSafeSide == 0 ? yinCalm : danger;
                    Color rightTint = YinYangSafeSide == 1 ? yangCalm : danger;

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

            // 终爆已在死亡弧线 250 帧处播放，这里只留落幕余韵
            YinEmperorHelper.CreateTalismanBurst(NPC.Center, 300f, 40);
            for (int i = 0; i < 3; i++) TriggerEnergyWave();
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 60; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(16f, 16f);
                    var d = Dust.NewDustPerfect(NPC.Center, Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch);
                    d.noGravity = true;
                    d.scale = 2.2f;
                    d.velocity = vel;
                }
            }

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                DownedBossSystem.downedYinEmperor = true;
            }
        }

        #endregion
    }
}
