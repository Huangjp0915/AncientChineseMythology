using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items
{
    /// <summary>
    /// 地府入侵召唤物——冥府令牌
    /// 使用后触发地府入侵事件
    /// 需要在击败月球领主之后才能使用
    /// </summary>
    public class UnderworldInvasionSummon : ModItem
    {
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(gold: 10);
            Item.consumable = true;
            Item.maxStack = 20;
        }

        public override bool CanUseItem(Player player) {
            // 需要击败月球领主 且 入侵未激活
            return NPC.downedMoonlord && !UnderworldInvasionSystem.InvasionActive;
        }

        public override bool? UseItem(Player player) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                UnderworldInvasionSystem.StartInvasion(player.Center);
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<SoulFragment>(20)
                .AddIngredient(ItemID.Ectoplasm, 10)
                .AddIngredient(ItemID.LunarBar, 5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
