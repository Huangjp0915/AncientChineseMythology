using System.Collections.Generic;
using Terraria;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace AncientChineseMythology.Systems
{
    public class XuanTieOreGenSystem : ModSystem
    {
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight) {
            int shiniesIndex = tasks.FindIndex(pass => pass.Name.Equals("Shinies"));
            if (shiniesIndex != -1) {
                tasks.Insert(shiniesIndex + 1,
                    new XuanTieOreGenPass("Generating XuanTie Ore", 237.43f));
            }
        }

        private class XuanTieOreGenPass : GenPass
        {
            public XuanTieOreGenPass(string name, float loadWeight) : base(name, loadWeight) { }

            protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
                progress.Message = "Scattering XuanTie Ore";

                int maxX = Main.maxTilesX;
                int maxY = Main.maxTilesY;
                // 与铁矿接近的密度系数
                int oreVeins = (int)((maxX * maxY) * 4E-04);

                for (int i = 0; i < oreVeins; i++) {
                    // 地表到地下岩层之间随机
                    int x = WorldGen.genRand.Next(0, maxX);
                    int y = WorldGen.genRand.Next((int)Main.worldSurface, Main.UnderworldLayer);

                    WorldGen.OreRunner(
                        x,
                        y,
                        WorldGen.genRand.Next(4, 9),    // vein radius
                        WorldGen.genRand.Next(3, 7),    // vein steps
                        (ushort)ModContent.TileType<Tiles.Placable.XuanTieOreTile>()
                    );
                }
            }
        }
    }
}