using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class ElixirFurnaceItem : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/ElixirFurnaceItem";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1; // 允许在旅程模式研究
        }

        public override void SetDefaults()
        {
            Item.width = 48;  // 物品宽度匹配贴图
            Item.height = 48; // 物品高度匹配贴图
            Item.maxStack = 99;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.sellPrice(0, 0, 50, 0);
            Item.createTile = ModContent.TileType<Tiles.ElixirFurnaceTile>(); // 关联Tile
            
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<BronzeIngot>(), 18)
                .AddIngredient(ModContent.ItemType<DiHuo>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }
}