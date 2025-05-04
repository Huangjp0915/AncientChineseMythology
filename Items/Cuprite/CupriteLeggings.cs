using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using AncientChineseMythology.Items.Bronze;

namespace AncientChineseMythology.Items.Cuprite
{

    [AutoloadEquip(EquipType.Legs)]

    public class CupriteLeggings : ModItem {
        public override string Texture => "AncientChineseMythology/Textures/Items/Cuprite/CupriteLeggings";
        public override void SetDefaults() {
            Item.width  = 18;
            Item.height = 18;
            Item.value  = Item.sellPrice(silver: 70);
            Item.rare   = ItemRarityID.Green;
            Item.defense = 9;
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<Cuprite>(), 20);
            recipe.AddIngredient(ModContent.ItemType<BronzeIngot>(), 10);
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }
    }
}