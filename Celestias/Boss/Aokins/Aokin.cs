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
    /// 南海龙王敖钦 - 月后初期Boss
    /// 火属性龙王主题，蛇形多段身体结构
    /// 一阶段：龙王巡游，火弹和龙息
    /// 二阶段：狂暴冲刺，烈焰龙卷和陨石雨
    /// </summary>
    [AutoloadBossHead]
    internal partial class Aokin : ModNPC
    {
        #region 常量定义

        /// <summary>二阶段血量百分比阈值</summary>
        public const float Phase2Threshold = 0.50f;

        /// <summary>蛇形身体段数</summary>
        private const int SegmentCount = 30;

        /// <summary>身体段间距</summary>
        private const int SegmentGap = 48;

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,
            // 开场封路
            Intro_SummonBarriers,
            // 一阶段
            Phase1_Patrol,
            Phase1_FireBarrage,
            Phase1_DragonBreath,
            Phase1_TailWhip,
            Phase1_MeteorRain,
            // 阶段转换
            PhaseTransition_2,
            // 二阶段
            Phase2_FuryCharge,
            Phase2_FlameVortex,
            Phase2_InfernoBreath,
            Phase2_MeteorStorm,
            Phase2_Divebomb,
            Phase2_SurpriseFireball
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

        // 私有状态
        private float globalTime;
        private int seed;
        private Random random;
        private float introProgress;
        private bool didPhase2Transition;

        // 蛇形身体
        private Vector2[] segmentPos = new Vector2[SegmentCount];
        private float[] segmentRot = new float[SegmentCount];
        private float tailTurnSpeed = 12f;

        // 冲刺控制
        private Vector2 chargeTarget;
        private int chargeCount;
        private int maxChargeCount;

        // 俯冲冷却
        private int divebombCooldown;

        // 封路龙卷控制
        private int[] barrierTornadoIds;
        private bool hasSpawnedBarriers;

        // 视觉效果
        private float flameAuraAlpha;
        private float flameRotation;
        private float flameScale;
        private float glowIntensity;

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

            Phase = BossPhase.Intro;
            PhaseTimer = 0;

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
            writer.Write((int)Phase);
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(chargeCount);
            writer.Write(divebombCooldown);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            chargeCount = reader.ReadInt32();
            divebombCooldown = reader.ReadInt32();

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
