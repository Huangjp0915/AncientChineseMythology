using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class PoJunDanDebuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_20";
        public override void SetStaticDefaults()
        {
            // 如无自定义图标，可使用原版图标（例如选择BuffID.Poisoned，对应路径："Terraria/Images/Buff_20"）
            // public override string Texture => "Terraria/Images/Buff_20";
            Main.buffNoTimeDisplay[Type] = false;
            Main.debuff[Type] = true;
        }
        
        public override void Update(Player player, ref int buffIndex)
        {
            // 后遗症效果由 ModPlayer 处理，此处无需额外逻辑
        }
    }
}
