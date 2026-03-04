using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Accessorys.Woodens;

/// <summary>
/// 树皮加固手套 - 前期近战/工具饰品
/// +5% 近战攻击速度（同时影响工具挥动速度）
/// </summary>
public class BarkReinforcedGlove : ModItem
{
    public override void SetDefaults() {
        Item.width = 26;
        Item.height = 28;
        Item.accessory = true;
        Item.value = Item.buyPrice(silver: 25);
        Item.rare = ItemRarityID.White;
    }

    public override void UpdateAccessory(Player player, bool hideVisual) {
        // +5% 近战攻速（镐子/斧头等工具同为 Melee 类，也会生效）
        player.GetAttackSpeed(DamageClass.Melee) += 0.05f;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 15)
            .AddIngredient(ItemID.Vine, 2)
            .AddIngredient(ItemID.Cobweb, 5)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
