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
            Recipe.Create(ModContent.ItemType<Items.ScrapElixir>())
                .AddIngredient(ItemID.FallenStar, 1)
                .AddIngredient(ModContent.ItemType<Items.YaoQiFragment>(), 1)
                .AddTile<Tiles.ElixirFurnaceTile>()
                .Register();
            
            Recipe.Create(ModContent.ItemType<Items.XuePoDan>())
                .AddIngredient(ItemID.FallenStar, 1)
                .AddIngredient(ModContent.ItemType<Items.YaoQiFragment>(), 1)
                .AddTile<Tiles.ElixirFurnaceTile>()
                .Register();

            Recipe.Create(ModContent.ItemType<Items.NingShenDan>())
                .AddIngredient(ItemID.FallenStar, 1)
                .AddIngredient(ModContent.ItemType<Items.YaoQiFragment>(), 1)
                .AddTile<Tiles.ElixirFurnaceTile>()
                .Register();

            Recipe.Create(ModContent.ItemType<Items.XuanGangDan>())
                .AddIngredient(ItemID.FallenStar, 1)
                .AddIngredient(ModContent.ItemType<Items.YaoQiFragment>(), 1)
                .AddTile<Tiles.ElixirFurnaceTile>()
                .Register();

            Recipe.Create(ModContent.ItemType<Items.PoJunDan>())
                .AddIngredient(ItemID.FallenStar, 1)
                .AddIngredient(ModContent.ItemType<Items.YaoQiFragment>(), 1)
                .AddTile<Tiles.ElixirFurnaceTile>()
                .Register();
        }
    }
}