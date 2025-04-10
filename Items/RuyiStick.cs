using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class RuyiStick : GrowthWeapon
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1; // 允许在旅程模式研究
        }

        public override void SetDefaults()
        {
            Item.damage = 49;
            Item.DamageType = DamageClass.Melee;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6f;
            Item.value = 20000;
            Item.rare = ItemRarityID.LightRed;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<GemStick>(), 1);
            recipe.AddIngredient(ItemID.HellstoneBar, 81);
            recipe.AddIngredient(ModContent.ItemType<YaoQiFragment>(), 40);
            recipe.AddTile(TileID.Hellforge);
            recipe.Register();
        }

        // 使用内置武士刀作为占位贴图
        public override string Texture => "Terraria/Images/Item_" + ItemID.Muramasa;
    }
}
