using AncientChineseMythology.Celestias.Boss.Dazhengs.Items;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dazhengs
{
    /// <summary>
    /// 大椿 — 上古树神 (固定不动)，月球领主后超级 Boss。
    ///
    /// V2「四季轮转 Cycle of Seasons」重做：
    ///  ● 保留出色的「破土升起」入场 (290-tick 4 段时间轴) + 收缩竞技场屏障。
    ///  ● <b>G5 门控</b>：入场后大椿无敌, 四角生成季节锚点; 须先毁 3 个才进入可受伤的季节循环 (4.5M 血"挣得起")。
    ///  ● <b>四季签名</b>：春(藤蔓迷宫+活体根须安全岛) / 夏(落叶雨) / 秋(黄金幻影诱饵 DPS 谜题) / 冬(减速+冰藤+生命汲取治疗线)。
    ///    每季 = 一招 + 一条战场规则; 击毁锚点可主动切换主导季节并开破绽窗口。
    ///  ● 表现走硬化 ACMShaders：PaletteLUT 四季调色 / ElementalScreenTint 季节氛围 / ArenaRunic 根须地纹 / DrawBeam 治疗线。
    ///
    /// V3 提升补强 (三大节拍齐备 + 重量感 + 波形节奏)：
    ///  ● <b>死亡演出「归土」</b>：CheckDead 拦截 → 叶落寂静 → 结界碎裂 → 巨躯沉回大地 → 留白 → 生命归还山林的金绿终爆 (~5s)。
    ///  ● <b>树身活化</b>：底部枢轴阻尼摇曳 + 呼吸缩放 + 树冠辉光滞后摆动; 发招/受创注入摇曳冲量 (质量即反应)。
    ///  ● <b>换阶段「四季失序」升级</b>：清弹 → 收束 → 静默坍缩拍 → RadialBloom 金爆 + 结界首现裂纹。
    ///  ● <b>季间连接拍</b>：48t 无弹幕段落 + 季语宣告 + Lifeburst 光环 + 新弹幕 60t 风速阀。
    ///  ● <b>季内高潮拍</b>：春·根刺阵(点名破土) / 夏·金叶风暴(收束→静默→三环 nova) / 秋·心跳定位+幻影压力 / 冬·饱食爆发(挣断可免)。
    ///  ● <b>结界演变</b>：裂纹随血量/阶段加深 (uCrack), 死亡时碎裂 — 专属 DazhengArenaCircle 扩展 + 专属 DazhengLifeburst 生命冲击环。
    ///
    /// 贴图尺寸：1024×558。逻辑服务器权威, 绘制 client-only。
    /// </summary>
    [AutoloadBossHead]
    public class Dazheng : ModNPC
    {
        #region 常量

        public const int TextureWidth = 1024;
        public const int TextureHeight = 558;
        public const float Phase2Threshold = 0.55f;

        // 锚点角位偏移 (战场四角)
        private const float AnchorOffsetX = 980f;
        private const float AnchorOffsetY = 660f;
        private const int GateAnchorCount = 4;
        private const int GatePassRemain = 1; // 4 - 3 = 还剩 1 即视为已毁 3 个 → 通过

        /// <summary>季间连接拍时长: 无敌意弹幕的段落呼吸 (季语宣告 + 生命光环)。</summary>
        private const int SeasonConnectorTicks = 48;

        // 死亡演出「归土」时间轴 (~5s)
        private const int DeathQuietEnd = 70;    // 「寂」: 叶暴落 + 颤抖 + 世界褪色
        private const int DeathSinkStart = 90;   // 「归土」: 巨躯沉回大地
        private const int DeathSinkEnd = 230;
        private const int DeathStillEnd = 268;   // 「静」: 终爆前留白
        private const int DeathFinish = 300;     // 真实死亡 (掉落/旗标)

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,
            Gate,            // G5 门控：毁 3 锚点
            SeasonCombat,    // 四季轮转主循环
            PhaseTransition_2,
            DeathCinematic   // 死亡演出「归土」(CheckDead 拦截)
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

        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;

        /// <summary>当前主导季节 (0~3, 见 <see cref="DazhengSeasons"/>)。供屏障/天幕换色读取。</summary>
        public int CurrentSeason => season;
        /// <summary>门控是否已通过。</summary>
        public bool GatePassed => gatePassed;
        /// <summary>是否处于死亡演出 (供屏障/天幕联动)。</summary>
        public bool IsDying => Phase == BossPhase.DeathCinematic;
        /// <summary>死亡演出进度 0~1 (供天幕联动)。</summary>
        public float DeathProgress => IsDying ? MathHelper.Clamp(PhaseTimer / (float)DeathFinish, 0f, 1f) : 0f;

        /// <summary>
        /// 结界裂纹强度 0~1 (供 DazhengArenaBarrier 读取, 驱动 uCrack):
        /// P1 满血 0 → P2 起 0.35 基底, 随血量降至濒死升到 ~0.85; 死亡演出内冲向 1。
        /// </summary>
        public float BarrierCrack {
            get {
                if (IsDying) {
                    // 「寂」段内 0.85 → 1.0, 之后保持 1 (碎裂由屏障自己的时间轴接管)
                    return MathHelper.Clamp(0.85f + 0.15f * (PhaseTimer / (float)DeathQuietEnd), 0.85f, 1f);
                }
                if (!didPhase2Transition && !IsPhase2)
                    return 0f;
                // P2: 0.35 基底 + 血量越低裂得越深 (55% → 0% 血映射 0.35 → 0.85)
                float lifeFrac = MathHelper.Clamp(NPC.life / (float)NPC.lifeMax, 0f, 1f);
                float t = 1f - MathHelper.Clamp(lifeFrac / Phase2Threshold, 0f, 1f);
                return MathHelper.Lerp(0.35f, 0.85f, t);
            }
        }

        // —— 季节锚点 → 大椿 的跨实体通信 (服务器权威; 大椿在 AI 中消费) ——
        internal static int RecentBrokenSeason = -1;
        internal static ulong RecentBrokenFrame;
        internal static bool DecoyKilled;
        internal static ulong DecoyEventFrame;
        /// <summary>冬季治疗线是否处于汲取阶段 (供 DazhengHealThread 切换视觉)。</summary>
        public static bool HealThreadActive;

        private float globalTime;
        private bool didPhase2Transition;
        private Vector2 spawnPosition;
        private float vineRotation;
        private float introRiseOffset;
        private bool isRising;

        // 季节状态
        private int season;          // 0 春 1 夏 2 秋 3 冬 (同步)
        private bool gatePassed;     // 门控是否通过 (同步)
        private int defenseDownTimer;// 破绽窗口 (同步)
        private int baseDef = 100;

        // 视觉/本地
        private float seasonFlash;   // 季节切换闪光 (本地)
        private float lutIntensity;  // PaletteLUT 强度 (本地)
        private Vector4 lutShadow, lutHi;
        private float lutSat = 1f;
        private float lutSatOverride = -1f; // ≥0 时覆写饱和度 (死亡褪色叙事)

        // —— 树身活化 (纯本地视觉: 底部枢轴摇曳 + 呼吸) ——
        private float swayAngle;     // 当前摇曳角 (rad)
        private float swayVel;       // 摇曳角速度 (阻尼弹簧)
        private float canopySway;    // 树冠辉光滞后摆动 (次级运动)
        private float breathPhase;   // 呼吸相位
        private int prevDefenseDown; // 破绽窗口上升沿检测 (客户端音效)
        private int prevLife;        // 受创冲量检测 (本地)

        // —— Lifeburst 生命冲击环 (本地视觉池, ≤3) ——
        private struct LifeRing
        {
            public float Age;       // 帧
            public float Lifetime;  // 帧
            public float MaxRadius; // 世界像素
            public float Thickness; // 世界像素
            public Color Core, Edge;
            public bool Active;
        }
        private readonly LifeRing[] lifeRings = new LifeRing[3];
        private static Asset<Effect> lifeburstEffect; // 专属着色器 (仅本 Boss 缓存, 不进 ACMShaders)

        // 服务器侧追踪 (不同步)
        private int decoyWhoAmI = -1;
        private int healThreadWhoAmI = -1;
        private int rootFieldWhoAmI = -1;
        private ulong lastConsumedBreakFrame;
        private bool anchorsSpawned;
        private int anchorRegrowTimer;
        private bool decoyWindowOpen;
        private bool decoyResolved;
        private bool healBroken;

        #endregion

        #region ModNPC 重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 0;
            NPCID.Sets.TrailCacheLength[Type] = 1;
        }

        public override void Unload() {
            lifeburstEffect = null;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 200;
            NPC.height = 200;
            NPC.damage = 250;
            NPC.defense = 100;
            NPC.lifeMax = 4500000;
            NPC.HitSound = SoundID.NPCHit1 with { Pitch = -0.5f };
            NPC.DeathSound = SoundID.NPCDeath1 with { Pitch = -0.8f };
            NPC.value = Item.buyPrice(platinum: 8);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 50f;
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
            potionType = ItemID.SuperHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            // 自然之斧 - 100%掉落
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<TheNaturalAxe>()));
            // 傲世神木 - 掉落45~60个
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<ArrogantDivineSylvan>(), minimumDropped: 45, maximumDropped: 60));
        }

        public override void OnSpawn(IEntitySource source) {
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            globalTime = 0;
            spawnPosition = FindGroundPosition(NPC.Center);
            introRiseOffset = TextureHeight + 100f;
            isRising = true;
            NPC.Center = spawnPosition + new Vector2(0, introRiseOffset);
            // 重置跨实体静态, 防上一场残留
            RecentBrokenSeason = -1;
            RecentBrokenFrame = 0;
            DecoyKilled = false;
            HealThreadActive = false;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        private Vector2 FindGroundPosition(Vector2 startPos) {
            int tileX = (int)(startPos.X / 16f);
            int startTileY = (int)(startPos.Y / 16f);

            int groundTileY = startTileY;
            for (int y = startTileY; y < startTileY + 150 && y < Main.maxTilesY - 1; y++) {
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    groundTileY = y;
                    break;
                }
            }

            float groundWorldY = groundTileY * 16f;
            float centerY = groundWorldY - TextureHeight / 2f;
            return new Vector2(startPos.X, centerY);
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((int)Phase);
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(spawnPosition.X);
            writer.Write(spawnPosition.Y);
            writer.Write(vineRotation);
            writer.Write(introRiseOffset);
            writer.Write(isRising);
            writer.Write(season);
            writer.Write(gatePassed);
            writer.Write(defenseDownTimer);
            writer.Write(baseDef);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            spawnPosition = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            vineRotation = reader.ReadSingle();
            introRiseOffset = reader.ReadSingle();
            isRising = reader.ReadBoolean();
            season = reader.ReadInt32();
            gatePassed = reader.ReadBoolean();
            defenseDownTimer = reader.ReadInt32();
            baseDef = reader.ReadInt32();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 2f;
            if (isRising || IsDying) return false;
            return null;
        }

        public override bool CheckActive() => false;

        public override void DrawBehind(int index) {
            // 破土升起 / 归土下沉 时藏入地形后
            if (isRising || (IsDying && PhaseTimer > DeathSinkStart)) {
                Main.instance.DrawCacheProjsBehindNPCsAndTiles.Add(index);
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            // 受击微幅摇曳 (质量即反应: 树身对每次打击有物理回应)
            swayVel += hit.HitDirection * 0.00045f;

            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.WoodFurniture, hit.HitDirection * 2f, -2f, 150, default, 1.5f);
                d.noGravity = false;
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.GrassBlades, hit.HitDirection * 1.5f, -1f, 100, default, 2f);
                d.noGravity = true;
            }

            if (NPC.life <= 0) {
                for (int i = 0; i < 60; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                        DustID.WoodFurniture, 0, 0, 100, default, 3f);
                    d.noGravity = true;
                    d.velocity *= 5f;
                }
                for (int i = 0; i < 40; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                        DustID.GrassBlades, 0, 0, 80, default, 2.5f);
                    d.noGravity = true;
                    d.velocity *= 4f;
                }
            }
        }

        /// <summary>
        /// 死亡拦截 → 「归土」演出: 首次致死不真正死亡, 进入 ~5s 的谢幕时间轴;
        /// 演出结束由服务器把 life 归零再次触发本方法, 此时放行真实死亡 (掉落/旗标不受影响)。
        /// </summary>
        public override bool CheckDead() {
            if (Phase == BossPhase.DeathCinematic)
                return true;

            NPC.life = 1;
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
            HealThreadActive = false;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                // 公平阀门 & 舞台清空: 抹掉全部敌意弹幕与所有随从载体
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.hostile && p.damage > 0)
                        p.Kill();
                }
                KillTrackedNPC(ref decoyWhoAmI);
                KillTrackedProjectile(ref healThreadWhoAmI);
                KillTrackedProjectile(ref rootFieldWhoAmI);
                int anchorType = ModContent.NPCType<DazhengSeasonAnchor>();
                foreach (NPC n in Main.ActiveNPCs) {
                    if (n.type == anchorType && (int)n.ai[0] == NPC.whoAmI) {
                        n.life = 0;
                        n.active = false;
                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendData(MessageID.SyncNPC, number: n.whoAmI);
                    }
                }
            }

            TransitionTo(BossPhase.DeathCinematic);
            return false;
        }

        public override void OnKill() {
            DownedBossSystem.downedDazheng = true;
            HealThreadActive = false;
            if (Main.netMode != NetmodeID.Server) {
                // 演出已把最大的一拍交给"生"之终爆, 此处只留收尾标点
                PunchCameraModifier modifier = new(NPC.Center,
                    (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(),
                    14f, 8f, 50, 3000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        #endregion

        #region AI 主循环

        public override void AI() {
            globalTime += 1f / 60f;

            NPC.velocity = Vector2.Zero;
            if (spawnPosition != Vector2.Zero) {
                NPC.Center = isRising ? spawnPosition + new Vector2(0, introRiseOffset) : spawnPosition;
            }

            NPC.behindTiles = isRising || (IsDying && PhaseTimer > DeathSinkStart);
            if (isRising) {
                for (int i = 0; i < 110; i++)
                    Lighting.AddLight(NPC.Center + VaultUtils.RandVr(0, 500), Color.Green.ToVector3() * 10);
            }

            UpdateBodyMotion();
            UpdateLifeRings();

            // 死亡演出: 不依赖目标存活, 时间轴自走到底
            if (Phase == BossPhase.DeathCinematic) {
                PhaseTimer++;
                RunDeathCinematic();
                UpdateSeasonVisuals();
                return;
            }

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            CheckPhaseTransition();
            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.Gate: RunGate(target); break;
                case BossPhase.SeasonCombat: RunSeasonCombat(target); break;
                case BossPhase.PhaseTransition_2: RunPhaseTransition2(target); break;
            }

            UpdateDefenseWindow();
            UpdateSeasonVisuals();

            // 树神的自然光辉
            float glow = 0.6f + MathF.Sin(globalTime * 2f) * 0.2f;
            Lighting.AddLight(NPC.Center, new Vector3(0.3f, 0.6f, 0.15f) * glow);

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                Vector2 leafPos = NPC.Center + new Vector2(
                    Main.rand.NextFloat(-TextureWidth / 2, TextureWidth / 2),
                    Main.rand.NextFloat(-TextureHeight / 2, TextureHeight / 4));
                Dust d = Dust.NewDustDirect(leafPos, 0, 0, DustID.GrassBlades,
                    Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(1f, 3f), 100, default, 1.5f);
                d.noGravity = false;
                d.fadeIn = 1.2f;
            }
        }

        /// <summary>树身活化: 底部枢轴阻尼弹簧摇曳 + 呼吸 + 树冠滞后 (纯本地视觉, 服务器不耗费)。</summary>
        private void UpdateBodyMotion() {
            if (Main.dedServ)
                return;

            // 阻尼弹簧: 刚度拉回 0°, 阻尼消耗角速度
            swayVel += -swayAngle * 0.012f;
            swayVel *= 0.94f;
            // 微风持续激励 — 静止的树也永远在呼吸
            swayVel += MathF.Sin(globalTime * 0.7f) * 0.00006f + MathF.Sin(globalTime * 1.73f + 1.3f) * 0.00004f;
            swayAngle = MathHelper.Clamp(swayAngle + swayVel, -0.055f, 0.055f);
            // 树冠辉光滞后摆动 (次级运动: 比主干晚半拍、幅度更大)
            canopySway = MathHelper.Lerp(canopySway, swayAngle * 1.7f, 0.06f);
            breathPhase += 0.016f;

            // 受创冲量: 短时间大量掉血 → 树身明显晃动 (掉血叙事)
            if (prevLife > 0 && prevLife - NPC.life > NPC.lifeMax / 400) {
                swayVel += Main.rand.NextBool() ? 0.0022f : -0.0022f;
            }
            prevLife = NPC.life;

            // 摇曳越猛, 抖落的叶子越多 (运动驱动的次级粒子)
            float agitation = MathF.Abs(swayVel) * 600f;
            if (agitation > 0.55f && Main.rand.NextFloat() < MathHelper.Clamp(agitation * 0.3f, 0f, 0.85f)) {
                Vector2 leafPos = NPC.Center + new Vector2(
                    Main.rand.NextFloat(-TextureWidth / 2f, TextureWidth / 2f),
                    Main.rand.NextFloat(-TextureHeight / 2f, -TextureHeight / 6f));
                Dust d = Dust.NewDustPerfect(leafPos, DustID.GrassBlades,
                    new Vector2(swayVel * 900f, Main.rand.NextFloat(1f, 3f)), 100, default, 1.6f);
                d.noGravity = false;
                d.fadeIn = 1.2f;
            }
        }

        /// <summary>给树身注入一次摇曳冲量 (发招后坐/踉跄; 本地视觉)。</summary>
        private void ImpartSway(float impulse) => swayVel += impulse;

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 && gatePassed &&
                Phase == BossPhase.SeasonCombat) {
                TransitionTo(BossPhase.PhaseTransition_2);
                didPhase2Transition = true;
            }
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            vineRotation = 0;
            NPC.netUpdate = true;
        }

        private void UpdateDefenseWindow() {
            // 客户端上升沿检测: 破绽开启的音效/闪光。
            // (修复: 原实现把音效放在服务器权威路径里, 多人端永远听不到)
            if (Main.netMode != NetmodeID.Server && defenseDownTimer > prevDefenseDown + 30) {
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.3f }, NPC.Center);
                seasonFlash = MathF.Max(seasonFlash, 0.6f);
            }
            prevDefenseDown = defenseDownTimer;

            if (defenseDownTimer > 0)
                defenseDownTimer--;
            NPC.defense = Math.Max(0, baseDef - (defenseDownTimer > 0 ? 50 : 0));
        }

        private void OpenVulnerabilityWindow(int ticks) {
            defenseDownTimer = Math.Max(defenseDownTimer, ticks);
            seasonFlash = MathF.Max(seasonFlash, 0.6f);
            NPC.netUpdate = true;
        }

        #endregion

        #region 季节核心

        private float ArenaRadius => IsPhase2 ? DazhengArenaBarrier.Phase2Radius : DazhengArenaBarrier.Phase1Radius;

        private static int SeasonDuration(int s, bool phase2) {
            // V3: 基础时长 +40t 补偿季间连接拍 (无弹幕段落)
            int baseDur = s switch {
                DazhengSeasons.Spring => 580,
                DazhengSeasons.Summer => 460,
                DazhengSeasons.Winter => 580,
                _ => 520,
            };
            return phase2 ? (int)(baseDur * 0.82f) : baseDur;
        }

        /// <summary>季间连接拍内 (无敌意弹幕的段落呼吸)。</summary>
        private bool InSeasonConnector => AttackTimer <= SeasonConnectorTicks;

        /// <summary>连接拍后 60t 内新弹幕速度 35%→100% 渐升 (转换后风速阀, 防瞬间接招)。</summary>
        private float WindupFactor => MathHelper.Clamp(
            0.35f + 0.65f * ((AttackTimer - SeasonConnectorTicks) / 60f), 0.35f, 1f);

        private void AdvanceSeason(int forced) {
            season = forced;
            AttackTimer = 0;
            SubState = 0;
            vineRotation = 0;
            seasonFlash = 1f;
            decoyResolved = false;
            decoyWindowOpen = false;
            healBroken = false;
            HealThreadActive = false;
            NPC.dontTakeDamage = false; // 各季默认可受伤 (秋季诱饵窗口会自行重置)

            // 切季前清掉上一季的持续载体 (诱饵 / 治疗线)
            KillTrackedNPC(ref decoyWhoAmI);
            KillTrackedProjectile(ref healThreadWhoAmI);

            // 段落呼吸: 上一季在场的压力弹幕截短寿命软退场 (换季不糊脸)
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int vineType = ModContent.ProjectileType<DazhengVine>();
                int leafType = ModContent.ProjectileType<DazhengLeaf>();
                foreach (Projectile p in Main.ActiveProjectiles) {
                    if ((p.type == vineType || p.type == leafType) && p.timeLeft > 45)
                        p.timeLeft = 45;
                }
            }

            // 活体根须场仅在 春(P2) / 冬 开启
            UpdateRootField();

            if (Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f, Volume = 0.9f }, NPC.Center);
            ACMUtils.AddScreenShake(7f);
            NPC.netUpdate = true;
        }

        /// <summary>
        /// 季间连接拍 (每季开头 48t): 季语宣告 + 生命光环 + 树身摇曳冲量。
        /// 无任何敌意弹幕 — 段落之间的呼吸口 (fight is a wave)。
        /// </summary>
        private void RunSeasonConnector() {
            if (AttackTimer == 2) {
                ImpartSway(Main.rand.NextBool() ? 0.005f : -0.005f);
                if (Main.netMode != NetmodeID.Server) {
                    string key = season switch {
                        DazhengSeasons.Spring => "SeasonSpring",
                        DazhengSeasons.Summer => "SeasonSummer",
                        DazhengSeasons.Autumn => "SeasonAutumn",
                        _ => "SeasonWinter",
                    };
                    string text = Language.GetTextValue("Mods.AncientChineseMythology.NPCs.Dazheng." + key);
                    CombatText.NewText(NPC.getRect(), DazhengSeasons.Tint(season), text, true);
                }
            }
            if (AttackTimer == 10 && Main.netMode != NetmodeID.Server) {
                // 季节色生命光环从树心晕开
                SpawnLifeRing(650f, 90f, DazhengSeasons.Accent(season), DazhengSeasons.Tint(season), 55);
            }
        }

        private void RunSeasonCombat(Player target) {
            // 锚点复生 (服务器)
            HandleAnchorRegrow();
            // 消费锚点击毁 → 强制切季 + 破绽窗口 (服务器权威)
            ConsumeAnchorBreak();

            switch (season) {
                case DazhengSeasons.Spring: SeasonSpring(target); break;
                case DazhengSeasons.Summer: SeasonSummer(target); break;
                case DazhengSeasons.Autumn: SeasonAutumn(target); break;
                case DazhengSeasons.Winter: SeasonWinter(target); break;
            }
        }

        private void ConsumeAnchorBreak() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (!gatePassed)
                return;
            if (RecentBrokenSeason < 0)
                return;
            if (RecentBrokenFrame <= lastConsumedBreakFrame)
                return;
            if (Main.GameUpdateCount - RecentBrokenFrame > 6)
                return; // 太旧, 不消费

            lastConsumedBreakFrame = RecentBrokenFrame;
            int forced = RecentBrokenSeason;
            OpenVulnerabilityWindow(180); // 击毁锚点奖励 3s 破绽
            if (forced != season)
                AdvanceSeason(forced);
        }

        #endregion

        #region 入场演出 (保留)

        private const int IntroRumbleEnd = 60;
        private const int IntroRiseEnd = 210;
        private const int IntroEruptEnd = 260;
        private const int IntroFinish = 290;

        private void RunIntro(Player target) {
            NPC.dontTakeDamage = true;
            float groundWorldY = spawnPosition.Y + TextureHeight / 2f;

            if (PhaseTimer <= IntroRumbleEnd) {
                if (PhaseTimer == 1) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.8f, Volume = 0.6f },
                        new Vector2(spawnPosition.X, groundWorldY));
                }
                if (PhaseTimer % 4 == 0 && Main.netMode != NetmodeID.Server) {
                    float shakeStr = MathHelper.Lerp(1f, 6f, PhaseTimer / (float)IntroRumbleEnd);
                    ACMUtils.AddScreenShake(shakeStr);
                }
                if (Main.netMode != NetmodeID.Server) {
                    int crackIntensity = (int)MathHelper.Lerp(1, 6, PhaseTimer / (float)IntroRumbleEnd);
                    for (int i = 0; i < crackIntensity; i++) {
                        Vector2 crackPos = new(spawnPosition.X + Main.rand.NextFloat(-300, 300),
                            groundWorldY + Main.rand.NextFloat(-8, 8));
                        Dust d = Dust.NewDustDirect(crackPos, 0, 0, DustID.WoodFurniture,
                            Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-5f, -1f),
                            150, default, Main.rand.NextFloat(1f, 2f));
                        d.noGravity = false;
                    }
                    if (PhaseTimer > 20) {
                        for (int i = 0; i < crackIntensity / 2 + 1; i++) {
                            Vector2 glowPos = new(spawnPosition.X + Main.rand.NextFloat(-200, 200),
                                groundWorldY + Main.rand.NextFloat(-4, 4));
                            Dust d = Dust.NewDustDirect(glowPos, 0, 0, DustID.GreenTorch,
                                0, Main.rand.NextFloat(-3f, -1f), 80, default, 1.8f);
                            d.noGravity = true;
                        }
                    }
                }
                Lighting.AddLight(new Vector2(spawnPosition.X, groundWorldY),
                    new Vector3(0.2f, 0.5f, 0.1f) * (PhaseTimer / (float)IntroRumbleEnd));
            }

            if (PhaseTimer > IntroRumbleEnd && PhaseTimer <= IntroRiseEnd) {
                float riseProgress = (PhaseTimer - IntroRumbleEnd) / (float)(IntroRiseEnd - IntroRumbleEnd);
                float easedProgress = riseProgress < 0.5f
                    ? 4f * riseProgress * riseProgress * riseProgress
                    : 1f - MathF.Pow(-2f * riseProgress + 2f, 3f) / 2f;

                float totalRise = TextureHeight + 100f;
                introRiseOffset = totalRise * (1f - easedProgress);

                if (PhaseTimer == IntroRumbleEnd + 1) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 0.8f },
                        new Vector2(spawnPosition.X, groundWorldY));
                }
                if (PhaseTimer == IntroRumbleEnd + 75) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f, Volume = 0.9f },
                        new Vector2(spawnPosition.X, groundWorldY));
                }

                if (Main.netMode != NetmodeID.Server) {
                    float riseSpeed = MathHelper.Clamp(
                        (riseProgress > 0.1f && riseProgress < 0.9f) ? 1.5f : 0.5f, 0f, 2f);
                    for (int i = 0; i < (int)(4 * riseSpeed); i++) {
                        Vector2 dirtPos = new(spawnPosition.X + Main.rand.NextFloat(-350, 350),
                            groundWorldY + Main.rand.NextFloat(-20, 10));
                        Dust d = Dust.NewDustDirect(dirtPos, 0, 0, DustID.WoodFurniture,
                            Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-6f, -2f),
                            120, default, Main.rand.NextFloat(1.5f, 2.5f));
                        d.noGravity = false;
                    }
                    if (riseProgress > 0.15f) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 vinePos = new(spawnPosition.X + Main.rand.NextFloat(-400, 400),
                                groundWorldY - Main.rand.NextFloat(0, 30));
                            Dust d = Dust.NewDustDirect(vinePos, 0, 0, DustID.JungleGrass,
                                Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-4f, -1f),
                                100, default, 2f);
                            d.noGravity = true;
                        }
                    }
                    if (riseProgress > 0.3f) {
                        int energyCount = (int)(6 * riseProgress);
                        for (int i = 0; i < energyCount; i++) {
                            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                            float dist = Main.rand.NextFloat(100, 400) * (1f - riseProgress * 0.5f);
                            Vector2 ePos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                            Dust d = Dust.NewDustDirect(ePos, 0, 0, DustID.GreenTorch, 0, 0, 80, default, 2f);
                            d.noGravity = true;
                            d.velocity = (NPC.Center - ePos).SafeNormalize(Vector2.Zero) * 4f;
                        }
                    }
                    if (PhaseTimer % 6 == 0)
                        ACMUtils.AddScreenShake(3f + riseSpeed * 4f);
                }

                float lightStr = MathHelper.Lerp(0.3f, 0.8f, riseProgress);
                Lighting.AddLight(NPC.Center, new Vector3(0.3f, 0.6f, 0.15f) * lightStr);
            }

            if (PhaseTimer == IntroRiseEnd) {
                introRiseOffset = 0f;
                isRising = false;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.4f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    ACMUtils.AddScreenShake(16f);
                    for (int i = 0; i < 40; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float speed = Main.rand.NextFloat(4f, 12f);
                        Vector2 debrisPos = new(spawnPosition.X + Main.rand.NextFloat(-200, 200),
                            groundWorldY + Main.rand.NextFloat(-30, 10));
                        Dust d = Dust.NewDustDirect(debrisPos, 0, 0, DustID.WoodFurniture,
                            MathF.Cos(angle) * speed, MathF.Sin(angle) * speed - 3f, 100, default, Main.rand.NextFloat(2f, 3.5f));
                        d.noGravity = false;
                    }
                    for (int i = 0; i < 30; i++) {
                        float angle = MathHelper.TwoPi / 30 * i;
                        float speed = Main.rand.NextFloat(6f, 14f);
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GreenTorch,
                            MathF.Cos(angle) * speed, MathF.Sin(angle) * speed, 60, default, 2.5f);
                        d.noGravity = true;
                    }
                    for (int i = 0; i < 20; i++) {
                        float angle = MathHelper.TwoPi / 20 * i;
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GoldFlame,
                            MathF.Cos(angle) * 8f, MathF.Sin(angle) * 8f, 80, default, 3f);
                        d.noGravity = true;
                    }
                }
                NPC.netUpdate = true;
            }

            if (PhaseTimer > IntroRiseEnd && PhaseTimer <= IntroEruptEnd) {
                float eruptFade = 1f - (PhaseTimer - IntroRiseEnd) / (float)(IntroEruptEnd - IntroRiseEnd);
                if (Main.netMode != NetmodeID.Server) {
                    int waveCount = (int)(8 * eruptFade);
                    for (int i = 0; i < waveCount; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = Main.rand.NextFloat(100, 500);
                        Vector2 ePos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        Dust d = Dust.NewDustDirect(ePos, 0, 0, DustID.GreenTorch, 0, -1f, 80, default, 2f * eruptFade);
                        d.noGravity = true;
                    }
                    if (PhaseTimer % 3 == 0) {
                        for (int i = 0; i < 4; i++) {
                            Vector2 leafPos = NPC.Center + new Vector2(
                                Main.rand.NextFloat(-TextureWidth / 2, TextureWidth / 2),
                                Main.rand.NextFloat(-TextureHeight / 2, 0));
                            Dust d = Dust.NewDustDirect(leafPos, 0, 0, DustID.GrassBlades,
                                Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(1f, 4f), 100, default, 2f);
                            d.noGravity = false;
                        }
                    }
                }
                Lighting.AddLight(NPC.Center, new Vector3(0.5f, 0.8f, 0.3f) * eruptFade);
            }

            if (PhaseTimer >= IntroEruptEnd && PhaseTimer < IntroFinish) {
                if (Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(200, 200);
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GreenTorch, 0, -1f, 100, default, 1.5f);
                        d.noGravity = true;
                    }
                }
            }

            if (PhaseTimer >= IntroFinish) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.1f }, NPC.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<DazhengArenaBarrier>(),
                        0, 0f, Main.myPlayer, NPC.whoAmI, DazhengArenaBarrier.Phase1Radius);
                }
                season = DazhengSeasons.Spring;
                TransitionTo(BossPhase.Gate);
            }
        }

        #endregion

        #region G5 门控

        private void RunGate(Player target) {
            NPC.dontTakeDamage = true;

            // 生成四角季节锚点 (一次)
            if (!anchorsSpawned) {
                anchorsSpawned = true;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int s = 0; s < GateAnchorCount; s++)
                        SpawnAnchor(s);
                }
            }

            // 门控提示: 季节锚点尘埃指引 + 轻压力藤蔓 (telegraphed)
            if (AttackTimer % 50 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 7f;
                vel = vel.RotatedByRandom(MathHelper.ToRadians(20f));
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<DazhengVine>(), NPC.damage / 6, 0f, Main.myPlayer);
            }

            // 暴露根脉的金绿核心脉冲 (可读: 现在打不动, 去毁锚点)
            if (Main.netMode != NetmodeID.Server && AttackTimer % 8 == 0) {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Dust d = Dust.NewDustPerfect(NPC.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 120f,
                    DustID.GreenFairy, Vector2.Zero, 120, default, 1.3f);
                d.noGravity = true;
            }

            // 通过条件: 剩余锚点 ≤ 1 (即已毁 3)
            if (Main.netMode != NetmodeID.MultiplayerClient && CountAnchors() <= GatePassRemain) {
                gatePassed = true;
                NPC.dontTakeDamage = false;
                lastConsumedBreakFrame = RecentBrokenFrame; // 避免门控末次击毁触发立即切季
                seasonFlash = 1f;
                ACMUtils.AddScreenShake(12f);
                if (Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);
                AdvanceSeason(DazhengSeasons.Spring);
                TransitionTo(BossPhase.SeasonCombat);
            }
        }

        private Vector2 GetAnchorPos(int s) {
            return s switch {
                DazhengSeasons.Spring => NPC.Center + new Vector2(-AnchorOffsetX, -AnchorOffsetY),
                DazhengSeasons.Summer => NPC.Center + new Vector2(AnchorOffsetX, -AnchorOffsetY),
                DazhengSeasons.Autumn => NPC.Center + new Vector2(AnchorOffsetX, AnchorOffsetY),
                _ => NPC.Center + new Vector2(-AnchorOffsetX, AnchorOffsetY),
            };
        }

        private void SpawnAnchor(int s) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            Vector2 pos = GetAnchorPos(s);
            int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)pos.X, (int)pos.Y,
                ModContent.NPCType<DazhengSeasonAnchor>(), 0, NPC.whoAmI, s);
            if (idx >= 0 && idx < Main.maxNPCs && Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, number: idx);
        }

        private int CountAnchors() {
            int c = 0;
            int type = ModContent.NPCType<DazhengSeasonAnchor>();
            foreach (NPC n in Main.ActiveNPCs)
                if (n.type == type && (int)n.ai[0] == NPC.whoAmI) c++;
            return c;
        }

        private bool AnchorExists(int s) {
            int type = ModContent.NPCType<DazhengSeasonAnchor>();
            foreach (NPC n in Main.ActiveNPCs)
                if (n.type == type && (int)n.ai[0] == NPC.whoAmI && (int)n.ai[1] == s) return true;
            return false;
        }

        private void HandleAnchorRegrow() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            anchorRegrowTimer++;
            if (anchorRegrowTimer < 780) // ~13s
                return;
            anchorRegrowTimer = 0;
            // 补回缺失的季节锚点 (保持四季控制可用)
            for (int s = 0; s < GateAnchorCount; s++) {
                if (!AnchorExists(s)) {
                    SpawnAnchor(s);
                    break;
                }
            }
        }

        #endregion

        #region 春 — 藤蔓迷宫 + 根刺阵 + 活体根须

        private void SeasonSpring(Player target) {
            int dur = SeasonDuration(DazhengSeasons.Spring, IsPhase2);

            RunSeasonConnector();
            if (InSeasonConnector) {
                if (AttackTimer == 1 && Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = -0.2f }, NPC.Center);
                return;
            }

            // 持久藤蔓迷宫墙: 每 ~75 tick 一道, timeLeft ~8s, 留明显缺口
            if (AttackTimer % 75 == 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                int wallType = (int)(AttackTimer / 75) % 4;
                bool horizontal = wallType < 2;
                int dir = (wallType % 2 == 0) ? -1 : 1;
                SpawnVineWall(target.Center, dir, horizontal, persistent: true);
                if (Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f }, target.Center);
            }

            // 根刺阵 (季内高潮拍): 每 ~150t 一轮点名破土, 位置在预警起始帧锁定
            if (AttackTimer % 150 == 100 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = IsPhase2 ? 5 : 3;
                float[] offsets = count == 5
                    ? new float[] { 0f, -150f, 150f, -300f, 300f }
                    : new float[] { 0f, -180f, 180f };
                int spawned = 0;
                int spearType = ModContent.ProjectileType<DazhengRootSpear>();
                foreach (Projectile p in Main.ActiveProjectiles)
                    if (p.type == spearType) spawned++;
                for (int i = 0; i < count && spawned < 6; i++) {
                    Vector2 basePos = FindGroundBelow(target.Center + new Vector2(offsets[i], 0));
                    if (basePos == Vector2.Zero)
                        continue;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), basePos, Vector2.Zero,
                        spearType, NPC.damage / 4, 0f, Main.myPlayer, 380f + Main.rand.NextFloat(80f));
                    spawned++;
                }
            }
            // 破土瞬间 (预警 42t 后) 的树身后坐 — 招式与身体反应对齐
            if (AttackTimer % 150 == 142)
                ImpartSway(Main.rand.NextBool() ? 0.004f : -0.004f);

            // 缝隙的安全提示尘 (玉青)
            if (Main.netMode != NetmodeID.Server && AttackTimer % 6 == 0) {
                Dust d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(30, 30),
                    DustID.GrassBlades, Vector2.Zero, 150, TelegraphColors.Safe, 1f);
                d.noGravity = true;
            }

            // 迷宫"通关"窗口: 存活到本季末 → 破绽 (defense −50)
            if (AttackTimer == dur - 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                if (target.active && !target.dead &&
                    Vector2.Distance(target.Center, NPC.Center) < ArenaRadius)
                    OpenVulnerabilityWindow(200);
            }

            if (AttackTimer > dur)
                AdvanceSeason(DazhengSeasons.Summer);
        }

        /// <summary>从给定位置向下找地表点 (根刺基点)。找不到返回 Zero。</summary>
        private static Vector2 FindGroundBelow(Vector2 from) {
            int tileX = (int)(from.X / 16f);
            int startY = (int)(from.Y / 16f);
            for (int y = startY; y < startY + 80 && y < Main.maxTilesY - 1; y++) {
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType])
                    return new Vector2(from.X, y * 16f);
            }
            return Vector2.Zero;
        }

        #endregion

        #region 夏 — 落叶雨 + 金叶风暴

        /// <summary>金叶风暴收束开始时刻 (季中高潮拍)。</summary>
        private int NovaStart(int dur) => dur / 2 - 68;

        private void SeasonSummer(Player target) {
            int dur = SeasonDuration(DazhengSeasons.Summer, IsPhase2);
            int novaStart = NovaStart(dur);
            // nova 窗口: 收束 60t + 静默 8t + 三环释放 (至 +104), 期间暂停常规压力
            bool novaWindow = AttackTimer >= novaStart && AttackTimer <= novaStart + 116;

            RunSeasonConnector();
            if (InSeasonConnector)
                return;

            if (!novaWindow) {
                int wave = (int)(AttackTimer / 45);
                int leafPerTick = 2 + wave;
                int cap = IsPhase2 ? 7 : 6;
                if (leafPerTick > cap) leafPerTick = cap;

                if (AttackTimer % (IsPhase2 ? 13 : 15) == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < leafPerTick; i++) {
                        Vector2 spawnPos = target.Center + new Vector2(
                            Main.rand.NextFloat(-600, 600), -500 - Main.rand.NextFloat(0, 200));
                        Vector2 vel = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(5f, 12f)) * WindupFactor;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                            ModContent.ProjectileType<DazhengLeaf>(), NPC.damage / 5, 0f, Main.myPlayer);
                    }
                }

                // 侧向藤蔓夹击 (telegraphed, 低频)
                if (AttackTimer % 80 == 40 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float side = Main.rand.NextBool() ? -1f : 1f;
                    for (int i = 0; i < 4; i++) {
                        Vector2 spawnPos = target.Center + new Vector2(side * 600, -200 + i * 120);
                        Vector2 vel = new Vector2(-side * 10f, 0) * WindupFactor;
                        int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                            ModContent.ProjectileType<DazhengVine>(), NPC.damage / 4, 0f, Main.myPlayer);
                        if (proj >= 0 && proj < Main.maxProjectiles)
                            Main.projectile[proj].timeLeft = 200;
                    }
                }

                if (AttackTimer % 45 == 0 && Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = 0.3f }, target.Center);
            }
            else {
                RunSummerNova(novaStart);
            }

            if (AttackTimer > dur)
                AdvanceSeason(DazhengSeasons.Autumn);
        }

        /// <summary>
        /// 金叶风暴 (夏季高潮): 60t 树冠叶尘向心收束 → 8t 静默 (爆前吸气) → 三环金叶 nova。
        /// 公平阀门: 三环共用一个 ±24° 安全扇区 (SubState 存扇区角, 同步), Safe 色光线标出。
        /// </summary>
        private void RunSummerNova(int novaStart) {
            float t = AttackTimer - novaStart;

            // 起手 (服务器): 掷出安全扇区角并同步
            if (t == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                SubState = Main.rand.NextFloat(MathHelper.TwoPi);
                NPC.netUpdate = true;
            }
            if (t == 1 && Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.6f, Volume = 0.8f }, NPC.Center);

            // 收束 0~60t: 树冠金叶尘向心 (密度 ∝ sqrt(进度), 72% 后骤停 → 静默前的收敛)
            if (t < 60 && Main.netMode != NetmodeID.Server) {
                float charge = t / 60f;
                if (charge < 0.87f && Main.rand.NextFloat() < 0.3f + MathF.Sqrt(charge) * 0.6f) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    float distIn = Main.rand.NextFloat(220f, 520f);
                    Vector2 p = NPC.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * distIn;
                    Dust d = Dust.NewDustPerfect(p, DustID.GoldFlame, Vector2.Zero, 90, default, 1.7f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - p).SafeNormalize(Vector2.Zero) * (4f + charge * 5f);
                }
                if ((int)t % 10 == 0)
                    ACMUtils.AddScreenShake(0.5f + charge * 2f);
            }
            // 60~68t: 静默拍 — 无粒子无声音 (爆发前的吸气)

            // 三环释放: 68 / 80 / 92
            if ((t == 68 || t == 80 || t == 92) && Main.netMode != NetmodeID.MultiplayerClient) {
                int ringIdx = t == 68 ? 0 : (t == 80 ? 1 : 2);
                float speed = 7.5f + ringIdx * 2f;
                float safeAngle = SubState;
                const float SafeHalf = MathHelper.Pi * 24f / 180f;
                const int Count = 30;
                for (int i = 0; i < Count; i++) {
                    float a = MathHelper.TwoPi / Count * i + ringIdx * 0.07f;
                    // 安全扇区放行 (三环共用, 玩家有稳定生路)
                    float delta = MathHelper.WrapAngle(a - safeAngle);
                    if (MathF.Abs(delta) < SafeHalf)
                        continue;
                    Vector2 vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * speed;
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DazhengLeaf>(), NPC.damage / 5, 0f, Main.myPlayer, 1f);
                    if (proj >= 0 && proj < Main.maxProjectiles)
                        Main.projectile[proj].timeLeft = 240;
                }
            }
            // 释放帧的冲击反馈 (各端本地)
            if (t == 68 || t == 80 || t == 92) {
                ImpartSway((t == 80 ? -1f : 1f) * 0.0045f);
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.2f + (t - 68f) / 120f, Volume = 1.1f }, NPC.Center);
                    ACMUtils.AddScreenShake(t == 68 ? 8f : 5f);
                    if (t == 68)
                        SpawnLifeRing(720f, 100f, new Color(255, 235, 150), new Color(230, 170, 60), 45);
                }
            }
        }

        #endregion

        #region 秋 — 黄金幻影·诱饵树 DPS 谜题

        private void SeasonAutumn(Player target) {
            RunSeasonConnector();
            if (InSeasonConnector)
                return;

            int windowStart = SeasonConnectorTicks + 1;

            // 起手: 真身无敌, 暴露金核, 镜像位生成诱饵树
            if (AttackTimer == windowStart) {
                DecoyKilled = false;
                decoyResolved = false;
                decoyWindowOpen = true;
                NPC.dontTakeDamage = true;
                ImpartSway(0.005f);
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
                    ACMUtils.AddScreenShake(8f);
                }
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    float mirrorX = 2f * target.Center.X - NPC.Center.X;
                    mirrorX = MathHelper.Clamp(mirrorX, NPC.Center.X - ArenaRadius * 0.6f, NPC.Center.X + ArenaRadius * 0.6f);
                    Vector2 pos = new(mirrorX, NPC.Center.Y);
                    decoyWhoAmI = NPC.NewNPC(NPC.GetSource_FromAI(), (int)pos.X, (int)pos.Y,
                        ModContent.NPCType<DazhengDecoyTree>(), 0, NPC.whoAmI);
                    if (decoyWhoAmI >= 0 && decoyWhoAmI < Main.maxNPCs && Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, number: decoyWhoAmI);
                }
            }

            // 窗口期: 真身金核脉冲 + 轻幻象压力
            if (decoyWindowOpen) {
                // 真身微特征①: 每 90t 一次低沉「心跳」定位音 + 摇曳微搏动 (幻影没有心跳)
                if ((AttackTimer - windowStart) % 90 == 0) {
                    ImpartSway(0.0018f);
                    if (Main.netMode != NetmodeID.Server)
                        SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.92f, Volume = 0.85f }, NPC.Center);
                }
                // 真身微特征②: 金核脉冲尘 (与心跳同拍收缩)
                if (Main.netMode != NetmodeID.Server && AttackTimer % 5 == 0) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    Dust d = Dust.NewDustPerfect(NPC.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 90f,
                        DustID.GoldFlame, Vector2.Zero, 100, default, 1.6f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 2f;
                }
                if (AttackTimer % 30 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 vel = ((target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 8f) * WindupFactor;
                    vel = vel.RotatedByRandom(MathHelper.ToRadians(18f));
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DazhengVine>(), NPC.damage / 5, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles)
                        Main.projectile[proj].ai[2] = 1f; // 金藤
                }
                // 幻影压力: 每 ~110t 自树冠放出 2 只金身幻影 (公平阀门版: 转向封顶+熄火)
                if ((AttackTimer - windowStart) % 110 == 70 && Main.netMode != NetmodeID.MultiplayerClient) {
                    int phantomType = ModContent.ProjectileType<DazhengGoldenPhantom>();
                    int alive = 0;
                    foreach (Projectile p in Main.ActiveProjectiles)
                        if (p.type == phantomType) alive++;
                    for (int i = 0; i < 2 && alive < 4; i++) {
                        Vector2 vel = new(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(2f, 4f));
                        Projectile.NewProjectile(NPC.GetSource_FromAI(),
                            NPC.Center + new Vector2(Main.rand.NextFloat(-260f, 260f), -TextureHeight * 0.3f), vel,
                            phantomType, NPC.damage / 5, 0f, Main.myPlayer);
                        alive++;
                    }
                }

                // 谜题解决: 诱饵被打掉 (静态事件) → 大破绽; 或诱饵消失 (超时)。
                // 用同步 NPC 列表扫描 + 45t 生成宽限, 保证多人客户端读到同一事实 (decoyWhoAmI 仅服务器可靠)
                bool decoyGone = AttackTimer > windowStart + 45 && !AnyDecoyAlive();
                bool killedRecently = DecoyKilled && Main.GameUpdateCount - DecoyEventFrame < 30;

                if (decoyGone || killedRecently) {
                    decoyWindowOpen = false;
                    decoyResolved = true;
                    NPC.dontTakeDamage = false;
                    if (killedRecently) {
                        OpenVulnerabilityWindow(300); // 解谜成功: 5s 大破绽
                        ImpartSway(-0.006f); // 镀金被戳穿的踉跄
                        if (Main.netMode != NetmodeID.Server) {
                            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.4f, Volume = 1.4f }, NPC.Center);
                            ACMUtils.AddScreenShake(10f);
                            SpawnLifeRing(560f, 80f, TelegraphColors.Holy, new Color(230, 170, 60), 45);
                        }
                    }
                    SubState = AttackTimer; // 记录解决时刻
                }

                // 安全阀: 极端情况下窗口最长 DazhengDecoyTree.Lifetime + 60
                if (AttackTimer > windowStart + DazhengDecoyTree.Lifetime + 60) {
                    decoyWindowOpen = false;
                    decoyResolved = true;
                    NPC.dontTakeDamage = false;
                    SubState = AttackTimer; // 修复: 超时也要记录时刻, 保证余波拍不被跳过
                }
                return;
            }

            // 解决后短余波 → 切冬
            if (decoyResolved && AttackTimer > SubState + 120)
                AdvanceSeason(DazhengSeasons.Winter);
        }

        /// <summary>是否仍有属于本体的诱饵树存活 (扫同步 NPC 列表, 多端一致)。</summary>
        private bool AnyDecoyAlive() {
            int type = ModContent.NPCType<DazhengDecoyTree>();
            foreach (NPC n in Main.ActiveNPCs)
                if (n.type == type && (int)n.ai[0] == NPC.whoAmI)
                    return true;
            return false;
        }

        #endregion

        #region 冬 — 减速 + 冰藤 + 生命汲取治疗线

        private void SeasonWinter(Player target) {
            int dur = SeasonDuration(DazhengSeasons.Winter, IsPhase2);
            int formEnd = SeasonConnectorTicks + 50;  // 蓄能(安全)阶段
            int healEnd = dur - 105;                  // 汲取阶段结束 (其后是饱食审判窗口)

            RunSeasonConnector();
            if (InSeasonConnector)
                return;

            // 冬季减速 (本地玩家, 场内): 冰冻凛冬规则
            if (Main.netMode != NetmodeID.Server) {
                Player lp = Main.LocalPlayer;
                if (lp.active && !lp.dead && Vector2.Distance(lp.Center, NPC.Center) < ArenaRadius)
                    lp.AddBuff(BuffID.Chilled, 6);
            }

            // 起手: 生成治疗线 (蓄能, 安全色)
            if (AttackTimer == SeasonConnectorTicks + 1) {
                healBroken = false;
                HealThreadActive = false;
                if (Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.4f, Volume = 0.9f }, NPC.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    healThreadWhoAmI = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<DazhengHealThread>(), 0, 0f, Main.myPlayer, NPC.whoAmI, NPC.target);
                }
            }

            // 续命治疗线投射物
            KeepThreadAlive();

            bool draining = AttackTimer >= formEnd && AttackTimer < healEnd && !healBroken;
            HealThreadActive = draining;

            if (draining) {
                // 汲取: 大椿回血 (服务器权威, 温和)
                if (Main.netMode != NetmodeID.MultiplayerClient && AttackTimer % 4 == 0) {
                    int heal = (int)(NPC.lifeMax * 0.0006f);
                    if (NPC.life < NPC.lifeMax) {
                        NPC.life = Math.Min(NPC.lifeMax, NPC.life + heal);
                        NPC.HealEffect(heal);
                        NPC.netUpdate = true;
                    }
                }
                // 挣断判定 (服务器权威): 目标冲刺/跳跃 (速度尖峰) → 打断 + 破绽
                if (Main.netMode != NetmodeID.MultiplayerClient && NPC.target >= 0 && NPC.target < Main.maxPlayers) {
                    Player t = Main.player[NPC.target];
                    if (t.active && !t.dead) {
                        bool dashed = t.velocity.Length() > 11f;
                        bool leapt = t.velocity.Y < -7.5f;
                        if (dashed || leapt) {
                            healBroken = true;
                            HealThreadActive = false;
                            // 斩线演出: 丝线转入 20t 回抽鞭甩, 而非凭空消失
                            SnapTrackedThread(ref healThreadWhoAmI);
                            OpenVulnerabilityWindow(200);
                            ImpartSway(Main.rand.NextBool() ? 0.007f : -0.007f); // 被扯断的踉跄
                            if (Main.netMode != NetmodeID.Server) {
                                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.4f }, NPC.Center);
                                ACMUtils.AddScreenShake(8f);
                            }
                        }
                    }
                }
            }

            // 饱食爆发 (惩罚拍): 丝线存活到汲取结束 → 45t 冰尘收束预警 → 12 根冰藤辐射爆。
            // 反制选择权在玩家: 任意时刻挣断丝线即可完全免除本招。
            if (!healBroken && AttackTimer >= healEnd - 45 && AttackTimer < healEnd &&
                Main.netMode != NetmodeID.Server) {
                float charge = 1f - (healEnd - AttackTimer) / 45f;
                if (Main.rand.NextFloat() < 0.35f + charge * 0.5f) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 p = NPC.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * Main.rand.NextFloat(160f, 420f);
                    Dust d = Dust.NewDustPerfect(p, DustID.IceTorch, Vector2.Zero, 100, default, 1.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - p).SafeNormalize(Vector2.Zero) * (3f + charge * 5f);
                }
                if ((int)AttackTimer % 10 == 0)
                    ACMUtils.AddScreenShake(0.5f + charge * 2f);
            }
            if (AttackTimer == healEnd && !healBroken) {
                ImpartSway(Main.rand.NextBool() ? 0.006f : -0.006f);
                SnapTrackedThread(ref healThreadWhoAmI); // 饱食完成, 丝线鞭甩收回
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 12; i++) {
                        float angle = MathHelper.TwoPi / 12 * i + 0.13f;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f;
                        int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<DazhengVine>(), NPC.damage / 4, 0f, Main.myPlayer);
                        if (proj >= 0 && proj < Main.maxProjectiles)
                            Main.projectile[proj].timeLeft = 220;
                    }
                }
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                    ACMUtils.AddScreenShake(9f);
                    SpawnLifeRing(680f, 95f, TelegraphColors.IceWhite, TelegraphColors.Frost, 45);
                }
            }

            // 冰藤: 低频、慢速的冰蓝藤蔓 (telegraphed; 饱食爆发预警期间暂停, 读法留白)
            bool feastTelegraph = !healBroken && AttackTimer >= healEnd - 45 && AttackTimer <= healEnd;
            if (!feastTelegraph && AttackTimer % (IsPhase2 ? 26 : 34) == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int arms = IsPhase2 ? 3 : 2;
                vineRotation += 0.5f;
                for (int a = 0; a < arms; a++) {
                    float angle = vineRotation + MathHelper.TwoPi / arms * a;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 6f * WindupFactor;
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DazhengVine>(), NPC.damage / 5, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles)
                        Main.projectile[proj].timeLeft = 220;
                }
            }

            if (AttackTimer > dur) {
                HealThreadActive = false;
                KillTrackedProjectile(ref healThreadWhoAmI);
                AdvanceSeason(DazhengSeasons.Spring);
            }
        }

        #endregion

        #region 阶段转换演出「四季失序」(V3 升级: 清弹 → 收束 → 静默坍缩 → 金爆)

        /// <summary>转换爆发帧 (供屏障 uFlash / 绘制层读取)。</summary>
        public const int Transition2BurstTick = 82;
        private const int Transition2SilenceTick = 70;
        private const int Transition2End = 160;

        /// <summary>换阶段静默坍缩拍内的树身收缩量 (0~1, 绘制用)。</summary>
        public float TransitionCollapse {
            get {
                if (Phase != BossPhase.PhaseTransition_2)
                    return 0f;
                if (PhaseTimer <= Transition2SilenceTick || PhaseTimer > Transition2BurstTick)
                    return 0f;
                return (PhaseTimer - Transition2SilenceTick) / (float)(Transition2BurstTick - Transition2SilenceTick);
            }
        }

        private void RunPhaseTransition2(Player target) {
            NPC.dontTakeDamage = true;

            // 起手: 公平阀门 — 清空全部敌意弹幕 + 清除持续载体; 宣告「四季失序」
            if (PhaseTimer == 1) {
                HealThreadActive = false;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < Main.maxProjectiles; i++) {
                        Projectile p = Main.projectile[i];
                        if (p.active && p.hostile && p.damage > 0)
                            p.Kill();
                    }
                    KillTrackedNPC(ref decoyWhoAmI);
                    KillTrackedProjectile(ref healThreadWhoAmI);
                }
                if (Main.netMode != NetmodeID.Server) {
                    string text = Language.GetTextValue("Mods.AncientChineseMythology.NPCs.Dazheng.SeasonsDisorder");
                    CombatText.NewText(NPC.getRect(), new Color(255, 210, 90), text, true);
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.7f, Volume = 1f }, NPC.Center);
                }
            }

            // 0~70t 收束: 金绿尘向心 + 树身内压摇曳 + 低频 rumble 渐强
            if (PhaseTimer <= Transition2SilenceTick) {
                float charge = PhaseTimer / (float)Transition2SilenceTick;
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 12; i++) {
                        float angle = MathHelper.TwoPi / 12 * i + globalTime * 3f;
                        float dist = MathHelper.Lerp(430f, 90f, charge);
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 80, default, 3f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * (6f + charge * 5f);
                    }
                    for (int i = 0; i < 6; i++) {
                        Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(300, 300);
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.JungleGrass, 0, 0, 100, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                    }
                    // rumble: 渐强的低频震 (charge² 曲线)
                    if (PhaseTimer % 6 == 0)
                        ACMUtils.AddScreenShake(1f + charge * charge * 4f);
                }
                // 内压: 交替小冲量, 树身不安地左右紧绷
                if (PhaseTimer % 20 == 0)
                    ImpartSway(((int)(PhaseTimer / 20) % 2 == 0 ? 1f : -1f) * 0.003f);
            }
            // 70~82t: 静默坍缩拍 — 粒子/声音硬切, 树身收缩 (爆发前的吸气, 由 TransitionCollapse 驱动绘制)

            if (PhaseTimer == Transition2BurstTick) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 1.5f }, NPC.Center);
                ACMUtils.AddScreenShake(12f);
                seasonFlash = 1f;
                ImpartSway(Main.rand.NextBool() ? 0.009f : -0.009f);
                if (Main.netMode != NetmodeID.Server)
                    SpawnLifeRing(1050f, 130f, new Color(255, 235, 150), new Color(120, 210, 90), 70);

                // 二阶段收缩限制圈
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    foreach (Projectile proj in Main.ActiveProjectiles) {
                        if (proj.type == ModContent.ProjectileType<DazhengArenaBarrier>() &&
                            (int)proj.ai[0] == NPC.whoAmI) {
                            proj.ai[1] = DazhengArenaBarrier.Phase2Radius;
                            proj.netUpdate = true;
                            break;
                        }
                    }
                    // 转阶段爆发 (紧接静默 → 对比爆发)
                    for (int i = 0; i < 14; i++) {
                        float angle = MathHelper.TwoPi / 14 * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 10f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<DazhengVine>(), NPC.damage / 3, 0f, Main.myPlayer);
                    }
                    for (int i = 0; i < 8; i++) {
                        float angle = MathHelper.TwoPi / 8 * i + MathHelper.ToRadians(22f);
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 7f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<DazhengGoldenPhantom>(), NPC.damage / 3, 0f, Main.myPlayer);
                    }
                }
            }

            // 82~160t: 收势余韵 (轻落叶 + 光点上浮)
            if (PhaseTimer > Transition2BurstTick && Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0) {
                Vector2 p = NPC.Center + Main.rand.NextVector2Circular(350, 300);
                Dust d = Dust.NewDustPerfect(p, DustID.GoldFlame, new Vector2(0, -Main.rand.NextFloat(0.5f, 1.5f)),
                    120, default, 1.4f);
                d.noGravity = true;
            }

            if (PhaseTimer >= Transition2End) {
                NPC.dontTakeDamage = false;
                baseDef = 120;
                NPC.damage = (int)(NPC.damage * 1.25f);
                // 续接四季循环, 重启当前季节 (P2 更密)
                AdvanceSeason(season);
                TransitionTo(BossPhase.SeasonCombat);
            }
        }

        #endregion

        #region 死亡演出「归土」

        /// <summary>结界碎裂时刻 (死亡时间轴内; 供 DazhengArenaBarrier 读取)。</summary>
        public const int DeathBarrierShatterTick = DeathQuietEnd;
        /// <summary>死亡终爆时刻 (供天幕金脉冲联动)。</summary>
        public const int DeathBurstTick = DeathStillEnd;

        /// <summary>归土下沉的绘制偏移 (纯视觉; NPC.Center 不动, 保证掉落物在地表)。</summary>
        public float DeathSinkOffset {
            get {
                if (!IsDying || PhaseTimer <= DeathSinkStart)
                    return 0f;
                float p = MathHelper.Clamp((PhaseTimer - DeathSinkStart) / (float)(DeathSinkEnd - DeathSinkStart), 0f, 1f);
                return (TextureHeight + 140f) * (p * p * p); // 三次方缓入: 先缓慢松动, 后加速沉没
            }
        }

        private void RunDeathCinematic() {
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
            HealThreadActive = false;
            float groundY = spawnPosition.Y + TextureHeight / 2f;

            // ---- 0~70t「寂」: 叶暴落 + 高频颤抖 + 世界褪色 ----
            if (PhaseTimer == 1 && Main.netMode != NetmodeID.Server) {
                string text = Language.GetTextValue("Mods.AncientChineseMythology.NPCs.Dazheng.ForestRequiem");
                CombatText.NewText(NPC.getRect(), new Color(190, 230, 180), text, true);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -1f, Volume = 1.2f }, NPC.Center);
            }
            if (PhaseTimer <= DeathQuietEnd) {
                float p = PhaseTimer / (float)DeathQuietEnd;
                // 高频细颤 (垂死的痉挛, 与平时的慢摇曳形成对比)
                swayVel += MathF.Sin(PhaseTimer * 1.9f) * 0.0009f * p;
                if (Main.netMode != NetmodeID.Server) {
                    // 叶暴落: 密度远超平时
                    for (int i = 0; i < 3; i++) {
                        Vector2 leafPos = NPC.Center + new Vector2(
                            Main.rand.NextFloat(-TextureWidth / 2f, TextureWidth / 2f),
                            Main.rand.NextFloat(-TextureHeight / 2f, 0f));
                        Dust d = Dust.NewDustPerfect(leafPos, DustID.GrassBlades,
                            new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(2f, 5f)), 100, default, 1.8f);
                        d.noGravity = false;
                        d.fadeIn = 1.2f;
                    }
                    if (PhaseTimer % 8 == 0)
                        ACMUtils.AddScreenShake(2f + p * 2f);
                }
            }

            // ---- 70t: 结界碎裂 (由屏障读取时刻自演; 此处只配声画节拍) ----
            if (PhaseTimer == DeathBarrierShatterTick && Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Shatter with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.7f, Volume = 0.8f }, NPC.Center);
                ACMUtils.AddScreenShake(10f);
            }

            // ---- 90~230t「归土」: 巨躯沉回大地 ----
            if (PhaseTimer > DeathSinkStart && PhaseTimer <= DeathSinkEnd) {
                float p = (PhaseTimer - DeathSinkStart) / (float)(DeathSinkEnd - DeathSinkStart);
                if (Main.netMode != NetmodeID.Server) {
                    // 根线泥土翻涌 (下沉越快翻涌越猛)
                    int churn = 1 + (int)(p * p * 5f);
                    for (int i = 0; i < churn; i++) {
                        Vector2 dirtPos = new(spawnPosition.X + Main.rand.NextFloat(-380f, 380f),
                            groundY + Main.rand.NextFloat(-14f, 8f));
                        Dust d = Dust.NewDustPerfect(dirtPos, DustID.WoodFurniture,
                            new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-5f, -1.5f)),
                            120, default, Main.rand.NextFloat(1.4f, 2.6f));
                        d.noGravity = false;
                    }
                    // 木质呻吟: 每 ~30t 一声, 随机音高 (垂死巨物的骨骼声)
                    if (PhaseTimer % 30 == 5)
                        SoundEngine.PlaySound(SoundID.Dig with {
                            Pitch = -0.9f + Main.rand.NextFloat(0.25f), Volume = 0.9f
                        }, NPC.Center);
                    if (PhaseTimer % 7 == 0)
                        ACMUtils.AddScreenShake(3f + p * 4f);
                    // 土中升起金色生命光点 (渐密 — 死亡即将转为馈赠的伏笔)
                    if (Main.rand.NextFloat() < 0.2f + p * 0.5f) {
                        Vector2 motePos = new(spawnPosition.X + Main.rand.NextFloat(-450f, 450f),
                            groundY + Main.rand.NextFloat(-10f, 20f));
                        Dust d = Dust.NewDustPerfect(motePos, DustID.GoldFlame,
                            new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(1f, 2.6f)),
                            110, default, 1.3f);
                        d.noGravity = true;
                    }
                }
            }

            // ---- 230~268t「静」: 终爆前留白 (只剩零星光点) ----
            if (PhaseTimer > DeathSinkEnd && PhaseTimer <= DeathStillEnd &&
                Main.netMode != NetmodeID.Server && Main.rand.NextBool(5)) {
                Vector2 motePos = new(spawnPosition.X + Main.rand.NextFloat(-300f, 300f),
                    groundY + Main.rand.NextFloat(-6f, 12f));
                Dust d = Dust.NewDustPerfect(motePos, DustID.GoldFlame,
                    new Vector2(0, -Main.rand.NextFloat(0.8f, 1.8f)), 130, default, 1.1f);
                d.noGravity = true;
            }

            // ---- 268t「生」: 生命归还山林 — 全场唯一的最大一拍 ----
            if (PhaseTimer == DeathStillEnd) {
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.5f, Volume = 1.3f }, NPC.Center);
                    ACMUtils.AddScreenShake(17f);
                    SpawnLifeRing(1400f, 170f, new Color(255, 245, 190), new Color(120, 220, 90), 90);
                    SpawnLifeRing(900f, 110f, new Color(200, 255, 170), new Color(80, 190, 80), 70);
                    // 沿地表荡开的新芽尘柱
                    for (int i = 0; i < 26; i++) {
                        float x = spawnPosition.X + Main.rand.NextFloat(-600f, 600f);
                        Dust d = Dust.NewDustPerfect(new Vector2(x, groundY + Main.rand.NextFloat(-4f, 8f)),
                            DustID.GrassBlades, new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(4f, 10f)),
                            80, default, Main.rand.NextFloat(1.6f, 2.6f));
                        d.noGravity = false;
                        d.fadeIn = 1.4f;
                    }
                    for (int i = 0; i < 40; i++) {
                        float a = MathHelper.TwoPi / 40 * i;
                        Dust d = Dust.NewDustPerfect(new Vector2(spawnPosition.X, groundY), DustID.GoldFlame,
                            new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.5f) * Main.rand.NextFloat(6f, 13f),
                            70, default, 2.2f);
                        d.noGravity = true;
                    }
                }
            }

            // 终爆后余辉: 光点缓慢上浮
            if (PhaseTimer > DeathStillEnd && Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                Vector2 motePos = new(spawnPosition.X + Main.rand.NextFloat(-500f, 500f),
                    groundY - Main.rand.NextFloat(0f, 60f));
                Dust d = Dust.NewDustPerfect(motePos, DustID.GreenFairy,
                    new Vector2(0, -Main.rand.NextFloat(0.6f, 1.6f)), 120, default, 1.2f);
                d.noGravity = true;
            }

            // ---- 300t: 真实死亡 (掉落 / downed 旗标) ----
            if (PhaseTimer >= DeathFinish && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.life = 0;
                NPC.checkDead(); // CheckDead 此时放行 → 正常掉落流程
            }
        }

        #endregion

        #region 持续载体管理 (诱饵 / 治疗线 / 根须场)

        private void UpdateRootField() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            // 活体根须场目标强度: 春(P2) 0.9, 冬 0.65, 其余 0
            float intensity = season switch {
                DazhengSeasons.Spring => IsPhase2 ? 0.9f : 0f,
                DazhengSeasons.Winter => 0.65f,
                _ => 0f,
            };

            bool fieldAlive = rootFieldWhoAmI >= 0 && rootFieldWhoAmI < Main.maxProjectiles &&
                              Main.projectile[rootFieldWhoAmI].active &&
                              Main.projectile[rootFieldWhoAmI].type == ModContent.ProjectileType<DazhengRootField>();

            if (intensity > 0f) {
                if (!fieldAlive) {
                    rootFieldWhoAmI = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<DazhengRootField>(), 0, 0f, Main.myPlayer, NPC.whoAmI, intensity);
                }
                else {
                    Main.projectile[rootFieldWhoAmI].ai[1] = intensity;
                    Main.projectile[rootFieldWhoAmI].netUpdate = true;
                }
            }
            else {
                // 本季不需要根须场: 直接清除, 避免空场残留
                KillTrackedProjectile(ref rootFieldWhoAmI);
            }
        }

        private void KeepRootFieldAlive() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (rootFieldWhoAmI >= 0 && rootFieldWhoAmI < Main.maxProjectiles) {
                Projectile p = Main.projectile[rootFieldWhoAmI];
                if (p.active && p.type == ModContent.ProjectileType<DazhengRootField>())
                    p.timeLeft = 12;
            }
        }

        private void KeepThreadAlive() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            if (healThreadWhoAmI >= 0 && healThreadWhoAmI < Main.maxProjectiles) {
                Projectile p = Main.projectile[healThreadWhoAmI];
                if (p.active && p.type == ModContent.ProjectileType<DazhengHealThread>())
                    p.timeLeft = 12;
            }
        }

        private void KillTrackedProjectile(ref int who) {
            if (Main.netMode != NetmodeID.MultiplayerClient && who >= 0 && who < Main.maxProjectiles) {
                Projectile p = Main.projectile[who];
                if (p.active)
                    p.Kill();
            }
            who = -1;
        }

        /// <summary>把治疗线切入「斩线回抽」演出 (ai[2]=1, 由丝线自走 20t 鞭甩后自灭)。</summary>
        private void SnapTrackedThread(ref int who) {
            if (Main.netMode != NetmodeID.MultiplayerClient && who >= 0 && who < Main.maxProjectiles) {
                Projectile p = Main.projectile[who];
                if (p.active && p.type == ModContent.ProjectileType<DazhengHealThread>()) {
                    p.ai[2] = 1f;
                    p.netUpdate = true;
                }
            }
            who = -1;
        }

        private void KillTrackedNPC(ref int who) {
            // 切季时强制清掉上一季诱饵, 避免残留虚影
            if (Main.netMode != NetmodeID.MultiplayerClient && who >= 0 && who < Main.maxNPCs) {
                NPC n = Main.npc[who];
                if (n.active && n.type == ModContent.NPCType<DazhengDecoyTree>()) {
                    n.life = 0;
                    n.active = false;
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, number: who);
                }
            }
            who = -1;
        }

        #endregion

        #region 季节视觉

        private void UpdateSeasonVisuals() {
            // 切季闪光衰减
            seasonFlash = MathHelper.Lerp(seasonFlash, 0f, 0.06f);

            // 续命根须场 (若在场)
            KeepRootFieldAlive();

            // 死亡演出: 世界褪色 → 终爆时回涌金绿 (LUT 讲叙事)
            if (IsDying) {
                lutSatOverride = PhaseTimer >= DeathStillEnd ? 1.12f : 0.45f;
            }
            else {
                lutSatOverride = -1f;
            }

            // LUT 参数 (PostDraw 用)
            float lutTarget = IsDying ? 0.6f : (gatePassed ? 0.5f : 0.18f);
            lutIntensity = MathHelper.Lerp(lutIntensity, lutTarget, 0.04f);
            lutShadow = DazhengSeasons.LutShadow(season);
            lutHi = DazhengSeasons.LutHighlight(season);
            float satGoal = lutSatOverride >= 0f ? lutSatOverride : DazhengSeasons.LutSaturation(season);
            lutSat = MathHelper.Lerp(lutSat, satGoal, IsDying ? 0.03f : 0.05f);

            // 发布季节氛围 (ElementalScreenTint, 廉价第二层; 死亡期渐弱让位于褪色)
            if (Main.netMode != NetmodeID.Server) {
                float strength = MathHelper.Clamp(0.55f + seasonFlash * 0.4f, 0f, 1f);
                if (IsDying)
                    strength *= 1f - DeathProgress * 0.8f;
                DazhengSeasonScreenSystem.Publish(DazhengSeasons.Tint(season), strength, globalTime);
            }
        }

        // ============================================================
        //  Lifeburst 生命冲击环 (专属着色器, 本地视觉池 ≤3)
        // ============================================================

        /// <summary>发射一圈生命冲击环 (纯本地视觉; 服务器忽略)。</summary>
        private void SpawnLifeRing(float maxRadius, float thickness, Color core, Color edge, int lifetime) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < lifeRings.Length; i++) {
                if (!lifeRings[i].Active) {
                    lifeRings[i] = new LifeRing {
                        Active = true,
                        Age = 0f,
                        Lifetime = lifetime,
                        MaxRadius = maxRadius,
                        Thickness = thickness,
                        Core = core,
                        Edge = edge,
                    };
                    return;
                }
            }
            // 池满: 覆写最老的一环
            int oldest = 0;
            for (int i = 1; i < lifeRings.Length; i++)
                if (lifeRings[i].Age > lifeRings[oldest].Age) oldest = i;
            lifeRings[oldest] = new LifeRing {
                Active = true, Age = 0f, Lifetime = lifetime,
                MaxRadius = maxRadius, Thickness = thickness, Core = core, Edge = edge,
            };
        }

        private void UpdateLifeRings() {
            if (Main.dedServ)
                return;
            for (int i = 0; i < lifeRings.Length; i++) {
                if (!lifeRings[i].Active)
                    continue;
                lifeRings[i].Age++;
                if (lifeRings[i].Age >= lifeRings[i].Lifetime)
                    lifeRings[i].Active = false;
            }
        }

        private static Effect GetLifeburstEffect() {
            if (Main.dedServ)
                return null;
            lifeburstEffect ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/DazhengLifeburst", AssetRequestMode.ImmediateLoad);
            return lifeburstEffect?.Value;
        }

        /// <summary>绘制在场的生命冲击环 (须在已有活动批的阶段调用; 每环一次开合批, 环稀少故可承受)。</summary>
        private void DrawLifeRings(SpriteBatch sb) {
            Effect fx = GetLifeburstEffect();
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            for (int i = 0; i < lifeRings.Length; i++) {
                ref LifeRing ring = ref lifeRings[i];
                if (!ring.Active)
                    continue;

                float progress = MathHelper.Clamp(ring.Age / ring.Lifetime, 0f, 1f);
                // 半径展开: 快出缓收 (1-(1-p)^2.4)
                float radius = ring.MaxRadius * (1f - MathF.Pow(1f - progress, 2.4f));

                ACMShaders.WorldDecalParams(NPC.Center, radius, out Vector2 uv, out float radFrac, out float aspect);
                float zoom = Main.GameViewMatrix.Zoom.X;
                fx.Parameters["uTime"]?.SetValue(globalTime);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(radFrac);
                fx.Parameters["uIntensity"]?.SetValue(0.85f);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorCore"]?.SetValue(ring.Core.ToVector4());
                fx.Parameters["uColorEdge"]?.SetValue(ring.Edge.ToVector4());
                fx.Parameters["uThickness"]?.SetValue(ring.Thickness * zoom / Main.screenHeight);
                fx.Parameters["uProgress"]?.SetValue(progress);

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);
                sb.Draw(noise, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
                sb.End();
                ACMShaders.RestoreDefaultBatch(sb);
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = new(0, 0, texture.Width, texture.Height);

            // —— 树身活化变换: 底部枢轴摇曳 + 呼吸 + 归土下沉 + 转换坍缩 ——
            float rot = swayAngle;
            float breath = 1f + MathF.Sin(breathPhase) * 0.006f;
            float collapse = TransitionCollapse;
            float sx = NPC.scale * (1f - collapse * 0.015f);
            float sy = NPC.scale * breath * (1f - collapse * 0.02f);

            Vector2 basePos = NPC.Center + new Vector2(0, TextureHeight / 2f + DeathSinkOffset) - screenPos;
            Vector2 bottomOrigin = new(TextureWidth / 2f, TextureHeight);
            // 摇曳后的躯干视觉中心 (供辉光叠层跟随)
            Vector2 drawPos = basePos + new Vector2(0, -TextureHeight / 2f * sy).RotatedBy(rot);

            // 二阶段金色底光 (随树冠滞后摆动 — 次级运动)
            float glowPulse = 0f;
            if (IsPhase2)
                glowPulse = 0.15f + MathF.Sin(globalTime * 4f) * 0.1f;
            if (glowPulse > 0) {
                Color goldGlow = new Color(255, 200, 50, 0) * glowPulse;
                spriteBatch.Draw(texture, basePos, frame, goldGlow, canopySway, bottomOrigin,
                    new Vector2(sx, sy) * 1.05f, SpriteEffects.None, 0f);
            }

            // 主体绘制 (底部枢轴)
            spriteBatch.Draw(texture, basePos, frame, drawColor, rot, bottomOrigin,
                new Vector2(sx, sy), SpriteEffects.None, 0f);

            // —— 廉价表现叠层 (加性, 不占全屏后处理名额) ——
            if (!Main.dedServ) {
                Texture2D soft = ACMAsset.SoftGlow;
                if (soft != null) {
                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    Vector2 go = soft.Size() / 2f;

                    // 切季闪光 (季节色)
                    if (seasonFlash > 0.02f) {
                        Color fc = DazhengSeasons.Tint(season) with { A = 0 };
                        spriteBatch.Draw(soft, drawPos, null, fc * (0.6f * seasonFlash), 0f, go,
                            (TextureWidth * 0.9f / soft.Width) * (0.6f + seasonFlash * 0.6f), SpriteEffects.None, 0f);
                    }

                    // 破绽窗口: 暴露的金核脉冲 (可读: 现在打它)
                    if (defenseDownTimer > 0) {
                        float p = 0.5f + 0.5f * MathF.Sin(globalTime * 10f);
                        Color core = TelegraphColors.Holy with { A = 0 };
                        spriteBatch.Draw(soft, drawPos - new Vector2(0, TextureHeight * 0.15f), null,
                            core * (0.5f * p), 0f, go, 1.2f + p * 0.4f, SpriteEffects.None, 0f);
                    }

                    // 秋季诱饵窗口: 真身金核暴露脉冲 (心跳同拍收缩 — 真身微特征)
                    if (Phase == BossPhase.SeasonCombat && season == DazhengSeasons.Autumn && decoyWindowOpen) {
                        float hb = MathF.Exp(-((AttackTimer - (SeasonConnectorTicks + 1)) % 90) / 14f); // 心跳衰减脉冲
                        float p = 0.5f + 0.5f * MathF.Sin(globalTime * 6f);
                        Color core = new Color(255, 210, 90, 0);
                        spriteBatch.Draw(soft, drawPos - new Vector2(0, TextureHeight * 0.12f), null,
                            core * (0.55f * p + 0.3f * hb), 0f, go, 1.4f + p * 0.5f + hb * 0.5f, SpriteEffects.None, 0f);
                    }

                    // 死亡演出「寂」段: 躯干金光渐熄 (生命向根部退去)
                    if (IsDying && PhaseTimer <= DeathSinkEnd) {
                        float fade = 1f - MathHelper.Clamp((float)(PhaseTimer / DeathSinkEnd), 0f, 1f);
                        Color dim = new Color(200, 230, 160, 0);
                        spriteBatch.Draw(soft, drawPos, null, dim * (0.3f * fade), 0f, go,
                            TextureWidth * 0.7f / soft.Width, SpriteEffects.None, 0f);
                    }

                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                }

                // —— 夏季金叶风暴: 安全扇区 Safe 光线 (公平阀门的可视化) ——
                DrawNovaSafeSector();

                // —— 生命冲击环 (专属 Lifeburst 着色器) ——
                DrawLifeRings(spriteBatch);

                // —— 爆发帧径向泛光 (占当帧全屏名额 → LUT 自动让位) ——
                DrawBurstBloom();
            }

            return false;
        }

        /// <summary>夏季金叶风暴安全扇区光线: 收束期渐显, 三环期间保持 (SubState=扇区角, 已同步)。</summary>
        private void DrawNovaSafeSector() {
            if (Phase != BossPhase.SeasonCombat || season != DazhengSeasons.Summer)
                return;
            int dur = SeasonDuration(DazhengSeasons.Summer, IsPhase2);
            int novaStart = NovaStart(dur);
            float t = AttackTimer - novaStart;
            if (t < 6 || t > 110)
                return;

            // 收束期渐显 → 释放期满亮 → 收尾渐隐
            float vis = MathHelper.Clamp(t / 40f, 0f, 1f) * MathHelper.Clamp((110f - t) / 14f, 0f, 1f);
            if (vis <= 0.02f)
                return;

            float safeAngle = SubState;
            Vector2 dir = safeAngle.ToRotationVector2();
            float len = ArenaRadius * 0.85f;
            float pulse = 0.75f + 0.25f * MathF.Sin(globalTime * 8f);
            ACMShaders.DrawBeam(NPC.Center + dir * 90f, NPC.Center + dir * len, 26f,
                TelegraphColors.Safe, new Color(90, 200, 120), 0.5f * vis * pulse,
                flowSpeed: 1.6f, flowScale: 2f, coreSharp: 1.6f);
        }

        /// <summary>转换爆发 / 死亡终爆的径向泛光 (内部走 RequestFullscreenSlot 名额契约)。</summary>
        private void DrawBurstBloom() {
            // 换阶段爆发帧后 20t 内衰减
            if (Phase == BossPhase.PhaseTransition_2 &&
                PhaseTimer >= Transition2BurstTick && PhaseTimer < Transition2BurstTick + 20) {
                float p = 1f - (PhaseTimer - Transition2BurstTick) / 20f;
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.28f * p + 0.06f, 0.85f * p,
                    new Color(255, 225, 130), rayCount: 12f, falloff: 2.2f);
            }
            // 死亡终爆帧后 32t 内衰减 (全场最大的一拍)
            else if (IsDying && PhaseTimer >= DeathStillEnd && PhaseTimer < DeathStillEnd + 32) {
                float p = 1f - (PhaseTimer - DeathStillEnd) / 32f;
                ACMShaders.DrawRadialBloomAt(
                    new Vector2(spawnPosition.X, spawnPosition.Y + TextureHeight / 2f),
                    0.4f * p + 0.08f, 0.95f * p, new Color(220, 255, 170), rayCount: 14f, falloff: 2f);
            }
        }

        // PaletteLUT 四季全屏调色 (单一全屏后处理名额)
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || lutIntensity <= 0.01f)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = ACMShaders.PaletteLUT;
            if (fx == null)
                return;

            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(lutIntensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uSaturation"]?.SetValue(lutSat);
            fx.Parameters["uHueShift"]?.SetValue(0f);
            fx.Parameters["uShadowTint"]?.SetValue(lutShadow);
            fx.Parameters["uHighlightTint"]?.SetValue(lutHi);
            fx.Parameters["uSplit"]?.SetValue(0f);

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        #endregion

        #region 共享: 藤蔓墙

        private void SpawnVineWall(Vector2 center, int direction, bool horizontal, bool persistent = false) {
            int vineCount = Main.expertMode ? 12 : 10;
            int gapIndex = Main.rand.Next(2, vineCount - 3);
            int gapSize = Main.expertMode ? 3 : 4;
            float speed = persistent ? 5f : 8f;
            int life = persistent ? 480 : 300;

            for (int i = 0; i < vineCount; i++) {
                if (i >= gapIndex && i < gapIndex + gapSize) continue;

                Vector2 spawnPos;
                Vector2 vel;
                float spacing = 80f;

                if (horizontal) {
                    spawnPos = center + new Vector2(direction * 760, -vineCount / 2 * spacing + i * spacing);
                    vel = new Vector2(-direction * speed, 0);
                }
                else {
                    spawnPos = center + new Vector2(-vineCount / 2 * spacing + i * spacing, direction * 760);
                    vel = new Vector2(0, -direction * speed);
                }

                int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                    ModContent.ProjectileType<DazhengVine>(), NPC.damage / 4, 0f, Main.myPlayer);
                if (proj >= 0 && proj < Main.maxProjectiles)
                    Main.projectile[proj].timeLeft = life;
            }
        }

        #endregion
    }
}
