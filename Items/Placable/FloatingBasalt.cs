using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Placable
{
    public class FloatingBasalt : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Placable/FloatingBasalt";

        public override void SetStaticDefaults() { }
        public override void SetDefaults() {
            Item.width = Item.height = 16;
            Item.maxStack = 9999;
            Item.useTime = Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<Tiles.Placable.FloatingBasalt>();
        }
    }
}