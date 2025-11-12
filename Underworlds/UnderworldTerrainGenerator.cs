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

            // 计算地府区域范围 - 地狱右半边及其上方
            int underworldStartX = Main.maxTilesX / 2;
            int underworldEndX = Main.maxTilesX - 200;
            
            // 地府核心区（地狱层）
            int underworldCoreStartY = Main.UnderworldLayer;
            int underworldCoreEndY = Main.maxTilesY;
            
            // 地府地表区（向上扩展）
            int underworldSurfaceStartY = (int)Main.rockLayer; // 从岩石层开始
            int underworldSurfaceEndY = Main.UnderworldLayer;

            // 验证范围有效性
            if (underworldEndX <= underworldStartX) {
                Main.NewText("错误：地图太小，无法生成地府地形", Color.Red);
                return;
            }

            if (underworldCoreEndY <= underworldCoreStartY) {
                Main.NewText("错误：地狱层配置异常，无法生成地府地形", Color.Red);
                return;
            }

            // 确保最小区域大小
            if (underworldEndX - underworldStartX < 500) {
                Main.NewText("警告：可用区域较小，地府地形可能不完整", Color.Yellow);
            }

            Main.NewText($"地府核心区：X({underworldStartX}-{underworldEndX}) Y({underworldCoreStartY}-{underworldCoreEndY})", Color.Cyan);
            Main.NewText($"地府地表区：X({underworldStartX}-{underworldEndX}) Y({underworldSurfaceStartY}-{underworldSurfaceEndY})", Color.Cyan);

            // ===== 地府核心区生成 =====
            
            // 第一步：彻底清除原有地形（核心区）
            ClearHellTerrain(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY);

            // 第二步：生成基础地形层
            Main.NewText("生成基础地形...", Color.Yellow);
            GenerateBaseTerrain(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY, rand);

            // 第三步：生成起伏的地表
            Main.NewText("生成六道轮回层...", Color.Yellow);
            GenerateUndulatingTerrain(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY, rand);

            // 第四步：生成洞穴和空间
            Main.NewText("生成洞穴殿堂...", Color.Yellow);
            GenerateCaverns(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY, rand);

            // 第五步：添加地府特色结构
            Main.NewText("生成地府建筑...", Color.Yellow);
            GenerateUnderworldStructures(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY, rand);

            // 第六步：生成黄泉之路（贯穿的主通道）
            Main.NewText("铺设黄泉之路...", Color.Yellow);
            GenerateYellowSpringsPath(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY, rand);

            // 第七步：平滑地形
            Main.NewText("平滑地形...", Color.Yellow);
            SmoothTerrain(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY);

            // 第八步：添加细节装饰
            Main.NewText("添加细节装饰...", Color.Yellow);
            AddDetails(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY, rand);

            // ===== 地府地表区生成 =====
            
            // 第九步：生成地府地表区域
            Main.NewText("开辟地府地表...", Color.Yellow);
            GenerateUnderworldSurface(underworldStartX, underworldEndX, underworldSurfaceStartY, underworldSurfaceEndY, rand);

            // 第十步：生成地表洞穴和峡谷
            Main.NewText("雕刻地表峡谷...", Color.Yellow);
            GenerateSurfaceCanyons(underworldStartX, underworldEndX, underworldSurfaceStartY, underworldSurfaceEndY, rand);

            // 第十一步：生成地表岩石柱和结构
            Main.NewText("竖立幽冥石柱...", Color.Yellow);
            GenerateSurfacePillars(underworldStartX, underworldEndX, underworldSurfaceStartY, underworldSurfaceEndY, rand);

            // 第十二步：铺设灵魂沙地表
            Main.NewText("铺设灵魂沙地表...", Color.Yellow);
            GenerateNetherSandLayer(underworldStartX, underworldEndX, underworldSurfaceStartY, underworldSurfaceEndY, rand);

            Main.NewText("地府地形生成完成！", Color.LightBlue);
            Main.NewText("使用地图查看完整的地府结构", Color.Cyan);
        }

        /// <summary>
        /// 清除地狱右半边的原有地形
        /// </summary>
        private static void ClearHellTerrain(int startX, int endX, int startY, int endY) {
            Main.NewText("清理原有地形中...", Color.Yellow);
            
            for (int i = startX; i < endX; i++) {
                for (int j = startY; j < endY; j++) {
                    Tile tile = Main.tile[i, j];
                    
                    // 保留基岩（底部边界）- 保留最底部的方块
                    if (j >= Main.maxTilesY - 5) {
                        continue;
                    }

                    // 清除所有方块（不仅仅是地狱方块）
                    if (tile.HasTile) {
                        tile.ClearTile();
                    }

                    // 清除所有液体（熔岩、水等）
                    if (tile.LiquidAmount > 0) {
                        tile.LiquidAmount = 0;
                        tile.LiquidType = 0;
                    }

                    // 清除所有墙壁
                    if (tile.WallType > 0) {
                        tile.WallType = 0;
                    }

                    // 清除其他属性
                    tile.ClearEverything();
                }
            }
            
            Main.NewText("原有地形清理完成", Color.LightGreen);
        }

        /// <summary>
        /// 生成基础幽冥石地形层
        /// </summary>
        private static void GenerateBaseTerrain(int startX, int endX, int startY, int endY, UnifiedRandom rand) {
            int umbralStoneType = ModContent.TileType<UmbralStone>();

            for (int i = startX; i < endX; i++) {
                for (int j = startY; j < endY; j++) {
                    // 底部完全填充（最底部100格）
                    if (j >= endY - 100) {
                        WorldGen.PlaceTile(i, j, umbralStoneType, forced: true, mute: true);
                        continue;
                    }

                    // 使用柏林噪声创建自然分布
                    float noise = GetPerlinNoise(i * 0.02f, j * 0.02f, rand);
                    
                    // 根据深度调整密度 - 使整体更密集
                    float depth = (j - startY) / (float)(endY - startY);
                    float density = 0.4f + depth * 0.5f; // 提高基础密度

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
                        tile.LiquidAmount = 0;
                    }
                }
                
                // 在路径底部和上方铺设幽冥石边界，形成明显的通道
                // 底部地板
                for (int floorWidth = -pathWidth - 2; floorWidth <= pathWidth + 2; floorWidth++) {
                    int floorJ = currentY + pathWidth + 1;
                    if (floorJ >= startY && floorJ < endY - 60) {
                        WorldGen.PlaceTile(i, floorJ, ModContent.TileType<UmbralStone>(), forced: true, mute: true);
                        // 加厚地板
                        if (floorJ + 1 < endY - 60) {
                            WorldGen.PlaceTile(i, floorJ + 1, ModContent.TileType<UmbralStone>(), forced: true, mute: true);
                        }
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

        /// <summary>
        /// 生成地府地表区域
        /// </summary>
        private static void GenerateUnderworldSurface(int startX, int endX, int startY, int endY, UnifiedRandom rand) {
            int umbralStoneType = ModContent.TileType<UmbralStone>();

            // 清除该区域的原有地形
            for (int i = startX; i < endX; i++) {
                for (int j = startY; j < endY; j++) {
                    Tile tile = Main.tile[i, j];
                    
                    // 清除方块
                    if (tile.HasTile) {
                        tile.ClearTile();
                    }
                    
                    // 清除液体
                    if (tile.LiquidAmount > 0) {
                        tile.LiquidAmount = 0;
                        tile.LiquidType = 0;
                    }
                    
                    // 清除墙壁
                    if (tile.WallType > 0) {
                        tile.WallType = 0;
                    }
                }
            }

            // 生成不规则的地府地表基础地形
            for (int i = startX; i < endX; i++) {
                // 创建起伏的地表高度
                float surfaceNoise = GetPerlinNoise(i * 0.03f, 0, rand);
                int surfaceHeight = startY + (int)((endY - startY) * 0.3f + surfaceNoise * (endY - startY) * 0.4f);

                // 从地表向下填充幽冥石
                for (int j = surfaceHeight; j < endY; j++) {
                    // 使用噪声创建不规则的密度
                    float noise = GetPerlinNoise(i * 0.04f, j * 0.04f, rand);
                    
                    // 根据深度调整密度
                    float depth = (j - surfaceHeight) / (float)(endY - surfaceHeight);
                    float density = 0.3f + depth * 0.6f;

                    if (noise > 1f - density) {
                        WorldGen.PlaceTile(i, j, umbralStoneType, forced: true, mute: true);
                    }
                }
            }
        }

        /// <summary>
        /// 生成地表峡谷和大型洞穴
        /// </summary>
        private static void GenerateSurfaceCanyons(int startX, int endX, int startY, int endY, UnifiedRandom rand) {
            // 生成垂直峡谷
            int canyonCount = rand.Next(8, 15);
            
            for (int n = 0; n < canyonCount; n++) {
                int safeStartX = Math.Max(startX + 40, startX);
                int safeEndX = Math.Max(endX - 40, safeStartX + 80);
                
                if (safeEndX <= safeStartX) continue;
                
                int canyonX = rand.Next(safeStartX, safeEndX);
                int canyonWidth = rand.Next(5, 15);
                int canyonDepth = rand.Next((endY - startY) / 2, (endY - startY) * 3 / 4);
                
                // 挖掘峡谷
                for (int i = canyonX - canyonWidth; i <= canyonX + canyonWidth; i++) {
                    for (int j = startY; j < startY + canyonDepth; j++) {
                        if (i >= startX && i < endX && j >= startY && j < endY) {
                            // 使用抛物线创建峡谷形状
                            float distFromCenter = Math.Abs(i - canyonX) / (float)canyonWidth;
                            float depthFactor = 1f - distFromCenter * distFromCenter;
                            
                            if (j < startY + canyonDepth * depthFactor) {
                                Tile tile = Main.tile[i, j];
                                tile.ClearTile();
                                tile.LiquidAmount = 0;
                            }
                        }
                    }
                }
            }

            // 生成大型洞穴
            int cavernCount = rand.Next(15, 25);
            
            for (int n = 0; n < cavernCount; n++) {
                int safeStartX = Math.Max(startX + 30, startX);
                int safeEndX = Math.Max(endX - 30, safeStartX + 60);
                int safeStartY = Math.Max(startY + 20, startY);
                int safeEndY = Math.Max(endY - 20, safeStartY + 40);
                
                if (safeEndX <= safeStartX || safeEndY <= safeStartY) continue;
                
                int centerX = rand.Next(safeStartX, safeEndX);
                int centerY = rand.Next(safeStartY, safeEndY);
                
                int radiusX = rand.Next(10, 30);
                int radiusY = rand.Next(8, 25);
                
                // 挖掘椭圆形洞穴
                for (int i = centerX - radiusX; i <= centerX + radiusX; i++) {
                    for (int j = centerY - radiusY; j <= centerY + radiusY; j++) {
                        if (i >= startX && i < endX && j >= startY && j < endY) {
                            float dx = (i - centerX) / (float)radiusX;
                            float dy = (j - centerY) / (float)radiusY;
                            
                            if (dx * dx + dy * dy <= 1f) {
                                float edge = dx * dx + dy * dy;
                                if (edge < 0.85f || rand.NextFloat() > edge) {
                                    Tile tile = Main.tile[i, j];
                                    tile.ClearTile();
                                    tile.LiquidAmount = 0;
                                }
                            }
                        }
                    }
                }
            }

            // 生成蜿蜒通道连接洞穴
            int tunnelCount = rand.Next(20, 30);
            
            for (int n = 0; n < tunnelCount; n++) {
                int safeStartX = Math.Max(startX + 10, startX);
                int safeEndX = Math.Max(endX - 10, safeStartX + 20);
                int safeStartY = Math.Max(startY + 10, startY);
                int safeEndY = Math.Max(endY - 10, safeStartY + 20);
                
                if (safeEndX <= safeStartX || safeEndY <= safeStartY) continue;
                
                int x = rand.Next(safeStartX, safeEndX);
                int y = rand.Next(safeStartY, safeEndY);
                
                int length = rand.Next(30, 80);
                int width = rand.Next(2, 6);
                
                float angle = rand.NextFloat() * MathHelper.TwoPi;
                
                for (int step = 0; step < length; step++) {
                    angle += (rand.NextFloat() - 0.5f) * 0.4f;
                    
                    x += (int)(Math.Cos(angle) * 2);
                    y += (int)(Math.Sin(angle) * 2);
                    
                    x = Math.Clamp(x, startX, endX - 1);
                    y = Math.Clamp(y, startY, endY - 1);
                    
                    for (int dx = -width; dx <= width; dx++) {
                        for (int dy = -width; dy <= width; dy++) {
                            int ti = x + dx;
                            int tj = y + dy;
                            
                            if (ti >= startX && ti < endX && tj >= startY && tj < endY) {
                                if (dx * dx + dy * dy <= width * width) {
                                    Tile tile = Main.tile[ti, tj];
                                    tile.ClearTile();
                                    tile.LiquidAmount = 0;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 生成地表岩石柱和尖刺
        /// </summary>
        private static void GenerateSurfacePillars(int startX, int endX, int startY, int endY, UnifiedRandom rand) {
            int umbralStoneType = ModContent.TileType<UmbralStone>();
            
            // 生成向上的石柱（钟乳石倒置）
            int pillarCountUp = rand.Next(30, 50);
            
            for (int n = 0; n < pillarCountUp; n++) {
                int safeStartX = Math.Max(startX + 10, startX);
                int safeEndX = Math.Max(endX - 10, safeStartX + 20);
                
                if (safeEndX <= safeStartX) continue;
                
                int x = rand.Next(safeStartX, safeEndX);
                
                // 从底部向上寻找地面
                int groundY = -1;
                for (int checkY = endY - 1; checkY >= startY; checkY--) {
                    if (Main.tile[x, checkY].HasTile && Main.tileSolid[Main.tile[x, checkY].TileType]) {
                        if (checkY > startY && !Main.tile[x, checkY - 1].HasTile) {
                            groundY = checkY;
                            break;
                        }
                    }
                }
                
                if (groundY == -1) continue;
                
                int height = rand.Next(5, 20);
                int baseWidth = rand.Next(2, 5);
                
                // 生成向上的锥形柱子
                for (int h = 0; h < height; h++) {
                    int currentWidth = (int)(baseWidth * (1f - h / (float)height));
                    if (currentWidth < 1) currentWidth = 1;
                    
                    for (int w = -currentWidth; w <= currentWidth; w++) {
                        int pi = x + w;
                        int pj = groundY - h;
                        
                        if (pi >= startX && pi < endX && pj >= startY && pj < endY) {
                            WorldGen.PlaceTile(pi, pj, umbralStoneType, forced: true, mute: true);
                        }
                    }
                }
            }

            // 生成向下的石柱（钟乳石）
            int pillarCountDown = rand.Next(30, 50);
            
            for (int n = 0; n < pillarCountDown; n++) {
                int safeStartX = Math.Max(startX + 10, startX);
                int safeEndX = Math.Max(endX - 10, safeStartX + 20);
                
                if (safeEndX <= safeStartX) continue;
                
                int x = rand.Next(safeStartX, safeEndX);
                
                // 从顶部向下寻找天花板
                int ceilingY = -1;
                for (int checkY = startY; checkY < endY; checkY++) {
                    if (Main.tile[x, checkY].HasTile && Main.tileSolid[Main.tile[x, checkY].TileType]) {
                        if (checkY < endY - 1 && !Main.tile[x, checkY + 1].HasTile) {
                            ceilingY = checkY;
                            break;
                        }
                    }
                }
                
                if (ceilingY == -1) continue;
                
                int height = rand.Next(5, 20);
                int baseWidth = rand.Next(2, 5);
                
                // 生成向下的锥形柱子
                for (int h = 0; h < height; h++) {
                    int currentWidth = (int)(baseWidth * (1f - h / (float)height));
                    if (currentWidth < 1) currentWidth = 1;
                    
                    for (int w = -currentWidth; w <= currentWidth; w++) {
                        int pi = x + w;
                        int pj = ceilingY + h;
                        
                        if (pi >= startX && pi < endX && pj >= startY && pj < endY) {
                            if (!Main.tile[pi, pj].HasTile) {
                                WorldGen.PlaceTile(pi, pj, umbralStoneType, forced: true, mute: true);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 铺设灵魂沙地表层
        /// </summary>
        private static void GenerateNetherSandLayer(int startX, int endX, int startY, int endY, UnifiedRandom rand) {
            int netherSandType = ModContent.TileType<NetherSand>();
            int umbralStoneType = ModContent.TileType<UmbralStone>();
            
            // 在幽冥石表面铺设灵魂沙
            for (int i = startX; i < endX; i++) {
                for (int j = startY; j < endY; j++) {
                    Tile tile = Main.tile[i, j];
                    
                    // 检查是否是幽冥石且上方是空气
                    if (tile.HasTile && tile.TileType == umbralStoneType) {
                        if (j > startY && !Main.tile[i, j - 1].HasTile) {
                            // 在表面上方放置灵魂沙
                            int sandDepth = rand.Next(1, 5); // 1-4层灵魂沙
                            
                            for (int depth = 0; depth < sandDepth; depth++) {
                                int sandJ = j - 1 - depth;
                                if (sandJ >= startY && sandJ < endY && !Main.tile[i, sandJ].HasTile) {
                                    WorldGen.PlaceTile(i, sandJ, netherSandType, forced: true, mute: true);
                                } else {
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // 添加一些灵魂沙堆和丘陵
            int sandPileCount = rand.Next(20, 35);
            
            for (int n = 0; n < sandPileCount; n++) {
                int safeStartX = Math.Max(startX + 20, startX);
                int safeEndX = Math.Max(endX - 20, safeStartX + 40);
                
                if (safeEndX <= safeStartX) continue;
                
                int centerX = rand.Next(safeStartX, safeEndX);
                
                // 寻找地面
                int groundY = -1;
                for (int checkY = startY; checkY < endY; checkY++) {
                    if (Main.tile[centerX, checkY].HasTile && Main.tileSolid[Main.tile[centerX, checkY].TileType]) {
                        groundY = checkY;
                        break;
                    }
                }
                
                if (groundY == -1 || groundY <= startY) continue;
                
                // 生成沙堆
                int pileWidth = rand.Next(5, 12);
                int pileHeight = rand.Next(3, 8);
                
                for (int i = centerX - pileWidth; i <= centerX + pileWidth; i++) {
                    if (i < startX || i >= endX) continue;
                    
                    float distFromCenter = Math.Abs(i - centerX) / (float)pileWidth;
                    int heightAtPos = (int)(pileHeight * (1f - distFromCenter));
                    
                    for (int h = 0; h < heightAtPos; h++) {
                        int pileJ = groundY - 1 - h;
                        if (pileJ >= startY && pileJ < endY) {
                            if (!Main.tile[i, pileJ].HasTile || Main.tile[i, pileJ].TileType == netherSandType) {
                                WorldGen.PlaceTile(i, pileJ, netherSandType, forced: true, mute: true);
                            }
                        }
                    }
                }
            }

            // 在一些洞穴底部也放置灵魂沙
            for (int i = startX; i < endX; i += 3) {
                for (int j = startY + 10; j < endY - 10; j += 3) {
                    Tile tile = Main.tile[i, j];
                    
                    // 如果是空气且下方有固体方块
                    if (!tile.HasTile && j + 1 < endY) {
                        Tile below = Main.tile[i, j + 1];
                        if (below.HasTile && Main.tileSolid[below.TileType]) {
                            // 有概率放置灵魂沙
                            if (rand.NextBool(4)) {
                                int sandLayers = rand.Next(1, 3);
                                for (int layer = 0; layer < sandLayers; layer++) {
                                    int sandJ = j - layer;
                                    if (sandJ >= startY && !Main.tile[i, sandJ].HasTile) {
                                        WorldGen.PlaceTile(i, sandJ, netherSandType, forced: true, mute: true);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
