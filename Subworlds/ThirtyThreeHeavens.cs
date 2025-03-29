using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using Terraria.GameContent.Generation;
using Terraria.IO;
using SubworldLibrary;
using Terraria.Audio;
using Terraria.Utilities;
using StructureHelper.API;
using static StructureHelper.API.Generator; 
using Terraria.DataStructures;

namespace AncientChineseMythology.Subworlds
{
    // 三十三重天子世界
    public class ThirtyThreeHeavens : Subworld
    {
        public override int Width => 2100;
        public override int Height => 600;

        // 保存子世界数据
        public override bool ShouldSave => true;
        public override bool NoPlayerSaving => false;

        // 顺序执行各个生成阶段：
        // 1. 生成云砖地形
        // 2. 在云砖顶铺设 5 层彩虹砖
        // 3. 对彩虹砖顶层平整并向下填补空隙
        // 4. 最后在地表放置地牢建筑（从 TEdit 导出的 Schematic 文件加载）
        public override List<GenPass> Tasks => new List<GenPass>()
        {
            new CloudGroundGenPass(),
            new RainbowFirstOverlayPass(),
            new RainbowFlattenAndFillGapsPass(),
            new CelestialDungeonStructurePass("CelestialDungeonStructurePass", 1f),
            new FloatingIslandGenPass()
        };

        public override void OnLoad()
        {
            Main.dayTime = true;
            Main.time = 27000;
            Main.worldSurface = 400;
            Main.rockLayer = 500;
            Main.spawnTileX = Width / 2;
            Main.spawnTileY = 380;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            if (Main.netMode != NetmodeID.Server)
            {
                Main.NewText("进入子世界：ThirtyThreeHeavens", Color.LightGreen);
                //Main.NewText($"DungeonX={Main.dungeonX}, DungeonY={Main.dungeonY}", Color.Orange);
                Main.NewText($"maxTilesX={this.Width}, maxTilesY={this.Height}", Color.Orange);
                int slot = MusicLoader.GetMusicSlot(Mod, "Music/HeavenTheme");
                //SoundEngine.StopMusic(true);
                //SoundEngine.PlayMusic(slot, 0f);
            }
        }
    }

    #region 云砖地形和彩虹砖覆盖
    public class CloudGroundGenPass : GenPass
    {
        public CloudGroundGenPass() : base("CloudGroundGenPass", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "生成随机云砖地面...";
            int width = ((Subworld)SubworldSystem.Current).Width;
            int height = ((Subworld)SubworldSystem.Current).Height;
            // 清空全图
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Main.tile[x, y].ClearTile();
                    Main.tile[x, y].WallType = 0;
                }
            }
            // 随机地表高度
            int surfaceBase = 380;
            int[] surfaceHeight = new int[width];
            surfaceHeight[0] = surfaceBase;
            for (int x = 1; x < width; x++)
            {
                int change = Main.rand.Next(-1, 2);
                surfaceHeight[x] = surfaceHeight[x - 1] + change;
                surfaceHeight[x] = Utils.Clamp(surfaceHeight[x], surfaceBase - 20, surfaceBase + 20);
            }
            // 放置云砖和云墙
            for (int x = 0; x < width; x++)
            {
                int groundY = surfaceHeight[x];
                WorldGen.PlaceTile(x, groundY, TileID.Cloud, forced: true);
                for (int y = groundY + 1; y < height; y++)
                {
                    WorldGen.PlaceTile(x, y, TileID.Cloud, forced: true);
                    WorldGen.PlaceWall(x, y, WallID.Cloud);
                }
            }
        }
    }

    public class RainbowFirstOverlayPass : GenPass
    {
        public RainbowFirstOverlayPass() : base("RainbowFirstOverlayPass", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "在云砖顶上加5层彩虹砖...";
            int width = Main.maxTilesX;
            int height = Main.maxTilesY;
            int overlayThickness = 5;
            for (int x = 0; x < width; x++)
            {
                int topCloudY = -1;
                for (int y = 0; y < height; y++)
                {
                    if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Cloud)
                    {
                        topCloudY = y;
                        break;
                    }
                }
                if (topCloudY != -1)
                {
                    int startY = topCloudY - 1;
                    int endY = topCloudY - overlayThickness;
                    if (endY < 0) endY = 0;
                    if (startY < 0) continue;
                    for (int fillY = startY; fillY >= endY; fillY--)
                    {
                        if (fillY >= 0 && fillY < height)
                        {
                            Main.tile[x, fillY].ClearTile();
                            WorldGen.PlaceTile(x, fillY, TileID.RainbowBrick, forced: true);
                        }
                    }
                }
            }
        }
    }

    public class RainbowFlattenAndFillGapsPass : GenPass
    {
        public RainbowFlattenAndFillGapsPass() : base("RainbowFlattenAndFillGapsPass", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "平坦顶层并向下填满缝隙...";
            int width = Main.maxTilesX;
            int height = Main.maxTilesY;
            int globalTopRainbowY = height;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.RainbowBrick)
                    {
                        if (y < globalTopRainbowY)
                            globalTopRainbowY = y;
                        break;
                    }
                }
            }
            if (globalTopRainbowY >= height) return;
            int thickness = 5;
            int topY = globalTopRainbowY;
            int bottomY = topY + thickness - 1;
            if (bottomY >= height) bottomY = height - 1;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < topY; y++)
                {
                    if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.RainbowBrick)
                    {
                        Main.tile[x, y].ClearTile();
                    }
                }
                for (int fillY = topY; fillY <= bottomY; fillY++)
                {
                    Main.tile[x, fillY].ClearTile();
                    WorldGen.PlaceTile(x, fillY, TileID.RainbowBrick, forced: true);
                }
            }
            for (int x = 0; x < width; x++)
            {
                int fillStartY = bottomY + 1;
                for (int y = fillStartY; y < height; y++)
                {
                    if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Cloud)
                        break;
                    if (!Main.tile[x, y].HasTile || Main.tile[x, y].TileType != TileID.Cloud)
                    {
                        Main.tile[x, y].ClearTile();
                        WorldGen.PlaceTile(x, y, TileID.RainbowBrick, forced: true);
                    }
                }
            }
        }
    }
    #endregion

    #region 天界地牢结构导入
