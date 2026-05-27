using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Materials
{
    internal class YinImperialSeal : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 34;
            Item.maxStack = 99;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.sellPrice(platinum: 1);
        }
    }
}
