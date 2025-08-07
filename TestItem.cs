using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using InnoVault.GameSystem;
using System.Collections.Generic;
using System.IO;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;

namespace AncientChineseMythology
{
    internal class TestItem : ModItem
    {
        public override string Texture => "AncientChineseMythology/icon";

        public override bool IsLoadingEnabled(Mod mod) {
            return true;
        }

        public override void SetDefaults() {
            Item.width = 80;
            Item.height = 80;
            Item.damage = 9999;
            Item.DamageType = DamageClass.Default;
            Item.useAnimation = Item.useTime = 13;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 2.25f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shootSpeed = 8f;
            Item.shoot = ProjectileID.PurificationPowder;
            Item.value = 100;
            Item.rare = ItemRarityID.Yellow;
        }

        public override void UpdateInventory(Player player) {
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            return false;
        }

        public override void HoldItem(Player player) {
        }

        public override bool? UseItem(Player player) {
            return true;
        }
    }

    public class 异世界复制 : SaveMod
    {
        public override string SavePath => Path.Combine(VaultSave.RootPath, "ModDatas", "森罗万象", "子世界", "异世界_v1.nbt");
        public override void Load() => Mod.EnsureFileFromMod("子世界/异世界_v1.nbt", SavePath);
        //###
        public override void SaveData(TagCompound tag) {
            tag["span"] = new Point16(Main.spawnTileX, Main.spawnTileY);

            List<TagCompound> worldTiles = [];
            for (int i = 0; i < Main.tile.Width; i++) {
                for (int j = 0; j < Main.tile.Height; j++) {
                    Tile tile = Main.tile[i, j];
                    if (!tile.HasTile && tile.WallType == 0 && tile.LiquidAmount == 0) {
                        continue;
                    }
                    TagCompound tileData = new TagCompound();
                    tileData["a"] = new Point16(i, j);
                    tileData["b"] = tile.HasTile;
                    if (tile.HasTile) {
                        tileData["c"] = tile.TileType;
                        tileData["d"] = tile.TileFrameX;
                        tileData["e"] = tile.TileFrameY;
                    }
                    tileData["f"] = tile.WallType;
                    tileData["g"] = (byte)tile.Slope;
                    tileData["h"] = tile.LiquidType;
                    tileData["i"] = tile.LiquidAmount;
                    worldTiles.Add(tileData);
                }
            }
            tag["worldTiles"] = worldTiles;

            List<TagCompound> chests = [];
            for (int i = 0; i < Main.chest.Length; i++) {
                Chest chest = Main.chest[i];
                if (chest == null) {
                    continue;
                }

                TagCompound chestData = new TagCompound();

                chestData["a"] = chest.GetPoint16();
                Tile tile = Framing.GetTileSafely(chest.GetPoint16());
                chestData["b"] = tile.TileType;

                List<TagCompound> items = [];
                for (int j = 0; j < chest.item.Length; j++) {
                    items.Add(ItemIO.Save(chest.item[j]));
                }

                chestData["c"] = items;

                chestData["d"] = new Point16(tile.TileFrameX, tile.TileFrameY);

                chests.Add(chestData);
            }
            tag["chests"] = chests;
        }

        public override void LoadData(TagCompound tag) {
            Point16 span = tag.Get<Point16>("span");
            Main.spawnTileX = span.X;
            Main.spawnTileY = span.Y;

            IList<TagCompound> worldTiles = tag.GetList<TagCompound>("worldTiles");
            float count = worldTiles.Count;
            int index = 0;
            foreach (var tileData in worldTiles) {
                Point16 pos = tileData.Get<Point16>("a");
                Tile tile = Main.tile[pos.X, pos.Y];
                tile.HasTile = tileData.Get<bool>("b");
                if (tile.HasTile) {
                    tile.TileType = tileData.Get<ushort>("c");
                    tile.TileFrameX = tileData.Get<short>("d");
                    tile.TileFrameY = tileData.Get<short>("e");
                }
                tile.WallType = tileData.Get<ushort>("f");
                tile.Slope = (SlopeType)tileData.Get<byte>("g");
                tile.LiquidType = tileData.Get<int>("h");
                tile.LiquidAmount = tileData.Get<byte>("i");

                index++;
            }

            index = 0;

            IList<TagCompound> chests = tag.GetList<TagCompound>("chests");
            foreach (var chestData in chests) {
                Point16 point = chestData.Get<Point16>("a");
                Point16 frame = chestData.Get<Point16>("d");
                WorldGen.KillTile(point.X, point.Y + 1);
                WorldGen.PlaceTile(point.X, point.Y + 1, chestData.Get<ushort>("b")
                    , mute: true, style: 0);//style: chestData.Get<Point16>("d").Y / 18

                Framing.GetTileSafely(point.X, point.Y).TileFrameX = frame.X;
                Framing.GetTileSafely(point.X, point.Y).TileFrameY = frame.Y;
                Framing.GetTileSafely(point.X + 1, point.Y).TileFrameX = (short)(frame.X + 18);
                Framing.GetTileSafely(point.X + 1, point.Y).TileFrameY = frame.Y;
                Framing.GetTileSafely(point.X, point.Y + 1).TileFrameX = frame.X;
                Framing.GetTileSafely(point.X, point.Y + 1).TileFrameY = (short)(frame.Y + 18);
                Framing.GetTileSafely(point.X + 1, point.Y + 1).TileFrameX = (short)(frame.X + 18);
                Framing.GetTileSafely(point.X + 1, point.Y + 1).TileFrameY = (short)(frame.Y + 18);

                int chestIndex = Chest.FindChest(point.X, point.Y);
                if (chestIndex > 0) {
                    Chest chest = Main.chest[chestIndex];
                    IList<TagCompound> itemTags = chestData.GetList<TagCompound>("c");
                    int indexBy = 0;
                    foreach (var itemTag in itemTags) {
                        chest.item[indexBy] = ItemIO.Load(itemTag);
                        indexBy++;
                    }
                }

                index++;
            }

            TagCache.Invalidate(SavePath);//直接释放掉缓存，因为数据过大，防止占用太多内存
        }
        //###
    }
}
