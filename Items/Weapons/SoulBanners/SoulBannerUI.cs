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
    /// 显示灵魂数量/进度条、大招状态、Boss 阶层列表、成长加成数值。
    /// 即时反馈: 增魂脉冲 (数字弹跳 + 条尾闪光 + "+N 魂"浮字)、满魂呼吸辉光、
    /// 大招就绪金色行。面板高度动态计算; Boss 列表超出可见区域时支持滚轮滚动。
    /// 交互语义不变: 按住 Shift 显示, 滚轮翻 Boss 列表。
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
        private static readonly Color BarFillDeep = new(80, 30, 140);
        private static readonly Color BarGlow = new(190, 120, 255);
        private static readonly Color DefeatedColor = new(100, 255, 140);
        private static readonly Color LockedColor = new(110, 70, 70);
        private static readonly Color NextColor = new(255, 210, 80);
        private static readonly Color UltReadyColor = new(255, 210, 80);
        private static readonly Color FullSoulGlow = new(225, 180, 255);

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
            float ratio = sbPlayer.GrowthRatio;
            bool fullSoul = hasBar && ratio >= 0.999f;
            // 增魂脉冲 0~1 (30 帧衰减)
            float gainPulse = sbPlayer.lastGainTimer > 0 ? sbPlayer.lastGainTimer / 30f : 0f;

            // ── 动态计算面板高度 ──
            int visibleBossRows = Math.Min(tiers.Length, MaxVisibleBossRows);
            int panelH = Padding                       // 顶部边距
                       + 28                            // 标题
                       + 8                             // 分隔线
                       + 22                            // 灵魂标签
                       + (hasBar ? 22 : 4)             // 进度条或间距
                       + (hasBar ? 20 : 0)             // 大招状态行
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
            //  背景 + 边框 (满魂时边框呼吸辉光)
            // ══════════════════════════════════════
            sb.Draw(pixel, new Rectangle(px, py, PanelW, panelH), PanelBg * a);

            float breathe = fullSoul ? 0.55f + 0.45f * MathF.Sin(glowPhase * 1.6f) : 0f;
            Color borderCol = fullSoul
                ? Color.Lerp(PanelBorder, FullSoulGlow, breathe)
                : PanelBorder;

            int bw = 2;
            sb.Draw(pixel, new Rectangle(px, py, PanelW, bw), borderCol * a);
            sb.Draw(pixel, new Rectangle(px, py + panelH - bw, PanelW, bw), borderCol * a);
            sb.Draw(pixel, new Rectangle(px, py, bw, panelH), borderCol * a);
            sb.Draw(pixel, new Rectangle(px + PanelW - bw, py, bw, panelH), borderCol * a);

            // 满魂: 边框外一圈柔和溢光
            if (fullSoul) {
                Color halo = FullSoulGlow * (a * 0.25f * breathe);
                sb.Draw(pixel, new Rectangle(px - 2, py - 2, PanelW + 4, 2), halo);
                sb.Draw(pixel, new Rectangle(px - 2, py + panelH, PanelW + 4, 2), halo);
                sb.Draw(pixel, new Rectangle(px - 2, py, 2, panelH), halo);
                sb.Draw(pixel, new Rectangle(px + PanelW, py, 2, panelH), halo);
            }

            // 四角高亮
            float cornerGlow = 0.6f + 0.4f * MathF.Sin(glowPhase);
            Color cg = borderCol * (a * cornerGlow);
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
            //  标题 (满魂时镀上暖金)
            // ══════════════════════════════════════
            string title = "〈 万魂幡 · 魂录 〉";
            Vector2 titleSize = font.MeasureString(title) * 0.95f;
            float titleX = px + (PanelW - titleSize.X) / 2f;
            Color titleCol = fullSoul ? Color.Lerp(TitleColor, UltReadyColor, breathe * 0.7f) : TitleColor;
            ChatManager.DrawColorCodedStringWithShadow(sb, font, title,
                new Vector2(titleX, cy), titleCol * a, 0f, Vector2.Zero, new Vector2(0.95f));
            cy += (int)titleSize.Y + 6;

            // 分隔线
            sb.Draw(pixel, new Rectangle(cx, cy, PanelW - Padding * 2, 1), PanelBorder * (a * 0.5f));
            cy += 8;

            // ══════════════════════════════════════
            //  灵魂进度 (增魂脉冲: 数字弹跳 + "+N 魂"浮字)
            // ══════════════════════════════════════
            string soulLabel = hasBar
                ? $"灵魂：{sbPlayer.soulCount} / {sbPlayer.soulCap}"
                : "灵魂：尚未觉醒";
            float labelScale = 0.85f * (1f + 0.22f * gainPulse * gainPulse);
            Color labelCol = Color.Lerp(LabelColor, BarGlow, gainPulse);
            Utils.DrawBorderString(sb, soulLabel, new Vector2(cx, cy), labelCol * a, labelScale);

            if (gainPulse > 0f && sbPlayer.lastGainAmount > 0) {
                string gainText = $"+{sbPlayer.lastGainAmount} 魂";
                Vector2 gainSize = font.MeasureString(gainText) * 0.8f;
                float floatUp = (1f - gainPulse) * 10f;
                Utils.DrawBorderString(sb, gainText,
                    new Vector2(px + PanelW - Padding - gainSize.X, cy - floatUp),
                    new Color(200, 140, 255) * (a * gainPulse), 0.8f);
            }
            cy += 22;

            if (hasBar) {
                int barX = cx;
                int barW = PanelW - Padding * 2;
                int barH = 14;

                sb.Draw(pixel, new Rectangle(barX, cy, barW, barH), BarBg * a);
                int fillW = (int)(barW * ratio);
                if (fillW > 0) {
                    // 双色纵向渐变 (上亮下深, 两条横带模拟)
                    sb.Draw(pixel, new Rectangle(barX, cy, fillW, barH), BarFillDeep * a);
                    sb.Draw(pixel, new Rectangle(barX, cy, fillW, barH / 2), BarFill * a);
                }
                if (fillW > 2)
                    sb.Draw(pixel, new Rectangle(barX, cy, fillW, 2), BarGlow * (a * 0.6f));

                // 增魂脉冲: 条尾闪光扩散
                if (gainPulse > 0f && fillW > 2) {
                    int flashW = (int)(14 + 26 * (1f - gainPulse));
                    int flashX = Math.Max(barX, barX + fillW - flashW);
                    sb.Draw(pixel, new Rectangle(flashX, cy, Math.Min(flashW, fillW), barH),
                        BarGlow * (a * 0.65f * gainPulse));
                }

                // 满魂流光: 一道亮带沿条循环
                if (fullSoul) {
                    float sweep = glowPhase * 0.35f % 1f;
                    int sweepW = 26;
                    int sweepX = barX + (int)((barW - sweepW) * sweep);
                    sb.Draw(pixel, new Rectangle(sweepX, cy, sweepW, barH),
                        FullSoulGlow * (a * 0.35f));
                }

                sb.Draw(pixel, new Rectangle(barX, cy, barW, 1), PanelBorder * (a * 0.4f));
                sb.Draw(pixel, new Rectangle(barX, cy + barH - 1, barW, 1), PanelBorder * (a * 0.4f));
                sb.Draw(pixel, new Rectangle(barX, cy, 1, barH), PanelBorder * (a * 0.4f));
                sb.Draw(pixel, new Rectangle(barX + barW - 1, cy, 1, barH), PanelBorder * (a * 0.4f));

                string pctText = $"{(int)(ratio * 100)}%";
                Vector2 pctSize = font.MeasureString(pctText) * 0.7f;
                Utils.DrawBorderString(sb, pctText,
                    new Vector2(barX + (barW - pctSize.X) / 2f, cy - 1), Color.White * a, 0.7f);

                cy += barH + 8;

                // ── 大招状态行 ──
                if (sbPlayer.UltReady) {
                    float ultBreathe = 0.7f + 0.3f * MathF.Sin(glowPhase * 2.2f);
                    string ultText = "◈ 万魂齐哭 · 就绪 —— 右键悬浮幡引爆";
                    Utils.DrawBorderString(sb, ultText, new Vector2(cx, cy),
                        UltReadyColor * (a * ultBreathe), 0.78f);
                }
                else {
                    string ultText = $"◈ 万魂齐哭 · 蓄魂 {sbPlayer.soulCount}/{SoulBannerPlayer.UltMinSouls}";
                    Utils.DrawBorderString(sb, ultText, new Vector2(cx, cy),
                        DimColor * a, 0.78f);
                }
                cy += 20;
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
