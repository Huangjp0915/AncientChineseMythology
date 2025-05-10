using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent.UI.Elements;

namespace AncientChineseMythology.UI
{
    public class BaGuaUI : UIState
    {
        /* ===== 可调参数 ===== */
        private const int   SLOT    = 40;     // 每格尺寸
        private const float marginX = -350f;  // 初始偏移
        private const float marginY = -265f;
        private const float RADIUS  = 68f;    // 环半径
        private const float RING_SHIFT_X = 6f;   // 正值 → 向右移
        private const float RING_SHIFT_Y = 3f;   // 正值 → 向下移
        public UIItemSlot[] Slots { get; private set; }
        /* =================== */

        private UIElement root;               // 整块可拖动面板
        private bool   dragging;
        private Vector2 dragOffset;
        private bool   prevMouseLeft;         // 记住上一帧鼠标状态

        /* ---------- 初始化 ---------- */
        public override void OnInitialize()
        {
            var bgTex = ModContent
                .Request<Texture2D>("AncientChineseMythology/Textures/UI/BaGuaUIBack",
                                    AssetRequestMode.ImmediateLoad)
                .Value;

            root = new UIElement();
            root.Width .Set(bgTex.Width , 0);
            root.Height.Set(bgTex.Height, 0);
            Append(root);

            root.Append(new UIImage(bgTex));

            Vector2 c = new(bgTex.Width / 2f, bgTex.Height / 2f);
            Slots = new UIItemSlot[8];
            for (int i = 0; i < 8; i++)
            {
                double ang = MathHelper.ToRadians(i * 45 - 90);
                Vector2 p = c + new Vector2(RING_SHIFT_X, RING_SHIFT_Y) 
                                    + RADIUS * new Vector2(
                                        (float)System.Math.Cos(ang),
                                        (float)System.Math.Sin(ang));

                var slot = new UIItemSlot(SLOT);
                slot.Left.Set(p.X - SLOT / 2f, 0);
                slot.Top .Set(p.Y - SLOT / 2f, 0);
                Slots[i] = slot;
                root.Append(slot);
            }

            PlaceAtInitial();                // ★ 首帧精准定位
        }

        public override void OnActivate() => PlaceAtInitial();

        /* ---------- 每帧处理拖拽 ---------- */
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            bool mouseNow = Main.mouseLeft;

            /* 检测“刚按下” —— 当前为 true 且上一帧为 false */
            if (!prevMouseLeft && mouseNow &&
                root.ContainsPoint(Main.MouseScreen))
            {
                dragging   = true;
                dragOffset = Main.MouseScreen - root.GetDimensions().Position();
                Main.LocalPlayer.mouseInterface = true;
            }

            /* 拖动中 */
            if (dragging)
            {
                if (!mouseNow)               // 松手结束
                    dragging = false;
                else
                {
                    root.Left.Set(Main.mouseX - dragOffset.X, 0);
                    root.Top .Set(Main.mouseY - dragOffset.Y, 0);
                    root.Recalculate();
                }
            }

            prevMouseLeft = mouseNow;        // 记录状态供下一帧比较
        }

        /* ---------- 初次放置 ---------- */
        private void PlaceAtInitial()
        {
            root.Left.Set(Main.screenWidth  * 0.5f + marginX - root.Width.Pixels  * 0.5f, 0);
            root.Top .Set(Main.screenHeight * 0.5f + marginY - root.Height.Pixels * 0.5f, 0);
            root.Recalculate();
        }

        /* 把玩家数据写进 UI */
        public void LoadFromPlayer(Player player)
        {
            var modPlr = player.GetModPlayer<Players.BaGuaPlayer>();
            for (int i = 0; i < Players.BaGuaPlayer.SlotCount; i++)
                Slots[i].item = modPlr.BaGuaItems[i].Clone();
        }

        /* 把 UI 当前物品写回玩家 */
        public void SaveToPlayer(Player player)
        {
            var modPlr = player.GetModPlayer<Players.BaGuaPlayer>();
            for (int i = 0; i < Players.BaGuaPlayer.SlotCount; i++)
            {
                modPlr.BaGuaItems[i] = Slots[i].item.Clone();
                modPlr.ResetWear(i); 
            }
        }
    }
}
