using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class BloodLingzhi : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Herbs/BloodLingzhi";
        public override void SetStaticDefaults() => Item.ResearchUnlockCount = 25;

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 999;
            Item.value = Item.buyPrice(gold: 2);
            Item.rare  = ItemRarityID.Orange;
        }
    }
}
