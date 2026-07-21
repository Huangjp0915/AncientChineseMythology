using AncientChineseMythology.Celestias.Boss.Dryades.Items;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
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

namespace AncientChineseMythology.Celestias.Boss.Dryades
{
    /// <summary>
    /// 树精 — 月后初期·潜地伏击树妖 (V3 缠绕与蔓延重做)。
    ///
    /// 主题: **大地本身在猎杀你** —— 阴柔、包围式的场地压迫 (对比大椿的四季堂皇/玄武的重甲工事)。
    /// 一半时间潜地 (伏击张力), 地表窗口按确定性循环表轮换 (PACING §2 手工编排):
    ///   P1  [根须爆发 → 藤鞭横扫 → 刺球领域]
    ///   P2  [藤鞭横扫×3 → 根须爆发(夹击) → 刺球领域(双辐条)] + 潜地留毒孢区 + 汲魂灵芽(治疗反制)
    ///   疯长(<25%) 循环表首位插入 [万藤缠狱] set-piece (冠层放射鞭阵 → 力竭破绽窗)
    ///
    /// 招牌机制:
    ///  - **藤鞭 VineLash**: 分段弹簧节点鞭 (DryadsVine 模式 2) — 长背挥蓄势 → poly(10) 一瞬抽落。
    ///  - **冒出点预警**: 地下等待期移动根瘤爬向目标 + ArenaRunic 根纹圈 (绿→末段赤红)。
    ///  - **疯长 Overgrowth**: 屏幕边缘 SDF 卷须随阶段/事件生长 (DryadsOvergrowth.fx)。
    ///  - **汲魂灵芽**: P2 打靶式治疗反制 (击杀断汲取, 区别于大椿的冲刺挣断导管)。
    ///
    /// 身体语言: 本体绕树根着地点做倾斜/挤压双弹簧 + 呼吸 (质量即反应, MOTION §4)。
    /// 贴图尺寸: 520×552, 底部 60 像素为树根需埋入地下。
    /// </summary>
    [AutoloadBossHead]
    public class Dryads : ModNPC
    {
        #region 常量

        public const int TextureWidth = 520;
        public const int TextureHeight = 552;
        public const int RootBuryOffset = 60; // 树根埋入地下的像素偏移
        public const float Phase2Threshold = 0.50f;
        public const float FrenzyThreshold = 0.25f;

        // 主题色
        private static readonly Color VerdantGreen = new(90, 210, 70);

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,
            Surface_RootBurst,   // 地表窗口 A: 根须柱行军波 (逐柱预告 → 破土)
            Surface_SpikeField,  // 地表窗口 C: 向心收缩刺环 + 旋转安全辐条
            Burrow,              // 潜地转移: 下沉(无敌) → 地下(移动根瘤+冒出预警) → 冒出
            PhaseTransition_2,   // P2 蔓生转换 (150f 心跳→绽放)
            Surface_VineLash,    // 地表窗口 B: 藤鞭横扫 (招牌)
            VinePrison,          // 万藤缠狱 set-piece (<25% 扣留内容)
            Exhausted,           // 缠狱后力竭破绽窗 (增伤 ×1.25, 无攻击)
            Death,               // 死亡演出 (~330f)
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
        public bool IsFrenzy => NPC.life < NPC.lifeMax * FrenzyThreshold;

        private float globalTime;
        private bool didPhase2Transition;
        private bool frenzyAnnounced;
        private bool deathStarted;
        private Vector2 anchorPosition;     // 当前固定位置（树精站桩点）
        private float introRiseOffset;      // 入场上升偏移
        private bool isRising;              // 是否正在上升
        private bool isBurrowing;           // 是否正在潜入/冒出中
        private float burrowProgress;       // 潜地进度 0=完全在地面, 1=完全在地下
        private int attackCycle;            // 循环表指针 (确定性轮换)
        private Vector2 burrowTargetPos;    // 潜地后的目标冒出位置（锚点空间）

        // —— 演出/机制状态 ——
        private float emergeTell;           // 冒出点地纹预警强度 0~1
        private float emergeFlash;          // 冒出/爆发瞬间径向泛光闪 0~1 (衰减)
        private Vector2 fieldCenter;        // 刺球领域收缩中心
        private float fieldGapAngle;        // 安全辐条角度 (逐波确定性旋转)
        private float spokeTell;            // 安全辐条预告强度 0~1
        private bool budSeenLocal;          // (本地视觉) 场上是否见过灵芽 → 检测被击杀的痛苦反馈
        private float budLastLifeFrac = 1f; // 灵芽上帧血量比 (区分"被击杀"与"自然缩回")

        // —— 身体弹簧动画 (纯视觉, 各端本地演化) ——
        private float lean;        // 倾斜角 (绕树根着地点)
        private float leanVel;
        private float squash;      // 挤压 (>0 = 纵向压缩, 保体积横向补偿)
        private float squashVel;
        private float deathWither; // 死亡凋萎度 0~1 (贴图向枯褐插值)
        private float crackFlash;  // 死亡心跳裂纹闪 0~1 (衰减)

        #endregion

        #region 循环表 (确定性编排, PACING §2)

        private static readonly BossPhase[] TableP1 = {
            BossPhase.Surface_RootBurst, BossPhase.Surface_VineLash, BossPhase.Surface_SpikeField,
        };
        private static readonly BossPhase[] TableP2 = {
            BossPhase.Surface_VineLash, BossPhase.Surface_RootBurst, BossPhase.Surface_SpikeField,
        };
        private static readonly BossPhase[] TableFrenzy = {
            BossPhase.VinePrison, BossPhase.Surface_VineLash,
            BossPhase.Surface_RootBurst, BossPhase.Surface_SpikeField,
        };

