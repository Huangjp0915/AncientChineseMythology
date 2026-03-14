using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace AncientChineseMythology.Menus
{
    /// <summary>
    /// 洪荒神话 —— 专属标题界面
    /// 主题意象：混沌初开、仙气缭绕、金光万道
    /// </summary>
    public class ACMMenu : ModMenu
    {
        // ── 纹理资源 ──
        private Asset<Texture2D> logo;
        private Asset<Texture2D> softGlow;

        // ── 粒子系统 ──
        private const int MaxMotes = 80;
        private readonly MoteDust[] motes = new MoteDust[MaxMotes];

        private const int MaxClouds = 6;
        private readonly CloudLayer[] clouds = new CloudLayer[MaxClouds];

        // ── 时间 ──
        private float timer;

        // ── 类型 ──
        private struct MoteDust
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Scale;
            public float Life;     // 0→1→0
            public float MaxLife;
            public float Hue;      // 0 = 金, 1 = 青
        }

        private struct CloudLayer
        {
            public float X;
            public float Y;
            public float Speed;
            public float Alpha;
            public float ScaleX;
            public float ScaleY;
        }

        // ═══════════ 生命周期 ═══════════

        public override void Load()
        {
            logo = ModContent.Request<Texture2D>("AncientChineseMythology/Menus/Logo");
            softGlow = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Masking/SoftGlow");

            InitClouds();
        }

        public override void OnSelected()
        {
            timer = 0f;
            for (int i = 0; i < MaxMotes; i++)
                SpawnMote(ref motes[i], randomStart: true);
            InitClouds();
        }

        // ═══════════ 属性覆写 ═══════════

        public override Asset<Texture2D> Logo => logo;

        public override int Music =>
            MusicLoader.GetMusicSlot(Mod, "Sounds/Music/HeavenTheme");

        public override string DisplayName => "洪荒神话";

        // ═══════════ 更新 ═══════════

        public override void Update(bool isOnTitleScreen)
        {
            timer += 0.016f; // ≈60fps

            // 更新灵粒
            for (int i = 0; i < MaxMotes; i++)
            {
                ref MoteDust m = ref motes[i];
                m.Life += 1f / (m.MaxLife * 60f);
                if (m.Life >= 1f)
                {
                    SpawnMote(ref m, randomStart: false);
                    continue;
                }
                m.Pos += m.Vel;
                // 微弱横向摇摆
                m.Pos.X += MathF.Sin(timer * 2f + m.Pos.Y * 0.01f) * 0.15f;
            }

            // 更新云层
            for (int i = 0; i < MaxClouds; i++)
            {
                ref CloudLayer c = ref clouds[i];
                c.X += c.Speed;
                if (c.X > Main.screenWidth + 600)
                    c.X = -600;
                if (c.X < -600)
                    c.X = Main.screenWidth + 600;
            }
        }

        // ═══════════ 绘制 ═══════════

        public override bool PreDrawLogo(SpriteBatch sb,
            ref Vector2 logoDrawCenter, ref float logoRotation,
            ref float logoScale, ref Color drawColor)
        {
            // ── 1. 全屏暗色背景（混沌深渊色） ──
            DrawFullScreenGradient(sb);

            // ── 2. 云雾层 ──
            DrawClouds(sb);

            // ── 3. 浮动灵粒 ──
            DrawMotes(sb);

            // ── 4. 四角晕影（聚焦中心） ──
            DrawVignette(sb);

            // ── 5. Logo 呼吸效果 ──
            float breathe = 1f + MathF.Sin(timer * 1.2f) * 0.015f;
            logoScale = 0.82f * breathe;
            logoDrawCenter = new Vector2(Main.screenWidth / 2f, 100f);

            // 金色微光 Logo 色调
            float glowPulse = 0.85f + MathF.Sin(timer * 1.8f) * 0.15f;
            drawColor = Color.Lerp(new Color(255, 230, 180), Color.White, glowPulse);

            return true;
        }

        public override void PostDrawLogo(SpriteBatch sb,
            Vector2 logoDrawCenter, float logoRotation,
            float logoScale, Color drawColor)
        {
            // Logo 后方光晕
            if (softGlow?.IsLoaded == true)
            {
                Texture2D glow = softGlow.Value;
                float pulse = 0.6f + MathF.Sin(timer * 1.5f) * 0.15f;
                sb.Draw(glow, logoDrawCenter,
                    null,
                    new Color(255, 200, 80) * (pulse * 0.25f),
                    0f,
                    new Vector2(glow.Width / 2f, glow.Height / 2f),
                    logoScale * 4.5f,
                    SpriteEffects.None, 0f);
            }
        }

        // ═══════════ 绘制辅助 ═══════════

        /// <summary>
        /// 全屏渐变：顶部深紫黑 → 底部墨蓝，营造"混沌初开"的幽暗氛围
        /// </summary>
        private void DrawFullScreenGradient(SpriteBatch sb)
        {
            int w = Main.screenWidth;
            int h = Main.screenHeight;
            int bands = 16;
            int bandH = h / bands + 1;

            Color topColor = new(12, 8, 24);         // 深紫黑
            Color midColor = new(18, 14, 40);         // 暗蓝紫
            Color bottomColor = new(8, 16, 32);       // 深墨蓝

            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;

            for (int i = 0; i < bands; i++)
            {
                float t = (float)i / (bands - 1);
                Color c;
                if (t < 0.5f)
                    c = Color.Lerp(topColor, midColor, t * 2f);
                else
                    c = Color.Lerp(midColor, bottomColor, (t - 0.5f) * 2f);

                sb.Draw(pixel, new Rectangle(0, i * bandH, w, bandH), c);
            }
        }

        /// <summary>
        /// 绘制缓慢飘动的云雾层（仙气缭绕感）
        /// </summary>
        private void DrawClouds(SpriteBatch sb)
        {
            if (softGlow?.IsLoaded != true) return;
            Texture2D glow = softGlow.Value;

            for (int i = 0; i < MaxClouds; i++)
            {
                ref CloudLayer c = ref clouds[i];
                float fade = c.Alpha * (0.7f + MathF.Sin(timer * 0.5f + i * 1.3f) * 0.3f);
                Color col = new Color(160, 140, 200) * (fade * 0.12f);

                sb.Draw(glow,
                    new Vector2(c.X, c.Y),
                    null, col, 0f,
                    new Vector2(glow.Width / 2f, glow.Height / 2f),
                    new Vector2(c.ScaleX, c.ScaleY),
                    SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 绘制上浮灵粒（金色 / 青玉色微光点）
        /// </summary>
        private void DrawMotes(SpriteBatch sb)
        {
            if (softGlow?.IsLoaded != true) return;
            Texture2D glow = softGlow.Value;
            Vector2 origin = new(glow.Width / 2f, glow.Height / 2f);

            for (int i = 0; i < MaxMotes; i++)
            {
                ref MoteDust m = ref motes[i];
                // 淡入淡出
                float alpha = m.Life < 0.2f
                    ? m.Life / 0.2f
                    : m.Life > 0.8f
                        ? (1f - m.Life) / 0.2f
                        : 1f;
                alpha *= 0.6f;

                Color col = m.Hue < 0.5f
                    ? new Color(255, 210, 100)  // 金色
                    : new Color(120, 220, 200); // 青玉色
                col *= alpha;

                sb.Draw(glow, m.Pos, null, col, 0f,
                    origin, m.Scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 四角暗角晕影，将视觉焦点引向画面中心与 Logo
        /// </summary>
        private void DrawVignette(SpriteBatch sb)
        {
            if (softGlow?.IsLoaded != true) return;
            Texture2D glow = softGlow.Value;
            Vector2 origin = new(glow.Width / 2f, glow.Height / 2f);

            int w = Main.screenWidth;
            int h = Main.screenHeight;
            float vScale = Math.Max(w, h) / (float)glow.Width * 1.2f;
            Color dark = Color.Black * 0.55f;

            // 四角
            Vector2[] corners = [
                new(0, 0),
                new(w, 0),
                new(0, h),
                new(w, h),
            ];
            foreach (Vector2 pos in corners)
                sb.Draw(glow, pos, null, dark, 0f, origin, vScale * 0.5f, SpriteEffects.None, 0f);
        }

        // ═══════════ 粒子初始化 ═══════════

        private void SpawnMote(ref MoteDust m, bool randomStart)
        {
            m.Pos = new Vector2(
                Main.rand.Next(-40, Math.Max(41, Main.screenWidth + 40)),
                Main.rand.Next(Math.Max(1, Main.screenHeight / 3), Math.Max(2, Main.screenHeight + 60)));
            m.Vel = new Vector2(
                Main.rand.NextFloat() * 0.3f - 0.15f,
                -(0.2f + Main.rand.NextFloat() * 0.5f));
            m.Scale = 0.08f + Main.rand.NextFloat() * 0.14f;
            m.MaxLife = 4f + Main.rand.NextFloat() * 6f;
            m.Life = randomStart ? Main.rand.NextFloat() : 0f;
            m.Hue = Main.rand.NextFloat();
        }

        private void InitClouds()
        {
            for (int i = 0; i < MaxClouds; i++)
            {
                clouds[i] = new CloudLayer
                {
                    X = Main.rand.Next(-400, Math.Max(1, Main.screenWidth + 400)),
                    Y = 50 + Main.rand.Next(0, Math.Max(1, Main.screenHeight / 2)),
                    Speed = (i % 2 == 0 ? 1 : -1) * (0.15f + Main.rand.NextFloat() * 0.25f),
                    Alpha = 0.5f + Main.rand.NextFloat() * 0.5f,
                    ScaleX = 4f + Main.rand.NextFloat() * 5f,
                    ScaleY = 1.5f + Main.rand.NextFloat() * 2f,
                };
            }
        }
    }
}
