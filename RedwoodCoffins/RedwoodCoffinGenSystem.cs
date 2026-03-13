using InnoVault.TileProcessors;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.RedwoodCoffins
{
    /// <summary>
    /// 红木棺材生成系统 - 在月球领主后在地下生成棺材
    /// </summary>
    public class RedwoodCoffinGenSystem : ModSystem
    {
        private const int CHECK_INTERVAL = 60 * 10; // 每10秒检查一次
        private int checkTimer = 0;
        private const int MAX_COFFINS_PER_WORLD = 5; // 每个世界最多生成5个棺材
        private int coffinsGenerated = 0;

        public override void SaveWorldData(Terraria.ModLoader.IO.TagCompound tag) {
            tag["CoffinsGenerated"] = coffinsGenerated;
        }

        public override void LoadWorldData(Terraria.ModLoader.IO.TagCompound tag) {
            coffinsGenerated = tag.GetInt("CoffinsGenerated");
        }

        public override void OnWorldLoad() {
            coffinsGenerated = 0;
        }

        public override void PostUpdateWorld() {
            // 只在服务器或单机端执行
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // 检查是否击败了月球领主
            if (!NPC.downedMoonlord)
                return;

            // 检查是否已经生成了足够的棺材
            if (coffinsGenerated >= MAX_COFFINS_PER_WORLD)
                return;

            // 增加计时器
            checkTimer++;
            if (checkTimer < CHECK_INTERVAL)
                return;

            checkTimer = 0;

            if (TileProcessorLoader.TP_ID_To_InWorld_Count.TryGetValue(TileProcessorLoader.GetModuleID<RedwoodCoffinTP>(), out var num)) {
                if (num > 0) {
                    return;
                }
            }

            // 尝试在有玩家的地下区域生成棺材
            TryGenerateCoffin();
        }

        private void TryGenerateCoffin() {
            // 寻找在地下的玩家
            Player targetPlayer = null;
            foreach (Player player in Main.player) {
                if (player.active && !player.dead && player.ZoneUnderworldHeight == false) {
                    // 玩家在地下或洞穴层
                    if (player.position.Y > Main.worldSurface * 16) {
                        targetPlayer = player;
                        break;
                    }
                }
            }

            if (targetPlayer == null)
                return;

            // 在玩家周围寻找合适的生成位置
            Point playerTilePos = targetPlayer.Center.ToTileCoordinates();

            // 尝试多次寻找合适位置
            for (int attempt = 0; attempt < 100; attempt++) {
                // 在玩家周围随机一个位置（距离30-80格）
                int offsetX = Main.rand.Next(-80, 80);
                int offsetY = Main.rand.Next(-40, 40);

                if (Math.Abs(offsetX) < 30)
                    offsetX += Main.rand.NextBool() ? 30 : -30;

                int spawnX = playerTilePos.X + offsetX;
                int spawnY = playerTilePos.Y + offsetY;

                // 检查生成位置是否合适
                if (IsValidSpawnLocation(spawnX, spawnY)) {
                    // 准备生成区域
                    PrepareSpawnArea(spawnX, spawnY);

                    // 放置棺材
                    if (PlaceCoffin(spawnX, spawnY)) {
                        coffinsGenerated++;

                        // 通知玩家
                        if (Main.netMode == NetmodeID.SinglePlayer) {
                            Main.NewText("你感觉到附近的地下有股诡异的气息...", 150, 150, 200);
                        }
                        else if (Main.netMode == NetmodeID.Server) {
                            Terraria.Chat.ChatHelper.BroadcastChatMessage(
                                Terraria.Localization.NetworkText.FromLiteral("地下深处传来了阵阵阴风..."),
                                new Color(150, 150, 200)
                            );
                        }

                        return;
                    }
                }
            }
        }

        private bool IsValidSpawnLocation(int x, int y) {
            // 检查是否在世界边界内
            if (x < 100 || x > Main.maxTilesX - 100 || y < 100 || y > Main.maxTilesY - 200)
                return false;

            // 检查是否在地下或洞穴层
            if (y < Main.worldSurface)
                return false;

            // 检查是否在地狱层
            if (y > Main.maxTilesY - 200)
                return false;

            // 检查周围是否有足够的空间（9宽 x 12高，加上额外空间）
            for (int checkX = x - 1; checkX < x + 10; checkX++) {
                for (int checkY = y - 12; checkY < y + 2; checkY++) {
                    if (!WorldGen.InWorld(checkX, checkY))
                        return false;

                    Tile tile = Main.tile[checkX, checkY];

                    // 底部需要有实心方块
                    if (checkY == y) {
                        if (!tile.HasTile || !Main.tileSolid[tile.TileType])
                            return false;
                    }
                    // 上方需要有空间或可以清理
                    else if (checkY < y) {
                        // 允许存在方块，我们会清理它们
                    }
                }
            }

            return true;
        }

        private void PrepareSpawnArea(int x, int y) {
            // 清理棺材上方的空间（12格高）
            for (int clearX = x - 1; clearX < x + 10; clearX++) {
                for (int clearY = y - 13; clearY < y; clearY++) {
                    if (WorldGen.InWorld(clearX, clearY)) {
                        WorldGen.KillTile(clearX, clearY, noItem: true);
                        WorldGen.KillWall(clearX, clearY);

                        // 清除液体
                        Tile tile = Main.tile[clearX, clearY];
                        tile.LiquidAmount = 0;
                    }
                }
            }

            // 确保底部有足够的实心方块（9格宽）
            for (int baseX = x; baseX < x + 9; baseX++) {
                if (WorldGen.InWorld(baseX, y)) {
                    Tile baseTile = Main.tile[baseX, y];

                    // 如果底部没有方块，放置石块
                    if (!baseTile.HasTile || !Main.tileSolid[baseTile.TileType]) {
                        WorldGen.PlaceTile(baseX, y, TileID.Stone, forced: true);
                    }

                    // 确保底部方块是平整的（没有斜坡）
                    baseTile.Slope = SlopeType.Solid;
                    baseTile.IsHalfBlock = false;
                }
            }

            // 同步地形更改
            if (Main.netMode == NetmodeID.Server) {
                for (int syncX = x - 2; syncX < x + 11; syncX++) {
                    for (int syncY = y - 14; syncY < y + 2; syncY++) {
                        if (WorldGen.InWorld(syncX, syncY)) {
                            NetMessage.SendTileSquare(-1, syncX, syncY, 1);
                        }
                    }
                }
            }
        }

        private bool PlaceCoffin(int x, int y) {
            // 放置棺材 - 注意Origin是(5, 11)，所以需要调整放置位置
            int placeX = x + 5;
            int placeY = y - 1;

            bool success = WorldGen.PlaceObject(placeX, placeY, ModContent.TileType<RedwoodCoffinTile>(), style: 0);

            if (success) {
                //获取箱子左上角位置并创建TP实体
                if (TPUtils.TryGetTopLeft(placeX, placeY, out var point)) {
                    var tp = TileProcessorLoader.AddInWorld(ModContent.TileType<RedwoodCoffinTile>(), point, null);
                }
                // 同步棺材放置
                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendObjectPlacement(-1, placeX, placeY, ModContent.TileType<RedwoodCoffinTile>(), 0, 0, -1, -1);
                }

                // 添加一些装饰效果（火把）
                AddDecoration(x, y);
            }

            return success;
        }

        private void AddDecoration(int x, int y) {
            // 在棺材两侧随机放置一些火把
            if (Main.rand.NextBool(2)) {
                WorldGen.PlaceTile(x - 2, y - 1, TileID.Torches, mute: true, style: 0);
            }

            if (Main.rand.NextBool(2)) {
                WorldGen.PlaceTile(x + 10, y - 1, TileID.Torches, mute: true, style: 0);
            }

            // 同步装饰
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendTileSquare(-1, x - 3, y - 2, 15);
            }
        }
    }
}
