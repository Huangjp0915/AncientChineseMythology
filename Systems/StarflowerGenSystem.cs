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
    public class StarflowerGenSystem : ModSystem
    {
        internal static LocalizedText StarflowerPassText;

        public override void SetStaticDefaults() {
            StarflowerPassText = Mod.GetLocalization("WorldGen.StarflowerPass");
        }

        // 将一个自定义 GenPass 插入原版任务表 —— 参考 ExampleOreSystem :contentReference[oaicite:4]{index=4}
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight) {
            int index = tasks.FindIndex(g => g.Name.Equals("Sky Lakes")); // 在云岛生成完成后插入
            if (index != -1) {
                tasks.Insert(index + 1, new PassLegacy(StarflowerPassText.Value, GenerateStarflowers));
            }
        }

        private void GenerateStarflowers(GenerationProgress progress, GameConfiguration _config) {
            progress.Message = StarflowerPassText.Value;
            int tries = Main.maxTilesX / 3; // roughly one per 3 screens

            for (int t = 0; t < tries; t++) {
                int i = WorldGen.genRand.Next(100, Main.maxTilesX - 100);
                // 找到天空岛表面
                for (int j = 200; j < Main.worldSurface; j++) {
                    Tile tile = Framing.GetTileSafely(i, j);
                    if (tile.HasTile && (tile.TileType == TileID.Cloud || tile.TileType == TileID.RainCloud)) {
                        WorldGen.PlaceTile(i, j - 1,
                            ModContent.TileType<StarflowerHerbTile>(), mute: true, style: 0);
                        break;
                    }
                }
            }
        }

        private void SpawnStarflower(CommandCaller caller, string input, string[] args) {
            Player p = caller.Player;
            Point tilePos = p.Center.ToTileCoordinates();
            WorldGen.PlaceTile(tilePos.X, tilePos.Y, ModContent.TileType<StarflowerHerbTile>());
        }
    }
}
