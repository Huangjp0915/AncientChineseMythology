using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙召唤物品
    /// </summary>
    public class NetherDragonSummonItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(0, 1, 0, 0);
        }

        public override bool CanUseItem(Player player)
        {
            // 检查是否已经存在Boss
            return !NPC.AnyNPCs(ModContent.NPCType<NetherDragonHead>());
        }

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI == Main.myPlayer)
            {
                // 播放召唤音效
                SoundEngine.PlaySound(SoundID.Roar, player.position);

                // 在玩家上方生成幽冥龙
                int npcID = NPC.NewNPC(
                    player.GetSource_ItemUse(Item),
                    (int)player.Center.X,
                    (int)player.Center.Y - 300,
                    ModContent.NPCType<NetherDragonHead>()
                );

                if (Main.netMode == NetmodeID.Server && npcID < Main.maxNPCs)
                {
                    NetMessage.SendData(MessageID.SyncNPC, number: npcID);
                }

                // 显示召唤文本
                Main.NewText("幽冥龙已苏醒！", new Color(100, 150, 255));
            }

            return true;
        }
    }
}
