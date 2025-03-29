using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items
{
    public class ScrapElixir : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 30; // 允许批量研究
        }

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 30;
            Item.maxStack = 999;
            Item.value = Item.sellPrice(0, 0, 1, 0);
            Item.rare = ItemRarityID.White;
            
            // 药水类基础设置
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.useTurn = true;
            Item.consumable = true;
            
            // 使用效果
            Item.healLife = 50; // 恢复50生命
            Item.buffType = BuffID.Poisoned; // 中毒debuff
            Item.buffTime = 900; // 15秒中毒
            Item.UseSound = SoundID.Item3; // 药水使用音效
        }
    }
}