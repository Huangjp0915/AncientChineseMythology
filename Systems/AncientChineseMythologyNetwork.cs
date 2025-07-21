using AncientChineseMythology.Systems;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology
{
    public class AncientChineseMythologyNetwork : ModSystem
    {
        internal enum MessageType : byte
        {
            SkyKeyUnlock,
            SyncBaGuaSlot
        }
    }
}
