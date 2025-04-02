using AncientChineseMythology.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class TeleportationItem : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/TeleportationItem";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 64;
            Item.height = 64;
            Item.maxStack = 99;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<TeleportationTile>();
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.Orange;
        }
    }
}
