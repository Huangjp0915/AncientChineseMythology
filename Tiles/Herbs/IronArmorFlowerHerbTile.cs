using AncientChineseMythology.Items;
using AncientChineseMythology.Items.Herbs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Metadata;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace AncientChineseMythology.Tiles.Herbs {

    // 三阶段：种子→生长→成熟
    public enum IronArmorStage : byte { Seed, Growing, Mature }

    public class IronArmorFlowerHerbTile : ModTile {

        public override string Texture => "AncientChineseMythology/Textures/Tiles/Herbs/IronArmorFlowerHerbTile";
        private const int FrameW = 18;
        private static readonly Point[] Off4 = { new(1,0), new(-1,0), new(0,1), new(0,-1) };

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileCut[Type]            = true;
            Main.tileNoFail[Type]         = true;
            Main.tileObsidianKill[Type]   = true;
            TileID.Sets.ReplaceTileBreakUp[Type]      = true;
            TileID.Sets.IgnoredByGrowingSaplings[Type] = true;

            TileMaterials.SetForTileId(Type, TileMaterials._materialsByName["Plant"]);

            LocalizedText name = CreateMapEntryName();
            AddMapEntry(new Color(150, 150, 200), name);               // 浅铁灰

            // 只能种在石/泥，但必须靠近矿石
            TileObjectData.newTile.CopyFrom(TileObjectData.StyleAlch);
            TileObjectData.newTile.AnchorValidTiles = new int[] { TileID.Stone, TileID.Mud, TileID.Dirt };
            TileObjectData.addTile(Type);

            DustType = DustID.Iron;
            HitSound = SoundID.Grass;
        }

        public override bool CanPlace(int i, int j)
        {
            int gi = i;       // ground x
            int gj = j + 1;   // ground y：植株下方那格

            foreach (Point p in Off4)
            {
                Tile near = Framing.GetTileSafely(gi + p.X, gj + p.Y);
                if (near.HasTile && TileID.Sets.Ore[near.TileType])
                    return base.CanPlace(i, j);
            }
            return false;
        }

        public override bool CanDrop(int i, int j) =>
            GetStage(i, j) == IronArmorStage.Mature;

        public override IEnumerable<Item> GetItemDrops(int i, int j) {
            Player pl = Main.player[Player.FindClosest(new Vector2(i, j).ToWorldCoordinates(), 16, 16)];
            bool regrow = pl.active &&
                (pl.HeldItem.type == ItemID.StaffofRegrowth || pl.HeldItem.type == ItemID.AcornAxe);

            int herbAmt = regrow ? Main.rand.Next(1, 3) : 1;
            int seedAmt = regrow ? Main.rand.Next(1, 6) : Main.rand.Next(1, 4);

            yield return new Item(ModContent.ItemType<IronArmorFlower>(), herbAmt);
            yield return new Item(ModContent.ItemType<IronArmorFlowerSeeds>(), seedAmt);
        }

        public override void RandomUpdate(int i, int j) {
            Tile t = Framing.GetTileSafely(i, j);
            if (GetStage(t) != IronArmorStage.Mature) {
                t.TileFrameX += FrameW;
                if (Main.netMode != NetmodeID.SinglePlayer)
                    NetMessage.SendTileSquare(-1, i, j, 1);
            }
        }

        public override bool IsTileSpelunkable(int i, int j) =>
            GetStage(i, j) == IronArmorStage.Mature;          // 成熟闪光

        public override void SetSpriteEffects(int i, int j, ref SpriteEffects effects) {
            if (i % 2 == 0) effects = SpriteEffects.FlipHorizontally;
        }

        private static IronArmorStage GetStage(int i, int j) => GetStage(Framing.GetTileSafely(i, j));
        private static IronArmorStage GetStage(Tile t) => (IronArmorStage)(t.TileFrameX / FrameW);
    }
}
