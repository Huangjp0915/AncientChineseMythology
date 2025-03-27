using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class GoldenStick : GrowthWeapon
    {
        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.damage = 22;
            Item.DamageType = DamageClass.Melee;
            Item.width = 44;
            Item.height = 44;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5f;
            Item.value = 5000;
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = false;
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<IronStick>(), 1);
            // 使用现有的配方组或自定义一个
            recipe.AddIngredient(ItemID.GoldBar, 81);
            recipe.AddIngredient(ModContent.ItemType<YaoQiFragment>(), 10);
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }

        // 使用内置金短剑贴图作为占位符
        public override string Texture => "Terraria/Images/Item_" + ItemID.GoldShortsword;
    }
}
