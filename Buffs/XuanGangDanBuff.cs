using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class XuanGangDanBuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_5";
        public override void SetStaticDefaults()
        {
            Main.buffNoTimeDisplay[Type] = false;
            Main.debuff[Type] = false;
        }
        
        public override void Update(Player player, ref int buffIndex)
        {
            // 增加防御力 15 点
            player.statDefense += 15;
            
            // 当 Buff 即将结束（只剩 1 tick）时，添加后遗症 Debuff，持续 20 秒（20*60 = 1200 ticks）
            if (player.buffTime[buffIndex] == 1)
            {
                player.AddBuff(ModContent.BuffType<XuanGangDanDebuff>(), 1200);
            }
        }
    }
}
