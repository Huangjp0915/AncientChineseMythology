using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Underworlds.Enemys;
using AncientChineseMythology.Underworlds.Items;
using AncientChineseMythology.Underworlds.Tiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙召唤物品
    /// </summary>
    public class NetherDragonSummonItem : ModItem
    {
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 1, 0, 0);
        }

        public override bool CanUseItem(Player player) {
            return NPC.downedMoonlord
                && UnderworldEnemySpawnSystem.IsInUnderworldRegion(player)
                && !NPC.AnyNPCs(ModContent.NPCType<NetherDragonHead>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.Roar, player.position);
                VaultUtils.TrySpawnBossWithNet(player, ModContent.NPCType<NetherDragonHead>());
                Main.NewText("幽冥龙已苏醒！", new Color(100, 150, 255));
            }

            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<SoulFragment>(20)
                .AddIngredient<UmbralStoneItem>(15)
                .AddIngredient<Bone>(8)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
