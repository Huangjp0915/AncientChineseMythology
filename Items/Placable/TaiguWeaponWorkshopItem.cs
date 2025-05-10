using Terraria.ID;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Content.Items.Placeables
{
    public class TaiguWeaponWorkshopItem : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Placable/TaiguWeaponWorkshopItem";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.DefaultToPlaceableTile(
                ModContent.TileType<Tiles.TaiguWeaponWorkshop>(), 0);

            Item.width  = 32;
            Item.height = 32;
            Item.maxStack = 99;
            Item.value = Item.buyPrice(gold: 0);
            Item.rare  = ItemRarityID.Orange;
        }

        // 以后想添加配方就在这里写 AddRecipes()
    }
}
