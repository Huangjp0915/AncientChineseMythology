using AncientChineseMythology.NPCs.Boss.Yingous;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    internal class YingouSummon : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.rare = ItemRarityID.Red;
            Item.value = 38000;
        }

        public override bool CanUseItem(Player player) {
            return !NPC.AnyNPCs(ModContent.NPCType<Yingou>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return false;
            }
            VaultUtils.TrySpawnBossWithNet(player, ModContent.NPCType<Yingou>());
            return true;
        }
    }
}
