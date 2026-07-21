using AncientChineseMythology.Celestias.Boss.Aokins.Items;
using AncientChineseMythology.Items.Materials;
using System;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    /// <summary>
    /// 南海龙王敖钦 — 月后初期 Boss（第 2 条龙王脊柱，南海珠门控，敖广之后）。
    /// 火属性熔火龙王主题，蛇形多段身体结构。
    ///
    /// ==========  V3 重做设计理念（"火是他的脾气"）  ==========
    /// ● 与敖广的威仪蓄势相反：**短蓄势、高频压迫、贴脸暴烈**。所有爆发均为
    ///   "反向拖拽 → 单帧 set → 硬刹车"的速度对比波形（choreography skill 本能 1/2）。
    /// ● 双压迫曲线：
    ///     - 「余烬温度 EmberHeat」（环境）：火招累积温度 = 全屏热浪强度条，满温强制炼狱茧泄压。
    ///     - 「逆鳞怒气 Rage」（行为，P2+）：受击积怒 → 满怒逆鳞爆气（清弹 + 无伤冲击 = 公平阀门）
    ///       → 6 秒狂暴（攻速提升、熔鳞泛白）。打得越狠仗越暴烈，清弹兜底公平。
    /// ● 三大演出齐备：入场「南海沸腾」（水下蓄势 → 破空跃出 → 静止瞪视 → 咆哮）、
    ///   P2 相变「沸海蒸腾」（清弹 + 蒸汽茧爆发）、P3 相变「焚海劫」（场地改造）、
    ///   死亡「逆鳞崩解」（逐段爆裂 → 冲天 → 寂静 → 金红新星，CheckDead 接管）。
    /// ● 专属着色器：AokinBreathCone（锥形龙息火舌）/ AokinShockRing（冲击火环·缺口反制）/
    ///   AokinMoltenScale（龙身熔鳞 emissive，随温度/狂暴/死亡变化）。
    /// ● 公平阀门：接触伤害仅在冲刺/盘绕/俯冲窗口生效（伤害窗口=视觉窗口）；
    ///   巡游喘息为强制休止符；预警配色走 TelegraphColors（红只留致命）。
    /// </summary>
    [AutoloadBossHead]
    internal partial class Aokin : ModNPC
    {
        #region 常量定义

        /// <summary>二阶段血量百分比阈值</summary>
        public const float Phase2Threshold = 0.50f;

        /// <summary>三阶段「焚海劫」血量百分比阈值</summary>
        public const float Phase3Threshold = 0.25f;

        /// <summary>蛇形身体段数</summary>
        private const int SegmentCount = 30;

        /// <summary>身体段间距</summary>
        private const int SegmentGap = 48;

        /// <summary>余烬温度上限（满温触发炼狱茧泄压）</summary>
        public const float MaxEmberHeat = 100f;

        /// <summary>逆鳞怒气上限（满怒触发逆鳞爆气 → 狂暴）</summary>
        public const float MaxRage = 100f;

        /// <summary>狂暴持续时长（6 秒）</summary>
        private const int RageDuration = 360;

        /// <summary>死亡演出总时长</summary>
        private const int DeathDuration = 230;

        // 巡游喘息时长
        private const int MinPatrolDuration = 95;
        private const int MaxPatrolDuration = 165;
        // 攻击前短预告
        private const int PreAttackDuration = 30;

        #endregion

        #region 状态枚举

        /// <summary>AI 主状态机（与 HP 阶段区 PhaseRegion 解耦）。新成员只允许追加在尾部（网络值兼容）。</summary>
        public enum MainState
        {
            Intro,
            SummonBarriers,    // 保留为保底出口（V3 入场自带封路龙卷）
            Patrol,            // 强制喘息：每招之间必经
            PreAttack,         // 攻击前短预告
            Attacking,         // 执行攻击
            PhaseTransition2,  // 50% 沸海蒸腾：清弹 + 蒸汽茧爆发
            PhaseTransition3,  // 25% 焚海劫：熔潮场地改造
            RageBurst,         // 逆鳞爆气：清弹 + 无伤冲击 → 狂暴
            DeathAnimation     // 死亡演出（CheckDead 接管）
        }

        /// <summary>攻击类型（按阶段区组牌库；加权无重复抽取）。新成员只允许追加在尾部。</summary>
        public enum AttackType
        {
            FireBarrage,     // 火弹扇射：三连发组 + 后坐停顿
            DragonBreath,    // 赤炎龙息：锥形火舌波次（V3 重做）
            EmberPillars,    // 劫火印记：预告式顺序火柱波
            CoilDive,        // 龙蛇盘绕俯冲：收紧的接触伤害螺旋（身体即机制）
            FuryCharge,      // P2+ 狂怒连冲：反向拖拽 → 单帧爆发 → 硬刹（V3 重做）
            FlameVortex,     // P2+ 烈焰旋涡（缓移压迫）
            InfernoBreath,   // 旧枚举保留（映射到赤炎龙息, 勿删避免网络值错位）
            Divebomb,        // P2+ 烈焰俯冲：屏内蓄势 + 垂直红线预警（V3 重做）
            InfernoCocoon,   // 满温泄压：无敌帧 + 带缺口扩张火环
            MoltenSurge,     // P3 熔潮涌动：再触发熔岩裂隙
            MoltenRain,      // V3 新增：熔金雨（仰天上抛熔金球, 落地成池）
            SteamCannon,     // V3 新增：蒸汽龙炮（长聚气单发大弹, 静默前奏 + 间歇泉齐鸣）
            FlameFlood       // V3 新增：龙焰洪流（P3 横贯火河, 上下走位安全）
        }

        #endregion

        #region 状态属性

        private MainState CurrentState {
            get => (MainState)(int)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        private AttackType CurrentAttack {
            get => (AttackType)(int)NPC.ai[2];
            set => NPC.ai[2] = (float)value;
        }

        /// <summary>攻击计时（入 ai[1] 走 NPC 常规同步, 修复 V2 子阶段多人漂移）。</summary>
        private int attackTimer {
            get => (int)NPC.ai[1];
            set => NPC.ai[1] = value;
        }

        /// <summary>攻击子阶段（入 ai[3] 走 NPC 常规同步）。</summary>
        private int subState {
            get => (int)NPC.ai[3];
            set => NPC.ai[3] = value;
        }

        /// <summary>当前 HP 阶段区：1=一阶段, 2=二阶段, 3=焚海劫。</summary>
        public int PhaseRegion {
            get {
                float r = (float)NPC.life / NPC.lifeMax;
                if (r < Phase3Threshold) return 3;
                if (r < Phase2Threshold) return 2;
                return 1;
            }
        }

        /// <summary>是否处于二阶段或更高（兼容旧绘制判断）</summary>
        public bool IsPhase2 => PhaseRegion >= 2;
        /// <summary>是否处于焚海劫阶段</summary>
        public bool IsPhase3 => PhaseRegion >= 3;

        /// <summary>余烬温度比例 0~1（= 全屏热浪强度条，恒定可读）。</summary>
        public float HeatRatio => MathHelper.Clamp(emberHeat / MaxEmberHeat, 0f, 1f);
        /// <summary>是否过热（满温，下一招强制炼狱茧泄压）。</summary>
        public bool IsOverheated => emberHeat >= MaxEmberHeat;

        /// <summary>逆鳞狂暴是否激活（供天空/绘制读取）。</summary>
        public bool IsEnraged => rageActive;
        /// <summary>逆鳞怒气比例 0~1。</summary>
        public float RageRatio => MathHelper.Clamp(rageCharge / MaxRage, 0f, 1f);
        /// <summary>死亡演出进度 0~1（供天空/绘制读取, 非死亡演出恒 0）。</summary>
        public float DeathProgress { get; private set; }

        /// <summary>当前竞技场半宽（封路龙卷读取，相变向内收缩）。</summary>
        public float ArenaHalfWidth { get; private set; } = 800f;

        /// <summary>入场水下蓄势期（本体隐匿, 绘制层跳过身体）。</summary>
        internal bool IntroHidden => CurrentState == MainState.Intro && attackTimer < IntroLeapFrame;

        // 入场脚本关键帧
        private const int IntroLeapFrame = 50;
        private const int IntroRoarFrame = 140;
        private const int IntroEndFrame = 170;

        // 私有状态
        private float globalTime;
        private int seed;
        private Random random;
        private bool didPhase2Transition;
        private bool didPhase3Transition;

        // 余烬温度资源
        private float emberHeat;

        // 逆鳞怒气（P2+ 受击积怒 → 爆气清弹 → 狂暴）
        private float rageCharge;
        private bool rageActive;
        private int rageTimer;
        private int lastLifeSeen;

        // 死亡演出
        private bool deathAnimationDone;
        private int deathBurntSegments;

        // 接触伤害基准与窗口（伤害窗口 = 视觉攻击窗口, 巡游期躯体无害）
        private int contactDamageBase;
        private bool bodyContactWindow;

        // 巡游喘息计时（时长服务器 roll 后经 SendExtraAI 同步）
        private int patrolTimer;
        private int patrolDuration;

        // 攻击历史（加权无重复）
        private AttackType lastAttack = (AttackType)(-1);

        // 蛇形身体
        private Vector2[] segmentPos = new Vector2[SegmentCount];
        private float[] segmentRot = new float[SegmentCount];
        private float segmentWaveDamp = 1f; // 盘绕/茧期收紧游动波

        // 冲刺 / 盘绕控制
        private Vector2 chargeTarget;
        private int chargeCount;
        private int maxChargeCount;
        private float coilAngle;
        private float coilRadius;

        // 俯冲冷却与预警线
        private int divebombCooldown;
        private float diveTelegraphX;
        private float diveTelegraphT;

        // 冲刺线预警强度 0~1
        private float chargeTelegraphT;

        // 龙息口部聚焰强度 0~1（绘制）
        private float breathGlow;

        // 本帧是否由状态手动控制朝向（跳过速度朝向）
        private bool rotationLocked;

        // 封路龙卷控制
        private int[] barrierTornadoIds;

        // 视觉效果
        private float flameAuraAlpha;
        private float flameRotation;
        private float flameScale;
        private float glowIntensity;
        private float introEyeGlow;
        private float rageVisual; // 狂暴熔鳞泛白平滑值

        // V3 热浪屏幕演出标量（纯本地视觉, 0~1, 平滑驱动并发布给 AokinHeatScreenSystem）
        private float heatTint;    // ElementalScreenTint 热浪氛围底色（= 温度条）
        private float heatWarp;    // AokinHeatHaze 全屏蜃景（仅签名时刻拉满）
        private float lavaBloom;   // 熔岩 / 泄压瞬间加性泛光
        private float runicTell;   // ArenaRunic 场地预警地纹（炼狱茧蓄力 / 焚海劫）
        private float rageFlash;   // 逆鳞爆气红脉冲
        private float deathWhite;  // 死亡新星白闪
        private float deathDim;    // 死亡寂静压暗

        // 热浪蜃景 vent 冲击环（咆哮/泄压/爆气/死亡冲击时触发, 0=未激活）
        private float ventProgress;
        private Vector2 ventCenter;

        #endregion

        #region ModNPC重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 120;
            NPC.height = 120;
            NPC.damage = 130;
            NPC.defense = 55;
            NPC.lifeMax = 420000;
            NPC.HitSound = SoundID.NPCHit56;
            NPC.DeathSound = SoundID.NPCDeath62;
            NPC.value = Item.buyPrice(platinum: 1, gold: 30);
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

            Music = MusicID.Boss2;
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.GreaterHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<DragonKingScale>(), 1, 8, 12));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<InfernoDragonSpear>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<FlamecoilChakram>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<CrimsonMaelstromBow>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<DraconicEmber>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<MeteorCallerStaff>(), 5));
        }

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(10000);
            random = new Random(seed);

            flameRotation = 0f;
            flameScale = 1f;
            glowIntensity = 1f;
            flameAuraAlpha = 0f;
            divebombCooldown = 0;
            emberHeat = 0f;
            rageCharge = 0f;
            rageActive = false;
            rageTimer = 0;
            lastLifeSeen = 0;
            deathAnimationDone = false;
            deathBurntSegments = 0;
            DeathProgress = 0f;
            contactDamageBase = 0;
            introEyeGlow = 0f;

            CurrentState = MainState.Intro;
            attackTimer = 0;

            // 初始化蛇形身体段位置
            for (int i = 0; i < SegmentCount; i++) {
                segmentPos[i] = NPC.Center - new Vector2(SegmentGap * (i + 1), 0);
                segmentRot[i] = 0f;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.netUpdate = true;
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
            writer.Write(globalTime);
            writer.Write(emberHeat);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(chargeCount);
            writer.Write(divebombCooldown);
            writer.Write((int)lastAttack);
            writer.Write(rageCharge);
            writer.Write(rageActive);
            writer.Write(rageTimer);
            writer.Write(deathAnimationDone);
            writer.Write(patrolTimer);
            writer.Write(patrolDuration);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            globalTime = reader.ReadSingle();
            emberHeat = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            chargeCount = reader.ReadInt32();
            divebombCooldown = reader.ReadInt32();
            lastAttack = (AttackType)reader.ReadInt32();
            rageCharge = reader.ReadSingle();
            rageActive = reader.ReadBoolean();
            rageTimer = reader.ReadInt32();
            deathAnimationDone = reader.ReadBoolean();
            patrolTimer = reader.ReadInt32();
            patrolDuration = reader.ReadInt32();

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

        /// <summary>
        /// 死亡演出接管：首次归零血量不死，转入「逆鳞崩解」演出；
        /// 演出播完由服务器置 <see cref="deathAnimationDone"/> 后再真正死亡（掉落/downed 标记照常走 OnKill）。
        /// </summary>
        public override bool CheckDead() {
            if (deathAnimationDone)
                return true;

            if (CurrentState != MainState.DeathAnimation) {
                TransitionTo(MainState.DeathAnimation);
            }
            // 演出期间血量保底（防外部再次打到 0 卡死）
            NPC.life = Math.Max(NPC.life, 1);
            NPC.dontTakeDamage = true;
            return false;
        }

        public override void OnKill() {
            // 关闭天空背景
            if (!VaultUtils.isServer && AokinSky.name != null) {
                Terraria.Graphics.Effects.SkyManager.Instance.Deactivate(AokinSky.name);
            }

            Systems.DownedBossSystem.downedAokin = true;
        }

        #endregion
    }
}
