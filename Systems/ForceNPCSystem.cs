using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using AncientChineseMythology.NPCs.Boss;
using Terraria.ID;
using AncientChineseMythology.NPCs;  // 引入你的 Boss 类所在命名空间

namespace AncientChineseMythology.Commands
{
    public class SpawnBlackBearCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "spawntangseng";
        public override string Usage => "/spawntangseng";
        public override string Description => "强制生成tangseng ，用于测试";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            Player player = caller.Player;
            IEntitySource source = player.GetSource_Misc("SpawnBlackBearCommand");
            int bossType = ModContent.NPCType<TangSengNPC>();

            // 仅在服务器端生成NPC并同步，SinglePlayer也走Server逻辑
            if (Main.netMode != NetmodeID.SinglePlayer && Main.netMode != NetmodeID.Server)
            {
                // 如果是客户端，则提示信息，但不真正生成
                caller.Reply("请在服务器端或单人游戏下使用该命令。", Microsoft.Xna.Framework.Color.Red);
                return;
            }

            // 在服务器/单人模式下，真正生成 Boss
            int npcIndex = NPC.NewNPC(source, (int)player.Center.X, (int)player.Center.Y, bossType);
            if (npcIndex < Main.maxNPCs)
            {
                Main.npc[npcIndex].netUpdate = true;
                // 同步给所有客户端
                NetMessage.SendData(MessageID.SyncNPC, number: npcIndex);

                caller.Reply("已生成黑熊精 Boss！", Microsoft.Xna.Framework.Color.LightGreen);
            }
            else
            {
                caller.Reply("Boss生成失败！", Microsoft.Xna.Framework.Color.Red);
            }
        }
    }
}
