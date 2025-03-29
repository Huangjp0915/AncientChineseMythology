using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using Terraria.GameContent.Generation;
using Terraria.IO;
using Microsoft.Xna.Framework;
using SubworldLibrary;
using System;

namespace AncientChineseMythology.Subworlds
{
    public class UnderworldSubworld : Subworld
    {
        public override int Width => 2100;
        public override int Height => 1000; // 更大的高度容纳岩浆海

        public override bool ShouldSave => true;
        public override bool NoPlayerSaving => false;

        // 三步生成流程：
        // 1. 生成基础岩浆地形
        // 2. 在岩浆层上生成狱炎砖结构
        // 3. 加固并完善地形
        public override List<GenPass> Tasks => new List<GenPass>()
        {
            new LavaGroundGenPass(),
            new HellstoneStructurePass(),
            new HellstoneReinforcementPass()
        };

        public override void OnLoad()
        {
            Main.dayTime = false;
            Main.time = 0;
            Main.raining = false;
            Main.worldSurface = 800; // 调整地表高度
            Main.rockLayer = 900;   // 调整岩石层高度
            Main.spawnTileX = Width / 2;
            Main.spawnTileY = 150;  // 生成在安全区域
        }

        public override void OnEnter()
        {
            if (Main.netMode != NetmodeID.Server)
            {
                Main.NewText("进入幽冥地府...", new Color(200, 50, 50));
            }
        }
    }

    /// <summary>
    /// Pass 1: 生成基础岩浆地形
    /// </summary>
    public class LavaGroundGenPass : GenPass
    {
        public LavaGroundGenPass() : base("LavaGroundGenPass", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "塑造岩浆地形...";
            int width = Main.maxTilesX;
            int height = Main.maxTilesY;

            // 清空世界
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Main.tile[x, y].ClearTile();
                    Main.tile[x, y].WallType = 0;
                }
            }

            // 生成岩浆海（底部150格）
            int lavaHeight = 150;
            for (int x = 0; x < width; x++)
            {
                for (int y = height - lavaHeight; y < height; y++)
                {
                    WorldGen.PlaceLiquid(x, y, (byte)LiquidID.Lava, 255);
                    WorldGen.PlaceTile(x, y, TileID.Obsidian); // 底部黑曜石
                }
            }

            // 生成起伏的地狱岩地表
            int surfaceBase = height - lavaHeight - 50;
            int[] surfaceHeight = new int[width];
            surfaceHeight[0] = surfaceBase;
            for (int x = 1; x < width; x++)
            {
                int change = Main.rand.Next(-3, 4);
                surfaceHeight[x] = surfaceHeight[x - 1] + change;
                surfaceHeight[x] = Utils.Clamp(surfaceHeight[x], surfaceBase - 30, surfaceBase + 30);
            }

            // 放置地狱岩
            for (int x = 0; x < width; x++)
            {
                int groundY = surfaceHeight[x];
                for (int y = groundY; y < height - lavaHeight; y++)
                {
                    WorldGen.PlaceTile(x, y, TileID.HellstoneBrick, forced: true);
                    WorldGen.PlaceWall(x, y, WallID.HellstoneBrickUnsafe);
                }
            }
        }
    }

    /// <summary>
    /// Pass 2: 生成主要狱炎结构
    /// </summary>
    public class HellstoneStructurePass : GenPass
    {
        public HellstoneStructurePass() : base("HellstoneStructurePass", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "构筑炼狱核心...";
            int width = Main.maxTilesX;
            int height = Main.maxTilesY;

            // 生成中央巨型黑曜石柱
            int centerX = width / 2;
            for (int x = centerX - 20; x <= centerX + 20; x++)
            {
                for (int y = 100; y < height - 150; y++)
                {
                    if (Math.Abs(x - centerX) <= 15 || y % 10 < 5)
                    {
                        WorldGen.PlaceTile(x, y, TileID.ObsidianBrick, forced: true);
                    }
                }
            }

            // 生成悬浮的狱炎平台
            for (int i = 0; i < 5; i++)
            {
                int platformY = 200 + i * 150;
                int platformWidth = 80 + i * 20;
                for (int x = centerX - platformWidth/2; x < centerX + platformWidth/2; x++)
                {
                    WorldGen.PlaceTile(x, platformY, TileID.HellstoneBrick, forced: true);
                    // 添加链条
                    if (x % 10 == 0)
                    {
                        for (int y = platformY - 1; y > platformY - 50; y--)
                        {
                            WorldGen.PlaceTile(x, y, TileID.Chain, forced: true);
                        }
                    }
                }
            }

            // 随机生成岩浆池
            for (int i = 0; i < 10; i++)
            {
                int poolX = Main.rand.Next(100, width - 100);
                int poolY = Main.rand.Next(300, height - 200);
                int poolWidth = Main.rand.Next(20, 40);
                int poolHeight = Main.rand.Next(10, 20);

                for (int x = poolX; x < poolX + poolWidth; x++)
                {
                    for (int y = poolY; y < poolY + poolHeight; y++)
                    {
                        WorldGen.PlaceLiquid(x, y, (byte)LiquidID.Lava, 255);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Pass 3: 加固结构并添加细节
    /// </summary>
    public class HellstoneReinforcementPass : GenPass
    {
        public HellstoneReinforcementPass() : base("HellstoneReinforcementPass", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "完善炼狱细节...";
            int width = Main.maxTilesX;
            int height = Main.maxTilesY;

            // 添加地狱火把
            for (int x = 50; x < width - 50; x += 30)
            {
                for (int y = 150; y < height - 200; y += 80)
                {
                    if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.HellstoneBrick)
                    {
                        WorldGen.PlaceTile(x, y - 1, TileID.Torches, style: 24); // 地狱火把
                    }
                }
            }

            // 添加随机黑曜石尖刺
            for (int i = 0; i < 20; i++)
            {
                int spikeX = Main.rand.Next(100, width - 100);
                int spikeBaseY = Main.rand.Next(300, height - 100);
                
                int spikeHeight = Main.rand.Next(10, 30);
                int spikeWidth = Main.rand.Next(3, 7);

                for (int x = spikeX - spikeWidth/2; x <= spikeX + spikeWidth/2; x++)
                {
                    for (int y = spikeBaseY; y > spikeBaseY - spikeHeight; y--)
                    {
                        WorldGen.PlaceTile(x, y, TileID.Obsidian, forced: true);
                    }
                }
            }

            // 生成Boss竞技场
            int arenaCenterX = width / 2;
            int arenaY = 150;
            int arenaWidth = 120;
            int arenaHeight = 40;

            // 平台
            for (int x = arenaCenterX - arenaWidth/2; x <= arenaCenterX + arenaWidth/2; x++)
            {
                WorldGen.PlaceTile(x, arenaY, TileID.HellstoneBrick, forced: true);
            }

            // 防护墙
            for (int y = arenaY - 20; y <= arenaY; y++)
            {
                WorldGen.PlaceTile(arenaCenterX - arenaWidth/2 - 2, y, TileID.ObsidianBrick);
                WorldGen.PlaceTile(arenaCenterX + arenaWidth/2 + 2, y, TileID.ObsidianBrick);
            }
        }
    }
}