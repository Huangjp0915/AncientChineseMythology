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
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dryades
{
    /// <summary>
    /// 树精 — 月后初期·潜地伏击树妖 (V2 burrow-centric rework)。
    ///
    /// 身份差异化 (vs 大椿 静态古树): 树精是**机动潜地伏击者**。约 50% 战斗在地下,
    /// 地表窗口只有两招——根须爆发 RootBurst 与 刺球领域 SpikeField; 其余时间潜地, 在玩家脚边冒出。
    ///
    /// 核心节拍循环 (确定性, 非随机喷弹):
    ///   [地表攻击] RootBurst / SpikeField 交替 → [潜地] 下沉(无敌) → 地下(冒出点地纹预警, 玩家须走开) → 冒出(根须放射 + 刺球陷阱) → 循环
    ///
    /// 招牌机制:
    ///  - **冒出点预警**: 地下等待期在目标点画 ArenaRunic 绿色根须裂纹圈, 末段转赤红 = 此处即将致命冒出。
    ///  - **刺球陷阱**: 冒出后散布 Acanthosphere 陷阱, 扎地 ~3s 后引爆 (空间封锁, 非直接投掷)。
    ///  - **刺球领域**: 向心收缩刺环 + 一条缓慢旋转的安全辐条 (一条清晰规则: 跟着安全缝走)。
    ///  - **P2 蔓生 Overgrown**: 每次潜地在**旧锚点**留下毒孢区 ~10s (火把/火系武器可烧除, 或直接跳过)——真·新机制, 非"P1 更快"。
    ///
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

        // 主题色
        private static readonly Color VerdantGreen = new(90, 210, 70);
        private static readonly Color SporePoison = new(120, 200, 60);

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,
            Surface_RootBurst,   // 地表窗口 A: 玩家脚边地面根须爆发 (逐点预告 → 喷涌)
            Surface_SpikeField,  // 地表窗口 B: 向心收缩刺环 + 旋转安全辐条
            Burrow,              // 潜地转移: 下沉 → 地下(冒出点预警) → 冒出(根须放射 + 刺球陷阱)
            PhaseTransition_2,
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

        private float globalTime;
        private bool didPhase2Transition;
        private Vector2 anchorPosition;     // 当前固定位置（树精站桩点）
        private float introRiseOffset;      // 入场上升偏移
        private bool isRising;              // 是否正在上升
        private bool isBurrowing;           // 是否正在潜入/冒出中
        private float burrowProgress;       // 潜地进度 0=完全在地面, 1=完全在地下
        private int attackCycle;            // 地表窗口交替计数 (RootBurst ↔ SpikeField)
        private Vector2 burrowTargetPos;    // 潜地后的目标冒出位置（锚点空间）

        // —— V2 演出/机制状态 ——
        private float emergeTell;           // 冒出点地纹预警强度 0~1 (地下等待期 ramp)
        private float emergeFlash;          // 冒出瞬间径向泛光闪 0~1 (衰减)
        private Vector2 fieldCenter;        // 刺球领域收缩中心 (开招时捕获玩家位置)
        private float fieldGapAngle;        // 安全辐条角度 (逐波缓慢旋转)
        private float spokeTell;            // 安全辐条预告强度 0~1

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

        public override void DrawBehind(int index) {
            if (isRising || isBurrowing)
                Main.instance.DrawCacheProjsBehindNPCsAndTiles.Add(index);
        }

        public override void HitEffect(NPC.HitInfo hit) {
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

            if (NPC.life <= 0) {
                for (int i = 0; i < 50; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                        DustID.WoodFurniture, 0, 0, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity *= 5f;
                }
                for (int i = 0; i < 30; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                        DustID.GrassBlades, 0, 0, 80, default, 2f);
                    d.noGravity = true;
                    d.velocity *= 4f;
                }
            }
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

            // 视觉计时衰减 (各客户端本地)
            if (emergeFlash > 0f) emergeFlash -= 0.04f;
            if (Phase != BossPhase.Burrow) emergeTell = MathHelper.Lerp(emergeTell, 0f, 0.2f);
            if (Phase != BossPhase.Surface_SpikeField) spokeTell = MathHelper.Lerp(spokeTell, 0f, 0.2f);

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

            // 潜地中不受攻击
            NPC.dontTakeDamage = isRising || burrowProgress > 0.5f;

            CheckPhaseTransition();
            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.Surface_RootBurst: RunRootBurst(target); break;
                case BossPhase.Surface_SpikeField: RunSpikeField(target); break;
                case BossPhase.Burrow: RunBurrow(target, IsPhase2); break;
                case BossPhase.PhaseTransition_2: RunPhaseTransition2(target); break;
            }

            // 自然光辉
            float glow = 0.4f + MathF.Sin(globalTime * 2f) * 0.15f;
            Lighting.AddLight(NPC.Center, new Vector3(0.2f, 0.45f, 0.1f) * glow);

            // 持续落叶粒子
            if (Main.netMode != NetmodeID.Server && !isBurrowing && Main.rand.NextBool(4)) {
                Vector2 leafPos = NPC.Center + new Vector2(
                    Main.rand.NextFloat(-TextureWidth / 2, TextureWidth / 2),
                    Main.rand.NextFloat(-TextureHeight / 2, TextureHeight / 4));
                Dust d = Dust.NewDustDirect(leafPos, 0, 0, DustID.GrassBlades,
                    Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(1f, 3f), 100, default, 1.3f);
                d.noGravity = false;
                d.fadeIn = 1.1f;
            }
        }

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 &&
                Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Intro &&
                !isBurrowing) {
                TransitionTo(BossPhase.PhaseTransition_2);
                didPhase2Transition = true;
            }
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

        /// <summary>潜地冒出后 → 交替进入两个地表窗口 (确定性轮替, 非随机)。</summary>
        private void GoSurface() {
            attackCycle++;
            TransitionTo((attackCycle % 2 == 0) ? BossPhase.Surface_RootBurst : BossPhase.Surface_SpikeField);
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
                TransitionTo(BossPhase.Surface_RootBurst);
            }
        }

        #endregion

        #region 潜地机制（核心特色）

        // 更频繁、更快的潜地循环 (V2: burrow-centric, 增加潜地频率)
        private const int BurrowSinkDuration = 50;
        private const int BurrowUndergroundWait = 85;
        private const int BurrowRiseDuration = 45;
        private const int BurrowP2SinkDuration = 40;
        private const int BurrowP2UndergroundWait = 65;
        private const int BurrowP2RiseDuration = 38;

        /// <summary>
        /// 潜地转移 — 树精核心机制 (V2)。
        ///  下沉(无敌) → 地下(冒出点 ArenaRunic 预警, 玩家须走开) → 冒出(根须放射 + 刺球陷阱 + 径向泛光)。
        ///  P2: 下沉时在**旧锚点**留下毒孢区 (DryadsSporeZone)。
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

                    // P2 蔓生: 旧锚点留毒孢区 (~10s, 火烧/跳过)
                    if (isPhase2 && Main.netMode != NetmodeID.MultiplayerClient) {
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

            // ========== 阶段2：地下（不可见）+ 冒出点预警 ==========
            if (PhaseTimer == sinkDur + 1) {
                burrowProgress = 1f;
                burrowTargetPos = FindGroundNearPlayer(target);
                NPC.netUpdate = true;
            }

            if (PhaseTimer > sinkDur && PhaseTimer <= totalSinkAndWait) {
                burrowProgress = 1f;

                int warningTimer = (int)PhaseTimer - sinkDur;
                float warnStart = waitDur * 0.22f;
                // 冒出点地纹预警强度: 渐显 (供 PreDraw 的 ArenaRunic 圈)
                emergeTell = MathHelper.Clamp((warningTimer - warnStart) / (waitDur - warnStart), 0f, 1f);

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
                float easedRise = 1f - (1f - riseT) * (1f - riseT);
                burrowProgress = 1f - easedRise;

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

            // ========== 冒出完成: 根须放射 + 刺球陷阱 ==========
            if (PhaseTimer == totalDuration) {
                burrowProgress = 0f;
                isBurrowing = false;
                emergeFlash = 1f;

                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f, Volume = 0.9f }, NPC.Center);
                ACMUtils.AddScreenShake(9f);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    // 根须放射 (直线弹, 强制玩家拉开)
                    int vineCount = isPhase2 ? 8 : 5;
                    for (int i = 0; i < vineCount; i++) {
                        float angle = MathHelper.TwoPi / vineCount * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (isPhase2 ? 7f : 5f);
                        vel.Y -= 3f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
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

            // 冒出后短暂恢复 → 进入地表攻击窗口 (交替)
            if (PhaseTimer > totalDuration + 25)
                GoSurface();
        }

        #endregion

        #region 地表窗口 A — 根须爆发 RootBurst

        /// <summary>
        /// 玩家脚边地面逐点喷涌根须: 每根 DryadsVine(root 模式) 自带 30 tick 地纹/光束预告 → 喷涌。
        /// 多波递进, 强制玩家持续走位 (telegraph 清晰, 红色只在喷涌瞬间)。
        /// </summary>
        private void RunRootBurst(Player target) {
            const int Lead = 12;
            const int WaveGap = 42;
            int waveCount = IsPhase2 ? 4 : 3;

            // 前摇
            if (AttackTimer == 1)
                SoundEngine.PlaySound(SoundID.Item64 with { Pitch = -0.4f, Volume = 0.6f }, NPC.Center);

            for (int w = 0; w < waveCount; w++) {
                int waveTick = Lead + w * WaveGap;
                if ((int)AttackTimer == waveTick && Main.netMode != NetmodeID.MultiplayerClient) {
                    int rootsPerWave = IsPhase2 ? 5 : 4;
                    float groundY = GroundYOf(anchorPosition);
                    // 围绕玩家在地表撒点 (玩家附近, 偏当前位置)
                    float spread = 520f;
                    for (int i = 0; i < rootsPerWave; i++) {
                        float frac = rootsPerWave == 1 ? 0.5f : (float)i / (rootsPerWave - 1);
                        float rx = target.Center.X + (frac - 0.5f) * spread + Main.rand.NextFloat(-30f, 30f);
                        // 根须在玩家所在地表线喷涌 (用玩家脚下地表近似 boss 地表线)
                        Vector2 spawn = new(rx, groundY + 24f);
                        // ai0=1 根须喷涌(telegraph)模式 (经 NewProjectile 同步)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, Vector2.Zero,
                            ModContent.ProjectileType<DryadsVine>(), NPC.damage / 4, 1.5f, Main.myPlayer, 1f);
                    }
                    SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.5f, Volume = 0.6f }, target.Center);
                }
            }

            int endTick = Lead + waveCount * WaveGap + 30;
            if (AttackTimer > endTick)
                GoBurrow();
        }

        #endregion

        #region 地表窗口 B — 刺球领域 SpikeField

        /// <summary>
        /// 向心收缩刺环 + 一条缓慢旋转的安全辐条 (一条清晰规则)。
        /// 开招捕获中心; 每波在收缩半径上铺满刺球, 留出安全辐条角度的缺口, 缺口逐波旋转。
        /// 安全辐条由 PreDraw 画翠玉光束预告 (TelegraphColors.Safe)。
        /// </summary>
        private void RunSpikeField(Player target) {
            const int Lead = 26;
            const int WaveGap = 48;
            int waveCount = IsPhase2 ? 4 : 3;
            float gapHalf = MathHelper.ToRadians(IsPhase2 ? 26f : 32f); // 安全缝半角

            if (AttackTimer == 1) {
                fieldCenter = target.Center;
                fieldGapAngle = Main.rand.NextFloat(MathHelper.TwoPi);
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

            for (int w = 0; w < waveCount; w++) {
                int waveTick = Lead + w * WaveGap;
                if ((int)AttackTimer == waveTick) {
                    float ringRadius = MathHelper.Lerp(640f, 210f, waveCount == 1 ? 0f : (float)w / (waveCount - 1));

                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int spikeCount = 20;
                        float inwardSpeed = 2.0f + w * 0.35f;
                        for (int i = 0; i < spikeCount; i++) {
                            float angle = MathHelper.TwoPi / spikeCount * i;
                            // 留出安全辐条缺口 (缺口角度逐波已旋转)
                            float delta = MathHelper.WrapAngle(angle - fieldGapAngle);
                            if (MathF.Abs(delta) < gapHalf) continue;
                            Vector2 pos = fieldCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ringRadius;
                            Vector2 vel = (fieldCenter - pos).SafeNormalize(Vector2.Zero) * inwardSpeed;
                            // ai0=2 环刺模式 (无重力直线向心, 经 NewProjectile 同步)
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                                ModContent.ProjectileType<Acanthosphere>(), NPC.damage / 4, 0.5f, Main.myPlayer, 2f);
                        }
                    }

                    SoundEngine.PlaySound(SoundID.Item17 with { Pitch = 0f, Volume = 0.6f }, fieldCenter);
                    // 安全辐条缓慢旋转 (下一波缺口转动)
                    fieldGapAngle += MathHelper.ToRadians(IsPhase2 ? 34f : 28f) * (Main.rand.NextBool() ? 1f : -1f);
                    if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
                }
            }

            int endTick = Lead + waveCount * WaveGap + 40;
            if (AttackTimer > endTick)
                GoBurrow();
        }

        #endregion

        #region 阶段转换

        private void RunPhaseTransition2(Player target) {
            NPC.dontTakeDamage = true;

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi / 8 * i + globalTime * 3f;
                    float dist = 300 - PhaseTimer * 2f;
                    if (dist < 60) dist = 60;
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GreenTorch,
                        0, 0, 80, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 6f;
                }
                for (int i = 0; i < 4; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(200, 200);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.JungleGrass,
                        0, 0, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                }
            }

            if (PhaseTimer == 50) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 1.3f }, NPC.Center);
                emergeFlash = 1f;
                ACMUtils.AddScreenShake(11f);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 12; i++) {
                        float angle = MathHelper.TwoPi / 12 * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<DryadsLeaf>(), NPC.damage / 3, 1f, Main.myPlayer);
                    }
                }
            }

            if (PhaseTimer >= 80) {
                NPC.dontTakeDamage = false;
                NPC.defense += 15;
                NPC.damage = (int)(NPC.damage * 1.2f);
                attackCycle = 0;
                // 进入 P2: 直接潜地 (开始留毒孢区)
                GoBurrow();
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // —— V2 演出层 (server-zero-draw 已由助手内部 Main.dedServ 守卫) ——
            DrawEmergeTell(spriteBatch);
            DrawSpikeSafeSpoke(spriteBatch);
            DrawEmergeBloom(spriteBatch);

            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 origin = new Vector2(TextureWidth / 2f, TextureHeight / 2f);
            Vector2 drawPos = NPC.Center - screenPos;

            float glowPulse = 0f;
            if (IsPhase2) {
                glowPulse = 0.12f + MathF.Sin(globalTime * 3.5f) * 0.08f;
            }

            if (glowPulse > 0) {
                Color greenGlow = new Color(100, 255, 80, 0) * glowPulse;
                spriteBatch.Draw(texture, drawPos, frame, greenGlow, 0f, origin,
                    NPC.scale * 1.04f, SpriteEffects.None, 0f);
            }

            spriteBatch.Draw(texture, drawPos, frame, drawColor, 0f, origin,
                NPC.scale, SpriteEffects.None, 0f);

            return false;
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
        }

        /// <summary>冒出/相变瞬间: 绿色径向泛光闪 (DrawRadialBloomAt, 内部仲裁全屏名额)。</summary>
        private void DrawEmergeBloom(SpriteBatch sb) {
            if (Main.dedServ || emergeFlash <= 0.02f)
                return;
            ACMShaders.DrawRadialBloomAt(NPC.Center, 0.20f, emergeFlash * 0.85f, VerdantGreen, rayCount: 12f, falloff: 2.2f);
        }

        #endregion
    }
}
