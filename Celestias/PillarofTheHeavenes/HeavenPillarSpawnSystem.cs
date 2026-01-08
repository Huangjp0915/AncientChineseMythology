using AncientChineseMythology.Celestias.PillarofTheHeavenes.Enemys;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes
{
    /// <summary>
    /// 天柱区域敌怪生成系统
    /// 管理天柱区域内特殊敌怪的生成
    /// </summary>
    public class HeavenPillarSpawnSystem : ModSystem
    {
        #region 常量配置
        /// <summary>生成检查间隔（帧）</summary>
        private const int SpawnCheckInterval = 60;

        /// <summary>最大同时存在的天柱敌怪数量</summary>
        private const int MaxHeavenEnemies = 12;

        /// <summary>每次生成的最大数量</summary>
        private const int MaxSpawnPerCheck = 2;

        /// <summary>生成距离范围（最小）</summary>
        private const float MinSpawnDistance = 600f;

        /// <summary>生成距离范围（最大）</summary>
        private const float MaxSpawnDistance = 1200f;
        #endregion

        #region 状态
        private int spawnTimer = 0;
        #endregion

        public override void PostUpdateWorld() {
            // 只在天柱降临后生效
            if (!HeavenPillarSystem.PillarsDescended) return;

            // 服务器端处理生成
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            spawnTimer++;
            if (spawnTimer < SpawnCheckInterval) return;
            spawnTimer = 0;

            // 对每个玩家检查生成
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || !player.active) continue;

                // 检查玩家是否在天柱区域内
                if (!HeavenPillarSystem.IsInPillarRange(player.Center)) continue;

                // 检查当前天柱敌怪数量
                int currentEnemyCount = CountHeavenEnemies();
                if (currentEnemyCount >= MaxHeavenEnemies) continue;

                // 尝试生成敌怪
                TrySpawnEnemies(player, MaxHeavenEnemies - currentEnemyCount);
            }
        }

        /// <summary>
        /// 统计当前存在的天柱敌怪数量
        /// </summary>
        private static int CountHeavenEnemies() {
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
        /// 尝试为指定玩家生成敌怪
        /// </summary>
        private static void TrySpawnEnemies(Player player, int maxCount) {
            int spawned = 0;

            for (int attempt = 0; attempt < 10 && spawned < Math.Min(maxCount, MaxSpawnPerCheck); attempt++) {
                // 随机选择生成位置
                Vector2 spawnPos = FindSpawnPosition(player);
                if (spawnPos == Vector2.Zero) continue;

                // 随机选择敌怪类型
                int npcType = ChooseEnemyType(spawnPos);
                if (npcType == -1) continue;

                // 生成敌怪
                int npcIndex = NPC.NewNPC(null, (int)spawnPos.X, (int)spawnPos.Y, npcType);
                if (npcIndex >= 0 && npcIndex < Main.maxNPCs) {
                    NPC npc = Main.npc[npcIndex];
                    npc.target = player.whoAmI;

                    // 生成粒子效果
                    SpawnEffect(spawnPos);

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
            // 尝试找到合适的生成点
            for (int i = 0; i < 20; i++) {
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

                // 检查是否在天柱范围内
                if (!HeavenPillarSystem.IsInPillarRange(testPos)) continue;

                // 检查生成点是否有效（不在方块内）
                Tile tile = Main.tile[tileX, tileY];
                if (tile.HasTile && Main.tileSolid[tile.TileType]) continue;

                // 对于地面敌怪，需要找到地面
                bool isGroundSpawn = Main.rand.NextBool(3); // 1/3概率是地面敌怪
                if (isGroundSpawn) {
                    // 向下搜索地面
                    int groundY = FindGround(tileX, tileY);
                    if (groundY == -1) continue;
                    testPos = new Vector2(tileX * 16f + 8f, groundY * 16f - 32f);
                }

                return testPos;
            }

            return Vector2.Zero;
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
        /// 根据位置选择敌怪类型
        /// </summary>
        private static int ChooseEnemyType(Vector2 position) {
            // 检查是否在地面上
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

            // 根据情况选择敌怪
            if (hasGround && Main.rand.NextBool(3)) {
                // 地面敌怪：金甲圣骑
                return ModContent.NPCType<OndPaladin>();
            }
            else {
                // 飞行敌怪
                int roll = Main.rand.Next(100);

                if (roll < 25) {
                    // 25%: 翔龙（稀有强力）
                    return ModContent.NPCType<Xianglong>();
                }
                else if (roll < 50) {
                    // 25%: 天眼
                    return ModContent.NPCType<HeavenObserver>();
                }
                else {
                    // 50%: 铜羽神鸟（最常见）
                    return ModContent.NPCType<BronzedivineBird>();
                }
            }
        }

        /// <summary>
        /// 生成视觉效果
        /// </summary>
        private static void SpawnEffect(Vector2 position) {
            // 神圣光效
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                int dust = Dust.NewDust(position, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            // 祥云效果
            for (int i = 0; i < 5; i++) {
                Vector2 cloudVel = Main.rand.NextVector2Circular(2f, 2f);
                int cloud = Dust.NewDust(position, 0, 0, DustID.Cloud, cloudVel.X, cloudVel.Y, 200, default, 2.5f);
                Main.dust[cloud].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 天柱区域全局NPC修改
    /// 增加天柱区域敌怪的基础生成率
    /// </summary>
    public class HeavenPillarGlobalNPC : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            // 在天柱区域增加敌怪生成率
            if (HeavenPillarSystem.PillarsDescended && HeavenPillarSystem.IsInPillarRange(player.Center)) {
                // 降低生成间隔（增加生成率）
                spawnRate = (int)(spawnRate * 0.6f);
                // 增加最大生成数量
                maxSpawns = (int)(maxSpawns * 1.5f);
            }
        }

        public override void EditSpawnPool(System.Collections.Generic.IDictionary<int, float> pool, NPCSpawnInfo spawnInfo) {
            // 在天柱区域添加天柱敌怪到生成池
            if (HeavenPillarSystem.PillarsDescended && HeavenPillarSystem.IsInPillarRange(spawnInfo.Player.Center)) {
                // 清空或大幅降低普通敌怪生成
                var keysToModify = new System.Collections.Generic.List<int>(pool.Keys);
                foreach (int key in keysToModify) {
                    pool[key] *= 0.3f; // 降低普通敌怪生成率
                }

                // 添加天柱敌怪
                pool[ModContent.NPCType<BronzedivineBird>()] = 0.4f;
                pool[ModContent.NPCType<HeavenObserver>()] = 0.25f;
                pool[ModContent.NPCType<Xianglong>()] = 0.15f;

                // 地面生成时添加金甲圣骑
                if (!spawnInfo.Sky && !spawnInfo.Water) {
                    pool[ModContent.NPCType<OndPaladin>()] = 0.2f;
                }
            }
        }
    }

    /// <summary>
    /// 天柱区域敌怪Buff效果
    /// 天柱敌怪在天柱区域内获得增益
    /// </summary>
    public class HeavenPillarEnemyBuff : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private bool isHeavenEnemy = false;
        private float glowTimer = 0f;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) {
            return entity.type == ModContent.NPCType<Xianglong>() ||
                   entity.type == ModContent.NPCType<HeavenObserver>() ||
                   entity.type == ModContent.NPCType<OndPaladin>() ||
                   entity.type == ModContent.NPCType<BronzedivineBird>();
        }

        public override void SetDefaults(NPC entity) {
            isHeavenEnemy = true;
        }

        public override void AI(NPC npc) {
            if (!isHeavenEnemy) return;

            glowTimer += 0.02f;

            // 在天柱区域内获得增益
            if (HeavenPillarSystem.IsInPillarRange(npc.Center)) {
                // 生命恢复（每秒1点）
                if (Main.GameUpdateCount % 60 == 0 && npc.life < npc.lifeMax) {
                    npc.life = Math.Min(npc.life + 1, npc.lifeMax);
                }

                // 防御加成
                npc.defense += 5;
            }
        }

        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers) {
            if (!isHeavenEnemy) return;

            // 在天柱区域内造成额外伤害
            if (HeavenPillarSystem.IsInPillarRange(npc.Center)) {
                modifiers.FinalDamage *= 1.15f;
            }
        }

        public override void OnKill(NPC npc) {
            if (!isHeavenEnemy) return;

            // 击杀天柱敌怪的额外奖励
            if (HeavenPillarSystem.IsInPillarRange(npc.Center)) {
                // 额外金币
                int bonusMoney = Main.rand.Next(5000, 15000);
                Item.NewItem(npc.GetSource_Death(), npc.getRect(), ItemID.GoldCoin, bonusMoney / 10000);

                // 额外经验粒子效果
                for (int i = 0; i < 10; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                    int dust = Dust.NewDust(npc.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }
    }
}
