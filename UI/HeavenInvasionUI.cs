using AncientChineseMythology.Celestias.PillarofTheHeavenes;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
using Terraria.UI.Chat;

namespace AncientChineseMythology.UI
{
    /// <summary>
    /// 天庭入侵事件进度UI —— 紧凑古卷轴风格
    /// 屏幕顶部居中：金色「 」角饰边框 + 一行文字 + 薄进度条
    /// </summary>
    public class HeavenInvasionUI : UIState
    {
        // 布局常量——紧凑尺寸
        private const int PanelWidth = 300;
        private const int PanelHeight = 38;
        private const int BarHeight = 6;
        private const int BarSidePad = 8;
        private const int CornerLen = 10;   // 角饰长度
        private const int CornerThick = 2;  // 角饰粗细

        // 颜色主题
        private static readonly Color PanelBg = new(12, 6, 28, 160);
        private static readonly Color Gold = new(218, 185, 68);
        private static readonly Color GoldDim = new(160, 135, 50);
        private static readonly Color BarBg = new(8, 4, 16, 180);
        private static readonly Color BarFill = new(220, 180, 40);
        private static readonly Color BarWaveMark = new(180, 220, 255, 180);
        private static readonly Color TitleColor = new(255, 220, 90);
        private static readonly Color InfoColor = new(210, 210, 230);

        // 动画状态
        private float displayAlpha = 0f;
        private float glowPhase = 0f;
        private float smoothProgress = 0f;

        private Texture2D pixel;

        public override void OnInitialize() {
            pixel = TextureAssets.MagicPixel.Value;
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);

            float targetAlpha = HeavenInvasionSystem.InvasionActive ? 1f : 0f;
            displayAlpha = MathHelper.Lerp(displayAlpha, targetAlpha, 0.08f);
            if (displayAlpha < 0.01f) displayAlpha = 0f;

            // 平滑进度动画
            float targetProgress = HeavenInvasionSystem.InvasionProgress / 100f;
            smoothProgress = MathHelper.Lerp(smoothProgress, targetProgress, 0.06f);

            glowPhase += 0.04f;
            if (glowPhase > MathHelper.TwoPi) glowPhase -= MathHelper.TwoPi;
        }

        protected override void DrawSelf(SpriteBatch sb) {
            if (displayAlpha <= 0.01f) return;
            pixel ??= TextureAssets.MagicPixel.Value;

            float a = displayAlpha;
            int px = (Main.screenWidth - PanelWidth) / 2;
            int py = 10;

            // ── 背景 ──
            sb.Draw(pixel, new Rectangle(px, py, PanelWidth, PanelHeight), PanelBg * a);

            // ── 金色「 」角饰 ──
            float glowA = 0.7f + 0.3f * (float)Math.Sin(glowPhase);
            Color cGold = Gold * a * glowA;
            DrawCornerBrackets(sb, px, py, PanelWidth, PanelHeight, cGold);

            // ── 顶部 / 底部细金线 ──
            sb.Draw(pixel, new Rectangle(px + CornerLen, py, PanelWidth - CornerLen * 2, 1), GoldDim * a * 0.4f);
            sb.Draw(pixel, new Rectangle(px + CornerLen, py + PanelHeight - 1, PanelWidth - CornerLen * 2, 1), GoldDim * a * 0.4f);

            // ── 文字行：标题 · 波次 · 百分比 ──
            int currentWave = HeavenInvasionSystem.CurrentWave;
            int totalWaves = HeavenInvasionSystem.TotalWaves;
            int progress = HeavenInvasionSystem.InvasionProgress;

            string title = "天庭入侵";
            string info = $"  {currentWave}/{totalWaves}波  {progress}%";

            var font = FontAssets.MouseText.Value;
            Vector2 titleSize = font.MeasureString(title) * 0.82f;
            Vector2 infoSize = font.MeasureString(info) * 0.72f;
            float totalTextW = titleSize.X + infoSize.X;
            float textX = px + (PanelWidth - totalTextW) / 2f;
            float textY = py + 3;

            ChatManager.DrawColorCodedStringWithShadow(sb, font, title,
                new Vector2(textX, textY), TitleColor * a, 0f, Vector2.Zero, new Vector2(0.82f));
            ChatManager.DrawColorCodedStringWithShadow(sb, font, info,
                new Vector2(textX + titleSize.X, textY + (titleSize.Y - infoSize.Y) * 0.5f),
                InfoColor * a, 0f, Vector2.Zero, new Vector2(0.72f));

            // ── 进度条 ──
            int barX = px + BarSidePad;
            int barY = py + PanelHeight - BarHeight - 5;
            int barW = PanelWidth - BarSidePad * 2;
            DrawBar(sb, barX, barY, barW, BarHeight, smoothProgress, a);

            // ── 波次分隔刻度线 ──
            DrawWaveTicks(sb, barX, barY, barW, BarHeight, totalWaves, a);

            // ── 进度条末端发光点 ──
            int fillW = (int)((barW - 2) * MathHelper.Clamp(smoothProgress, 0f, 1f));
            if (fillW > 2) {
                float glow = 0.5f + 0.5f * (float)Math.Sin(glowPhase * 2f);
                int dotX = barX + 1 + fillW;
                sb.Draw(pixel, new Rectangle(dotX - 2, barY - 1, 4, BarHeight + 2), Color.White * a * glow * 0.6f);
                sb.Draw(pixel, new Rectangle(dotX - 1, barY, 2, BarHeight), BarFill * a * glow);
            }
        }

