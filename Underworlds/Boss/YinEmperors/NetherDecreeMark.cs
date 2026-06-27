using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors
{
    /// <summary>
    /// 冥律标记 —— 阴天子审判机制的可视化指示。层数由 <see cref="YinJudgmentPlayer"/> 管理，
    /// 满层触发“定魂”移动锁定。此处仅作图标提示，实际逻辑在 ModPlayer 中处理。
    /// </summary>
    public class NetherDecreeMark : ModBuff
    {
        // 复用原版 debuff 贴图，避免缺图崩溃
        public override string Texture => "Terraria/Images/Buff_" + Terraria.ID.BuffID.ShadowFlame;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            // 逻辑全部在 YinJudgmentPlayer 中处理
        }
    }
}
