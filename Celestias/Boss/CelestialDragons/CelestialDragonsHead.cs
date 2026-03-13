using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialDragons
{
    /// <summary>
    /// 天庭巡卫金龙 - 头部
    /// 贴图尺寸: 382x256
    /// </summary>
    [AutoloadBossHead]
    public class CelestialDragonsHead : CelestialDragons
    {
        public override WormType NPCWormType => WormType.Head;

        public override string BossHeadTexture => "AncientChineseMythology/Celestias/Boss/CelestialDragons/CelestialDragons_Head";

        public override void SetStaticDefaults() {
            base.SetStaticDefaults();
            NPCID.Sets.ShouldBeCountedAsBoss[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(NPC.type);
        }

        public override void SetDefaults() {
            base.SetDefaults();
            // 宽度用于计算与下一节的距离
            // 头部贴图382宽，第一个体节从头部的一半开始
            NPC.width = (int)(HeadTextureWidth * 0.5f);
            NPC.height = HeadTextureHeight;
            NPC.boss = true;
        }

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<CelestialDragonsBody>();
        }
    }
}
