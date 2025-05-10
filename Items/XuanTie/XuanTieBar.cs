using Terraria.ID;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.XuanTie
{
    public class XuanTieBar : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/XuanTie/XuanTieBar";
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 999;
            Item.value = Item.buyPrice(silver: 80);
            Item.rare  = ItemRarityID.White;
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<XuanTieOre>(), 3);
            recipe.AddTile(TileID.Furnaces);
            recipe.Register();
        }
    }
}