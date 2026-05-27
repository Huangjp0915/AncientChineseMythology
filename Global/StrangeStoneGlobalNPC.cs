using AncientChineseMythology.Items.Materials;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace AncientChineseMythology.Global
{
    public class StrangeStoneGlobalNPC : GlobalNPC
    {
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot) {
            //排除友好 NPC 或生命值很低的NPC（例如城镇NPC）
            if (!npc.friendly && npc.lifeMax > 5) {
                //  interim rebalance: 1/8000（目标 1/800 + pity）
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<StrangeStone>(), 8000));
            }
        }
    }
}
