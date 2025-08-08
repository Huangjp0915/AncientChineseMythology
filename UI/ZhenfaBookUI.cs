using AncientChineseMythology.Players;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace AncientChineseMythology.UI
{
    public class ZhenfaBookUI : UIState
    {
        private UIPanel root;      // 外层可拖动面板
        private UIList list;      // 配方列表
        private UIScrollbar scroll;
        private UIText title;

        /* ── 拖拽 ───────────────────────────── */
        private bool dragging;
        private Vector2 dragOffset;
        private bool prevMouseLeft;

        /* ── 常量 ───────────────────────────── */
        private const float WIDTH = 360f;
        private const float HEIGHT = 440f;
        private const float TITLE_H = 26f;

        public override void OnInitialize() {
            // 根面板（不再设置 HAlign/VAlign，完全由 Left/Top 控制）
            root = new UIPanel();
            root.SetPadding(12);
            root.Width.Set(WIDTH, 0f);
            root.Height.Set(HEIGHT, 0f);
            Append(root);

            // 标题
            title = new UIText("阵法百科全书", 0.9f, true) {
                HAlign = 0.5f,
                TextColor = new Color(255, 240, 170)
            };
            root.Append(title);

            // 配方列表
            list = new UIList {
                Width = { Pixels = -24, Percent = 1f },
                Height = { Pixels = -68, Percent = 1f },
                Top = { Pixels = TITLE_H + 10 },
                ListPadding = 6f
            };
            root.Append(list);

            // 滚动条
            scroll = new UIScrollbar();
            scroll.SetView(100f, 1000f);
            scroll.Height.Set(-68, 1f);
            scroll.Top.Set(TITLE_H + 10, 0f);
            scroll.HAlign = 1f;
            root.Append(scroll);
            list.SetScrollbar(scroll);

            // 初始居中
            PlaceAtCenter();
        }

        public override void OnActivate() { /* 不再重置位置，保持上一次拖动 */ }

        /* ───────────────── 拖拽 ───────────────── */
        public override void Update(GameTime gameTime) {
            base.Update(gameTime);
            bool mouseNow = Main.mouseLeft;

            if (!prevMouseLeft && mouseNow && root.ContainsPoint(Main.MouseScreen)) {
                dragging = true;
                dragOffset = Main.MouseScreen - root.GetDimensions().Position();
                Main.LocalPlayer.mouseInterface = true;
            }

            if (dragging) {
                if (!mouseNow)
                    dragging = false;
                else {
                    root.Left.Set(Main.mouseX - dragOffset.X, 0f);
                    root.Top.Set(Main.mouseY - dragOffset.Y, 0f);
                    root.Recalculate();
                }
            }

            prevMouseLeft = mouseNow;
        }

        private void PlaceAtCenter() {
            root.Left.Set(Main.screenWidth * 0.5f - WIDTH * 0.5f, 0f);
            root.Top.Set(Main.screenHeight * 0.5f - HEIGHT * 0.5f, 0f);
            root.Recalculate();
        }

        /* ─────────────── 重建列表 ─────────────── */
        public void RebuildList() {
            if (list == null) return;
            list.Clear();

            if (Main.gameMenu || Main.LocalPlayer == null) return;

            List<string> recipes = Main.LocalPlayer.GetModPlayer<ZhenfaPlayer>().DiscoveredRecipes;
            if (recipes.Count == 0) {
                list.Add(new UIText("暂无记录…", 0.8f) {
                    TextColor = Color.Gray,
                    HAlign = 0.5f
                });
                return;
            }

            foreach (string r in recipes) {
                list.Add(new UIText("• " + r, 0.8f) {
                    Width = { Percent = 1f, Pixels = -8 },
                    IsWrapped = true
                });
            }
        }
    }
}