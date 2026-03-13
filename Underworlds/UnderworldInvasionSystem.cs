using AncientChineseMythology.Systems;
using AncientChineseMythology.Underworlds.Enemys;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds
{
    /// <summary>
    /// 地府入侵事件系统
    /// 
    /// 事件触发后：
    /// - 四座鬼门在事件区域四方展开
    /// - 不断刷新地府敌怪（夜叉、死者、墓中骸骨、摄魂使者）
    /// - 玩家需要击杀足够数量的敌怪来推进事件进度
    /// - 共20波，进度达到100%后事件结束
    /// </summary>
    public class UnderworldInvasionSystem : ModSystem
    {
        #region 常量配置

        /// <summary>鬼门之间的间距（像素）</summary>
        private const float GateSpacingPixels = 1200f;

        /// <summary>入侵事件完成所需击杀积分</summary>
        public const int MaxInvasionPoints = 1200;

        /// <summary>总波次数</summary>
        public const int TotalWaves = 20;

        /// <summary>每波所需积分 = MaxInvasionPoints / TotalWaves</summary>
        public static int PointsPerWave => MaxInvasionPoints / TotalWaves;

        /// <summary>敌怪生成检查间隔（帧）</summary>
        private const int SpawnCheckInterval = 18;

        /// <summary>入侵期间最大同时存在的敌怪数量</summary>
        private const int MaxInvasionEnemies = 22;

        /// <summary>每次最多生成数量</summary>
        private const int MaxSpawnPerCheck = 4;

        /// <summary>生成距离范围（最小）</summary>
        private const float MinSpawnDistance = 400f;

        /// <summary>生成距离范围（最大）</summary>
        private const float MaxSpawnDistance = 900f;

        /// <summary>入侵影响范围半径（像素）</summary>
        public const float InvasionRadius = 3500f;

        /// <summary>初始波次生成数量</summary>
        private const int InitialWaveSpawnCount = 6;

        /// <summary>鬼门数量</summary>
        private const int GateCount = 4;

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

        /// <summary>入侵事件中心坐标</summary>
        public static Vector2 InvasionCenter { get; private set; } = Vector2.Zero;

        /// <summary>四座鬼门位置</summary>
        public static Vector2[] GatePositions { get; private set; } = new Vector2[GateCount];

        /// <summary>生成计时器</summary>
        private static int spawnTimer = 0;

        /// <summary>事件波次提示计时器</summary>
        private static int waveMessageTimer = 0;

        /// <summary>当前波次</summary>
        private static int currentWave = 0;

        /// <summary>鬼门粒子计时器</summary>
        private static int gateParticleTimer = 0;

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
            GatePositions = new Vector2[GateCount];
            spawnTimer = 0;
            waveMessageTimer = 0;
            currentWave = 0;
            gateParticleTimer = 0;
        }

        public override void PostUpdateWorld() {
            if (!InvasionActive) return;
            UnderworldPlayer.UnderworldEffect = true;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // 检查入侵进度
            UpdateInvasionProgress();

            // 鬼门粒子效果
            UpdateGateEffects();

            // 生成敌怪
            UpdateEnemySpawning();

            // 波次提示
            UpdateWaveMessages();
        }

        #endregion

        #region 入侵启动与结束

        /// <summary>
        /// 启动地府入侵事件
        /// </summary>
        /// <param name="triggerPosition">触发位置（通常为玩家位置）</param>
        public static void StartInvasion(Vector2 triggerPosition) {
            if (InvasionActive) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            InvasionActive = true;
            InvasionPoints = 0;
            currentWave = 1;
            spawnTimer = SpawnCheckInterval; // 下一帧立刻触发生成
            waveMessageTimer = 0;
            gateParticleTimer = 0;

            // 计算中心位置
            InvasionCenter = triggerPosition;

            // 计算四座鬼门的位置
            CalculateGatePositions(triggerPosition);

            // 生成鬼门开启粒子效果
            SpawnGateOpenEffects();

            // 立刻生成初始波次敌怪
            SpawnInitialWave(triggerPosition);

            // 广播事件开始
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.WorldData);
            }

            // 显示入侵开始提示
            Main.NewText("地府入侵！四方鬼门大开，亡灵邪魅蜂拥而出！", 180, 100, 255);
        }

        /// <summary>
        /// 鬼门开启时的粒子效果
        /// </summary>
        private static void SpawnGateOpenEffects() {
            if (Main.dedServ) return;

            for (int i = 0; i < GateCount; i++) {
                Vector2 gatePos = GatePositions[i];
                if (gatePos == Vector2.Zero) continue;

                // 每座鬼门生成大量暗紫色粒子
                for (int j = 0; j < 40; j++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(7f, 7f);
                    int dust = Dust.NewDust(gatePos, 0, 0, DustID.Shadowflame, vel.X, vel.Y, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].fadeIn = 2f;
                }

                // 幽魂效果
                for (int j = 0; j < 20; j++) {
                    Vector2 ghostVel = Main.rand.NextVector2Circular(4f, 4f);
                    int ghost = Dust.NewDust(gatePos, 0, 0, DustID.SpectreStaff, ghostVel.X, ghostVel.Y, 200, default, 3f);
                    Main.dust[ghost].noGravity = true;
                }

                // 从地面涌出的暗光
                for (int j = 0; j < 20; j++) {
                    Vector2 lightPos = gatePos + new Vector2(Main.rand.NextFloat(-100, 100), Main.rand.NextFloat(0, 300));
                    int light = Dust.NewDust(lightPos, 0, 0, DustID.PurpleTorch, 0, -3f, 150, default, 2f);
                    Main.dust[light].noGravity = true;
                }
            }
        }

        /// <summary>
        /// 鬼门持续粒子效果
        /// </summary>
        private static void UpdateGateEffects() {
            if (Main.dedServ) return;

            gateParticleTimer++;
            if (gateParticleTimer < 10) return;
            gateParticleTimer = 0;

            for (int i = 0; i < GateCount; i++) {
                Vector2 gatePos = GatePositions[i];
                if (gatePos == Vector2.Zero) continue;

                // 持续冒出幽灵粒子
                for (int j = 0; j < 5; j++) {
                    Vector2 vel = Main.rand.NextVector2Circular(3f, 3f);
                    int dustType = Main.rand.NextBool() ? DustID.Shadowflame : DustID.SpectreStaff;
                    int dust = Dust.NewDust(gatePos + Main.rand.NextVector2Circular(60, 60),
                        0, 0, dustType, vel.X, vel.Y - 1f, 150, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }

        /// <summary>
        /// 在入侵开始时立刻生成初始波次的敌怪
        /// </summary>
        private static void SpawnInitialWave(Vector2 center) {
            int spawned = 0;

            for (int attempt = 0; attempt < 40 && spawned < InitialWaveSpawnCount; attempt++) {
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
                if (roll < 15) npcType = ModContent.NPCType<SoulHarvester>();
                else if (roll < 35) npcType = ModContent.NPCType<ThebonesinTheTomb>();
                else if (roll < 55) npcType = ModContent.NPCType<Yaksha>();
                else npcType = ModContent.NPCType<TheDeceasedPerson>();

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
                Main.NewText($"第一波亡灵涌出！[{spawned}只]", 180, 140, 255);
            }
        }

        /// <summary>
        /// 结束地府入侵事件
        /// </summary>
        private static void EndInvasion() {
            if (!InvasionActive) return;

            InvasionActive = false;

            // 标记入侵完成
            DownedBossSystem.downedUnderworldInvasion = true;

            // 广播事件结束
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.WorldData);
            }

            // 鬼门关闭效果
            SpawnGateCloseEffects();

            // 显示入侵完成提示
            Main.NewText("地府入侵已被击退！冥界之门重新封印……", 100, 255, 150);
        }

        /// <summary>
        /// 鬼门关闭粒子效果
        /// </summary>
        private static void SpawnGateCloseEffects() {
            if (Main.dedServ) return;

            for (int i = 0; i < GateCount; i++) {
                Vector2 gatePos = GatePositions[i];
                if (gatePos == Vector2.Zero) continue;

                for (int j = 0; j < 30; j++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                    int dust = Dust.NewDust(gatePos, 0, 0, DustID.PurpleTorch, vel.X, vel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }

        #endregion

        #region 鬼门位置计算

        /// <summary>
        /// 计算四座鬼门的位置
        /// 以触发点为中心，四个方向分布
        /// </summary>
        private static void CalculateGatePositions(Vector2 center) {
            // 东（右）
            float eastX = MathHelper.Clamp(center.X + GateSpacingPixels, 200 * 16f, (Main.maxTilesX - 200) * 16f);
            // 西（左）
            float westX = MathHelper.Clamp(center.X - GateSpacingPixels, 200 * 16f, (Main.maxTilesX - 200) * 16f);
            // 南（下偏右）
            float southX = MathHelper.Clamp(center.X + GateSpacingPixels * 0.5f, 200 * 16f, (Main.maxTilesX - 200) * 16f);
            // 北（上偏左）
            float northX = MathHelper.Clamp(center.X - GateSpacingPixels * 0.5f, 200 * 16f, (Main.maxTilesX - 200) * 16f);

            // 鬼门在地下，使用玩家附近的Y坐标偏移
            GatePositions[0] = new Vector2(eastX, center.Y + 100);
            GatePositions[1] = new Vector2(southX, center.Y + 200);
            GatePositions[2] = new Vector2(westX, center.Y + 100);
            GatePositions[3] = new Vector2(northX, center.Y - 100);

            // 更新中心为四门平均位置
            InvasionCenter = (GatePositions[0] + GatePositions[1] +
                              GatePositions[2] + GatePositions[3]) / 4f;
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

            int newWave = Math.Clamp(InvasionPoints / PointsPerWave + 1, 1, TotalWaves);

            if (newWave > currentWave) {
                currentWave = newWave;

                string waveMessage = currentWave switch {
                    <= 3 => $"第{currentWave}波：游荡的亡魂开始聚集！",
                    <= 6 => $"第{currentWave}波：地府冥兵涌出鬼门！",
                    <= 9 => $"第{currentWave}波：地府精锐鬼卒到达！",
                    10 => "第10波：入侵已过半——阴气愈发浓烈！",
                    <= 13 => $"第{currentWave}波：摄魂使者率众而来！",
                    <= 16 => $"第{currentWave}波：冥界深处的力量在觉醒！",
                    <= 19 => $"第{currentWave}波：地府倾巢而出！坚持住！",
                    20 => "最终波：冥王的最后部署——黎明将至！",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(waveMessage)) {
                    Main.NewText(waveMessage, 180, 140, 255);
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

            int currentCount = CountInvasionEnemies();
            if (currentCount >= MaxInvasionEnemies) return;

            // 敌怪上限随波次递增：每4波+4
            int adjustedMax = MaxInvasionEnemies + (currentWave / 4) * 4;
            if (currentCount >= adjustedMax) return;

            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || !player.active) continue;

                int canSpawn = Math.Min(MaxSpawnPerCheck, adjustedMax - currentCount);
                TrySpawnInvasionEnemies(player, canSpawn);
                currentCount = CountInvasionEnemies();
                if (currentCount >= adjustedMax) break;
            }
        }

        /// <summary>
        /// 统计当前入侵敌怪数量
        /// </summary>
        private static int CountInvasionEnemies() {
            int count = 0;
            int harvesterType = ModContent.NPCType<SoulHarvester>();
            int yakshaType = ModContent.NPCType<Yaksha>();
            int deceasedType = ModContent.NPCType<TheDeceasedPerson>();
            int bonesType = ModContent.NPCType<ThebonesinTheTomb>();

            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == harvesterType || npc.type == yakshaType ||
                    npc.type == deceasedType || npc.type == bonesType) {
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

                int npcType = ChooseInvasionEnemyType();
                if (npcType == -1) continue;

                var source = new EntitySource_SpawnNPC();
                int npcIndex = NPC.NewNPC(source, (int)spawnPos.X, (int)spawnPos.Y, npcType);
                if (npcIndex >= 0 && npcIndex < Main.maxNPCs) {
                    NPC npc = Main.npc[npcIndex];
                    npc.target = player.whoAmI;
                    npc.direction = player.Center.X > npc.Center.X ? 1 : -1;
                    npc.spriteDirection = npc.direction;

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

                // 确保在屏幕外
                float distToPlayer = Vector2.Distance(testPos, player.Center);
                if (distToPlayer < 400f) continue;

                // 确保有足够空间（地府在地下，不需要地面检测）
                bool hasSpace = true;
                for (int checkY = -2; checkY <= 2 && hasSpace; checkY++) {
                    for (int checkX = -1; checkX <= 1 && hasSpace; checkX++) {
                        int cx = tileX + checkX;
                        int cy = tileY + checkY;
                        if (cx < 0 || cx >= Main.maxTilesX || cy < 0 || cy >= Main.maxTilesY) {
                            hasSpace = false;
                            break;
                        }
                        Tile checkTile = Main.tile[cx, cy];
                        if (checkTile.HasTile && Main.tileSolid[checkTile.TileType]) {
                            hasSpace = false;
                        }
                    }
                }
                if (!hasSpace) continue;

                return testPos;
            }

            // 备选位置
            float fallbackX = player.Center.X + Main.rand.NextFloat(-500, 500);
            float fallbackY = player.Center.Y + Main.rand.NextFloat(-300, 300);
            return new Vector2(
                MathHelper.Clamp(fallbackX, 100 * 16f, (Main.maxTilesX - 100) * 16f),
                MathHelper.Clamp(fallbackY, 100 * 16f, (Main.maxTilesY - 100) * 16f));
        }

        /// <summary>
        /// 根据当前波次选择敌怪类型
        /// 随着波次推进，强力敌怪的出现概率逐渐增加
        /// </summary>
        private static int ChooseInvasionEnemyType() {
            // 摄魂使者概率：5% → 35%（波次1→20）
            // 墓中骸骨概率：20% → 30%
            // 夜叉概率：30% → 20%
            // 死者概率：45% → 15%
            float waveRatio = (currentWave - 1f) / (TotalWaves - 1f); // 0~1
            int harvesterChance = (int)MathHelper.Lerp(5, 35, waveRatio);
            int bonesChance = (int)MathHelper.Lerp(20, 30, waveRatio);
            int yakshaChance = (int)MathHelper.Lerp(30, 20, waveRatio);

            int roll = Main.rand.Next(100);

            if (roll < harvesterChance) return ModContent.NPCType<SoulHarvester>();
            if (roll < harvesterChance + bonesChance) return ModContent.NPCType<ThebonesinTheTomb>();
            if (roll < harvesterChance + bonesChance + yakshaChance) return ModContent.NPCType<Yaksha>();
            return ModContent.NPCType<TheDeceasedPerson>();
        }

        /// <summary>
        /// 入侵敌怪生成视觉效果
        /// </summary>
        private static void SpawnInvasionEffect(Vector2 position) {
            if (Main.dedServ) return;

            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                int dustType = Main.rand.NextBool() ? DustID.Shadowflame : DustID.PurpleTorch;
                int dust = Dust.NewDust(position, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
            for (int i = 0; i < 6; i++) {
                Vector2 smokeVel = Main.rand.NextVector2Circular(2f, 2f);
                int smoke = Dust.NewDust(position, 0, 0, DustID.Smoke, smokeVel.X, smokeVel.Y, 200, Color.Black, 2.5f);
                Main.dust[smoke].noGravity = true;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查指定位置是否在入侵范围内
        /// </summary>
        public static bool IsInInvasionRange(Vector2 position) {
            if (!InvasionActive) return false;

            for (int i = 0; i < GateCount; i++) {
                if (GatePositions[i] == Vector2.Zero) continue;
                if (Vector2.Distance(position, GatePositions[i]) <= InvasionRadius) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取距离指定位置最近的鬼门索引
        /// </summary>
        public static int GetNearestGate(Vector2 position) {
            if (!InvasionActive) return -1;

            int nearest = -1;
            float minDist = float.MaxValue;

            for (int i = 0; i < GateCount; i++) {
                if (GatePositions[i] == Vector2.Zero) continue;
                float dist = Vector2.Distance(position, GatePositions[i]);
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
    /// 地府入侵事件GlobalNPC——用于追踪击杀积分和调整生成率
    /// </summary>
    public class UnderworldInvasionGlobalNPC : GlobalNPC
    {
        public override void OnKill(NPC npc) {
            if (!UnderworldInvasionSystem.InvasionActive) return;
            if (!UnderworldInvasionSystem.IsInInvasionRange(npc.Center)) return;

            // 根据敌怪类型给予不同积分
            int points = 0;
            if (npc.type == ModContent.NPCType<TheDeceasedPerson>()) points = 1;
            else if (npc.type == ModContent.NPCType<Yaksha>()) points = 2;
            else if (npc.type == ModContent.NPCType<ThebonesinTheTomb>()) points = 2;
            else if (npc.type == ModContent.NPCType<SoulHarvester>()) points = 3;

            if (points > 0) {
                UnderworldInvasionSystem.AddInvasionPoints(points);
            }
        }

        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            if (UnderworldInvasionSystem.InvasionActive && UnderworldInvasionSystem.IsInInvasionRange(player.Center)) {
                spawnRate = (int)(spawnRate * 0.3f);
                maxSpawns = (int)(maxSpawns * 2.5f);
            }
        }

        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo) {
            if (!UnderworldInvasionSystem.InvasionActive) return;
            if (!UnderworldInvasionSystem.IsInInvasionRange(spawnInfo.Player.Center)) return;

            // 入侵期间大幅降低普通敌怪
            var keysToModify = new List<int>(pool.Keys);
            foreach (int key in keysToModify) {
                pool[key] *= 0.1f;
            }

            pool[ModContent.NPCType<TheDeceasedPerson>()] = 0.5f;
            pool[ModContent.NPCType<Yaksha>()] = 0.35f;
            pool[ModContent.NPCType<ThebonesinTheTomb>()] = 0.3f;
            pool[ModContent.NPCType<SoulHarvester>()] = 0.2f;
        }
    }
}
