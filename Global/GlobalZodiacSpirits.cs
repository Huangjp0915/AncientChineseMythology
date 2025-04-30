using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System.Collections.Generic;

namespace AncientChineseMythology
{
    // 全局 NPC 类，用于统一处理怪物死亡掉落
    public class GlobalZodiacSpirits : GlobalNPC
    {
        // 每个 NPC 拥有独立实例数据（根据需求可保留或移除）
        public override bool InstancePerEntity => true;

        // 当 NPC 死亡掉落时调用
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            // 万分之一掉落概率 (1/10000)
            if (Main.rand.Next(10000) < 1)
            {
                // 定义 12 个生肖精魄物品的 ID 数组
                int[] zodiacSpiritIDs = new int[]
                {
                    ModContent.ItemType<Items.ZodiacRat>(),
                    ModContent.ItemType<Items.ZodiacCow>(),
                    ModContent.ItemType<Items.ZodiacTiger>(),
                    ModContent.ItemType<Items.ZodiacRabbit>(),
                    ModContent.ItemType<Items.ZodiacDragon>(),
                    ModContent.ItemType<Items.ZodiacSnake>(),
                    ModContent.ItemType<Items.ZodiacHorse>(),
                    ModContent.ItemType<Items.ZodiacGoat>(),
                    ModContent.ItemType<Items.ZodiacMonkey>(),
                    ModContent.ItemType<Items.ZodiacChicken>(),
                    ModContent.ItemType<Items.ZodiacDog>(),
                    ModContent.ItemType<Items.ZodiacPig>()
                };

                // 随机选择一个生肖精魄
                int dropItemID = zodiacSpiritIDs[Main.rand.Next(zodiacSpiritIDs.Length)];

                // 在 NPC 的位置生成物品掉落，并传入掉落来源（兼容多人联机）
                Item.NewItem(npc.GetSource_Loot(), npc.getRect(), dropItemID);
            }
        }
    }
}
