using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Materials
{
    public class CoffinNailFragment : ModItem
    {
        public override void SetDefaults() {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = 9999;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Red;
        }

        public override string Texture => "AncientChineseMythology/Items/Weapons/CoffinNail";
    }
}
