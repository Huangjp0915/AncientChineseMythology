using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.NPCs.Boss.BlackBear;
using AncientChineseMythology.NPCs.Monsters;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace AncientChineseMythology.Global
{
    public class YaoQiFragmentGlobalNPC : GlobalNPC
    {
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot) {
            // 如果愤怒的石像鬼（AngryGargoyle）被击杀，设置掉落
            if (npc.type == ModContent.NPCType<angry_gargoyle>()) {
                // 这里的 Common() 第一个参数是物品，第二个参数是掉落几率分母，第三/第四个参数是最小/最大掉落数量
                // 例如：1/4 几率掉落 1~2 个
                npcLoot.Add(ItemDropRule.Common(
                    ModContent.ItemType<YaoQiFragment>(),
                    10, // 掉落几率 1/4
                    1, // 最小数量
                    3  // 最大数量
                ));
            }

            // 如果黑熊精（HeiXiongJing）被击杀，设置掉落
            if (npc.type == ModContent.NPCType<BlackBear>()) {
                // Boss 通常可设置更高的掉落数量，或保证掉落
                // 这里示例：100% 掉落 5~10 个
                npcLoot.Add(ItemDropRule.Common(
                    ModContent.ItemType<YaoQiFragment>(),
                    1,  // 掉落几率 1/1 = 100%
                    5,  // 最小数量
                    5  // 最大数量
                ));
            }

            if (npc.type == ModContent.NPCType<BloodCrow>()) {
                npcLoot.Add(ItemDropRule.Common(
                    ModContent.ItemType<YaoQiFragment>(),
                    10, // 掉落几率 1/10
                    1, // 最小数量
                    2  // 最大数量
                ));
            }

            if (npc.type == ModContent.NPCType<ChangGhost>()) {
                npcLoot.Add(ItemDropRule.Common(
                    ModContent.ItemType<YaoQiFragment>(),
                    10, // 掉落几率 1/10
                    1, // 最小数量
                    2  // 最大数量
                ));
            }

            if (npc.type == ModContent.NPCType<Demon>()) {
                npcLoot.Add(ItemDropRule.Common(
                    ModContent.ItemType<YaoQiFragment>(),
                    10, // 掉落几率 1/10
                    1, // 最小数量
                    2  // 最大数量
                ));
            }

            if (npc.type == ModContent.NPCType<JiaoSha>()) {
                npcLoot.Add(ItemDropRule.Common(
                    ModContent.ItemType<YaoQiFragment>(),
                    10, // 掉落几率 1/10
                    1, // 最小数量
                    2  // 最大数量
                ));
            }

            if (npc.type == ModContent.NPCType<MingCrow>()) {
                npcLoot.Add(ItemDropRule.Common(
                    ModContent.ItemType<YaoQiFragment>(),
                    10, // 掉落几率 1/10
                    1, // 最小数量
                    2  // 最大数量
                ));
            }
        }
    }
}
