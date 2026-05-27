using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors.Items
{
    /// <summary>
    /// 鬼门关钥匙 — 阴天子 33% 三选一
    /// </summary>
    internal class GhostGateKey : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 28;
            Item.maxStack = 1;
            Item.value = Item.sellPrice(platinum: 1);
            Item.rare = ItemRarityID.Purple;
        }
    }
}
