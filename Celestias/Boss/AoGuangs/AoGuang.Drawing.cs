using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    internal partial class AoGuang
    {
        #region 绘制

        /// <summary>fake-Z 折算的显示缩放 (Z 越大越远越小)。</summary>
        private float ZScale => 1f / (visualZ + 1f);

        /// <summary>fake-Z 折算的显示透明度。</summary>
        private float ZOpacity => MathHelper.Clamp(1f / (visualZ * 0.55f + 1f), 0.2f, 1f);

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            float waterPulse = 1f + MathF.Sin(globalTime * 3f) * 0.06f;
            float zScale = ZScale;
            float zOpacity = ZOpacity;

            // 龙躯水流 ribbon (顶点带 + 专属着色器, 画在本体之下)
            DrawSerpentBody(spriteBatch, screenPos);

            // 戟落冲击环 (纯视觉事件)
            if (tidalRingVisual > 0.01f) {
                float ringT = 1f - tidalRingVisual;
                AoGuangHelper.DrawTidalWave(spriteBatch, NPC.Center, 90f + ringT * 620f, tidalRingVisual * 0.9f);
            }

            // 速度门控残影: 只有穿刺时刻才可见 (dressing 常开 = 噪声)
            if (pierceGlow > 0.2f) {
                SpriteEffects fx0 = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
                for (int i = 2; i < NPC.oldPos.Length && i < 12; i += 2) {
                    if (NPC.oldPos[i] == Vector2.Zero) continue;
                    float prog = 1f - i / 12f;
                    Color ghost = AoGuangHelper.OceanTeal * (pierceGlow * prog * 0.4f);
                    ghost.A = 0;
                    Vector2 gp = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                    float gr = NPC.oldRot.Length > i ? NPC.oldRot[i] : NPC.rotation;
                    spriteBatch.Draw(tex, gp, null, ghost, gr, origin, NPC.scale * zScale * (0.92f - i * 0.02f), fx0, 0f);
                }
            }

            // 水光晕
            DrawWaterAura(spriteBatch, screenPos, tex, origin, waterPulse, zScale, zOpacity);

            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            Vector2 drawPos = NPC.Center - screenPos;

            // 主体色: 水蓝浸染
            Color waterTint = Color.Lerp(drawColor, AoGuangHelper.DragonBlue, 0.4f);
            waterTint = Color.Lerp(waterTint, Color.White, 0.2f);

            if (dissolveProgress > 0.01f) {
                // 死亡溶解: 共享 DissolveBurn 单 pass, 从尾向头吃掉躯体
                DrawDissolvingSprite(spriteBatch, tex, drawPos, origin, waterTint, effects, zScale);
            }
            else {
                // 外层发光
                Color outerGlow = AoGuangHelper.WaterGlow * 0.4f * waterPulse * zOpacity;
                outerGlow.A = 0;
                spriteBatch.Draw(tex, drawPos, null, outerGlow,
                    NPC.rotation, origin, NPC.scale * zScale * 1.12f * waterPulse, effects, 0f);

                // 主体
                spriteBatch.Draw(tex, drawPos, null, waterTint * NPC.Opacity * zOpacity,
                    NPC.rotation, origin, NPC.scale * zScale * waterPulse, effects, 0f);

                // 内层高光
                Color innerGlow = AoGuangHelper.PureWhite * 0.28f * waterPulse * zOpacity;
                innerGlow.A = 0;
                spriteBatch.Draw(tex, drawPos, null, innerGlow,
                    NPC.rotation, origin, NPC.scale * zScale * 0.8f, effects, 0f);

                // 龙眼光效 (三阶段泛红)
                DrawDragonEyes(spriteBatch, screenPos, zScale, zOpacity);
            }

            return false;
        }

        /// <summary>
        /// 死亡溶解绘制: 共享 DissolveBurn (s0=贴图, s1=共享噪声), 灼烧边为水光青。
        /// </summary>
        private void DrawDissolvingSprite(SpriteBatch sb, Texture2D tex, Vector2 drawPos, Vector2 origin,
            Color tint, SpriteEffects effects, float zScale) {
            Effect fx = ACMShaders.DissolveBurn;
            if (fx == null) {
                sb.Draw(tex, drawPos, null, tint * NPC.Opacity * (1f - dissolveProgress), NPC.rotation, origin,
                    NPC.scale * zScale, effects, 0f);
                return;
            }

            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uIntensity"]?.SetValue(1f);
            fx.Parameters["uThreshold"]?.SetValue(MathHelper.Clamp(dissolveProgress, 0f, 1f));
            fx.Parameters["uEdgeWidth"]?.SetValue(0.09f);
            fx.Parameters["uNoiseScale"]?.SetValue(2.6f);
            fx.Parameters["uEdgeColor"]?.SetValue(new Vector4(AoGuangHelper.WaterGlow.ToVector3(), 0.9f));
            fx.Parameters["uDirection"]?.SetValue(new Vector2(NPC.spriteDirection == 1 ? -0.6f : 0.6f, 0.2f));
            fx.Parameters["uSweepStrength"]?.SetValue(0.5f);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = ACMShaders.NoiseTexture;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(tex, drawPos, null, tint, NPC.rotation, origin, NPC.scale * zScale, effects, 0f);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 龙躯水流 ribbon: 以 oldPos 轨迹为脊柱的 TriangleStrip 水带 (AoGuangWaterSerpent 着色器)。
        /// 高速时 34 帧轨迹拉出 ~1500px 的长龙, 低速时盘卷在本体周围 — 龙的身体就是它游过的水。
        /// </summary>
        private void DrawSerpentBody(SpriteBatch sb, Vector2 screenPos) {
            if (Main.dedServ || dissolveProgress >= 0.98f)
                return;
            Effect fx = AoGuangHelper.WaterSerpentEffect;
            if (fx == null)
                return;

            // 组 spine: 龙首前伸一点 + 历史轨迹
            int maxPts = 34;
            Span<Vector2> raw = stackalloc Vector2[maxPts + 1];
            int n = 0;
            raw[n++] = NPC.Center + NPC.rotation.ToRotationVector2() * 46f * ZScale - screenPos;
            for (int i = 0; i < NPC.oldPos.Length && n <= maxPts; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;
                Vector2 p = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                // 相邻点重叠时跳过 (悬停态 oldPos 会挤成一团)
                if (n > 0 && Vector2.DistanceSquared(raw[n - 1], p) < 9f) continue;
                raw[n++] = p;
            }
            if (n < 3)
                return;

            var posArr = new Vector2[n];
            raw[..n].CopyTo(posArr);

            float glow = pierceGlow;
            float bodyAlpha = (0.55f + glow * 0.45f) * ZOpacity * (1f - dissolveProgress);
            float zScale = ZScale;
            int subdivisions = MythologyConfig.Trail == TrailQualityLevel.High ? 2 : 1;
            if (MythologyConfig.Trail == TrailQualityLevel.Off)
                return;

            var verts = ACMUtils.BuildRibbonStrip(
                posArr,
                p => MathHelper.Lerp(30f, 4f, p) * (0.85f + glow * 0.5f) * zScale,
                p => {
                    Color c = Color.White * ((1f - p * 0.75f) * bodyAlpha);
                    c.A = (byte)(c.R); // a 通道作衰减权重 (着色器 vert.a)
                    return c;
                },
                uvScroll: globalTime * 0.5f,
                subdivisions: subdivisions);
            if (verts.Length < 4)
                return;

            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uGlow"]?.SetValue(glow);
            fx.Parameters["uFlowSpeed"]?.SetValue(1.1f + glow * 1.1f);
            fx.Parameters["uFoamWidth"]?.SetValue(0.3f);
            fx.Parameters["uDeepColor"]?.SetValue(new Vector4(AoGuangHelper.DeepSeaBlue.ToVector3(), 1f));
            fx.Parameters["uCoreColor"]?.SetValue(new Vector4(AoGuangHelper.OceanTeal.ToVector3(), 1f));
            fx.Parameters["uFoamColor"]?.SetValue(new Vector4(AoGuangHelper.FoamWhite.ToVector3(), 1f));

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[0] = ACMShaders.NoiseTexture;
            gd.SamplerStates[0] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        private void DrawWaterAura(SpriteBatch spriteBatch, Vector2 screenPos, Texture2D tex, Vector2 origin,
            float pulse, float zScale, float zOpacity) {
            if (waterAuraAlpha <= 0f || dissolveProgress > 0.5f) return;

            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float breathScale = 1f + MathF.Sin(globalTime * 2.2f) * 0.08f;

            for (int i = 3; i >= 0; i--) {
                float layerAlpha = waterAuraAlpha * (0.13f - i * 0.026f) * zOpacity;
                float layerScale = breathScale * (1.28f + i * 0.14f) * zScale;
                float layerRot = globalTime * 0.45f * (1f + i * 0.2f);

                Color layerColor = Color.Lerp(AoGuangHelper.DragonBlue, AoGuangHelper.OceanTeal, i / 3f);
                layerColor *= layerAlpha * pulse;
                layerColor.A = 0;

                spriteBatch.Draw(tex, NPC.Center - screenPos, null, layerColor,
                    NPC.rotation + layerRot * (i % 2 == 0 ? 1 : -1), origin, NPC.scale * layerScale, effects, 0f);
            }
        }

        private void DrawDragonEyes(SpriteBatch spriteBatch, Vector2 screenPos, float zScale, float zOpacity) {
            if (ACMAsset.LightShot == null) return;

            Vector2 eyeOffset = NPC.rotation.ToRotationVector2() * 35f * zScale;
            Vector2 eyePos = NPC.Center + eyeOffset - screenPos;

            float eyePulse = 0.7f + MathF.Sin(globalTime * 5f) * 0.3f;

            // 阶段眼色: P1 龙王蓝 → P2 水光白 → P3 泛红 (eyeRedLerp 由相变二脚本推满)
            Color eyeColor = IsPhase2 ? AoGuangHelper.WaterGlow : AoGuangHelper.DragonBlue;
            eyeColor = Color.Lerp(eyeColor, new Color(255, 70, 60), eyeRedLerp * 0.65f);

            eyeColor *= eyePulse * 0.8f * zOpacity;
            eyeColor.A = 0;

            spriteBatch.Draw(ACMAsset.LightShot, eyePos, null, eyeColor, 0f,
                ACMAsset.LightShot.Size() / 2f, 0.5f * eyePulse * glowIntensity * zScale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 「沧海沉浸」全屏后处理 (AoGuangAbyssalSea 专属着色器): 折射 + 水位线 + 向心吸入 + impact frame。
        /// 走 <see cref="ACMShaders.RequestFullscreenSlot"/> 名额契约 (每帧 ≤1), 强度全零时早退。
        /// </summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;
            bool anyActive = submersionWarp > 0.01f || waterLevel > 0.01f ||
                             impactFrame > 0.01f || vortexInward > 0.02f;
            if (!anyActive)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = AoGuangHelper.AbyssalSeaEffect;
            if (fx == null)
                return;

            // 折射/吸入中心: 深渊漩涡时锚向涡心 (玩家附近), 其余锚向龙王
            Vector2 warpCenter = NPC.Center;
            if (vortexInward > 0.05f) {
                Player t = Main.player[NPC.target];
                if (t.active && !t.dead)
                    warpCenter = Vector2.Lerp(NPC.Center, chargeTarget, vortexInward);
            }

            Vector2 centerUV = (warpCenter - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;

            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(submersionWarp, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(0.95f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uRadialPull"]?.SetValue(vortexInward * 0.85f);
            fx.Parameters["uWaterLevel"]?.SetValue(MathHelper.Clamp(waterLevel, 0f, 0.9f));
            fx.Parameters["uImpact"]?.SetValue(MathHelper.Clamp(impactFrame, 0f, 1f));
            fx.Parameters["uTint"]?.SetValue(new Vector4(AoGuangHelper.OceanTeal.ToVector3(), 0.45f));

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        #endregion

        #region 死亡落定

        public override void OnKill() {
            // 标记击败 (进度不可回退)
            Systems.DownedBossSystem.downedAoGuang = true;

            // 演出已在 Death 状态播完, 这里只留最后的泡沫余韵
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 40; i++) {
                    float angle = MathHelper.TwoPi * i / 40f;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);
                    int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BubbleBlock;
                    Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, dustType, vel.X, vel.Y, 120, default, 2f);
                    d.noGravity = true;
                }
            }

            Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath62 with { Volume = 1.2f, Pitch = -0.3f }, NPC.Center);
        }

        #endregion
    }
}
