using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Arguses
{
    /// <summary>
    /// 追魂弓弦 — 召唤天目·追魂弧
    /// </summary>
    public class ArgusSummon : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(gold: 50);
            Item.maxStack = 1;
        }

        public override bool CanUseItem(Player player) {
            return !NPC.AnyNPCs(ModContent.NPCType<Argus>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer)
                return false;

            VaultUtils.TrySpawnBossWithNet(player, ModContent.NPCType<Argus>());
            return true;
        }
    }
}
