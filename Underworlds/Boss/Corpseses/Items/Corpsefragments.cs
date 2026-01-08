using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses.Items
{
    internal class Corpsefragments : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 30;
            Item.maxStack = 9999;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.sellPrice(gold: 12);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.HoldUp;
        }
    }
}
