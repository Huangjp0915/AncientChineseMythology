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
                //以 1/1000 的概率掉落 StrangeStone
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<StrangeStone>(), 10000));
            }
        }
    }
}
