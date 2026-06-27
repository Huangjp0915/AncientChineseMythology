using AncientChineseMythology.Celestias.Boss.Dazhengs.Items;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework;
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

namespace AncientChineseMythology.Celestias.Boss.Dazhengs
{
    /// <summary>
    /// 大椿 — 上古树神 (固定不动)，月球领主后超级 Boss。
    ///
    /// V2「四季轮转 Cycle of Seasons」重做：
    ///  ● 保留出色的「破土升起」入场 (290-tick 4 段时间轴) + 收缩竞技场屏障。
    ///  ● <b>G5 门控</b>：入场后大椿无敌, 四角生成季节锚点; 须先毁 3 个才进入可受伤的季节循环 (4.5M 血"挣得起")。
    ///  ● <b>四季签名</b>：春(藤蔓迷宫+活体根须安全岛) / 夏(落叶雨) / 秋(黄金幻影诱饵 DPS 谜题) / 冬(减速+冰藤+生命汲取治疗线)。
    ///    每季 = 一招 + 一条战场规则; 击毁锚点可主动切换主导季节并开破绽窗口。<b>杀掉 Phase2_FuryPatrol</b>。
    ///  ● 表现走硬化 ACMShaders：PaletteLUT 四季调色 / ElementalScreenTint 季节氛围 / ArenaRunic 根须地纹 / DrawBeam 治疗线。
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

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,
            Gate,            // G5 门控：毁 3 锚点
            SeasonCombat,    // 四季轮转主循环
            PhaseTransition_2
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
            if (isRising) return false;
            return null;
        }

        public override bool CheckActive() => false;

        public override void DrawBehind(int index) {
            if (isRising) {
                Main.instance.DrawCacheProjsBehindNPCsAndTiles.Add(index);
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
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

        public override void OnKill() {
            DownedBossSystem.downedDazheng = true;
            HealThreadActive = false;
            if (Main.netMode != NetmodeID.Server) {
                PunchCameraModifier modifier = new(NPC.Center,
                    (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(),
                    25f, 12f, 80, 3000f, FullName);
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

            NPC.behindTiles = isRising;
            if (isRising) {
                for (int i = 0; i < 110; i++)
                    Lighting.AddLight(NPC.Center + VaultUtils.RandVr(0, 500), Color.Green.ToVector3() * 10);
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
            if (defenseDownTimer > 0)
                defenseDownTimer--;
            NPC.defense = Math.Max(0, baseDef - (defenseDownTimer > 0 ? 50 : 0));
        }

        private void OpenVulnerabilityWindow(int ticks) {
            defenseDownTimer = Math.Max(defenseDownTimer, ticks);
            seasonFlash = MathF.Max(seasonFlash, 0.6f);
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.3f }, NPC.Center);
            }
            NPC.netUpdate = true;
        }

        #endregion

        #region 季节核心

        private float ArenaRadius => IsPhase2 ? DazhengArenaBarrier.Phase2Radius : DazhengArenaBarrier.Phase1Radius;

        private static int SeasonDuration(int s, bool phase2) {
            int baseDur = s switch {
                DazhengSeasons.Spring => 540,
                DazhengSeasons.Summer => 420,
                DazhengSeasons.Winter => 540,
                _ => 480,
            };
            return phase2 ? (int)(baseDur * 0.82f) : baseDur;
        }

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

            // 活体根须场仅在 春(P2) / 冬 开启
            UpdateRootField();

            if (Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f, Volume = 0.9f }, NPC.Center);
            ACMUtils.AddScreenShake(7f);
            NPC.netUpdate = true;
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

        #region 春 — 藤蔓迷宫 + 活体根须

        private void SeasonSpring(Player target) {
            int dur = SeasonDuration(DazhengSeasons.Spring, IsPhase2);

            if (AttackTimer == 1) {
                if (Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = -0.2f }, NPC.Center);
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

        #endregion

        #region 夏 — 落叶雨

        private void SeasonSummer(Player target) {
            int dur = SeasonDuration(DazhengSeasons.Summer, IsPhase2);

            int wave = (int)(AttackTimer / 45);
            int leafPerTick = 2 + wave;
            int cap = IsPhase2 ? 7 : 6;
            if (leafPerTick > cap) leafPerTick = cap;

            if (AttackTimer % (IsPhase2 ? 13 : 15) == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < leafPerTick; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(
                        Main.rand.NextFloat(-600, 600), -500 - Main.rand.NextFloat(0, 200));
                    Vector2 vel = new(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(5f, 12f));
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DazhengLeaf>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            // 侧向藤蔓夹击 (telegraphed, 低频)
            if (AttackTimer % 80 == 40 && Main.netMode != NetmodeID.MultiplayerClient) {
                float side = Main.rand.NextBool() ? -1f : 1f;
                for (int i = 0; i < 4; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(side * 600, -200 + i * 120);
                    Vector2 vel = new(-side * 10f, 0);
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DazhengVine>(), NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles)
                        Main.projectile[proj].timeLeft = 200;
                }
            }

            if (AttackTimer % 45 == 0 && Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = 0.3f }, target.Center);

            if (AttackTimer > dur)
                AdvanceSeason(DazhengSeasons.Autumn);
        }

        #endregion

        #region 秋 — 黄金幻影·诱饵树 DPS 谜题

        private void SeasonAutumn(Player target) {
            // 起手: 真身无敌, 暴露金核, 镜像位生成诱饵树
            if (AttackTimer == 1) {
                DecoyKilled = false;
                decoyResolved = false;
                decoyWindowOpen = true;
                NPC.dontTakeDamage = true;
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
                if (Main.netMode != NetmodeID.Server && AttackTimer % 5 == 0) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    Dust d = Dust.NewDustPerfect(NPC.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 90f,
                        DustID.GoldFlame, Vector2.Zero, 100, default, 1.6f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 2f;
                }
                if (AttackTimer % 30 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 8f;
                    vel = vel.RotatedByRandom(MathHelper.ToRadians(18f));
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DazhengVine>(), NPC.damage / 5, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles)
                        Main.projectile[proj].ai[2] = 1f; // 金藤
                }

                // 谜题解决: 诱饵被打掉 (静态事件) → 大破绽; 或诱饵消失 (超时)
                bool decoyGone = !(decoyWhoAmI >= 0 && decoyWhoAmI < Main.maxNPCs &&
                                   Main.npc[decoyWhoAmI].active &&
                                   Main.npc[decoyWhoAmI].type == ModContent.NPCType<DazhengDecoyTree>());
                bool killedRecently = DecoyKilled && Main.GameUpdateCount - DecoyEventFrame < 30;

                if (decoyGone || killedRecently) {
                    decoyWindowOpen = false;
                    decoyResolved = true;
                    NPC.dontTakeDamage = false;
                    if (killedRecently) {
                        OpenVulnerabilityWindow(300); // 解谜成功: 5s 大破绽
                        if (Main.netMode != NetmodeID.Server) {
                            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.4f, Volume = 1.4f }, NPC.Center);
                            ACMUtils.AddScreenShake(10f);
                        }
                    }
                    SubState = AttackTimer; // 记录解决时刻
                }

                // 安全阀: 极端情况下窗口最长 DazhengDecoyTree.Lifetime + 60
                if (AttackTimer > DazhengDecoyTree.Lifetime + 60) {
                    decoyWindowOpen = false;
                    decoyResolved = true;
                    NPC.dontTakeDamage = false;
                }
                return;
            }

            // 解决后短余波 → 切冬
            if (decoyResolved && AttackTimer > SubState + 120)
                AdvanceSeason(DazhengSeasons.Winter);
        }

        #endregion

        #region 冬 — 减速 + 冰藤 + 生命汲取治疗线

        private void SeasonWinter(Player target) {
            int dur = SeasonDuration(DazhengSeasons.Winter, IsPhase2);
            const int FormEnd = 50;     // 蓄能(安全)阶段
            int healEnd = dur - 60;     // 汲取阶段结束

            // 冬季减速 (本地玩家, 场内): 冰冻凛冬规则
            if (Main.netMode != NetmodeID.Server) {
                Player lp = Main.LocalPlayer;
                if (lp.active && !lp.dead && Vector2.Distance(lp.Center, NPC.Center) < ArenaRadius)
                    lp.AddBuff(BuffID.Chilled, 6);
            }

            // 起手: 生成治疗线 (蓄能, 安全色)
            if (AttackTimer == 1) {
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

            bool draining = AttackTimer >= FormEnd && AttackTimer < healEnd && !healBroken;
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
                            KillTrackedProjectile(ref healThreadWhoAmI);
                            OpenVulnerabilityWindow(200);
                            if (Main.netMode != NetmodeID.Server) {
                                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.4f }, NPC.Center);
                                ACMUtils.AddScreenShake(8f);
                            }
                        }
                    }
                }
            }

            // 冰藤: 低频、慢速的冰蓝藤蔓 (telegraphed)
            if (AttackTimer % (IsPhase2 ? 26 : 34) == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int arms = IsPhase2 ? 3 : 2;
                vineRotation += 0.5f;
                for (int a = 0; a < arms; a++) {
                    float angle = vineRotation + MathHelper.TwoPi / arms * a;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 6f;
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

        #region 阶段转换演出 (保留骨架)

        private void RunPhaseTransition2(Player target) {
            NPC.dontTakeDamage = true;

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 12; i++) {
                    float angle = MathHelper.TwoPi / 12 * i + globalTime * 3f;
                    float dist = 400 - PhaseTimer * 2.5f;
                    if (dist < 80) dist = 80;
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 80, default, 3f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 8f;
                }
                for (int i = 0; i < 6; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(300, 300);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.JungleGrass, 0, 0, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                }
            }

            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 1.5f }, NPC.Center);
                ACMUtils.AddScreenShake(12f);
                seasonFlash = 1f;

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
                    // 转阶段爆发
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

            if (PhaseTimer >= 100) {
                NPC.dontTakeDamage = false;
                baseDef = 120;
                NPC.damage = (int)(NPC.damage * 1.25f);
                // 续接四季循环, 重启当前季节 (P2 更密)
                AdvanceSeason(season);
                TransitionTo(BossPhase.SeasonCombat);
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

            // LUT 参数 (PostDraw 用)
            float lutTarget = gatePassed ? 0.5f : 0.18f;
            lutIntensity = MathHelper.Lerp(lutIntensity, lutTarget, 0.04f);
            lutShadow = DazhengSeasons.LutShadow(season);
            lutHi = DazhengSeasons.LutHighlight(season);
            lutSat = MathHelper.Lerp(lutSat, DazhengSeasons.LutSaturation(season), 0.05f);

            // 发布季节氛围 (ElementalScreenTint, 廉价第二层)
            if (Main.netMode != NetmodeID.Server) {
                float strength = MathHelper.Clamp(0.55f + seasonFlash * 0.4f, 0f, 1f);
                DazhengSeasonScreenSystem.Publish(DazhengSeasons.Tint(season), strength, globalTime);
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = new(0, 0, texture.Width, texture.Height);
            Vector2 origin = new(TextureWidth / 2f, TextureHeight / 2f);
            Vector2 drawPos = NPC.Center - screenPos;

            // 二阶段金色底光
            float glowPulse = 0f;
            if (IsPhase2)
                glowPulse = 0.15f + MathF.Sin(globalTime * 4f) * 0.1f;
            if (glowPulse > 0) {
                Color goldGlow = new Color(255, 200, 50, 0) * glowPulse;
                spriteBatch.Draw(texture, drawPos, frame, goldGlow, 0f, origin,
                    NPC.scale * 1.05f, SpriteEffects.None, 0f);
            }

            // 主体绘制
            spriteBatch.Draw(texture, drawPos, frame, drawColor, 0f, origin, NPC.scale, SpriteEffects.None, 0f);

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

                    // 秋季诱饵窗口: 真身金核暴露脉冲
                    if (Phase == BossPhase.SeasonCombat && season == DazhengSeasons.Autumn && decoyWindowOpen) {
                        float p = 0.5f + 0.5f * MathF.Sin(globalTime * 6f);
                        Color core = new Color(255, 210, 90, 0);
                        spriteBatch.Draw(soft, drawPos - new Vector2(0, TextureHeight * 0.12f), null,
                            core * (0.55f * p), 0f, go, 1.4f + p * 0.5f, SpriteEffects.None, 0f);
                    }

                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                }
            }

            return false;
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
