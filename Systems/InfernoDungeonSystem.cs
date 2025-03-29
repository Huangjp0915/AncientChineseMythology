using Terraria;
using Terraria.IO;
using Terraria.WorldBuilding;
using Terraria.ModLoader;
using System.Collections.Generic;
using Terraria.GameContent.Generation;
using Terraria.ID;
using AncientChineseMythology.Tiles;

namespace AncientChineseMythology.Systems
{
    public class InfernoDungeonSystem : ModSystem
    {
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int index = tasks.FindIndex(genPass => genPass.Name == "Final Cleanup");
            if (index != -1)
            {
                tasks.Insert(index + 1, new PassLegacy("生成地狱地牢", GenerateInfernoDungeon));
            }
        }

        private void GenerateInfernoDungeon(GenerationProgress progress, GameConfiguration config)
        {
            if (!PostMoonLordSystem.MoonLordDefeated) return;

            progress.Message = "冥界之门正在显现...";
            bool leftSide = Main.rand.NextBool();
            int x = leftSide ? 300 : Main.maxTilesX - 300;
            int y = FindSurfaceHeight(x);

            // 生成垂直通道
            GenerateVerticalShaft(x, y);

            // 生成触发区域
            GenerateTeleportationZone(x, y + 200);
        }

        private int FindSurfaceHeight(int x)
        {
            for (int y = 0; y < Main.maxTilesY; y++)
            {
                if (Main.tile[x, y].HasTile && Main.tileSolid[Main.tile[x, y].TileType])
                    return y + 15;
            }
            return 150;
        }

        private void GenerateVerticalShaft(int startX, int startY)
        {
            // 5x5核心通道
            for (int x = startX - 2; x <= startX + 2; x++)
            {
                for (int y = startY; y < startY + 250; y++)
                {
                    WorldGen.KillTile(x, y);
                    WorldGen.PlaceTile(x, y, TileID.HellstoneBrick, forced: true);
                    WorldGen.PlaceWall(x, y, WallID.HellstoneBrickUnsafe);
                }
            }

            // 两侧加固结构
            for (int x = startX - 5; x <= startX + 5; x++)
            {
                for (int y = startY; y < startY + 250; y++)
                {
                    if (x < startX - 2 || x > startX + 2)
                    {
                        WorldGen.PlaceTile(x, y, TileID.HellstoneBrick, forced: true);
                    }
                }
            }
        }

        private void GenerateTeleportationZone(int x, int y)
        {
            for (int i = x - 3; i <= x + 3; i++)
            {
                for (int j = y - 3; j <= y + 3; j++)
                {
                    WorldGen.PlaceTile(i, j, ModContent.TileType<TeleportationTile>());
                }
            }
        }
    }
}