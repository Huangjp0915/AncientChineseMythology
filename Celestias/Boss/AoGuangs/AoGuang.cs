using AncientChineseMythology.Celestias.Boss.AoGuangs.Items;
using AncientChineseMythology.Items.Materials;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    /// <summary>
    /// 东海龙王敖广 — 月后初期天界 Boss (V3 重做)。
    /// 主题「东海潮主·定海龙王」: 四海之长, 以潮汐节律作战 —— 涨潮(蛇形巡曳) → 憋潮(定身蓄势) → 溃堤(穿刺/浪墙)。
    /// 与三弟区分: 敖钦=炎热 / 敖顺=风暴 / 敖闰=冰霜, 敖广 = 纯水的质量与秩序。
    /// 编排骨架: 洗牌袋选招 + 巡曳连接拍 + 三大演出(背景冲镜入场 / 没顶 / 定海潮止) + 死亡「潮退归海」。
    /// </summary>
    [AutoloadBossHead]
    internal partial class AoGuang : ModNPC
    {
        #region 常量定义

        /// <summary>二阶段血量百分比阈值</summary>
        public const float Phase2Threshold = 0.65f;

        /// <summary>三阶段血量百分比阈值</summary>
        public const float Phase3Threshold = 0.30f;

        /// <summary>距离栓绳: 与目标超过该距离时强制回追 (防飞屏绕圈)</summary>
        public const float LeashDistance = 1700f;

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            /// <summary>入场全脚本: 背景冲镜 → 静止威压 → 戟落 → 封路龙卷</summary>
            Intro,
            /// <summary>巡曳连接拍: 蛇形缓游, 段落呼吸口, 结束时服务器选招</summary>
            Cruise,
            /// <summary>P1 潮弓三连: 三波扇形潮矢</summary>
            TideBoltVolley,
            /// <summary>P1/P2 穿刺巡游: 盘旋-锁线-反吸-穿刺</summary>
            PierceRun,
            /// <summary>P1 潮涌立柱: 地面水柱依次喷发</summary>
            SurgePillars,
            /// <summary>相变一「没顶」: 冲天离场 + 水位吞屏 + 穿越浪墙 + 破水回场</summary>
            Transition2,
            /// <summary>P2 签名·浪墙层涌: 多波整面浪墙给穿越缺口</summary>
            TsunamiWaves,
            /// <summary>P2 水龙卷投掷: 掷出行走水龙卷</summary>
            TornadoThrow,
            /// <summary>P2/P3 龙息水柱: 蓄力扫射水束</summary>
            DragonBreath,
            /// <summary>相变二「定海·潮止」: 全屏静止 → 戟落 impact frame</summary>
            Transition3,
            /// <summary>P3 签名·深渊漩涡: 定点漩涡 + 切向穿刺</summary>
            AbyssalMaw,
            /// <summary>P3 终潮天倾: 半场天倾巨浪 ×2 + 贯场穿刺</summary>
            SkyfallTide,
            /// <summary>P3 狂龙连刺: 三连高速穿刺 + 末刺浪爆</summary>
            FuryPierce,
            /// <summary>死亡演出「潮退归海」: 失控螺旋 → 顶点定格 → 水爆散身</summary>
            Death
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

        /// <summary>是否达到二阶段血线</summary>
        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;

        /// <summary>是否达到三阶段血线</summary>
        public bool IsPhase3 => NPC.life < NPC.lifeMax * Phase3Threshold;

        // ===== 同步状态 (SendExtraAI) =====
        private bool didPhase2Transition;
        private bool didPhase3Transition;
        private bool deathAnimStarted;

        /// <summary>穿刺锁定的目标点 / 漩涡定点 / 天倾分界锚点 (服务器决定, 同步)</summary>
        private Vector2 chargeTarget;
        /// <summary>本轮已完成的穿刺/波次计数</summary>
        private int chargeCount;
        /// <summary>浪墙来向 / 天倾危险半场方向 (-1 左 / +1 右)</summary>
        private float wallDir = 1f;
        /// <summary>龙息扫射方向 (-1/+1)</summary>
        private float sweepDir = 1f;

        // ===== 服务器专用 (选招洗牌袋, 客户端从 ai[0] 跟随, 不需同步) =====
        private BossPhase[] attackBag;
        private int bagCursor;
        private BossPhase lastAttack = BossPhase.Cruise;

        // ===== 龙息/穿刺角度 (弹幕读取; 服务器写入后经 SendExtraAI 同步) =====
        public float breathAngle;

        // ===== 编排暂存 (由同步状态每帧推导) =====
        private int maxChargeCount;
        /// <summary>接触伤害窗口: 仅穿刺爆发帧为 true (伤害窗口与视觉严格对齐)</summary>
        private bool contactDamageActive;
        /// <summary>本帧的姿态覆盖角 (演出拍用, NaN=跟随速度)</summary>
        private float poseRotOverride = float.NaN;

        // ===== 纯视觉状态 (本地, 不同步) =====
        private float globalTime;
        private float visualZ;             // fake-Z: scale = 1/(Z+1), 入场/相变冲镜
        private float eyeRedLerp;          // 三阶段龙眼转红
        private float dissolveProgress;    // 死亡溶解进度
        private float pierceGlow;          // 速度门控的龙躯能量档
        private float glowIntensity = 1f;
        private float waterAuraAlpha;
        private float tidalRingVisual;     // 戟落冲击环 (纯视觉, 1→0)
        private float stillness;           // 「潮止」静默档 (抑制环境粒子)

        // ===== 屏幕演出标量 (本地) =====
        private float submersionWarp;      // 全屏折射强度
        private float tideTint;            // 潮汐氛围底色强度
        private float waterBloom;          // 潮涌泛光 (事件触发置 1, 自衰减)
        private float vortexInward;        // 深渊漩涡向心吸力 0~1
        private float waterLevel;          // 水位线高度占屏比 0~1
        private float waterLevelTarget;    // 水位目标 (状态机驱动)
        private float impactFrame;         // impact frame 0~1 (戟落定格, 一场唯一)

        #endregion

        #region ModNPC重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            // 长轨迹缓存: 供龙躯水流 ribbon 使用 (34 点 spine)
            NPCID.Sets.TrailingMode[Type] = 2;
            NPCID.Sets.TrailCacheLength[Type] = 34;
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
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<AbyssalDragonblade>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<JadeDragonChakram>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<MaelstromBow>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<TidecallersDecree>(), 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<TsunamiPiercer>(), 5));
        }

        public override void OnSpawn(IEntitySource source) {
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            visualZ = 6f; // 从背景深处冲向镜头

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.netUpdate = true;
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(deathAnimStarted);
            writer.WriteVector2(chargeTarget);
            writer.Write(chargeCount);
            writer.Write(wallDir);
            writer.Write(sweepDir);
            writer.Write(breathAngle);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            deathAnimStarted = reader.ReadBoolean();
            chargeTarget = reader.ReadVector2();
            chargeCount = reader.ReadInt32();
            wallDir = reader.ReadSingle();
            sweepDir = reader.ReadSingle();
            breathAngle = reader.ReadSingle();
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        /// <summary>接触伤害仅在穿刺爆发帧生效 (伤害窗口与视觉严格对齐)。</summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => contactDamageActive;

        /// <summary>
        /// 死亡演出接管: 首次致死改为进入「潮退归海」演出 (清弹/无敌/伤害归零),
        /// 演出结束后由 AI 端调用 checkDead 真正死亡 (保留掉落与 downed 标记)。
        /// </summary>
        public override bool CheckDead() {
            if (!deathAnimStarted) {
                deathAnimStarted = true;
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                NPC.damage = 0;
                contactDamageActive = false;
                // 封路龙卷不即时清除: 它们检测到 Death 状态后自行淡出且不再造成伤害
                ClearHostileProjectiles(keepBarriers: true);
                TransitionTo(BossPhase.Death);
                NPC.netUpdate = true;
                return false;
            }
            return true;
        }

        #endregion
    }
}
