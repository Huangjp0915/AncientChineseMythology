using System.Linq;
using AncientChineseMythology.Players;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Global
{
    public class BaGuaTimeAndManaGlobalItem : GlobalItem
    {
        public override void ModifyManaCost(Item item, Player player, ref float reduce, ref float mult) {
            // 检查是否有时空扭曲阵
            if (player.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) {
                var baGuaPlayer = player.GetModPlayer<BaGuaPlayer>();
                var cur = baGuaPlayer.BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
                if (cur != null && baGuaPlayer.CheckShiKongNiuQuFormation(cur)) {
                    // 魔力消耗减少10%
                    mult *= 0.90f;
                }
            }
        }
    }
}
