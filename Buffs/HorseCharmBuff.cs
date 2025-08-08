using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class HorseCharmBuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/HorseCharmBuff";

        public override void SetStaticDefaults() {
            Main.buffNoTimeDisplay[Type] = true; //不显示剩余时间
            Main.debuff[Type] = false; //不是负面状态
        }

    }
}
