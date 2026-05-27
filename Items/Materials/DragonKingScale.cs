using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Materials
{
    /// <summary>龙王鳞 — 四海龙王共享掉落材料（§5.2）。</summary>
    public class DragonKingScale : ModItem
    {
        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = 9999;
            Item.value = Item.buyPrice(gold: 50);
            Item.rare = ItemRarityID.Purple;
        }

        public override string Texture => "AncientChineseMythology/Textures/Items/Materials/DragonKingScale";
    }
}
