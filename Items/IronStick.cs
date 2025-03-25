using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class IronStick : GrowthWeapon
    {
        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.damage = 17;
            Item.DamageType = DamageClass.Melee;
            Item.width = 42;
            Item.height = 42;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 4.5f;
            Item.value = 2500;
            Item.rare = ItemRarityID.White;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = false;
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<WoodenStick>(), 1);
            recipe.AddRecipeGroup(RecipeGroupID.IronBar, 100);
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }

        // 使用内置铁短剑贴图作为占位符
        public override string Texture => "Terraria/Images/Item_" + ItemID.IronShortsword;
    }
}
