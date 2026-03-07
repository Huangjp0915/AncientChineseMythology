using AncientChineseMythology.Underworlds;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
using Terraria.UI.Chat;

namespace AncientChineseMythology.UI
{
    /// <summary>
    /// 地府入侵事件进度UI
    /// 在屏幕顶部显示：事件名称、当前波次/总波次、总进度条、波次进度条
    /// 暗紫色/幽魂主题
    /// </summary>
    public class UnderworldInvasionUI : UIState
    {
        // 布局常量
        private const int PanelWidth = 400;
        private const int PanelHeight = 80;
        private const int BarHeight = 14;
        private const int BarPadding = 6;
        private const int TextPadding = 4;

        // 颜色主题——暗紫色/幽冥风格
        private static readonly Color PanelBg = new(15, 8, 30, 210);
        private static readonly Color PanelBorder = new(130, 70, 200, 220);
        private static readonly Color BarBg = new(8, 4, 18, 180);
        private static readonly Color BarTotalFill = new(160, 80, 220); // 暗紫总进度
        private static readonly Color BarWaveFill = new(100, 200, 160); // 幽绿波次进度
        private static readonly Color TitleColor = new(200, 130, 255);
        private static readonly Color WaveTextColor = new(180, 200, 230);
        private static readonly Color ProgressTextColor = new(180, 180, 200);

        // 动画
        private float displayAlpha = 0f;
        private float glowPulse = 0f;

        private Texture2D pixel;

        public override void OnInitialize() {
            pixel = TextureAssets.MagicPixel.Value;
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);

            // 淡入淡出
            float targetAlpha = UnderworldInvasionSystem.InvasionActive ? 1f : 0f;
            displayAlpha = MathHelper.Lerp(displayAlpha, targetAlpha, 0.08f);
            if (displayAlpha < 0.01f) displayAlpha = 0f;

            // 光晕脉动
            glowPulse += 0.025f;
            if (glowPulse > MathHelper.TwoPi) glowPulse -= MathHelper.TwoPi;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch) {
            if (displayAlpha <= 0.01f) return;
            if (pixel == null) pixel = TextureAssets.MagicPixel.Value;

            // 面板位置：屏幕顶部居中，如果天庭入侵也在显示则往下偏移
            int screenW = Main.screenWidth;
            int panelX = (screenW - PanelWidth) / 2;
            int panelY = 20;

            // 若天庭入侵UI也在显示，则下移避免重叠
            if (Celestias.PillarofTheHeavenes.HeavenInvasionSystem.InvasionActive) {
                panelY += PanelHeight + 10;
            }

            float alpha = displayAlpha;

            // === 绘制面板背景 ===
            DrawPanel(spriteBatch, panelX, panelY, alpha);

            // === 绘制标题 ===
            string title = "地府入侵";
            Vector2 titleSize = FontAssets.DeathText.Value.MeasureString(title) * 0.5f;
            Vector2 titlePos = new(panelX + PanelWidth / 2f - titleSize.X / 2f, panelY + TextPadding);
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.DeathText.Value,
                title, titlePos, TitleColor * alpha, 0f, Vector2.Zero, new Vector2(0.5f));

            // === 绘制波次信息 ===
            int currentWave = UnderworldInvasionSystem.CurrentWave;
            int totalWaves = UnderworldInvasionSystem.TotalWaves;
            int progress = UnderworldInvasionSystem.InvasionProgress;

            string waveText = $"第 {currentWave} / {totalWaves} 波";
            string progressText = $"{progress}%";
            Vector2 waveSize = FontAssets.MouseText.Value.MeasureString(waveText);
            Vector2 progressSize = FontAssets.MouseText.Value.MeasureString(progressText);

            float waveY = panelY + TextPadding + titleSize.Y + 2;

