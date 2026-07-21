using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    internal partial class Aokin
    {
        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 入场水下蓄势期: 本体隐匿（海面赤光由屏幕系统与粒子承担）
            if (IntroHidden)
                return false;

            // 预警线（画在本体之下）
            DrawTelegraphs(spriteBatch);

            // 熔鳞着色器身体批（含头部主体）; 着色器缺失时回退普通绘制
            DrawBodyMoltenScale(spriteBatch, screenPos, drawColor);

            // 头部光效层（光环 / 拖尾 / 龙眼 / 口部聚焰）
            DrawHeadOverlays(spriteBatch, screenPos);

            return false;
        }

        /// <summary>
        /// 签名时刻的全屏热浪蜃景（专属 AokinHeatHaze：垂直对流扭曲 + 余烬亮点 + vent 冲击环）。
        /// 喂 Main.screenTarget 的昂贵后处理, 受单一全屏后处理名额约束: 仅炼狱茧泄压 / 相变 / 焚海劫 /
        /// 逆鳞爆气 / 死亡新星时拉满, 平时 0 直接早退。氛围/泛光/地纹由 <see cref="AokinHeatScreenSystem"/> 单独承担。
        /// 专属着色器缺失时回退共享 GenericWarp(heat)。
        /// </summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;

            var (warp, ember, vent, ventWorldCenter) = HazeParams;
            bool ventOn = vent > 0.001f && vent < 0.999f;
            if (warp <= 0.01f && !ventOn)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Vector2 screenSize = new Vector2(Main.screenWidth, Main.screenHeight);
            Vector2 centerUV = (NPC.Center - Main.screenPosition) / screenSize;
            float aspect = (float)Main.screenWidth / Main.screenHeight;

            Effect haze = AokinHelper.HeatHazeEffect;
            if (haze != null) {
                haze.Parameters["uTime"]?.SetValue(globalTime);
                haze.Parameters["uCenter"]?.SetValue(centerUV);
                haze.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(warp, 0f, 1f));
                haze.Parameters["uAspect"]?.SetValue(aspect);
                haze.Parameters["uEmber"]?.SetValue(MathHelper.Clamp(ember * MathF.Max(warp, ventOn ? 0.4f : 0f), 0f, 1f));
                haze.Parameters["uVent"]?.SetValue(ventOn ? vent : 0f);
                haze.Parameters["uVentCenter"]?.SetValue((ventWorldCenter - Main.screenPosition) / screenSize);
                haze.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.Flame.ToVector3(), 0.45f));

                ACMShaders.ApplyScreenPostProcess(spriteBatch, haze);
                return;
            }

            // 回退：共享 GenericWarp(heat)
            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;

            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(warp, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(0.9f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uWarpScale"]?.SetValue(1.3f);
            fx.Parameters["uChroma"]?.SetValue(0.4f);
            fx.Parameters["uRadialPull"]?.SetValue(-0.2f); // 轻微向外推 = 热浪上腾
            fx.Parameters["uMode"]?.SetValue(0f);          // 0 = heat 主题
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.Flame.ToVector3(), 0.55f));

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        #endregion

        #region 预警线（冲刺线 / 俯冲红线）

        private void DrawTelegraphs(SpriteBatch spriteBatch) {
            // 狂怒连冲: 冲刺线（金 → 临发射转致命红）
            if (chargeTelegraphT > 0.05f && CurrentState == MainState.Attacking && CurrentAttack == AttackType.FuryCharge) {
                Vector2 dir = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitX);
                Color lineColor = Color.Lerp(TelegraphColors.Gold, TelegraphColors.Lethal,
                    MathHelper.Clamp((chargeTelegraphT - 0.5f) * 2f, 0f, 1f));
                AokinHelper.DrawTelegraphLine(spriteBatch,
                    NPC.Center + dir * 60f, NPC.Center + dir * 980f,
                    lineColor, chargeTelegraphT, 0.09f);
            }

            // 烈焰俯冲: 垂直致命红线
            if (diveTelegraphT > 0.05f && CurrentState == MainState.Attacking && CurrentAttack == AttackType.Divebomb) {
                Player target = Main.player[NPC.target];
                float bottomY = (target?.active ?? false) ? target.Center.Y + 340f : NPC.Center.Y + 900f;
                AokinHelper.DrawTelegraphLine(spriteBatch,
                    new Vector2(diveTelegraphX, NPC.Center.Y),
                    new Vector2(diveTelegraphX, bottomY),
                    TelegraphColors.Lethal, diveTelegraphT, 0.08f);
            }
        }

        #endregion

        #region 熔鳞身体批

        /// <summary>
        /// 用 AokinMoltenScale 着色器一次 Immediate 批绘制全部身体段 + 头部主体：
        /// 温度驱动熔纹呼吸, 狂暴熔纹泛白, 死亡演出从尾至头逐段焦黑熄灭。
        /// </summary>
        private void DrawBodyMoltenScale(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D bodyTex = ModContent.Request<Texture2D>("AncientChineseMythology/Celestias/Boss/Aokins/AokinBody").Value;
            Texture2D tailTex = ModContent.Request<Texture2D>("AncientChineseMythology/Celestias/Boss/Aokins/AokinTail").Value;
            Texture2D headTex = TextureAssets.Npc[Type].Value;
            Rectangle headFrame = headTex.GetRectangle(((int)Main.GameUpdateCount) / 10 % 3, 3);

            Effect fx = AokinHelper.MoltenScaleEffect;
            Texture2D noise = ACMShaders.NoiseTexture;
            float firePulse = 1f + MathF.Sin(globalTime * 3f) * 0.06f;
            bool useShader = fx != null && noise != null;

            if (useShader) {
                fx.Parameters["uTime"]?.SetValue(globalTime);
                fx.Parameters["uHeat"]?.SetValue(HeatRatio);
                fx.Parameters["uRage"]?.SetValue(rageVisual);
                fx.Parameters["uGlowColor"]?.SetValue(AokinHelper.MoltenOrange.ToVector4());
                fx.Parameters["uGlowColor2"]?.SetValue(AokinHelper.DragonFlameRed.ToVector4());

                GraphicsDevice gd = Main.graphics.GraphicsDevice;
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            }

            // —— 身体段（尾 → 头）——
            for (int i = SegmentCount - 1; i >= 0; i--) {
                Texture2D segTex = (i == SegmentCount - 1) ? tailTex : bodyTex;
                Vector2 origin = segTex.Size() / 2f;

                Color segColor = Lighting.GetColor((int)segmentPos[i].X / 16, (int)segmentPos[i].Y / 16);
                segColor = Color.Lerp(segColor, Color.White, 0.1f);

                float segDirX = MathF.Cos(segmentRot[i]);
                SpriteEffects effects = segDirX < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                if (useShader) {
                    // 死亡演出: 尾部先烧, deathBurntSegments 覆盖到该段后 3 步内烧透
                    float death = MathHelper.Clamp((deathBurntSegments - (SegmentCount - 1 - i)) * 0.34f, 0f, 1f);
                    fx.Parameters["uSegPhase"]?.SetValue(i * 0.61f);
                    fx.Parameters["uDeath"]?.SetValue(death);
                    fx.CurrentTechnique.Passes[0].Apply();
                }

                spriteBatch.Draw(segTex, segmentPos[i] - screenPos, null, segColor,
                    segmentRot[i], origin, NPC.scale, effects, 0f);
            }

            // —— 头部主体（同批同着色器, 光效层在批外叠加）——
            {
                Vector2 headOrigin = headFrame.Size() / 2f;
                SpriteEffects effects = NPC.velocity.X < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
                Color fireTint = Color.Lerp(drawColor, AokinHelper.MoltenOrange, 0.2f);
                fireTint = Color.Lerp(fireTint, Color.White, 0.15f);

                if (useShader) {
                    float headDeath = MathHelper.Clamp((deathBurntSegments - SegmentCount + 1) * 0.5f, 0f, 0.9f);
                    fx.Parameters["uSegPhase"]?.SetValue(-0.7f);
                    fx.Parameters["uDeath"]?.SetValue(headDeath);
                    fx.CurrentTechnique.Passes[0].Apply();
                }

                spriteBatch.Draw(headTex, NPC.Center - screenPos, headFrame, fireTint * NPC.Opacity,
                    NPC.rotation, headOrigin, NPC.scale * firePulse, effects, 0f);
            }

            if (useShader) {
                spriteBatch.End();
                ACMShaders.RestoreDefaultBatch(spriteBatch);
            }

            // 身体火焰光晕层（加色, 批外）
            if (flameAuraAlpha > 0.05f) {
                for (int i = SegmentCount - 1; i >= 0; i -= 2) {
                    Texture2D segTex = (i == SegmentCount - 1) ? tailTex : bodyTex;
                    Vector2 origin = segTex.Size() / 2f;
                    float progress = (float)i / SegmentCount;
                    Color glowColor = Color.Lerp(AokinHelper.DragonFlameRed, AokinHelper.MoltenOrange, progress);
                    glowColor = Color.Lerp(glowColor, AokinHelper.SteamWhite, rageVisual * 0.6f);
                    glowColor *= flameAuraAlpha * 0.3f * firePulse;
                    glowColor.A = 0;
                    float segDirX = MathF.Cos(segmentRot[i]);
                    SpriteEffects effects = segDirX < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                    spriteBatch.Draw(segTex, segmentPos[i] - screenPos, null, glowColor,
                        segmentRot[i], origin, NPC.scale * 1.2f * firePulse, effects, 0f);
                }
            }
        }

        #endregion

        #region 头部光效层

        private void DrawHeadOverlays(SpriteBatch spriteBatch, Vector2 screenPos) {
            Texture2D headTex = TextureAssets.Npc[Type].Value;
            Rectangle rectangle = headTex.GetRectangle(((int)Main.GameUpdateCount) / 10 % 3, 3);
            Vector2 origin = rectangle.Size() / 2f;

            float firePulse = 1f + MathF.Sin(globalTime * 3f) * 0.08f;

            // 火焰光环
            DrawFireAura(spriteBatch, screenPos, headTex, origin, firePulse);

            // 速度门控的火焰拖尾（残影只在真正快的时刻出现）
            DrawFireTrail(spriteBatch, screenPos, headTex, origin);

            SpriteEffects effects = NPC.velocity.X < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // 外层发光
            Color outerGlow = AokinHelper.BlazingGold * 0.4f * firePulse;
            outerGlow = Color.Lerp(outerGlow, AokinHelper.SteamWhite * 0.5f, rageVisual * 0.7f);
            outerGlow.A = 0;
            spriteBatch.Draw(headTex, NPC.Center - screenPos, rectangle, outerGlow * NPC.Opacity,
                NPC.rotation, origin, NPC.scale * 1.15f * firePulse, effects, 0f);

            // 内部高光
            Color innerGlow = AokinHelper.PureWhite * (0.25f + rageVisual * 0.15f) * firePulse;
            innerGlow.A = 0;
            spriteBatch.Draw(headTex, NPC.Center - screenPos, rectangle, innerGlow * NPC.Opacity,
                NPC.rotation, origin, NPC.scale * 0.8f, effects, 0f);

            // 口部聚焰（龙息/熔金雨/龙炮蓄力）
            if (breathGlow > 0.05f && ACMAsset.SoftGlow != null) {
                Vector2 mouthPos = NPC.Center + NPC.rotation.ToRotationVector2() * 55f - screenPos;
                float flicker = 0.8f + MathF.Sin(globalTime * 17f) * 0.2f;
                Color mouthColor = Color.Lerp(AokinHelper.BlazingGold, AokinHelper.PureWhite, breathGlow * 0.6f);
                mouthColor *= breathGlow * flicker;
                mouthColor.A = 0;
                spriteBatch.Draw(ACMAsset.SoftGlow, mouthPos, null, mouthColor, 0f,
                    ACMAsset.SoftGlow.Size() / 2f, 0.55f + breathGlow * 0.5f, SpriteEffects.None, 0f);
            }

            // 龙眼光效
            DrawDragonEyes(spriteBatch, screenPos);
        }

        private void DrawFireAura(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D tex, Vector2 origin, float pulse) {
            if (flameAuraAlpha <= 0f) return;

            Rectangle rectangle = tex.GetRectangle(((int)Main.GameUpdateCount) / 10 % 3, 3);
            SpriteEffects effects = NPC.velocity.X < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            float baseRot = NPC.rotation;

            for (int i = 3; i >= 0; i--) {
                float layerAlpha = flameAuraAlpha * (0.15f - i * 0.03f);
                float layerScale = flameScale * (1.3f + i * 0.15f);
                float layerRot = flameRotation * (1f + i * 0.2f);

                Color layerColor = Color.Lerp(AokinHelper.DragonFlameRed, AokinHelper.MoltenOrange, i / 3f);
                layerColor = Color.Lerp(layerColor, AokinHelper.SteamWhite, rageVisual * 0.5f);
                layerColor *= layerAlpha * pulse;
                layerColor.A = 0;

                spriteBatch.Draw(tex, NPC.Center - screenPos, rectangle, layerColor * NPC.Opacity,
                    baseRot + layerRot * (i % 2 == 0 ? 1 : -1), origin, NPC.scale * layerScale, effects, 0f);
            }
        }

        /// <summary>速度门控残影：只有真正的高速时刻才出现（dressing gated by speed）。</summary>
        private void DrawFireTrail(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D tex, Vector2 origin) {
            float speed = NPC.velocity.Length();
            float speedGate = MathHelper.Clamp((speed - 12f) / 38f, 0f, 1f);
            if (speedGate <= 0.03f)
                return;

            SpriteEffects effects = NPC.velocity.X < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Rectangle rectangle = tex.GetRectangle(((int)Main.GameUpdateCount) / 10 % 3, 3);
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / NPC.oldPos.Length;
                Color trailColor = Color.Lerp(AokinHelper.MoltenOrange, AokinHelper.DragonFlameRed, 1f - progress);
                trailColor = Color.Lerp(trailColor, AokinHelper.SteamWhite, rageVisual * 0.4f);
                trailColor *= progress * (0.2f + speedGate * 0.45f);
                trailColor.A = 0;

                Vector2 pos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float trailScale = NPC.scale * (0.9f - i * 0.04f);
                float trailRot = (NPC.oldRot.Length > i ? NPC.oldRot[i] : NPC.rotation);

                spriteBatch.Draw(tex, pos, rectangle, trailColor, trailRot, origin, trailScale, effects, 0f);
            }
        }

        private void DrawDragonEyes(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (ACMAsset.SoftGlow == null) return;

            Vector2 eyeOffset = NPC.rotation.ToRotationVector2() * 35f;
            Vector2 eyePos = NPC.Center + eyeOffset - screenPos;

            float eyePulse = 0.7f + MathF.Sin(globalTime * 5f) * 0.3f;

            Color eyeColor;
            if (rageVisual > 0.3f) {
                // 狂暴: 白热龙眼
                eyeColor = Color.Lerp(AokinHelper.BlazingGold, AokinHelper.PureWhite, rageVisual);
            }
            else if (IsPhase2) {
                eyeColor = Color.Lerp(AokinHelper.DragonFlameRed, AokinHelper.BlazingGold, MathF.Sin(globalTime * 4f) * 0.5f + 0.5f);
            }
            else {
                eyeColor = AokinHelper.MoltenOrange;
            }

            eyeColor *= eyePulse * 0.8f;
            eyeColor.A = 0;

            spriteBatch.Draw(ACMAsset.SoftGlow, eyePos, null, eyeColor, 0f,
                ACMAsset.SoftGlow.Size() / 2f, 0.4f * eyePulse * glowIntensity, SpriteEffects.None, 0f);

            // 入场瞪视 / 死亡寂静的龙眼渐亮（menace is stillness）
            if (introEyeGlow > 0.03f) {
                Color stare = AokinHelper.PureWhite * introEyeGlow * (0.7f + MathF.Sin(globalTime * 3f) * 0.1f);
                stare.A = 0;
                spriteBatch.Draw(ACMAsset.SoftGlow, eyePos, null, stare, 0f,
                    ACMAsset.SoftGlow.Size() / 2f, 0.75f * introEyeGlow, SpriteEffects.None, 0f);
            }
        }

        #endregion
    }
}
