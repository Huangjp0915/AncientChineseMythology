using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using Terraria.Localization;
using Terraria.GameContent.Generation;
using Terraria.IO;
using AncientChineseMythology.Tiles.Herbs;

namespace AncientChineseMythology.Worldgen
{
    public class IronArmorFlowerGenSystem : ModSystem
    {
        internal static LocalizedText PassText;
        private static readonly Point[] Off4 = { new(1,0), new(-1,0), new(0,1), new(0,-1) };

        public override void SetStaticDefaults() =>
            PassText = Mod.GetLocalization("WorldGen.IronArmorFlowerPass");

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double _) {
            int idx = tasks.FindIndex(g => g.Name == "Sky Lakes");
            if (idx != -1)
                tasks.Insert(idx + 1, new PassLegacy(PassText.Value, Generate));
        }

        private void Generate(GenerationProgress progress, GameConfiguration _) {
            progress.Message = PassText.Value;
            int tries = Main.maxTilesX / 3;

            for (int t = 0; t < tries; t++) {
                int i = WorldGen.genRand.Next(300, Main.maxTilesX - 300);
                int j = WorldGen.genRand.Next((int)Main.worldSurface, (int)Main.rockLayer);

                while (j < Main.maxTilesY - 200 && !Main.tileSolid[Main.tile[i,j].TileType]) j++;
                int groundType = Main.tile[i,j].TileType;
                if (groundType != TileID.Stone && groundType != TileID.Mud) continue;

                bool nearOre = false;
                foreach (Point p in Off4) {
                    Tile t2 = Framing.GetTileSafely(i + p.X, j + p.Y);
                    if (t2.HasTile && TileID.Sets.Ore[t2.TileType]) { nearOre = true; break; }
                }
                if (!nearOre) continue;

                WorldGen.PlaceTile(i, j - 1,
                    ModContent.TileType<IronArmorFlowerHerbTile>(), mute:true);
            }
        }

        private void SpawnStarflower(CommandCaller caller, string input, string[] args) {
			Player p = caller.Player;
			Point tilePos = p.Center.ToTileCoordinates();
			WorldGen.PlaceTile(tilePos.X, tilePos.Y, ModContent.TileType<IronArmorFlowerHerbTile>());
		}
    }
}
