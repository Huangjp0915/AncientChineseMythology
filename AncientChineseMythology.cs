using System.IO;
using Terraria;
using Terraria.ModLoader;


namespace AncientChineseMythology
{
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class AncientChineseMythology : Mod
	{
		public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
            AncientChineseMythologyMessageType msgType = (AncientChineseMythologyMessageType)reader.ReadByte();
            switch (msgType)
            {
                case AncientChineseMythologyMessageType.SyncGrowthPlayer:
                    {
                        int playerID = reader.ReadInt32();
                        float bonus = reader.ReadSingle();
                        int count = reader.ReadInt32();
                        var enemyList = new System.Collections.Generic.List<int>();
                        for (int i = 0; i < count; i++)
                        {
                            enemyList.Add(reader.ReadInt32());
                        }
                        // 获取对应的 GrowthPlayer 并更新数据
                        if (playerID >= 0 && playerID < Main.maxPlayers)
                        {
                            var modPlayer = Main.player[playerID].GetModPlayer<Players.GrowthPlayer>();
                            modPlayer.growthBonus = bonus;
                            modPlayer.growthEnemies = enemyList;
                        }
                    }
                    break;
            }
        }

	}

	public enum AncientChineseMythologyMessageType : byte
    {
        SyncGrowthPlayer
    }
}
