using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class PoJunDanDebuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/PoJunDanDebuff";

        public override void SetStaticDefaults() {
            Main.buffNoTimeDisplay[Type] = false;
            Main.debuff[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            //后遗症效果由 ModPlayer 处理，此处无需额外逻辑
        }
    }
}
