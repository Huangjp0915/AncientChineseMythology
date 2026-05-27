using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items;
using AncientChineseMythology.Underworlds.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors.Items
{
    /// <summary>
    /// 酆帝诏书 — 召唤阴天子
    /// </summary>
    public class YinEmperorEdict : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 45;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(platinum: 1);
            Item.maxStack = 1;
            Item.consumable = false;
        }

        public override bool CanUseItem(Player player) {
            return NPC.downedMoonlord && !NPC.AnyNPCs(ModContent.NPCType<YinEmperor>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer)
                return false;

            VaultUtils.TrySpawnBossWithNet(player, ModContent.NPCType<YinEmperor>());
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<VoidDragonSinew>(8)
                .AddIngredient<Corpsefragments>(12)
                .AddIngredient<AwakenedNetherCore>()
                .AddIngredient<SoulFragment>(50)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
