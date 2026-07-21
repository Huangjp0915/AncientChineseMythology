using Terraria;

namespace AncientChineseMythology.NPCs.Boss.TribulationCloud
{
    /// <summary>
    /// 赤雷劫云 (红) —— 佯攻点雷。假蓄力 (非红主题色烟) 诱你提前闪避, 真雷追到你的新站位 (红色预警) 才落。
    /// 波1/2 单重佯攻 → 波3 双重佯攻 (假→假→真, 骗两次) → 终雷不佯攻 ("最后一记, 天不骗你")。
    /// 反制 = 别被假动作骗走, 等真正的红色预警出现再躲。全部共享逻辑见 <see cref="TribulationCloudBase"/>。
    /// </summary>
    public class TribulationCloudRed : TribulationCloudBase
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/TribulationCloud/TribulationCloud_red";

        public override TribulationKind Kind => TribulationKind.Red;
        // 暗赤风暴氛围 (压暗去饱和的赤色, 非纯红致命色; 致命预警仍走 TelegraphColors.Lethal)
        public override Color ThemeColor => new(140, 52, 70);

        // 6~9 记佯攻雷 (每记含假/真两段, 实际压迫更长)
        protected override int RollTotalStrikes() => Main.rand.Next(6, 10);
    }
}
