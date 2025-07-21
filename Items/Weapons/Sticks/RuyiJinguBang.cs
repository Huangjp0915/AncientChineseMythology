using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Sticks
{
    public class RuyiJinguBang : ModItem
    {
        public override void SetStaticDefaults() {
            // DisplayName.SetDefault("如意金箍棒");
            // Tooltip.SetDefault("唐僧口中的神兵，威力大增！");
            Item.ResearchUnlockCount = 1; // 允许在旅程模式研究
        }

        public override void SetDefaults() {
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
            Item.value = Item.buyPrice(10, 0, 0, 0);
        }

        /*public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<TrueRuyiStick>(), 1);
            recipe.AddIngredient(ItemID.ChlorophyteBar, 100);
            recipe.AddIngredient(ItemID.TurtleShell, 10);
            recipe.AddIngredient(ItemID.FragmentNebula, 20);
            recipe.AddIngredient(ItemID.FragmentSolar, 20);
            recipe.AddIngredient(ItemID.FragmentStardust, 20);
            recipe.AddIngredient(ItemID.FragmentVortex, 20);
            recipe.AddIngredient(ModContent.ItemType<YaoQiFragment>(), 160);
            recipe.AddTile(TileID.MythrilAnvil);
            recipe.Register();
        }*/

        public override string Texture => "Terraria/Images/Item_" + ItemID.SilverBroadsword;
    }
}
