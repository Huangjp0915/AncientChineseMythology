using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using Terraria.IO;
using AncientChineseMythology.Tiles.Placable;

namespace AncientChineseMythology.Systems
{
    public class CupriteOreGenSystem : ModSystem
    {
        // 重写 ModifyWorldGenTasks
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            // 在生成步骤列表中查找名称为 "Shinies" 的任务（矿石生成阶段）
            int shiniesIndex = tasks.FindIndex(genpass => genpass.Name.Equals("Shinies"));
            if (shiniesIndex != -1)
            {
                // 在 "Shinies" 后插入自定义矿石生成步骤
                tasks.Insert(shiniesIndex + 1, new CupriteOreGenPass("Generating Cuprite Ore", 237.4298f));
            }
        }

        private class CupriteOreGenPass : GenPass
        {
            public CupriteOreGenPass(string name, float loadWeight) : base(name, loadWeight) { }

            protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
            {
                progress.Message = "Generating Cuprite Ore near lava";

                int maxTilesX = Main.maxTilesX;
                int maxTilesY = Main.maxTilesY;
                // 根据世界面积确定生成矿脉的数量，系数可调整
                int oreVeins = (int)((maxTilesX * maxTilesY) * 4E-03);

                for (int i = 0; i < oreVeins; i++)
                {
                    // 随机选择一个位置：从 Main.rockLayer 到世界底部上方200个瓷砖之间
                    int x = WorldGen.genRand.Next(0, maxTilesX);
                    int y = WorldGen.genRand.Next((int)Main.rockLayer, maxTilesY - 200);

                    bool nearLava = false;
                    // 检查周围 7×7 区域内是否存在熔岩
                    for (int xx = x - 3; xx <= x + 3 && !nearLava; xx++)
                    {
                        for (int yy = y - 3; yy <= y + 3 && !nearLava; yy++)
                        {
                            if (xx < 0 || xx >= maxTilesX || yy < 0 || yy >= maxTilesY)
                                continue;
                            Tile tile = Main.tile[xx, yy];
                            if (tile != null && tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Lava)
                            {
                                nearLava = true;
                            }
                        }
                    }

                    if (nearLava)
                    {
                        // 使用 OreRunner 生成矿脉，参数可调整以控制矿脉大小
                        WorldGen.OreRunner(
                            x,
                            y,
                            WorldGen.genRand.Next(4, 9),  // 矿脉宽度
                            WorldGen.genRand.Next(3, 8),  // 矿脉深度
                            (ushort)ModContent.TileType<CupriteOreTile>()
                        );
                    }
                }
            }
        }
    }
}
