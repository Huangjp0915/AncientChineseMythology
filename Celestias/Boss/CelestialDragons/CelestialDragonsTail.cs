using Terraria;

namespace AncientChineseMythology.Celestias.Boss.CelestialDragons
{
    /// <summary>
    /// 天庭巡卫金龙 - 尾部
    /// 贴图尺寸: 412x124
    /// </summary>
    public class CelestialDragonsTail : CelestialDragons
    {
        public override WormType NPCWormType => WormType.Tail;

        public override void SetDefaults() {
            base.SetDefaults();
            // 尾部宽度
            NPC.width = (int)(TailTextureWidth * 0.5f);
            NPC.height = TailTextureHeight;
        }

        /// <summary>体节限伤：尾部受到的伤害降低70%</summary>
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            modifiers.FinalDamage *= 0.3f;
        }

        public override void ChangeSummonType() {
            // 尾巴不再生成后续节点
            SummonNPCType = 0;
        }

        protected override float GetSegmentWidth() {
            // 尾部较长，使用较小的有效宽度以保证紧密连接
            return TailTextureWidth * 0.3f;
        }
    }
}
