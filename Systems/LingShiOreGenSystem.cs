// AncientChineseMythology/Systems/LingShiOreGenSystem.cs
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using Terraria.IO;
using AncientChineseMythology.Tiles.Placable;

namespace AncientChineseMythology.Systems
{
    public class LingShiOreGenSystem : ModSystem
    {
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int shiniesIndex = tasks.FindIndex(p => p.Name.Equals("Shinies"));
            if (shiniesIndex != -1)
            {
                tasks.Insert(shiniesIndex + 1,
                    new LingShiOreGenPass("Generating LingShi Ore", 123.456f));
            }
        }

        private class LingShiOreGenPass : GenPass
        {
            public LingShiOreGenPass(string name, float weight) : base(name, weight) {}

            protected override void ApplyPass(GenerationProgress progress,
                                              GameConfiguration _)
            {
                progress.Message = "Scattering LingShi crystals";

                int maxX = Main.maxTilesX;
                int maxY = Main.maxTilesY;

                /* 密度：约为铁矿(0.0008)的一半 ≈ 金 / 铂 */
                int oreVeins = (int)(maxX * maxY * 5E-05);

                for (int i = 0; i < oreVeins; i++)
                {
                    int x = WorldGen.genRand.Next(0, maxX);
                    int y = WorldGen.genRand.Next((int)Main.rockLayer,
                                                  (int)Main.UnderworldLayer);  // 洞穴层往下

                    WorldGen.OreRunner(
                        x, y,
                        WorldGen.genRand.Next(5, 9),   // 矿脉宽度
                        WorldGen.genRand.Next(4, 8),   // 矿脉深度
                        (ushort)ModContent.TileType<LingShiOreTile>());
                }
            }
        }
    }
}
