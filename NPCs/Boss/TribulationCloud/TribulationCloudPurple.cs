using Terraria;

namespace AncientChineseMythology.NPCs.Boss.TribulationCloud
{
    /// <summary>
    /// 紫霄劫云 (紫) —— 移动安全区扫雷。雷幕横扫战场, 幕上的安全缝 (法眼) 是唯一活路。
    /// 波1 单幕缝静止 → 波2 缝随扫平滑漂移 (预告期翠玉幽线标出漂移路径) → 波3/终雷 双幕对扫合拢。
    /// 反制 = 提前读缝位 (翠玉安全色预告), 跟着缝走。全部共享逻辑见 <see cref="TribulationCloudBase"/>。
    /// </summary>
    public class TribulationCloudPurple : TribulationCloudBase
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/TribulationCloud/TribulationCloud_purple";

        public override TribulationKind Kind => TribulationKind.Purple;
        // 紫霄风暴
        public override Color ThemeColor => new(168, 96, 224);

        // 4~7 道扫雷 (每道为一次完整横扫事件, 单次时长长)
        protected override int RollTotalStrikes() => Main.rand.Next(4, 8);
    }
}
