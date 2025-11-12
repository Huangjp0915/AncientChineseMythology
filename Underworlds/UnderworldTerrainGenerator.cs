using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace AncientChineseMythology.Underworlds
{
    /// <summary>
    /// 地府地形生成器 - 提供在地狱右侧生成地府地形的接口
    /// </summary>
    public class UnderworldTerrainGenerator : ModSystem
    {
        /// <summary>
        /// 生成地府地形的主接口
        /// </summary>
        /// <param name="seed">随机种子，默认使用当前时间</param>
        public static void GenerateUnderworldTerrain(int? seed = null) {
            int randomSeed = seed ?? (int)DateTime.Now.Ticks;
            UnifiedRandom rand = new UnifiedRandom(randomSeed);

            Main.NewText("开始生成地府地形...", Color.Purple);

            // 计算地府区域范围 - 地狱右半边
            int underworldStartX = Main.maxTilesX / 2;
            int underworldEndX = Main.maxTilesX - 200;
            int underworldStartY = Main.UnderworldLayer;
            int underworldEndY = Main.maxTilesY;

            // 验证范围有效性
            if (underworldEndX <= underworldStartX) {
                Main.NewText("错误：地图太小，无法生成地府地形", Color.Red);
                return;
            }

            if (underworldEndY <= underworldStartY) {
                Main.NewText("错误：地狱层配置异常，无法生成地府地形", Color.Red);
                return;
            }

            // 确保最小区域大小
            if (underworldEndX - underworldStartX < 500 || underworldEndY - underworldStartY < 200) {
                Main.NewText("警告：可用区域较小，地府地形可能不完整", Color.Yellow);
            }

            // 第一步：清除原有地形
            ClearHellTerrain(underworldStartX, underworldEndX, underworldStartY, underworldEndY);

            // 第二步：生成基础地形层
            GenerateBaseTerrain(underworldStartX, underworldEndX, underworldStartY, underworldEndY, rand);

            // 第三步：生成起伏的地表
            GenerateUndulatingTerrain(underworldStartX, underworldEndX, underworldStartY, underworldEndY, rand);

            // 第四步：生成洞穴和空间
            GenerateCaverns(underworldStartX, underworldEndX, underworldStartY, underworldEndY, rand);

            // 第五步：添加地府特色结构
            GenerateUnderworldStructures(underworldStartX, underworldEndX, underworldStartY, underworldEndY, rand);

            // 第六步：生成黄泉之路（贯穿的主通道）
            GenerateYellowSpringsPath(underworldStartX, underworldEndX, underworldStartY, underworldEndY, rand);

            // 第七步：平滑地形
            SmoothTerrain(underworldStartX, underworldEndX, underworldStartY, underworldEndY);

            // 第八步：添加细节装饰
            AddDetails(underworldStartX, underworldEndX, underworldStartY, underworldEndY, rand);

            Main.NewText("地府地形生成完成!", Color.LightBlue);
        }

        /// <summary>
        /// 清除地狱右半边的原有地形
        /// </summary>
        private static void ClearHellTerrain(int startX, int endX, int startY, int endY) {
            for (int i = startX; i < endX; i++) {
                for (int j = startY; j < endY; j++) {
                    Tile tile = Main.tile[i, j];
                    
                    // 保留基岩（底部边界）
                    if (j >= Main.maxTilesY - 220) {
                        continue;
                    }

                    // 清除地狱砖、灰烬等地狱方块
                    if (tile.TileType == TileID.Hellstone ||
                        tile.TileType == TileID.HellstoneBrick ||
                        tile.TileType == TileID.Ash ||
                        tile.TileType == TileID.Obsidian ||
                        tile.TileType == TileID.ObsidianBrick) {
                        
                        tile.ClearEverything();
                    }

                    // 清除熔岩
                    if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Lava) {
                        tile.LiquidAmount = 0;
                    }
                }
            }
        }

        /// <summary>
        /// 生成基础幽冥石地形层
        /// </summary>
        private static void GenerateBaseTerrain(int startX, int endX, int startY, int endY, UnifiedRandom rand) {
            int umbralStoneType = ModContent.TileType<UmbralStone>();

            for (int i = startX; i < endX; i++) {
                for (int j = startY; j < endY; j++) {
                    // 底部完全填充
                    if (j >= endY - 50) {
                        WorldGen.PlaceTile(i, j, umbralStoneType, forced: true, mute: true);
                        continue;
                    }

                    // 使用柏林噪声创建自然分布
                    float noise = GetPerlinNoise(i * 0.02f, j * 0.02f, rand);
                    
                    // 根据深度调整密度
                    float depth = (j - startY) / (float)(endY - startY);
                    float density = 0.3f + depth * 0.5f; // 越深越密集

                    if (noise > 1f - density) {
                        WorldGen.PlaceTile(i, j, umbralStoneType, forced: true, mute: true);
                    }
                }
            }
        }

        /// <summary>
        /// 生成起伏的地府地表
        /// </summary>
        private static void GenerateUndulatingTerrain(int startX, int endX, int startY, int endY, UnifiedRandom rand) {
            int umbralStoneType = ModContent.TileType<UmbralStone>();
            
            // 生成多层起伏的平台
            int platformCount = 6; // 6层不同高度的平台，象征六道轮回
            
            for (int layer = 0; layer < platformCount; layer++) {
                int baseHeight = startY + (endY - startY) / (platformCount + 1) * (layer + 1);
                
                for (int i = startX; i < endX; i++) {
                    // 使用正弦波叠加柏林噪声创建自然起伏
                    float wave = (float)Math.Sin(i * 0.05f + layer * 2f) * 10f;
                    float noise = GetPerlinNoise(i * 0.03f, layer * 10f, rand) * 15f;
                    int height = baseHeight + (int)(wave + noise);
                    
                    // 生成平台厚度
                    int thickness = rand.Next(3, 10);
                    
                    for (int j = height; j < height + thickness; j++) {
                        if (j >= startY && j < endY) {
                            WorldGen.PlaceTile(i, j, umbralStoneType, forced: true, mute: true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 生成洞穴和开放空间
        /// </summary>
        private static void GenerateCaverns(int startX, int endX, int startY, int endY, UnifiedRandom rand) {
            // 生成大型空洞 - 代表地府的各个殿堂空间
            int cavernCount = rand.Next(20, 30);
            
            for (int n = 0; n < cavernCount; n++) {
                // 确保有足够的空间生成洞穴
                int safeStartX = Math.Max(startX + 50, startX);
                int safeEndX = Math.Max(endX - 50, safeStartX + 100);
                int safeStartY = Math.Max(startY + 50, startY);
                int safeEndY = Math.Max(endY - 100, safeStartY + 100);
                
                // 如果范围不够，跳过这个洞穴
                if (safeEndX <= safeStartX || safeEndY <= safeStartY) {
                    continue;
                }
                
                int centerX = rand.Next(safeStartX, safeEndX);
                int centerY = rand.Next(safeStartY, safeEndY);
                
                // 随机椭圆形洞穴
                int radiusX = rand.Next(15, 50);
                int radiusY = rand.Next(10, 35);
                
                for (int i = centerX - radiusX; i <= centerX + radiusX; i++) {
                    for (int j = centerY - radiusY; j <= centerY + radiusY; j++) {
                        if (i >= startX && i < endX && j >= startY && j < endY) {
                            // 椭圆方程
                            float dx = (i - centerX) / (float)radiusX;
                            float dy = (j - centerY) / (float)radiusY;
                            
                            if (dx * dx + dy * dy <= 1f) {
                                // 添加边缘随机性
                                float edge = dx * dx + dy * dy;
                                if (edge < 0.8f || rand.NextFloat() > edge) {
                                    Tile tile = Main.tile[i, j];
                                    tile.ClearTile();
                                }
                            }
                        }
                    }
                }
            }

            // 生成蜿蜒的通道
            int tunnelCount = rand.Next(25, 40);
            
            for (int n = 0; n < tunnelCount; n++) {
                // 确保有足够的空间生成通道
                int safeStartX = Math.Max(startX + 20, startX);
                int safeEndX = Math.Max(endX - 20, safeStartX + 40);
                int safeStartY = Math.Max(startY + 20, startY);
                int safeEndY = Math.Max(endY - 80, safeStartY + 40);
                
                // 如果范围不够，跳过这个通道
                if (safeEndX <= safeStartX || safeEndY <= safeStartY) {
                    continue;
                }
                
                int x = rand.Next(safeStartX, safeEndX);
                int y = rand.Next(safeStartY, safeEndY);
                
                int length = rand.Next(40, 120);
                int width = rand.Next(3, 8);
                
                float angle = rand.NextFloat() * MathHelper.TwoPi;
                
                for (int step = 0; step < length; step++) {
                    // 随机改变方向
                    angle += (rand.NextFloat() - 0.5f) * 0.3f;
                    
                    x += (int)(Math.Cos(angle) * 2);
                    y += (int)(Math.Sin(angle) * 2);
                    
                    // 确保不超出边界
                    x = Math.Clamp(x, startX, endX - 1);
                    y = Math.Clamp(y, startY, endY - 61);
                    
                    // 挖掘通道
                    for (int dx = -width; dx <= width; dx++) {
                        for (int dy = -width; dy <= width; dy++) {
                            int ti = x + dx;
                            int tj = y + dy;
                            
                            if (ti >= startX && ti < endX && tj >= startY && tj < endY - 60) {
                                if (dx * dx + dy * dy <= width * width) {
                                    Tile tile = Main.tile[ti, tj];
                                    tile.ClearTile();
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 生成黄泉之路 - 贯穿地府的主通道
        /// </summary>
        private static void GenerateYellowSpringsPath(int startX, int endX, int startY, int endY, UnifiedRandom rand) {
            // 从左至右的蜿蜒主路
            int pathY = startY + (endY - startY) / 2;
            int pathWidth = 12;
            
            for (int i = startX; i < endX; i++) {
                // 上下波动
                float wave = (float)Math.Sin(i * 0.02f) * 20f;
                int currentY = pathY + (int)wave;
                
                // 清空路径
                for (int dy = -pathWidth; dy <= pathWidth; dy++) {
                    int j = currentY + dy;
                    if (j >= startY && j < endY - 60) {
                        Tile tile = Main.tile[i, j];
                        tile.ClearTile();
                    }
                }
                
                // 在路径底部铺设幽冥石地板
                for (int floorWidth = -pathWidth; floorWidth <= pathWidth; floorWidth++) {
                    int floorJ = currentY + pathWidth + 1;
                    if (floorJ < endY - 60) {
                        WorldGen.PlaceTile(i, floorJ, ModContent.TileType<UmbralStone>(), forced: true, mute: true);
                    }
                }
            }
        }

        /// <summary>
        /// 生成地府特色结构（柱子、小型建筑等）
        /// </summary>
        private static void GenerateUnderworldStructures(int startX, int endX, int startY, int endY, UnifiedRandom rand) {
            int umbralStoneType = ModContent.TileType<UmbralStone>();
            
            // 生成幽冥石柱 - 象征地府的支柱
            int pillarCount = rand.Next(15, 25);
            
            for (int n = 0; n < pillarCount; n++) {
                // 确保有足够的空间
                int safeStartX = Math.Max(startX + 30, startX);
                int safeEndX = Math.Max(endX - 30, safeStartX + 60);
                
                if (safeEndX <= safeStartX) continue;
                
                int x = rand.Next(safeStartX, safeEndX);
                
                // 计算安全的Y范围
                int yRange = endY - startY - 100;
                if (yRange <= 50) continue;
                
                int y = startY + rand.Next(50, Math.Max(51, yRange));
                
                // 寻找可放置位置（空气下方有实心方块）
                bool foundGround = false;
                int maxCheckY = Math.Min(endY - 80, Main.maxTilesY - 80);
                
                for (int checkY = y; checkY < maxCheckY; checkY++) {
                    if (!Main.tile[x, checkY].HasTile && Main.tile[x, checkY + 1].HasTile && Main.tileSolid[Main.tile[x, checkY + 1].TileType]) {
                        y = checkY;
                        foundGround = true;
                        break;
                    }
                }
                
                if (!foundGround) continue;
                
                // 生成柱子（向上或向下）
                int height = rand.Next(15, 40);
                int width = rand.Next(2, 5);
                bool goingUp = rand.NextBool();
                
                for (int h = 0; h < height; h++) {
                    for (int w = -width; w <= width; w++) {
                        int pi = x + w;
                        int pj = goingUp ? y - h : y + h;
                        
                        if (pi >= startX && pi < endX && pj >= startY && pj < endY - 60) {
                            WorldGen.PlaceTile(pi, pj, umbralStoneType, forced: true, mute: true);
                        }
                    }
                    
                    // 柱子逐渐变细
                    if (h > height / 2 && width > 1 && rand.NextBool(3)) {
                        width--;
                    }
                }
            }

            // 生成小型石台 - 象征判官台、奈何桥等
            int platformCount = rand.Next(10, 15);
            
            for (int n = 0; n < platformCount; n++) {
                // 确保有足够的空间
                int safeStartX = Math.Max(startX + 40, startX);
                int safeEndX = Math.Max(endX - 40, safeStartX + 80);
                int safeStartY = Math.Max(startY + 60, startY);
                int safeEndY = Math.Max(endY - 120, safeStartY + 120);
                
                if (safeEndX <= safeStartX || safeEndY <= safeStartY) continue;
                
                int x = rand.Next(safeStartX, safeEndX);
                int y = rand.Next(safeStartY, safeEndY);
                
                int platformWidth = rand.Next(8, 16);
                int platformHeight = rand.Next(2, 5);
                
                // 确保位置是空气
                bool canPlace = true;
                for (int i = Math.Max(x - platformWidth, startX); i <= Math.Min(x + platformWidth, endX - 1); i++) {
                    for (int j = y; j < Math.Min(y + platformHeight + 10, endY - 60); j++) {
                        if (Main.tile[i, j].HasTile) {
                            canPlace = false;
                            break;
                        }
                    }
                    if (!canPlace) break;
                }
                
                if (!canPlace) continue;
                
                // 放置平台
                for (int i = x - platformWidth; i <= x + platformWidth; i++) {
                    for (int j = y; j < y + platformHeight; j++) {
                        if (i >= startX && i < endX && j >= startY && j < endY - 60) {
                            WorldGen.PlaceTile(i, j, umbralStoneType, forced: true, mute: true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 添加细节装饰
        /// </summary>
        private static void AddDetails(int startX, int endX, int startY, int endY, UnifiedRandom rand) {
            int umbralStoneType = ModContent.TileType<UmbralStone>();
            
            // 确保有效范围
            if (endX <= startX || endY <= startY + 60) {
                return;
            }
            
            // 添加随机的幽冥石块，增加地形的不规则感
            for (int n = 0; n < 1000; n++) {
                int x = rand.Next(startX, endX);
                int y = rand.Next(startY, endY - 60);
                
                // 边界检查
                if (x < startX || x >= endX || y < startY || y >= endY - 60) continue;
                if (y + 1 >= endY) continue;
                
                if (!Main.tile[x, y].HasTile && Main.tile[x, y + 1].HasTile) {
                    if (rand.NextBool(3)) {
                        WorldGen.PlaceTile(x, y, umbralStoneType, forced: true, mute: true);
                    }
                }
            }
        }

        /// <summary>
        /// 平滑地形，使其更自然
        /// </summary>
        private static void SmoothTerrain(int startX, int endX, int startY, int endY) {
            // 简单的平滑处理
            for (int pass = 0; pass < 2; pass++) {
                for (int i = startX + 2; i < endX - 2; i += 5) {
                    for (int j = startY + 2; j < endY - 62; j += 5) {
                        if (WorldGen.genRand.NextBool(2)) {
                            WorldGen.TileRunner(i, j, WorldGen.genRand.Next(2, 4), WorldGen.genRand.Next(2, 4), 
                                ModContent.TileType<UmbralStone>(), false, 0f, 0f, false, true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 简单的柏林噪声实现
        /// </summary>
        private static float GetPerlinNoise(float x, float y, UnifiedRandom rand) {
            // 使用三角函数模拟柏林噪声
            float noise = 0f;
            noise += (float)Math.Sin(x * 1.0f) * 0.5f;
            noise += (float)Math.Sin(y * 1.0f) * 0.5f;
            noise += (float)Math.Sin((x + y) * 0.5f) * 0.3f;
            noise += (float)Math.Sin((x - y) * 0.7f) * 0.2f;
            
            // 归一化到 0-1
            return (noise + 1.5f) / 3f;
        }
    }
}
