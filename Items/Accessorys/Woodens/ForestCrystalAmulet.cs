using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Accessorys.Woodens;

/// <summary>
/// 森灵晶核护符 - 前期法师饰品
/// +20 最大法力，降低 3% 魔法消耗
/// </summary>
public class ForestCrystalAmulet : ModItem
{
    public override void SetDefaults() {
        Item.width = 24;
        Item.height = 28;
        Item.accessory = true;
        Item.value = Item.buyPrice(silver: 40);
        Item.rare = ItemRarityID.Blue;
    }

    public override void UpdateAccessory(Player player, bool hideVisual) {
        player.statManaMax2 += 20;
        player.manaCost -= 0.03f;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 8)
            .AddIngredient(ItemID.Emerald, 2)
            .AddIngredient(ItemID.JungleSpores, 3)
            .AddIngredient(ItemID.FallenStar, 2)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
