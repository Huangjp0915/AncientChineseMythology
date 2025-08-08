using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Buffs
{
    public class XuanGangDanDebuff : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/XuanGangDanDebuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;

        }

        public override void Update(Player player, ref int buffIndex) {
            //降低玩家移动速度：
            //将跑步加速度和最大跑速乘以 0.3（即仅保留30%的移动能力）
            player.accRunSpeed *= 0.3f;
            player.maxRunSpeed *= 0.3f;
            //重置速度，防止持续滑动
            player.velocity = Vector2.Zero;
        }
    }
}
