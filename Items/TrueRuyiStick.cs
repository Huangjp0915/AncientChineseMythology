using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class TrueRuyiStick : GrowthWeapon
    {
        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.damage = 32; // 根据实际平衡调整
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

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            // 需要1个如意棍（原 RuyiJinguBang）
            recipe.AddIngredient(ModContent.ItemType<RuyiStick>(), 1);
            // 以下各材料需要你自己实现对应物品（或替换为内置物品ID），这里假设你有对应的自定义物品：
            recipe.AddIngredient(ItemID.SoulofFright, 50); // 恐惧之魂
            recipe.AddIngredient(ItemID.SoulofMight, 50); // 视域之魂（请根据实际情况调整ID）
            recipe.AddIngredient(ItemID.SoulofSight, 50); // 力量之魂（请根据实际情况调整ID）
            recipe.AddIngredient(ItemID.SoulofFlight , 30); // 飞翔之魂（请根据实际情况调整ID）
            recipe.AddIngredient(ItemID.SoulofLight, 30); // 光明之魂（请根据实际情况调整ID）
            recipe.AddIngredient(ItemID.SoulofNight, 30); // 黑暗之魂（请根据实际情况调整ID）
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }
    }
}
