using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace AncientChineseMythology.Items.Weapons.SoulBanners
{
    /// <summary>
    /// 万魂幡 UI 生命周期管理 ——
    /// · 按住 Shift + 手持万魂幡 → 显示面板
    /// · 松开 Shift 或切换武器 → 隐藏面板
    /// </summary>
    [Autoload(Side = ModSide.Client)]
    public class SoulBannerUISystem : ModSystem
    {
        private UserInterface _userInterface;
        private SoulBannerUI _uiState;

        public override void Load()
        {
            _uiState = new SoulBannerUI();
            _uiState.Activate();
            _userInterface = new UserInterface();
            _userInterface.SetState(_uiState);
        }

        public override void Unload()
        {
            _uiState = null;
            _userInterface = null;
        }

        public override void UpdateUI(GameTime gameTime)
        {
            // 判定条件：按住 Shift + 手持万魂幡
            bool shouldShow = false;

            Player player = Main.LocalPlayer;
            if (player.active && !player.dead
                && player.HeldItem != null
                && player.HeldItem.type == ModContent.ItemType<SoulBanner>())
            {
                KeyboardState kb = Keyboard.GetState();
                if (kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift))
                    shouldShow = true;
            }

            _uiState.Visible = shouldShow;
            _userInterface?.Update(gameTime);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
            if (index != -1)
            {
                layers.Insert(index, new LegacyGameInterfaceLayer(
                    "AncientChineseMythology: SoulBanner Growth Panel",
                    () =>
                    {
                        _userInterface.Draw(Main.spriteBatch, new GameTime());
                        return true;
                    },
                    InterfaceScaleType.UI));
            }
        }
    }
}
