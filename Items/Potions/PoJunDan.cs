using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Buffs;

namespace AncientChineseMythology.Items
{
    public class PoJunDan : ModItem
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Potions/PoJunDan";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 30;
        }
        
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 30;
            Item.maxStack = 999;
            Item.value = Item.buyPrice(0, 2, 0, 0);
            Item.rare = ItemRarityID.Green;
            
            // 药水设置
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.useTurn = true;
            Item.consumable = true;
            
            // 使用后给予60秒正面Buff（破军丹效果）
            Item.buffType = ModContent.BuffType<PoJunDanBuff>();
            Item.buffTime = 3600; // 60秒
            Item.UseSound = SoundID.Item3;
        }
        
        public override bool? UseItem(Player player)
        {
            // 同时施加10分钟药水病
            player.AddBuff(BuffID.PotionSickness, 36000, false);
            return base.UseItem(player);
        }

    }
}
