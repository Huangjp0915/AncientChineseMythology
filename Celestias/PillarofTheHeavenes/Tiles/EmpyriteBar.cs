using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes.Tiles
{
    /// <summary>
    /// 天极锭 - 由天极矿熔炼而成的神圣金属
    /// 金色+青色主题，用于制作天柱系列装备
    /// </summary>
    public class EmpyriteBar : ModItem
    {
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 25;
        }

        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 9999;
            Item.value = Item.sellPrice(gold: 2);
            Item.rare = ItemRarityID.Red;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.autoReuse = true;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<EmpyriteBarTile>();
            Item.placeStyle = 0;
        }

        public override void PostUpdate() {
            // 掉落时发光 - 更强烈
            Lighting.AddLight(Item.Center, new Vector3(1f, 0.95f, 0.5f) * 0.6f);
        }

        public override Color? GetAlpha(Color lightColor) {
            // 自发光效果
            return Color.Lerp(lightColor, Color.White, 0.4f);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<EmpyriteOre>(4)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
