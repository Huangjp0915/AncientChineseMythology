using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers
{
    /// <summary>
    /// 觉醒-冥府尽头-幽冥龙 身体部分
    /// </summary>
    public class AwakeningNetherBody : AwakeningNether
    {
        public override WormType NPCWormType => WormType.Body;

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<AwakeningNetherBody>();

            // 最后几节切换到尾巴
            if (SummonCount > SummonMax - 3)
                SummonNPCType = ModContent.NPCType<AwakeningNetherTail>();
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 40;
            NPC.height = 40;
        }
    }
}
