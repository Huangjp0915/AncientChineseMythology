using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes.Items
{
    internal class HeavenFragment : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 30;
            Item.maxStack = 9999;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.sellPrice(gold: 8);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.HoldUp;
        }
    }
}
