using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class TrueRuyiStick : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1; // 允许在旅程模式研究
        }

        public override void SetDefaults()
        {
            Item.damage = 32; 
            Item.DamageType = DamageClass.Melee;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6.5f;
            Item.value = 30000;
            Item.rare = ItemRarityID.LightRed;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override string Texture => "Terraria/Images/Item_676";

        /*public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<RuyiStick>(), 1);
            recipe.AddIngredient(ItemID.SoulofFright, 50); // 恐惧之魂
            recipe.AddIngredient(ItemID.SoulofMight, 50); // 视域之魂
            recipe.AddIngredient(ItemID.SoulofSight, 50); // 力量之魂
            recipe.AddIngredient(ItemID.SoulofFlight , 30); // 飞翔之魂
            recipe.AddIngredient(ItemID.SoulofLight, 30); // 光明之魂
            recipe.AddIngredient(ItemID.SoulofNight, 30); // 黑暗之魂
            recipe.AddIngredient(ModContent.ItemType<YaoQiFragment>(), 80);
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }*/
    }
}
