namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙尾巴
    /// </summary>
    public class NetherDragonTail : NetherDragon
    {
        public override WormType NPCWormType => WormType.Tail;

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 22;
            NPC.height = 22;
        }
    }
}
