using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Spectres
{
    /// <summary>
    /// 怨灵天空效果 - 青黄色调的幽暗天空
    /// </summary>
    public class SpectreSky : CustomSky
    {
        private bool active;
        private float intensity;
        private float maxIntensity = 0.75f;
        private Color skyColor;

        internal static string name;

        public static void LoadInstance() {
            name = "AncientChineseMythology:SpectreSky";
            SkyManager.Instance[name] = new SpectreSky();
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Reset() {
            active = false;
            intensity = 0.01f;
        }

        public override bool IsActive() => active;

        public override void Update(GameTime gameTime) {
            if (NPC.AnyNPCs(ModContent.NPCType<Spectre>())) {
                NPC boss = null;
                foreach (var npc in Main.ActiveNPCs) {
                    if (npc.type == ModContent.NPCType<Spectre>()) {
                        boss = npc;
                        break;
                    }
                }

                if (boss != null) {
                    float distance = Main.LocalPlayer.Distance(boss.Center);
                    float t = MathHelper.Clamp(distance / 1400f, 0f, 1f);

                    // 怨灵风格多重色阶：深青 -> 青黄混合 -> 暗金
                    skyColor = VaultUtils.MultiStepColorLerp(t,
                        new Color(15, 35, 35),   // 深暗青（最压迫）
                        new Color(30, 70, 65),   // 深青绿
                        new Color(60, 90, 70),   // 青黄过渡
                        new Color(80, 85, 50));  // 暗金黄（远处）

                    if (intensity < maxIntensity)
                        intensity += 0.012f;

                    // 阶段2和阶段3时增强效果
                    float lifePercent = (float)boss.life / boss.lifeMax;
                    if (lifePercent < 0.5f) {
                        maxIntensity = 0.85f;
                        // 添加红色调
                        if (lifePercent < 0.25f) {
                            skyColor = Color.Lerp(skyColor, new Color(60, 30, 30), 0.2f);
                        }
                    }

                    active = true;
                }
            }
            else {
                intensity -= 0.015f;
                if (intensity <= 0f) {
                    intensity = 0f;
                    Deactivate();
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (maxDepth >= 0 && minDepth < 0) {
                // 计算轻微震颤
                Vector2 shake = Main.rand.NextVector2Circular(1.2f * intensity, 1.2f * intensity);

                // 绘制天空背景
                spriteBatch.Draw(
                    TextureAssets.MagicPixel.Value,
                    new Rectangle(
                        (int)shake.X,
                        (int)shake.Y,
                        Main.screenWidth,
                        Main.screenHeight
                    ),
                    skyColor * intensity
                );

                // 绘制氛围粒子效果
                DrawAtmosphereParticles(spriteBatch);
            }
        }

        private void DrawAtmosphereParticles(SpriteBatch spriteBatch) {
            if (intensity < 0.3f) return;

            // 漂浮的幽魂粒子
            float time = Main.GlobalTimeWrappedHourly;
            int particleCount = (int)(15 * intensity);

            for (int i = 0; i < particleCount; i++) {
                // 基于索引和时间的伪随机位置
                float seed = i * 137.5f;
                float x = ((seed + time * 20f) % Main.screenWidth);
                float y = ((seed * 2.3f + time * 15f) % Main.screenHeight);

                // 粒子大小和透明度的波动
                float pulse = MathF.Sin(time * 2f + seed * 0.1f) * 0.3f + 0.7f;
                float alpha = pulse * intensity * 0.3f;

                // 交替青色和黄色
                Color particleColor = i % 2 == 0 ? SpectreHelper.SpectreCyan : SpectreHelper.SpectreYellow;
                particleColor *= alpha;
                particleColor.A = 0;

                // 绘制粒子
                Vector2 pos = new Vector2(x, y);
                float size = 2f + pulse * 2f;

                spriteBatch.Draw(
                    TextureAssets.MagicPixel.Value,
                    new Rectangle((int)pos.X, (int)pos.Y, (int)size, (int)size),
                    particleColor
                );
            }
        }

        public override Color OnTileColor(Color inColor) {
            // 给地形添加青黄色调
            Color tint = Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, 0.3f);
            return Color.Lerp(inColor, tint, intensity * 0.4f);
        }

        public override float GetCloudAlpha() {
            return 1f - intensity * 0.7f;
        }
    }

    /// <summary>
    /// 加载怨灵天空效果
    /// </summary>
    public class SpectreSkyLoader : ModSystem
    {
        public override void Load() {
            if (Main.dedServ) return;
            SpectreSky.LoadInstance();
        }
    }

    /// <summary>
    /// 怨灵Boss战斗管理
    /// </summary>
    public class SpectreBossSystem : ModSystem
    {
        public override void PostUpdateNPCs() {
            // 当怨灵Boss存在时激活天空效果
            if (NPC.AnyNPCs(ModContent.NPCType<Spectre>())) {
                if (!SkyManager.Instance[SpectreSky.name].IsActive()) {
                    SkyManager.Instance.Activate(SpectreSky.name);
                }
            }
            else {
                if (SkyManager.Instance[SpectreSky.name].IsActive()) {
                    SkyManager.Instance.Deactivate(SpectreSky.name);
                }
            }
        }
    }
}
