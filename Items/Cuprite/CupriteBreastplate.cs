using AncientChineseMythology.Items.Bronze;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Cuprite
{
    [AutoloadEquip(EquipType.Body)]
    public class CupriteBreastplate : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Cuprite/CupriteBreastplate";

        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.sellPrice(silver: 90);
            Item.rare = ItemRarityID.Green;
            Item.defense = 10;
        }

        public override void AddRecipes() {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<Cuprite>(), 25);
            recipe.AddIngredient(ModContent.ItemType<BronzeIngot>(), 15);
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }
    }
}