        /// <summary>绘制四角「 」形金色角饰</summary>
        private void DrawCornerBrackets(SpriteBatch sb, int x, int y, int w, int h, Color c) {
            int cl = CornerLen, ct = CornerThick;
            // 左上 ┌
            sb.Draw(pixel, new Rectangle(x, y, cl, ct), c);
            sb.Draw(pixel, new Rectangle(x, y, ct, cl), c);
            // 右上 ┐
            sb.Draw(pixel, new Rectangle(x + w - cl, y, cl, ct), c);
            sb.Draw(pixel, new Rectangle(x + w - ct, y, ct, cl), c);
            // 左下 └
            sb.Draw(pixel, new Rectangle(x, y + h - ct, cl, ct), c);
            sb.Draw(pixel, new Rectangle(x, y + h - cl, ct, cl), c);
            // 右下 ┘
            sb.Draw(pixel, new Rectangle(x + w - cl, y + h - ct, cl, ct), c);
            sb.Draw(pixel, new Rectangle(x + w - ct, y + h - cl, ct, cl), c);
        }

        /// <summary>绘制薄进度条</summary>
        private void DrawBar(SpriteBatch sb, int x, int y, int w, int h, float pct, float a) {
            // 底色
            sb.Draw(pixel, new Rectangle(x, y, w, h), BarBg * a);

            // 填充
            int fw = (int)((w - 2) * MathHelper.Clamp(pct, 0f, 1f));
            if (fw > 0) {
                sb.Draw(pixel, new Rectangle(x + 1, y + 1, fw, h - 2), BarFill * a * 0.9f);
                // 上高光
                sb.Draw(pixel, new Rectangle(x + 1, y + 1, fw, 1), Color.White * a * 0.2f);
            }

            // 左右边
            sb.Draw(pixel, new Rectangle(x, y, 1, h), GoldDim * a * 0.5f);
            sb.Draw(pixel, new Rectangle(x + w - 1, y, 1, h), GoldDim * a * 0.5f);
        }

        /// <summary>在进度条上绘制波次分隔刻度线</summary>
        private void DrawWaveTicks(SpriteBatch sb, int x, int y, int w, int h, int waves, float a) {
            if (waves <= 1) return;
            for (int i = 1; i < waves; i++) {
                int tickX = x + (int)((w - 2) * ((float)i / waves)) + 1;
                sb.Draw(pixel, new Rectangle(tickX, y, 1, h), BarWaveMark * a * 0.25f);
            }
        }
    }
}
