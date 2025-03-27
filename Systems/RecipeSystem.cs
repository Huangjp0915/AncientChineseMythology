// Systems/RecipeSystem.cs
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Systems
{
    public class RecipeSystem : ModSystem
    {
        public override void AddRecipes()
        {
            // 废丹配方
            Recipe.Create(ModContent.ItemType<Items.ScrapElixir>())
                .AddIngredient(ItemID.FallenStar, 1)
                .AddIngredient(ModContent.ItemType<Items.YaoQiFragment>(), 1)
                .AddTile<Tiles.ElixirFurnaceTile>()
                .Register();
        }
    }
}