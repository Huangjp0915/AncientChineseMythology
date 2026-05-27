using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors.Items
{
    /// <summary>
    /// 酆帝冠 — 阴天子 33% 三选一
    /// </summary>
    internal class FengduImperialCrown : ModItem
    {
        public override void SetDefaults() {
            Item.width = 30;
            Item.height = 24;
            Item.value = Item.sellPrice(platinum: 1);
            Item.rare = ItemRarityID.Purple;
            Item.accessory = true;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.statDefense += 12;
            player.GetDamage(DamageClass.Generic) += 0.08f;
        }
    }
}
