using AncientChineseMythology.Celestias.Boss.Aoyuans.Items;
using AncientChineseMythology.Items.Materials;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// 西海龙王敖闰 - 月后初期Boss（第3条龙王脊柱, 敖钦之后）
    ///
    /// ==========  V3 重做: "西海静渊 · 刹那冰锋"  ==========
    /// ● 幻想内核: 四海中最冷静克制的剑客龙——长时间盘蜷静滞蓄势(寒气凝结、时间仿佛冻结)
    ///   → 刹那冰封突刺(9~12帧贯穿), 剑过之处留一线冰封航迹, 慢半拍凝晶成伤害墙。
    /// ● 与玄武差异化: 玄武=厚重巨兽守势(盾/覆盖), 敖闰=凌厉剑客攻势(线/定格/折射)。
    /// ● 招式池洗牌袋: P1{突刺/冰镜阵/寒潮/困龙局/回旋连斩} + P2{绝对零度/镜界瞬狱}。
    /// ● 伤害窗口与视觉对齐: 接触伤害仅突刺帧满额, 静滞/巡逻降至 45%。
    /// ● 三大演出: 破镜现身入场 / 时滞破境(50%) / 晶化升天死亡(CheckDead 接管)。
    /// ● 专属着色器: AoyuanCrystalline(棱镜后处理) / AoyuanFrostGround(冻土·陷阱) / AoyuanMirror(冰镜)。
    /// </summary>
    [AutoloadBossHead]
    internal partial class Aoyuan : ModNPC
    {
        #region 常量定义

        /// <summary>二阶段血量百分比阈值</summary>
        public const float Phase2Threshold = 0.50f;
        /// <summary>低血狂澜阈值（巡逻减半、突刺三连）</summary>
        public const float DesperationThreshold = 0.25f;

        /// <summary>蠕虫身体段帧序列（对应AoyuanBody纹理5帧中的帧号）</summary>
        public static readonly int[] BodyFrameSequence = [1, 2, 0, 1, 2, 1, 2, 0, 1, 2, 1, 2, 0, 1, 2, 3, 4];

        /// <summary>头部纹理帧数</summary>
        private const int HeadFrameCount = 3;

        /// <summary>巡逻时长范围（帧）— 远比旧版短, 攻击间只留呼吸拍</summary>
        private const int PatrolMin = 46, PatrolMax = 80;
        private const int PatrolMinP2 = 30, PatrolMaxP2 = 60;

        /// <summary>收剑连接拍时长</summary>
        private const int SheathDuration = 25;

        #endregion

        #region 状态枚举

        /// <summary>AI主状态机</summary>
        public enum AoyuanState
        {
            Intro,           // 破镜现身入场演出
            Patrol,          // 收剑巡游（绕玩家盘旋）
            Sheath,          // 收剑连接拍（攻击后的段落句号）
            Attacking,       // 执行攻击
            PhaseTransition, // 50% 时滞破境
            DeathAnim        // 晶化升天死亡演出
        }

        /// <summary>攻击类型</summary>
        public enum AoyuanAttackType
        {
            // --- 一阶段 ---
            InstantThrust,   // 刹那·冰封突刺（签名: 盘蜷→锁线→贯穿→冰封航迹）
            MirrorArray,     // 冰镜·折光阵（弧列冰镜依序射折光冰束）
            ColdWave,        // 寒潮·冻土席卷（俯冲触地→地面结霜蔓延+冰脊波+尖刺）
            FreezeTrap,      // 冰封·困龙局（倒计时冻结区+放牧压制）
            FrostBlades,     // 霜刃·回旋连斩（V/X 形短突刺连击）
            // --- 二阶段追加 ---
            AbsoluteZero,    // 绝对零度（吸气蓄力→放射冻结, 弱点可打断）
            MirrorRealm      // 镜界·瞬狱连突（入镜隐没→出口镜白亮→爆出贯穿 ×3）
        }

        #endregion

        #region 状态字段

        // NPC.ai[0]: 蠕虫是否已初始化（0=未初始化，1=已初始化）
        // NPC.ai[1]: 状态计时器（同步）
        // NPC.ai[2]: 当前攻击类型（AoyuanAttackType）
        // NPC.ai[3]: 脱战计时器

        /// <summary>是否处于二阶段</summary>
        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;
        /// <summary>低血狂澜</summary>
        public bool IsDesperation => NPC.life < NPC.lifeMax * DesperationThreshold;

        // 阶段状态（用internalAI网络同步）
        public float[] internalAI = new float[4];
        // internalAI[0]: 当前主状态（AoyuanState）
        // internalAI[1]: 招式参数A（突刺角度/出口镜索引等）
        // internalAI[2]: 是否已破境（0/1）
        // internalAI[3]: 招式参数B（循环/波次计数）

        public AoyuanState CurrentState {
            get => (AoyuanState)(int)internalAI[0];
            private set => internalAI[0] = (float)value;
        }

        /// <summary>状态计时器（NPC.ai[1] 同步别名）</summary>
        private ref float StateTimer => ref NPC.ai[1];

        /// <summary>招式参数A: 突刺方向角/镜索引（同步）</summary>
        private ref float ParamA => ref internalAI[1];
        /// <summary>招式参数B: 内部循环计数（同步）</summary>
        private ref float ParamB => ref internalAI[3];

        // 私有运行时状态
        private bool despawn;
        private bool DidPhase2Transition => internalAI[2] == 1f;

        // 张嘴动画（吸气/咆哮）
        private bool fireAttack;
        private int attackFrame;
        private int attackCounter;

        // 巡逻
        private int patrolDuration = PatrolMax;
        private float orbitAngle;
        private int orbitDir = 1;

        // 盘蜷运动学（纯位置修饰, 各端本地积分, 状态切换时经 netUpdate 校正）
        private float coilAngle;

        // 突刺
        private Vector2 lastWakePos;
        /// <summary>突刺伤害窗（速度门控 + 状态门控, 供头/身伤害与残影读取）</summary>
        public bool BladeActive { get; private set; }
        private int contactDamageBase;
        /// <summary>基准接触伤害（不受伤害窗口逐帧调制影响, 供生成器定弹幕伤害）</summary>
        public int ContactDamageBase => contactDamageBase;

        // 洗牌袋（服务器权威）
        private readonly System.Collections.Generic.List<AoyuanAttackType> attackBag = [];
        private AoyuanAttackType lastAttack = (AoyuanAttackType)(-1);

        // 绝对零度弱点机制（公开供身体段读取）
        /// <summary>绝对零度蓄力中：身体段暴露冰晶弱点</summary>
        public bool WeakPointsExposed;
        /// <summary>蓄力期间身体段累计承受的伤害（用于判断是否打断）</summary>
        public int WeakPointDamageTaken;
        /// <summary>弱点被击破后的踉跄易伤窗</summary>
        private int staggerTimer;

        // 隐身（入场未现身 / 镜界瞬狱入镜）— 身体段同步隐藏
        /// <summary>头与身体段是否处于隐没状态（入场前 / 入镜中）</summary>
        public bool BodyHidden { get; private set; }

        // 死亡演出
        private bool reallyDead;
        /// <summary>死亡演出: 已晶化的身体段数（从尾部数）, 身体段据此白化</summary>
        public int CrystallizedSegments { get; private set; }

        // 视觉效果
        private float globalTime;
        private float glowIntensity = 1f;

        // 屏幕演出标量（纯本地视觉, 由 UpdateScreenFx 平滑驱动）
        private float frostTint;      // ElementalScreenTint 氛围底色
        private float freezeBloom;    // 冻爆泛光（释放瞬间置 1, 逐帧衰减）
        private float arenaRunic;     // 绝对零度蓄力法阵地纹
        private float crystalFx;      // AoyuanCrystalline 棱面折射强度
        private float stillFx;        // 时滞去饱和（破境/死亡）
        private float flashFx;        // 冲击帧（死亡碎裂唯一一次）
        private float frostEdge;      // 屏幕边缘结霜

        // 预警线视觉（客户端）
        private float telegraphAlpha;   // 突刺预警线强度
        private float telegraphLock;    // 预警线锁定白闪
        private float slashFlash;       // 出剑爆闪（SlashBurst）

        /// <summary>时滞标量（供 AoyuanSky 压暗天幕, 纯视觉）</summary>
        public float StillFxFactor => stillFx;

        #endregion

        #region ModNPC重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = HeadFrameCount;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 80;
            NPC.height = 80;
            NPC.damage = 140;
            NPC.defense = 80;
            NPC.lifeMax = 430000;
            NPC.HitSound = SoundID.NPCHit56;
            NPC.DeathSound = SoundID.NPCDeath62;
            NPC.value = Item.buyPrice(platinum: 1, gold: 30);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.behindTiles = true;
            NPC.npcSlots = 20f;
            NPC.aiStyle = -1;
            NPC.alpha = 255;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.35f);
                NPC.damage = (int)(NPC.damage * 1.25f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.35f);
            }

            contactDamageBase = NPC.damage;

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
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<GlacialDragonblade>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<PermafrostTrident>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<VortexPrimordialStain>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<InkscaledFlowFan>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<BlizzardPiercer>(), 5));
        }

        public override void SendExtraAI(BinaryWriter writer) {
            base.SendExtraAI(writer);
            if (Main.netMode == NetmodeID.Server || Main.dedServ) {
                writer.Write(internalAI[0]);
                writer.Write(internalAI[1]);
                writer.Write(internalAI[2]);
                writer.Write(internalAI[3]);
                writer.Write(WeakPointsExposed);
                writer.Write(BodyHidden);
                writer.Write((byte)System.Math.Clamp(CrystallizedSegments, 0, 255));
            }
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                internalAI[0] = reader.ReadSingle();
                internalAI[1] = reader.ReadSingle();
                internalAI[2] = reader.ReadSingle();
                internalAI[3] = reader.ReadSingle();
                WeakPointsExposed = reader.ReadBoolean();
                BodyHidden = reader.ReadBoolean();
                CrystallizedSegments = reader.ReadByte();
            }
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            return null;
        }

        public override bool CheckActive() => false;

        /// <summary>
        /// 死亡演出接管: 首次"致死"只进入 DeathAnim 状态锁血 1, 演出末尾由状态机真正击杀。
        /// </summary>
        public override bool CheckDead() {
            if (reallyDead)
                return true;
            if (CurrentState != AoyuanState.DeathAnim) {
                BeginDeathAnim();
            }
            NPC.life = 1;
            NPC.dontTakeDamage = true;
            return false;
        }

        public override void OnKill() {
            Systems.DownedBossSystem.downedAoyuan = true;

            if (Main.netMode != NetmodeID.Server) {
                AoyuanHelper.CreateIceBurst(NPC.Center, 200f, 4, 20);
            }
        }

        /// <summary>绝对零度被打断后的踉跄易伤窗（×1.3 承伤）</summary>
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            if (staggerTimer > 0)
                modifiers.FinalDamage *= 1.3f;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (NPC.life <= 0 && Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 30; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                    int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
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

        public override void BossHeadRotation(ref float rotation) {
            rotation = NPC.rotation;
        }

        #endregion
    }
}
