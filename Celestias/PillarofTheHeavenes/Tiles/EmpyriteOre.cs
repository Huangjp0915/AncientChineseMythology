using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes.Tiles
{
    /// <summary>
    /// 天极矿 - 天柱周围生成的神圣矿物
    /// 金色+青色主题，月后初期材料
    /// </summary>
    public class EmpyriteOre : ModItem
    {
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 100;
        }

        public override void SetDefaults() {
            Item.width = 12;
            Item.height = 12;
            Item.maxStack = 9999;
            Item.value = Item.sellPrice(silver: 50);
            Item.rare = ItemRarityID.Red;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.autoReuse = true;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<EmpyriteOreTile>();
        }

        public override void PostUpdate() {
            // 掉落时发光
            Lighting.AddLight(Item.Center, new Vector3(1f, 0.9f, 0.5f) * 0.4f);
        }

        public override Color? GetAlpha(Color lightColor) {
            // 自发光效果
            return Color.Lerp(lightColor, Color.White, 0.3f);
        }
    }
}
