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

            // 最后几节切换到尾巴
            if (SummonCount > SummonMax - 3)
                SummonNPCType = ModContent.NPCType<NetherDragonTail>();
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 22;
            NPC.height = 22;
        }
    }
}
