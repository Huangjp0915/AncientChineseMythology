using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas
{
    /// <summary>
    /// 毗沙门天王 - 绘制方法（分离文件）
    /// </summary>
    internal partial class Vaisravana
    {
        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 绘制神圣光环（底层）
            DrawDivineAura(spriteBatch, screenPos);

            // 绘制拖尾
            DrawTrail(spriteBatch, screenPos);

            // 绘制宝塔
            DrawTowers(spriteBatch, screenPos, drawColor);

            // 绘制光晕（在本体之前）
            DrawHalo(spriteBatch, screenPos);

            // 绘制本体
            DrawMainBody(spriteBatch, screenPos, drawColor);

            // 绘制外层光效
            DrawOuterGlow(spriteBatch, screenPos);

            return false;
        }

        private void DrawDivineAura(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.LightShot == null) return;

            Texture2D auraTexture = ACMAsset.LightShot;
            Vector2 drawPos = NPC.Center - screenPos;

            // 仙气白色光环
            Color auraColor = VaisravanaHelper.PureWhite * divineAuraAlpha;
            auraColor.A = 0;

            float auraScale = 9f * haloScale;

            spriteBatch.Draw(
                auraTexture,
                drawPos,
                null,
                auraColor,
                MathHelper.PiOver2,
                auraTexture.Size() / 2f,
                auraScale,
                SpriteEffects.None,
                0f
            );

            // 第二层淡蓝光环
            Color azureAura = VaisravanaHelper.CelestialAzure * divineAuraAlpha * 0.5f;
            azureAura.A = 0;

            spriteBatch.Draw(
                auraTexture,
                drawPos,
                null,
                azureAura,
                MathHelper.PiOver2,
                auraTexture.Size() / 2f,
                auraScale * 1.3f,
                SpriteEffects.None,
                0f
            );
        }

        private void DrawTrail(SpriteBatch spriteBatch, Vector2 screenPos) {
            Texture2D texture = TextureAssets.Npc[Type].Value;

            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / NPC.oldPos.Length;
                Color trailColor = VaisravanaHelper.SpiritSilver * progress * 0.25f * NPC.Opacity;
                trailColor.A = 0;
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float scale = NPC.scale * progress * 0.9f;

                spriteBatch.Draw(
                    texture,
                    drawPos,
                    null,
                    trailColor,
                    NPC.rotation,
                    texture.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawTowers(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (towerAngles == null) return;

            // 使用BlankStar绘制宝塔效果（可替换为专用纹理）
            Texture2D towerTexture = ACMAsset.BlankStar;
            if (towerTexture == null) return;

            for (int i = 0; i < TowerCount; i++) {
                Vector2 towerPos = GetTowerPosition(i) - screenPos;

                // 外层金光晕
                Color outerGlow = VaisravanaHelper.TowerGold * 0.5f;
                outerGlow.A = 0;
                spriteBatch.Draw(
                    towerTexture,
                    towerPos,
                    null,
                    outerGlow,
                    globalTime + i * 0.6f,
                    towerTexture.Size() / 2f,
                    0.7f,
                    SpriteEffects.None,
                    0f
                );

                // 核心白光
                Color coreColor = VaisravanaHelper.PureWhite;
                coreColor.A = 0;
                spriteBatch.Draw(
                    towerTexture,
                    towerPos,
                    null,
                    coreColor,
                    -globalTime * 0.6f + i * 0.4f,
                    towerTexture.Size() / 2f,
                    0.45f,
                    SpriteEffects.None,
                    0f
                );

                // 内核高光
                Color innerCore = VaisravanaHelper.DivineWhite;
                innerCore.A = 0;
                spriteBatch.Draw(
                    towerTexture,
                    towerPos,
                    null,
                    innerCore * 0.8f,
                    0f,
                    towerTexture.Size() / 2f,
                    0.3f,
                    SpriteEffects.None,
                    0f
                );

                // 宝塔连接线效果
                if (ACMAsset.GlaciateWave != null) {
                    Vector2 toCenter = NPC.Center - GetTowerPosition(i);
                    float distance = toCenter.Length();
                    float rotation = toCenter.ToRotation();

                    Color lineColor = VaisravanaHelper.SpiritSilver * 0.3f;
                    lineColor.A = 0;

                    Vector2 lineOrigin = new Vector2(0, ACMAsset.GlaciateWave.Height / 2f);
                    Vector2 lineScale = new Vector2(distance / ACMAsset.GlaciateWave.Width, 0.05f);

                    spriteBatch.Draw(
                        ACMAsset.GlaciateWave,
                        GetTowerPosition(i) - screenPos,
                        null,
                        lineColor,
                        rotation,
                        lineOrigin,
                        lineScale,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        private void DrawHalo(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.BlankStar == null) return;

            Texture2D haloTexture = ACMAsset.BlankStar;
            Vector2 drawPos = NPC.Center - screenPos;

            // 多层光环
            for (int i = 0; i < 3; i++) {
                float layerRotation = haloRotation + i * MathHelper.TwoPi / 3f;
                float layerScale = (1.6f + i * 0.35f) * haloScale;
                Color layerColor = VaisravanaHelper.PureWhite * (0.35f - i * 0.08f);
                layerColor.A = 0;

                spriteBatch.Draw(
                    haloTexture,
                    drawPos,
                    null,
                    layerColor,
                    layerRotation,
                    haloTexture.Size() / 2f,
                    layerScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // 反向旋转的淡蓝光环
            Color azureHalo = VaisravanaHelper.CelestialAzure * 0.25f;
            azureHalo.A = 0;
            spriteBatch.Draw(
                haloTexture,
                drawPos,
                null,
                azureHalo,
                -haloRotation * 0.7f,
                haloTexture.Size() / 2f,
                2f * haloScale,
                SpriteEffects.None,
                0f
            );
        }

        private void DrawMainBody(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;

            // 内层发光
            Color glowColor = VaisravanaHelper.PureWhite * 0.35f * NPC.Opacity;
            glowColor.A = 0;

            for (int i = 0; i < 4; i++) {
                float angle = globalTime * 1.8f + i * MathHelper.PiOver2;
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 5f;
                spriteBatch.Draw(
                    texture,
                    drawPos + offset,
                    null,
                    glowColor,
                    NPC.rotation,
                    texture.Size() / 2f,
                    NPC.scale * 1.08f,
                    SpriteEffects.None,
                    0f
                );
            }

            // 本体
            Color bodyColor = drawColor * NPC.Opacity;
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                bodyColor,
                NPC.rotation,
                texture.Size() / 2f,
                NPC.scale,
                SpriteEffects.None,
                0f
            );

            // 高光叠加
            Color highlightColor = VaisravanaHelper.DivineWhite * 0.2f * NPC.Opacity;
            highlightColor.A = 0;
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                highlightColor,
                NPC.rotation,
                texture.Size() / 2f,
                NPC.scale * 0.95f,
                SpriteEffects.None,
                0f
            );
        }

        private void DrawOuterGlow(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.Sparkle == null) return;

            Texture2D sparkleTexture = ACMAsset.Sparkle;
            Vector2 drawPos = NPC.Center - screenPos;

            Color sparkleColor = VaisravanaHelper.PureWhite * 0.28f * glowIntensity;
            sparkleColor.A = 0;

            // 旋转的星芒
            spriteBatch.Draw(
                sparkleTexture,
                drawPos,
                null,
                sparkleColor,
                globalTime * 0.4f,
                sparkleTexture.Size() / 2f,
                2.2f * haloScale,
                SpriteEffects.None,
                0f
            );

            // 反向旋转的星芒
            Color secondarySparkle = VaisravanaHelper.CelestialAzure * 0.18f * glowIntensity;
            secondarySparkle.A = 0;
            spriteBatch.Draw(
                sparkleTexture,
                drawPos,
                null,
                secondarySparkle,
                -globalTime * 0.25f,
                sparkleTexture.Size() / 2f,
                2.8f * haloScale,
                SpriteEffects.None,
                0f
            );

            // 三阶段额外光效
            if (IsPhase3 && ACMAsset.LightShot != null) {
                float pulseAlpha = 0.15f + MathF.Sin(globalTime * 4f) * 0.08f;
                Color pulseColor = VaisravanaHelper.DivineWhite * pulseAlpha;
                pulseColor.A = 0;

                spriteBatch.Draw(
                    ACMAsset.LightShot,
                    drawPos,
                    null,
                    pulseColor,
                    0f,
                    ACMAsset.LightShot.Size() / 2f,
                    5f * haloScale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        #endregion
    }
}
