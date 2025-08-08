using AncientChineseMythology.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace AncientChineseMythology.Systems
{
    public class ZhenfaUISystem : ModSystem
    {
        internal static ZhenfaBookUI BookUI;
        private static UserInterface _interface;
        internal static bool ShowBookUI;

        public override void Load() {
            if (Main.dedServ) return;          //服务器端不加载 UI

            BookUI = new ZhenfaBookUI();    //仅实例化
            _interface = new UserInterface();  //尚未 SetState
        }

        public override void Unload() {
            BookUI = null;
            _interface = null;
        }

        public override void UpdateUI(GameTime gameTime) {
            if (ShowBookUI)
                _interface?.Update(gameTime);
        }

        public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers) {
            int idx = layers.FindIndex(l => l.Name.Equals("Vanilla: Cursor"));
            if (idx != -1 && ShowBookUI) {
                layers.Insert(idx, new LegacyGameInterfaceLayer(
                    "洪荒: ZhenfaBookUI",
                    () => {
                        _interface.Draw(Main.spriteBatch, new GameTime());
                        return true;
                    },
                    InterfaceScaleType.UI));
            }
        }

        /* ---------- Toggle ---------- */
        public static void ToggleBookUI() {
            ShowBookUI = !ShowBookUI;

            if (ShowBookUI) {
                Main.playerInventory = false;

                //★ 先 Activate → 创建所有 UIElement，避免 _list 空引用
                BookUI.Activate();

                _interface.SetState(BookUI);
                BookUI.RebuildList();           //现在安全调用

                Main.LocalPlayer.mouseInterface = true;
            }
            else {
                _interface.SetState(null);
            }

            SoundEngine.PlaySound(SoundID.MenuOpen, Main.LocalPlayer.Center);
        }
    }
}
