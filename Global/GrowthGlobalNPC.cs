using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using AncientChineseMythology.Players;
using AncientChineseMythology.Items;
using Terraria.ID;

    namespace AncientChineseMythology
    {
        public class GrowthGlobalNPC : GlobalNPC
        {
            public override void OnKill(NPC npc)
            {
                // 仅在服务器上更新数据（调试时可以暂时注释掉这行，确认方法是否被调用）
                if (Main.netMode == NetmodeID.MultiplayerClient)
                    return;


                int playerIndex = npc.lastInteraction;
                if (playerIndex < 0 || playerIndex >= Main.maxPlayers)
                    return;

                Player player = Main.player[playerIndex];
                if (!npc.friendly && npc.lifeMax > 5)
                {
                    // 暂时取消 HeldItem 检查
                    // if (player.HeldItem != null && player.HeldItem.ModItem is GrowthWeapon gw && gw.IsGrowthWeapon)
                    {
                        GrowthPlayer modPlayer = player.GetModPlayer<GrowthPlayer>();
                        if (!modPlayer.growthEnemies.Contains(npc.type))
                        {
                            modPlayer.growthEnemies.Add(npc.type);
                            modPlayer.growthBonus += 0.01f; // 增加 1%
                            Main.NewText("武器成长+1%，当前加成：" + (modPlayer.growthBonus * 100f).ToString("F0") + "%", Microsoft.Xna.Framework.Color.LimeGreen);

                            // 同步更新到所有客户端
                            modPlayer.SyncPlayer(-1, player.whoAmI, false);
                        }
                    }
                }
            }

        }
    }