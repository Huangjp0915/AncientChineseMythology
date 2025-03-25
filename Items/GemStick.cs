using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class GemStick : GrowthWeapon
    {
        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.damage = 28;
            Item.DamageType = DamageClass.Melee;
            Item.width = 46;
            Item.height = 46;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5.5f;
            Item.value = 10000;
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = false;
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<GoldenStick>(), 1);
            recipe.AddIngredient(ItemID.Ruby, 20);
            recipe.AddIngredient(ItemID.Sapphire, 20);
            recipe.AddIngredient(ItemID.Emerald, 20);
            recipe.AddIngredient(ItemID.Topaz, 20);
            recipe.AddIngredient(ItemID.Amethyst, 20);
            recipe.AddIngredient(ItemID.Diamond, 20);
            recipe.AddTile(TileID.HeavyWorkBench);
            recipe.Register();
        }

        // 使用内置红宝石锤作为占位贴图
        public override string Texture => "Terraria/Images/Item_4258";

    }
}
