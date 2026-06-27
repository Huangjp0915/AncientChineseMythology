using Microsoft.Xna.Framework;
using Terraria;

namespace AncientChineseMythology.NPCs.Boss.TribulationCloud
{
    /// <summary>
    /// 玄雷劫云 (黑) —— 固定节奏点雷, 教玩家"读拍子"。蓄力后在记录的站位原地落雷, 节奏稳定可预读。
    /// 全部共享逻辑见 <see cref="TribulationCloudBase"/>; 本类只参数化外观/节奏。
    /// </summary>
    public class TribulationCloudBlack : TribulationCloudBase
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/TribulationCloud/TribulationCloud_black";

        public override TribulationKind Kind => TribulationKind.Black;
        // 玄色风暴 (幽蓝紫氛围, 非红主题色)
        public override Color ThemeColor => new Color(72, 84, 140);

        // 固定 12 记 —— 稳定拍子, 入门考验
        protected override int RollTotalStrikes() => 12;
    }
}
