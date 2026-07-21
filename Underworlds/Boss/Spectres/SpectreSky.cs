using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Spectres
{
    /// <summary>
    /// 怨灵天空 (V3) — 由 Boss AI 每帧<b>发布</b>战斗状态驱动 (阶段/怨念/死亡进度/入场压暗),
    /// 不再自行扫描 NPC 数组。发布过期 (Boss 消失) 自动淡出。
    /// 与地府共享环境 <see cref="UnderworldFogSky"/> 协同：本天空只做压暗染色 + 上飘魂点视差,
    /// 不重复雾层职责。
    /// </summary>
    public class SpectreSky : CustomSky
    {
        private bool active;
        private float intensity;
        private Color skyColor;

        internal static string name;

        // —— 战斗状态发布 (客户端纯视觉; Boss 离场 = 发布断流 → 淡出) ——
        private static ulong s_publishFrame;
        private static Vector2 s_center;
        private static int s_phaseLevel = 1;
        private static float s_grudge;
        private static float s_deathProgress;
        private static float s_introDark = 1f;

        /// <summary>发布是否新鲜 (2 帧内)。</summary>
        public static bool PublishFresh =>
            Main.GameUpdateCount >= s_publishFrame && Main.GameUpdateCount - s_publishFrame <= 2;

        /// <summary>
        /// Boss AI 每帧 (客户端) 调用发布战斗状态。
        /// </summary>
        /// <param name="center">Boss 世界坐标。</param>
        /// <param name="phaseLevel">阶段 1/2/3。</param>
        /// <param name="grudge">怨念账归一化 0~1 (天色褪向纸钱黄)。</param>
        /// <param name="deathProgress">死亡演出进度 0~1 (收束发暗 → 大爆后回暖)。</param>
        /// <param name="introDark">入场压暗爬升 0~1 (战斗开场快速入夜)。</param>
        public static void Publish(Vector2 center, int phaseLevel, float grudge, float deathProgress, float introDark) {
            s_publishFrame = Main.GameUpdateCount;
            s_center = center;
            s_phaseLevel = phaseLevel;
            s_grudge = grudge;
            s_deathProgress = deathProgress;
            s_introDark = introDark;
        }

        public static void LoadInstance() {
            name = "AncientChineseMythology:SpectreSky";
            SkyManager.Instance[name] = new SpectreSky();
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = Math.Max(intensity, 0.01f);
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override bool IsActive() => active || intensity > 0f;

        public override void Update(GameTime gameTime) {
            if (PublishFresh) {
                active = true;

                // 入场 60f 内快速压暗 (introDark 0→1), 阶段越深上限越高
                float maxIntensity = s_phaseLevel >= 3 ? 0.85f : (s_phaseLevel == 2 ? 0.8f : 0.75f);
                maxIntensity *= MathHelper.Lerp(0.35f, 1f, s_introDark);
                intensity = Math.Min(intensity + 0.015f, maxIntensity);

                // 距离色阶: 深暗青 (压迫) → 暗金黄 (远处)
                float distT = MathHelper.Clamp(Main.LocalPlayer.Distance(s_center) / 1400f, 0f, 1f);
                Color col = VaultUtils.MultiStepColorLerp(distT,
                    new Color(15, 35, 35),
                    new Color(30, 70, 65),
                    new Color(60, 90, 70),
                    new Color(80, 85, 50));

                // 怨念账越厚, 天越褪向纸钱黄
                col = Color.Lerp(col, new Color(78, 74, 42), s_grudge * 0.4f);

                if (s_phaseLevel >= 3)
                    col = Color.Lerp(col, new Color(60, 30, 30), 0.22f);

                // 死亡演出: 大爆前收束发暗, 爆后天色回暖 (魂债偿清)
                if (s_deathProgress > 0f) {
                    const float burstT = 150f / 210f;
                    if (s_deathProgress < burstT)
                        col = Color.Lerp(col, new Color(22, 14, 20), s_deathProgress / burstT * 0.8f);
                    else
                        col = Color.Lerp(col, new Color(125, 112, 88), (s_deathProgress - burstT) / (1f - burstT) * 0.85f);
                }

                skyColor = col;
            }
            else {
                intensity -= 0.012f;
                if (intensity <= 0f) {
                    intensity = 0f;
                    Deactivate();
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (maxDepth < 0 || minDepth >= 0 || intensity <= 0.01f)
                return;

            // 压暗染色底
            spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                skyColor * intensity);

            DrawSoulMotes(spriteBatch);
        }

        /// <summary>
        /// 上飘魂点: 两层视差 (远层慢小暗 / 近层快大亮), 索引伪随机 + 世界视差锚定,
        /// 上升中带正弦横摆 — 地府"万魂上渡"的氛围粒子。
        /// </summary>
        private void DrawSoulMotes(SpriteBatch sb) {
            if (intensity < 0.25f)
                return;

            Texture2D px = TextureAssets.MagicPixel.Value;
            float time = Main.GlobalTimeWrappedHourly;
            float w = Main.screenWidth;
            float h = Main.screenHeight;

            for (int layer = 0; layer < 2; layer++) {
                int count = layer == 0 ? 24 : 12;
                float parallax = layer == 0 ? 0.05f : 0.11f;
                float rise = layer == 0 ? 26f : 44f;
                float baseSize = layer == 0 ? 2f : 3f;
                float baseAlpha = layer == 0 ? 0.20f : 0.32f;
                Vector2 anchor = -Main.screenPosition * parallax;

                for (int i = 0; i < count; i++) {
                    float seed = i * 149.3f + layer * 61.7f;
                    float fracX = (MathF.Sin(seed) * 4373.58f) % 1f;
                    float fracY = (MathF.Sin(seed * 1.7f) * 2653.29f) % 1f;
                    if (fracX < 0f) fracX += 1f;
                    if (fracY < 0f) fracY += 1f;

                    float x = Wrap(fracX * w + anchor.X + MathF.Sin(time * 0.7f + seed) * 18f, w + 80f) - 40f;
                    float y = Wrap(fracY * h - time * rise + anchor.Y, h + 80f) - 40f;

                    float pulse = MathF.Sin(time * 2f + seed * 0.31f) * 0.35f + 0.65f;
                    Color c = i % 3 == 0 ? SpectreHelper.SpectreYellow : SpectreHelper.SpectreCyan;
                    c *= pulse * intensity * baseAlpha;
                    c.A = 0;

                    float size = baseSize + pulse * 2f;
                    sb.Draw(px, new Rectangle((int)x, (int)y, (int)size, (int)size), c);
                    // 上飘拖尾 (半亮细条)
                    sb.Draw(px, new Rectangle((int)(x + size * 0.25f), (int)(y + size), (int)(size * 0.5f), (int)(size * 2f)), c * 0.4f);
                }
            }
        }

        private static float Wrap(float v, float period) {
            v %= period;
            return v < 0f ? v + period : v;
        }

        public override Color OnTileColor(Color inColor) {
            // 地形染青黄; 死亡收尾回暖
            Color tint = Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, 0.3f + s_grudge * 0.25f);
            if (s_deathProgress > 150f / 210f)
                tint = Color.Lerp(tint, new Color(230, 210, 170), 0.5f);
            return Color.Lerp(inColor, tint, intensity * 0.4f);
        }

        public override float GetCloudAlpha() => 1f - intensity * 0.7f;
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
    /// 怨灵天空激活管理 — 以 Boss 发布新鲜度为准, 不再每帧扫描 NPC 数组。
    /// </summary>
    public class SpectreBossSystem : ModSystem
    {
        public override void PostUpdateNPCs() {
            if (Main.dedServ)
                return;
            bool shouldBeActive = SpectreSky.PublishFresh;
            if (shouldBeActive) {
                if (!SkyManager.Instance[SpectreSky.name].IsActive())
                    SkyManager.Instance.Activate(SpectreSky.name);
            }
            else if (SkyManager.Instance[SpectreSky.name].IsActive()) {
                SkyManager.Instance.Deactivate(SpectreSky.name);
            }
        }
    }
}
