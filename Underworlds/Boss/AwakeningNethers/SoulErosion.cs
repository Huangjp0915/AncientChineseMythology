using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers
{
    /// <summary>
    /// 魂蚀 (Soul Erosion) — 觉醒冥龙的签名地府 DoT。
    /// 站在虚空魂雾中会叠加层数；层数越高，灵魂腐蚀越快，预告酆都装备身份。
    /// 实际伤害与层数由 <see cref="AwakeningNetherPlayer"/> 处理，本类仅作为可视 Debuff。
    /// </summary>
    public class SoulErosion : ModBuff
    {
        // 复用原版暗影焰 Debuff 贴图，避免缺失 PNG 崩溃。
        public override string Texture => "Terraria/Images/Buff_" + BuffID.ShadowFlame;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex) {
            // 持续伤害交由 AwakeningNetherPlayer 根据层数处理。
        }
    }

    /// <summary>
    /// 觉醒冥龙魂蚀适配器 —— 已统一到共享地府身份层 <see cref="UnderworldFieldPlayer"/>。
    /// 保留此类与其方法签名，使觉醒冥龙现有调用点（AddSoulErosion）零改动、行为完全保留；
    /// 实际层数/DoT/衰减全部由共享身份层处理（调参与原 P0 一致：MaxStacks=10、interval、dmg）。
    /// </summary>
    public class AwakeningNetherPlayer : ModPlayer
    {
        public const int MaxStacks = UnderworldFieldPlayer.MaxSoulErosion;

        /// <summary>当前魂蚀层数（读取共享身份层）。</summary>
        public int SoulErosionStacks => Player.GetModPlayer<UnderworldFieldPlayer>().SoulErosionStacks;

        /// <summary>由魂雾 / 弹幕叠加魂蚀（委托共享身份层）。</summary>
        public void AddSoulErosion(int amount) =>
            Player.GetModPlayer<UnderworldFieldPlayer>().AddSoulErosion(amount);
    }
}
