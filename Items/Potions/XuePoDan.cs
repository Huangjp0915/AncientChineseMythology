using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Buffs;

namespace AncientChineseMythology.Items.Potions
{
    public class XuePoDan : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Potions/XuePoDan";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 30;
        }
        
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 30;
            Item.maxStack = 999;
            Item.value = Item.sellPrice(0, 2, 0, 0);
            Item.rare = ItemRarityID.Green;
            
            // 药水基本设置
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.useTurn = true;
            Item.consumable = true;
            
            // 使用后给予 60 秒正面 Buff（血魄丹效果）
            Item.buffType = ModContent.BuffType<XuePoDanBuff>();
            Item.buffTime = 3600; // 60秒 (60*60)
            Item.UseSound = SoundID.Item3;
        }
        
        public override bool? UseItem(Player player)
        {
            // 同时施加药水病：10分钟（600秒 * 60 = 36000）
            player.AddBuff(BuffID.PotionSickness, 36000, false);
            return base.UseItem(player);
        }
        
    }
}
