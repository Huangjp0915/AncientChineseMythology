using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Baihus
{
    /// <summary>
    /// 白虎·爪痕 Claw-Mark —— 白虎在猎杀中给玩家烙下的金属爪痕（纯视觉/反馈标记）。
    /// 真正驱动「蓄势扑击」升格的层数计在 <see cref="Baihu"/> 服务端字段 clawCharge 上（确定性、可同步），
    /// 本 Debuff 只负责给被标记的玩家明确「你已被盯上」的可读反馈，不直接改数值（避免 MP 命中判定不一致）。
    /// </summary>
    public class BaihuClawMark : ModBuff
    {
        public override string Texture => "AncientChineseMythology/Textures/Buffs/BlankBuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.pvpBuff[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            // 轻微减速，强化「被猎物锁定」的压迫感（不致命，纯节奏）。
            player.moveSpeed *= 0.94f;
        }

        /// <summary>给玩家烙下/刷新爪痕标记（客户端反馈用，安全可重复调用）。</summary>
        public static void Apply(Player player, int ticks = 300) {
            if (player != null && player.active && !player.dead)
                player.AddBuff(ModContent.BuffType<BaihuClawMark>(), ticks);
        }
    }
}
