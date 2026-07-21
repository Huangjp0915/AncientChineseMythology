using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙身体部分
    /// </summary>
    public class NetherDragonBody : NetherDragon
    {
        public override WormType NPCWormType => WormType.Body;

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<NetherDragonBody>();

            // 末节切换到尾巴 (尾的 SummonCount = SummonMax+1 > SummonMax → 链在尾自然终止,
            // 避免 V2 提前切尾导致尾继续走 BasicWorm 生成分支)
            if (SummonCount >= SummonMax)
                SummonNPCType = ModContent.NPCType<NetherDragonTail>();
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 22;
            NPC.height = 22;
        }
    }
}
