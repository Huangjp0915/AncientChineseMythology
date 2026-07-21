using AncientChineseMythology.Celestias.Boss.Aoshuns.Items;
using AncientChineseMythology.Items.Materials;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    /// <summary>
    /// 北海龙王敖顺 — V3 全面重做（设计文档: Docs/BossRedo/Aoshun.md）
    ///
    /// ==========  主题: "你不是在打一条龙, 你是在一场活着的风暴里求生"  ==========
    /// ● 本体 = 风暴的眼壁: 蠕虫链穿行于地下与雨云之间, 露面即攻击
    /// ● 臂 = 风: 臂段(AoshunArms)以弹簧手势独立编排 — 蓄势后仰挥风刃 / 聚拢压掌造龙卷 / 张臂唤雷
    /// ● 雨 = 幕布: 二阶段起全屏风暴扭曲 + 斜向雨幕(AoshunStormWarp), 风暴之眼内部无雨无扭曲
    /// ● 雷 = 标点: 致命预警一律 TelegraphColors.Lethal 红色契约
    ///
    /// 阶段: P1 疾风(100~65%) → T2 雷暴降临 → P2 雷霆(65~30%) → T3 坠入眼中 → P3 风暴之眼(30~0)
    /// 选招: 每阶段洗牌袋 + 防复读 + 重压/控场相间; 攻击间 45f「蜷缩重整」连接拍
    /// 蓄电: 钻地积攒 StormCharge, 满电下一招过载强化并清空
    /// </summary>
    [AutoloadBossHead]
    internal partial class Aoshun : ModNPC
    {
        #region 常量定义

        /// <summary>二阶段血量阈值（雷暴降临）</summary>
        public const float Phase2Threshold = 0.65f;
        /// <summary>三阶段血量阈值（坠入眼中）</summary>
        public const float Phase3Threshold = 0.30f;

        /// <summary>头部纹理帧数（2帧）</summary>
        private const int HeadFrameCount = 2;

        /// <summary>钻地巡逻基础速度</summary>
        private const float PatrolSpeed = 17f;
        /// <summary>二阶段后巡逻速度</summary>
        private const float PatrolSpeedLate = 23f;

        /// <summary>风暴蓄电最大值</summary>
        public const float MaxStormCharge = 100f;
        /// <summary>每帧钻地蓄电量</summary>
        private const float ChargePerDigTick = 0.45f;

        /// <summary>连接拍「蜷缩重整」时长</summary>
        private const int RegroupCoilTime = 45;

        /// <summary>入场演出总时长</summary>
        private const int IntroDuration = 210;
        /// <summary>T2 雷暴降临演出时长</summary>
        private const int Transition2Duration = 240;
        /// <summary>T3 坠入眼中演出时长</summary>
        private const int Transition3Duration = 180;
        /// <summary>死亡「风暴葬礼」演出时长</summary>
        private const int DeathDuration = 330;

        /// <summary>风暴之眼常驻竞技场初始/最小半径</summary>
        public const float EyeStartRadius = 700f;
        public const float EyeHoldRadius = 430f;

        #endregion

        #region 状态枚举

        /// <summary>AI 主状态</summary>
        public enum AoshunState
        {
            Intro,          // 入场演出「破土升天」
            Regroup,        // 连接拍: 蜷缩重整 + 钻地巡逻蓄电
            Attacking,      // 执行洗牌袋选出的攻击
            Transition2,    // 换阶段演出「雷暴降临」
            Transition3,    // 换阶段演出「坠入眼中」
            Dying           // 死亡演出「风暴葬礼」
        }

        /// <summary>攻击类型（详见设计文档 §4 编排表）</summary>
        public enum AoshunAttackType
        {
            GaleCleave,     // 风刃连斩: 臂后仰→错相挥出风刃
            CyclonePalm,    // 龙卷压掌: 双臂内拢→压掌落龙卷
            ThunderSeal,    // 天雷印: 沿玩家动向扇形铺印, 延迟引爆雷柱
            AbyssBreach,    // 破渊突袭: 深潜→静默→地裂预警→垂直破土
            StormNet,       // 雷链电网: 环形节点网 + 迁移安全缺口 (P2+)
            HeavensCall,    // 张臂唤雷: 蓄力唤落错相雷柱 (P2+)
            TempestPierce,  // 风暴穿刺: 后拉蓄势→红线预告→高速穿刺 (P2+)
            KingRoar,       // 龙王怒啸: P3 进场演出化连接招
            EyePierce,      // 眼弦穿刺: 沿风暴眼弦线穿刺 (P3)
            WallCyclone,    // 沿壁龙卷: 龙卷贴眼壁巡游 (P3)
            EyeEdgeCall     // 眼缘落雷: 雷柱落在眼内边缘环带 (P3)
        }

        /// <summary>臂部手势（臂段读取头部此状态做弹簧编排, 纯视觉）</summary>
        public enum ArmGestureKind
        {
            None,       // 收拢贴体
            ReelBack,   // 蓄势后仰
            Slash,      // 骤然挥出
            FoldIn,     // 双臂内拢(压掌前)
            SpreadOut,  // 张臂(唤雷/咆哮)
            Tremor      // 震颤(蓄势/濒死)
        }

        #endregion

        #region 状态字段

        // NPC.ai[0]: 蠕虫身体是否已生成
        // NPC.ai[1]: 状态计时器（自动同步）
        // NPC.ai[2]: 当前攻击类型（自动同步）
        // NPC.ai[3]: 脱战计时器

        // internalAI[0]: 主状态  [1]: 攻击子状态  [2]: 风暴蓄电  [3]: 本次攻击是否过载
        public float[] internalAI = new float[4];

        private AoshunState CurrentState {
            get => (AoshunState)(int)internalAI[0];
            set => internalAI[0] = (int)value;
        }
        private int SubState {
            get => (int)internalAI[1];
            set => internalAI[1] = value;
        }
        public float StormCharge {
            get => internalAI[2];
            set => internalAI[2] = value;
        }
        /// <summary>本次攻击是否为满电过载强化版</summary>
        public bool Overloaded {
            get => internalAI[3] == 1f;
            set => internalAI[3] = value ? 1f : 0f;
        }

        private int StateTimer {
            get => (int)NPC.ai[1];
            set => NPC.ai[1] = value;
        }
        private AoshunAttackType CurrentAttack {
            get => (AoshunAttackType)(int)NPC.ai[2];
            set => NPC.ai[2] = (int)value;
        }

        /// <summary>风暴蓄电是否已满</summary>
        public bool IsFullyCharged => StormCharge >= MaxStormCharge;

        public float HpFrac => NPC.life / (float)NPC.lifeMax;
        /// <summary>是否进入二阶段（雷暴）</summary>
        public bool InPhase2 => HpFrac < Phase2Threshold || didTransition2;
        /// <summary>是否进入三阶段（风暴之眼）</summary>
        public bool InPhase3 => HpFrac < Phase3Threshold || didTransition3;

        /// <summary>接触伤害闸门: 演出期间(入场/换阶段/死亡)全链无接触伤害</summary>
        public bool ContactDamageEnabled =>
            CurrentState == AoshunState.Regroup || CurrentState == AoshunState.Attacking;

        /// <summary>是否处于停火演出（身段经 <see cref="AoshunHelper.HeadIsPacified"/> 读取）</summary>
        public bool IsPacified => !ContactDamageEnabled;

        /// <summary>死亡演出进度 0~1（身段渐隐白热用）</summary>
        public float DeathProgress => CurrentState == AoshunState.Dying
            ? MathHelper.Clamp(StateTimer / (float)DeathDuration, 0f, 1f)
            : 0f;

        // --- 同步的演出/瞄准状态（SendExtraAI） ---
        private Vector2 aimPoint;       // 通用锚点: 伏击落点 / 唤雷中心 / 眼锚点
        private Vector2 aimVector;      // 通用方向: 穿刺方向 / 眼弦方向
        private bool didTransition2;
        private bool didTransition3;
        private bool deathTriggered;    // CheckDead 已拦截, 正在演出
        private int attackCounter;      // 已完成攻击数（决定盘旋方向等）

        // --- 服务器端选招 ---
        private System.Collections.Generic.List<int> attackBag = [];
        private int lastBagTail = -1;

        // --- 本地运行时（不同步, 各端从同步状态确定性推导或纯视觉） ---
        private bool despawn;
        private bool isUnderground;
        private float globalTime;
        private bool close;

        // 臂部手势（各端由状态机确定性驱动, 纯视觉）
        private ArmGestureKind gestureKind = ArmGestureKind.None;
        private int gestureTimer;
        private int gestureDuration = 1;
        private float gestureStagger;

        // 风暴之眼（P3 常驻竞技场, 参数每帧从眼弹幕读取）
        public bool EyeActive { get; private set; }
        public Vector2 EyeCenter { get; private set; }
        public float EyeRadius { get; private set; }

        // 屏幕演出标量（纯本地视觉）
        private float stormTintFx;          // 压暗强度（电量/阶段驱动）
        private float stormWeatherFx;       // 风雨强度（T2 后常驻, 眼内抠除由着色器做）
        private bool stormWasFullyCharged;  // 满电边沿检测
        private float dashVisualHeat;       // 冲刺残影热度（速度门控, 客户端视觉）

        #endregion

        #region 臂部手势 API（臂段读取）

        /// <summary>当前手势</summary>
        public ArmGestureKind Gesture => gestureKind;

        /// <summary>
        /// 指定臂段（按链上序号）的手势进度 0~1。段错相 = 序号 × gestureStagger 帧,
        /// 让挥击沿身体波浪式传递而非整排齐动。
        /// </summary>
        public float GestureProgress(int segmentIndex) {
            float t = (gestureTimer - segmentIndex * gestureStagger) / Math.Max(gestureDuration, 1);
            return MathHelper.Clamp(t, 0f, 1f);
        }

        /// <summary>设置臂部手势（重复设置同类手势不重置计时）</summary>
        private void SetGesture(ArmGestureKind kind, int duration, float staggerPerSegment = 0f) {
            if (gestureKind != kind) {
                gestureKind = kind;
                gestureTimer = 0;
            }
            gestureDuration = Math.Max(duration, 1);
            gestureStagger = staggerPerSegment;
        }

        #endregion

        #region ModNPC重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = HeadFrameCount;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 30;
            NPC.height = 28;
            NPC.damage = 150;
            NPC.defense = 45;
            NPC.lifeMax = 430000;
            NPC.HitSound = SoundID.NPCHit56;
            NPC.DeathSound = SoundID.NPCDeath60;
            NPC.value = Item.buyPrice(platinum: 1, gold: 30);
            NPC.knockBackResist = 0f;
            NPC.lavaImmune = true;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.behindTiles = true;
            NPC.netAlways = true;
            NPC.npcSlots = 1f;
            NPC.aiStyle = -1;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.35f);
                NPC.damage = (int)(NPC.damage * 1.25f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.35f);
            }

            Music = MusicID.Boss2;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.GreaterHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<DragonKingScale>(), 1, 8, 12));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<ThunderlordHalberd>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<StormchainWhip>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<TempestRepeater>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<LightningEdictTome>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<AzureRuinBlade>(), 5));
        }

        public override void SendExtraAI(BinaryWriter writer) {
            base.SendExtraAI(writer);
            writer.Write(internalAI[0]);
            writer.Write(internalAI[1]);
            writer.Write(internalAI[2]);
            writer.Write(internalAI[3]);
            writer.WriteVector2(aimPoint);
            writer.WriteVector2(aimVector);
            writer.Write(didTransition2);
            writer.Write(didTransition3);
            writer.Write(deathTriggered);
            writer.Write((short)attackCounter);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            internalAI[0] = reader.ReadSingle();
            internalAI[1] = reader.ReadSingle();
            internalAI[2] = reader.ReadSingle();
            internalAI[3] = reader.ReadSingle();
            aimPoint = reader.ReadVector2();
            aimVector = reader.ReadVector2();
            didTransition2 = reader.ReadBoolean();
            didTransition3 = reader.ReadBoolean();
            deathTriggered = reader.ReadBoolean();
            attackCounter = reader.ReadInt16();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            return null;
        }

        public override bool CheckActive() => false;

        /// <summary>
        /// 死亡拦截: 血量归零时不立即死亡, 转入「风暴葬礼」演出（清弹/无敌/逐段爆裂）,
        /// 演出末尾由 AI 主动触发真实死亡。
        /// </summary>
        public override bool CheckDead() {
            if (!deathTriggered) {
                deathTriggered = true;
                NPC.life = Math.Max(NPC.life, 1);
                NPC.dontTakeDamage = true;
                CurrentState = AoshunState.Dying;
                StateTimer = 0;
                SubState = 0;
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    AoshunAttacks.ClearHostileProjectiles();
                NPC.netUpdate = true;
                return false;
            }
            return true;
        }

        /// <summary>演出期间（入场/换阶段/死亡）头部无接触伤害</summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => ContactDamageEnabled;

        public override void OnKill() {
            Systems.DownedBossSystem.downedAoshun = true;

            if (Main.netMode != NetmodeID.Server) {
                AoshunHelper.CreateThunderBurst(NPC.Center, 220f, 4, 20);
                ACMUtils.AddScreenShake(9f);
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (NPC.life <= 0 && Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 30; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                    int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                    int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height, dustType, vel.X, vel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }

        public override void BossHeadSpriteEffects(ref SpriteEffects spriteEffects) {
            spriteEffects = NPC.spriteDirection == 1
                ? SpriteEffects.FlipHorizontally
                : SpriteEffects.None;
        }

        #endregion
    }
}
