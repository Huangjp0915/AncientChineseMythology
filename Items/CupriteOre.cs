using AncientChineseMythology.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class CupriteOre : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/CupriteOre";
        
        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 999;
            Item.value = Item.buyPrice(silver: 10);
            Item.rare = ItemRarityID.White;
            
            // 设置为可放置的 Tile 物品
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 10;
            Item.useAnimation = 15;
            Item.consumable = true; // 使用后消耗物品
            Item.createTile = ModContent.TileType<PlacedCupriteOreTile>();
        }
    }
}
