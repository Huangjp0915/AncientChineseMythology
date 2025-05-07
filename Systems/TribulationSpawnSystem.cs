using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;

namespace AncientChineseMythology.Systems
{
    public class TribulationSpawnSystem : ModSystem
    {
        private const int DelayTicks = 900;        // 5 秒（60 tick = 1 秒）
        private int  timer  = 0;
        private int  npcTypePending = 0;           // 要生成的云体类型
        private int  playerWhoAmI  = -1;           // 记录发起玩家

        /// <summary>由按钮调用：请求在 delay 后生成 BOSS</summary>
        public void RequestSpawn(int playerID, int npcType)
        {
            timer           = DelayTicks;
            npcTypePending  = npcType;
            playerWhoAmI    = playerID;
        }

        public override void PostUpdatePlayers()
        {
            if (timer <= 0 || npcTypePending == 0) return;

            timer--;
            if (timer == 0)
            {
                Player p = Main.player[playerWhoAmI];
                if (!p.active) { npcTypePending = 0; return; }

                IEntitySource src = p.GetSource_FromThis();
                int idx = NPC.NewNPC(src,
                    (int)p.Center.X, (int)(p.Center.Y - 800),
                    npcTypePending);

                if (idx >= 0 && Main.netMode == NetmodeID.MultiplayerClient)
                    NetMessage.SendData(MessageID.SyncNPC, number: idx);

                npcTypePending = 0;                // 清状态
            }
        }
    }
}
