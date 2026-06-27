using Microsoft.Xna.Framework;
using Terraria;

namespace AncientChineseMythology.NPCs.Boss.TribulationCloud
{
    /// <summary>
    /// 紫霄劫云 (紫) —— 移动安全区扫雷。一道雷幕横扫战场, 幕上有一道安全缝 (法眼), 玩家须站进缝里随之移动。
    /// 反制 = 提前读缝位 (翠玉安全色预告), 跟着缝走。全部共享逻辑见 <see cref="TribulationCloudBase"/>。
    /// </summary>
    public class TribulationCloudPurple : TribulationCloudBase
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/TribulationCloud/TribulationCloud_purple";

        public override TribulationKind Kind => TribulationKind.Purple;
        // 紫霄风暴
        public override Color ThemeColor => new Color(168, 96, 224);

        // 4~7 道扫雷 (每道为一次完整横扫事件, 单次时长长)
        protected override int RollTotalStrikes() => Main.rand.Next(4, 8);
    }
}
