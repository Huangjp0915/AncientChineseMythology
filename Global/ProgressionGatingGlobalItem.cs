using AncientChineseMythology.Celestias.Boss.Arguses;
using AncientChineseMythology.Celestias.Boss.Vigors;
using AncientChineseMythology.Celestias.PillarofTheHeavenes.Items;
using AncientChineseMythology.Items;
using AncientChineseMythology.Items.Summons;
using AncientChineseMythology.Systems;
using AncientChineseMythology.Underworlds.Items;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Global
{
    /// <summary>关键召唤物进度门控集中入口（spec §3.1）。</summary>
    public class ProgressionGatingGlobalItem : GlobalItem
    {
        public override bool CanUseItem(Item item, Player player) {
            int type = item.type;

            if (type == ModContent.ItemType<VigorSummon>()) {
                if (!DownedBossSystem.downedHeavenInvasion) {
                    Fail(player, "需先击退天庭入侵，方可召唤神威·断罪刃。");
                    return false;
                }
            }
            else if (type == ModContent.ItemType<ArgusSummon>()) {
                if (!DownedBossSystem.downedVigor) {
                    Fail(player, "需先击败神威·断罪刃，方可召唤天目·追魂弧。");
                    return false;
                }
            }
            else if (type == ModContent.ItemType<KyuubiSummonsHairpin>()) {
                if (!NPC.downedPlantBoss) {
                    Fail(player, "需击败世纪之花后，方可召唤九尾妖狐。");
                    return false;
                }
            }
            else if (type == ModContent.ItemType<UnderworldPairSummons>()) {
                if (!Main.hardMode) {
                    Fail(player, "需击败血肉墙后，方可使用冥途双引符。");
                    return false;
                }
            }
            else if (type == ModContent.ItemType<HeavenInvasionSummon>()) {
                if (!NPC.downedMoonlord) {
                    Fail(player, "需击败月亮领主后，方可发起天庭入侵。");
                    return false;
                }
            }
            else if (type == ModContent.ItemType<UnderworldInvasionSummon>()) {
                if (!NPC.downedMoonlord) {
                    Fail(player, "需击败月亮领主后，方可发起地府入侵。");
                    return false;
                }
            }
            else if (type == ModContent.ItemType<YingouSummon>()) {
                if (!NPC.downedMoonlord) {
                    Fail(player, "需击败月亮领主后，方可使用鬼面具召唤赢勾。");
                    return false;
                }
            }

            return base.CanUseItem(item, player);
        }

        private static void Fail(Player player, string message) {
            if (player.whoAmI == Main.myPlayer) {
                Main.NewText(message, byte.MaxValue, 180, 80);
            }
        }
    }
}
