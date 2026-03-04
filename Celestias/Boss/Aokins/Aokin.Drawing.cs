using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    internal partial class Aokin
    {
        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 绘制蛇形身体段 - 从尾部到头部
            DrawSegments(spriteBatch, screenPos);

            // 绘制头部
            DrawHead(spriteBatch, screenPos, drawColor);

            return false;
        }

        private void DrawSegments(SpriteBatch spriteBatch, Vector2 screenPos) {
            Texture2D bodyTex = ModContent.Request<Texture2D>("AncientChineseMythology/Celestias/Boss/Aokins/AokinBody").Value;
            Texture2D tailTex = ModContent.Request<Texture2D>("AncientChineseMythology/Celestias/Boss/Aokins/AokinTail").Value;

            float firePulse = 1f + MathF.Sin(globalTime * 3f) * 0.06f;

            for (int i = SegmentCount - 1; i >= 0; i--) {
                Texture2D segTex = (i == SegmentCount - 1) ? tailTex : bodyTex;
                Vector2 origin = segTex.Size() / 2f;

                Color segColor = Lighting.GetColor((int)segmentPos[i].X / 16, (int)segmentPos[i].Y / 16);

                SpriteEffects effects = NPC.velocity.X > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                // 火焰光晕层（随阶段变化强度）
                if (flameAuraAlpha > 0.05f) {
                    float progress = (float)i / SegmentCount;
                    Color glowColor = Color.Lerp(AokinHelper.DragonFlameRed, AokinHelper.MoltenOrange, progress);
                    glowColor *= flameAuraAlpha * 0.3f * firePulse;
                    glowColor.A = 0;

                    spriteBatch.Draw(segTex, segmentPos[i] - screenPos, null, glowColor,
                        segmentRot[i] + MathF.PI / 2f, origin, NPC.scale * 1.2f * firePulse, effects, 0f);
                }

                // 主体
                spriteBatch.Draw(segTex, segmentPos[i] - screenPos, null, segColor,
                    segmentRot[i] + MathF.PI / 2f, origin, NPC.scale, effects, 0f);
            }
        }

        private void DrawHead(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D headTex = TextureAssets.Npc[Type].Value;
            Vector2 origin = headTex.Size() / 2f;

            float firePulse = 1f + MathF.Sin(globalTime * 3f) * 0.08f;

            // 火焰光环
            DrawFireAura(spriteBatch, screenPos, headTex, origin, firePulse);

            // 火焰拖尾
            DrawFireTrail(spriteBatch, screenPos, headTex, origin);

            // 主体颜色 - 微微带火焰色调
            Color fireTint = Color.Lerp(drawColor, AokinHelper.MoltenOrange, 0.2f);
            fireTint = Color.Lerp(fireTint, Color.White, 0.15f);

            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // 外层发光
            Color outerGlow = AokinHelper.BlazingGold * 0.4f * firePulse;
            outerGlow.A = 0;
            spriteBatch.Draw(headTex, NPC.Center - screenPos, null, outerGlow,
                NPC.rotation, origin, NPC.scale * 1.15f * firePulse, effects, 0f);

            // 主体
            spriteBatch.Draw(headTex, NPC.Center - screenPos, null, fireTint * NPC.Opacity,
                NPC.rotation, origin, NPC.scale * firePulse, effects, 0f);

            // 内部高光
            Color innerGlow = AokinHelper.PureWhite * 0.25f * firePulse;
            innerGlow.A = 0;
            spriteBatch.Draw(headTex, NPC.Center - screenPos, null, innerGlow,
                NPC.rotation, origin, NPC.scale * 0.8f, effects, 0f);

            // 龙眼光效
            DrawDragonEyes(spriteBatch, screenPos);
        }

        private void DrawFireAura(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D tex, Vector2 origin, float pulse) {
            if (flameAuraAlpha <= 0f) return;

            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            for (int i = 3; i >= 0; i--) {
                float layerAlpha = flameAuraAlpha * (0.15f - i * 0.03f);
                float layerScale = flameScale * (1.3f + i * 0.15f);
                float layerRot = flameRotation * (1f + i * 0.2f);

                Color layerColor = Color.Lerp(AokinHelper.DragonFlameRed, AokinHelper.MoltenOrange, i / 3f);
                layerColor *= layerAlpha * pulse;
                layerColor.A = 0;

                spriteBatch.Draw(tex, NPC.Center - screenPos, null, layerColor,
                    NPC.rotation + layerRot * (i % 2 == 0 ? 1 : -1), origin, NPC.scale * layerScale, effects, 0f);
            }
        }

        private void DrawFireTrail(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D tex, Vector2 origin) {
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / NPC.oldPos.Length;
                Color trailColor = Color.Lerp(AokinHelper.MoltenOrange, AokinHelper.DragonFlameRed, 1f - progress);
                trailColor *= progress * 0.3f;
                trailColor.A = 0;

                Vector2 pos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float trailScale = NPC.scale * (0.9f - i * 0.04f);
                float trailRot = NPC.oldRot.Length > i ? NPC.oldRot[i] : NPC.rotation;

                spriteBatch.Draw(tex, pos, null, trailColor, trailRot, origin, trailScale, effects, 0f);
            }
        }

        private void DrawDragonEyes(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.LightShot == null) return;

            Vector2 eyeOffset = NPC.rotation.ToRotationVector2() * 35f;
            Vector2 eyePos = NPC.Center + eyeOffset - screenPos;

            float eyePulse = 0.7f + MathF.Sin(globalTime * 5f) * 0.3f;

            Color eyeColor;
            if (IsPhase2) {
                eyeColor = Color.Lerp(AokinHelper.DragonFlameRed, AokinHelper.BlazingGold, MathF.Sin(globalTime * 4f) * 0.5f + 0.5f);
            }
            else {
                eyeColor = AokinHelper.MoltenOrange;
            }

            eyeColor *= eyePulse * 0.8f;
            eyeColor.A = 0;

            spriteBatch.Draw(ACMAsset.LightShot, eyePos, null, eyeColor, 0f,
                ACMAsset.LightShot.Size() / 2f, 0.5f * eyePulse * glowIntensity, SpriteEffects.None, 0f);
        }

        #endregion
    }
}
