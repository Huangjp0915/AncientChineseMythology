using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes.Items
{
    /// <summary>
    /// 天庭入侵召唤物——天庭令牌
    /// 使用后触发天庭入侵事件
    /// 需要在击败月球领主之后才能使用
    /// </summary>
    public class HeavenInvasionSummon : ModItem
    {
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(gold: 10);
            Item.consumable = true;
            Item.maxStack = 20;
        }

        public override bool CanUseItem(Player player) {
            // 需要击败月球领主 且 入侵未激活
            return NPC.downedMoonlord && !HeavenInvasionSystem.InvasionActive;
        }

        public override bool? UseItem(Player player) {
            // 在单人或服务器端触发入侵
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                HeavenInvasionSystem.StartInvasion(player.Center);
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.FragmentSolar, 5)
                .AddIngredient(ItemID.FragmentVortex, 5)
                .AddIngredient(ItemID.FragmentNebula, 5)
                .AddIngredient(ItemID.FragmentStardust, 5)
                .AddIngredient(ItemID.LunarBar, 5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
