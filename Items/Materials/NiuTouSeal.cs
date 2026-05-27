using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Materials
{
    public class NiuTouSeal : ModItem
    {
        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = 9999;
            Item.value = Item.buyPrice(gold: 1);
            Item.rare = ItemRarityID.Pink;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.AncientGoldHelmet;
    }
}
