using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    internal partial class Aoshun
    {
        #region 绘制

        /// <summary>
        /// 头部绘制 - 纹理Aoshun.png: 52×140, 2帧, 每帧52×70
        /// 新增：风暴蓄电状态下的电光效果
        /// </summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            int frameHeight = texture.Height / HeadFrameCount;
            int yPos = frameHeight * NPC.frame.Y;
            Rectangle sourceRectangle = new Rectangle(0, yPos, texture.Width, frameHeight);

            Vector2 origin = NPC.spriteDirection == -1
                ? new Vector2(texture.Width * 0.5f + 10, frameHeight * 0.5f + 16)
                : new Vector2(texture.Width - 10, frameHeight + 16);

            Vector2 drawPos = NPC.Center - screenPos;

            // === 风暴蓄电视觉 ===
            float chargeRatio = StormCharge / MaxStormCharge;

            if (chargeRatio > 0.2f) {
                // 蓄电光晕 - 随蓄电量增强
                Texture2D glowTex = ACMAsset.SoftGlow;
                if (glowTex != null) {
                    Vector2 glowOrigin = glowTex.Size() / 2f;
                    float pulse = 1f + MathF.Sin(globalTime * 4f) * 0.15f;
                    float auraScale = (0.8f + chargeRatio * 0.8f) * pulse;
                    float auraAlpha = chargeRatio * 0.35f;

                    // 外层紫色光晕
                    Color outerGlow = AoshunHelper.ThunderPurple * auraAlpha * 0.5f;
                    outerGlow.A = 0;
                    spriteBatch.Draw(glowTex, drawPos, null, outerGlow, 0f, glowOrigin, auraScale * 1.5f, SpriteEffects.None, 0f);

                    // 内层蓝色光晕
                    Color innerGlow = AoshunHelper.LightningBlue * auraAlpha * 0.7f;
                    innerGlow.A = 0;
                    spriteBatch.Draw(glowTex, drawPos, null, innerGlow, 0f, glowOrigin, auraScale, SpriteEffects.None, 0f);

                    // 满电闪烁白光
                    if (IsFullyCharged) {
                        float flash = MathF.Sin(globalTime * 8f) * 0.5f + 0.5f;
                        Color whiteFlash = AoshunHelper.ElectricWhite * flash * 0.4f;
                        whiteFlash.A = 0;
                        spriteBatch.Draw(glowTex, drawPos, null, whiteFlash, 0f, glowOrigin, auraScale * 0.6f, SpriteEffects.None, 0f);
                    }
                }

                // === ElectricArcSheet帧动画电弧装饰 - 围绕头部 ===
                Texture2D arcSheet = ACMAsset.ElectricArcSheet;
                if (arcSheet != null && chargeRatio > 0.5f) {
                    int arcIndex = ((int)(globalTime * 8f)) % 4;
                    int arcHeight = arcSheet.Height / 4;
                    Rectangle arcSourceRect = new Rectangle(0, arcIndex * arcHeight, arcSheet.Width, arcHeight);
                    Vector2 arcOrigin = new Vector2(arcSourceRect.Width / 2f, arcSourceRect.Height / 2f);

                    float arcAlpha = (chargeRatio - 0.5f) * 2f * 0.35f;
                    Color arcColor = AoshunHelper.LightningBlue * arcAlpha;
                    arcColor.A = 0;

                    // 左侧电弧
                    spriteBatch.Draw(arcSheet, drawPos + new Vector2(-20, -5), arcSourceRect, arcColor,
                        NPC.rotation + MathHelper.PiOver4, arcOrigin, 0.1f, SpriteEffects.None, 0f);
                    // 右侧电弧
                    spriteBatch.Draw(arcSheet, drawPos + new Vector2(20, -5), arcSourceRect, arcColor * 0.8f,
                        NPC.rotation - MathHelper.PiOver4, arcOrigin, 0.1f, SpriteEffects.FlipHorizontally, 0f);

                    // 满电时额外一层
                    if (IsFullyCharged) {
                        int arcIndex2 = ((int)(globalTime * 12f + 2)) % 4;
                        Rectangle arcRect2 = new Rectangle(0, arcIndex2 * arcHeight, arcSheet.Width, arcHeight);
                        Color arcColor2 = AoshunHelper.ElectricWhite * 0.2f;
                        arcColor2.A = 0;
                        spriteBatch.Draw(arcSheet, drawPos + new Vector2(0, -15), arcRect2, arcColor2,
                            NPC.rotation, arcOrigin, 0.08f, SpriteEffects.None, 0f);
                    }
                }
            }

            // === 阶段转换时的强光效果 ===
            if (CurrentState == AoshunState.PhaseTransition) {
                Texture2D glowTex = ACMAsset.SoftGlow;
                if (glowTex != null) {
                    float transitionPulse = MathF.Sin(globalTime * 6f) * 0.5f + 0.5f;
                    Vector2 glowOrigin = glowTex.Size() / 2f;
                    Color transColor = AoshunHelper.ElectricWhite * transitionPulse * 0.6f;
                    transColor.A = 0;
                    spriteBatch.Draw(glowTex, drawPos, null, transColor, globalTime * 2f, glowOrigin, 2.5f, SpriteEffects.None, 0f);
                }
            }

            // === 主体纹理 ===
            // 蓄电时叠加蓝色高光
            Color finalDrawColor = drawColor;
            if (chargeRatio > 0.5f) {
                float tint = (chargeRatio - 0.5f) * 2f; // 0~1
                finalDrawColor = Color.Lerp(drawColor, Color.Lerp(drawColor, AoshunHelper.LightningBlue, 0.3f), tint);
            }

            spriteBatch.Draw(texture, drawPos,
                sourceRectangle, finalDrawColor, NPC.rotation, origin, NPC.scale, effects, 0);

            return false;
        }

        public override void FindFrame(int frameHeight) {
            // 攻击/蓄力时张嘴
            if (close || CurrentState == AoshunState.Attacking || CurrentState == AoshunState.Emerge) {
                NPC.frame.Y = 1;
            }
            else {
                NPC.frame.Y = 0;
            }
        }

        public override void BossHeadRotation(ref float rotation) {
            rotation = NPC.rotation;
        }

        #endregion
    }
}
