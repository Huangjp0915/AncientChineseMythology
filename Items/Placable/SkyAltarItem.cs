using AncientChineseMythology.Tiles.Placable;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Placable
{
    public class SkyAltarItem : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Placable/SkyAltarItem";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width  = 32;                  // 贴图尺寸
            Item.height = 24;
            Item.maxStack = 1;                 // ★ 单件堆叠
            Item.useTurn  = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;  // 挥砍放置
            Item.consumable = true;                // 放置后会消耗
            Item.value = Item.buyPrice(gold: 1);    // 商店价格
            Item.createTile = ModContent.TileType<SkyAltarTile>(); // 放置生成的Tile
        }

        public override void AddRecipes()
        {
            // 示例配方：5 Fallen Stars + 25 Stone Block + 1 Work Bench
            CreateRecipe()
                .AddIngredient(ItemID.FallenStar, 5)
                .AddIngredient(ItemID.StoneBlock, 25)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        // 可选：确保玩家有权在多人服放置（例如限制地牢前）
        public override bool CanUseItem(Player player)
        {
            return true; // 如需判断条件，在此 return 条件
        }
    }
}
