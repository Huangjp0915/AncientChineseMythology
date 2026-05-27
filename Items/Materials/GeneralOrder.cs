using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Materials
{
    /// <summary>将军令 — 天将线共享材料，用于毗沙门召唤链（§5.3）。</summary>
    public class GeneralOrder : ModItem
    {
        public override void SetDefaults() {
            Item.width = 24;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.value = Item.buyPrice(gold: 80);
            Item.rare = ItemRarityID.Purple;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.AdamantiteHeadgear;
    }
}
