using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Tiles
{
    internal class NetherSand : ModTile
    {
        public override void SetStaticDefaults() {
            Main.tileBlendAll[Type] = true;
            Main.tileMerge[Type][TileID.Grass] = true;
            Main.tileBrick[Type] = true;

            TileID.Sets.Grass[Type] = true;
            TileID.Sets.ChecksForMerge[Type] = true;
            TileID.Sets.NeedsGrassFraming[Type] = true;
            TileID.Sets.NeedsGrassFramingDirt[Type] = TileID.Dirt;
            TileID.Sets.CanBeDugByShovel[Type] = true;
            TileID.Sets.ResetsHalfBrickPlacementAttempt[Type] = true;
            TileID.Sets.DoesntPlaceWithTileReplacement[Type] = true;
            TileID.Sets.ForcedDirtMerging[Type] = true;
            TileID.Sets.SpreadOverground[Type] = true;
            TileID.Sets.SpreadUnderground[Type] = true;
            TileID.Sets.CanBeClearedDuringOreRunner[Type] = true;

            TileID.Sets.Conversion.Grass[Type] = true;
            TileID.Sets.Conversion.MergesWithDirtInASpecialWay[Type] = true;
            Main.tileMerge[Type][ModContent.TileType<UmbralStone>()] = true;

            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;

            AddMapEntry(new Color(255, 153, 51));

            MinPick = 10;
            MineResist = 0.1f;
            DustType = DustID.Flare;
            HitSound = SoundID.Dig;
            RegisterItemDrop(ItemID.DirtBlock);
        }
    }
}
