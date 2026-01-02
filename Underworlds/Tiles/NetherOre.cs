using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Tiles
{
    /// <summary>
    /// 幽冥矿物品
    /// </summary>
    public class NetherOre : ModItem
    {
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 100;
            ItemID.Sets.SortingPriorityMaterials[Type] = 95;
        }

        public override void SetDefaults() {
            Item.width = 12;
            Item.height = 12;
            Item.maxStack = Item.CommonMaxStack;
            Item.value = Item.sellPrice(silver: 30);
            Item.rare = ItemRarityID.Cyan;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.autoReuse = true;
            Item.consumable = true;
            Item.createTile = ModContent.TileType<NetherOreTile>();
        }

        public override void AddRecipes() {
            // 幽冥矿锭配方：4个幽冥矿 = 1个幽冥锭
            CreateRecipe()
                .AddIngredient<NetherOre>(4)
                .AddTile(TileID.AdamantiteForge)
                .Register();
        }
    }
}
