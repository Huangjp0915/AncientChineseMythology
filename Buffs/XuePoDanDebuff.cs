using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class XuePoDanDebuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buffs/Buff_20";

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }
        
        public override void Update(Player player, ref int buffIndex)
        {
            // 效果由 ModPlayer 中处理，这里可添加额外视觉效果
        }
    }
}
