using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace AncientChineseMythology.UI
{
    /// <summary>
    /// 地府入侵UI系统——管理入侵进度条的显示与隐藏
    /// 入侵激活时自动显示，结束后自动淡出
    /// </summary>
    public class UnderworldInvasionUISystem : ModSystem
    {
        internal static UnderworldInvasionUI uiInstance;
        internal static UserInterface userInterface;

        public override void Load() {
            if (!Main.dedServ) {
                uiInstance = new UnderworldInvasionUI();
                uiInstance.Activate();
                userInterface = new UserInterface();
                userInterface.SetState(uiInstance);
            }
        }

        public override void Unload() {
            uiInstance = null;
            userInterface = null;
        }

        public override void UpdateUI(GameTime gameTime) {
            userInterface?.Update(gameTime);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            int mouseTextIdx = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIdx != -1) {
                layers.Insert(mouseTextIdx, new LegacyGameInterfaceLayer(
                    "AncientChineseMythology: UnderworldInvasionUI",
                    () => {
                        userInterface?.Draw(Main.spriteBatch, new GameTime());
                        return true;
                    },
                    InterfaceScaleType.UI));
            }
        }
    }
}
