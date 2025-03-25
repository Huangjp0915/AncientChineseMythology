using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using Terraria.GameContent.Generation;
using Terraria.IO;
using Microsoft.Xna.Framework;
using SubworldLibrary;
using Terraria.Audio;

namespace AncientChineseMythology.Subworlds
{
    public class ThirtyThreeHeavens : Subworld
    {
        public override int Width => 2100;
        public override int Height => 600;


        // 保存子世界数据
        public override bool ShouldSave => true;
        public override bool NoPlayerSaving => false;

        // 三步：云砖地形 -> 在云砖顶上加5层彩虹 -> 平坦并向下填补
        public override List<GenPass> Tasks => new List<GenPass>()
        {
            new CloudGroundGenPass(),
            new RainbowFirstOverlayPass(),
            new RainbowFlattenAndFillGapsPass()
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

                // 获取音乐槽位
                int slot = MusicLoader.GetMusicSlot(Mod, "Music/HeavenTheme");
                // 先停止当前音乐（防止有淡出或切换效果）
                //SoundEngine.StopMusic(true);
                // 强制播放指定的音乐
                //SoundEngine.PlayMusic(slot, 0f);
            }
        }
    }

    /// <summary>
    /// Pass 1: 生成随机云砖地形
    /// </summary>
    public class CloudGroundGenPass : GenPass
    {
        public CloudGroundGenPass() : base("CloudGroundGenPass", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "生成随机云砖地面...";
            int width = Main.maxTilesX;
            int height = Main.maxTilesY;

            // 清空
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Main.tile[x, y].ClearTile();
                    Main.tile[x, y].WallType = 0;
                }
            }

            // 随机地表
            int surfaceBase = 380;
            int[] surfaceHeight = new int[width];
            surfaceHeight[0] = surfaceBase;
            for (int x = 1; x < width; x++)
            {
                int change = Main.rand.Next(-1, 2);
                surfaceHeight[x] = surfaceHeight[x - 1] + change;
                surfaceHeight[x] = Utils.Clamp(surfaceHeight[x], surfaceBase - 20, surfaceBase + 20);
            }

            // 放置云砖
            for (int x = 0; x < width; x++)
            {
                int groundY = surfaceHeight[x];
                // 顶层云砖
                WorldGen.PlaceTile(x, groundY, TileID.Cloud, forced: true);
                // 下面全是云砖+云墙
                for (int y = groundY + 1; y < height; y++)
                {
                    WorldGen.PlaceTile(x, y, TileID.Cloud, forced: true);
                    WorldGen.PlaceWall(x, y, WallID.Cloud);
                }
            }
        }
    }

    /// <summary>
    /// Pass 2: 在云砖顶层上方再铺 5 层彩虹砖
    /// </summary>
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
                // 找到云砖最顶层
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
                    // 上方 5 层
                    int startY = topCloudY - 1;
                    int endY = topCloudY - overlayThickness; // topCloudY - 5
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

    /// <summary>
    /// Pass 3: 统一最上层并向下填补所有空隙
    /// </summary>
    public class RainbowFlattenAndFillGapsPass : GenPass
    {
        public RainbowFlattenAndFillGapsPass() : base("RainbowFlattenAndFillGapsPass", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "平坦顶层并向下填满缝隙...";
            int width = Main.maxTilesX;
            int height = Main.maxTilesY;

            // 1. 找到最上方的彩虹砖
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

            // 2. 假设想要 5 层厚度 => topY..topY+4
            int thickness = 5;
            int topY = globalTopRainbowY;
            int bottomY = topY + thickness - 1;
            if (bottomY >= height) bottomY = height - 1;

            // 3. 先清理 topY 上方所有彩虹砖，再把 [topY..bottomY] 全部设为彩虹砖
            for (int x = 0; x < width; x++)
            {
                // 清理 topY 之上
                for (int y = 0; y < topY; y++)
                {
                    if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.RainbowBrick)
                    {
                        Main.tile[x, y].ClearTile();
                    }
                }
                // [topY..bottomY] 强制彩虹砖
                for (int fillY = topY; fillY <= bottomY; fillY++)
                {
                    Main.tile[x, fillY].ClearTile();
                    WorldGen.PlaceTile(x, fillY, TileID.RainbowBrick, forced: true);
                }
            }

            // 4. **向下填补**：从顶层最下方一直到碰到云砖或地图底，凡是空气都替换成彩虹砖
            for (int x = 0; x < width; x++)
            {
                // 先找到平坦带的最底 -> bottomY
                // 然后从 bottomY+1 往下，直到遇到云砖(Cloud) 或超出地图
                int fillStartY = bottomY + 1;
                for (int y = fillStartY; y < height; y++)
                {
                    // 如果遇到云砖就停止
                    if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Cloud)
                        break;

                    // 否则，若是空气或别的砖，都清理后放彩虹砖
                    if (!Main.tile[x, y].HasTile || Main.tile[x, y].TileType != TileID.Cloud)
                    {
                        Main.tile[x, y].ClearTile();
                        WorldGen.PlaceTile(x, y, TileID.RainbowBrick, forced: true);
                    }
                }
            }
        }
    }
}
