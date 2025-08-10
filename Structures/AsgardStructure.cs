using InnoVault.GameSystem;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AncientChineseMythology.Structures
{
    internal class AsgardStructure : SaveStructure
    {
        public override string SavePath => Path.Combine(VaultSave.RootPath, "Structure", "AncientChineseMythology", "AsgardStructure_v1.nbt");
        public Point16 Point;
        public override void Load() => Mod.EnsureFileFromMod("Structures/AsgardStructure_v1.nbt", SavePath);

        public override void SaveData(TagCompound tag) { }

        public override void LoadData(TagCompound tag) {
            LoadRegion(tag, Point);
            TagCache.Invalidate(SavePath);

            if (!VaultUtils.isServer) {//只可能让服务器生成这个东西
                return;
            }

            ModPacket modPacket = Mod.GetPacket();
            modPacket.Write((byte)NetID.AsgardStructure);
            modPacket.WritePoint16(Point);
            modPacket.Send();
        }
    }

    internal class SpwanAsgardStructure : GlobalNPC
    {
        public override void OnKill(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }

            if (NPC.downedMoonlord) {
                return;
            }

            if (npc.type != NPCID.MoonLordCore) {
                return;
            }

            AsgardStructure.GetInstance<AsgardStructure>().Point = new Point16(WorldGen.genRand.Next(Main.maxTilesX), WorldGen.genRand.Next(20));
            AsgardStructure.DoLoad<AsgardStructure>();
            NPC.downedMoonlord = true;
        }
    }
}
