using AncientChineseMythology.Celestias.Boss.Aoshuns.Items;
using AncientChineseMythology.Items.Materials;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    /// <summary>
    /// 北海龙王敖顺 - 月后初期Boss
    /// 雷暴/风暴属性蠕虫龙王
    /// 
    /// ==========  设计理念（与敖闰完全差异化）  ==========
    /// ● AI架构：正规状态机（巡逻→预攻击→攻击→冷却），替代敖闰的0-400计时器循环
    /// ● 核心机制：风暴蓄电 - 钻地移动积攒电荷，电荷满时身体带电增伤
    /// ● 攻击体系：8个全新攻击（雷链穿刺/深渊伏击/龙鳞风暴/龙卷缠绕/天雷印/龙王怒啸/风暴之眼/雷霆连环冲）
    /// ● 移动差异：一阶段以钻地为主，二阶段加入空中盘旋→俯冲交替
    /// </summary>
    [AutoloadBossHead]
    internal partial class Aoshun : ModNPC
    {
        #region 常量定义

        /// <summary>二阶段血量百分比阈值</summary>
        public const float Phase2Threshold = 0.50f;

        /// <summary>头部纹理帧数（2帧）</summary>
        private const int HeadFrameCount = 2;

        /// <summary>巡逻状态下基础速度</summary>
        private const float PatrolSpeed = 16f;
        /// <summary>二阶段巡逻速度</summary>
        private const float PatrolSpeedPhase2 = 22f;
        /// <summary>冲刺速度</summary>
        private const float ChargeSpeed = 32f;

        /// <summary>风暴蓄电最大值</summary>
        private const float MaxStormCharge = 100f;
        /// <summary>每帧钻地蓄电量</summary>
        private const float ChargePerDigTick = 0.35f;

        /// <summary>巡逻切换攻击的最小间隔（帧）</summary>
        private const int MinPatrolDuration = 180;
        /// <summary>巡逻切换攻击的最大间隔（帧）</summary>
        private const int MaxPatrolDuration = 360;

        /// <summary>攻击后冷却帧数</summary>
        private const int CooldownDuration = 60;

        #endregion

        #region 状态枚举

        /// <summary>AI主状态</summary>
        public enum AoshunState
        {
            Intro,          // 出场动画
            Patrol,         // 蠕虫钻地巡逻，积攒电荷
            PreAttack,      // 攻击前短暂蓄力/电报
            Attacking,      // 执行攻击
            Cooldown,       // 攻击后冷却
            Submerge,       // 深潜（深渊伏击专用）
            Emerge,         // 从地下爆出
            PhaseTransition // 阶段转换
        }

        /// <summary>攻击类型</summary>
        public enum AoshunAttackType
        {
            // --- 一阶段攻击 ---
            ChainLightning,     // 雷链穿刺：释放闪电节点，节点间连锁放电
            AbyssalAmbush,      // 深渊伏击：潜地消失→预警标记→脚下爆出+冲击波
            DragonScaleStorm,   // 龙鳞风暴：高速移动中身体段抛射带电龙鳞
            TornadoEnsnare,     // 龙卷缠绕：绕玩家盘旋释放追踪龙卷风
            ThunderSeal,        // 天雷印：标记玩家位置，延迟天雷轰击
            // --- 二阶段追加 ---
            DragonKingRoar,     // 龙王怒啸：全屏减速+降防debuff波
            EyeOfTheStorm,      // 风暴之眼：制造缩小安全区，区外持续高伤
            ThunderChainCharge  // 雷霆连环冲：多次快速穿越留持续电痕
        }

        /// <summary>一阶段攻击数量</summary>
        private const int Phase1AttackCount = 5;
        /// <summary>二阶段攻击数量</summary>
        private const int Phase2AttackCount = 8;

        #endregion

        #region 状态字段

        // NPC.ai[0]: 蠕虫是否已初始化（0=未初始化，1=已初始化）
        // NPC.ai[1]: 通用计时器（各状态复用）
        // NPC.ai[2]: 当前攻击类型（AoshunAttackType）
        // NPC.ai[3]: 脱战计时器

        /// <summary>是否处于二阶段</summary>
        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;

        // 使用internalAI做网络同步的额外状态
        public float[] internalAI = new float[4];
        // internalAI[0]: 当前主状态（AoshunState）
        // internalAI[1]: 攻击子状态/进度
        // internalAI[2]: 风暴蓄电值
        // internalAI[3]: 下次巡逻持续时间

        // --- 私有运行时状态 ---
        private bool despawn;
        private bool isUnderground;     // 当前是否在地下（碰撞检测）
        private bool didPhase2Transition;

        // 状态机
        private AoshunState CurrentState {
            get => (AoshunState)(int)internalAI[0];
            set => internalAI[0] = (float)value;
        }
        private float AttackProgress {
            get => internalAI[1];
            set => internalAI[1] = value;
        }
        private float StormCharge {
            get => internalAI[2];
            set => internalAI[2] = value;
        }

        /// <summary>风暴蓄电是否已满</summary>
        public bool IsFullyCharged => StormCharge >= MaxStormCharge;

        // 攻击专用
        private int attackTimer;
        private int patrolTimer;        // 巡逻计时
        private int patrolDuration;     // 本次巡逻总时长

        // 深渊伏击
        private Vector2 ambushTarget;
        private int ambushWarningTimer;

        // 龙卷缠绕
        private float orbitAngle;
        private int tornadoCount;

        // 雷霆连环冲
        private int chainChargeCount;
        private int maxChainCharges;
        private Vector2 chargeDirection;

        // 龙鳞抛射计数
        private int scaleBarrageTimer;

        // 近距离判定
        private bool close;

        // 视觉效果
        private float globalTime;
        private float stormAuraAlpha;

        // V2 风暴屏幕演出标量（纯本地视觉，AoshunStormScreenSystem 驱动）
        private float stormTintFx;          // 风暴压暗强度 0~1（平滑跟随电量/阶段）
        private bool stormWasFullyCharged;  // 满电"雷暴临界"边沿检测（一次性演出）
        private bool stormEyeActive;        // 风暴之眼安全区是否生效
        private Vector2 stormEyeCenter;     // 风暴之眼中心（世界）
        private float stormEyeRadius;       // 风暴之眼当前半径（世界像素）

        // 攻击历史（避免连续相同攻击）
        private AoshunAttackType lastAttack = (AoshunAttackType)(-1);

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
            if (Main.netMode == NetmodeID.Server || Main.dedServ) {
                writer.Write(internalAI[0]);
                writer.Write(internalAI[1]);
                writer.Write(internalAI[2]);
                writer.Write(internalAI[3]);
            }
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                internalAI[0] = reader.ReadSingle();
                internalAI[1] = reader.ReadSingle();
                internalAI[2] = reader.ReadSingle();
                internalAI[3] = reader.ReadSingle();
            }
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            return null;
        }

        public override bool CheckActive() => false;

        public override void OnKill() {
            Systems.DownedBossSystem.downedAoshun = true;

            if (Main.netMode != NetmodeID.Server) {
                AoshunHelper.CreateThunderBurst(NPC.Center, 200f, 4, 20);
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
