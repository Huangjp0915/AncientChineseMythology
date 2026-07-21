using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Hanbas
{
    /// <summary>
    /// 旱魃天幕 — 赤地千里的大旱穹顶 (V3 重制, 注册名与类名不变)。
    /// 层次: 焦棕→血红→炽橙距离渐变底色 + 霞光渐变条 + <b>HanbaScorchSun 着色器焦日</b>
    /// (屏幕相对定位, 分辨率自适应; 着色器缺失时回退旧贴图日)。
    /// 焦日爆发度/灰蚀度由 Boss 经 <see cref="PublishSunState"/> 联动 (蚀日增辉 / 死亡熄灭退红)。
    /// </summary>
    [VaultLoaden("AncientChineseMythology/Textures/Backgrounds/")]
    internal class HanbaSky : CustomSky
    {
        private bool active;
        private float intensity;
        private const float MaxIntensity = 0.6f;
        private Color skyColor;
        private float driftPhase;
        internal static string name;
        internal static Asset<Texture2D> HanbaSkySun;
        internal static Asset<Texture2D> HanbaSkyColorBar;

        // —— Boss → 天幕联动 (纯本地视觉, 每帧发布制) ——
        private static float _sunFlareTarget;  // 焦日爆发度 0~1 (蚀日/坠日拉高)
        private static float _sunAshTarget;    // 焦日灰蚀度 0~1 (死亡演出熄灭)
        private static ulong _lastPublishFrame;
        private float sunFlare;
        private float sunAsh;

        public static void LoadInstance() {
            name = "AncientChineseMythology:HanbaSky";
            SkyManager.Instance[name] = new HanbaSky();
        }

        /// <summary>由 Boss 每帧调用, 发布焦日状态 (爆发度/灰蚀度)。</summary>
        public static void PublishSunState(float flare, float ash) {
            _sunFlareTarget = MathHelper.Clamp(flare, 0f, 1f);
            _sunAshTarget = MathHelper.Clamp(ash, 0f, 1f);
            _lastPublishFrame = Main.GameUpdateCount;
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            // 仅最远背景深度层绘制一次 (防同帧重复绘制闪烁)
            if (!(maxDepth >= 0 && minDepth < 0) || intensity <= 0.01f)
                return;

            // 低频漂移 (替换旧版每帧 rand 抖动): 热浪蒸腾的缓慢摇曳
            Vector2 drift = new(MathF.Sin(driftPhase) * 3f * intensity, MathF.Cos(driftPhase * 0.7f) * 2f * intensity);

            // 底色层 (距离驱动的焦棕→炽橙)
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle((int)drift.X, (int)drift.Y, Main.screenWidth, Main.screenHeight), skyColor * intensity);

            // 霞光渐变条
            if (HanbaSkyColorBar?.Value != null) {
                spriteBatch.Draw(HanbaSkyColorBar.Value,
                    new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White * intensity);
            }

            DrawScorchSun(spriteBatch);
        }

        // 焦日: 屏幕相对定位 (0.74w, 0.16h), 着色器日轮; 缺着色器回退贴图日
        private void DrawScorchSun(SpriteBatch spriteBatch) {
            Vector2 sunUV = new(0.74f, 0.16f);
            float fade = intensity / MaxIntensity;

            Effect fx = MythologyConfig.FullscreenShadersEnabled ? HanbaVFX.ScorchSun : null;
            Texture2D noise = fx != null ? ACMShaders.NoiseTexture : null;

            if (fx != null && noise != null) {
                HanbaVFX.SetSunParams(fx, sunUV, 0.11f + sunFlare * 0.05f, fade, sunFlare, sunAsh);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);
                spriteBatch.Draw(noise, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
                spriteBatch.End();
                // 恢复天幕默认批 (与 AncestralDragonSky 同约定)
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            }
            else if (HanbaSkySun?.Value != null) {
                Vector2 sunPos = new(Main.screenWidth * sunUV.X, Main.screenHeight * sunUV.Y);
                Color sunColor = new Color(255, 180, 100, 0) * fade * (1.2f + sunFlare * 0.8f) * (1f - sunAsh * 0.7f);
                spriteBatch.Draw(HanbaSkySun.Value, sunPos, null, sunColor, 0f,
                    HanbaSkySun.Size() / 2f, 1.5f + sunFlare * 0.5f, SpriteEffects.None, 0f);
            }
        }

        public override bool IsActive() {
            return active;
        }

        public override void Reset() {
            active = false;
            intensity = 0.01f;
        }

        public override void Update(GameTime gameTime) {
            driftPhase += 0.006f;

            // 发布过期 (Boss 消失/死亡收尾) → 焦日目标回落
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _sunFlareTarget = 0f;
            }
            sunFlare = MathHelper.Lerp(sunFlare, _sunFlareTarget, 0.05f);
            sunAsh = MathHelper.Lerp(sunAsh, _sunAshTarget, 0.08f);

            if (NPC.AnyNPCs(ModContent.NPCType<Hanba>())) {
                NPC boss = null;
                foreach (var npc in Main.ActiveNPCs) {
                    if (npc.type == ModContent.NPCType<Hanba>()) {
                        boss = npc;
                        break;
                    }
                }

                if (boss != null) {
                    float distance = Main.LocalPlayer.Distance(boss.Center);
                    float t = MathHelper.Clamp(distance / 1600f, 0f, 1f); //越近越暗红
                    skyColor = VaultUtils.MultiStepColorLerp(t,
                        new Color(100, 30, 0),    //焦棕
                        new Color(140, 20, 20),   //血红
                        new Color(255, 80, 0));   //炽橙

                    // 死亡演出中 (灰蚀拉满) 天幕加速退红 — "旱魃死, 天将雨"
                    float target = _sunAshTarget > 0.5f ? 0.12f : MaxIntensity;
                    if (intensity < target)
                        intensity = MathF.Min(intensity + 0.012f, target);
                    else
                        intensity = MathF.Max(intensity - 0.02f, target);

                    active = true;
                }
            }
            else {
                _sunAshTarget = 0f;
                intensity -= 0.01f;
                if (intensity <= 0f) {
                    intensity = 0f;
                    Deactivate();
                }
            }
        }

        public override Color OnTileColor(Color inColor) {
            // 旧版 1-intensity 遮蔽过黑; 收敛为暖色黄昏压光 (≤42%)
            Color dusk = new(255, 150, 90);
            Color tinted = Color.Lerp(inColor, inColor.MultiplyRGB(dusk), intensity * 0.8f);
            return tinted * (1f - intensity * 0.42f);
        }
    }
}
