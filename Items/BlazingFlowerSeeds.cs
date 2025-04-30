using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class BlazingFlowerSeeds : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/BlazingFlowerSeeds";
        public override void SetStaticDefaults() => Item.ResearchUnlockCount = 25;

        public override void SetDefaults()
        {
            Item.width  = 14;
            Item.height = 14;
            Item.maxStack = 999;
            Item.consumable = true;

            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = Item.useAnimation = 15;
            Item.autoReuse = true;
            Item.UseSound  = SoundID.Grass;

            Item.rare = ItemRarityID.Blue;
            Item.createTile = ModContent.TileType<Tiles.BlazingFlowerHerbTile>();
        }
    }
}
