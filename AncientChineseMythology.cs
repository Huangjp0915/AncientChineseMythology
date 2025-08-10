global using InnoVault;
global using Microsoft.Xna.Framework;
using AncientChineseMythology.NPCs.Boss.Hanbas;
using AncientChineseMythology.NPCs.Boss.Hoqings;
using System.IO;
using Terraria.ModLoader;


namespace AncientChineseMythology
{
    //Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
    public class AncientChineseMythology : Mod
    {
        public enum AncientChineseMythologyMessageType : byte
        {
            SyncGrowthPlayer
        }

        public override void Load() {
            if (VaultUtils.isServer) {
                return;
            }
            HanbaSky.LoadInstance();
            HoqingSky.LoadInstance();
        }

        public override void HandlePacket(BinaryReader reader, int whoAmI) => ACMNetWork.HandlePacket(reader, whoAmI);
    }
}
