using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class BaGuaBuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/BaGuaBuff";

        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.buffTime[buffIndex] = 2;
        }

        public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare) {
            var bp = Main.LocalPlayer.GetModPlayer<Players.BaGuaPlayer>();

            if (!string.IsNullOrEmpty(bp.CurrentName)) {
                //tip 最前放阵法名称，换行再放描述
                tip = $"{bp.CurrentName}\n{bp.CurrentDesc}";
            }
            else {
                tip = "没有激活的阵法";               //可自定义为空或其它文字
            }
        }
    }
}