            // 波次文字（左侧）
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value,
                waveText, new Vector2(panelX + BarPadding + 2, waveY),
                WaveTextColor * alpha, 0f, Vector2.Zero, Vector2.One * 0.85f);

            // 进度百分比（右侧）
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value,
                progressText, new Vector2(panelX + PanelWidth - BarPadding - progressSize.X * 0.85f - 2, waveY),
                ProgressTextColor * alpha, 0f, Vector2.Zero, Vector2.One * 0.85f);

            // === 绘制总进度条 ===
            float barY = waveY + waveSize.Y * 0.85f + 3;
            int barWidth = PanelWidth - BarPadding * 2;
            DrawProgressBar(spriteBatch, panelX + BarPadding, (int)barY, barWidth, BarHeight,
                (float)progress / 100f, BarTotalFill, alpha);

            // === 绘制波次进度条（细条） ===
            float waveBarY = barY + BarHeight + 3;
            float waveProgress = UnderworldInvasionSystem.CurrentWaveProgress;
            DrawProgressBar(spriteBatch, panelX + BarPadding, (int)waveBarY, barWidth, BarHeight - 4,
                waveProgress, BarWaveFill, alpha);

            // 波次进度条上的标签
            string waveLabel = "当前波次";
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value,
                waveLabel, new Vector2(panelX + BarPadding + 4, waveBarY - 1),
                Color.White * alpha * 0.8f, 0f, Vector2.Zero, Vector2.One * 0.6f);
        }

        /// <summary>
        /// 绘制带边框的半透明面板
        /// </summary>
        private void DrawPanel(SpriteBatch sb, int x, int y, float alpha) {
            // 外层幽光
            float glowAlpha = 0.25f + 0.12f * (float)Math.Sin(glowPulse);
            Rectangle glowRect = new(x - 3, y - 3, PanelWidth + 6, PanelHeight + 6);
            sb.Draw(pixel, glowRect, PanelBorder * alpha * glowAlpha);

            // 边框
            int border = 2;
            Rectangle borderRect = new(x - border, y - border, PanelWidth + border * 2, PanelHeight + border * 2);
            sb.Draw(pixel, borderRect, PanelBorder * alpha * 0.7f);

            // 背景
            Rectangle bgRect = new(x, y, PanelWidth, PanelHeight);
            sb.Draw(pixel, bgRect, PanelBg * alpha);

            // 顶部装饰线
            Rectangle topLine = new(x + 4, y + 1, PanelWidth - 8, 1);
            sb.Draw(pixel, topLine, TitleColor * alpha * 0.4f);
        }

        /// <summary>
        /// 绘制进度条
        /// </summary>
        private void DrawProgressBar(SpriteBatch sb, int x, int y, int width, int height,
            float percent, Color fillColor, float alpha) {
            // 进度条背景
            Rectangle bgRect = new(x, y, width, height);
            sb.Draw(pixel, bgRect, BarBg * alpha);

            // 进度条边框
            Rectangle borderTop = new(x, y, width, 1);
            Rectangle borderBot = new(x, y + height - 1, width, 1);
            Rectangle borderLeft = new(x, y, 1, height);
            Rectangle borderRight = new(x + width - 1, y, 1, height);
            Color borderColor = PanelBorder * alpha * 0.5f;
            sb.Draw(pixel, borderTop, borderColor);
            sb.Draw(pixel, borderBot, borderColor);
            sb.Draw(pixel, borderLeft, borderColor);
            sb.Draw(pixel, borderRight, borderColor);

            // 填充
            int fillWidth = (int)((width - 4) * MathHelper.Clamp(percent, 0f, 1f));
            if (fillWidth > 0) {
                Rectangle fillRect = new(x + 2, y + 2, fillWidth, height - 4);
                sb.Draw(pixel, fillRect, fillColor * alpha);

                // 填充高光
                Rectangle highlightRect = new(x + 2, y + 2, fillWidth, (height - 4) / 3);
                sb.Draw(pixel, highlightRect, Color.White * alpha * 0.12f);
            }
        }
    }
}
