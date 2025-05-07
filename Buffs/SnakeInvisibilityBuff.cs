using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class SnakeInvisibilityBuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/SnakeInvisibilityBuff";

        public override void SetStaticDefaults()
        {
            // 不显示时间
            Main.buffNoTimeDisplay[Type] = true;
            // 不是 debuff 类型
            Main.debuff[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            // 设置隐身效果
            player.invis = true;
        }
    }
}
