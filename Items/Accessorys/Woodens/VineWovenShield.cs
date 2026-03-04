using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Accessorys.Woodens;

/// <summary>
/// 盘藤护盾 - 前期防御饰品
/// +1 防御，受近战接触伤害时反弹1点伤害（荆棘效果）
/// </summary>
public class VineWovenShield : ModItem
{
    public override void SetDefaults() {
        Item.width = 28;
        Item.height = 30;
        Item.accessory = true;
        Item.defense = 1;
        Item.value = Item.buyPrice(silver: 30);
        Item.rare = ItemRarityID.White;
    }

    public override void UpdateAccessory(Player player, bool hideVisual) {
        // 微弱荆棘效果：近战接触伤害反弹1点
        player.thorns = MathHelper.Max(player.thorns, 0.1f);
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 15)
            .AddIngredient(ItemID.Vine, 3)
            .AddIngredient(ItemID.Stinger, 1)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
