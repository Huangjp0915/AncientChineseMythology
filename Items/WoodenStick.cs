using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class WoodenStick : GrowthWeapon
    {
        public override void SetStaticDefaults()
        {
            // 如果使用 .hjson 进行本地化，则不需要在代码里设置 DisplayName/Tooltip
            // 如不使用，则可以尝试：
            // DisplayName.SetDefault("木棍");
            // Tooltip.SetDefault("唐僧赠送的武器\n据说可以升级成如意金箍棒");
        }

        public override void SetDefaults()
        {
            Item.damage = 10;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 4f;
            Item.value = 1000;
            Item.rare = ItemRarityID.White;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = false;
        }

        public override void AddRecipes()
        {
        }
    }
}
