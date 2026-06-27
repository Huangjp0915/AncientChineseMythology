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

            // G7 处决资格（酆都套机制的最小桩实现）：
            // 阴帝印幻象阶段后、阴天子血量 ≤18% 时，佩戴者攻击可触发处决。
            // 正式的“酆都”护甲套同样应在其套装加成中置位该标记。
            player.GetModPlayer<YinJudgmentPlayer>().fengduSetActive = true;
        }
    }
}
