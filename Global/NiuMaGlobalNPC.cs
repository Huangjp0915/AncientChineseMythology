using AncientChineseMythology.NPCs.Boss.NiutouMamian;
using AncientChineseMythology.Systems;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Global
{
    public class NiuMaGlobalNPC : GlobalNPC
    {
        public override void OnKill(NPC npc) {
            if (npc.type != ModContent.NPCType<NiuTou>() && npc.type != ModContent.NPCType<MaMian>()) {
                return;
            }
            if (NPC.AnyNPCs(ModContent.NPCType<NiuTou>()) || NPC.AnyNPCs(ModContent.NPCType<MaMian>())) {
                return;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            DownedBossSystem.downedNiuMa = true;
        }
    }
}
