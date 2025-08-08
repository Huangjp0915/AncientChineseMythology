using AncientChineseMythology.Tiles.Placable;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Generation;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace AncientChineseMythology.Content.Systems
{
    public class ShengZhuStatueSystem : ModSystem
    {

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight) {
            int idx = tasks.FindIndex(p => p.Name == "Sky Lakes");
            if (idx != -1) tasks.Insert(idx + 1, new PassLegacy("ShengZhu Statue", Generate));
        }
        private void Generate(GenerationProgress progress, GameConfiguration _) {
            progress.Message = "Placing the ShengZhu Statue";
            while (true) {
                int i = WorldGen.genRand.Next(200, Main.maxTilesX - 200);
                int j = (int)Main.worldSurface - 10;
                for (; j < Main.worldSurface + 50; j++) {
                    if (Main.tileSolid[Main.tile[i, j].TileType]) {
                        WorldGen.PlaceObject(i, j, ModContent.TileType<ShengZhuStatueTile>());
                        if (Main.tile[i, j].HasTile) return;
                        break;
                    }
                }
            }
        }

        internal void TriggerStatue(Point16 pos, int playerID) {
            Player pl = Main.player[playerID];

            /* ① 删除背包 + 装备栏中全部 *Charm 物品 */
            for (int k = 0; k < pl.inventory.Length; k++) {
                if (IsCharm(pl.inventory[k])) pl.inventory[k].TurnToAir();
            }
            for (int k = 0; k < pl.armor.Length; k++) {                 //20 个装备/配饰槽 :contentReference[oaicite:3]{index=3}
                if (IsCharm(pl.armor[k])) pl.armor[k].TurnToAir();
            }

            Main.NewText("圣主雕像散发圣光，符咒尽数化尘", 255, 240, 150);
        }

        private static bool IsCharm(Item item) =>
            item.ModItem != null && item.ModItem.GetType().Name.Contains("Charm");

    }
}
