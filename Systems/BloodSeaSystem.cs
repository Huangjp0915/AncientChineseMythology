using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using Terraria.GameContent.Generation;
using System;

namespace AncientChineseMythology.Systems
{
    public class BloodSeaSystem : ModSystem
    {
        private const int BloodSeaWidth = 350;   
        public static int NearbyBloodTiles; 
        private static int BloodSeaStart => Main.maxTilesX - BloodSeaWidth;

        /* ---------- Biome 判定：只看 X 区域 ---------- */
        public override void TileCountsAvailable(ReadOnlySpan<int> tileCounts) // ← 该钩子仍在 ModSystem
        {
            NearbyBloodTiles = tileCounts[ModContent.TileType<Tiles.BloodSeaSand>()];
        }

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            // 在原版“Beaches”之后立刻插入
            int beachIdx = tasks.FindIndex(p => p.Name == "Beaches");
            if (beachIdx != -1)
                tasks.Insert(beachIdx + 1, new PassLegacy("Blood Sea", GenerateBloodSea));
        }

        private void GenerateBloodSea(GenerationProgress progress, GameConfiguration _)
        {
            progress.Message = "Painting the sea crimson...";

            int startX   = Main.maxTilesX - 350;            // 右侧 350 tile
            int endX     = Main.maxTilesX - 1;
            int topY     = (int)Main.worldSurface - 120;    // 覆盖沙坡顶部
            int bottomY  = (int)Main.worldSurface + 60;     // 至浅层海床
            ushort bloodSand = (ushort)ModContent.TileType<Tiles.BloodSeaSand>();

            // 1) 把原版沙/泥/石替换为血海砂
            for (int x = startX; x <= endX; x++)
                for (int y = topY; y <= bottomY; y++)
                {
                    Tile t = Main.tile[x, y];
                    if (t.HasTile && (t.TileType == TileID.Sand ||
                                    t.TileType == TileID.Sandstone ||
                                    t.TileType == TileID.HardenedSand ||
                                    t.TileType == TileID.Dirt))
                    {
                        WorldGen.KillTile(x, y, noItem: true);
                        WorldGen.PlaceTile(x, y, bloodSand, forced: true);
                    }
                }

            // 2) 填充海水（液体 255）
            for (int x = startX; x <= endX; x++)
                for (int y = (int)Main.worldSurface - 3; y <= (int)Main.worldSurface + 30; y++)
                    WorldGen.PlaceLiquid(x, y, (byte)LiquidID.Water, (byte)255);
        }
    }
}
