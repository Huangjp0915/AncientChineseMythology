using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class RuyiJinguBang : GrowthWeapon
    {
        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("如意金箍棒");
            // Tooltip.SetDefault("唐僧口中的神兵，威力大增！");
        }

        public override void SetDefaults()
        {
            Item.damage = 120;
            Item.DamageType = DamageClass.Melee;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 10;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 12f;
            Item.value = 100000;
            Item.rare = ItemRarityID.Orange;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            // 需要1个真·如意棍
            recipe.AddIngredient(ModContent.ItemType<TrueRuyiStick>(), 1);
            // 叶绿锭：假设你有自定义叶绿锭，否则可用内置替换
            recipe.AddIngredient(ItemID.ChlorophyteBar, 100);
            // 龟甲：可以用内置 TurtleShell（如果存在），否则自定义
            recipe.AddIngredient(ItemID.TurtleShell, 10);
            // 星璇碎片、日耀碎片、星云碎片、星尘碎片：使用内置月球碎片替换
            recipe.AddIngredient(ItemID.FragmentNebula, 20);
            recipe.AddIngredient(ItemID.FragmentSolar, 20);
            recipe.AddIngredient(ItemID.FragmentStardust, 20);
            recipe.AddIngredient(ItemID.FragmentVortex, 20);
            // 制作需要在远古操纵机
            recipe.AddTile(TileID.MythrilAnvil);
            recipe.Register();
        }

        // 临时使用内置银剑纹理
        public override string Texture => "Terraria/Images/Item_" + ItemID.SilverBroadsword;
    }
}
