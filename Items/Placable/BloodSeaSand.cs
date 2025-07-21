using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Placable
{
    public class BloodSeaSand : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Placable/BloodSeaSand";
        public override void SetStaticDefaults() { }
        public override void SetDefaults() {
            Item.width = 12;
            Item.height = 12;
            Item.maxStack = 999;
            Item.value = 0;
            Item.rare = ItemRarityID.White;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<Tiles.Placable.BloodSeaSand>();
        }
    }
}
