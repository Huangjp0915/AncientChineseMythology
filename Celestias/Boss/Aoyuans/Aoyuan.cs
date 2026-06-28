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
    /// 西海龙王敖闰 - 月后初期Boss（第3条龙王脊柱，西海珠门控，敖钦之后）
    /// 冰霜/寒水属性蠕虫龙王主题
    ///
    /// ==========  重做设计理念（移植敖顺 FSM 架构）  ==========
    /// ● AI架构：正规状态机（出场→巡逻→预攻击→攻击→冷却→阶段转换），替代原 0-400 计时器循环
    /// ● 签名机制：永冻立场（Permafrost Field）——巡游时留下寒冰地痕，玩家站在地痕上叠加冰冻层（减速→3层冻结约1秒）
    /// ● 攻击体系：6个带预告的命名攻击（冰晶棋局/暴雪帷幕/寒霜吐息/冰柱雨/冰霜环 + 二阶段·绝对零度大招）
    /// ● 阶段转换：50% “浮空破境”——脱离贴地钻行，二阶段地痕令地面打滑，解锁空中俯冲攻击（改规则而非加弹）
    /// ● 蠕虫身体：绝对零度蓄力时身体段暴露冰晶弱点，玩家击破弱点可打断/削弱全屏冻结
    /// </summary>
    [AutoloadBossHead]
    internal partial class Aoyuan : ModNPC
    {
        #region 常量定义

        /// <summary>二阶段血量百分比阈值</summary>
        public const float Phase2Threshold = 0.50f;

        /// <summary>蠕虫身体段帧序列（对应AoyuanBody纹理5帧中的帧号）</summary>
        public static readonly int[] BodyFrameSequence = [1, 2, 0, 1, 2, 1, 2, 0, 1, 2, 1, 2, 0, 1, 2, 3, 4];

        /// <summary>头部纹理帧数</summary>
        private const int HeadFrameCount = 3;

        /// <summary>一阶段巡游速度</summary>
        private const float PatrolSpeed = 12f;
        /// <summary>二阶段巡游速度</summary>
        private const float PatrolSpeedPhase2 = 15f;

        /// <summary>巡逻切换攻击的最小间隔（帧）</summary>
        private const int MinPatrolDuration = 150;
        /// <summary>巡逻切换攻击的最大间隔（帧）</summary>
        private const int MaxPatrolDuration = 300;

        /// <summary>预攻击（蓄力电报）帧数</summary>
        private const int PreAttackDuration = 45;
        /// <summary>攻击后冷却帧数</summary>
        private const int CooldownDuration = 55;

        #endregion

        #region 状态枚举

        /// <summary>AI主状态机</summary>
        public enum AoyuanState
        {
            Intro,           // 出场
            Patrol,          // 蠕虫巡游追踪，铺设永冻地痕
            PreAttack,       // 攻击前的蓄力电报
            Attacking,       // 执行攻击
            Cooldown,        // 攻击后冷却
            PhaseTransition  // 50% 浮空破境
        }

        /// <summary>攻击类型</summary>
        public enum AoyuanAttackType
        {
            // --- 一阶段 ---
            GlacialPillarChess, // 冰晶棋局：预告 3x3 幽灵冰柱，仅部分落下
            BlizzardVeil,       // 暴雪帷幕：推进的雪墙，留一道移动缺口
            FrostBreath,        // 寒霜吐息：张嘴蓄力 → 锥形冰锥吐息（专用张嘴动画）
            IcicleRainCombo,    // 冰柱雨：多波次天降冰柱
            FrostRingCombo,     // 冰霜环：环形冰弹 + 冰柱穿插
            // --- 二阶段追加 ---
            AbsoluteZero        // 绝对零度：锚定 + 3秒吸气蓄力 → 全屏放射冻结（可破弱点）
        }

        /// <summary>一阶段攻击数量</summary>
        private const int Phase1AttackCount = 5;
        /// <summary>二阶段攻击数量</summary>
        private const int Phase2AttackCount = 6;

        #endregion

        #region 状态字段

        // NPC.ai[0]: 蠕虫是否已初始化（0=未初始化，1=已初始化）
        // NPC.ai[1]: 通用计时器（保留）
        // NPC.ai[2]: 当前攻击类型（AoyuanAttackType）
        // NPC.ai[3]: 脱战计时器

        /// <summary>是否处于二阶段</summary>
        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;

        // 阶段状态（用internalAI网络同步）
        public float[] internalAI = new float[4];
        // internalAI[0]: 当前主状态（AoyuanState）
        // internalAI[1]: 攻击进度（保留）
        // internalAI[2]: 是否已浮空破境（0/1）
        // internalAI[3]: 下次巡逻时长

        private AoyuanState CurrentState {
            get => (AoyuanState)(int)internalAI[0];
            set => internalAI[0] = (float)value;
        }

        // 私有运行时状态
        private bool despawn;
        private bool didPhase2Transition;

        // 张嘴动画（寒霜吐息/大招）
        private bool fireAttack;
        private int attackFrame;
        private int attackCounter;

        // 通用攻击计时
        private int attackTimer;
        private int patrolTimer;
        private int patrolDuration;

        // 永冻地痕节流
        private int trailTimer;

        // 暴雪帷幕计数
        private int veilCount;
        // 冰柱雨/冰霜环波次
        private int waveCount;

        // 绝对零度弱点机制（公开供身体段读取）
        /// <summary>绝对零度蓄力中：身体段暴露冰晶弱点</summary>
        public bool WeakPointsExposed;
        /// <summary>蓄力期间身体段累计承受的伤害（用于判断是否打断）</summary>
        public int WeakPointDamageTaken;

        // 视觉效果
        private float globalTime;
        private float glowIntensity = 1f;

        // V2 霜冻屏幕演出标量（纯本地视觉, 0~1, 由 UpdateFrostScreenFx 平滑驱动）
        private float frostTint;    // ElementalScreenTint 二阶段氛围底色
        private float frostWarp;    // GenericWarp(frost) 全屏扭曲（仅大招/破境的签名时刻）
        private float freezeBloom;  // 绝对零度释放冻爆泛光（释放瞬间置 1, 逐帧衰减）
        private float arenaRunic;   // 蓄力期向心收口霜冻法阵地纹

        // 攻击历史（避免连续相同攻击）
        private AoyuanAttackType lastAttack = (AoyuanAttackType)(-1);

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
            Systems.DownedBossSystem.downedAoyuan = true;

            if (Main.netMode != NetmodeID.Server) {
                AoyuanHelper.CreateIceBurst(NPC.Center, 200f, 4, 20);
            }
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

