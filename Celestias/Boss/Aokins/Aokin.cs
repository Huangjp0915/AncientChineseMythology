using System;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Celestias.Boss.Aokins.Items;
using AncientChineseMythology.Items.Materials;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    /// <summary>
    /// 南海龙王敖钦 — 月后初期 Boss（第 2 条龙王脊柱，南海珠门控，敖广之后）。
    /// 火属性熔火龙王主题，蛇形多段身体结构。
    ///
    /// ==========  V2 重做设计理念（火 / 熔火 set-piece）  ==========
    /// ● 身份差异化（脱离"敖广换皮"）：
    ///     - 「余烬温度 EmberHeat」资源（对位敖顺 StormCharge）：火招累积温度，温度=全屏热浪强度条。
    ///     - 满温触发「炼狱茧 Inferno Cocoon」泄压：无敌帧 + 带缺口的扩张火环（有反制，钻缝即破）。
    ///     - 「龙蛇盘绕俯冲 CoilDive」：用身体段盘成不断收紧的接触伤害螺旋（身体即机制）。
    /// ● 攻击体系：去除随机陨石刷屏 → 「劫火印记」预告式顺序火柱波；加权无重复攻击牌库 + 强制巡游喘息。
    /// ● 真三阶段（≤25%）「焚海劫」：熔潮场地改造——地面熔岩裂隙标记，仅余安全平台（改规则，非更快的二阶段）。
    /// ● 演出：GenericWarp(heat)/ElementalScreenTint 热浪；DrawRadialBloomAt 熔岩泛光；WorldDecalParams+ArenaRunic
    ///         裂隙/火柱预告；DrawBeam 火柱；相变点燃封路龙卷向内收缩。
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

        // 巡游喘息时长
        private const int MinPatrolDuration = 95;
        private const int MaxPatrolDuration = 165;
        // 攻击前短预告
        private const int PreAttackDuration = 30;

        #endregion

        #region 状态枚举

        /// <summary>AI 主状态机（与 HP 阶段区 PhaseRegion 解耦）。</summary>
        public enum MainState
        {
            Intro,
            SummonBarriers,
            Patrol,            // 强制喘息：每招之间必经
            PreAttack,         // 攻击前短预告
            Attacking,         // 执行攻击
            PhaseTransition2,  // 50% 转换：点燃封路龙卷向内收缩
            PhaseTransition3   // 25% 焚海劫：熔潮场地改造
        }

        /// <summary>攻击类型（按阶段区组牌库；加权无重复抽取）。</summary>
        public enum AttackType
        {
            FireBarrage,     // 火弹扇射（小压制）
            DragonBreath,    // 龙息喷射
            EmberPillars,    // 劫火印记：预告式顺序火柱波（替代陨石刷屏）
            CoilDive,        // 龙蛇盘绕俯冲：收紧的接触伤害螺旋（身体即机制）
            FuryCharge,      // P2+ 狂怒冲刺
            FlameVortex,     // P2+ 烈焰旋涡
            InfernoBreath,   // P2+ 地狱龙息
            Divebomb,        // P2+ 烈焰俯冲（带冷却）
            InfernoCocoon,   // 满温泄压：无敌帧 + 带缺口扩张火环
            MoltenSurge      // P3 熔潮涌动：再触发熔岩裂隙
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

        /// <summary>当前竞技场半宽（封路龙卷读取，相变向内收缩）。</summary>
        public float ArenaHalfWidth { get; private set; } = 800f;

        // 私有状态
        private float globalTime;
        private int seed;
        private Random random;
        private float introProgress;
        private bool didPhase2Transition;
        private bool didPhase3Transition;

        // 余烬温度资源
        private float emberHeat;

        // 攻击计时 / 喘息
        private int attackTimer;
        private int patrolTimer;
        private int patrolDuration;
        private int subState;

        // 攻击历史（加权无重复）
        private AttackType lastAttack = (AttackType)(-1);

        // 蛇形身体
        private Vector2[] segmentPos = new Vector2[SegmentCount];
        private float[] segmentRot = new float[SegmentCount];

        // 冲刺 / 盘绕控制
        private Vector2 chargeTarget;
        private int chargeCount;
        private int maxChargeCount;
        private float coilAngle;
        private float coilRadius;

        // 俯冲冷却
        private int divebombCooldown;

        // 封路龙卷控制
        private int[] barrierTornadoIds;

        // 视觉效果
        private float flameAuraAlpha;
        private float flameRotation;
        private float flameScale;
        private float glowIntensity;

        // V2 热浪屏幕演出标量（纯本地视觉, 0~1, 平滑驱动并发布给 AokinHeatScreenSystem）
        private float heatTint;    // ElementalScreenTint 热浪氛围底色（= 温度条）
        private float heatWarp;    // GenericWarp(heat) 全屏扭曲（仅签名时刻）
        private float lavaBloom;   // 熔岩 / 泄压瞬间加性泛光
        private float runicTell;   // ArenaRunic 场地预警地纹（炼狱茧蓄力 / 焚海劫）

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
