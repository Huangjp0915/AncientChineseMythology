using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.XuanTie
{
    public class XuanTieOre : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/XuanTie/XuanTieOre";
        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 999;
            Item.value = Item.buyPrice(silver: 50);
            Item.rare = ItemRarityID.White;

            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 10;
            Item.useAnimation = 15;
            Item.consumable = true;
            // 直接复用天然 Tile
            Item.createTile = ModContent.TileType<Tiles.Placable.XuanTieOreTile>();
        }
    }
}