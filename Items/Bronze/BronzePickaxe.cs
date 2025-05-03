using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class BronzePickaxe : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Bronze/BronzePickaxe";
        public override void SetDefaults()
        {
            Item.damage = 15; // 镐力
            Item.noMelee = false; // 攻击类型
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 20; // 使用时间
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing; // 持物品的姿势
            Item.pick = 65; // 镐力（矿石打击能力）
            Item.knockBack = 6;
            Item.rare = ItemRarityID.Yellow; // 稀有度
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<BronzeIngot>(), 12);
            recipe.AddTile(TileID.Anvils);
            recipe.Register();
        }
    }
}
