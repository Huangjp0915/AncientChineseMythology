using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dazhengs
{
    /// <summary>
    /// 大椿 - 自然化身，上古树神
    /// 月球领主后超级Boss，固定不动
    /// 一阶段：藤蔓弹幕地狱与迷宫 + 树叶雨 + 黄金幻象
    /// 贴图尺寸：512×280
    /// </summary>
    [AutoloadBossHead]
    public class Dazheng : ModNPC
    {
        #region 常量

        public const int TextureWidth = 1024;
        public const int TextureHeight = 558;
        public const float Phase2Threshold = 0.55f;

        // 藤蔓迷宫参数
        private const int VineMazeInterval = 90;
        private const int VineWallInterval = 50;
        private const int VineSpiralInterval = 6;
        private const int LeafRainInterval = 12;
        private const int GoldenPhantomInterval = 200;

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,
            Phase1_Idle,
            Phase1_VineMaze,         // 藤蔓迷宫 - 编织出弹幕墙壁
            Phase1_VineWhip,         // 藤蔓鞭笞 - 快速伸展的藤蔓链
            Phase1_LeafStorm,        // 树叶风暴 - 密集落叶雨
            Phase1_GoldenPhantom,    // 黄金幻象 - 金色分身攻击
            Phase1_VineSpiral,       // 藤蔓螺旋 - 旋转藤蔓弹幕地狱
            Phase1_NatureWrath,      // 自然之怒 - 综合攻击
            PhaseTransition_2,
            Phase2_VineHell,         // 藤蔓地狱 - 更密集的弹幕
            Phase2_AncientRoots,     // 远古根须 - 地面涌出的攻击
            Phase2_GoldenForest,     // 黄金森林 - 多重幻象
            Phase2_LifeDrain,        // 生命汲取 - 收缩型弹幕
            Phase2_FuryPatrol        // 狂怒巡逻 - 持续施压
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
        private Vector2 spawnPosition;
        private float vineRotation; // 旋转藤蔓角度
        private int attackCycle; // 攻击循环计数

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
            // 碰撞箱使用合理尺寸，绘制时用纹理原始尺寸
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
            // 掉落占位 - 后续添加专属掉落物
        }

        public override void OnSpawn(IEntitySource source) {
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            globalTime = 0;
            // 自动贴地：从召唤位置向下扫描找到地面
            spawnPosition = FindGroundPosition(NPC.Center);
            NPC.Center = spawnPosition;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        /// <summary>
        /// 从给定位置向下扫描物块，找到地面并将Boss底部对齐地面
        /// Boss的Center会被设置为：地面Y - 纹理高度/2，使树根贴地
        /// </summary>
        private Vector2 FindGroundPosition(Vector2 startPos) {
            int tileX = (int)(startPos.X / 16f);
            int startTileY = (int)(startPos.Y / 16f);

            // 向下扫描，最多扫描150格（2400像素）
            int groundTileY = startTileY;
            for (int y = startTileY; y < startTileY + 150 && y < Main.maxTilesY - 1; y++) {
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    groundTileY = y;
                    break;
                }
            }

            // 地面顶部的世界坐标
            float groundWorldY = groundTileY * 16f;
            // Boss中心 = 地面Y - 纹理高度的一半（使纹理底部贴地）
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
            writer.Write(attackCycle);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            spawnPosition = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            vineRotation = reader.ReadSingle();
            attackCycle = reader.ReadInt32();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 2f;
            return null;
        }

        public override bool CheckActive() => false;

        public override void HitEffect(NPC.HitInfo hit) {
            // 受击时落叶飞溅
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

            // 固定在原地不动
            NPC.velocity = Vector2.Zero;
            if (spawnPosition != Vector2.Zero)
                NPC.Center = spawnPosition;

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
                case BossPhase.Phase1_Idle: RunPhase1Idle(target); break;
                case BossPhase.Phase1_VineMaze: RunPhase1VineMaze(target); break;
                case BossPhase.Phase1_VineWhip: RunPhase1VineWhip(target); break;
                case BossPhase.Phase1_LeafStorm: RunPhase1LeafStorm(target); break;
                case BossPhase.Phase1_GoldenPhantom: RunPhase1GoldenPhantom(target); break;
                case BossPhase.Phase1_VineSpiral: RunPhase1VineSpiral(target); break;
                case BossPhase.Phase1_NatureWrath: RunPhase1NatureWrath(target); break;
                case BossPhase.PhaseTransition_2: RunPhaseTransition2(target); break;
                case BossPhase.Phase2_VineHell: RunPhase2VineHell(target); break;
                case BossPhase.Phase2_AncientRoots: RunPhase2AncientRoots(target); break;
                case BossPhase.Phase2_GoldenForest: RunPhase2GoldenForest(target); break;
                case BossPhase.Phase2_LifeDrain: RunPhase2LifeDrain(target); break;
                case BossPhase.Phase2_FuryPatrol: RunPhase2FuryPatrol(target); break;
            }

            // 树神的自然光辉
            float glow = 0.6f + MathF.Sin(globalTime * 2f) * 0.2f;
            Lighting.AddLight(NPC.Center, new Vector3(0.3f, 0.6f, 0.15f) * glow);

            // 持续落叶粒子效果
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
            if (!didPhase2Transition && IsPhase2 &&
                Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Intro) {
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
            attackCycle = 0;
            NPC.netUpdate = true;
        }

        private BossPhase GetRandomPhase1Attack() {
            attackCycle++;
            // 每几轮强制插入黄金幻象
            if (attackCycle % 4 == 0)
                return BossPhase.Phase1_GoldenPhantom;

            return (BossPhase)(Main.rand.Next(5) switch {
                0 => (int)BossPhase.Phase1_VineMaze,
                1 => (int)BossPhase.Phase1_VineWhip,
                2 => (int)BossPhase.Phase1_LeafStorm,
                3 => (int)BossPhase.Phase1_VineSpiral,
                _ => (int)BossPhase.Phase1_NatureWrath
            });
        }

        private BossPhase GetRandomPhase2Attack() {
            return (BossPhase)(Main.rand.Next(4) switch {
                0 => (int)BossPhase.Phase2_VineHell,
                1 => (int)BossPhase.Phase2_AncientRoots,
                2 => (int)BossPhase.Phase2_GoldenForest,
                _ => (int)BossPhase.Phase2_LifeDrain
            });
        }

        #endregion

        #region 入场演出

        private void RunIntro(Player target) {
            if (PhaseTimer == 1) {
                // spawnPosition已在OnSpawn中通过贴地逻辑设置
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 1.3f }, NPC.Center);
            }

            NPC.dontTakeDamage = true;

            // 自然能量汇聚效果
            if (Main.netMode != NetmodeID.Server) {
                int particleCount = (int)MathHelper.Lerp(3, 15, PhaseTimer / 150f);
                for (int i = 0; i < particleCount; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(200, 600) * (1f - PhaseTimer / 150f);
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GreenTorch, 0, 0, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                }

                // 地面涌出藤蔓粒子
                for (int i = 0; i < 2; i++) {
                    Vector2 groundPos = NPC.Center + new Vector2(Main.rand.NextFloat(-400, 400), 200);
                    Dust d = Dust.NewDustDirect(groundPos, 0, 0, DustID.JungleGrass, 0, -3f, 100, default, 2f);
                    d.noGravity = true;
                }
            }

            if (PhaseTimer >= 150) {
                NPC.dontTakeDamage = false;
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center,
                        (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(),
                        20f, 10f, 40, 2000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f }, NPC.Center);
                TransitionTo(BossPhase.Phase1_Idle);
            }
        }

        #endregion

        #region 一阶段：自然之力

        private void RunPhase1Idle(Player target) {
            // 待机状态，轻微施压（给予玩家喘息窗口）
            if (PhaseTimer % 40 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 8f;
                vel = vel.RotatedByRandom(MathHelper.ToRadians(25f));
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<DazhengVine>(), NPC.damage / 5, 0f, Main.myPlayer);
            }

            if (PhaseTimer > 120) TransitionTo(GetRandomPhase1Attack());
        }

        /// <summary>
        /// 藤蔓迷宫 - 编织出弹幕墙壁迫使玩家在缝隙中穿梭
        /// </summary>
        private void RunPhase1VineMaze(Player target) {
            // 构建藤蔓墙壁：横向和纵向交替生成
            if (AttackTimer % 30 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int wallType = (int)(AttackTimer / 30) % 4;

                switch (wallType) {
                    case 0: // 左侧横向藤蔓墙（留缺口）
                        SpawnVineWall(target.Center, -1, true);
                        break;
                    case 1: // 右侧横向藤蔓墙
                        SpawnVineWall(target.Center, 1, true);
                        break;
                    case 2: // 上方纵向藤蔓帘
                        SpawnVineWall(target.Center, -1, false);
                        break;
                    case 3: // 下方纵向藤蔓
                        SpawnVineWall(target.Center, 1, false);
                        break;
                }
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f }, target.Center);
            }

            // 墙壁间歇期才释放少量追踪藤蔓（避免填满缝隙）
            if (AttackTimer % 30 >= 15 && AttackTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 9f;
                vel = vel.RotatedByRandom(MathHelper.ToRadians(20f));
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<DazhengVine>(), NPC.damage / 5, 0f, Main.myPlayer);
            }

            if (AttackTimer > 200) TransitionTo(BossPhase.Phase1_Idle);
        }

        private void SpawnVineWall(Vector2 center, int direction, bool horizontal) {
            int vineCount = Main.expertMode ? 12 : 10;
            int gapIndex = Main.rand.Next(2, vineCount - 3); // 留出缝隙
            int gapSize = Main.expertMode ? 3 : 4;

            for (int i = 0; i < vineCount; i++) {
                // 跳过缝隙位置
                if (i >= gapIndex && i < gapIndex + gapSize) continue;

                Vector2 spawnPos;
                Vector2 vel;
                float spacing = 80f;

                if (horizontal) {
                    spawnPos = center + new Vector2(direction * 700, -vineCount / 2 * spacing + i * spacing);
                    vel = new Vector2(-direction * 8f, 0);
                } else {
                    spawnPos = center + new Vector2(-vineCount / 2 * spacing + i * spacing, direction * 700);
                    vel = new Vector2(0, -direction * 8f);
                }

                int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                    ModContent.ProjectileType<DazhengVine>(), NPC.damage / 4, 0f, Main.myPlayer);
                if (proj >= 0 && proj < Main.maxProjectiles)
                    Main.projectile[proj].timeLeft = 300;
            }
        }

        /// <summary>
        /// 藤蔓鞭笞 - 从Boss身体快速伸展的藤蔓鞭
        /// </summary>
        private void RunPhase1VineWhip(Player target) {
            // 多方向藤蔓鞭笞（降低频率，保持单次威胁感）
            int whipInterval = Main.expertMode ? 14 : 18;
            if (AttackTimer % whipInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int whipCount = Main.expertMode ? 4 : 3;
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                float spreadAngle = MathHelper.ToRadians(12f);

                for (int i = -whipCount / 2; i <= whipCount / 2; i++) {
                    Vector2 vel = toPlayer.RotatedBy(i * spreadAngle) * 18f;
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DazhengVine>(), NPC.damage / 4, 2f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) {
                        Main.projectile[proj].timeLeft = 120;
                        Main.projectile[proj].ai[1] = 1f; // 标记为鞭笞模式（更快速）
                    }
                }
                SoundEngine.PlaySound(SoundID.Item153 with { Pitch = -0.3f }, NPC.Center);
            }

            // 落叶仅在鞭笞间歇期释放，不同时施压
            if (AttackTimer % whipInterval >= whipInterval / 2 && AttackTimer % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 3; i++) {
                    Vector2 leafPos = target.Center + new Vector2(Main.rand.NextFloat(-400, 400), -600);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(6f, 10f));
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), leafPos, vel,
                        ModContent.ProjectileType<DazhengLeaf>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            if (AttackTimer > 140) TransitionTo(BossPhase.Phase1_Idle);
        }

        /// <summary>
        /// 树叶风暴 - 从天空降下密集的树叶雨
        /// </summary>
        private void RunPhase1LeafStorm(Player target) {
            // 天降落叶，波次递增（上限降低，保留可躲空间）
            int wave = (int)(AttackTimer / 45);
            int leafPerTick = 2 + wave;
            if (leafPerTick > 6) leafPerTick = 6;

            if (AttackTimer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < leafPerTick; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(
                        Main.rand.NextFloat(-600, 600), -500 - Main.rand.NextFloat(0, 200));
                    Vector2 vel = new Vector2(
                        Main.rand.NextFloat(-3f, 3f),
                        Main.rand.NextFloat(5f, 12f));
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DazhengLeaf>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            // 侧向藤蔓夹击（降低频率和数量，避免与叶雨同时覆盖全屏）
            if (AttackTimer % 75 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float side = Main.rand.NextBool() ? -1f : 1f;
                for (int i = 0; i < 4; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(side * 600, -200 + i * 120);
                    Vector2 vel = new Vector2(-side * 10f, 0);
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DazhengVine>(), NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles)
                        Main.projectile[proj].timeLeft = 200;
                }
            }

            // 警告音效
            if (AttackTimer % 45 == 0)
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = 0.3f }, target.Center);

            if (AttackTimer > 240) TransitionTo(BossPhase.Phase1_Idle);
        }

        /// <summary>
        /// 黄金幻象 - 在Boss位置生成金色虚影进行攻击
        /// </summary>
        private void RunPhase1GoldenPhantom(Player target) {
            NPC.dontTakeDamage = PhaseTimer < 30;

            if (PhaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
            }

            // 蓄力阶段 - 金色粒子汇聚
            if (PhaseTimer < 30 && Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 8; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(100, 400);
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 6f;
                }
            }

            // 释放黄金幻象弹幕
            if (PhaseTimer == 30 && Main.netMode != NetmodeID.MultiplayerClient) {
                // 从Boss四周释放多个金色幻象弹
                int phantomCount = Main.expertMode ? 8 : 6;
                for (int i = 0; i < phantomCount; i++) {
                    float angle = MathHelper.TwoPi / phantomCount * i;
                    Vector2 spawnPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 200f;
                    Vector2 vel = (target.Center - spawnPos).SafeNormalize(Vector2.UnitY) * 6f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DazhengGoldenPhantom>(), NPC.damage / 3, 1f, Main.myPlayer);
                }

                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center,
                        Vector2.UnitY, 10f, 6f, 20, 1500f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
            }

            // 后续追加金色藤蔓
            if (PhaseTimer > 50 && PhaseTimer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 spawnPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 150f;
                Vector2 vel = (target.Center - spawnPos).SafeNormalize(Vector2.UnitY) * 12f;
                int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                    ModContent.ProjectileType<DazhengVine>(), NPC.damage / 4, 0f, Main.myPlayer);
                if (proj >= 0 && proj < Main.maxProjectiles)
                    Main.projectile[proj].ai[2] = 1f; // 金色藤蔓标记
            }

            if (PhaseTimer > 120) TransitionTo(BossPhase.Phase1_Idle);
        }

        /// <summary>
        /// 藤蔓螺旋 - 从Boss处发射旋转的藤蔓弹幕地狱
        /// </summary>
        private void RunPhase1VineSpiral(Player target) {
            vineRotation += 0.05f;

            // 螺旋藤蔓（降低臂数和频率，保留视觉旋转感但留出穿越间隙）
            if (AttackTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int armCount = Main.expertMode ? 3 : 2;
                for (int arm = 0; arm < armCount; arm++) {
                    float angle = vineRotation + MathHelper.TwoPi / armCount * arm;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 9f;
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DazhengVine>(), NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles)
                        Main.projectile[proj].timeLeft = 200;
                }
            }

            // 反向旋转的内圈落叶（降低频率，不与藤蔓同帧发射）
            if (AttackTimer % 18 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float innerAngle = -vineRotation * 1.5f + AttackTimer * 0.08f;
                for (int i = 0; i < 2; i++) {
                    float a = innerAngle + i * MathHelper.Pi;
                    Vector2 vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 6f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DazhengLeaf>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            // 粒子效果
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 3; i++) {
                    float angle = vineRotation + Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(50, 200);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.JungleGrass, 0, 0, 100, default, 1.8f);
                    d.noGravity = true;
                    d.velocity = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 3f;
                }
            }

            if (AttackTimer > 180) TransitionTo(BossPhase.Phase1_Idle);
        }

        /// <summary>
        /// 自然之怒 - 多种攻击综合，体现自然的狂暴力量
        /// </summary>
        private void RunPhase1NatureWrath(Player target) {
            // 分波次攻击，每波之间留出反应窗口

            // 第一波（tick 20）：环形藤蔓爆发
            if (AttackTimer == 20 && Main.netMode != NetmodeID.MultiplayerClient) {
                int ringCount = Main.expertMode ? 12 : 10;
                for (int i = 0; i < ringCount; i++) {
                    float angle = MathHelper.TwoPi / ringCount * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 7f;
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DazhengVine>(), NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles)
                        Main.projectile[proj].timeLeft = 250;
                }
                SoundEngine.PlaySound(SoundID.Item153 with { Pitch = -0.5f }, NPC.Center);
            }

            // 第二波（tick 60-130）：落叶暴雨（环形已散开后才开始）
            if (AttackTimer > 60 && AttackTimer < 130 && AttackTimer % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 3; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(Main.rand.NextFloat(-500, 500), -600);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(7f, 13f));
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DazhengLeaf>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            // 第三波（tick 140）：黄金弹幕（落叶结束后释放）
            if (AttackTimer == 140 && Main.netMode != NetmodeID.MultiplayerClient) {
                int phantomCount = 5;
                for (int i = 0; i < phantomCount; i++) {
                    float angle = MathHelper.TwoPi / phantomCount * i + MathHelper.ToRadians(30f);
                    Vector2 pos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 250f;
                    Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.UnitY) * 5f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                        ModContent.ProjectileType<DazhengGoldenPhantom>(), NPC.damage / 3, 0f, Main.myPlayer);
                }
            }

            // 低威胁旋转藤蔓（仅在后半段，且频率降低）
            vineRotation += 0.04f;
            if (AttackTimer > 100 && AttackTimer % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int arm = 0; arm < 2; arm++) {
                    float angle = vineRotation + arm * MathHelper.Pi;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DazhengVine>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            if (AttackTimer > 190) TransitionTo(BossPhase.Phase1_Idle);
        }

        #endregion

        #region 阶段转换演出

        private void RunPhaseTransition2(Player target) {
            NPC.dontTakeDamage = true;

            // 大地震动效果
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
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center,
                        (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(),
                        25f, 12f, 60, 3000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }

                // 转阶段爆发 - 全向藤蔓+金色弹幕（降低密度，保持气势）
                if (Main.netMode != NetmodeID.MultiplayerClient) {
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
                NPC.defense += 20;
                NPC.damage = (int)(NPC.damage * 1.25f);
                TransitionTo(BossPhase.Phase2_FuryPatrol);
            }
        }

        #endregion

        #region 二阶段：远古觉醒

        private void RunPhase2FuryPatrol(Player target) {
            // 持续施压 - 藤蔓+落叶交替（降低频率，给更多喘息空间）
            if (PhaseTimer % 14 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 12f;
                vel = vel.RotatedByRandom(MathHelper.ToRadians(20f));
                if (PhaseTimer % 28 == 0) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DazhengVine>(), NPC.damage / 4, 0f, Main.myPlayer);
                } else {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DazhengLeaf>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            if (PhaseTimer > 90) TransitionTo(GetRandomPhase2Attack());
        }

        /// <summary>
        /// 藤蔓地狱 - 极度密集的藤蔓弹幕，多层旋转+墙壁
        /// </summary>
        private void RunPhase2VineHell(Player target) {
            vineRotation += 0.07f;

            // 四臂螺旋藤蔓（降低频率，保留臂间空档）
            if (AttackTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int armCount = 4;
                for (int arm = 0; arm < armCount; arm++) {
                    float angle = vineRotation + MathHelper.TwoPi / armCount * arm;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 10f;
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DazhengVine>(), NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles)
                        Main.projectile[proj].timeLeft = 180;
                }
            }

            // 藤蔓墙（拉大间隔，不与螺旋同帧）
            if (AttackTimer % 50 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                SpawnVineWall(target.Center, Main.rand.NextBool() ? 1 : -1, Main.rand.NextBool());
            }

            // 追踪落叶补刀（降低数量和频率）
            if (AttackTimer % 25 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 2; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(Main.rand.NextFloat(-500, 500), -600);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(8f, 14f));
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DazhengLeaf>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            if (AttackTimer > 220) TransitionTo(BossPhase.Phase2_FuryPatrol);
        }

        /// <summary>
        /// 远古根须 - 从玩家周围地面涌出的藤蔓攻击
        /// </summary>
        private void RunPhase2AncientRoots(Player target) {
            // 从下方涌出的藤蔓柱（降低频率和数量）
            if (AttackTimer % 28 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int rootCount = Main.expertMode ? 5 : 4;
                for (int i = 0; i < rootCount; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(
                        Main.rand.NextFloat(-500, 500), 400);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(10f, 16f));
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DazhengVine>(), NPC.damage / 3, 1f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles)
                        Main.projectile[proj].timeLeft = 200;
                }
                SoundEngine.PlaySound(SoundID.Item153 with { Pitch = -0.6f, Volume = 0.8f }, target.Center);
            }

            // 从上方落下对称藤蔓（拉大间隔，不与根须同时发射）
            if (AttackTimer % 28 >= 14 && AttackTimer % 14 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 3; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(
                        Main.rand.NextFloat(-400, 400), -500);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(8f, 14f));
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DazhengVine>(), NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles)
                        Main.projectile[proj].timeLeft = 200;
                }
            }

            // Boss自身释放旋转藤蔓（大幅降低频率，避免三源重叠）
            vineRotation += 0.04f;
            if (AttackTimer % 22 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int arm = 0; arm < 2; arm++) {
                    float angle = vineRotation + MathHelper.TwoPi / 2 * arm;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 7f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<DazhengVine>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            if (AttackTimer > 220) TransitionTo(BossPhase.Phase2_FuryPatrol);
        }

        /// <summary>
        /// 黄金森林 - 多重黄金幻象同时攻击
        /// </summary>
        private void RunPhase2GoldenForest(Player target) {
            if (PhaseTimer == 1)
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);

            // 三波黄金幻象轮流释放（拉大波次间隔，降低每波数量）
            if ((AttackTimer == 30 || AttackTimer == 80 || AttackTimer == 130) && Main.netMode != NetmodeID.MultiplayerClient) {
                int phantomCount = Main.expertMode ? 8 : 6;
                float offset = AttackTimer * 0.1f;
                for (int i = 0; i < phantomCount; i++) {
                    float angle = MathHelper.TwoPi / phantomCount * i + offset;
                    Vector2 spawnPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 300f;
                    Vector2 vel = (target.Center - spawnPos).SafeNormalize(Vector2.UnitY) * 7f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DazhengGoldenPhantom>(), NPC.damage / 3, 1f, Main.myPlayer);
                }

                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center,
                        Vector2.UnitY, 8f, 5f, 15, 1200f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
            }

            // 间隔中藤蔓施压（降低频率，不在幻象释放帧附近发射）
            bool nearPhantomWave = AttackTimer is >= 25 and <= 35 or >= 75 and <= 85 or >= 125 and <= 135;
            if (!nearPhantomWave && AttackTimer % 16 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 14f;
                vel = vel.RotatedByRandom(MathHelper.ToRadians(25f));
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<DazhengVine>(), NPC.damage / 4, 0f, Main.myPlayer);
            }

            // 金色粒子效果
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 5; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(250, 250);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GoldFlame, 0, -1f, 100, default, 2f);
                    d.noGravity = true;
                }
            }

            if (AttackTimer > 180) TransitionTo(BossPhase.Phase2_FuryPatrol);
        }

        /// <summary>
        /// 生命汲取 - 从外圈向内收缩的藤蔓牢笼
        /// </summary>
        private void RunPhase2LifeDrain(Player target) {
            // 藤蔓牢笼（减少层数和每层数量，每层留逃脱缺口）
            if (AttackTimer == 20 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int ring = 0; ring < 3; ring++) {
                    int vineCount = 16 + ring * 3;
                    float ringRadius = 500f + ring * 120f;
                    int gapStart = Main.rand.Next(vineCount); // 随机缺口位置
                    int gapSize = 3 + ring; // 外圈缺口更大
                    for (int i = 0; i < vineCount; i++) {
                        // 跳过缺口位置，确保每层都有逃生路线
                        if ((i >= gapStart && i < gapStart + gapSize) ||
                            (gapStart + gapSize > vineCount && i < (gapStart + gapSize) % vineCount))
                            continue;
                        float angle = MathHelper.TwoPi / vineCount * i + ring * MathHelper.ToRadians(10f);
                        Vector2 pos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ringRadius;
                        Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * (2.5f + ring * 0.8f);
                        int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                            ModContent.ProjectileType<DazhengVine>(), NPC.damage / 4, 0f, Main.myPlayer);
                        if (proj >= 0 && proj < Main.maxProjectiles)
                            Main.projectile[proj].timeLeft = 300;
                    }
                }
                SoundEngine.PlaySound(SoundID.Item153 with { Volume = 1.2f }, target.Center);
            }

            // 同步追踪黄金幻象（降低频率和数量）
            if (AttackTimer > 60 && AttackTimer % 40 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 3; i++) {
                    float angle = MathHelper.TwoPi / 3 * i + AttackTimer * 0.05f;
                    Vector2 pos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 200f;
                    Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.UnitY) * 8f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                        ModContent.ProjectileType<DazhengGoldenPhantom>(), NPC.damage / 3, 0f, Main.myPlayer);
                }
            }

            // 落叶雨（降低密度）
            if (AttackTimer % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 3; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(Main.rand.NextFloat(-500, 500), -600);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(6f, 12f));
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<DazhengLeaf>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            if (AttackTimer > 200) TransitionTo(BossPhase.Phase2_FuryPatrol);
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = new Rectangle(0, 0, texture.Width, texture.Height);
            // 绘制原点设在纹理底部中心，使树根对齐NPC底部
            Vector2 origin = new Vector2(TextureWidth / 2f, TextureHeight / 2f);
            Vector2 drawPos = NPC.Center - screenPos;

            // 生命低于阈值时轻微发光
            float glowPulse = 0f;
            if (IsPhase2) {
                glowPulse = 0.15f + MathF.Sin(globalTime * 4f) * 0.1f;
            }

            // 金色底光层（二阶段增强）
            if (glowPulse > 0) {
                Color goldGlow = new Color(255, 200, 50, 0) * glowPulse;
                spriteBatch.Draw(texture, drawPos, frame, goldGlow, 0f, origin,
                    NPC.scale * 1.05f, SpriteEffects.None, 0f);
            }

            // 主体绘制
            spriteBatch.Draw(texture, drawPos, frame, drawColor, 0f, origin, NPC.scale, SpriteEffects.None, 0f);

            return false;
        }

        #endregion
    }
}
