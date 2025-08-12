global using InnoVault;
global using Microsoft.Xna.Framework;
using AncientChineseMythology.NPCs.Boss.Hanbas;
using AncientChineseMythology.NPCs.Boss.Hoqings;
using AncientChineseMythology.NPCs.Boss.Jiangcens;
using AncientChineseMythology.NPCs.Boss.Yingous;
using System.IO;
using Terraria.ModLoader;


namespace AncientChineseMythology
{
    public class AncientChineseMythology : Mod
    {
        public override void Load() {
            if (VaultUtils.isServer) {
                return;
            }
            HanbaSky.LoadInstance();
            HoqingSky.LoadInstance();
            YingouSky.LoadInstance();
            JiangcenSky.LoadInstance();
        }

        public override void HandlePacket(BinaryReader reader, int whoAmI) => ACMNetWork.HandlePacket(reader, whoAmI);
    }
}
