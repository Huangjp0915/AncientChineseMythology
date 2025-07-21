// AncientChineseMythology/Items/LingShi/LingShiOre.cs
using AncientChineseMythology.Tiles.Placable;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Materials
{
    public class LingShiOre : ModItem
    {
        public override string Texture =>
            "AncientChineseMythology/Textures/Items/Materials/LingShiOre";

        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 999;
            Item.value = Terraria.Item.buyPrice(silver: 60); // 注意用类型名 :contentReference[oaicite:4]{index=4}:contentReference[oaicite:5]{index=5}
            Item.rare = ItemRarityID.Green;

            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 10;
            Item.useAnimation = 15;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<LingShiOreTile>();
        }
    }
}
