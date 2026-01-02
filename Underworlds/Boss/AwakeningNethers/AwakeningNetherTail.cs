namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers
{
    /// <summary>
    /// 觉醒-冥府尽头-幽冥龙 尾巴部分
    /// </summary>
    public class AwakeningNetherTail : AwakeningNether
    {
        public override WormType NPCWormType => WormType.Tail;

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 35;
            NPC.height = 35;
        }
    }
}
