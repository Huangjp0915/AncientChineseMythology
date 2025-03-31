using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class XuePoDanBuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/XuePoDanBuff";

        public override void SetStaticDefaults()
        {
            Main.buffNoTimeDisplay[Type] = false;
            Main.debuff[Type] = false;
        }
        
        public override void Update(Player player, ref int buffIndex)
        {
            // 增加最大生命值
            player.statLifeMax2 += 100;
            player.statLife = player.statLifeMax2;
            
            // 当 Buff 即将结束时（只剩 1 tick）添加后遗症 Buff，持续 30 秒（1800 ticks）
            if (player.buffTime[buffIndex] == 1)
            {
                player.AddBuff(ModContent.BuffType<XuePoDanDebuff>(), 1800);
            }
        }
    }
}
