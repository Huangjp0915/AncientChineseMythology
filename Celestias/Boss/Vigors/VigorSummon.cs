using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vigors
{
    /// <summary>
    /// 断罪令牌 — 召唤神威·断罪刃
    /// </summary>
    public class VigorSummon : ModItem
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
            return !NPC.AnyNPCs(ModContent.NPCType<Vigor>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer)
                return false;

            VaultUtils.TrySpawnBossWithNet(player, ModContent.NPCType<Vigor>());
            return true;
        }
    }
}
