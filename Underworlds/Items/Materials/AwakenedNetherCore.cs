using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Materials
{
    /// <summary>
    /// 觉醒龙心 — 阴天子诏书主材（觉醒幽冥龙掉落，Phase 2+）
    /// </summary>
    internal class AwakenedNetherCore : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.maxStack = 99;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.sellPrice(platinum: 2);
        }
    }
}
