using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace AncientChineseMythology.Buffs
{
    public class XuanGangDanDebuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_20";
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
            // 如无自定义图标，可以使用原版例如 Poisoned（BuffID.Poisoned=20）的图标：
            // public override string Texture => "Terraria/Images/Buff_20";
        }
        
        public override void Update(Player player, ref int buffIndex)
        {
            // 降低玩家移动速度：
            // 将跑步加速度和最大跑速乘以 0.3（即仅保留30%的移动能力）
            player.accRunSpeed *= 0.3f;
            player.maxRunSpeed *= 0.3f;
            // 重置速度，防止持续滑动
            player.velocity = Vector2.Zero;
        }
    }
}
