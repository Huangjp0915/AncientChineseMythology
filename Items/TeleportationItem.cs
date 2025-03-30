using AncientChineseMythology.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class TeleportationItem : ModItem
    {
        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            // 物品贴图现为 64×64 像素，在物品栏中显示为 64×64 大小
            Item.width = 64;
            Item.height = 64;
            Item.maxStack = 99;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.consumable = true;
            // 放置后生成对应的 TeleportationTile
            Item.createTile = ModContent.TileType<TeleportationTile>();
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.Orange;
            // 如有需要，可调整手持时的缩放
            // Item.scale = 0.8f;
        }
    }
}
