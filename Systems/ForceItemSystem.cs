using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace AncientChineseMythology.Commands
{
    public class SkyKeyCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat; // 设定命令类型为聊天命令

        public override string Command => "skykey";           // 命令名称

        public override string Description => "生成天界之钥";       // 命令描述

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            // 如果在服务器模式下执行，则提示错误信息
            if (Main.netMode == NetmodeID.Server)
            {
                caller.Reply("该指令需在游戏内使用", Color.Red);
                return;
            }

            // 获取本地玩家，并生成物品
            var player = Main.LocalPlayer;
            int itemType = ModContent.ItemType<Items.SkyKey>();
            player.QuickSpawnItem(player.GetSource_Misc("CMD"), itemType, 1);
            Main.NewText("[c/FFD700:天界之钥已发放！]", Color.Gold);
        }
    }
}
