using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Placable
{
    public class CelestialJadeBrick : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Placable/CelestialJadeBrick";
        public override void SetStaticDefaults() {}
        public override void SetDefaults()
        {
            Item.width = Item.height = 16;
            Item.maxStack = 9999;
            Item.consumable = true;
            Item.autoReuse  = true;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = Item.useAnimation = 10;
            Item.createTile = ModContent.TileType<Tiles.Placable.CelestialJadeBrick>();
        }
    }
}