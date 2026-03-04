using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Accessorys.Woodens;

/// <summary>
/// 林地木靴 - 前期移动饰品
/// +5% 移动速度，在草地/泥土上额外降低 10% 坠落伤害
/// </summary>
public class WoodlandBarkBoots : ModItem
{
    public override void SetDefaults() {
        Item.width = 28;
        Item.height = 26;
        Item.accessory = true;
        Item.value = Item.buyPrice(silver: 25);
        Item.rare = ItemRarityID.White;
    }

    public override void UpdateAccessory(Player player, bool hideVisual) {
        player.moveSpeed += 0.05f;

        // 检测脚下方块: 草地或泥土块时降低坠落伤害
        int tileX = (int)(player.Bottom.X / 16f);
        int tileY = (int)(player.Bottom.Y / 16f);
        if (tileX >= 0 && tileX < Main.maxTilesX && tileY >= 0 && tileY < Main.maxTilesY) {
            Tile tile = Framing.GetTileSafely(tileX, tileY);
            if (tile.HasTile) {
                int type = tile.TileType;
                if (type == TileID.Grass || type == TileID.Dirt ||
                    type == TileID.JungleGrass || type == TileID.MushroomGrass ||
                    type == TileID.Mud) {
                    // 降低坠落伤害 10%
                    player.extraFall += 5;
                }
            }
        }
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 12)
            .AddIngredient(ItemID.GlowingMushroom, 3)
            .AddIngredient(ItemID.Gel, 5)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
