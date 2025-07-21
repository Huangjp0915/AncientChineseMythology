using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace AncientChineseMythology.UI
{
    public class UIItemSlot : UIElement
    {
        public Item item = new();

        private readonly int size;          // 方框像素尺寸
        private const int VANILLA = 56;     // 原版 InventoryBack 贴图边长

        private int originalStack = 0;      // 放入时的初始堆叠，用于进度条

        public UIItemSlot(int pixelSize) {
            size = pixelSize;
            item.TurnToAir();
            Width.Set(size, 0f);
            Height.Set(size, 0f);
        }

        protected override void DrawSelf(SpriteBatch sb) {
            var dim = GetInnerDimensions();

            /* ---------- 背景框 ---------- */
            float backScale = size / (float)VANILLA; // 将 56px 的背框缩放到自定义尺寸
            sb.Draw(TextureAssets.InventoryBack.Value,
                    dim.Position(),
                    null,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    backScale,
                    SpriteEffects.None,
                    0f);

            /* ---------- 更新初始堆叠 ---------- */
            if (item.IsAir)
                originalStack = 0;
            else if (originalStack == 0 || item.stack > originalStack)
                originalStack = item.stack; // 第一次放入或补充时刷新基准

            /* ---------- 物品贴图 ---------- */
            if (!item.IsAir) {
                Main.instance.LoadItem(item.type);
                Texture2D tex = TextureAssets.Item[item.type].Value;
                Rectangle frame = tex.Frame();

                // 把贴图等比缩放到「方框 - 10px」大小，使绝大多数图标都能铺满但不溢出
                float iconScale = (size - 10f) / Math.Max(frame.Width, frame.Height);
                Vector2 pos = dim.Position() + new Vector2(size / 2f) - frame.Size() * iconScale / 2f;

                sb.Draw(tex, pos, frame, Color.White, 0f, Vector2.Zero, iconScale, SpriteEffects.None, 0f);

                /* ---------- 数量文本 ---------- */
                if (item.stack > 1) {
                    Utils.DrawBorderString(sb,
                                            item.stack.ToString(),
                                            dim.Position() + new Vector2(2f, size - 18f),
                                            Color.White,
                                            0.8f);
                }

                /* ---------- 耐久 / 进度条 ---------- */
                if (originalStack > 0 && item.stack < originalStack) {
                    float ratio = item.stack / (float)originalStack; // 0~1
                    int barWidth = (int)(size * ratio);
                    Rectangle bar = new((int)dim.X, (int)dim.Y + size - 6, barWidth, 4);
                    sb.Draw(TextureAssets.MagicPixel.Value, bar, Color.Lime);
                }
            }

            /* ---------- 鼠标交互 ---------- */
            if (ContainsPoint(Main.MouseScreen) &&
                Main.mouseLeft && Main.mouseLeftRelease) {
                Main.mouseLeftRelease = false;
                Utils.Swap(ref item, ref Main.mouseItem);

                // 重新放入时重置初始堆叠计数
                originalStack = item.IsAir ? 0 : item.stack;
            }

            if (IsMouseHovering)
                Main.LocalPlayer.mouseInterface = true;
        }
    }
}
