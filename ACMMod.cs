global using InnoVault;
global using Microsoft.Xna.Framework;
using AncientChineseMythology.Celestias.Boss.Aokins;
using AncientChineseMythology.NPCs.Boss.Hanbas;
using AncientChineseMythology.NPCs.Boss.Hoqings;
using AncientChineseMythology.NPCs.Boss.Jiangcens;
using AncientChineseMythology.NPCs.Boss.Yingous;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;


namespace AncientChineseMythology
{
    public class ACMMod : Mod
    {
        internal static List<IACMLoader> ILoaders { get; private set; } = [];
        public override void Load() {
            ILoaders = VaultUtils.GetDerivedInstances<IACMLoader>();
            foreach (var load in ILoaders) {
                load.LoadData();
            }
            if (VaultUtils.isServer) {
                return;
            }
            HanbaSky.LoadInstance();
            HoqingSky.LoadInstance();
            YingouSky.LoadInstance();
            JiangcenSky.LoadInstance();
            AokinSky.LoadInstance();
        }
        public override void PostSetupContent() {
            foreach (var load in ILoaders) {
                load.SetupData();
                if (!Main.dedServ) {
                    load.LoadAsset();
                }
            }
        }
        public override void Unload() {
            foreach (var load in ILoaders) {
                load.UnLoadData();
            }
        }
        public override void HandlePacket(BinaryReader reader, int whoAmI) => ACMNetWork.HandlePacket(reader, whoAmI);
    }
}
