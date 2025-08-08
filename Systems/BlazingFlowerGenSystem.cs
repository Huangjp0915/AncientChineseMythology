using AncientChineseMythology.Tiles.Herbs;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

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
                int j = Main.UnderworldLayer + 50;                       // 从地狱层顶向下找灰烬
                while (j < Main.maxTilesY - 200 && !Main.tile[i, j].HasTile) j++;
                if (Main.tile[i, j].TileType != TileID.Ash) continue;

                WorldGen.PlaceTile(i, j - 1,
                    ModContent.TileType<BlazingFlowerHerbTile>(), mute: true);
            }
        }

        private void SpawnStarflower(CommandCaller caller, string input, string[] args) {
            Player p = caller.Player;
            Point tilePos = p.Center.ToTileCoordinates();
            WorldGen.PlaceTile(tilePos.X, tilePos.Y, ModContent.TileType<BlazingFlowerHerbTile>());
        }
    }
}
