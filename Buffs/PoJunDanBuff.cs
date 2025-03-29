using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class PoJunDanBuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_5";
        public override void SetStaticDefaults()
        {
            // 如无自定义图标，可使用原版图标（例如选择 BuffID.Ironskin 对应的图标："Terraria/Images/Buff_5"）
            // public override string Texture => "Terraria/Images/Buff_5";
            Main.buffNoTimeDisplay[Type] = false;
            Main.debuff[Type] = false;
        }
        
        public override void Update(Player player, ref int buffIndex)
        {
            // 正面效果由 ModPlayer 处理，此处只做触发逻辑
            
            // 当正面Buff即将结束（剩1 tick）时，自动添加后遗症Debuff，持续30秒（1800 ticks）
            if (player.buffTime[buffIndex] == 1)
            {
                player.AddBuff(ModContent.BuffType<PoJunDanDebuff>(), 1800);
            }
        }
    }
}
