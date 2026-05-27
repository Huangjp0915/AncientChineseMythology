using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Materials
{
    /// <summary>观察者之眼 — 天庭观察者 100% 掉落（§5.6）。</summary>
    public class OverseersEye : ModItem
    {
        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = 9999;
            Item.value = Item.buyPrice(platinum: 1);
            Item.rare = ItemRarityID.Red;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.EyeoftheGolem;
    }
}
