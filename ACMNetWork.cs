using AncientChineseMythology.Structures;
using System.IO;
using Terraria;
using Terraria.DataStructures;

namespace AncientChineseMythology
{
    internal enum NetID : byte
    {
        AsgardStructure,
    }

    internal class ACMNetWork
    {
        public static void HandlePacket(BinaryReader reader, int whoAmI) {
            NetID netID = (NetID)reader.ReadByte();
            if (netID == NetID.AsgardStructure) {
                Point16 point = reader.ReadPoint16();
                if (!VaultUtils.isClient) {
                    return;//首先这个结构只能在客户端上生成，由服务器向所有客户端广播
                }
                AsgardStructure.GetInstance<AsgardStructure>().Point = point;
                AsgardStructure.DoLoad<AsgardStructure>();
            }
        }
    }
}