        private BossPhase[] CurrentTable => IsFrenzy ? TableFrenzy : (IsPhase2 ? TableP2 : TableP1);

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

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 160;
            NPC.height = 200;
            NPC.damage = 150;
            NPC.defense = 60;
            NPC.lifeMax = 1000000;
            NPC.HitSound = SoundID.NPCHit1 with { Pitch = -0.3f };
            NPC.DeathSound = SoundID.NPCDeath1 with { Pitch = -0.6f };
            NPC.value = Item.buyPrice(platinum: 3);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 30f;
            NPC.aiStyle = -1;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.35f);
                NPC.damage = (int)(NPC.damage * 1.2f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.3f);
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
            // 活木 - 掉落45~60个
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Livinglog>(), minimumDropped: 45, maximumDropped: 60));
        }

        public override void OnSpawn(IEntitySource source) {
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            globalTime = 0;
            anchorPosition = FindGroundPosition(NPC.Center);
            introRiseOffset = TextureHeight + 100f;
            isRising = true;
            isBurrowing = false;
            burrowProgress = 0f;
            NPC.Center = anchorPosition + new Vector2(0, introRiseOffset);
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        /// <summary>
        /// 判断指定位块是否为真正的坚实地面（排除平台、桌子等SolidTop物块和被致动的物块）
        /// </summary>
        private static bool IsSolidGround(Tile tile) {
            return tile.HasTile
                && Main.tileSolid[tile.TileType]
                && !Main.tileSolidTop[tile.TileType]
                && !tile.IsActuated;
        }

        /// <summary>
        /// 从给定位置向下扫描找到固体地面
        /// 树精底部对齐地面，但树根60像素需埋入地下
        /// 所以Center = 地面Y - TextureHeight/2 + RootBuryOffset
        /// </summary>
        private Vector2 FindGroundPosition(Vector2 startPos) {
            int tileX = (int)(startPos.X / 16f);
            int startTileY = (int)(startPos.Y / 16f);

            int groundTileY = startTileY;
            for (int y = startTileY; y < startTileY + 150 && y < Main.maxTilesY - 1; y++) {
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (IsSolidGround(tile)) {
                    groundTileY = y;
                    break;
                }
            }

            float groundWorldY = groundTileY * 16f;
            // Center位置：地面 - 纹理半高 + 树根埋入偏移
            float centerY = groundWorldY - TextureHeight / 2f + RootBuryOffset;
            return new Vector2(startPos.X, centerY);
        }

        /// <summary>
        /// 任意世界 X 处的地面线 Y (从参考高度上方 200px 向下扫描)。
        /// 供根须柱/藤鞭在**自己的**坐标下采样真实地面 (修复旧版用 Boss 地表线近似导致的坡地错位)。
        /// </summary>
        private static float FindGroundYAt(float worldX, float refWorldY) {
            int tileX = (int)(worldX / 16f);
            int startTileY = (int)((refWorldY - 200f) / 16f);
            if (startTileY < 1) startTileY = 1;
            for (int y = startTileY; y < startTileY + 160 && y < Main.maxTilesY - 1; y++) {
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (IsSolidGround(tile))
                    return y * 16f;
            }
            return refWorldY + 200f; // 兜底: 悬空场景直接给参考线下方
        }

        /// <summary>
        /// 从玩家位置附近找到最近的坚实地面，用于潜地冒出。
        /// 多次采样，优先选择真正的坚实地面而非平台。
        /// </summary>
        private Vector2 FindGroundNearPlayer(Player player) {
            const int Candidates = 5;
            const float SearchRangeX = 300f;

            Vector2 bestPos = Vector2.Zero;
            float bestDist = float.MaxValue;
            bool bestIsSolid = false;

            for (int i = 0; i < Candidates; i++) {
                float offsetX = Main.rand.NextFloat(-SearchRangeX, SearchRangeX);
                Vector2 searchStart = player.Center + new Vector2(offsetX, -400);

                int tileX = (int)(searchStart.X / 16f);
                int startTileY = (int)(searchStart.Y / 16f);

                bool foundSolid = false;
                int groundTileY = startTileY;

                for (int y = startTileY; y < startTileY + 150 && y < Main.maxTilesY - 1; y++) {
                    Tile tile = Framing.GetTileSafely(tileX, y);
                    if (IsSolidGround(tile)) {
                        groundTileY = y;
                        foundSolid = true;
                        break;
                    }
                }

                float groundWorldY = groundTileY * 16f;
                float centerY = groundWorldY - TextureHeight / 2f + RootBuryOffset;
                Vector2 candidatePos = new(searchStart.X, centerY);
                float dist = Vector2.Distance(player.Center, candidatePos);

                // 坚实地面优先；同类地面中选最近的
                if ((foundSolid && !bestIsSolid) || (foundSolid == bestIsSolid && dist < bestDist)) {
                    bestPos = candidatePos;
                    bestDist = dist;
                    bestIsSolid = foundSolid;
                }
            }

            // 如果所有候选都没找到坚实地面，回退到普通扫描
            if (bestPos == Vector2.Zero)
                return FindGroundPosition(player.Center + new Vector2(0, -400));

            return bestPos;
        }

        /// <summary>当前锚点对应的地表世界 Y（树根埋点上方的地面线）。</summary>
        private float GroundYOf(Vector2 anchor) => anchor.Y + TextureHeight / 2f - RootBuryOffset;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((int)Phase);
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(frenzyAnnounced);
            writer.Write(deathStarted);
            writer.Write(anchorPosition.X);
            writer.Write(anchorPosition.Y);
            writer.Write(introRiseOffset);
            writer.Write(isRising);
            writer.Write(isBurrowing);
            writer.Write(burrowProgress);
            writer.Write(attackCycle);
            writer.Write(burrowTargetPos.X);
            writer.Write(burrowTargetPos.Y);
            writer.Write(fieldCenter.X);
            writer.Write(fieldCenter.Y);
            writer.Write(fieldGapAngle);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            frenzyAnnounced = reader.ReadBoolean();
            deathStarted = reader.ReadBoolean();
            anchorPosition = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            introRiseOffset = reader.ReadSingle();
            isRising = reader.ReadBoolean();
            isBurrowing = reader.ReadBoolean();
            burrowProgress = reader.ReadSingle();
            attackCycle = reader.ReadInt32();
            burrowTargetPos = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            fieldCenter = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            fieldGapAngle = reader.ReadSingle();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            if (isRising || burrowProgress > 0.8f) return false;
            return null;
        }

        public override bool CheckActive() => false;

        /// <summary>
        /// 接触伤害公平阀: 入场/潜地全程/上升/换阶段/破绽窗/死亡演出一律无接触伤害
        /// (伤害窗与视觉对齐 — 半截身子在土里的树不该"贴脸怼")。
        /// </summary>
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            if (isRising || isBurrowing || burrowProgress > 0.05f)
                return false;
            return Phase is not (BossPhase.Intro or BossPhase.PhaseTransition_2
                or BossPhase.Exhausted or BossPhase.Death);
        }

        /// <summary>力竭破绽窗: 受到伤害 ×1.25 (全场唯一增伤窗, 缠狱后的正反馈)。</summary>
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            if (Phase == BossPhase.Exhausted)
                modifiers.FinalDamage *= 1.25f;
        }

        public override void DrawBehind(int index) {
            if (isRising || isBurrowing)
                Main.instance.DrawCacheProjsBehindNPCsAndTiles.Add(index);
        }

        public override void HitEffect(NPC.HitInfo hit) {
            // 受击微反应: 倾斜弹簧吃一记小冲量 (质量即反应)
            leanVel += hit.HitDirection * 0.0035f;

            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.WoodFurniture, hit.HitDirection * 2f, -2f, 150, default, 1.5f);
                d.noGravity = false;
            }
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.GrassBlades, hit.HitDirection * 1.5f, -1f, 100, default, 1.8f);
                d.noGravity = true;
            }
        }

        /// <summary>死亡演出拦截: 首次致死转入 Death 编排, 演出末尾真正死亡。</summary>
        public override bool CheckDead() {
            if (deathStarted)
                return true;

            deathStarted = true;
            NPC.life = 1;
            NPC.dontTakeDamage = true;
            isBurrowing = false;
            isRising = false;
            burrowProgress = 0f;

            // 清弹清芽 (公平阀 + 演出留白)
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.hostile && p.damage > 0)
                        p.Kill();
                }
                int budType = ModContent.NPCType<DryadsSiphonBud>();
                for (int i = 0; i < Main.maxNPCs; i++) {
                    if (Main.npc[i].active && Main.npc[i].type == budType)
                        Main.npc[i].active = false;
                }
            }

            TransitionTo(BossPhase.Death);
            return false;
        }

        public override void OnKill() {
            DownedBossSystem.downedDryads = true;
            if (Main.netMode != NetmodeID.Server) {
                PunchCameraModifier modifier = new(NPC.Center,
                    (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(),
                    18f, 10f, 60, 2500f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        #endregion

        #region AI 主循环

        public override void AI() {
            globalTime += 1f / 60f;

            NPC.velocity = Vector2.Zero;

            // 位置跟随当前锚点（考虑入场/潜地偏移）
            if (anchorPosition != Vector2.Zero) {
                if (isRising)
                    NPC.Center = anchorPosition + new Vector2(0, introRiseOffset);
                else if (isBurrowing) {
                    float burrowOffset = burrowProgress * (TextureHeight + 60f);
                    NPC.Center = anchorPosition + new Vector2(0, burrowOffset);
                }
                else
                    NPC.Center = anchorPosition;
            }

            NPC.behindTiles = isRising || isBurrowing;
            if (isRising) {
                for (int i = 0; i < 60; i++)
                    Lighting.AddLight(NPC.Center + Main.rand.NextVector2Circular(300, 300),
                        Color.ForestGreen.ToVector3() * 6);
            }

            // —— 身体弹簧演化 (纯视觉, 每端本地) ——
            UpdateBodySprings();

            // 视觉计时衰减 (各客户端本地)
            if (emergeFlash > 0f) emergeFlash -= 0.04f;
            if (crackFlash > 0f) crackFlash -= 0.05f;
            if (Phase != BossPhase.Burrow) emergeTell = MathHelper.Lerp(emergeTell, 0f, 0.2f);
            if (Phase != BossPhase.Surface_SpikeField) spokeTell = MathHelper.Lerp(spokeTell, 0f, 0.2f);

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if ((!target.active || target.dead) && Phase != BossPhase.Death) {
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            // 潜地中不受攻击 (Death 演出的无敌由 CheckDead 落锁)
            if (!deathStarted)
                NPC.dontTakeDamage = isRising || burrowProgress > 0.5f;

            CheckPhaseTransition();
            CheckFrenzyAnnounce();
            DetectBudSevered();
            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.Surface_RootBurst: RunRootBurst(target); break;
                case BossPhase.Surface_SpikeField: RunSpikeField(target); break;
                case BossPhase.Surface_VineLash: RunVineLash(target); break;
                case BossPhase.Burrow: RunBurrow(target, IsPhase2); break;
                case BossPhase.PhaseTransition_2: RunPhaseTransition2(target); break;
                case BossPhase.VinePrison: RunVinePrison(target); break;
                case BossPhase.Exhausted: RunExhausted(target); break;
                case BossPhase.Death: RunDeath(target); break;
            }

            // 状态机保底出口: 任何非入场/死亡状态滞留过久 → 强制潜地重置 (失败模式: 状态死路)
            if (Phase is not (BossPhase.Intro or BossPhase.Death) && PhaseTimer > 1800)
                GoBurrow();

            // 疯长屏幕蔓延发布 (纯本地视觉)
            PublishOvergrowth();

            // 自然光辉
            float glow = 0.4f + MathF.Sin(globalTime * 2f) * 0.15f;
            Lighting.AddLight(NPC.Center, new Vector3(0.2f, 0.45f, 0.1f) * glow);

            // 持续落叶粒子
            if (Main.netMode != NetmodeID.Server && !isBurrowing && Phase != BossPhase.Death && Main.rand.NextBool(4)) {
                Vector2 leafPos = NPC.Center + new Vector2(
                    Main.rand.NextFloat(-TextureWidth / 2, TextureWidth / 2),
                    Main.rand.NextFloat(-TextureHeight / 2, TextureHeight / 4));
                Dust d = Dust.NewDustDirect(leafPos, 0, 0, DustID.GrassBlades,
                    Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(1f, 3f), 100, default, 1.3f);
                d.noGravity = false;
                d.fadeIn = 1.1f;
            }
        }

        /// <summary>倾斜/挤压双弹簧 + 呼吸 (MOTION §4: 弹簧肢体/事后余摆)。</summary>
        private void UpdateBodySprings() {
            // 待机微摆 (风)
            float leanTarget = MathF.Sin(globalTime * 0.5f) * 0.012f;

            // 状态驱动的倾斜目标
            if (Phase == BossPhase.Surface_VineLash && AttackTimer < 60f)
                leanTarget += 0.03f * MathF.Sign(NPC.Center.X - Main.player[NPC.target].Center.X); // 反向蓄势
            if (Phase == BossPhase.VinePrison && PhaseTimer < 50f)
                leanTarget -= 0.10f * (PhaseTimer / 50f) * MathF.Sign(Main.player[NPC.target].Center.X - NPC.Center.X); // 深后仰
            if (Phase == BossPhase.Exhausted)
                leanTarget += 0.16f * MathF.Sign(Main.player[NPC.target].Center.X - NPC.Center.X); // 前倾瘫软

            leanVel += (leanTarget - lean) * 0.10f;
            leanVel *= 0.84f;
            lean += leanVel;
            lean = MathHelper.Clamp(lean, -0.32f, 0.32f);

            float squashTarget = 0f;
            if (Phase == BossPhase.Exhausted) squashTarget = 0.07f;
            if (Phase == BossPhase.Death && PhaseTimer > 200f) squashTarget = 0.12f;

            squashVel += (squashTarget - squash) * 0.14f;
            squashVel *= 0.80f;
            squash += squashVel;
            squash = MathHelper.Clamp(squash, -0.22f, 0.25f);
        }

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 &&
                Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Intro &&
                Phase != BossPhase.Death && !isBurrowing) {
                TransitionTo(BossPhase.PhaseTransition_2);
                didPhase2Transition = true;
            }
        }

        /// <summary>疯长阈值播报 (<25%): 不打断动作, 循环表换挡 + 屏幕事件。</summary>
        private void CheckFrenzyAnnounce() {
            if (frenzyAnnounced || !IsFrenzy || Phase == BossPhase.Intro || Phase == BossPhase.Death)
                return;
            frenzyAnnounced = true;
            attackCycle = 0; // 循环表指针归零 → 下一个地表窗口即为万藤缠狱 set-piece
            NPC.netUpdate = true;
            squashVel -= 0.07f;
            DryadsOvergrownSystem.Pulse(1f);
            if (Main.netMode != NetmodeID.Server) {
                Announce("Frenzy", VerdantGreen);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.75f, Volume = 1.1f }, NPC.Center);
                ACMUtils.AddScreenShake(7f);
            }
        }

        /// <summary>
        /// 灵芽被击杀的痛苦反馈 (本地视觉检测: 上帧在场、本帧消失, 且上帧血量已明显受损 →
        /// 判为被击杀而非自然缩回)。
        /// </summary>
        private void DetectBudSevered() {
            bool found = false;
            int budType = ModContent.NPCType<DryadsSiphonBud>();
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == budType) {
                    found = true;
                    budLastLifeFrac = npc.life / (float)Math.Max(npc.lifeMax, 1);
                    break;
                }
            }
            if (budSeenLocal && !found && budLastLifeFrac < 0.55f) {
                leanVel += 0.055f * (Main.rand.NextBool() ? 1f : -1f);
                squashVel -= 0.06f;
                if (Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(SoundID.NPCHit7 with { Pitch = -0.5f, Volume = 0.9f }, NPC.Center);
            }
            budSeenLocal = found;
            if (!found)
                budLastLifeFrac = 1f;
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            NPC.netUpdate = true;
        }

        /// <summary>地表攻击窗口结束 → 潜地 (burrow-centric: 每个地表窗口后必潜地)。</summary>
        private void GoBurrow() => TransitionTo(BossPhase.Burrow);

        /// <summary>潜地冒出后 → 按确定性循环表轮换地表窗口。</summary>
        private void GoSurface() {
            BossPhase[] table = CurrentTable;
            TransitionTo(table[attackCycle % table.Length]);
            attackCycle++;
        }

        /// <summary>本地播报 (CombatText, 客户端各自渲染)。</summary>
        private void Announce(string key, Color color) {
            if (Main.dedServ)
                return;
            string text = Language.GetTextValue("Mods.AncientChineseMythology.NPCs.Dryads." + key);
            CombatText.NewText(NPC.getRect(), color, text, true);
        }

        /// <summary>疯长屏幕蔓延强度发布 (发布-订阅, 见 DryadsOvergrownSystem)。</summary>
        private void PublishOvergrowth() {
            if (Main.netMode == NetmodeID.Server)
                return;

            float og = 0f;
            float wither = 0f;

            if (Phase == BossPhase.PhaseTransition_2)
                og = MathHelper.Lerp(0f, 0.42f, MathHelper.Clamp(PhaseTimer / 90f, 0f, 1f));
            else if (Phase == BossPhase.Death) {
                // 死亡: 心跳期维持 → 收束期起枯化退潮
                og = MathHelper.Lerp(0.6f, 0.1f, MathHelper.Clamp((PhaseTimer - 200f) / 120f, 0f, 1f));
                wither = deathWither;
            }
            else if (Phase == BossPhase.VinePrison)
                og = MathHelper.Lerp(0.5f, 0.9f, MathHelper.Clamp(PhaseTimer / 50f, 0f, 1f));
            else if (Phase == BossPhase.Exhausted)
                og = 0.45f;
            else if (didPhase2Transition)
                og = IsFrenzy ? 0.5f : 0.3f;

            DryadsOvergrownSystem.Publish(og, wither);
        }

        #endregion

        #region 入场演出

        private const int IntroRumbleEnd = 50;
        private const int IntroRiseEnd = 170;
        private const int IntroEruptEnd = 210;
        private const int IntroFinish = 240;

        private void RunIntro(Player target) {
            NPC.dontTakeDamage = true;
            float groundWorldY = GroundYOf(anchorPosition);

            // ========== 地面震动预兆（0-50）==========
            if (PhaseTimer <= IntroRumbleEnd) {
                if (PhaseTimer == 1)
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.6f, Volume = 0.5f },
                        new Vector2(anchorPosition.X, groundWorldY));

                if (PhaseTimer % 5 == 0)
                    ACMUtils.AddScreenShake(MathHelper.Lerp(1f, 5f, PhaseTimer / (float)IntroRumbleEnd));

                if (Main.netMode != NetmodeID.Server) {
                    int crackIntensity = (int)MathHelper.Lerp(1, 4, PhaseTimer / (float)IntroRumbleEnd);
                    for (int i = 0; i < crackIntensity; i++) {
                        Vector2 crackPos = new(
                            anchorPosition.X + Main.rand.NextFloat(-200, 200),
                            groundWorldY + Main.rand.NextFloat(-6, 6));
                        Dust d = Dust.NewDustDirect(crackPos, 0, 0, DustID.WoodFurniture,
                            Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, -1f),
                            150, default, Main.rand.NextFloat(1f, 1.8f));
                        d.noGravity = false;
                    }
                    if (PhaseTimer > 15) {
                        for (int i = 0; i < crackIntensity / 2 + 1; i++) {
                            Vector2 glowPos = new(
                                anchorPosition.X + Main.rand.NextFloat(-150, 150),
                                groundWorldY + Main.rand.NextFloat(-4, 4));
                            Dust d = Dust.NewDustDirect(glowPos, 0, 0, DustID.GreenTorch,
                                0, Main.rand.NextFloat(-2f, -0.5f), 80, default, 1.5f);
                            d.noGravity = true;
                        }
                    }
                }

                Lighting.AddLight(new Vector2(anchorPosition.X, groundWorldY),
                    new Vector3(0.15f, 0.4f, 0.08f) * (PhaseTimer / (float)IntroRumbleEnd));
            }

            // ========== 从地下上升（50-170）==========
            if (PhaseTimer > IntroRumbleEnd && PhaseTimer <= IntroRiseEnd) {
                float riseProgress = (PhaseTimer - IntroRumbleEnd) / (float)(IntroRiseEnd - IntroRumbleEnd);
                float easedProgress = riseProgress < 0.5f
                    ? 4f * riseProgress * riseProgress * riseProgress
                    : 1f - MathF.Pow(-2f * riseProgress + 2f, 3f) / 2f;

                float totalRise = TextureHeight + 100f;
                introRiseOffset = totalRise * (1f - easedProgress);

                if (PhaseTimer == IntroRumbleEnd + 1)
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 0.7f },
                        new Vector2(anchorPosition.X, groundWorldY));

                if (Main.netMode != NetmodeID.Server) {
                    float riseSpeed = (riseProgress > 0.1f && riseProgress < 0.9f) ? 1.2f : 0.4f;
                    for (int i = 0; i < (int)(3 * riseSpeed); i++) {
                        Vector2 dirtPos = new(
                            anchorPosition.X + Main.rand.NextFloat(-250, 250),
                            groundWorldY + Main.rand.NextFloat(-15, 8));
                        Dust d = Dust.NewDustDirect(dirtPos, 0, 0, DustID.WoodFurniture,
                            Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-5f, -1.5f),
                            120, default, Main.rand.NextFloat(1.2f, 2f));
                        d.noGravity = false;
                    }

                    if (riseProgress > 0.2f) {
                        for (int i = 0; i < 2; i++) {
                            Vector2 vinePos = new(
                                anchorPosition.X + Main.rand.NextFloat(-280, 280),
                                groundWorldY - Main.rand.NextFloat(0, 20));
                            Dust d = Dust.NewDustDirect(vinePos, 0, 0, DustID.JungleGrass,
                                Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-3f, -1f),
                                100, default, 1.6f);
                            d.noGravity = true;
                        }
                    }

                    if (PhaseTimer % 7 == 0)
                        ACMUtils.AddScreenShake(2f + riseSpeed * 3f);
                }

                float lightStr = MathHelper.Lerp(0.2f, 0.6f, riseProgress);
                Lighting.AddLight(NPC.Center, new Vector3(0.2f, 0.45f, 0.1f) * lightStr);
            }

            // ========== 破土爆发（170）==========
            if (PhaseTimer == IntroRiseEnd) {
                introRiseOffset = 0f;
                isRising = false;

                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.5f, Volume = 1f }, NPC.Center);
                emergeFlash = 1f;
                // 爆发帧: 挤压冲击 + 侧倾余摆 (入场落定的"重量")
                squashVel = -0.18f;
                leanVel = 0.05f;

                if (Main.netMode != NetmodeID.Server) {
                    // 入场一次性大震 (cinematic, §6.2 入场预算)
                    PunchCameraModifier modifier = new(NPC.Center,
                        (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(),
                        16f, 10f, 30, 2500f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);

                    for (int i = 0; i < 30; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float speed = Main.rand.NextFloat(3f, 10f);
                        Vector2 debrisPos = new(anchorPosition.X + Main.rand.NextFloat(-150, 150),
                            groundWorldY + Main.rand.NextFloat(-20, 8));
                        Dust d = Dust.NewDustDirect(debrisPos, 0, 0, DustID.WoodFurniture,
                            MathF.Cos(angle) * speed, MathF.Sin(angle) * speed - 2f,
                            100, default, Main.rand.NextFloat(1.5f, 3f));
                        d.noGravity = false;
                    }
                    for (int i = 0; i < 20; i++) {
                        float angle = MathHelper.TwoPi / 20 * i;
                        float speed = Main.rand.NextFloat(5f, 10f);
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GreenTorch,
                            MathF.Cos(angle) * speed, MathF.Sin(angle) * speed,
                            60, default, 2f);
                        d.noGravity = true;
                    }
                }

                NPC.netUpdate = true;
            }

            // 余波（170-210）
            if (PhaseTimer > IntroRiseEnd && PhaseTimer <= IntroEruptEnd) {
                float eruptFade = 1f - (PhaseTimer - IntroRiseEnd) / (float)(IntroEruptEnd - IntroRiseEnd);
                if (Main.netMode != NetmodeID.Server) {
                    int waveCount = (int)(5 * eruptFade);
                    for (int i = 0; i < waveCount; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = Main.rand.NextFloat(80, 350);
                        Vector2 ePos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        Dust d = Dust.NewDustDirect(ePos, 0, 0, DustID.GreenTorch,
                            0, -1f, 80, default, 1.5f * eruptFade);
                        d.noGravity = true;
                    }
                }
                Lighting.AddLight(NPC.Center, new Vector3(0.35f, 0.6f, 0.2f) * eruptFade);
            }

            // ========== 进入战斗（240）==========
            if (PhaseTimer >= IntroFinish) {
                NPC.dontTakeDamage = false;
                attackCycle = 0;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 0.9f }, NPC.Center);
                GoSurface();
            }
        }

        #endregion

        #region 潜地机制（核心节拍）

        private const int BurrowSinkDuration = 50;
        private const int BurrowUndergroundWait = 85;
        private const int BurrowRiseDuration = 45;
        private const int BurrowP2SinkDuration = 40;
        private const int BurrowP2UndergroundWait = 65;
        private const int BurrowP2RiseDuration = 38;

        /// <summary>场上毒孢区计数 (上限控制)。</summary>
        private static int CountSporeZones() {
            int type = ModContent.ProjectileType<DryadsSporeZone>();
            int n = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
                if (Main.projectile[i].active && Main.projectile[i].type == type)
                    n++;
            return n;
        }

        /// <summary>
        /// 潜地转移 — 树精核心机制 (V3)。
        ///  下沉(无敌, P2 旧锚点留毒孢区) → 地下(移动根瘤爬向目标 + ArenaRunic 预警圈)
        ///  → 冒出(挤压回弹 + 根须飞梭放射 + 刺球陷阱 + 径向泛光; P2 循环表开头种汲魂灵芽)。
        /// </summary>
        private void RunBurrow(Player target, bool isPhase2) {
            int sinkDur = isPhase2 ? BurrowP2SinkDuration : BurrowSinkDuration;
            int waitDur = isPhase2 ? BurrowP2UndergroundWait : BurrowUndergroundWait;
            int riseDur = isPhase2 ? BurrowP2RiseDuration : BurrowRiseDuration;
            int totalSinkAndWait = sinkDur + waitDur;
            int totalDuration = totalSinkAndWait + riseDur;

            float groundWorldY = GroundYOf(anchorPosition);

            // ========== 阶段1：下沉 ==========
            if (PhaseTimer <= sinkDur) {
                isBurrowing = true;
                float sinkT = PhaseTimer / (float)sinkDur;
                burrowProgress = sinkT * sinkT;

                if (PhaseTimer == 1) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.7f, Volume = 0.6f }, NPC.Center);
                    squashVel += 0.05f; // 下潜前压身

                    // P2 蔓生: 旧锚点留毒孢区 (~10s, 火烧/跳过; 同屏 ≤3)
                    if (isPhase2 && Main.netMode != NetmodeID.MultiplayerClient && CountSporeZones() < 3) {
                        Vector2 sporePos = new(anchorPosition.X, groundWorldY - 30f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), sporePos, Vector2.Zero,
                            ModContent.ProjectileType<DryadsSporeZone>(), NPC.damage / 6, 0f, Main.myPlayer);
                    }
                }

                if (Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 debrisPos = new(
                            anchorPosition.X + Main.rand.NextFloat(-200, 200),
                            groundWorldY + Main.rand.NextFloat(-10, 10));
                        Dust d = Dust.NewDustDirect(debrisPos, 0, 0, DustID.WoodFurniture,
                            Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-3f, -0.5f),
                            130, default, 1.5f);
                        d.noGravity = false;
                    }
                }

                if (PhaseTimer % 8 == 0)
                    ACMUtils.AddScreenShake(3f + sinkT * 2f);
            }

            // ========== 阶段2：地下 — 移动根瘤 + 冒出点预警 ==========
            if (PhaseTimer == sinkDur + 1) {
                burrowProgress = 1f;
                burrowTargetPos = FindGroundNearPlayer(target);
                NPC.netUpdate = true;
            }

            if (PhaseTimer > sinkDur && PhaseTimer <= totalSinkAndWait) {
                burrowProgress = 1f;

                int warningTimer = (int)PhaseTimer - sinkDur;
                float travelT = MathHelper.Clamp(warningTimer / (waitDur * 0.85f), 0f, 1f);
                float warnStart = waitDur * 0.22f;
                emergeTell = MathHelper.Clamp((warningTimer - warnStart) / (waitDur - warnStart), 0f, 1f);

                // —— 移动根瘤: 一条尘土隆起从旧锚点爬向冒出点 (行踪可读) ——
                if (Main.netMode != NetmodeID.Server && burrowTargetPos != Vector2.Zero) {
                    float knotX = MathHelper.Lerp(anchorPosition.X, burrowTargetPos.X,
                        travelT * travelT * (3f - 2f * travelT));
                    float knotGroundY = FindGroundYAt(knotX, MathF.Min(GroundYOf(anchorPosition), GroundYOf(burrowTargetPos)) - 60f);
                    if (warningTimer % 2 == 0) {
                        Dust d = Dust.NewDustDirect(new Vector2(knotX + Main.rand.NextFloat(-26f, 26f),
                            knotGroundY + Main.rand.NextFloat(-6f, 2f)), 0, 0, DustID.WoodFurniture,
                            Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2.5f, -0.8f), 130, default, 1.3f);
                        d.noGravity = false;
                    }
                    if (warningTimer % 5 == 0) {
                        Dust g = Dust.NewDustDirect(new Vector2(knotX, knotGroundY - 4f), 0, 0,
                            DustID.GreenTorch, 0, -1.2f, 90, default, 1.1f);
                        g.noGravity = true;
                    }
                }

                if (warningTimer > warnStart && Main.netMode != NetmodeID.Server) {
                    float targetGroundY = GroundYOf(burrowTargetPos);
                    int dustCount = (int)(4 * emergeTell) + 1;
                    for (int i = 0; i < dustCount; i++) {
                        // 末段转赤红 = 致命 (TelegraphColors.Lethal)
                        Vector2 warnPos = new(
                            burrowTargetPos.X + Main.rand.NextFloat(-180, 180),
                            targetGroundY + Main.rand.NextFloat(-8, 8));
                        int dustType = emergeTell > 0.7f ? DustID.RedTorch : DustID.GreenTorch;
                        Dust d = Dust.NewDustDirect(warnPos, 0, 0, dustType,
                            Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-3f, -0.5f),
                            80, default, 1.5f * emergeTell);
                        d.noGravity = true;
                    }

                    if (warningTimer % 6 == 0)
                        ACMUtils.AddScreenShake(2f * emergeTell);

                    Lighting.AddLight(new Vector2(burrowTargetPos.X, targetGroundY),
                        new Vector3(0.2f, 0.5f, 0.1f) * emergeTell);
                }
            }

            // ========== 阶段3：从新位置冒出 ==========
            if (PhaseTimer == totalSinkAndWait + 1) {
                anchorPosition = burrowTargetPos;
                emergeTell = 0f;
                NPC.netUpdate = true;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 0.9f }, anchorPosition);
            }

            if (PhaseTimer > totalSinkAndWait && PhaseTimer <= totalDuration) {
                float riseT = (PhaseTimer - totalSinkAndWait) / (float)riseDur;
                // back-out 过冲: 微超出地面再落回 (破土的momentum); 负值 = 短暂弹出地面上方
                float easedRise = 1f + 2.2f * MathF.Pow(riseT - 1f, 3f) + 1.2f * MathF.Pow(riseT - 1f, 2f);
                burrowProgress = MathHelper.Clamp(1f - easedRise, -0.05f, 1f);

                groundWorldY = GroundYOf(anchorPosition);

                if (Main.netMode != NetmodeID.Server && PhaseTimer % 3 == 0) {
                    for (int i = 0; i < 4; i++) {
                        Vector2 debrisPos = new(
                            anchorPosition.X + Main.rand.NextFloat(-250, 250),
                            groundWorldY + Main.rand.NextFloat(-15, 8));
                        Dust d = Dust.NewDustDirect(debrisPos, 0, 0, DustID.WoodFurniture,
                            Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-5f, -1f),
                            120, default, Main.rand.NextFloat(1.2f, 2.2f));
                        d.noGravity = false;
                    }
                }

                if (PhaseTimer % 6 == 0)
                    ACMUtils.AddScreenShake(2f + riseT * 3f);
            }

            // ========== 冒出完成: 根须飞梭放射 + 刺球陷阱 (+P2 灵芽) ==========
            if (PhaseTimer == totalDuration) {
                burrowProgress = 0f;
                isBurrowing = false;
                emergeFlash = 1f;
                squashVel = -0.14f; // 落定挤压回弹
                leanVel += 0.03f * MathF.Sign(target.Center.X - NPC.Center.X);

                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f, Volume = 0.9f }, NPC.Center);
                ACMUtils.AddScreenShake(9f);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    // 根须飞梭放射 (上半扇重力弧, 强制玩家拉开)
                    int vineCount = isPhase2 ? 9 : 6;
                    for (int i = 0; i < vineCount; i++) {
                        // 上半扇均布 (-160° ~ -20°)
                        float angle = MathHelper.ToRadians(MathHelper.Lerp(-160f, -20f, vineCount == 1 ? 0.5f : i / (float)(vineCount - 1)));
                        Vector2 vel = angle.ToRotationVector2() * (isPhase2 ? 10f : 8f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center - new Vector2(0, 80f), vel,
                            ModContent.ProjectileType<DryadsVine>(), NPC.damage / 4, 1f, Main.myPlayer);
                    }

                    // 刺球陷阱散布 (扎地 ~3s 后引爆, 空间封锁)
                    int trapCount = isPhase2 ? 6 : 4;
                    float trapGroundY = GroundYOf(anchorPosition);
                    for (int i = 0; i < trapCount; i++) {
                        float dx = ((float)i / (trapCount - 1) - 0.5f) * (isPhase2 ? 760f : 560f);
                        dx += Main.rand.NextFloat(-40f, 40f);
                        Vector2 spawn = new(anchorPosition.X + dx, trapGroundY - 220f);
                        Vector2 vel = new(dx * 0.012f, Main.rand.NextFloat(2f, 4f));
                        // ai0=1 陷阱模式 (经 NewProjectile 同步)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, vel,
                            ModContent.ProjectileType<Acanthosphere>(), NPC.damage / 4, 1f, Main.myPlayer, 1f);
                    }

                    // P2: 循环表回到开头的那次冒出 → 种一株汲魂灵芽 (全场 ≤1)
                    if (isPhase2 && attackCycle % CurrentTable.Length == 0 &&
                        !NPC.AnyNPCs(ModContent.NPCType<DryadsSiphonBud>())) {
                        float side = target.Center.X > NPC.Center.X ? -1f : 1f; // 种在玩家背侧
                        float budX = anchorPosition.X + side * 300f;
                        float budGroundY = FindGroundYAt(budX, trapGroundY - 60f);
                        // NewNPC 的 (X,Y) 为底部中心; 稍埋入土中, 由灵芽自身出土动画升起
                        NPC.NewNPC(NPC.GetSource_FromAI(), (int)budX, (int)(budGroundY + 14f),
                            ModContent.NPCType<DryadsSiphonBud>(), 0, NPC.whoAmI);
                    }
                }

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 20; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float speed = Main.rand.NextFloat(3f, 8f);
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GreenTorch,
                            MathF.Cos(angle) * speed, MathF.Sin(angle) * speed,
                            60, default, 2f);
                        d.noGravity = true;
                    }
                }

                NPC.netUpdate = true;
            }

            // 冒出后短暂恢复 → 进入地表攻击窗口 (循环表)
            if (PhaseTimer > totalDuration + 25)
                GoSurface();
        }

        #endregion

        #region 地表窗口 A — 根须爆发 RootBurst (行军波)

        /// <summary>
        /// 根须柱行军波: 每波 4-5 根柱从树精向玩家方向逐波推进 (每柱: 30f 光束预告 → 8f 破土速升 → 持续 → 凋落)。
        /// P2 追加反向第二列从玩家身后半波错相夹击。每柱在自己 X 下方采样真实地面 (修复坡地错位)。
        /// </summary>
        private void RunRootBurst(Player target) {
            const int Lead = 20;
            const int WaveGap = 44;
            int waveCount = IsPhase2 ? 4 : 3;

            // 开招捕获行军方向 (确定性, 同步)
            if (AttackTimer == 1) {
                SubState = MathF.Sign(target.Center.X - NPC.Center.X);
                if (SubState == 0) SubState = 1;
                SoundEngine.PlaySound(SoundID.Item64 with { Pitch = -0.4f, Volume = 0.6f }, NPC.Center);
                leanVel += 0.025f * SubState; // 前倾指向
                NPC.netUpdate = true;
            }

            float dir = SubState;

            for (int w = 0; w < waveCount; w++) {
                int waveTick = Lead + w * WaveGap;

                // —— 正面行军列: 树精 → 玩家方向逐波推进 ——
                if ((int)AttackTimer == waveTick && Main.netMode != NetmodeID.MultiplayerClient) {
                    int rootsPerWave = IsPhase2 ? 5 : 4;
                    float waveCenter = NPC.Center.X + dir * (280f + w * 170f);
                    SpawnPillarRow(target, waveCenter, rootsPerWave, w);
                }
                if ((int)AttackTimer == waveTick) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.5f, Volume = 0.6f }, target.Center);
                    squashVel -= 0.02f; // 每波蹬地反作用
                }

                // —— P2 背面夹击列 (半波错相): 从玩家身后闭合 ——
                if (IsPhase2 && (int)AttackTimer == waveTick + 22 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float rearCenter = target.Center.X - dir * (440f - w * 120f);
                    SpawnPillarRow(target, rearCenter, 3, w);
                }
            }

            int endTick = Lead + waveCount * WaveGap + 100;
            if (AttackTimer > endTick)
                GoBurrow();
        }

        /// <summary>在 waveCenter 附近横向铺一排根须柱 (各柱独立采样地面)。仅服务器调用。</summary>
        private void SpawnPillarRow(Player target, float waveCenter, int count, int waveIndex) {
            for (int i = 0; i < count; i++) {
                float frac = count == 1 ? 0.5f : (float)i / (count - 1);
                float rx = waveCenter + (frac - 0.5f) * (count - 1) * 95f + Main.rand.NextFloat(-18f, 18f);
                float groundY = FindGroundYAt(rx, target.Bottom.Y);
                Vector2 spawn = new(rx, groundY + 8f);
                float height = (i + waveIndex) % 2 == 0 ? 280f : 340f;
                // ai0=1 根须柱, ai2=高度 (经 NewProjectile 同步)
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, Vector2.Zero,
                    ModContent.ProjectileType<DryadsVine>(), NPC.damage / 4, 1.5f, Main.myPlayer, 1f, 0f, height);
            }
        }

        #endregion

        #region 地表窗口 B — 藤鞭横扫 VineLash (招牌)

        /// <summary>场上藤鞭计数 (模式 2/3, 上限 8)。</summary>
        private static int CountWhips() {
            int type = ModContent.ProjectileType<DryadsVine>();
            int n = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && ((int)p.ai[0] == 2 || (int)p.ai[0] == 3))
                    n++;
            }
            return n;
        }

        private static readonly int[] LashTicksP1 = { 16, 46 };
        private static readonly int[] LashTicksP2 = { 16, 42, 68 };

        /// <summary>
        /// 藤鞭横扫: 玩家两侧地面破土抽打 (P1 两鞭错拍, P2 三鞭)。
        /// 每鞭自带完整 anticipation→strike→recovery 波形与红导引线 (见 DryadsVine 模式 2)。
        /// </summary>
        private void RunVineLash(Player target) {
            if (AttackTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.6f, Volume = 0.7f }, NPC.Center);
                NPC.netUpdate = true;
            }

            // 错拍出鞭: 第 i 鞭在玩家"当时"位置两侧取根点 (追走位)
            int[] ticks = IsPhase2 ? LashTicksP2 : LashTicksP1;
            for (int i = 0; i < ticks.Length; i++) {
                if ((int)AttackTimer == ticks[i] && Main.netMode != NetmodeID.MultiplayerClient && CountWhips() < 8) {
                    float side = i % 2 == 0 ? 1f : -1f;
                    // 首鞭从玩家移动方向一侧来 (迎头截击)
                    if (MathF.Abs(target.velocity.X) > 0.5f)
                        side *= MathF.Sign(target.velocity.X);
                    float rootX = target.Center.X + side * Main.rand.NextFloat(200f, 390f);
                    float groundY = FindGroundYAt(rootX, target.Bottom.Y);
                    Vector2 root = new(rootX, groundY + 6f);
                    Vector2 aim = target.Center + target.velocity * 10f;
                    float angle = (aim - root).ToRotation();
                    // ai0=2 藤鞭, ai2=攻击角 (经 NewProjectile 同步)
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), root, Vector2.Zero,
                        ModContent.ProjectileType<DryadsVine>(), NPC.damage / 3, 2f, Main.myPlayer, 2f, 0f, angle);
                }
            }

            int endTick = (IsPhase2 ? 68 : 46) + 150;
            if (AttackTimer > endTick)
                GoBurrow();
        }

        #endregion

        #region 地表窗口 C — 刺球领域 SpikeField

        /// <summary>
        /// 向心收缩刺环 + 缓慢旋转的安全辐条 (单一清晰规则: 跟着安全缝走)。
        /// V3: 辐条旋转确定性 (消灭客户端随机); P2 双辐条对置; 每波环位尘圈闪现。
        /// </summary>
        private void RunSpikeField(Player target) {
            const int Lead = 26;
            const int WaveGap = 48;
            int waveCount = IsPhase2 ? 4 : 3;
            float gapHalf = MathHelper.ToRadians(IsPhase2 ? 22f : 32f); // 安全缝半角

            if (AttackTimer == 1) {
                fieldCenter = target.Center;
                fieldGapAngle = Main.rand.NextFloat(MathHelper.TwoPi); // 服务器随机, 经 ExtraAI 同步
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.3f, Volume = 0.7f }, NPC.Center);
                NPC.netUpdate = true;
            }

            // 安全辐条预告强度 (波前 ~18 tick 亮起)
            float nearestLeadDist = 9999f;
            for (int w = 0; w < waveCount; w++) {
                int waveTick = Lead + w * WaveGap;
                float dToWave = waveTick - AttackTimer;
                if (dToWave > 0f && dToWave < nearestLeadDist) nearestLeadDist = dToWave;
            }
            spokeTell = MathHelper.Clamp(1f - nearestLeadDist / 18f, 0f, 1f);

            // 旋转方向由循环指针奇偶决定 (确定性, 各端一致)
            float rotDir = attackCycle % 2 == 0 ? 1f : -1f;

            for (int w = 0; w < waveCount; w++) {
                int waveTick = Lead + w * WaveGap;
                if ((int)AttackTimer == waveTick) {
                    float ringRadius = MathHelper.Lerp(640f, 210f, waveCount == 1 ? 0f : (float)w / (waveCount - 1));

                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int spikeCount = 20;
                        float inwardSpeed = 2.0f + w * 0.35f;
                        for (int i = 0; i < spikeCount; i++) {
                            float angle = MathHelper.TwoPi / spikeCount * i;
                            // 留出安全辐条缺口; P2 双辐条 (对置)
                            float delta = MathHelper.WrapAngle(angle - fieldGapAngle);
                            if (MathF.Abs(delta) < gapHalf) continue;
                            if (IsPhase2) {
                                float delta2 = MathHelper.WrapAngle(angle - fieldGapAngle - MathHelper.Pi);
                                if (MathF.Abs(delta2) < gapHalf) continue;
                            }
                            Vector2 pos = fieldCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ringRadius;
                            Vector2 vel = (fieldCenter - pos).SafeNormalize(Vector2.Zero) * inwardSpeed;
                            // ai0=2 环刺模式 (无重力直线向心, 经 NewProjectile 同步)
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                                ModContent.ProjectileType<Acanthosphere>(), NPC.damage / 4, 0.5f, Main.myPlayer, 2f);
                        }
                    }

                    // 环位尘圈闪现 (读出"刺从哪里来", 客户端视觉)
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 26; i++) {
                            float a = MathHelper.TwoPi / 26 * i;
                            Vector2 p = fieldCenter + a.ToRotationVector2() * ringRadius;
                            Dust d = Dust.NewDustDirect(p, 0, 0, DustID.GreenTorch, 0, 0, 90, default, 1.4f);
                            d.noGravity = true;
                            d.velocity = (fieldCenter - p).SafeNormalize(Vector2.Zero) * 1.5f;
                        }
                    }

                    SoundEngine.PlaySound(SoundID.Item17 with { Pitch = 0f, Volume = 0.6f }, fieldCenter);
                    // 安全辐条确定性旋转 (下一波缺口转动, 各端一致)
                    fieldGapAngle += MathHelper.ToRadians(28f + w * 3f) * rotDir;
                }
            }

            int endTick = Lead + waveCount * WaveGap + 40;
            if (AttackTimer > endTick)
                GoBurrow();
        }

        #endregion

        #region 万藤缠狱 VinePrison (<25% set-piece)

        private const int PrisonWindup = 50;
        private const int PrisonCastEnd = 434;   // 最后一记鞭在 ~411f 甩出
        private const int PrisonWiltEnd = 540;   // 等末鞭余摆收完 (411+127≈538) 再入力竭
        private const int PrisonWhipInterval = 24;
        private const int PrisonWhipCount = 16;

        /// <summary>
        /// 万藤缠狱: 冠层 16 记放射藤鞭按 45° 步进顺甩 (第二圈错相 22.5°), 笼缘毒孢叶飘落。
        /// 应对法: 顺旋转方向"跟着钟摆走"。结束转入力竭破绽窗 (波形收束: 最大压迫→保底喘息)。
        /// </summary>
        private void RunVinePrison(Player target) {
            // —— 拔根蓄势 (0-50): 播报 + 尘埃向树冠收束 ——
            if (PhaseTimer == 1) {
                Announce("VinePrison", TelegraphColors.Lethal);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.85f, Volume = 1.2f }, NPC.Center);
                DryadsOvergrownSystem.Pulse(1f);

                // 清掉残留刺球/根须弹 (set-piece 开台留白; 毒孢区保留 — 慢性地形不干扰读招)
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int sphereType = ModContent.ProjectileType<Acanthosphere>();
                    int vineType = ModContent.ProjectileType<DryadsVine>();
                    for (int i = 0; i < Main.maxProjectiles; i++) {
                        Projectile p = Main.projectile[i];
                        if (p.active && (p.type == sphereType || p.type == vineType))
                            p.Kill();
                    }
                }
                NPC.netUpdate = true;
            }

            Vector2 crown = NPC.Center - new Vector2(0f, 150f);

            if (PhaseTimer <= PrisonWindup) {
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 4; i++) {
                        Vector2 from = crown + Main.rand.NextVector2CircularEdge(360f, 360f);
                        Dust d = Dust.NewDustDirect(from, 0, 0, DustID.GreenTorch, 0, 0, 80, default, 1.7f);
                        d.noGravity = true;
                        d.velocity = (crown - from).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(5f, 9f);
                    }
                }
                if (PhaseTimer % 10 == 0)
                    ACMUtils.AddScreenShake(1.5f + PhaseTimer / PrisonWindup * 2.5f);
                return;
            }

            // —— 放射鞭阵 (50-434): 每 24f 一记, 45° 步进, 第二圈错相 22.5° ——
            int castT = (int)PhaseTimer - PrisonWindup - 1; // 首记在 PhaseTimer=51 (castT=0)
            if (castT >= 0 && castT % PrisonWhipInterval == 0 && castT / PrisonWhipInterval < PrisonWhipCount) {
                int k = castT / PrisonWhipInterval;
                float baseAngle = SubState; // 开阵基准角 (首记时捕获)
                if (k == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    SubState = (target.Center - crown).ToRotation(); // 首鞭指向玩家 (读得懂的起点)
                    baseAngle = SubState;
                    NPC.netUpdate = true;
                }
                float angle = baseAngle + MathHelper.ToRadians(45f * k + (k >= 8 ? 22.5f : 0f));

                if (Main.netMode != NetmodeID.MultiplayerClient && CountWhips() < 8) {
                    // ai0=3 冠层放射鞭 (根点=树冠)
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), crown, Vector2.Zero,
                        ModContent.ProjectileType<DryadsVine>(), NPC.damage / 3, 2f, Main.myPlayer, 3f, 0f, angle);
                }
                if (Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(SoundID.Item64 with { Pitch = 0.1f, Volume = 0.45f }, crown);
                squashVel -= 0.015f; // 每记出鞭的后坐
            }

            // 笼缘毒孢叶飘落 (慢性空间压力)
            if (castT >= 0 && PhaseTimer < PrisonCastEnd && castT % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 spawn = NPC.Center + new Vector2(s * 620f + Main.rand.NextFloat(-60f, 60f), -520f);
                    Vector2 vel = new(Main.rand.NextFloat(-0.6f, 0.6f) - s * 0.8f, Main.rand.NextFloat(1.5f, 2.4f));
                    // ai1=1 毒孢叶变体
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, vel,
                        ModContent.ProjectileType<DryadsLeaf>(), NPC.damage / 5, 0.5f, Main.myPlayer, 0f, 1f);
                }
            }

            // —— 凋垂 (434-460) → 力竭 ——
            if (PhaseTimer >= PrisonWiltEnd)
                TransitionTo(BossPhase.Exhausted);
        }

        /// <summary>力竭破绽窗 (100f): 树身前倾瘫软, 受伤 ×1.25, 无任何攻击。</summary>
        private void RunExhausted(Player target) {
            NPC.dontTakeDamage = false;

            if (PhaseTimer == 1) {
                Announce("Exhausted", TelegraphColors.Safe);
                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = 0.2f, Volume = 0.7f }, NPC.Center);
                squashVel += 0.09f; // 瘫软下沉
                NPC.netUpdate = true;
            }

            // 破绽金绿光尘标记 (安全色 — 打这里)
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                Vector2 p = NPC.Center + Main.rand.NextVector2Circular(140f, 190f);
                Dust d = Dust.NewDustDirect(p, 0, 0, DustID.GreenFairy, 0, -0.8f, 100, default, 1.5f);
                d.noGravity = true;
            }

            if (PhaseTimer >= 100)
                GoBurrow();
        }

        #endregion

        #region 阶段转换 — P2 蔓生 (150f)

        private static readonly int[] TransitionHeartbeats = { 30, 58, 80 };

        private void RunPhaseTransition2(Player target) {
            NPC.dontTakeDamage = true;

            // 开场清弹 (公平阀: 换阶段留白)
            if (PhaseTimer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.hostile && p.damage > 0)
                        p.Kill();
                }
            }

            // 尘埃收束 (能量向躯干汇聚)
            if (Main.netMode != NetmodeID.Server && PhaseTimer < 88) {
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi / 6 * i + globalTime * 3f;
                    float dist = MathF.Max(300f - PhaseTimer * 2.6f, 60f);
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * dist;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GreenTorch, 0, 0, 80, default, 2.2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 6f;
                }
            }

            // 三记心跳 (间隔递减 → 加速感)
            for (int i = 0; i < TransitionHeartbeats.Length; i++) {
                if ((int)PhaseTimer == TransitionHeartbeats[i]) {
                    squashVel -= 0.07f + i * 0.02f;
                    DryadsOvergrownSystem.Pulse(0.5f + i * 0.25f);
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.9f, Volume = 0.55f + i * 0.15f }, NPC.Center);
                        ACMUtils.AddScreenShake(3f + i * 2f);
                    }
                }
            }

            // 绽放帧 (90)
            if (PhaseTimer == 90) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 1.3f }, NPC.Center);
                emergeFlash = 1f;
                squashVel = -0.16f;
                Announce("Overgrown", VerdantGreen);
                DryadsOvergrownSystem.Pulse(1f);
                ACMUtils.AddScreenShake(11f);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 12; i++) {
                        float angle = MathHelper.TwoPi / 12 * i;
                        Vector2 vel = angle.ToRotationVector2() * 8f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<DryadsLeaf>(), NPC.damage / 3, 1f, Main.myPlayer);
                    }
                }
            }

            // 灵叶飘落收势 (90-150) → 潜地 (开始留毒孢)
            if (PhaseTimer >= 150) {
                NPC.dontTakeDamage = false;
                NPC.defense += 15;
                NPC.damage = (int)(NPC.damage * 1.2f);
                attackCycle = 0;
                GoBurrow();
            }
        }

        #endregion

        #region 死亡演出 (~330f)

        private const int DeathWiltEnd = 60;
        private static readonly int[] DeathHeartbeats = { 60, 100, 134, 160, 180 };
        private const int DeathGatherStart = 200;
        private const int DeathSilenceStart = 232;
        private const int DeathBurst = 250;
        private const int DeathFadeEnd = 320;
        private const int DeathFinish = 322;

        /// <summary>
        /// 归土: 凋萎 → 心跳加速龟裂 → 收束吸气 (末 18f 全粒子静默) → 爆裂 (全场唯一 shake16 时刻)
        /// → 躯干溶解、绿魂光尘升天 → 真死。
        /// </summary>
        private void RunDeath(Player target) {
            NPC.dontTakeDamage = true;
            NPC.damage = 0;

            // 低吟启幕 (放在 AI 内: 多人时客户端也各自播放)
            if (PhaseTimer == 1 && Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.8f, Volume = 1.1f }, NPC.Center);

            // —— 凋萎推进 (贴图向枯褐插值 + 屏幕蔓延退潮) ——
            deathWither = MathHelper.Clamp(PhaseTimer / DeathBurst, 0f, 1f);

            // —— 心跳加速 (60/100/134/160/180): 闷响 + 挤压脉冲 + 裂纹闪 ——
            for (int i = 0; i < DeathHeartbeats.Length; i++) {
                if ((int)PhaseTimer == DeathHeartbeats[i]) {
                    squashVel -= 0.06f + i * 0.02f;
                    crackFlash = 0.5f + i * 0.13f;
                    DryadsOvergrownSystem.Pulse(0.4f + i * 0.15f);
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.95f, Volume = 0.5f + i * 0.12f }, NPC.Center);
                        ACMUtils.AddScreenShake(3f + i * 0.8f);
                    }
                }
            }

            // —— 收束吸气 (200-250): 光尘向躯干拉拽; 末 18f 静默 (爆前留白) ——
            if (PhaseTimer > DeathGatherStart && PhaseTimer < DeathSilenceStart &&
                Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 3; i++) {
                    Vector2 from = NPC.Center + Main.rand.NextVector2CircularEdge(320f, 320f);
                    Dust d = Dust.NewDustDirect(from, 0, 0, DustID.GreenFairy, 0, 0, 60, default, 1.6f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - from).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(6f, 10f);
                }
            }

            // —— 爆裂帧 (250): 全场唯一大时刻 ——
            if (PhaseTimer == DeathBurst) {
                emergeFlash = 1f;
                squashVel = -0.2f;
                SoundEngine.PlaySound(SoundID.NPCDeath30 with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f, Volume = 1f }, NPC.Center);
                ACMUtils.AddScreenShake(16f);

                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier punch = new(NPC.Center,
                        (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(),
                        14f, 8f, 45, 2500f, FullName);
                    Main.instance.CameraModifiers.Add(punch);

                    // 木屑喷泉 + 升天绿魂
                    for (int i = 0; i < 55; i++) {
                        Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.WoodFurniture,
                            Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-12f, -3f), 100, default,
                            Main.rand.NextFloat(1.6f, 2.8f));
                        d.noGravity = false;
                    }
                    for (int i = 0; i < 35; i++) {
                        Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GreenFairy,
                            Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-7f, -2f), 40, default, 1.8f);
                        d.noGravity = true;
                    }
                }
            }

            // —— 溶解升天 (250-320): 绿魂持续上升 (本体溶解由 PreDraw DissolveBurn 承担) ——
            if (PhaseTimer > DeathBurst && PhaseTimer < DeathFadeEnd &&
                Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                Vector2 p = NPC.Center + Main.rand.NextVector2Circular(200f, 240f);
                Dust d = Dust.NewDustDirect(p, 0, 0, DustID.GreenFairy, 0, Main.rand.NextFloat(-4f, -1.5f),
                    60, default, 1.5f);
                d.noGravity = true;
            }

            // —— 真死 (322): 掉落/downed 照常 (服务器权威, 客户端经同步收到死亡) ——
            if (PhaseTimer >= DeathFinish && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.life = 0;
                NPC.HitEffect();
                NPC.checkDead();
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // —— 演出层 (server-zero-draw 已由助手内部 Main.dedServ 守卫) ——
            DrawEmergeTell(spriteBatch);
            DrawSpikeSafeSpoke(spriteBatch);
            DrawEmergeBloom(spriteBatch);

            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = new(0, 0, texture.Width, texture.Height);

            // —— 基点变换: 绕树根着地点做倾斜 + 保体积挤压 + 呼吸 ——
            Vector2 rootPivot = NPC.Center + new Vector2(0f, TextureHeight / 2f - RootBuryOffset);
            Vector2 origin = new(TextureWidth / 2f, TextureHeight - RootBuryOffset);
            Vector2 drawPos = rootPivot - screenPos;

            float breath = 1f + MathF.Sin(globalTime * MathHelper.Pi) * 0.006f;
            Vector2 scaleVec = new Vector2(
                NPC.scale * (1f + squash * 0.55f),
                NPC.scale * (1f - squash) * breath);
            float rotation = lean;

            // 死亡凋萎: 色调向枯褐插值
            Color bodyColor = drawColor;
            if (deathWither > 0f) {
                Color withered = new(
                    (byte)(drawColor.R * 0.72f + 45), (byte)(drawColor.G * 0.55f + 25),
                    (byte)(drawColor.B * 0.4f + 12), drawColor.A);
                bodyColor = Color.Lerp(drawColor, withered, deathWither * 0.85f);
            }

            // P2 灵光辉膜
            float glowPulse = 0f;
            if (IsPhase2 && Phase != BossPhase.Death)
                glowPulse = 0.12f + MathF.Sin(globalTime * 3.5f) * 0.08f;
            if (glowPulse > 0f) {
                Color greenGlow = new Color(100, 255, 80, 0) * glowPulse;
                spriteBatch.Draw(texture, drawPos, frame, greenGlow, rotation, origin,
                    scaleVec * 1.04f, SpriteEffects.None, 0f);
            }

            // —— 本体: 死亡末段走 DissolveBurn 溶解, 其余常规绘制 ——
            if (Phase == BossPhase.Death && PhaseTimer > DeathBurst) {
                DrawDeathDissolve(spriteBatch, texture, frame, drawPos, rotation, origin, scaleVec, bodyColor);
            }
            else {
                spriteBatch.Draw(texture, drawPos, frame, bodyColor, rotation, origin,
                    scaleVec, SpriteEffects.None, 0f);
            }

            // 死亡心跳裂纹闪 (SlashBurst 加性裂片)
            if (crackFlash > 0.02f)
                DrawCrackFlash(spriteBatch, screenPos);

            return false;
        }

        /// <summary>死亡溶解: DissolveBurn 噪声 clip + 绿灼边 (250-320f, uThreshold 0→1)。</summary>
        private void DrawDeathDissolve(SpriteBatch sb, Texture2D texture, Rectangle frame,
            Vector2 drawPos, float rotation, Vector2 origin, Vector2 scaleVec, Color bodyColor) {
            Effect fx = ACMShaders.DissolveBurn;
            if (fx == null) {
                // 着色器缺失兜底: 直接透明淡出
                float fade = 1f - MathHelper.Clamp((PhaseTimer - DeathBurst) / (float)(DeathFadeEnd - DeathBurst), 0f, 1f);
                sb.Draw(texture, drawPos, frame, bodyColor * fade, rotation, origin, scaleVec, SpriteEffects.None, 0f);
                return;
            }

            float threshold = MathHelper.Clamp((PhaseTimer - DeathBurst) / (float)(DeathFadeEnd - DeathBurst), 0f, 0.999f);

            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uIntensity"]?.SetValue(1f);
            fx.Parameters["uThreshold"]?.SetValue(threshold);
            fx.Parameters["uEdgeWidth"]?.SetValue(0.09f);
            fx.Parameters["uNoiseScale"]?.SetValue(2.6f);
            fx.Parameters["uEdgeColor"]?.SetValue(new Vector4(0.5f, 1f, 0.35f, 0.9f));
            fx.Parameters["uDirection"]?.SetValue(new Vector2(0f, -1f)); // 自下而上归土
            fx.Parameters["uSweepStrength"]?.SetValue(0.55f);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = ACMShaders.NoiseTexture;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(texture, drawPos, frame, bodyColor, rotation, origin, scaleVec, SpriteEffects.None, 0f);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>死亡心跳裂纹: 躯干上的 SlashBurst 加性亮绿裂片 (随 crackFlash 衰减)。</summary>
        private void DrawCrackFlash(SpriteBatch sb, Vector2 screenPos) {
            Texture2D crack = ACMAsset.SlashBurst;
            if (crack == null)
                return;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 o = crack.Size() / 2f;
            for (int i = 0; i < 3; i++) {
                float rot = i * 2.09f + lean; // 三片裂纹绕躯干分布
                Vector2 at = NPC.Center - screenPos + new Vector2(0, -40f + i * 46f);
                Color c = new Color(140, 255, 90, 0) * (crackFlash * (0.55f - i * 0.12f));
                sb.Draw(crack, at, null, c, rot, o, 0.32f + crackFlash * 0.1f, SpriteEffects.None, 0f);
            }

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>冒出点预警: ArenaRunic 绿色根须裂纹圈, 末段转赤红 (致命)。玩家须走开。</summary>
        private void DrawEmergeTell(SpriteBatch sb) {
            if (Main.dedServ || emergeTell <= 0.01f || burrowTargetPos == Vector2.Zero)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            Vector2 markCenter = new(burrowTargetPos.X, GroundYOf(burrowTargetPos));
            float worldRadius = MathHelper.Lerp(150f, 220f, emergeTell);
            ACMShaders.WorldDecalParams(markCenter, worldRadius, out Vector2 uv, out float radUV, out float aspect);

            // 绿 → 赤红 (emergeTell>0.7 后转致命)
            Color primary = Color.Lerp(VerdantGreen, TelegraphColors.Lethal, MathHelper.Clamp((emergeTell - 0.7f) / 0.3f, 0f, 1f));
            Color secondary = Color.Lerp(new Color(30, 90, 20), new Color(150, 20, 20), MathHelper.Clamp((emergeTell - 0.7f) / 0.3f, 0f, 1f));

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(emergeTell, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(9f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.NonPremultiplied);
        }

        /// <summary>刺球领域安全辐条预告: 沿安全角画翠玉光束 (TelegraphColors.Safe), 指明可站位。</summary>
        private void DrawSpikeSafeSpoke(SpriteBatch sb) {
            if (Main.dedServ || spokeTell <= 0.01f || fieldCenter == Vector2.Zero)
                return;
            Vector2 dir = fieldGapAngle.ToRotationVector2();
            Vector2 end = fieldCenter + dir * 700f;
            Vector2 start = fieldCenter - dir * 700f; // 双向贯穿: 安全辐条整条都安全
            ACMShaders.DrawBeam(start, end, 26f * spokeTell, TelegraphColors.Safe,
                new Color(120, 255, 170), spokeTell * 0.8f, flowSpeed: 0.8f, flowScale: 1.4f);
            // P2 双辐条: 垂直方向补第二条 (对置缺口在着弹逻辑中同角度)
            if (IsPhase2) {
                // 对置辐条与主辐条同线 (缺口在 fieldGapAngle 与 fieldGapAngle+π), 已由双向贯穿覆盖
            }
        }

        /// <summary>冒出/相变/爆裂瞬间: 绿色径向泛光闪 (DrawRadialBloomAt, 内部仲裁全屏名额)。</summary>
        private void DrawEmergeBloom(SpriteBatch sb) {
            if (Main.dedServ || emergeFlash <= 0.02f)
                return;
            ACMShaders.DrawRadialBloomAt(NPC.Center, 0.20f, emergeFlash * 0.85f, VerdantGreen, rayCount: 12f, falloff: 2.2f);
        }

        #endregion
    }
}
