using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Enemys
{
    /// <summary>
    /// 地府敌怪生成系统
    /// 管理地府区域内特殊敌怪的生成
    /// </summary>
    public class UnderworldEnemySpawnSystem : ModSystem
    {
        #region 常量配置
        /// <summary>生成检查间隔（帧）</summary>
        private const int SpawnCheckInterval = 90;

        /// <summary>最大同时存在的地府敌怪数量</summary>
        private const int MaxUnderworldEnemies = 10;

        /// <summary>每次生成的最大数量</summary>
        private const int MaxSpawnPerCheck = 2;

        /// <summary>生成距离范围（最小）</summary>
        private const float MinSpawnDistance = 500f;

        /// <summary>生成距离范围（最大）</summary>
        private const float MaxSpawnDistance = 1000f;

        /// <summary>地府深度阈值（以世界底部为基准）</summary>
        private const int UnderworldDepthFromBottom = 200;
        #endregion

        #region 状态
        private int spawnTimer = 0;
        #endregion

        public override void PostUpdateWorld() {
            // 服务器端处理生成
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            spawnTimer++;
            if (spawnTimer < SpawnCheckInterval) return;
            spawnTimer = 0;

            // 对每个玩家检查生成
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || !player.active) continue;

                // 检查玩家是否在地府区域内
                if (!IsInUnderworldRegion(player)) continue;

                // 检查当前地府敌怪数量
                int currentEnemyCount = CountUnderworldEnemies();
                if (currentEnemyCount >= MaxUnderworldEnemies) continue;

                // 尝试生成敌怪
                TrySpawnEnemies(player, MaxUnderworldEnemies - currentEnemyCount);
            }
        }

        /// <summary>
        /// 检查位置是否在地府区域
        /// </summary>
        public static bool IsInUnderworldRegion(Player player) => UnderworldFogEffect.IsActive(player);

        /// <summary>
        /// 统计当前存在的地府敌怪数量
        /// </summary>
        private static int CountUnderworldEnemies() {
            int count = 0;
            int yakshaType = ModContent.NPCType<Yaksha>();
            int deceasedType = ModContent.NPCType<TheDeceasedPerson>();
            int bonesType = ModContent.NPCType<ThebonesinTheTomb>();
            int harvesterType = ModContent.NPCType<SoulHarvester>();

            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == yakshaType || npc.type == deceasedType ||
                    npc.type == bonesType || npc.type == harvesterType) {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 尝试为指定玩家生成敌怪
        /// </summary>
        private static void TrySpawnEnemies(Player player, int maxCount) {
            int spawned = 0;

            for (int attempt = 0; attempt < 15 && spawned < Math.Min(maxCount, MaxSpawnPerCheck); attempt++) {
                // 随机选择生成位置
                Vector2 spawnPos = FindSpawnPosition(player);
                if (spawnPos == Vector2.Zero) continue;

                // 随机选择敌怪类型
                int npcType = ChooseEnemyType(player);
                if (npcType == -1) continue;

                // 生成敌怪
                int npcIndex = NPC.NewNPC(null, (int)spawnPos.X, (int)spawnPos.Y, npcType);
                if (npcIndex >= 0 && npcIndex < Main.maxNPCs) {
                    NPC npc = Main.npc[npcIndex];
                    npc.target = player.whoAmI;

                    // 生成粒子效果
                    SpawnEffect(spawnPos, npcType);

                    spawned++;

                    if (Main.netMode == NetmodeID.Server) {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);
                    }
                }
            }
        }

        /// <summary>
        /// 寻找合适的生成位置
        /// </summary>
        private static Vector2 FindSpawnPosition(Player player) {
            for (int i = 0; i < 25; i++) {
                // 随机方向和距离
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(MinSpawnDistance, MaxSpawnDistance);
                Vector2 offset = angle.ToRotationVector2() * distance;
                Vector2 testPos = player.Center + offset;

                // 检查是否在世界范围内
                int tileX = (int)(testPos.X / 16f);
                int tileY = (int)(testPos.Y / 16f);

                if (tileX < 50 || tileX > Main.maxTilesX - 50) continue;
                if (tileY < 50 || tileY > Main.maxTilesY - 50) continue;

                // 检查是否在地府区域内
                if (!IsInUnderworldRegion(player)) continue;

                // 检查生成点是否有效（不在方块内）
                Tile tile = Main.tile[tileX, tileY];
                if (tile.HasTile && Main.tileSolid[tile.TileType]) continue;

                // 确保有一定空间
                bool hasSpace = true;
                for (int checkY = -2; checkY <= 2; checkY++) {
                    for (int checkX = -1; checkX <= 1; checkX++) {
                        int cx = tileX + checkX;
                        int cy = tileY + checkY;
                        if (cx < 0 || cx >= Main.maxTilesX || cy < 0 || cy >= Main.maxTilesY) {
                            hasSpace = false;
                            break;
                        }
                        Tile checkTile = Main.tile[cx, cy];
                        if (checkTile.HasTile && Main.tileSolid[checkTile.TileType]) {
                            hasSpace = false;
                            break;
                        }
                    }
                    if (!hasSpace) break;
                }

                if (!hasSpace) continue;

                return testPos;
            }

            return Vector2.Zero;
        }

        /// <summary>
        /// 根据玩家进度选择敌怪类型
        /// </summary>
        private static int ChooseEnemyType(Player player) {
            // 根据游戏进度调整生成概率
            bool hardMode = Main.hardMode;
            bool postPlantera = NPC.downedPlantBoss;
            bool postMoonLord = NPC.downedMoonlord;

            int roll = Main.rand.Next(100);

            if (postMoonLord) {
                // 月球领主后：摄魂使者更常见
                if (roll < 30) {
                    return ModContent.NPCType<SoulHarvester>();
                }
                else if (roll < 55) {
                    return ModContent.NPCType<ThebonesinTheTomb>();
                }
                else if (roll < 80) {
                    return ModContent.NPCType<Yaksha>();
                }
                else {
                    return ModContent.NPCType<TheDeceasedPerson>();
                }
            }
            else if (postPlantera) {
                // 世纪之花后：均衡分布
                if (roll < 25) {
                    return ModContent.NPCType<SoulHarvester>();
                }
                else if (roll < 50) {
                    return ModContent.NPCType<ThebonesinTheTomb>();
                }
                else if (roll < 75) {
                    return ModContent.NPCType<Yaksha>();
                }
                else {
                    return ModContent.NPCType<TheDeceasedPerson>();
                }
            }
            else if (hardMode) {
                // 困难模式：基础敌怪为主
                if (roll < 10) {
                    return ModContent.NPCType<SoulHarvester>();
                }
                else if (roll < 35) {
                    return ModContent.NPCType<ThebonesinTheTomb>();
                }
                else if (roll < 65) {
                    return ModContent.NPCType<Yaksha>();
                }
                else {
                    return ModContent.NPCType<TheDeceasedPerson>();
                }
            }
            else {
                // 普通模式：只生成较弱的敌怪
                if (roll < 40) {
                    return ModContent.NPCType<TheDeceasedPerson>();
                }
                else if (roll < 80) {
                    return ModContent.NPCType<Yaksha>();
                }
                else {
                    return ModContent.NPCType<ThebonesinTheTomb>();
                }
            }
        }

        /// <summary>
        /// 生成视觉效果
        /// </summary>
        private static void SpawnEffect(Vector2 position, int npcType) {
            int dustType;
            Color dustColor;

            if (npcType == ModContent.NPCType<Yaksha>()) {
                dustType = DustID.Torch;
                dustColor = new Color(255, 100, 50);
            }
            else if (npcType == ModContent.NPCType<TheDeceasedPerson>()) {
                dustType = DustID.SpectreStaff;
                dustColor = new Color(100, 150, 255);
            }
            else if (npcType == ModContent.NPCType<ThebonesinTheTomb>()) {
                dustType = DustID.Bone;
                dustColor = new Color(200, 200, 180);
            }
            else {
                dustType = DustID.Shadowflame;
                dustColor = new Color(150, 80, 200);
            }

            // 幽暗光效
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                int dust = Dust.NewDust(position, 0, 0, dustType, vel.X, vel.Y, 100, dustColor, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            // 黑暗烟雾效果
            for (int i = 0; i < 4; i++) {
                Vector2 smokeVel = Main.rand.NextVector2Circular(2f, 2f);
                int smoke = Dust.NewDust(position, 0, 0, DustID.Smoke, smokeVel.X, smokeVel.Y, 150, Color.Black, 2f);
                Main.dust[smoke].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 地府区域全局NPC修改
    /// 增加地府区域敌怪的基础生成率
    /// </summary>
    public class UnderworldEnemyGlobalNPC : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            // 在地府区域增加敌怪生成率
            if (UnderworldEnemySpawnSystem.IsInUnderworldRegion(player)) {
                // 降低生成间隔（增加生成率）
                spawnRate = (int)(spawnRate * 0.7f);
                // 增加最大生成数量
                maxSpawns = (int)(maxSpawns * 1.3f);
            }
        }

        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo) {
            // 在地府区域添加地府敌怪到生成池
            if (UnderworldEnemySpawnSystem.IsInUnderworldRegion(spawnInfo.Player)) {
                // 根据游戏进度调整
                bool hardMode = Main.hardMode;
                bool postPlantera = NPC.downedPlantBoss;

                // 添加地府敌怪
                float baseRate = hardMode ? 0.15f : 0.08f;
                float rareRate = postPlantera ? 0.1f : 0.03f;

                pool[ModContent.NPCType<Yaksha>()] = baseRate;
                pool[ModContent.NPCType<TheDeceasedPerson>()] = baseRate * 1.2f;

                if (hardMode) {
                    pool[ModContent.NPCType<ThebonesinTheTomb>()] = baseRate * 0.8f;
                }

                if (postPlantera) {
                    pool[ModContent.NPCType<SoulHarvester>()] = rareRate;
                }
            }
        }
    }

    /// <summary>
    /// 地府敌怪增益效果
    /// 地府敌怪在地府区域内获得增益
    /// </summary>
    public class UnderworldEnemyBuff : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private bool isUnderworldEnemy = false;
        private float auraTimer = 0f;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) {
            return entity.type == ModContent.NPCType<Yaksha>() ||
                   entity.type == ModContent.NPCType<TheDeceasedPerson>() ||
                   entity.type == ModContent.NPCType<ThebonesinTheTomb>() ||
                   entity.type == ModContent.NPCType<SoulHarvester>();
        }

        public override void SetDefaults(NPC entity) {
            isUnderworldEnemy = true;
        }

        public override void AI(NPC npc) {
            if (!isUnderworldEnemy) return;

            auraTimer += 0.03f;
        }

        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers) {
            if (!isUnderworldEnemy) return;

            // 在地府区域内造成额外伤害
            if (UnderworldEnemySpawnSystem.IsInUnderworldRegion(Main.player[npc.target])) {
                modifiers.FinalDamage *= 1.1f;
            }
        }

        public override void OnKill(NPC npc) {
            if (!isUnderworldEnemy) return;

            // 击杀地府敌怪的额外奖励
            if (UnderworldEnemySpawnSystem.IsInUnderworldRegion(Main.player[npc.target])) {
                // 额外经验粒子效果
                int dustType = DustID.Shadowflame;
                if (npc.type == ModContent.NPCType<Yaksha>()) {
                    dustType = DustID.Torch;
                }
                else if (npc.type == ModContent.NPCType<TheDeceasedPerson>()) {
                    dustType = DustID.SpectreStaff;
                }
                else if (npc.type == ModContent.NPCType<ThebonesinTheTomb>()) {
                    dustType = DustID.Bone;
                }

                for (int i = 0; i < 8; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                    int dust = Dust.NewDust(npc.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }
    }
}
