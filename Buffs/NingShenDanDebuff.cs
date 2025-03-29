using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class NingShenDanDebuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_20";
        public override void SetStaticDefaults()
        {
            // 例如使用原版中毒图标
            // public override string Texture => "Terraria/Images/Buff_20";
            Main.debuff[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }
        
        public override void Update(Player player, ref int buffIndex)
        {
            // 后遗症效果扣魔由 ModPlayer 处理
        }
    }
}
