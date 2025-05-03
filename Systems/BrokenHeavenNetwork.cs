using System.IO;
using AncientChineseMythology.Systems;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ModLoader.Utilities;

namespace AncientChineseMythology
{
    public class BrokenHeavenNetwork : ModSystem
    {
        public static void SendSkyKeyUnlock()
        {
            Player pl = Main.LocalPlayer;
            AncientChineseMythology.SendSkyKeyUnlock(pl.whoAmI);  // 直接转发
        }
    }
}
