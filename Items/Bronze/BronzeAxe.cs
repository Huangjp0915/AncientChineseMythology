using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Bronze
{
    public class BronzeAxe : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Bronze/BronzeAxe";
        public override void SetDefaults() {
            Item.damage = 12; // 斧力
            Item.noMelee = false; // 攻击类型
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 35; // 使用时间
            Item.useAnimation = 35;
            Item.useStyle = ItemUseStyleID.Swing; // 持物品的姿势
            Item.axe = 15; // 斧力（砍树能力）
            Item.knockBack = 6;
            Item.rare = ItemRarityID.Yellow; // 稀有度
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override void AddRecipes() {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<BronzeIngot>(), 12);
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }
    }
}
