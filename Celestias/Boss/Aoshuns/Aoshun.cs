using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    /// <summary>
    /// 北海龙王敖顺 - 月后初期Boss
    /// 雷电/风暴属性龙王主题，蠕虫多段NPC身体结构
    /// 头部纹理3帧（112×438），身体纹理5帧（112×320）
    /// 一阶段：龙王巡游，雷球和雷息
    /// 二阶段：狂暴冲刺，雷暴龙卷和雷柱雨
    /// </summary>
    [AutoloadBossHead]
    internal partial class Aoshun : ModNPC
    {
        #region 常量定义

        /// <summary>二阶段血量百分比阈值</summary>
        public const float Phase2Threshold = 0.50f;

        /// <summary>蠕虫身体段数（Body+Arms交替，不含尾部）</summary>
        private const int WormBodyLength = 30;

        /// <summary>头部纹理帧数（52×140, 2帧, 每帧52×70）</summary>
        private const int HeadFrameCount = 2;

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,
            // 一阶段
            Phase1_Patrol,
            Phase1_ThunderBarrage,
            Phase1_LightningBreath,
            Phase1_TailWhip,
            Phase1_ThunderRain,
            // 阶段转换
            PhaseTransition_2,
            // 二阶段
            Phase2_FuryCharge,
            Phase2_ThunderVortex,
            Phase2_StormBreath,
            Phase2_AbyssalThunder,
            Phase2_Divebomb,
            Phase2_ThunderLance
        }

        #endregion

        #region 状态字段

        // 使用localAI存储阶段状态，ai[0]用于蠕虫初始化标记
        // ai[0]: 蠕虫是否已初始化（0=未初始化，1=已初始化）
        // ai[1]: 通用计时器
        // ai[2]: 未使用
        // ai[3]: 脱战计时器

        /// <summary>是否处于二阶段</summary>
        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;

        // 阶段状态（用internalAI网络同步）
        public float[] internalAI = new float[4];
        // internalAI[0]: 攻击总计时器
        // internalAI[1]: 攻击类型选择
        // internalAI[2]: 阶段标记
        // internalAI[3]: 子状态

        // 私有状态
        private bool despawn;
        private bool close;
        private bool chargePlayer;
        private bool fireAttack;
        private int attackFrame;
        private int attackCounter;
        private int attackTimer;
        private bool didPhase2Transition;

        // 冲刺控制
        private Vector2 chargeTarget;
        private int chargeCount;
        private int maxChargeCount;

        // 俯冲冷却
        private int divebombCooldown;

        // 雷柱激光冷却
        private int beamCooldown;

        // 龙息连射计数
        private int breathBurstCount;

        // 视觉效果
        private float globalTime;
        private float thunderAuraAlpha;
        private float glowIntensity = 1f;

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
