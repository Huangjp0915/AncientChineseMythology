using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using Terraria.Localization;
using Terraria.GameContent.Generation;
using Terraria.IO;

namespace AncientChineseMythology.Worldgen
{
    public class BlazingFlowerGenSystem : ModSystem
    {
        internal static LocalizedText PassText;

        public override void SetStaticDefaults() =>
            PassText = Mod.GetLocalization("WorldGen.BlazingFlowerPass");

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double _) {
            int idx = tasks.FindIndex(g => g.Name == "Underworld");
            if (idx == -1) idx = tasks.Count - 1;
            tasks.Insert(idx + 1, new PassLegacy(PassText.Value, Generate));
        }

        private void Generate(GenerationProgress progress, GameConfiguration _) {
            progress.Message = PassText.Value;
            int tries = Main.maxTilesX / 3;

            for (int n = 0; n < tries; n++) {
                int i = WorldGen.genRand.Next(200, Main.maxTilesX - 200);
                int j = (int)Main.UnderworldLayer + 50;                       // 从地狱层顶向下找灰烬
                while (j < Main.maxTilesY - 200 && !Main.tile[i,j].HasTile) j++;
                if (Main.tile[i,j].TileType != TileID.Ash) continue;

                WorldGen.PlaceTile(i, j - 1,
                    ModContent.TileType<Tiles.BlazingFlowerHerbTile>(), mute:true);
            }
        }
    }
}
