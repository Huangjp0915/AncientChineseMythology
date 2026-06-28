using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
using Terraria.UI.Chat;

namespace AncientChineseMythology.Items.Weapons.SoulBanners
{
    /// <summary>
    /// 万魂幡成长面板 —— 纯 DrawSelf 手绘 UI
    /// 显示灵魂数量/进度条、Boss 阶层列表、成长加成数值
    /// 面板高度动态计算；Boss 列表超出可见区域时支持滚轮滚动
    /// </summary>
    public class SoulBannerUI : UIState
    {
        // ── 布局常量 ──
        private const int PanelW = 380;
        private const int Padding = 14;
        private const int RowH = 24;
        private const int MaxVisibleBossRows = 12; // Boss 列表最大可见行数

        // ── 颜色方案 ──
        private static readonly Color PanelBg = new(18, 10, 32, 220);
        private static readonly Color PanelBorder = new(120, 50, 200, 180);
        private static readonly Color TitleColor = new(210, 160, 255);
        private static readonly Color LabelColor = new(180, 180, 200);
        private static readonly Color ValueColor = new(140, 255, 200);
        private static readonly Color DimColor = new(90, 90, 110);
        private static readonly Color BarBg = new(30, 15, 50, 200);
        private static readonly Color BarFill = new(140, 55, 220);
        private static readonly Color BarGlow = new(190, 120, 255);
        private static readonly Color DefeatedColor = new(100, 255, 140);
        private static readonly Color LockedColor = new(110, 70, 70);
        private static readonly Color NextColor = new(255, 210, 80);
        private static readonly Color SectionTitleColor = new(160, 130, 200);

        private float fadeAlpha;
        private float glowPhase;

        /// <summary>Boss 列表滚动偏移（行数）</summary>
        private int scrollOffset;

        public bool Visible { get; set; }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);

            float target = Visible ? 1f : 0f;
            fadeAlpha = MathHelper.Lerp(fadeAlpha, target, 0.15f);
            if (fadeAlpha < 0.01f) {
                fadeAlpha = 0f;
                scrollOffset = 0; // 关闭时重置滚动
            }

            glowPhase += 0.04f;

