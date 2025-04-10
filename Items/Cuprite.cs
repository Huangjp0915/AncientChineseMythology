using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class Cuprite : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Cuprite";

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
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ItemID.CopperOre, 3); // 需要3个铜矿
            recipe.AddIngredient(ItemID.IronOre, 2);   // 需要2个铁矿
            recipe.AddTile(TileID.Furnaces);           // 必须在熔炉旁合成
            recipe.Register();
        }
    }
}
