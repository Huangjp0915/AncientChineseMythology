using AncientChineseMythology.Celestias.Boss.AoGuangs.Items;
using System;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    /// <summary>
    /// 东海龙王敖广 - 月后初期Boss
    /// 水属性龙王主题，参考猪鲨的攻击模式
    /// 一阶段：龙王巡游，水弹和旋涡
    /// 二阶段：狂暴冲刺，召唤虾兵蟹将
    /// 三阶段：龙王怒涛，终极水柱激光
    /// </summary>
    [AutoloadBossHead]
    internal partial class AoGuang : ModNPC
    {
        #region 常量定义

        /// <summary>二阶段血量百分比阈值</summary>
        public const float Phase2Threshold = 0.65f;

        /// <summary>三阶段血量百分比阈值</summary>
        public const float Phase3Threshold = 0.30f;

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,
            // 开场封路
            Intro_SummonBarriers,
            // 一阶段
            Phase1_Patrol,
            Phase1_WaterBarrage,
            Phase1_VortexSummon,
            Phase1_TidalWave,
            Phase1_BubbleStorm,
            Phase1_CoralSpike,
            // 阶段转换
            PhaseTransition_2,
            // 二阶段
            Phase2_Charge,
            Phase2_SummonMinions,
            Phase2_Whirlpool,
            Phase2_DragonBreath,
            Phase2_TornadoRush,
            Phase2_TsunamiWall,
            Phase2_DragonClaw,
            // 阶段转换
            PhaseTransition_3,
            // 三阶段
            Phase3_FuryCharge,
            Phase3_TridentStorm,
            Phase3_TidalBeam,
            Phase3_DragonCoil,
            Phase3_FinalTsunami,
            Phase3_SeaDragonDance,
            Phase3_AbyssalVortex
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

        /// <summary>是否处于二阶段</summary>
        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;

        /// <summary>是否处于三阶段</summary>
        public bool IsPhase3 => NPC.life < NPC.lifeMax * Phase3Threshold;

        // 私有状态
        private float globalTime;
        private int seed;
        private Random random;
        private float introProgress;
        private bool didPhase2Transition;
        private bool didPhase3Transition;

        // 冲刺控制
        private Vector2 chargeTarget;
        private Vector2 chargeVelocity;
        private int chargeCount;
        private int maxChargeCount;

        // 旋涡控制
        private float vortexAngle;
        private float vortexRadius;

        // 龙息控制 - public供弹幕访问
        public float breathAngle;
        private bool isBreathActive;

        // 封路龙卷控制
        private int[] barrierTornadoIds;
        private bool hasSpawnedBarriers;

        // 龙爪攻击控制
        private Vector2[] clawPositions;
        private int clawIndex;

        // 视觉效果
        private float waveRotation;
        private float waveScale;
        private float glowIntensity;
        private float waterAuraAlpha;
        private float tailSwayPhase;

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
            NPC.width = 150;
            NPC.height = 150;
            NPC.damage = 120;
            NPC.defense = 60;
            NPC.lifeMax = 450000; // 月后初期级别血量
            NPC.HitSound = SoundID.NPCHit56;
            NPC.DeathSound = SoundID.NPCDeath62;
            NPC.value = Item.buyPrice(platinum: 1, gold: 50);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 20f;
            NPC.aiStyle = -1;

            // 调整难度
            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.35f);
                NPC.damage = (int)(NPC.damage * 1.25f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.35f);
            }

            Music = MusicID.Boss2; // 可替换为自定义音乐
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.GreaterHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<AbyssalDragonblade>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<JadeDragonChakram>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<MaelstromBow>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<TidecallersDecree>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<TsunamiPiercer>(), 5));
        }

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(10000);
            random = new Random(seed);

            // 初始化视觉效果
            waveRotation = 0f;
            waveScale = 1f;
            glowIntensity = 1f;
            waterAuraAlpha = 0f;
            tailSwayPhase = 0f;

            Phase = BossPhase.Intro;
            PhaseTimer = 0;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.netUpdate = true;
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
            writer.Write((int)Phase);
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.WriteVector2(chargeTarget);
            writer.Write(chargeCount);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            chargeTarget = reader.ReadVector2();
            chargeCount = reader.ReadInt32();

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

        #endregion
    }
}
