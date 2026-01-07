using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialDragons
{
    /// <summary>
    /// 天庭巡卫金龙 - 体节
    /// 贴图尺寸: 152x92
    /// 每个体节覆盖前一个体节约40%宽度
    /// </summary>
    public class CelestialDragonsBody : CelestialDragons
    {
        public override WormType NPCWormType => WormType.Body;

        public override void SetDefaults()
        {
            base.SetDefaults();
            // 体节宽度，用于计算跟随距离
            NPC.width = BodyTextureWidth;
            NPC.height = BodyTextureHeight;
        }

        public override void ChangeSummonType()
        {
            SummonNPCType = ModContent.NPCType<CelestialDragonsBody>();

            // 最后3节切换到尾巴
            if (SummonCount >= SummonMax - 1)
                SummonNPCType = ModContent.NPCType<CelestialDragonsTail>();
        }
    }
}
