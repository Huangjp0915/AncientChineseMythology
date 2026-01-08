using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls
{
    internal class AncestralDragonEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(ModContent.NPCType<AncestralDragonSoulHead>());
        public override void SpecialVisuals(Player player, bool isActive) {
            if (player.Alives()) {
                player.ManageSpecialBiomeVisuals(AncestralDragonSky.SkyName, isActive);
            }
        }
    }

    /// <summary>
    /// 祖龙残魂天空效果 - 迷幻仙气的天空背景
    /// 白色雾气弥漫，空灵飘渺的视觉效果
    /// 使用自动检测Boss存在的方式管理激活/停用
    /// </summary>
    public class AncestralDragonSky : CustomSky, IACMLoader
    {
        public const string SkyName = "ACM:AncestralDragonSky";

        private bool active;
        private float intensity;
        private float globalTime;

        private const float MaxIntensity = 1f;
        private const float FadeInSpeed = 0.015f;
        private const float FadeOutSpeed = 0.02f;

        // 雾气层参数
        private const int MistLayerCount = 4;
        private float[] mistOffsets;
        private readonly float[] mistSpeeds = [0.02f, 0.015f, 0.025f, 0.01f];
        private readonly float[] mistScales = [1.5f, 2f, 1.2f, 2.5f];

        // 龙魂轨迹
        private Vector2[] soulTrailPoints;
        private int trailIndex = 0;

        // Boss状态缓存
        private float bossHealthPercent = 1f;

        void IACMLoader.LoadData() {
            SkyManager.Instance[SkyName] = this;
            Filters.Scene[SkyName] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.0f, 0.0f, 0.0f)
                .UseOpacity(0.5f), EffectPriority.High);
            mistOffsets = new float[MistLayerCount];
            soulTrailPoints = new Vector2[20];
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;

            // 初始化雾气偏移
            mistOffsets ??= new float[MistLayerCount];
            for (int i = 0; i < MistLayerCount; i++) {
                mistOffsets[i] = Main.rand.NextFloat(1000f);
            }

            // 初始化龙魂轨迹
            soulTrailPoints ??= new Vector2[20];
            for (int i = 0; i < soulTrailPoints.Length; i++) {
                soulTrailPoints[i] = Vector2.Zero;
            }
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override bool IsActive() => active || intensity > 0.01f;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            globalTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

            // 自动检测Boss是否存在
            NPC boss = FindBoss();
            bool shouldBeActive = boss != null && boss.active;

            if (shouldBeActive) {
                // Boss存在时激活
                if (!active) {
                    Activate(Vector2.Zero);
                }

                // 缓存Boss血量比例（用于视觉效果强度）
                bossHealthPercent = (float)boss.life / boss.lifeMax;

                // 根据Boss阶段调整目标强度
                float targetIntensity = MaxIntensity;
                if (bossHealthPercent < 0.3f) {
                    targetIntensity = MaxIntensity * 1.2f; // 三阶段更强烈
                }
                else if (bossHealthPercent < 0.6f) {
                    targetIntensity = MaxIntensity * 1.1f; // 二阶段稍强
                }

                // 平滑渐变到目标强度
                intensity = MathHelper.Lerp(intensity, targetIntensity, FadeInSpeed);
            }
            else {
                // Boss不存在时平滑淡出
                intensity -= FadeOutSpeed;
                if (intensity <= 0f) {
                    intensity = 0f;
                    if (active) {
                        Deactivate();
                    }
                }
            }

            // 更新雾气偏移
            if (mistOffsets != null) {
                for (int i = 0; i < MistLayerCount; i++) {
                    mistOffsets[i] += mistSpeeds[i];
                }
            }

            // 更新龙魂轨迹
            UpdateDragonSoulTrail();
        }

        private void UpdateDragonSoulTrail() {
            if (soulTrailPoints == null) return;

            // 寻找祖龙头部
            NPC boss = FindBoss();
            if (boss != null && boss.active) {
                // 记录位置
                soulTrailPoints[trailIndex] = boss.Center;
                trailIndex = (trailIndex + 1) % soulTrailPoints.Length;
            }
        }

        /// <summary>
        /// 查找祖龙残魂Boss
        /// </summary>
        private static NPC FindBoss() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<AncestralDragonSoulHead>() && npc.active) {
                    return npc;
                }
            }
            return null;
        }

        /// <summary>
        /// 检查Boss是否存在（供外部使用）
        /// </summary>
        public static bool IsBossActive() => FindBoss() != null;

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (maxDepth >= 0 && minDepth < 0 && intensity > 0.01f) {
                // 绘制迷幻天空背景
                DrawMysticalBackground(spriteBatch);

                // 绘制流动雾气
                DrawFlowingMist(spriteBatch);

                // 绘制龙魂轨迹光效
                DrawSoulTrail(spriteBatch);

                // 绘制星辰光点
                DrawEtherealStars(spriteBatch);

                // 绘制边缘晕影
                DrawVignette(spriteBatch);
            }
        }

        private void DrawMysticalBackground(SpriteBatch spriteBatch) {
            // 渐变背景 - 从深灰蓝到淡青白
            Rectangle screenRect = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);

            // 使用基础纹理创建渐变效果
            Texture2D pixel = ACMAsset.BlankStar ?? Main.Assets.Request<Texture2D>("Images/MagicPixel").Value;

            // 底层 - 深色
            Color bottomColor = new Color(30, 35, 50) * intensity * 0.8f;
            spriteBatch.Draw(pixel, screenRect, null, bottomColor, 0f, Vector2.Zero, SpriteEffects.None, 0f);

            // 上层渐变 - 更亮的青白色向顶部渐变
            for (int i = 0; i < 10; i++) {
                float t = i / 10f;
                int height = Main.screenHeight / 10;
                Rectangle layerRect = new Rectangle(0, i * height, Main.screenWidth, height);

                Color layerColor = Color.Lerp(
                    new Color(40, 50, 70),
                    new Color(80, 100, 130),
                    1f - t
                ) * intensity * 0.5f;

                spriteBatch.Draw(pixel, layerRect, null, layerColor, 0f, Vector2.Zero, SpriteEffects.None, 0f);
            }
        }

        private void DrawFlowingMist(SpriteBatch spriteBatch) {
            Texture2D mistTex = ACMAsset.GlaciateWave;
            if (mistTex == null) return;

            Vector2 screenCenter = new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);

            for (int layer = 0; layer < MistLayerCount; layer++) {
                float layerDepth = 0.5f + layer * 0.15f;
                float alpha = (0.15f - layer * 0.025f) * intensity;

                // 层级颜色变化
                Color mistColor = layer switch {
                    0 => new Color(255, 255, 255),
                    1 => new Color(220, 235, 255),
                    2 => new Color(200, 220, 245),
                    _ => new Color(180, 200, 230)
                };
                mistColor *= alpha;
                mistColor.A = 0;

                // 多条雾气带
                for (int band = 0; band < 3; band++) {
                    float xOffset = mistOffsets[layer] * 100f + band * 400f;
                    float yOffset = MathF.Sin(globalTime * 0.5f + layer + band) * 50f;

                    Vector2 position = new Vector2(
                        (xOffset % (Main.screenWidth + 500)) - 250,
                        Main.screenHeight * 0.3f + band * Main.screenHeight * 0.25f + yOffset
                    );

                    float rotation = MathF.Sin(globalTime * 0.3f + layer + band) * 0.1f;
                    float scale = mistScales[layer] + MathF.Sin(globalTime + layer) * 0.2f;

                    Vector2 drawScale = new Vector2(
                        Main.screenWidth * 0.8f / mistTex.Width * scale,
                        0.3f * scale
                    );

                    spriteBatch.Draw(
                        mistTex,
                        position,
                        null,
                        mistColor,
                        rotation,
                        new Vector2(mistTex.Width / 2f, mistTex.Height / 2f),
                        drawScale,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        private void DrawSoulTrail(SpriteBatch spriteBatch) {
            if (ACMAsset.LightShot == null) return;

            Texture2D lightTex = ACMAsset.LightShot;
            Vector2 origin = lightTex.Size() / 2f;

            for (int i = 0; i < soulTrailPoints.Length; i++) {
                if (soulTrailPoints[i] == Vector2.Zero) continue;

                int index = (trailIndex - i - 1 + soulTrailPoints.Length) % soulTrailPoints.Length;
                Vector2 worldPos = soulTrailPoints[index];
                Vector2 screenPos = worldPos - Main.screenPosition;

                // 只绘制屏幕内的
                if (screenPos.X < -100 || screenPos.X > Main.screenWidth + 100 ||
                    screenPos.Y < -100 || screenPos.Y > Main.screenHeight + 100) continue;

                float progress = 1f - (float)i / soulTrailPoints.Length;
                float alpha = progress * 0.2f * intensity;

                Color trailColor = Color.Lerp(new Color(255, 255, 255), new Color(180, 210, 255), 1f - progress);
                trailColor *= alpha;
                trailColor.A = 0;

                float scale = 0.3f * progress + MathF.Sin(globalTime * 3f + i * 0.5f) * 0.05f;

                spriteBatch.Draw(lightTex, screenPos, null, trailColor, 0f, origin, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawEtherealStars(SpriteBatch spriteBatch) {
            Texture2D starTex = ACMAsset.BlankStar;
            if (starTex == null) return;

            Vector2 origin = starTex.Size() / 2f;

            // 绘制一些飘动的光点
            int starCount = 30;
            for (int i = 0; i < starCount; i++) {
                // 使用固定种子生成伪随机位置
                float seed = i * 1234.567f;
                float x = ((seed * 7.89f) % 1f) * Main.screenWidth;
                float y = ((seed * 3.45f) % 1f) * Main.screenHeight * 0.7f;

                // 添加动态偏移
                float offsetX = MathF.Sin(globalTime * 0.5f + seed) * 20f;
                float offsetY = MathF.Sin(globalTime * 0.3f + seed * 1.5f) * 15f;

                Vector2 position = new Vector2(x + offsetX, y + offsetY);

                // 闪烁效果
                float twinkle = (MathF.Sin(globalTime * 2f + seed * 2f) + 1f) * 0.5f;
                float alpha = (0.3f + twinkle * 0.4f) * intensity;

                Color starColor = new Color(220, 235, 255) * alpha;
                starColor.A = 0;

                float scale = 0.15f + twinkle * 0.1f;

                spriteBatch.Draw(starTex, position, null, starColor, globalTime + seed, origin, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawVignette(SpriteBatch spriteBatch) {
            // 边缘晕影效果，增加迷幻感
            Texture2D pixel = ACMAsset.LightShot;
            if (pixel == null) return;

            Vector2 screenCenter = new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);

            // 四角暗化
            Color vignetteColor = new Color(20, 25, 40) * intensity * 0.4f;

            float cornerSize = MathF.Min(Main.screenWidth, Main.screenHeight) * 0.4f;
            Vector2 origin = pixel.Size() / 2f;

            // 左上角
            spriteBatch.Draw(pixel, new Vector2(0, 0), null, vignetteColor, 0f, origin, cornerSize / pixel.Width, SpriteEffects.None, 0f);
            // 右上角
            spriteBatch.Draw(pixel, new Vector2(Main.screenWidth, 0), null, vignetteColor, 0f, origin, cornerSize / pixel.Width, SpriteEffects.None, 0f);
            // 左下角
            spriteBatch.Draw(pixel, new Vector2(0, Main.screenHeight), null, vignetteColor, 0f, origin, cornerSize / pixel.Width, SpriteEffects.None, 0f);
            // 右下角
            spriteBatch.Draw(pixel, new Vector2(Main.screenWidth, Main.screenHeight), null, vignetteColor, 0f, origin, cornerSize / pixel.Width, SpriteEffects.None, 0f);
        }

        public override Color OnTileColor(Color inColor) {
            // 使场景整体偏冷色调
            Color tintColor = Color.Lerp(Color.White, new Color(200, 210, 230), intensity * 0.3f);
            return new Color(
                (int)(inColor.R * tintColor.R / 255f),
                (int)(inColor.G * tintColor.G / 255f),
                (int)(inColor.B * tintColor.B / 255f),
                inColor.A
            );
        }

        public override float GetCloudAlpha() {
            // 减少原版云层
            return 1f - intensity * 0.8f;
        }
    }
}
