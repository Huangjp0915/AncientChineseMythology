using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent.ItemDropRules;
using AncientChineseMythology.Items;

namespace AncientChineseMythology.Global
{
    public class StrangeStoneGlobalNPC : GlobalNPC
    {
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            // 排除友好 NPC 或生命值很低的NPC（例如城镇NPC）
            if (!npc.friendly && npc.lifeMax > 5)
            {
                // 以 1/10000 的概率掉落 StrangeStone
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<StrangeStone>(), 10000));
            }
        }
    }
}
