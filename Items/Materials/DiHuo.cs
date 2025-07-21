using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Materials
{
    public class DiHuo : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Materials/DiHuo";

        public override void SetStaticDefaults() {
        }

        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 20;
            // 物品价值10金
            Item.value = Item.buyPrice(gold: 49);
            // 稀有度可根据需求调整
            Item.rare = ItemRarityID.Blue;
            Item.consumable = true;
        }
    }
}
