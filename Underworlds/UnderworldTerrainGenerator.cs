using AncientChineseMythology.Underworlds.Tiles;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace AncientChineseMythology.Underworlds
{
    /// <summary>
    /// 地府地形生成器 - 提供在地狱右侧生成地府地形的接口
    /// 支持异步分块生成，避免阻塞主线程
    /// </summary>
    public class UnderworldTerrainGenerator : ModSystem
    {
        // 生成状态管理
        private static bool _isGenerating = false;
        private static CancellationTokenSource _cancellationTokenSource;
        private static float _generationProgress = 0f;

        /// <summary>
        /// 是否正在生成地形
        /// </summary>
        public static bool IsGenerating => _isGenerating;

        /// <summary>
        /// 当前生成进度 (0-1)
        /// </summary>
        public static float GenerationProgress => _generationProgress;

        /// <summary>
        /// 取消正在进行的地形生成
        /// </summary>
        public static void CancelGeneration() {
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// 每帧处理的最大 Tile 操作数量，用于控制帧率
        /// </summary>
        private const int TilesPerFrame = 5000;

        /// <summary>
        /// 异步生成地府地形的主接口 - 不阻塞主线程
        /// </summary>
        /// <param name="seed">随机种子，默认使用当前时间</param>
        /// <param name="onProgress">进度回调</param>
        /// <param name="onComplete">完成回调</param>
        public static async Task GenerateUnderworldTerrainAsync(int? seed = null, Action<float, string> onProgress = null, Action<bool> onComplete = null) {
            if (_isGenerating) {
                Main.NewText("地形生成正在进行中，请稍候...", Color.Yellow);
                return;
            }

            _isGenerating = true;
            _generationProgress = 0f;
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            try {
                int randomSeed = seed ?? (int)DateTime.Now.Ticks;
                UnifiedRandom rand = new UnifiedRandom(randomSeed);

                Main.NewText("开始异步生成地府地形...", Color.Purple);
                onProgress?.Invoke(0f, "初始化...");

                // 计算地府区域范围
                int underworldStartX = Main.maxTilesX / 2;
                int underworldEndX = Main.maxTilesX - 200;
                int underworldCoreStartY = Main.UnderworldLayer;
                int underworldCoreEndY = Main.maxTilesY;
                int underworldSurfaceStartY = (int)Main.rockLayer;
                int underworldSurfaceEndY = Main.UnderworldLayer;

                // 验证范围
                if (underworldEndX <= underworldStartX || underworldCoreEndY <= underworldCoreStartY) {
                    Main.NewText("错误：地图配置异常，无法生成地府地形", Color.Red);
                    onComplete?.Invoke(false);
                    return;
                }

                // 定义生成步骤
                var steps = new List<(string name, Func<Task> action, float weight)> {
                    ("清理原有地形", () => ClearHellTerrainAsync(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY, token), 0.1f),
                    ("生成基础地形", () => GenerateBaseTerrainAsync(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY, rand, token), 0.1f),
                    ("生成六道轮回层", () => GenerateUndulatingTerrainAsync(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY, rand, token), 0.1f),
                    ("生成洞穴殿堂", () => GenerateCavernsAsync(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY, rand, token), 0.1f),
                    ("生成地府建筑", () => GenerateUnderworldStructuresAsync(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY, rand, token), 0.1f),
                    ("铺设黄泉之路", () => GenerateYellowSpringsPathAsync(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY, rand, token), 0.05f),
                    ("平滑地形", () => SmoothTerrainAsync(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY, token), 0.05f),
                    ("添加细节装饰", () => AddDetailsAsync(underworldStartX, underworldEndX, underworldCoreStartY, underworldCoreEndY, rand, token), 0.05f),
                    ("开辟地府地表", () => GenerateUnderworldSurfaceAsync(underworldStartX, underworldEndX, underworldSurfaceStartY, underworldSurfaceEndY, rand, token), 0.1f),
                    ("雕刻地表峡谷", () => GenerateSurfaceCanyonsAsync(underworldStartX, underworldEndX, underworldSurfaceStartY, underworldSurfaceEndY, rand, token), 0.1f),
                    ("竖立幽冥石柱", () => GenerateSurfacePillarsAsync(underworldStartX, underworldEndX, underworldSurfaceStartY, underworldSurfaceEndY, rand, token), 0.05f),
                    ("铺设灵魂沙地表", () => GenerateNetherSandLayerAsync(underworldStartX, underworldEndX, underworldSurfaceStartY, underworldSurfaceEndY, rand, token), 0.1f),
                };

                float progressAccum = 0f;

                for (int i = 0; i < steps.Count; i++) {
                    if (token.IsCancellationRequested) {
                        Main.NewText("地形生成已取消", Color.Orange);
                        onComplete?.Invoke(false);
                        return;
                    }

                    var step = steps[i];
                    Main.NewText($"{step.name}...", Color.Yellow);
                    onProgress?.Invoke(progressAccum, step.name);

                    await step.action();

                    progressAccum += step.weight;
                    _generationProgress = progressAccum;
                }

                _generationProgress = 1f;
                Main.NewText("地府已经完整侵入！", Color.LightBlue);
                onProgress?.Invoke(1f, "完成");
                onComplete?.Invoke(true);
            }
            catch (OperationCanceledException) {
                Main.NewText("地形生成已取消", Color.Orange);
                onComplete?.Invoke(false);
            }
            catch (Exception ex) {
                Main.NewText($"地形生成出错: {ex.Message}", Color.Red);
                onComplete?.Invoke(false);
            }
            finally {
                _isGenerating = false;
                _generationProgress = 0f;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 生成地府地形的同步接口（保留兼容性，但会阻塞主线程）
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

            Main.NewText("地府已经完整侵入！", Color.LightBlue);
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

            // 不再完全清空，而是选择性地清除和替换
            // 第一步：生成地府的"侵蚀"范围 - 不规则的清理区域
            for (int i = startX; i < endX; i++) {
                for (int j = startY; j < endY; j++) {
                    Tile tile = Main.tile[i, j];

                    // 使用噪声决定是否清除该位置
                    float erosionNoise = GetPerlinNoise(i * 0.015f, j * 0.015f, rand);

                    // 距离地狱层越近，清除概率越高
                    float depthFactor = (j - startY) / (float)(endY - startY);
                    float clearChance = depthFactor * 0.7f + 0.2f; // 20%-90%的清除概率

                    // 添加边缘渐变效果
                    float edgeDistanceX = Math.Min(i - startX, endX - i) / 100f;
                    edgeDistanceX = Math.Clamp(edgeDistanceX, 0f, 1f);
                    clearChance *= edgeDistanceX;

                    if (erosionNoise > 1f - clearChance) {
                        // 清除方块
                        if (tile.HasTile) {
                            tile.ClearTile();
                        }

                        // 清除液体
                        if (tile.LiquidAmount > 0) {
                            tile.LiquidAmount = 0;
                            tile.LiquidType = 0;
                        }

                        // 部分清除墙壁（不是全部）
                        if (tile.WallType > 0 && rand.NextBool(2)) {
                            tile.WallType = 0;
                        }
                    }
                }
            }

            // 第二步：在清空的区域中生成不规则的幽冥石地形
            for (int i = startX; i < endX; i++) {
                // 创建起伏的地府地表轮廓
                float surfaceNoise = GetPerlinNoise(i * 0.02f, 0, rand);
                int baseSurfaceHeight = startY + (int)((endY - startY) * 0.5f + surfaceNoise * (endY - startY) * 0.3f);

                // 从地表向下填充幽冥石，但密度逐渐降低
                for (int j = baseSurfaceHeight; j < endY; j++) {
                    // 只在已清空的位置放置
                    if (Main.tile[i, j].HasTile) continue;

                    // 使用噪声创建不规则的填充
                    float noise = GetPerlinNoise(i * 0.05f, j * 0.05f, rand);

                    // 根据深度调整密度 - 越接近地狱层越密集
                    float depth = (j - baseSurfaceHeight) / (float)(endY - baseSurfaceHeight);
                    float density = 0.2f + depth * 0.5f; // 20%-70%的密度

                    // 添加垂直方向的变化
                    float verticalNoise = GetPerlinNoise(i * 0.08f, j * 0.08f, rand);
                    density += verticalNoise * 0.2f;

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
            // 减少垂直峡谷数量，使其更稀疏
            int canyonCount = rand.Next(3, 7);

            for (int n = 0; n < canyonCount; n++) {
                int safeStartX = Math.Max(startX + 100, startX);
                int safeEndX = Math.Max(endX - 100, safeStartX + 200);

                if (safeEndX <= safeStartX) continue;

                int canyonX = rand.Next(safeStartX, safeEndX);
                int canyonWidth = rand.Next(8, 20);
                int canyonDepth = rand.Next((endY - startY) / 3, (endY - startY) / 2);

                // 挖掘峡谷 - 使用更柔和的曲线
                for (int i = canyonX - canyonWidth; i <= canyonX + canyonWidth; i++) {
                    for (int j = startY; j < startY + canyonDepth; j++) {
                        if (i >= startX && i < endX && j >= startY && j < endY) {
                            // 使用更平滑的抛物线
                            float distFromCenter = Math.Abs(i - canyonX) / (float)canyonWidth;
                            float depthFactor = (1f - distFromCenter * distFromCenter);

                            // 添加噪声使边缘不规则
                            float edgeNoise = GetPerlinNoise(i * 0.1f, j * 0.1f, rand);
                            depthFactor *= (0.8f + edgeNoise * 0.4f);

                            if (j < startY + canyonDepth * depthFactor) {
                                Tile tile = Main.tile[i, j];
                                tile.ClearTile();
                                tile.LiquidAmount = 0;
                            }
                        }
                    }
                }
            }

            // 减少洞穴数量
            int cavernCount = rand.Next(8, 15);

            for (int n = 0; n < cavernCount; n++) {
                int safeStartX = Math.Max(startX + 50, startX);
                int safeEndX = Math.Max(endX - 50, safeStartX + 100);
                int safeStartY = Math.Max(startY + 30, startY);
                int safeEndY = Math.Max(endY - 30, safeStartY + 60);

                if (safeEndX <= safeStartX || safeEndY <= safeStartY) continue;

                int centerX = rand.Next(safeStartX, safeEndX);
                int centerY = rand.Next(safeStartY, safeEndY);

                // 减小洞穴大小
                int radiusX = rand.Next(8, 20);
                int radiusY = rand.Next(6, 15);

                // 挖掘椭圆形洞穴
                for (int i = centerX - radiusX; i <= centerX + radiusX; i++) {
                    for (int j = centerY - radiusY; j <= centerY + radiusY; j++) {
                        if (i >= startX && i < endX && j >= startY && j < endY) {
                            float dx = (i - centerX) / (float)radiusX;
                            float dy = (j - centerY) / (float)radiusY;

                            if (dx * dx + dy * dy <= 1f) {
                                // 更柔和的边缘
                                float edge = dx * dx + dy * dy;
                                float edgeNoise = GetPerlinNoise(i * 0.15f, j * 0.15f, rand);

                                if (edge < 0.7f || (edge < 0.9f && edgeNoise > 0.3f)) {
                                    Tile tile = Main.tile[i, j];
                                    tile.ClearTile();
                                    tile.LiquidAmount = 0;
                                }
                            }
                        }
                    }
                }
            }

            // 减少通道数量
            int tunnelCount = rand.Next(10, 18);

            for (int n = 0; n < tunnelCount; n++) {
                int safeStartX = Math.Max(startX + 30, startX);
                int safeEndX = Math.Max(endX - 30, safeStartX + 60);
                int safeStartY = Math.Max(startY + 20, startY);
                int safeEndY = Math.Max(endY - 20, safeStartY + 40);

                if (safeEndX <= safeStartX || safeEndY <= safeStartY) continue;

                int x = rand.Next(safeStartX, safeEndX);
                int y = rand.Next(safeStartY, safeEndY);

                // 缩短通道长度
                int length = rand.Next(20, 50);
                int width = rand.Next(2, 5);

                float angle = rand.NextFloat() * MathHelper.TwoPi;

                for (int step = 0; step < length; step++) {
                    // 更温和的方向变化
                    angle += (rand.NextFloat() - 0.5f) * 0.2f;

                    x += (int)(Math.Cos(angle) * 1.5f);
                    y += (int)(Math.Sin(angle) * 1.5f);

                    x = Math.Clamp(x, startX, endX - 1);
                    y = Math.Clamp(y, startY, endY - 1);

                    for (int dx = -width; dx <= width; dx++) {
                        for (int dy = -width; dy <= width; dy++) {
                            int ti = x + dx;
                            int tj = y + dy;

                            if (ti >= startX && ti < endX && tj >= startY && tj < endY) {
                                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                                if (dist <= width) {
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

            // 减少向上石柱的数量
            int pillarCountUp = rand.Next(15, 25);

            for (int n = 0; n < pillarCountUp; n++) {
                int safeStartX = Math.Max(startX + 30, startX);
                int safeEndX = Math.Max(endX - 30, safeStartX + 60);

                if (safeEndX <= safeStartX) continue;

                int x = rand.Next(safeStartX, safeEndX);

                // 从底部向上寻找地面
                int groundY = -1;
                for (int checkY = endY - 1; checkY >= startY; checkY--) {
                    if (Main.tile[x, checkY].HasTile && Main.tileSolid[Main.tile[x, checkY].TileType]) {
                        // 确保是幽冥石或合适的地府方块
                        if ((Main.tile[x, checkY].TileType == umbralStoneType ||
                             Main.tile[x, checkY].TileType == ModContent.TileType<NetherSand>()) &&
                            checkY > startY && !Main.tile[x, checkY - 1].HasTile) {
                            groundY = checkY;
                            break;
                        }
                    }
                }

                if (groundY == -1) continue;

                // 减小石柱高度
                int height = rand.Next(3, 12);
                int baseWidth = rand.Next(1, 3);

                // 生成向上的锥形柱子
                for (int h = 0; h < height; h++) {
                    int currentWidth = (int)(baseWidth * (1f - h / (float)height * 0.7f));
                    if (currentWidth < 1) currentWidth = 1;

                    for (int w = -currentWidth; w <= currentWidth; w++) {
                        int pi = x + w;
                        int pj = groundY - h;

                        if (pi >= startX && pi < endX && pj >= startY && pj < endY) {
                            if (!Main.tile[pi, pj].HasTile) {
                                WorldGen.PlaceTile(pi, pj, umbralStoneType, forced: true, mute: true);
                            }
                        }
                    }
                }
            }

            // 减少向下石柱的数量
            int pillarCountDown = rand.Next(15, 25);

            for (int n = 0; n < pillarCountDown; n++) {
                int safeStartX = Math.Max(startX + 30, startX);
                int safeEndX = Math.Max(endX - 30, safeStartX + 60);

                if (safeEndX <= safeStartX) continue;

                int x = rand.Next(safeStartX, safeEndX);

                // 从顶部向下寻找天花板
                int ceilingY = -1;
                for (int checkY = startY; checkY < endY; checkY++) {
                    if (Main.tile[x, checkY].HasTile && Main.tileSolid[Main.tile[x, checkY].TileType]) {
                        // 确保是幽冥石
                        if (Main.tile[x, checkY].TileType == umbralStoneType &&
                            checkY < endY - 1 && !Main.tile[x, checkY + 1].HasTile) {
                            ceilingY = checkY;
                            break;
                        }
                    }
                }

                if (ceilingY == -1) continue;

                // 减小石柱高度
                int height = rand.Next(3, 12);
                int baseWidth = rand.Next(1, 3);

                // 生成向下的锥形柱子
                for (int h = 0; h < height; h++) {
                    int currentWidth = (int)(baseWidth * (1f - h / (float)height * 0.7f));
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

            // 在幽冥石表面铺设灵魂沙 - 更稀疏的覆盖
            for (int i = startX; i < endX; i++) {
                for (int j = startY; j < endY; j++) {
                    Tile tile = Main.tile[i, j];

                    // 检查是否是幽冥石且上方是空气
                    if (tile.HasTile && tile.TileType == umbralStoneType) {
                        if (j > startY && !Main.tile[i, j - 1].HasTile) {
                            // 使用噪声决定是否在此处放置灵魂沙
                            float sandNoise = GetPerlinNoise(i * 0.08f, j * 0.08f, rand);

                            if (sandNoise > 0.4f) { // 只在60%的表面放置灵魂沙
                                // 在表面上方放置灵魂沙，深度较浅
                                int sandDepth = rand.Next(1, 4); // 1-3层灵魂沙

                                for (int depth = 0; depth < sandDepth; depth++) {
                                    int sandJ = j - 1 - depth;
                                    if (sandJ >= startY && sandJ < endY && !Main.tile[i, sandJ].HasTile) {
                                        WorldGen.PlaceTile(i, sandJ, netherSandType, forced: true, mute: true);
                                    }
                                    else {
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 减少灵魂沙堆的数量
            int sandPileCount = rand.Next(10, 20);

            for (int n = 0; n < sandPileCount; n++) {
                int safeStartX = Math.Max(startX + 50, startX);
                int safeEndX = Math.Max(endX - 50, safeStartX + 100);

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

                if (groundY == -1 || groundY <= startY + 5) continue;

                // 生成较小的沙堆
                int pileWidth = rand.Next(4, 8);
                int pileHeight = rand.Next(2, 5);

                for (int i = centerX - pileWidth; i <= centerX + pileWidth; i++) {
                    if (i < startX || i >= endX) continue;

                    float distFromCenter = Math.Abs(i - centerX) / (float)pileWidth;
                    int heightAtPos = (int)(pileHeight * (1f - distFromCenter * distFromCenter));

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

            // 在洞穴底部放置少量灵魂沙 - 更稀疏
            for (int i = startX; i < endX; i += 5) {
                for (int j = startY + 20; j < endY - 20; j += 5) {
                    Tile tile = Main.tile[i, j];

                    // 如果是空气且下方有固体方块
                    if (!tile.HasTile && j + 1 < endY) {
                        Tile below = Main.tile[i, j + 1];
                        if (below.HasTile && Main.tileSolid[below.TileType]) {
                            // 降低概率
                            if (rand.NextBool(6)) { // 约16%的概率
                                int sandLayers = rand.Next(1, 2);
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

        #region 异步版本的生成方法

        /// <summary>
        /// 异步让出控制权，避免阻塞主线程
        /// </summary>
        private static async Task YieldAsync(CancellationToken token) {
            token.ThrowIfCancellationRequested();
            await Task.Delay(1, token); // 短暂让出控制权
        }

        /// <summary>
        /// 异步清除地狱右半边的原有地形
        /// </summary>
        private static async Task ClearHellTerrainAsync(int startX, int endX, int startY, int endY, CancellationToken token) {
            int processedCount = 0;

            for (int i = startX; i < endX; i++) {
                for (int j = startY; j < endY; j++) {
                    token.ThrowIfCancellationRequested();

                    Tile tile = Main.tile[i, j];

                    if (j >= Main.maxTilesY - 5) {
                        continue;
                    }

                    if (tile.HasTile) {
                        tile.ClearTile();
                    }

                    if (tile.LiquidAmount > 0) {
                        tile.LiquidAmount = 0;
                        tile.LiquidType = 0;
                    }

                    if (tile.WallType > 0) {
                        tile.WallType = 0;
                    }

                    tile.ClearEverything();

                    processedCount++;
                    if (processedCount >= TilesPerFrame) {
                        processedCount = 0;
                        await YieldAsync(token);
                    }
                }
            }
        }

        /// <summary>
        /// 异步生成基础幽冥石地形层
        /// </summary>
        private static async Task GenerateBaseTerrainAsync(int startX, int endX, int startY, int endY, UnifiedRandom rand, CancellationToken token) {
            int umbralStoneType = ModContent.TileType<UmbralStone>();
            int processedCount = 0;

            for (int i = startX; i < endX; i++) {
                for (int j = startY; j < endY; j++) {
                    token.ThrowIfCancellationRequested();

                    if (j >= endY - 100) {
                        WorldGen.PlaceTile(i, j, umbralStoneType, forced: true, mute: true);
                    }
                    else {
                        float noise = GetPerlinNoise(i * 0.02f, j * 0.02f, rand);
                        float depth = (j - startY) / (float)(endY - startY);
                        float density = 0.4f + depth * 0.5f;

                        if (noise > 1f - density) {
                            WorldGen.PlaceTile(i, j, umbralStoneType, forced: true, mute: true);
                        }
                    }

                    processedCount++;
                    if (processedCount >= TilesPerFrame) {
                        processedCount = 0;
                        await YieldAsync(token);
                    }
                }
            }
        }

        /// <summary>
        /// 异步生成起伏的地府地表
        /// </summary>
        private static async Task GenerateUndulatingTerrainAsync(int startX, int endX, int startY, int endY, UnifiedRandom rand, CancellationToken token) {
            int umbralStoneType = ModContent.TileType<UmbralStone>();
            int platformCount = 6;
            int processedCount = 0;

            for (int layer = 0; layer < platformCount; layer++) {
                int baseHeight = startY + (endY - startY) / (platformCount + 1) * (layer + 1);

                for (int i = startX; i < endX; i++) {
                    token.ThrowIfCancellationRequested();

                    float wave = (float)Math.Sin(i * 0.05f + layer * 2f) * 10f;
                    float noise = GetPerlinNoise(i * 0.03f, layer * 10f, rand) * 15f;
                    int height = baseHeight + (int)(wave + noise);
                    int thickness = rand.Next(3, 10);

                    for (int j = height; j < height + thickness; j++) {
                        if (j >= startY && j < endY) {
                            WorldGen.PlaceTile(i, j, umbralStoneType, forced: true, mute: true);
                        }
                    }

                    processedCount++;
                    if (processedCount >= TilesPerFrame / 10) {
                        processedCount = 0;
                        await YieldAsync(token);
                    }
                }
            }
        }

        /// <summary>
        /// 异步生成洞穴和开放空间
        /// </summary>
        private static async Task GenerateCavernsAsync(int startX, int endX, int startY, int endY, UnifiedRandom rand, CancellationToken token) {
            int cavernCount = rand.Next(20, 30);

            for (int n = 0; n < cavernCount; n++) {
                token.ThrowIfCancellationRequested();

                int safeStartX = Math.Max(startX + 50, startX);
                int safeEndX = Math.Max(endX - 50, safeStartX + 100);
                int safeStartY = Math.Max(startY + 50, startY);
                int safeEndY = Math.Max(endY - 100, safeStartY + 100);

                if (safeEndX <= safeStartX || safeEndY <= safeStartY) continue;

                int centerX = rand.Next(safeStartX, safeEndX);
                int centerY = rand.Next(safeStartY, safeEndY);
                int radiusX = rand.Next(15, 50);
                int radiusY = rand.Next(10, 35);

                for (int i = centerX - radiusX; i <= centerX + radiusX; i++) {
                    for (int j = centerY - radiusY; j <= centerY + radiusY; j++) {
                        if (i >= startX && i < endX && j >= startY && j < endY) {
                            float dx = (i - centerX) / (float)radiusX;
                            float dy = (j - centerY) / (float)radiusY;

                            if (dx * dx + dy * dy <= 1f) {
                                float edge = dx * dx + dy * dy;
                                if (edge < 0.8f || rand.NextFloat() > edge) {
                                    Main.tile[i, j].ClearTile();
                                }
                            }
                        }
                    }
                }

                await YieldAsync(token);
            }

            int tunnelCount = rand.Next(25, 40);

            for (int n = 0; n < tunnelCount; n++) {
                token.ThrowIfCancellationRequested();

                int safeStartX = Math.Max(startX + 20, startX);
                int safeEndX = Math.Max(endX - 20, safeStartX + 40);
                int safeStartY = Math.Max(startY + 20, startY);
                int safeEndY = Math.Max(endY - 80, safeStartY + 40);

                if (safeEndX <= safeStartX || safeEndY <= safeStartY) continue;

                int x = rand.Next(safeStartX, safeEndX);
                int y = rand.Next(safeStartY, safeEndY);
                int length = rand.Next(40, 120);
                int width = rand.Next(3, 8);
                float angle = rand.NextFloat() * MathHelper.TwoPi;

                for (int step = 0; step < length; step++) {
                    angle += (rand.NextFloat() - 0.5f) * 0.3f;
                    x += (int)(Math.Cos(angle) * 2);
                    y += (int)(Math.Sin(angle) * 2);
                    x = Math.Clamp(x, startX, endX - 1);
                    y = Math.Clamp(y, startY, endY - 61);

                    for (int dx = -width; dx <= width; dx++) {
                        for (int dy = -width; dy <= width; dy++) {
                            int ti = x + dx;
                            int tj = y + dy;

                            if (ti >= startX && ti < endX && tj >= startY && tj < endY - 60) {
                                if (dx * dx + dy * dy <= width * width) {
                                    Main.tile[ti, tj].ClearTile();
                                }
                            }
                        }
                    }
                }

                await YieldAsync(token);
            }
        }

        /// <summary>
        /// 异步生成黄泉之路
        /// </summary>
        private static async Task GenerateYellowSpringsPathAsync(int startX, int endX, int startY, int endY, UnifiedRandom rand, CancellationToken token) {
            int pathY = startY + (endY - startY) / 2;
            int pathWidth = 12;
            int processedCount = 0;

            for (int i = startX; i < endX; i++) {
                token.ThrowIfCancellationRequested();

                float wave = (float)Math.Sin(i * 0.02f) * 20f;
                int currentY = pathY + (int)wave;

                for (int dy = -pathWidth; dy <= pathWidth; dy++) {
                    int j = currentY + dy;
                    if (j >= startY && j < endY - 60) {
                        Tile tile = Main.tile[i, j];
                        tile.ClearTile();
                        tile.LiquidAmount = 0;
                    }
                }

                for (int floorWidth = -pathWidth - 2; floorWidth <= pathWidth + 2; floorWidth++) {
                    int floorJ = currentY + pathWidth + 1;
                    if (floorJ >= startY && floorJ < endY - 60) {
                        WorldGen.PlaceTile(i, floorJ, ModContent.TileType<UmbralStone>(), forced: true, mute: true);
                        if (floorJ + 1 < endY - 60) {
                            WorldGen.PlaceTile(i, floorJ + 1, ModContent.TileType<UmbralStone>(), forced: true, mute: true);
                        }
                    }
                }

                processedCount++;
                if (processedCount >= TilesPerFrame / 20) {
                    processedCount = 0;
                    await YieldAsync(token);
                }
            }
        }

        /// <summary>
        /// 异步生成地府特色结构
        /// </summary>
        private static async Task GenerateUnderworldStructuresAsync(int startX, int endX, int startY, int endY, UnifiedRandom rand, CancellationToken token) {
            int umbralStoneType = ModContent.TileType<UmbralStone>();
            int pillarCount = rand.Next(15, 25);

            for (int n = 0; n < pillarCount; n++) {
                token.ThrowIfCancellationRequested();

                int safeStartX = Math.Max(startX + 30, startX);
                int safeEndX = Math.Max(endX - 30, safeStartX + 60);

                if (safeEndX <= safeStartX) continue;

                int x = rand.Next(safeStartX, safeEndX);
                int yRange = endY - startY - 100;
                if (yRange <= 50) continue;

                int y = startY + rand.Next(50, Math.Max(51, yRange));
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

                    if (h > height / 2 && width > 1 && rand.NextBool(3)) {
                        width--;
                    }
                }

                await YieldAsync(token);
            }

            int platformCount = rand.Next(10, 15);

            for (int n = 0; n < platformCount; n++) {
                token.ThrowIfCancellationRequested();

                int safeStartX = Math.Max(startX + 40, startX);
                int safeEndX = Math.Max(endX - 40, safeStartX + 80);
                int safeStartY = Math.Max(startY + 60, startY);
                int safeEndY = Math.Max(endY - 120, safeStartY + 120);

                if (safeEndX <= safeStartX || safeEndY <= safeStartY) continue;

                int x = rand.Next(safeStartX, safeEndX);
                int y = rand.Next(safeStartY, safeEndY);
                int platformWidth = rand.Next(8, 16);
                int platformHeight = rand.Next(2, 5);

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

                for (int i = x - platformWidth; i <= x + platformWidth; i++) {
                    for (int j = y; j < y + platformHeight; j++) {
                        if (i >= startX && i < endX && j >= startY && j < endY - 60) {
                            WorldGen.PlaceTile(i, j, umbralStoneType, forced: true, mute: true);
                        }
                    }
                }

                await YieldAsync(token);
            }
        }

        /// <summary>
        /// 异步添加细节装饰
        /// </summary>
        private static async Task AddDetailsAsync(int startX, int endX, int startY, int endY, UnifiedRandom rand, CancellationToken token) {
            int umbralStoneType = ModContent.TileType<UmbralStone>();

            if (endX <= startX || endY <= startY + 60) return;

            int processedCount = 0;

            for (int n = 0; n < 1000; n++) {
                token.ThrowIfCancellationRequested();

                int x = rand.Next(startX, endX);
                int y = rand.Next(startY, endY - 60);

                if (x < startX || x >= endX || y < startY || y >= endY - 60) continue;
                if (y + 1 >= endY) continue;

                if (!Main.tile[x, y].HasTile && Main.tile[x, y + 1].HasTile) {
                    if (rand.NextBool(3)) {
                        WorldGen.PlaceTile(x, y, umbralStoneType, forced: true, mute: true);
                    }
                }

                processedCount++;
                if (processedCount >= TilesPerFrame) {
                    processedCount = 0;
                    await YieldAsync(token);
                }
            }
        }

        /// <summary>
        /// 异步平滑地形
        /// </summary>
        private static async Task SmoothTerrainAsync(int startX, int endX, int startY, int endY, CancellationToken token) {
            int processedCount = 0;

            for (int pass = 0; pass < 2; pass++) {
                for (int i = startX + 2; i < endX - 2; i += 5) {
                    for (int j = startY + 2; j < endY - 62; j += 5) {
                        token.ThrowIfCancellationRequested();

                        if (WorldGen.genRand.NextBool(2)) {
                            WorldGen.TileRunner(i, j, WorldGen.genRand.Next(2, 4), WorldGen.genRand.Next(2, 4),
                                ModContent.TileType<UmbralStone>(), false, 0f, 0f, false, true);
                        }

                        processedCount++;
                        if (processedCount >= TilesPerFrame / 50) {
                            processedCount = 0;
                            await YieldAsync(token);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 异步生成地府地表区域
        /// </summary>
        private static async Task GenerateUnderworldSurfaceAsync(int startX, int endX, int startY, int endY, UnifiedRandom rand, CancellationToken token) {
            int umbralStoneType = ModContent.TileType<UmbralStone>();
            int processedCount = 0;

            // 第一步：生成地府的"侵蚀"范围
            for (int i = startX; i < endX; i++) {
                for (int j = startY; j < endY; j++) {
                    token.ThrowIfCancellationRequested();

                    Tile tile = Main.tile[i, j];
                    float erosionNoise = GetPerlinNoise(i * 0.015f, j * 0.015f, rand);
                    float depthFactor = (j - startY) / (float)(endY - startY);
                    float clearChance = depthFactor * 0.7f + 0.2f;
                    float edgeDistanceX = Math.Min(i - startX, endX - i) / 100f;
                    edgeDistanceX = Math.Clamp(edgeDistanceX, 0f, 1f);
                    clearChance *= edgeDistanceX;

                    if (erosionNoise > 1f - clearChance) {
                        if (tile.HasTile) tile.ClearTile();
                        if (tile.LiquidAmount > 0) {
                            tile.LiquidAmount = 0;
                            tile.LiquidType = 0;
                        }
                        if (tile.WallType > 0 && rand.NextBool(2)) {
                            tile.WallType = 0;
                        }
                    }

                    processedCount++;
                    if (processedCount >= TilesPerFrame) {
                        processedCount = 0;
                        await YieldAsync(token);
                    }
                }
            }

            // 第二步：在清空的区域中生成不规则的幽冥石地形
            processedCount = 0;
            for (int i = startX; i < endX; i++) {
                token.ThrowIfCancellationRequested();

                float surfaceNoise = GetPerlinNoise(i * 0.02f, 0, rand);
                int baseSurfaceHeight = startY + (int)((endY - startY) * 0.5f + surfaceNoise * (endY - startY) * 0.3f);

                for (int j = baseSurfaceHeight; j < endY; j++) {
                    if (Main.tile[i, j].HasTile) continue;

                    float noise = GetPerlinNoise(i * 0.05f, j * 0.05f, rand);
                    float depth = (j - baseSurfaceHeight) / (float)(endY - baseSurfaceHeight);
                    float density = 0.2f + depth * 0.5f;
                    float verticalNoise = GetPerlinNoise(i * 0.08f, j * 0.08f, rand);
                    density += verticalNoise * 0.2f;

                    if (noise > 1f - density) {
                        WorldGen.PlaceTile(i, j, umbralStoneType, forced: true, mute: true);
                    }
                }

                processedCount++;
                if (processedCount >= TilesPerFrame / 100) {
                    processedCount = 0;
                    await YieldAsync(token);
                }
            }
        }

        /// <summary>
        /// 异步生成地表峡谷和大型洞穴
        /// </summary>
        private static async Task GenerateSurfaceCanyonsAsync(int startX, int endX, int startY, int endY, UnifiedRandom rand, CancellationToken token) {
            int canyonCount = rand.Next(3, 7);

            for (int n = 0; n < canyonCount; n++) {
                token.ThrowIfCancellationRequested();

                int safeStartX = Math.Max(startX + 100, startX);
                int safeEndX = Math.Max(endX - 100, safeStartX + 200);

                if (safeEndX <= safeStartX) continue;

                int canyonX = rand.Next(safeStartX, safeEndX);
                int canyonWidth = rand.Next(8, 20);
                int canyonDepth = rand.Next((endY - startY) / 3, (endY - startY) / 2);

                for (int i = canyonX - canyonWidth; i <= canyonX + canyonWidth; i++) {
                    for (int j = startY; j < startY + canyonDepth; j++) {
                        if (i >= startX && i < endX && j >= startY && j < endY) {
                            float distFromCenter = Math.Abs(i - canyonX) / (float)canyonWidth;
                            float depthFactor = (1f - distFromCenter * distFromCenter);
                            float edgeNoise = GetPerlinNoise(i * 0.1f, j * 0.1f, rand);
                            depthFactor *= (0.8f + edgeNoise * 0.4f);

                            if (j < startY + canyonDepth * depthFactor) {
                                Tile tile = Main.tile[i, j];
                                tile.ClearTile();
                                tile.LiquidAmount = 0;
                            }
                        }
                    }
                }

                await YieldAsync(token);
            }

            int cavernCount = rand.Next(8, 15);

            for (int n = 0; n < cavernCount; n++) {
                token.ThrowIfCancellationRequested();

                int safeStartX = Math.Max(startX + 50, startX);
                int safeEndX = Math.Max(endX - 50, safeStartX + 100);
                int safeStartY = Math.Max(startY + 30, startY);
                int safeEndY = Math.Max(endY - 30, safeStartY + 60);

                if (safeEndX <= safeStartX || safeEndY <= safeStartY) continue;

                int centerX = rand.Next(safeStartX, safeEndX);
                int centerY = rand.Next(safeStartY, safeEndY);
                int radiusX = rand.Next(8, 20);
                int radiusY = rand.Next(6, 15);

                for (int i = centerX - radiusX; i <= centerX + radiusX; i++) {
                    for (int j = centerY - radiusY; j <= centerY + radiusY; j++) {
                        if (i >= startX && i < endX && j >= startY && j < endY) {
                            float dx = (i - centerX) / (float)radiusX;
                            float dy = (j - centerY) / (float)radiusY;

                            if (dx * dx + dy * dy <= 1f) {
                                float edge = dx * dx + dy * dy;
                                float edgeNoise = GetPerlinNoise(i * 0.15f, j * 0.15f, rand);

                                if (edge < 0.7f || (edge < 0.9f && edgeNoise > 0.3f)) {
                                    Tile tile = Main.tile[i, j];
                                    tile.ClearTile();
                                    tile.LiquidAmount = 0;
                                }
                            }
                        }
                    }
                }

                await YieldAsync(token);
            }

            int tunnelCount = rand.Next(10, 18);

            for (int n = 0; n < tunnelCount; n++) {
                token.ThrowIfCancellationRequested();

                int safeStartX = Math.Max(startX + 30, startX);
                int safeEndX = Math.Max(endX - 30, safeStartX + 60);
                int safeStartY = Math.Max(startY + 20, startY);
                int safeEndY = Math.Max(endY - 20, safeStartY + 40);

                if (safeEndX <= safeStartX || safeEndY <= safeStartY) continue;

                int x = rand.Next(safeStartX, safeEndX);
                int y = rand.Next(safeStartY, safeEndY);
                int length = rand.Next(20, 50);
                int width = rand.Next(2, 5);
                float angle = rand.NextFloat() * MathHelper.TwoPi;

                for (int step = 0; step < length; step++) {
                    angle += (rand.NextFloat() - 0.5f) * 0.2f;
                    x += (int)(Math.Cos(angle) * 1.5f);
                    y += (int)(Math.Sin(angle) * 1.5f);
                    x = Math.Clamp(x, startX, endX - 1);
                    y = Math.Clamp(y, startY, endY - 1);

                    for (int dx = -width; dx <= width; dx++) {
                        for (int dy = -width; dy <= width; dy++) {
                            int ti = x + dx;
                            int tj = y + dy;

                            if (ti >= startX && ti < endX && tj >= startY && tj < endY) {
                                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                                if (dist <= width) {
                                    Tile tile = Main.tile[ti, tj];
                                    tile.ClearTile();
                                    tile.LiquidAmount = 0;
                                }
                            }
                        }
                    }
                }

                await YieldAsync(token);
            }
        }

        /// <summary>
        /// 异步生成地表岩石柱和尖刺
        /// </summary>
        private static async Task GenerateSurfacePillarsAsync(int startX, int endX, int startY, int endY, UnifiedRandom rand, CancellationToken token) {
            int umbralStoneType = ModContent.TileType<UmbralStone>();
            int pillarCountUp = rand.Next(15, 25);

            for (int n = 0; n < pillarCountUp; n++) {
                token.ThrowIfCancellationRequested();

                int safeStartX = Math.Max(startX + 30, startX);
                int safeEndX = Math.Max(endX - 30, safeStartX + 60);

                if (safeEndX <= safeStartX) continue;

                int x = rand.Next(safeStartX, safeEndX);
                int groundY = -1;

                for (int checkY = endY - 1; checkY >= startY; checkY--) {
                    if (Main.tile[x, checkY].HasTile && Main.tileSolid[Main.tile[x, checkY].TileType]) {
                        if ((Main.tile[x, checkY].TileType == umbralStoneType ||
                             Main.tile[x, checkY].TileType == ModContent.TileType<NetherSand>()) &&
                            checkY > startY && !Main.tile[x, checkY - 1].HasTile) {
                            groundY = checkY;
                            break;
                        }
                    }
                }

                if (groundY == -1) continue;

                int height = rand.Next(3, 12);
                int baseWidth = rand.Next(1, 3);

                for (int h = 0; h < height; h++) {
                    int currentWidth = (int)(baseWidth * (1f - h / (float)height * 0.7f));
                    if (currentWidth < 1) currentWidth = 1;

                    for (int w = -currentWidth; w <= currentWidth; w++) {
                        int pi = x + w;
                        int pj = groundY - h;

                        if (pi >= startX && pi < endX && pj >= startY && pj < endY) {
                            if (!Main.tile[pi, pj].HasTile) {
                                WorldGen.PlaceTile(pi, pj, umbralStoneType, forced: true, mute: true);
                            }
                        }
                    }
                }

                await YieldAsync(token);
            }

            int pillarCountDown = rand.Next(15, 25);

            for (int n = 0; n < pillarCountDown; n++) {
                token.ThrowIfCancellationRequested();

                int safeStartX = Math.Max(startX + 30, startX);
                int safeEndX = Math.Max(endX - 30, safeStartX + 60);

                if (safeEndX <= safeStartX) continue;

                int x = rand.Next(safeStartX, safeEndX);
                int ceilingY = -1;

                for (int checkY = startY; checkY < endY; checkY++) {
                    if (Main.tile[x, checkY].HasTile && Main.tileSolid[Main.tile[x, checkY].TileType]) {
                        if (Main.tile[x, checkY].TileType == umbralStoneType &&
                            checkY < endY - 1 && !Main.tile[x, checkY + 1].HasTile) {
                            ceilingY = checkY;
                            break;
                        }
                    }
                }

                if (ceilingY == -1) continue;

                int height = rand.Next(3, 12);
                int baseWidth = rand.Next(1, 3);

                for (int h = 0; h < height; h++) {
                    int currentWidth = (int)(baseWidth * (1f - h / (float)height * 0.7f));
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

                await YieldAsync(token);
            }
        }

        /// <summary>
        /// 异步铺设灵魂沙地表层
        /// </summary>
        private static async Task GenerateNetherSandLayerAsync(int startX, int endX, int startY, int endY, UnifiedRandom rand, CancellationToken token) {
            int netherSandType = ModContent.TileType<NetherSand>();
            int umbralStoneType = ModContent.TileType<UmbralStone>();
            int processedCount = 0;

            // 在幽冥石表面铺设灵魂沙
            for (int i = startX; i < endX; i++) {
                for (int j = startY; j < endY; j++) {
                    token.ThrowIfCancellationRequested();

                    Tile tile = Main.tile[i, j];

                    if (tile.HasTile && tile.TileType == umbralStoneType) {
                        if (j > startY && !Main.tile[i, j - 1].HasTile) {
                            float sandNoise = GetPerlinNoise(i * 0.08f, j * 0.08f, rand);

                            if (sandNoise > 0.4f) {
                                int sandDepth = rand.Next(1, 4);

                                for (int depth = 0; depth < sandDepth; depth++) {
                                    int sandJ = j - 1 - depth;
                                    if (sandJ >= startY && sandJ < endY && !Main.tile[i, sandJ].HasTile) {
                                        WorldGen.PlaceTile(i, sandJ, netherSandType, forced: true, mute: true);
                                    }
                                    else {
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    processedCount++;
                    if (processedCount >= TilesPerFrame) {
                        processedCount = 0;
                        await YieldAsync(token);
                    }
                }
            }

            // 生成灵魂沙堆
            int sandPileCount = rand.Next(10, 20);

            for (int n = 0; n < sandPileCount; n++) {
                token.ThrowIfCancellationRequested();

                int safeStartX = Math.Max(startX + 50, startX);
                int safeEndX = Math.Max(endX - 50, safeStartX + 100);

                if (safeEndX <= safeStartX) continue;

                int centerX = rand.Next(safeStartX, safeEndX);
                int groundY = -1;

                for (int checkY = startY; checkY < endY; checkY++) {
                    if (Main.tile[centerX, checkY].HasTile && Main.tileSolid[Main.tile[centerX, checkY].TileType]) {
                        groundY = checkY;
                        break;
                    }
                }

                if (groundY == -1 || groundY <= startY + 5) continue;

                int pileWidth = rand.Next(4, 8);
                int pileHeight = rand.Next(2, 5);

                for (int i = centerX - pileWidth; i <= centerX + pileWidth; i++) {
                    if (i < startX || i >= endX) continue;

                    float distFromCenter = Math.Abs(i - centerX) / (float)pileWidth;
                    int heightAtPos = (int)(pileHeight * (1f - distFromCenter * distFromCenter));

                    for (int h = 0; h < heightAtPos; h++) {
                        int pileJ = groundY - 1 - h;
                        if (pileJ >= startY && pileJ < endY) {
                            if (!Main.tile[i, pileJ].HasTile || Main.tile[i, pileJ].TileType == netherSandType) {
                                WorldGen.PlaceTile(i, pileJ, netherSandType, forced: true, mute: true);
                            }
                        }
                    }
                }

                await YieldAsync(token);
            }

            // 在洞穴底部放置少量灵魂沙
            processedCount = 0;
            for (int i = startX; i < endX; i += 5) {
                for (int j = startY + 20; j < endY - 20; j += 5) {
                    token.ThrowIfCancellationRequested();

                    Tile tile = Main.tile[i, j];

                    if (!tile.HasTile && j + 1 < endY) {
                        Tile below = Main.tile[i, j + 1];
                        if (below.HasTile && Main.tileSolid[below.TileType]) {
                            if (rand.NextBool(6)) {
                                int sandLayers = rand.Next(1, 2);
                                for (int layer = 0; layer < sandLayers; layer++) {
                                    int sandJ = j - layer;
                                    if (sandJ >= startY && !Main.tile[i, sandJ].HasTile) {
                                        WorldGen.PlaceTile(i, sandJ, netherSandType, forced: true, mute: true);
                                    }
                                }
                            }
                        }
                    }

                    processedCount++;
                    if (processedCount >= TilesPerFrame) {
                        processedCount = 0;
                        await YieldAsync(token);
                    }
                }
            }
        }

        #endregion
    }
}
