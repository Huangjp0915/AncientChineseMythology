using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.NPCs.Boss.KyuubiKitsunes;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Summons
{
    /// <summary>九尾狐毫 — Plantera 后于秘银砧召唤九尾妖狐（§4.1.2）。</summary>
    public class KyuubiSummonsHairpin : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.maxStack = 20;
            Item.useTime = Item.useAnimation = 45;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = true;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.buyPrice(gold: 5);
        }

        public override bool CanUseItem(Player player) {
            return NPC.downedPlantBoss
                && !NPC.AnyNPCs(ModContent.NPCType<KyuubiKitsune>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return false;
            }
            VaultUtils.TrySpawnBossWithNet(player, ModContent.NPCType<KyuubiKitsune>());
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<StrangeStone>(1)
                .AddIngredient<YaoQiFragment>(15)
                .AddIngredient(ItemID.JungleSpores, 10)
                .AddIngredient(ItemID.SoulofFright, 5)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
