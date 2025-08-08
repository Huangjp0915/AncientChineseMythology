using AncientChineseMythology.UI;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace AncientChineseMythology.Systems;

public class MythologyUISystem : ModSystem
{
    //internal static ModKeybind ToggleKey;
    private UserInterface _ui;
    private MythologySidebar _sidebar;

    public override void Load() {
        if (Main.dedServ) return;

        //ToggleKey = KeybindLoader.RegisterKeybind(Mod, "Toggle Cultivation Panel", "P");

        _ui = new UserInterface();

        _sidebar = new MythologySidebar();
        _sidebar.Activate();

        _ui.SetState(_sidebar);
    }

    public override void UpdateUI(GameTime gameTime) => _ui?.Update(gameTime);

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
        int index = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
        if (index != -1) {
            layers.Insert(index, new LegacyGameInterfaceLayer(
                "AncientChineseMythology: Cultivation Sidebar",
                () => { _ui.Draw(Main.spriteBatch, new GameTime()); return true; },
                InterfaceScaleType.UI));
        }
    }

    //public override void PostUpdateInput() {
    //    if (ToggleKey.JustPressed)
    //        _sidebar.Toggle();
    //}
}
