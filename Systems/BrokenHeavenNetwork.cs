using AncientChineseMythology.Systems;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology
{
    public class BrokenHeavenNetwork : ModSystem
    {
        internal enum MessageType : byte {
            SkyKeyUnlock
        }
        
        public static void SendSkyKeyUnlock(Player player) {
            if (Main.netMode == NetmodeID.SinglePlayer) {
                BrokenHeavenIslandSystem.OpenSkyIsland(player.whoAmI);
                return;
            }
            var mod = ModContent.GetInstance<AncientChineseMythology>();
            ModPacket p = mod.GetPacket();
            p.Write((byte)MessageType.SkyKeyUnlock);
            p.Write((byte)player.whoAmI);
            p.Send();
        }
    }
}
