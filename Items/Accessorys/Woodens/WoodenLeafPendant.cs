using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Accessorys.Woodens;

/// <summary>
/// 木制叶纹吐坠 - 前期生活饰品
/// 装备后 +1 生命回复（约 +0.5/s）
/// </summary>
public class WoodenLeafPendant : ModItem
{
    public override void SetDefaults() {
        Item.width = 24;
        Item.height = 28;
        Item.accessory = true;
        Item.value = Item.buyPrice(silver: 20);
        Item.rare = ItemRarityID.White;
    }

    public override void UpdateAccessory(Player player, bool hideVisual) {
        // +1 lifeRegen ≈ +0.5 HP/s
        player.lifeRegen += 1;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 12)
            .AddIngredient(ItemID.DaybloomSeeds, 2)
            .AddIngredient(ItemID.Vine, 1)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
