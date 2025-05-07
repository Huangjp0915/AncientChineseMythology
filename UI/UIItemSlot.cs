using Terraria;
using Terraria.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using System;

namespace AncientChineseMythology.UI
{
    public class UIItemSlot : UIElement
    {
        public Item item = new();
        private readonly int size;                   // 方框目标像素
        private const int VANILLA = 56;              // 原版 InventoryBack 边长

        public UIItemSlot(int pixelSize)
        {
            size = pixelSize;
            item.TurnToAir();
            Width.Set(size, 0);
            Height.Set(size, 0);
        }

        protected override void DrawSelf(SpriteBatch sb)
        {
            CalculatedStyle dim = GetInnerDimensions();

            /* -------- 背景框 (按 SLOT 缩放) -------- */
            float backScale = size / (float)VANILLA; // e.g. 32/56 = 0.57
            sb.Draw(TextureAssets.InventoryBack.Value,
                    dim.Position(),
                    null,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    backScale,  
                    SpriteEffects.None,
                    0f);

            /* -------- 物品贴图 -------- */
            if (!item.IsAir)
            {
                Main.instance.LoadItem(item.type);
                Texture2D tex   = TextureAssets.Item[item.type].Value;
                Rectangle frame = tex.Bounds;

                // 让图标占方框 66% 大小
                float iconScale = (size * 0.66f) / Math.Max(frame.Width, frame.Height);
                Vector2 pos     = dim.Position() + new Vector2(size / 2f) - frame.Size() * iconScale / 2;
                sb.Draw(tex, pos, frame, Color.White, 0f, Vector2.Zero, iconScale, 0, 0);
            }

            /* -------- 点击交换 -------- */
            if (ContainsPoint(Main.MouseScreen) &&
                Main.mouseLeft && Main.mouseLeftRelease)
            {
                Main.mouseLeftRelease = false;
                Utils.Swap(ref item, ref Main.mouseItem);
            }

            if (IsMouseHovering)
                Main.LocalPlayer.mouseInterface = true;
        }
    }
}
