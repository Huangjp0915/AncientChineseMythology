using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Buffs;

namespace AncientChineseMythology.Items
{
    public class NingShenDan : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 30;
        }
        
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 30;
            Item.maxStack = 999;
            Item.value = Item.sellPrice(0, 1, 0, 0);
            Item.rare = ItemRarityID.Green;
            
            // 药水基本设置
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.useTurn = true;
            Item.consumable = true;
            
            // 使用后给予 60 秒正面 Buff（凝神丹效果）
            Item.buffType = ModContent.BuffType<NingShenDanBuff>();
            Item.buffTime = 3600; // 60秒
            Item.UseSound = SoundID.Item3;
        }
        
        public override bool? UseItem(Player player)
        {
            // 同时施加药水病：10分钟 (600秒 * 60)
            player.AddBuff(BuffID.PotionSickness, 36000, false);
            // 使用凝神丹时同时将魔力回满（包含扩容效果）
            player.statMana = player.statManaMax2;
            return base.UseItem(player);
        }
    }
}
