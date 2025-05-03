using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class BronzeIngot : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Bronze/BronzeIngot";
        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 999;
            Item.value = 1000;
            Item.rare = ItemRarityID.White;
        }

        public override void AddRecipes()
        {
            // 1. 使用 2 个铜矿 + 1 个铁矿
            Recipe recipe1 = CreateRecipe();
            recipe1.AddIngredient(ItemID.CopperOre, 2);
            recipe1.AddIngredient(ItemID.IronOre, 1);
            recipe1.AddTile(TileID.Furnaces);
            recipe1.Register();

            // 2. 使用 2 个铜矿 + 1 个铅矿
            Recipe recipe2 = CreateRecipe();
            recipe2.AddIngredient(ItemID.CopperOre, 2);
            recipe2.AddIngredient(ItemID.LeadOre, 1);
            recipe2.AddTile(TileID.Furnaces);
            recipe2.Register();

            // 3. 使用 2 个锡矿 + 1 个铁矿
            Recipe recipe3 = CreateRecipe();
            recipe3.AddIngredient(ItemID.TinOre, 2);
            recipe3.AddIngredient(ItemID.IronOre, 1);
            recipe3.AddTile(TileID.Furnaces);
            recipe3.Register();

            // 4. 使用 2 个锡矿 + 1 个铅矿
            Recipe recipe4 = CreateRecipe();
            recipe4.AddIngredient(ItemID.TinOre, 2);
            recipe4.AddIngredient(ItemID.LeadOre, 1);
            recipe4.AddTile(TileID.Furnaces);
            recipe4.Register();

            // 5. 混合使用 1 个铜矿 + 1 个锡矿 + 1 个铁矿
            Recipe recipe5 = CreateRecipe();
            recipe5.AddIngredient(ItemID.CopperOre, 1);
            recipe5.AddIngredient(ItemID.TinOre, 1);
            recipe5.AddIngredient(ItemID.IronOre, 1);
            recipe5.AddTile(TileID.Furnaces);
            recipe5.Register();

            // 6. 混合使用 1 个铜矿 + 1 个锡矿 + 1 个铅矿
            Recipe recipe6 = CreateRecipe();
            recipe6.AddIngredient(ItemID.CopperOre, 1);
            recipe6.AddIngredient(ItemID.TinOre, 1);
            recipe6.AddIngredient(ItemID.LeadOre, 1);
            recipe6.AddTile(TileID.Furnaces);
            recipe6.Register();
        }
    }
}
