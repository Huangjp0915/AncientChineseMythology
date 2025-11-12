using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Tiles
{
    internal class UmbralStoneItem : ModItem
    {
        public override void SetDefaults() {
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.consumable = true;
            Item.maxStack = Item.CommonMaxStack;
            Item.width = Item.height = 16;
            Item.useTime = 10;
            Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.createTile = ModContent.TileType<UmbralStone>();
        }
    }

    internal class UmbralStone : ModTile
    {
        public override void SetStaticDefaults() {
            Main.tileSolid[Type] = true;
            Main.tileMergeDirt[Type] = true;
            Main.tileBlendAll[Type] = true;
            Main.tileBrick[Type] = true;
            Main.tileMerge[Type][TileID.HardenedSand] = true;
            Main.tileMerge[TileID.HardenedSand][Type] = true;
            Main.tileMerge[Type][TileID.CorruptHardenedSand] = true;
            Main.tileMerge[TileID.CorruptHardenedSand][Type] = true;
            Main.tileMerge[Type][TileID.CrimsonHardenedSand] = true;
            Main.tileMerge[TileID.CrimsonHardenedSand][Type] = true;
            Main.tileMerge[Type][TileID.HallowHardenedSand] = true;
            Main.tileMerge[TileID.HallowHardenedSand][Type] = true;
            TileID.Sets.Conversion.HardenedSand[Type] = true;
            TileID.Sets.ForAdvancedCollision.ForSandshark[Type] = true;
            TileID.Sets.isDesertBiomeSand[Type] = true;
            TileID.Sets.CanBeClearedDuringGeneration[Type] = true;
            TileID.Sets.ChecksForMerge[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileLighted[Type] = true;
            AddMapEntry(new Color(31, 38, 46));
            MineResist = 1.5f;
            DustType = DustID.BlueMoss;
        }
    }
}
