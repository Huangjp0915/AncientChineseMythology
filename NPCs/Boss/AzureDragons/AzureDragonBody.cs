using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 青龙身体段 - 带有青蓝光效脉动的体节
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
        }

        public override void ChangeSummonType() {
            SummonNPCType = ModContent.NPCType<AzureDragonBody>();
            if (SummonCount >= SummonMax - 5)
                SummonNPCType = ModContent.NPCType<AzureDragonTail>();
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 贴图朝右为正方向，向左飞行时垂直翻转
            SpriteEffects effects = NPC.velocity.X < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // rotation已是原始速度角度（IsUseSpriteDirection=false时PostAI不会叠加Pi）
            Main.EntitySpriteDraw(tex, NPC.Center - screenPos, null, drawColor, NPC.rotation, origin, 1f, effects);

            // 体节光效脉动
            DrawSegmentGlow(spriteBatch, screenPos, drawColor);

            // 体节间的能量流动效果
            DrawEnergyFlow(spriteBatch, screenPos);

            return false;
        }

        /// <summary>
        /// 沿体节绘制能量流动光效
        /// </summary>
        private void DrawEnergyFlow(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.LightShot == null) return;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 能量流动波纹 - 使用SummonCount创建延迟波纹效果
            float wavePhase = segmentPulsePhase - SummonCount * 0.15f;
            float wavePulse = MathF.Max(0, MathF.Sin(wavePhase));

            if (wavePulse > 0.1f) {
                Color flowColor = DragonCyan * (0.35f * wavePulse);
                flowColor.A = 0;

                Vector2 flowOrigin = new(ACMAsset.LightShot.Width / 2f, ACMAsset.LightShot.Height / 2f);
                float flowScale = 0.8f + 0.4f * wavePulse;

                spriteBatch.Draw(ACMAsset.LightShot, NPC.Center - screenPos, null, flowColor,
                    NPC.rotation, flowOrigin, flowScale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
