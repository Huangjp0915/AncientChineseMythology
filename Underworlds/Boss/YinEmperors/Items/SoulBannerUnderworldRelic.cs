using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors.Items
{
    /// <summary>
    /// 万魂幡·阴 relic — 阴天子 33% 三选一
    /// </summary>
    internal class SoulBannerUnderworldRelic : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.value = Item.sellPrice(platinum: 1);
            Item.rare = ItemRarityID.Purple;
            Item.accessory = true;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetDamage(DamageClass.Summon) += 0.12f;
            player.maxMinions += 1;
        }
    }
}
