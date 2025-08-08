using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Tiles.Placable
{
    public class XuanTieOreTile : ModTile
    {
        public override string Texture => "AncientChineseMythology/Textures/Tiles/Placable/XuanTieOreTile";

        public override void SetStaticDefaults() {
            Main.tileSolid[Type] = true;
            Main.tileMergeDirt[Type] = true;
            Main.tileSpelunker[Type] = true;
            Main.tileOreFinderPriority[Type] = 320;          // 接近 Iron
            Main.tileShine[Type] = 900;
            Main.tileShine2[Type] = true;
            Main.tileBlockLight[Type] = true;
            TileID.Sets.Ore[Type] = true;
            TileID.Sets.FriendlyFairyCanLureTo[Type] = true;

            Main.tileFrameImportant[Type] = false;

            HitSound = SoundID.Tink;
            DustType = DustID.Iron;

            LocalizedText name = CreateMapEntryName();
            AddMapEntry(new Color(110, 110, 120), name);     // 深灰色

            MineResist = 3.3f;   // 挖掘速度
            MinPick = 35;      // 铜镐即可
        }

        public override bool IsTileBiomeSightable(int i, int j, ref Color sightColor) {
            sightColor = Color.Gray;
            return true;
        }

        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem) {
            if (!fail && !effectOnly && !noItem) {
                Item.NewItem(new EntitySource_TileBreak(i, j), i * 16, j * 16, 16, 16,
                             ModContent.ItemType<Items.XuanTie.XuanTieOre>());
            }
        }
    }
}