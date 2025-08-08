using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace AncientChineseMythology.UI
{
    public class BaGuaUISystem : ModSystem
    {
        internal static BaGuaUI uiInstance;
        internal static UserInterface userInterface;
        private static bool visible;

        public override void Load() {
            if (!Main.dedServ) {
                uiInstance = new BaGuaUI();
                userInterface = new UserInterface();
                userInterface.SetState(uiInstance);
            }
        }

        public override void UpdateUI(GameTime gameTime) {
            if (visible && !Main.playerInventory) {
                uiInstance.SaveToPlayer(Main.LocalPlayer);
                visible = false;
            }

            if (visible) {
                userInterface?.Update(gameTime);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            int inventoryIdx = layers.FindIndex(l => l.Name.Equals("Vanilla: Inventory"));
            if (inventoryIdx != -1) {
                layers.Insert(inventoryIdx + 1, new LegacyGameInterfaceLayer(
                    "AncientChineseMythology: BaGuaUI",
                    () => {
                        if (visible)
                            userInterface.Draw(Main.spriteBatch, new GameTime());
                        return true;
                    },
                    InterfaceScaleType.UI));
            }
        }

        public static void Toggle(Player player) {
            visible = !visible;
            if (visible) {
                Main.playerInventory = true;
                uiInstance.LoadFromPlayer(player);   // 打开时读取
            }
            else {
                uiInstance.SaveToPlayer(player);     // 关闭时保存
            }
        }

        public override void OnWorldUnload() {
            if (uiInstance?.Slots != null) {
                uiInstance.SaveToPlayer(Main.LocalPlayer);
                foreach (var s in uiInstance.Slots)
                    s.item.TurnToAir();
            }
        }
    }
}
