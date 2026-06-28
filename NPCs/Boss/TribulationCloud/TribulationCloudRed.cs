using Terraria;

namespace AncientChineseMythology.NPCs.Boss.TribulationCloud
{
    /// <summary>
    /// 赤雷劫云 (红) —— 佯攻点雷。先放假蓄力 (非红尘烟) 诱你提前闪避, 0.5s 后真雷追到你的新站位 (红色预警) 才落。
    /// 反制 = 别被假动作骗走, 等真正的红色预警出现再躲。全部共享逻辑见 <see cref="TribulationCloudBase"/>。
    /// </summary>
    public class TribulationCloudRed : TribulationCloudBase
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/TribulationCloud/TribulationCloud_red";

        public override TribulationKind Kind => TribulationKind.Red;
        // 暗赤风暴氛围 (压暗去饱和的赤色, 非纯红致命色; 致命预警仍走 TelegraphColors.Lethal)
        public override Color ThemeColor => new Color(140, 52, 70);

        // 6~9 记佯攻雷 (每记含假/真两段, 实际压迫更长)
        protected override int RollTotalStrikes() => Main.rand.Next(6, 10);
    }
}
