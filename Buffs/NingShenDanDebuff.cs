using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class NingShenDanDebuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/NingShenDanDebuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex) {
            // 后遗症效果扣魔由 ModPlayer 处理
        }
    }
}