            if (Visible)
                Main.LocalPlayer.mouseInterface = true;
        }

        public override void ScrollWheel(UIScrollWheelEvent evt) {
            if (!Visible) return;
            int totalRows = SoulBannerPlayer.Tiers.Length;
            int maxScroll = Math.Max(0, totalRows - MaxVisibleBossRows);
            scrollOffset = Math.Clamp(scrollOffset - Math.Sign(evt.ScrollWheelValue), 0, maxScroll);
        }

        protected override void DrawSelf(SpriteBatch sb) {
            if (fadeAlpha < 0.01f) return;

            var sbPlayer = Main.LocalPlayer.GetModPlayer<SoulBannerPlayer>();
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            var tiers = SoulBannerPlayer.Tiers;

            float a = fadeAlpha;
            bool hasStats = sbPlayer.soulCount > 0;
            bool hasBar = sbPlayer.soulCap > 0;

            // ── 动态计算面板高度 ──
            int visibleBossRows = Math.Min(tiers.Length, MaxVisibleBossRows);
            int panelH = Padding                       // 顶部边距
                       + 28                            // 标题
                       + 8                             // 分隔线
                       + 22                            // 灵魂标签
                       + (hasBar ? 22 : 4)             // 进度条或间距
                       + (hasStats ? 6 + 5 * 20 + 4 : 0) // 属性区
                       + 8                             // Boss 分隔线
                       + 22                            // Boss 标题
                       + visibleBossRows * RowH        // Boss 行
                       + (tiers.Length > MaxVisibleBossRows ? 16 : 0) // 滚动提示
                       + 8                             // 间距
                       + 16                            // 底部提示
                       + Padding;                      // 底部边距

            // ── 面板位置（屏幕居中）──
            int px = (Main.screenWidth - PanelW) / 2;
            int py = (Main.screenHeight - panelH) / 2;

            // ══════════════════════════════════════
            //  背景 + 边框
            // ══════════════════════════════════════
            sb.Draw(pixel, new Rectangle(px, py, PanelW, panelH), PanelBg * a);

            int bw = 2;
            sb.Draw(pixel, new Rectangle(px, py, PanelW, bw), PanelBorder * a);
            sb.Draw(pixel, new Rectangle(px, py + panelH - bw, PanelW, bw), PanelBorder * a);
            sb.Draw(pixel, new Rectangle(px, py, bw, panelH), PanelBorder * a);
            sb.Draw(pixel, new Rectangle(px + PanelW - bw, py, bw, panelH), PanelBorder * a);

            // 四角高亮
            float cornerGlow = 0.6f + 0.4f * MathF.Sin(glowPhase);
            Color cg = PanelBorder * (a * cornerGlow);
            int cl = 12;
            sb.Draw(pixel, new Rectangle(px, py, cl, bw + 1), cg);
            sb.Draw(pixel, new Rectangle(px, py, bw + 1, cl), cg);
            sb.Draw(pixel, new Rectangle(px + PanelW - cl, py, cl, bw + 1), cg);
            sb.Draw(pixel, new Rectangle(px + PanelW - bw - 1, py, bw + 1, cl), cg);
            sb.Draw(pixel, new Rectangle(px, py + panelH - bw - 1, cl, bw + 1), cg);
            sb.Draw(pixel, new Rectangle(px, py + panelH - cl, bw + 1, cl), cg);
            sb.Draw(pixel, new Rectangle(px + PanelW - cl, py + panelH - bw - 1, cl, bw + 1), cg);
            sb.Draw(pixel, new Rectangle(px + PanelW - bw - 1, py + panelH - cl, bw + 1, cl), cg);

            int cx = px + Padding;
            int cy = py + Padding;

            // ══════════════════════════════════════
            //  标题
            // ══════════════════════════════════════
            string title = "〈 万魂幡 · 魂录 〉";
            Vector2 titleSize = font.MeasureString(title) * 0.95f;
            float titleX = px + (PanelW - titleSize.X) / 2f;
            ChatManager.DrawColorCodedStringWithShadow(sb, font, title,
                new Vector2(titleX, cy), TitleColor * a, 0f, Vector2.Zero, new Vector2(0.95f));
            cy += (int)titleSize.Y + 6;

            // 分隔线
            sb.Draw(pixel, new Rectangle(cx, cy, PanelW - Padding * 2, 1), PanelBorder * (a * 0.5f));
            cy += 8;

            // ══════════════════════════════════════
            //  灵魂进度
            // ══════════════════════════════════════
            string soulLabel = hasBar
                ? $"灵魂：{sbPlayer.soulCount} / {sbPlayer.soulCap}"
                : "灵魂：尚未觉醒";
            Utils.DrawBorderString(sb, soulLabel, new Vector2(cx, cy), LabelColor * a, 0.85f);
            cy += 22;

            if (hasBar) {
                int barX = cx;
                int barW = PanelW - Padding * 2;
                int barH = 14;
                float ratio = sbPlayer.GrowthRatio;

                sb.Draw(pixel, new Rectangle(barX, cy, barW, barH), BarBg * a);
                int fillW = (int)(barW * ratio);
                if (fillW > 0)
                    sb.Draw(pixel, new Rectangle(barX, cy, fillW, barH), BarFill * a);
                if (fillW > 2)
                    sb.Draw(pixel, new Rectangle(barX, cy, fillW, 2), BarGlow * (a * 0.6f));
                sb.Draw(pixel, new Rectangle(barX, cy, barW, 1), PanelBorder * (a * 0.4f));
                sb.Draw(pixel, new Rectangle(barX, cy + barH - 1, barW, 1), PanelBorder * (a * 0.4f));
                sb.Draw(pixel, new Rectangle(barX, cy, 1, barH), PanelBorder * (a * 0.4f));
                sb.Draw(pixel, new Rectangle(barX + barW - 1, cy, 1, barH), PanelBorder * (a * 0.4f));

                string pctText = $"{(int)(ratio * 100)}%";
                Vector2 pctSize = font.MeasureString(pctText) * 0.7f;
                Utils.DrawBorderString(sb, pctText,
                    new Vector2(barX + (barW - pctSize.X) / 2f, cy - 1), Color.White * a, 0.7f);

                cy += barH + 8;
            }
            else {
                cy += 4;
            }

            // ══════════════════════════════════════
            //  成长加成
            // ══════════════════════════════════════
            if (hasStats) {
                sb.Draw(pixel, new Rectangle(cx, cy, PanelW - Padding * 2, 1), PanelBorder * (a * 0.3f));
                cy += 6;

                DrawStatLine(sb, font, cx, ref cy, a, "伤害倍率",
                    $"×{sbPlayer.DamageMultiplier:F2}", ValueColor);
                DrawStatLine(sb, font, cx, ref cy, a, "吸魂范围",
                    $"×{sbPlayer.AbsorbRadiusMultiplier:F2}", ValueColor);
                DrawStatLine(sb, font, cx, ref cy, a, "引魂持续",
                    $"×{sbPlayer.ChannelTimeMultiplier:F2}", ValueColor);
                DrawStatLine(sb, font, cx, ref cy, a, "生命回复",
                    $"×{sbPlayer.HealMultiplier:F2}", ValueColor);
                DrawStatLine(sb, font, cx, ref cy, a, "击退强度",
                    $"×{sbPlayer.KnockbackMultiplier:F2}", ValueColor);

                cy += 4;
            }

            // ══════════════════════════════════════
            //  Boss 阶层列表（支持滚动）
            // ══════════════════════════════════════
            sb.Draw(pixel, new Rectangle(cx, cy, PanelW - Padding * 2, 1), PanelBorder * (a * 0.3f));
            cy += 8;

            string sectionTitle = "— 封印之魂 —";
            Vector2 secSize = font.MeasureString(sectionTitle) * 0.8f;
            Utils.DrawBorderString(sb, sectionTitle,
                new Vector2(px + (PanelW - secSize.X) / 2f, cy),
                TitleColor * (a * 0.8f), 0.8f);
            cy += 22;

            // 只绘制可见范围内的 Boss 行（scrollOffset ~ scrollOffset+MaxVisibleBossRows）
            bool foundNext = false;
            // 先扫描一遍确定 foundNext 在 scrollOffset 之前是否已出现
            for (int i = 0; i < scrollOffset && i < tiers.Length; i++) {
                if (!sbPlayer.defeatedBossTiers.Contains(tiers[i].TierId) && !foundNext)
                    foundNext = true;
            }

            int endRow = Math.Min(tiers.Length, scrollOffset + visibleBossRows);
            for (int i = scrollOffset; i < endRow; i++) {
                int drawY = cy + (i - scrollOffset) * RowH;
                var tier = tiers[i];
                bool defeated = sbPlayer.defeatedBossTiers.Contains(tier.TierId);

                string icon;
                Color nameColor;
                Color capColor;

                if (defeated) {
                    icon = "✦";
                    nameColor = DefeatedColor;
                    capColor = DefeatedColor * 0.7f;
                }
                else if (!foundNext) {
                    icon = "▸";
                    nameColor = NextColor;
                    capColor = NextColor * 0.8f;
                    foundNext = true;
                }
                else {
                    icon = "✧";
                    nameColor = LockedColor;
                    capColor = LockedColor * 0.6f;
                }

                Utils.DrawBorderString(sb, icon, new Vector2(cx, drawY), nameColor * a, 0.78f);
                Utils.DrawBorderString(sb, tier.NameZh, new Vector2(cx + 20, drawY), nameColor * a, 0.78f);

                string capText = $"→ {tier.CapValue}";
                Vector2 capSize = font.MeasureString(capText) * 0.72f;
                Utils.DrawBorderString(sb, capText,
                    new Vector2(px + PanelW - Padding - capSize.X, drawY), capColor * a, 0.72f);
            }

            cy += visibleBossRows * RowH;

            // 滚动提示
            if (tiers.Length > MaxVisibleBossRows) {
                int maxScroll = tiers.Length - MaxVisibleBossRows;
                string scrollHint = scrollOffset < maxScroll
                    ? "▼ 滚轮查看更多 ▼"
                    : "▲ 滚轮向上 ▲";
                Vector2 scrollSize = font.MeasureString(scrollHint) * 0.65f;
                Utils.DrawBorderString(sb, scrollHint,
                    new Vector2(px + (PanelW - scrollSize.X) / 2f, cy + 2), DimColor * (a * 0.5f), 0.65f);
                cy += 16;
            }

            // ══════════════════════════════════════
            //  底部提示
            // ══════════════════════════════════════
            cy += 8;
            string hint = "松开 [Shift] 关闭";
            Vector2 hintSize = font.MeasureString(hint) * 0.7f;
            Utils.DrawBorderString(sb, hint,
                new Vector2(px + (PanelW - hintSize.X) / 2f, cy), DimColor * (a * 0.6f), 0.7f);
        }

        /// <summary>绘制一行属性：左侧标签 + 右侧数值</summary>
        private static void DrawStatLine(SpriteBatch sb, DynamicSpriteFont font,
            int x, ref int y, float alpha, string label, string value, Color valColor) {
            Utils.DrawBorderString(sb, label, new Vector2(x, y), LabelColor * alpha, 0.78f);

            Vector2 valSize = font.MeasureString(value) * 0.78f;
            float rightX = x + PanelW - Padding * 2 - valSize.X;
            Utils.DrawBorderString(sb, value, new Vector2(rightX, y), valColor * alpha, 0.78f);

            y += 20;
        }
    }
}
