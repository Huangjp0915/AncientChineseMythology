using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Placable
{
    public class CloudyGoldSand : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Placable/CloudyGoldSand";

        public override void SetStaticDefaults() { }
        public override void SetDefaults() {
            Item.width = Item.height = 16;
            Item.maxStack = 9999;
            Item.consumable = true;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = Item.useAnimation = 10;
            Item.autoReuse = true;
            Item.createTile = ModContent.TileType<Tiles.Placable.CloudyGoldSand>();
        }
    }
}