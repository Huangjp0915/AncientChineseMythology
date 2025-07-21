using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using static AncientChineseMythology.AncientChineseMythology;

namespace AncientChineseMythology.Players
{
    public class GrowthPlayer : ModPlayer
    {
        public float growthBonus = 0f;
        public List<int> growthEnemies = new List<int>();

        public override void ResetEffects() {
            // 如果需要每帧重置某些数据，可以放这里
        }

        public override void SaveData(TagCompound tag) {
            tag["growthBonus"] = growthBonus;
            tag["growthEnemies"] = growthEnemies;
        }

        public override void LoadData(TagCompound tag) {
            growthBonus = tag.GetFloat("growthBonus");
            growthEnemies = tag.Get<List<int>>("growthEnemies");
        }

        // 重写 SyncPlayer 方法进行数据同步
        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            // 创建一个 ModPacket 发送数据
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)AncientChineseMythologyMessageType.SyncGrowthPlayer);
            packet.Write(Player.whoAmI);
            packet.Write(growthBonus);
            packet.Write(growthEnemies.Count);
            foreach (int enemy in growthEnemies) {
                packet.Write(enemy);
            }
            packet.Send(toWho, fromWho);
        }
    }
}
