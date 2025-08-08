using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class XuanTieBleed : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/BlankBuff";
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;   //视为 Debuff
            Main.buffNoSave[Type] = true;
            Main.pvpBuff[Type] = true;
        }
    }
}