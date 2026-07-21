using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 苍龙之躯 — 跟随体节。
    /// V3: 绘制完全移交头部统一处理 (PreDraw 直接跳过);
    /// 「龙身放电」由头部权威驱动 (头部按 SummonCount 轮询放弹), 本类只保留跟随与轻量粒子。
    /// 接触伤害下调至头部的 55% (盘绕表演不至于碾人, 公平阀门)。
    /// </summary>
    public class AzureDragonBody : AzureDragon
    {
        public override WormType NPCWormType => WormType.Body;

        public override void SetStaticDefaults() {
            base.SetStaticDefaults();
            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = drawModifiers;
        }

        public override void SetDefaults() {
            base.SetDefaults();
            NPC.width = 60;
            NPC.height = 60;
            NPC.damage = (int)(NPC.damage * 0.55f);
        }

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<AzureDragonBody>();
            if (SummonCount >= SummonMax - 5)
                SummonNPCType = ModContent.NPCType<AzureDragonTail>();
        }

        // 绘制由头部在 AzureDragonDraw 中统一承担 (含条带/贴图/辉光), 体节自身零绘制、零批次切换
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => false;
    }
}
