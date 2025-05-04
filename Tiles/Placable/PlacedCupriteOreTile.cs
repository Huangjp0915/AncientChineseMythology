using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;
using Terraria.DataStructures;
using AncientChineseMythology.Items.Cuprite;

namespace AncientChineseMythology.Tiles.Placable
{
    public class PlacedCupriteOreTile : ModTile
    {
        public override string Texture => "AncientChineseMythology/Textures/Tiles/Placable/CupriteOreTile";

        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type] = true;
            Main.tileMergeDirt[Type] = true;
            Main.tileSpelunker[Type] = true;
            Main.tileOreFinderPriority[Type] = 410;
            Main.tileShine[Type] = 975;
            Main.tileShine2[Type] = true;
            Main.tileBlockLight[Type] = true;

            HitSound = SoundID.Tink;
            DustType = DustID.Iron;

            LocalizedText name = CreateMapEntryName();
            AddMapEntry(new Color(200, 100, 50), name);

            MineResist = 4f;
            MinPick = 65;
        }

        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (!fail && !effectOnly && !noItem)
            {
                // 掉落原来的矿石物品
                Item.NewItem(new EntitySource_TileBreak(i, j), i * 16, j * 16, 16, 16, ModContent.ItemType<CupriteOre>());
            }
        }
    }
}
