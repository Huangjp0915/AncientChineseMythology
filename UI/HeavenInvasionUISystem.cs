using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace AncientChineseMythology.UI
{
    /// <summary>
    /// 天庭入侵UI系统——管理入侵进度条的显示与隐藏
    /// 入侵激活时自动显示，结束后自动淡出
    /// </summary>
    public class HeavenInvasionUISystem : ModSystem
    {
        internal static HeavenInvasionUI uiInstance;
        internal static UserInterface userInterface;

        public override void Load() {
            if (!Main.dedServ) {
                uiInstance = new HeavenInvasionUI();
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
            // 入侵激活时始终更新UI（包括淡出动画）
            userInterface?.Update(gameTime);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            // 插入到 "Vanilla: Mouse Text" 之前，确保在所有游戏内容之上但鼠标文字之下
            int mouseTextIdx = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIdx != -1) {
                layers.Insert(mouseTextIdx, new LegacyGameInterfaceLayer(
                    "AncientChineseMythology: HeavenInvasionUI",
                    () => {
                        userInterface?.Draw(Main.spriteBatch, new GameTime());
                        return true;
                    },
                    InterfaceScaleType.UI));
            }
        }
    }
}
