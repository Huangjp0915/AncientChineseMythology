//Systems/RecipeSystem.cs
using AncientChineseMythology.Items.Herbs;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.Potions;
using AncientChineseMythology.Tiles.Placable;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Systems
{
    public class RecipeSystem : ModSystem
    {
        public override void AddRecipes() {
            Recipe.Create(ModContent.ItemType<ScrapElixir>())
                .AddIngredient(ItemID.FallenStar, 5)
                .AddIngredient(ModContent.ItemType<YaoQiFragment>(), 5)
                .AddTile<ElixirFurnaceTile>()
                .Register();

            Recipe.Create(ModContent.ItemType<XuePoDan>())
                .AddIngredient(ItemID.Daybloom, 9)
                .AddIngredient(ModContent.ItemType<BloodLingzhi>(), 9)
                .AddIngredient(ModContent.ItemType<YaoQiFragment>(), 5)
                .AddTile<ElixirFurnaceTile>()
                .Register();

            Recipe.Create(ModContent.ItemType<NingShenDan>())
                .AddIngredient(ItemID.Moonglow, 9)
                .AddIngredient(ModContent.ItemType<Starflower>(), 9)
                .AddIngredient(ModContent.ItemType<YaoQiFragment>(), 5)
                .AddTile<ElixirFurnaceTile>()
                .Register();

            Recipe.Create(ModContent.ItemType<XuanGangDan>())
                .AddIngredient(ItemID.Shiverthorn, 9)
                .AddIngredient(ModContent.ItemType<IronArmorFlower>(), 9)
                .AddIngredient(ModContent.ItemType<YaoQiFragment>(), 5)
                .AddTile<ElixirFurnaceTile>()
                .Register();

            Recipe.Create(ModContent.ItemType<PoJunDan>())
                .AddIngredient(ItemID.Deathweed, 9)
                .AddIngredient(ModContent.ItemType<BlazingFlower>(), 9)
                .AddIngredient(ModContent.ItemType<YaoQiFragment>(), 5)
                .AddTile<ElixirFurnaceTile>()
                .Register();
        }
    }
}