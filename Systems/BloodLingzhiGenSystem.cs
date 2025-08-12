using AncientChineseMythology.Tiles.Herbs;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace AncientChineseMythology.Systems
{
    public class BloodLingzhiGenSystem : ModSystem
    {
        internal static LocalizedText PassText;

        public override void SetStaticDefaults() =>
            PassText = Mod.GetLocalization("WorldGen.BloodLingzhiPass");

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
                int j = WorldGen.genRand.Next((int)Main.worldSurface, (int)Main.rockLayer);

                //下落到实心方块顶面
                while (j < Main.maxTilesY - 200 && !Main.tile[i, j].HasTile) j++;
                if (!Main.tile[i, j].HasTile || !Main.tileSolid[Main.tile[i, j].TileType]) continue;

                //位置必须位于猩红生物群系
                if (!WorldgenHelpers.IsCrimson(i, j)) continue;

                WorldGen.PlaceTile(i, j - 1,
                    ModContent.TileType<BloodLingzhiHerbTile>(), mute: true);
            }
        }
        private void SpawnStarflower(CommandCaller caller, string input, string[] args) {
            Player p = caller.Player;
            Point tilePos = p.Center.ToTileCoordinates();
            WorldGen.PlaceTile(tilePos.X, tilePos.Y, ModContent.TileType<BloodLingzhiHerbTile>());
        }
    }

    internal static class WorldgenHelpers
    {
        public static bool IsCrimson(int i, int j) {
            //判定方式：检查 tile 或 wall 是否为猩红系
            Tile t = Framing.GetTileSafely(i, j);
            return t.TileType == TileID.Crimstone || t.TileType == TileID.CrimsonGrass
                || t.TileType == TileID.CrimstoneBrick
                || t.WallType == WallID.CrimsonGrassUnsafe || t.WallType == WallID.CrimstoneUnsafe
                || t.WallType == WallID.Flesh;
        }
    }
}