public class CelestialDungeonStructurePass : GenPass
    {
        public CelestialDungeonStructurePass(string name, float loadWeight) : base(name, loadWeight) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "加载并放置天界地牢 (子世界 2100x600) ...";

    
            int worldWidth = ((Subworld)SubworldSystem.Current).Width; 
            int worldHeight = ((Subworld)SubworldSystem.Current).Height;

            // 2. 加载单结构...
            string path = "structures/celestialdungeon";
            Point16 dims = GetStructureDimensions(
                path,
                ModContent.GetInstance<AncientChineseMythology>(),
                false
            );
            int structureW = dims.X;
            int structureH = dims.Y;

            // 3. 计算 & Clamp 放置坐标
            int placeX = 100;
            if (placeX + structureW >= worldWidth) {
                placeX = worldWidth - structureW;
                if (placeX < 0) placeX = 0;
            }

            // 扫描地表
            int minSurfaceY = worldHeight - 1;
            for (int x = placeX; x < placeX + structureW; x++) {
                for (int y = 0; y < worldHeight; y++) {
                    if (Main.tile[x, y].HasTile && Main.tileSolid[Main.tile[x, y].TileType]) {
                        if (y < minSurfaceY)
                            minSurfaceY = y;
                        break;
                    }
                }
            }
            int placeY = minSurfaceY - structureH;
            if (placeY < 0) placeY = 0;
            if (placeY + structureH >= worldHeight) {
                placeY = worldHeight - structureH;
                if (placeY < 0) placeY = 0;
            }

            if (placeX < 0 || placeX + structureW > worldWidth ||
                placeY < 0 || placeY + structureH > worldHeight) {
                progress.Message = "地牢结构放置位置仍越界, 生成停止！";
                return;
            }

            // 4. 生成结构
            GenerateStructure(
                path,
                new Point16((short)placeX, (short)placeY),
                ModContent.GetInstance<AncientChineseMythology>(),
                false, 
                false,
                StructureHelper.GenFlags.None
            );

            progress.Message = $"天界地牢放置完毕: place=({placeX},{placeY}), dims=({structureW},{structureH}) in Subworld {worldWidth}x{worldHeight}";
        }
    }
    #endregion

    public class FloatingIslandGenPass : GenPass
    {
        public FloatingIslandGenPass() : base("FloatingIslandGenPass", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "生成浮空岛...";

            // 1. 获取子世界大小 (避免使用 Main.maxTilesX / Main.maxTilesY)
            int worldWidth = ((Subworld)SubworldLibrary.SubworldSystem.Current).Width; 
            int worldHeight = ((Subworld)SubworldLibrary.SubworldSystem.Current).Height;

            // 2. 准备加载浮空岛结构文件: structures/floatingisland.shstruct (示例文件名)
            string path = "structures/floatingisland";

            // 3. 获取结构宽高
            Point16 dims = Generator.GetStructureDimensions(
                path,
                ModContent.GetInstance<AncientChineseMythology>(),
                false
            );
            int islandW = dims.X;
            int islandH = dims.Y;

            // 4. 随机/固定 X 坐标，并 clamp
            int placeX = WorldGen.genRand.Next(worldWidth - islandW);
            // 若需要固定, 直接: int placeX = 100; 并再 clamp 即可

            // 5. 计算/限制 Y 坐标：要求整座浮空岛 [顶部>=0], [底部<300]
            int maxY = 300 - islandH; // 岛底不能超过 y=299 (若 islandH=1)
            if (maxY < 0)
            {
                // 说明岛本身太高 or 300 太小，没有足够空间放置
                progress.Message = "无法放置浮空岛：高度不足300!";
                return;
            }
            int placeY = WorldGen.genRand.Next(0, maxY + 1);

            // 再二次 clamp (确保不超世界实际高度, 虽然本例中我们只到300)
            if (placeY + islandH >= worldHeight)
            {
                // 若真的超过世界边界(小概率)，就顶到 worldHeight - islandH
                placeY = worldHeight - islandH;
                if (placeY < 0) placeY = 0;
            }

            // 6. 最终检查
            if (placeX < 0 || placeX + islandW > worldWidth ||
                placeY < 0 || placeY + islandH > worldHeight)
            {
                progress.Message = "浮空岛生成越界，停止。";
                return;
            }

            // 7. 使用 Generator.GenerateStructure 放置单结构
            Generator.GenerateStructure(
                path,
                new Point16((short)placeX, (short)placeY),
                ModContent.GetInstance<AncientChineseMythology>(),
                false,  // fullPath
                false,  // ignoreNull
                StructureHelper.GenFlags.None
            );

            progress.Message = $"浮空岛生成完成: place=({placeX},{placeY}), dims=({islandW},{islandH})";
        }
    }
}