using AncientChineseMythology.Tiles.Placable;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Placable
{
    public class ShengZhuStatue : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Placable/ShengZhuStatue";

        public override void SetDefaults() {
            Item.CloneDefaults(ItemID.ArmorStatue);      //基础属性
            Item.createTile = ModContent.TileType<ShengZhuStatueTile>();
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 0);
        }
    }
}
