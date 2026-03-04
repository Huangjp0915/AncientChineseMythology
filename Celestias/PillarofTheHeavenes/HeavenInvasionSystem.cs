using AncientChineseMythology.Celestias.PillarofTheHeavenes.Enemys;
using AncientChineseMythology.Systems;
using InnoVault.Actors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes
{
    /// <summary>
    /// 天庭入侵事件系统
    /// 
    /// 事件触发后：
    /// - 四根天柱悬停在事件开始时的初始位置
    /// - 不断刷新天庭敌怪（铜羽神鸟、翔龙、天眼、金甲圣骑）
    /// - 玩家需要击杀足够数量的敌怪来推进事件进度
    /// - 进度达到100%后事件结束
    /// </summary>
    public class HeavenInvasionSystem : ModSystem
    {
        #region 常量配置

        /// <summary>事件中四根天柱之间的间距（像素）</summary>
        private const float PillarSpacingPixels = 1600f;

        /// <summary>天柱悬停高度——在地表上方多少像素</summary>
        private const int PillarHoverHeight = 800;

        /// <summary>入侵事件完成所需击杀积分</summary>
        public const int MaxInvasionPoints = 1500;

        /// <summary>总波次数</summary>
        public const int TotalWaves = 20;

        /// <summary>每波所需积分 = MaxInvasionPoints / TotalWaves</summary>
        public static int PointsPerWave => MaxInvasionPoints / TotalWaves;

        /// <summary>敌怪生成检查间隔（帧）</summary>
        private const int SpawnCheckInterval = 20;

        /// <summary>入侵期间最大同时存在的敌怪数量</summary>
        private const int MaxInvasionEnemies = 25;

        /// <summary>每次最多生成数量</summary>
        private const int MaxSpawnPerCheck = 5;

        /// <summary>生成距离范围（最小）</summary>
        private const float MinSpawnDistance = 400f;

        /// <summary>生成距离范围（最大）</summary>
        private const float MaxSpawnDistance = 1000f;

        /// <summary>入侵影响范围半径（像素）——对天柱中心点</summary>
        public const float InvasionRadius = 4000f;

        /// <summary>初始波次生成数量</summary>
        private const int InitialWaveSpawnCount = 8;

        #endregion

        #region 状态数据

        /// <summary>入侵事件是否正在进行</summary>
        public static bool InvasionActive { get; private set; } = false;

        /// <summary>当前入侵积分（击杀敌怪累积）</summary>
        public static int InvasionPoints { get; private set; } = 0;

        /// <summary>入侵进度百分比（0-100）</summary>
        public static int InvasionProgress => InvasionActive ? (int)(InvasionPoints * 100f / MaxInvasionPoints) : 0;

        /// <summary>当前波次（公开访问，供UI使用）</summary>
        public static int CurrentWave => currentWave;

        /// <summary>当前波次内的进度百分比（0-1）</summary>
        public static float CurrentWaveProgress {
            get {
                if (!InvasionActive || currentWave <= 0) return 0f;
                int waveStart = (currentWave - 1) * PointsPerWave;
                int waveEnd = currentWave * PointsPerWave;
                float progress = (float)(InvasionPoints - waveStart) / (waveEnd - waveStart);
                return MathHelper.Clamp(progress, 0f, 1f);
            }
        }

        /// <summary>入侵事件中心坐标（四柱中心）</summary>
        public static Vector2 InvasionCenter { get; private set; } = Vector2.Zero;

        /// <summary>入侵用天柱位置（4根）</summary>
        public static Vector2[] InvasionPillarPositions { get; private set; } = new Vector2[4];

        /// <summary>入侵用天柱Actor索引</summary>
        public static int[] InvasionPillarActorIndices { get; private set; } = new int[4];

        /// <summary>生成计时器</summary>
        private static int spawnTimer = 0;

        /// <summary>事件波次提示计时器</summary>
        private static int waveMessageTimer = 0;

        /// <summary>当前波次</summary>
        private static int currentWave = 0;

        #endregion

        #region 生命周期

        public override void OnWorldLoad() {
            ResetInvasion();
        }

        public override void OnWorldUnload() {
            ResetInvasion();
        }

        private static void ResetInvasion() {
            InvasionActive = false;
            InvasionPoints = 0;
            InvasionCenter = Vector2.Zero;
            InvasionPillarPositions = new Vector2[4];
            InvasionPillarActorIndices = new int[4];
            for (int i = 0; i < 4; i++) {
                InvasionPillarActorIndices[i] = -1;
            }
            spawnTimer = 0;
            waveMessageTimer = 0;
            currentWave = 0;
        }

        public override void PostUpdateWorld() {
            if (!InvasionActive) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // 检查入侵进度
            UpdateInvasionProgress();

            // 维护天柱存在
            ValidateInvasionPillars();

            // 生成敌怪
            UpdateEnemySpawning();

            // 波次提示
            UpdateWaveMessages();
        }

        #endregion

        #region 入侵启动与结束

        /// <summary>
        /// 启动天庭入侵事件
        /// </summary>
        /// <param name="triggerPosition">触发位置（通常为玩家位置）</param>
        public static void StartInvasion(Vector2 triggerPosition) {
            if (InvasionActive) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            InvasionActive = true;
            InvasionPoints = 0;
            currentWave = 1;
            spawnTimer = SpawnCheckInterval; // 设置为满值，下一帧立刻触发生成
            waveMessageTimer = 0;

            // 计算中心位置
            InvasionCenter = triggerPosition;

            // 计算四根天柱的位置（以触发点为中心，四个方向分布）
            CalculateInvasionPillarPositions(triggerPosition);

            // 生成天柱Actor
            SpawnInvasionPillars();

            // 生成天柱降临粒子效果
            SpawnPillarDescendEffects();

            // 立刻生成初始波次敌怪
            SpawnInitialWave(triggerPosition);

            // 广播事件开始
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.WorldData);
            }

            // 显示入侵开始提示
            Main.NewText("天庭入侵！四方天柱降临，神兵天将蜂拥而至！", 255, 200, 50);
        }

        /// <summary>
        /// 天柱降临时的粒子效果
        /// </summary>
        private static void SpawnPillarDescendEffects() {
            if (Main.dedServ) return;

            for (int i = 0; i < 4; i++) {
                Vector2 pillarPos = InvasionPillarPositions[i];
                if (pillarPos == Vector2.Zero) continue;

                // 每根天柱位置生成大量粒子
                for (int j = 0; j < 40; j++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
                    int dust = Dust.NewDust(pillarPos, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].fadeIn = 2f;
                }

                // 祥云效果
                for (int j = 0; j < 15; j++) {
                    Vector2 cloudVel = Main.rand.NextVector2Circular(4f, 4f);
                    int cloud = Dust.NewDust(pillarPos, 0, 0, DustID.Cloud, cloudVel.X, cloudVel.Y, 200, default, 4f);
                    Main.dust[cloud].noGravity = true;
                }

                // 光柱粒子从天而降
                for (int j = 0; j < 20; j++) {
                    Vector2 lightPos = pillarPos + new Vector2(Main.rand.NextFloat(-100, 100), Main.rand.NextFloat(-500, 0));
                    int light = Dust.NewDust(lightPos, 0, 0, DustID.WhiteTorch, 0, 3f, 150, default, 2f);
                    Main.dust[light].noGravity = true;
                }
            }
        }

        /// <summary>
        /// 在入侵开始时立刻生成初始波次的敌怪
        /// </summary>
        private static void SpawnInitialWave(Vector2 center) {
            int spawned = 0;

            for (int attempt = 0; attempt < 40 && spawned < InitialWaveSpawnCount; attempt++) {
                // 在触发位置周围随机生成
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(MinSpawnDistance, MaxSpawnDistance);
                Vector2 spawnPos = center + angle.ToRotationVector2() * distance;

                int tileX = (int)(spawnPos.X / 16f);
                int tileY = (int)(spawnPos.Y / 16f);

                if (tileX < 50 || tileX > Main.maxTilesX - 50) continue;
                if (tileY < 50 || tileY > Main.maxTilesY - 50) continue;

                Tile tile = Main.tile[tileX, tileY];
                if (tile.HasTile && Main.tileSolid[tile.TileType]) continue;

                // 选择敌怪类型
                int npcType;
                int roll = Main.rand.Next(100);
                if (roll < 15) npcType = ModContent.NPCType<Xianglong>();
                else if (roll < 35) npcType = ModContent.NPCType<HeavenObserver>();
                else if (roll < 50) npcType = ModContent.NPCType<OndPaladin>();
                else npcType = ModContent.NPCType<BronzedivineBird>();

                var source = new EntitySource_SpawnNPC();
                int npcIndex = NPC.NewNPC(source, (int)spawnPos.X, (int)spawnPos.Y, npcType);
                if (npcIndex >= 0 && npcIndex < Main.maxNPCs) {
                    Main.npc[npcIndex].target = Main.myPlayer;
                    SpawnInvasionEffect(spawnPos);
                    spawned++;

                    if (Main.netMode == NetmodeID.Server) {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);
                    }
                }
            }

            if (spawned > 0) {
                Main.NewText($"第一波天兵到达！[{spawned}只]", 255, 230, 100);
            }
        }

        /// <summary>
        /// 结束天庭入侵事件
        /// </summary>
        private static void EndInvasion() {
            if (!InvasionActive) return;

            InvasionActive = false;

            // 移除入侵天柱
            DespawnInvasionPillars();

            // 标记入侵完成
            DownedBossSystem.downedHeavenInvasion = true;

            // 广播事件结束
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.WorldData);
            }

            // 显示入侵完成提示
            Main.NewText("天庭入侵已被击退！神圣碎片散落大地……", 100, 255, 100);
        }

        #endregion

        #region 天柱位置计算与管理

        /// <summary>
        /// 计算入侵天柱的初始位置
        /// 四根天柱以触发点为中心，形成菱形分布
        /// </summary>
        private static void CalculateInvasionPillarPositions(Vector2 center) {
            float worldWidth = Main.maxTilesX * 16f;

            // 东（右）
            float eastX = MathHelper.Clamp(center.X + PillarSpacingPixels, 200 * 16f, (Main.maxTilesX - 200) * 16f);
            // 西（左）
            float westX = MathHelper.Clamp(center.X - PillarSpacingPixels, 200 * 16f, (Main.maxTilesX - 200) * 16f);
            // 南（下）
            float southX = MathHelper.Clamp(center.X + PillarSpacingPixels * 0.5f, 200 * 16f, (Main.maxTilesX - 200) * 16f);
            // 北（上）
            float northX = MathHelper.Clamp(center.X - PillarSpacingPixels * 0.5f, 200 * 16f, (Main.maxTilesX - 200) * 16f);

            // 查找地表并悬停
            InvasionPillarPositions[0] = GetHoverPosition(eastX);   // 东方天柱
            InvasionPillarPositions[1] = GetHoverPosition(southX);  // 南方天柱
            InvasionPillarPositions[2] = GetHoverPosition(westX);   // 西方天柱
            InvasionPillarPositions[3] = GetHoverPosition(northX);  // 北方天柱

            // 更新中心为四柱平均位置
            InvasionCenter = (InvasionPillarPositions[0] + InvasionPillarPositions[1] +
                              InvasionPillarPositions[2] + InvasionPillarPositions[3]) / 4f;
        }

        /// <summary>
        /// 获取指定X坐标的悬停位置（在地表上方）
        /// </summary>
        private static Vector2 GetHoverPosition(float worldX) {
            int tileX = (int)(worldX / 16f);
            int surfaceY = FindSurfaceY(tileX);
            return new Vector2(worldX, surfaceY * 16f - PillarHoverHeight);
        }

        /// <summary>
        /// 查找指定X坐标的地表Y坐标
        /// </summary>
        private static int FindSurfaceY(int tileX) {
            for (int y = 100; y < Main.maxTilesY - 200; y++) {
                Tile tile = Main.tile[tileX, y];
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return y;
                }
            }
            return (int)Main.worldSurface;
        }

        /// <summary>
        /// 生成入侵天柱Actor
        /// </summary>
        private static void SpawnInvasionPillars() {
            for (int i = 0; i < 4; i++) {
                Vector2 pos = InvasionPillarPositions[i];
                if (pos == Vector2.Zero) continue;

                int actorId = ActorLoader.GetActorID<HeavenPillarActor>();
                int slot = ActorLoader.NewActor(actorId, pos, Vector2.Zero);

                if (slot >= 0 && slot < ActorLoader.MaxActorCount) {
                    InvasionPillarActorIndices[i] = slot;

                    Actor actor = ActorLoader.Actors[slot];
                    if (actor is HeavenPillarActor pillar) {
                        pillar.PillarStyleIndex = i;
                    }
                }
            }
        }

        /// <summary>
        /// 移除入侵天柱Actor
        /// </summary>
        private static void DespawnInvasionPillars() {
            for (int i = 0; i < 4; i++) {
                int actorIndex = InvasionPillarActorIndices[i];
                if (actorIndex >= 0 && actorIndex < ActorLoader.MaxActorCount) {
                    Actor actor = ActorLoader.Actors[actorIndex];
                    if (actor != null && actor.Active && actor is HeavenPillarActor) {
                        actor.Active = false;
                    }
                }
                InvasionPillarActorIndices[i] = -1;
            }
        }

        /// <summary>
        /// 维护入侵天柱的存在——丢失时重新生成
        /// </summary>
        private static void ValidateInvasionPillars() {
            for (int i = 0; i < 4; i++) {
                int actorIndex = InvasionPillarActorIndices[i];
                Vector2 pos = InvasionPillarPositions[i];
                if (pos == Vector2.Zero) continue;

                bool needsRestore = false;

                if (actorIndex < 0 || actorIndex >= ActorLoader.MaxActorCount) {
                    needsRestore = true;
                }
                else {
                    Actor actor = ActorLoader.Actors[actorIndex];
                    if (actor == null || !actor.Active || actor is not HeavenPillarActor) {
                        needsRestore = true;
                    }
                }

                if (needsRestore) {
                    int actorId = ActorLoader.GetActorID<HeavenPillarActor>();
                    int slot = ActorLoader.NewActor(actorId, pos, Vector2.Zero);

                    if (slot >= 0 && slot < ActorLoader.MaxActorCount) {
                        InvasionPillarActorIndices[i] = slot;

                        Actor actor = ActorLoader.Actors[slot];
                        if (actor is HeavenPillarActor pillar) {
                            pillar.PillarStyleIndex = i;
                            pillar.HasDescended = true;
                        }
                    }
                }
            }
        }

        #endregion

        #region 入侵进度

        /// <summary>
        /// 入侵事件中击杀敌怪加分
        /// </summary>
        public static void AddInvasionPoints(int points) {
            if (!InvasionActive) return;
            InvasionPoints += points;
        }

        /// <summary>
        /// 更新入侵进度，检查是否完成
        /// </summary>
        private static void UpdateInvasionProgress() {
            if (InvasionPoints >= MaxInvasionPoints) {
                EndInvasion();
            }
        }

        /// <summary>
        /// 波次提示——根据积分动态计算当前波次（共20波）
        /// </summary>
        private static void UpdateWaveMessages() {
            waveMessageTimer++;

            // 根据积分计算当前应处的波次
            int newWave = Math.Clamp(InvasionPoints / PointsPerWave + 1, 1, TotalWaves);

            if (newWave > currentWave) {
                currentWave = newWave;

                // 每波都显示提示，关键波次显示特殊消息
                string waveMessage = currentWave switch {
                    <= 3 => $"第{currentWave}波：天兵先锋部队来袭！",
                    <= 6 => $"第{currentWave}波：天庭加派增援！",
                    <= 9 => $"第{currentWave}波：天庭精锐部队到达战场！",
                    10 => "第10波：入侵已过半——天将们发起猛攻！",
                    <= 13 => $"第{currentWave}波：天庭源源不断地派出天将！",
                    <= 16 => $"第{currentWave}波：强大的天庭战力集结！",
                    <= 19 => $"第{currentWave}波：天庭倾尽全力！坚持住！",
                    20 => "最终波：天庭最后的力量正在集结！",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(waveMessage)) {
                    Main.NewText(waveMessage, 255, 220, 80);
                }
            }
        }

        #endregion

        #region 敌怪生成

        /// <summary>
        /// 更新敌怪生成逻辑
        /// </summary>
        private static void UpdateEnemySpawning() {
            spawnTimer++;
            if (spawnTimer < SpawnCheckInterval) return;
            spawnTimer = 0;

            // 检查当前入侵敌怪数量
            int currentCount = CountInvasionEnemies();
            if (currentCount >= MaxInvasionEnemies) return;

            // 根据波次调整数量上限——随波次递增
            int adjustedMax = MaxInvasionEnemies + (currentWave / 4) * 5; // 每4波+5
            if (currentCount >= adjustedMax) return;

            // 为每个活跃玩家尝试生成（入侵是全局事件，不需要玩家在天柱范围内）
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || !player.active) continue;

                int canSpawn = Math.Min(MaxSpawnPerCheck, adjustedMax - currentCount);
                TrySpawnInvasionEnemies(player, canSpawn);
                currentCount = CountInvasionEnemies(); // 刷新计数
                if (currentCount >= adjustedMax) break;
            }
        }

        /// <summary>
        /// 统计当前入侵敌怪数量
        /// </summary>
        private static int CountInvasionEnemies() {
            int count = 0;
            int xianglongType = ModContent.NPCType<Xianglong>();
            int observerType = ModContent.NPCType<HeavenObserver>();
            int paladinType = ModContent.NPCType<OndPaladin>();
            int birdType = ModContent.NPCType<BronzedivineBird>();

            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == xianglongType || npc.type == observerType ||
                    npc.type == paladinType || npc.type == birdType) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 尝试为指定玩家生成入侵敌怪
        /// </summary>
        private static void TrySpawnInvasionEnemies(Player player, int maxCount) {
            int spawned = 0;

            for (int attempt = 0; attempt < 20 && spawned < maxCount; attempt++) {
                Vector2 spawnPos = FindInvasionSpawnPosition(player);
                if (spawnPos == Vector2.Zero) continue;

                int npcType = ChooseInvasionEnemyType(spawnPos);
                if (npcType == -1) continue;

                var source = new EntitySource_SpawnNPC();
                int npcIndex = NPC.NewNPC(source, (int)spawnPos.X, (int)spawnPos.Y, npcType);
                if (npcIndex >= 0 && npcIndex < Main.maxNPCs) {
                    NPC npc = Main.npc[npcIndex];
                    npc.target = player.whoAmI;

                    // 让NPC朝向玩家
                    npc.direction = player.Center.X > npc.Center.X ? 1 : -1;
                    npc.spriteDirection = npc.direction;

                    // 生成视觉效果
                    SpawnInvasionEffect(spawnPos);
                    spawned++;

                    if (Main.netMode == NetmodeID.Server) {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);
                    }
                }
            }
        }

        /// <summary>
        /// 寻找入侵生成位置
        /// </summary>
        private static Vector2 FindInvasionSpawnPosition(Player player) {
            for (int i = 0; i < 30; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(MinSpawnDistance, MaxSpawnDistance);
                Vector2 offset = angle.ToRotationVector2() * distance;
                Vector2 testPos = player.Center + offset;

                int tileX = (int)(testPos.X / 16f);
                int tileY = (int)(testPos.Y / 16f);

                if (tileX < 50 || tileX > Main.maxTilesX - 50) continue;
                if (tileY < 50 || tileY > Main.maxTilesY - 50) continue;

                Tile tile = Main.tile[tileX, tileY];
                if (tile.HasTile && Main.tileSolid[tile.TileType]) continue;

                // 确保在屏幕外自然生成（不会突然出现在玩家面前）
                float distToPlayer = Vector2.Distance(testPos, player.Center);
                if (distToPlayer < 400f) continue;

                // 地面敌怪概率
                bool isGroundSpawn = Main.rand.NextBool(4);
                if (isGroundSpawn) {
                    int groundY = FindGround(tileX, tileY);
                    if (groundY == -1) continue;
                    testPos = new Vector2(tileX * 16f + 8f, groundY * 16f - 32f);
                }

                return testPos;
            }

            // 备选：如果找不到合适位置，直接在玩家头顶上方生成（飞行怪可以用）
            float fallbackX = player.Center.X + Main.rand.NextFloat(-600, 600);
            float fallbackY = player.Center.Y - Main.rand.NextFloat(400, 800);
            return new Vector2(fallbackX, Math.Max(100 * 16f, fallbackY));
        }

        /// <summary>
        /// 从指定位置向下搜索地面
        /// </summary>
        private static int FindGround(int tileX, int startY) {
            for (int y = startY; y < Math.Min(startY + 30, Main.maxTilesY - 50); y++) {
                Tile tile = Main.tile[tileX, y];
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    return y;
                }
            }
            return -1;
        }

        /// <summary>
        /// 根据当前波次和位置选择敌怪类型
        /// 随着波次推进，强力敌怪的出现概率逐渐增加
        /// </summary>
        private static int ChooseInvasionEnemyType(Vector2 position) {
            int tileX = (int)(position.X / 16f);
            int tileY = (int)(position.Y / 16f);
            bool hasGround = false;

            for (int y = tileY; y < Math.Min(tileY + 5, Main.maxTilesY - 50); y++) {
                Tile tile = Main.tile[tileX, y];
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    hasGround = true;
                    break;
                }
            }

            // 地面敌怪：金甲圣骑
            if (hasGround && Main.rand.NextBool(3)) {
                return ModContent.NPCType<OndPaladin>();
            }

            // 飞行敌怪——概率随波次线性递增
            // 翔龙概率：10% → 40%（波次1→20）
            // 天眼概率：20% → 30%
            // 铜羽神鸟概率：70% → 30%
            float waveRatio = (currentWave - 1f) / (TotalWaves - 1f); // 0~1
            int xianglongChance = (int)MathHelper.Lerp(10, 40, waveRatio);
            int observerChance = (int)MathHelper.Lerp(20, 30, waveRatio);

            int roll = Main.rand.Next(100);

            if (roll < xianglongChance) return ModContent.NPCType<Xianglong>();
            if (roll < xianglongChance + observerChance) return ModContent.NPCType<HeavenObserver>();
            return ModContent.NPCType<BronzedivineBird>();
        }

        /// <summary>
        /// 入侵敌怪生成视觉效果
        /// </summary>
        private static void SpawnInvasionEffect(Vector2 position) {
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                int dust = Dust.NewDust(position, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
            for (int i = 0; i < 8; i++) {
                Vector2 cloudVel = Main.rand.NextVector2Circular(3f, 3f);
                int cloud = Dust.NewDust(position, 0, 0, DustID.Cloud, cloudVel.X, cloudVel.Y, 200, default, 3f);
                Main.dust[cloud].noGravity = true;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查指定位置是否在入侵范围内
        /// </summary>
        public static bool IsInInvasionRange(Vector2 position) {
            if (!InvasionActive) return false;

            // 检查是否在任意天柱的影响范围内
            for (int i = 0; i < 4; i++) {
                if (InvasionPillarPositions[i] == Vector2.Zero) continue;
                if (Vector2.Distance(position, InvasionPillarPositions[i]) <= InvasionRadius) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取距离指定位置最近的入侵天柱索引
        /// </summary>
        public static int GetNearestInvasionPillar(Vector2 position) {
            if (!InvasionActive) return -1;

            int nearest = -1;
            float minDist = float.MaxValue;

            for (int i = 0; i < 4; i++) {
                if (InvasionPillarPositions[i] == Vector2.Zero) continue;
                float dist = Vector2.Distance(position, InvasionPillarPositions[i]);
                if (dist < minDist) {
                    minDist = dist;
                    nearest = i;
                }
            }
            return nearest;
        }

        #endregion
    }

    /// <summary>
    /// 入侵事件GlobalNPC——用于追踪击杀积分和调整生成率
    /// </summary>
    public class HeavenInvasionGlobalNPC : GlobalNPC
    {
        public override void OnKill(NPC npc) {
            if (!HeavenInvasionSystem.InvasionActive) return;
            if (!HeavenInvasionSystem.IsInInvasionRange(npc.Center)) return;

            // 根据敌怪类型给予不同积分
            int points = 0;
            if (npc.type == ModContent.NPCType<BronzedivineBird>()) points = 1;
            else if (npc.type == ModContent.NPCType<HeavenObserver>()) points = 2;
            else if (npc.type == ModContent.NPCType<OndPaladin>()) points = 2;
            else if (npc.type == ModContent.NPCType<Xianglong>()) points = 3;

            if (points > 0) {
                HeavenInvasionSystem.AddInvasionPoints(points);
            }
        }

        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            if (HeavenInvasionSystem.InvasionActive && HeavenInvasionSystem.IsInInvasionRange(player.Center)) {
                // 入侵期间大幅增加生成率
                spawnRate = (int)(spawnRate * 0.3f);
                maxSpawns = (int)(maxSpawns * 2.5f);
            }
        }

        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo) {
            if (!HeavenInvasionSystem.InvasionActive) return;
            if (!HeavenInvasionSystem.IsInInvasionRange(spawnInfo.Player.Center)) return;

            // 入侵期间清空普通敌怪，只生成天庭敌怪
            var keysToModify = new List<int>(pool.Keys);
            foreach (int key in keysToModify) {
                pool[key] *= 0.1f; // 大幅降低普通敌怪
            }

            pool[ModContent.NPCType<BronzedivineBird>()] = 0.5f;
            pool[ModContent.NPCType<HeavenObserver>()] = 0.3f;
            pool[ModContent.NPCType<Xianglong>()] = 0.2f;

            if (!spawnInfo.Sky && !spawnInfo.Water) {
                pool[ModContent.NPCType<OndPaladin>()] = 0.25f;
            }
        }
    }
}
