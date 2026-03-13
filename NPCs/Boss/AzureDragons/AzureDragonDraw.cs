using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 青龙头部 - 绘制与视觉特效
    /// 利用ACMAsset中的纹理实现叠加光效
    /// </summary>
    public partial class AzureDragonHead
    {
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            // 贴图朝右为正方向，向左飞行时垂直翻转
            SpriteEffects effects = NPC.velocity.X < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // 拖尾残影
            float ghostAlpha = 0.2f;
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;
                float t = i / (float)NPC.oldPos.Length;
                float alpha = ghostAlpha * (1f - t);
                Color ghostColor = Color.Lerp(DragonLightning, DragonDeep, t) * alpha;
                float rot = NPC.oldRot[i]; // 已是原始速度角度，无需额外旋转
                float scale = 1f - t * 0.15f;

                Main.EntitySpriteDraw(tex, NPC.oldPos[i] + NPC.Size / 2f - screenPos, null,
                    ghostColor, rot, origin, scale, effects);
            }

            // 主体绘制 - rotation已是原始速度角度
            Main.EntitySpriteDraw(tex, NPC.Center - screenPos, null, drawColor, NPC.rotation, origin, 1f, effects);

            // 叠加青蓝光效（使用SoftGlow纹理Additive叠加）
            DrawHeadGlow(spriteBatch, screenPos);

            // 闪电电弧装饰（使用ElectricArcSheet纹理）
            DrawHeadElectricArc(spriteBatch, screenPos);

            return false;
        }

        /// <summary>
        /// 头部青蓝柔光叠加
        /// </summary>
        private void DrawHeadGlow(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.SoftGlow == null) return;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            float pulse = 0.7f + 0.3f * MathF.Sin(globalTime * 4f);
            float scale = 2.0f + 0.5f * auraIntensity;

            // 主光晕
            Color glowColor = DragonCyan * (0.5f * pulse * auraIntensity);
            glowColor.A = 0;
            spriteBatch.Draw(ACMAsset.SoftGlow, NPC.Center - screenPos, null, glowColor,
                0f, new Vector2(ACMAsset.SoftGlow.Width / 2f, ACMAsset.SoftGlow.Height / 2f),
                scale, SpriteEffects.None, 0f);

            // 内层更亮的白光核心
            Color coreColor = Color.White * (0.3f * pulse * auraIntensity);
            coreColor.A = 0;
            spriteBatch.Draw(ACMAsset.SoftGlow, NPC.Center - screenPos, null, coreColor,
                0f, new Vector2(ACMAsset.SoftGlow.Width / 2f, ACMAsset.SoftGlow.Height / 2f),
                scale * 0.5f, SpriteEffects.None, 0f);

            // 星星光效（使用BlankStar纹理）
            if (ACMAsset.BlankStar != null && auraIntensity > 0.8f) {
                Color starColor = DragonLightning * (0.35f * pulse);
                starColor.A = 0;
                spriteBatch.Draw(ACMAsset.BlankStar, NPC.Center - screenPos, null, starColor,
                    globalTime * 1.5f, new Vector2(ACMAsset.BlankStar.Width / 2f, ACMAsset.BlankStar.Height / 2f),
                    1.8f * auraIntensity, SpriteEffects.None, 0f);
            }

            // Sparkle爆炸线效果（阶段转换和冲刺时增强）
            if (ACMAsset.Sparkle != null && (State == AIState.PhaseTransition_2 || State == AIState.PhaseTransition_3)) {
                float sparkPulse = MathF.Sin(globalTime * 8f) * 0.5f + 0.5f;
                Color sparkColor = Color.White * (0.6f * sparkPulse);
                sparkColor.A = 0;
                spriteBatch.Draw(ACMAsset.Sparkle, NPC.Center - screenPos, null, sparkColor,
                    globalTime * 3f, new Vector2(ACMAsset.Sparkle.Width / 2f, ACMAsset.Sparkle.Height / 2f),
                    2.5f, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// 头部闪电电弧装饰
        /// </summary>
        private void DrawHeadElectricArc(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.ElectricArcSheet == null || auraIntensity < 0.5f) return;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D arcTex = ACMAsset.ElectricArcSheet;
            // 取其中一段电弧（通过UV偏移）
            int arcIndex = ((int)(globalTime * 8f)) % 4;
            int arcHeight = arcTex.Height / 4;
            Rectangle sourceRect = new(0, arcIndex * arcHeight, arcTex.Width, arcHeight);

            float arcAlpha = 0.4f * auraIntensity * (0.6f + 0.4f * MathF.Sin(globalTime * 6f));
            Color arcColor = DragonCyan * arcAlpha;
            arcColor.A = 0;

            Vector2 arcOrigin = new(sourceRect.Width / 2f, sourceRect.Height / 2f);
            float arcScale = 0.25f;
            float arcRot = NPC.rotation + MathHelper.PiOver2;

            spriteBatch.Draw(arcTex, NPC.Center - screenPos, sourceRect, arcColor,
                arcRot, arcOrigin, arcScale, SpriteEffects.None, 0f);

            // 对称另一侧
            spriteBatch.Draw(arcTex, NPC.Center - screenPos, sourceRect, arcColor * 0.6f,
                arcRot + MathHelper.Pi, arcOrigin, arcScale * 0.8f, SpriteEffects.FlipHorizontally, 0f);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
