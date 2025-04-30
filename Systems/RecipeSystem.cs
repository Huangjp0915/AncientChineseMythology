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
                .AddIngredient(ModContent.ItemType<Items.YaoQiFragment>(), 5)
                .AddTile<Tiles.ElixirFurnaceTile>()
                .Register();
            
            Recipe.Create(ModContent.ItemType<Items.XuePoDan>())
                .AddIngredient(ItemID.Daybloom, 9)
                .AddIngredient(ModContent.ItemType<Items.BloodLingzhi>(), 9)
                .AddIngredient(ModContent.ItemType<Items.YaoQiFragment>(), 5)
                .AddTile<Tiles.ElixirFurnaceTile>()
                .Register();

            Recipe.Create(ModContent.ItemType<Items.NingShenDan>())
                .AddIngredient(ItemID.Moonglow, 9)
                .AddIngredient(ModContent.ItemType<Items.Starflower>(), 9)
                .AddIngredient(ModContent.ItemType<Items.YaoQiFragment>(), 5)
                .AddTile<Tiles.ElixirFurnaceTile>()
                .Register();

            Recipe.Create(ModContent.ItemType<Items.XuanGangDan>())
                .AddIngredient(ItemID.Shiverthorn, 9)
                .AddIngredient(ModContent.ItemType<Items.IronArmorFlower>(), 9)
                .AddIngredient(ModContent.ItemType<Items.YaoQiFragment>(), 5)
                .AddTile<Tiles.ElixirFurnaceTile>()
                .Register();

            Recipe.Create(ModContent.ItemType<Items.PoJunDan>())
                .AddIngredient(ItemID.Deathweed, 9)
                .AddIngredient(ModContent.ItemType<Items.BlazingFlower>(), 9)
                .AddIngredient(ModContent.ItemType<Items.YaoQiFragment>(), 5)
                .AddTile<Tiles.ElixirFurnaceTile>()
                .Register();
        }
    }
}