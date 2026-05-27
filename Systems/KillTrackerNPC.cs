using AncientChineseMythology.Celestias.Boss.CelestialDragons;
using AncientChineseMythology.NPCs.Boss.AzureDragons;
using AncientChineseMythology.NPCs.Boss.KyuubiKitsunes;
using AncientChineseMythology.Players;
using AncientChineseMythology.Underworlds.Boss.YinEmperors;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Systems;

public class KillTrackerNPC : GlobalNPC
{
    public override void OnKill(NPC npc) {
        MarkBossDowned(npc);

        int killer = npc.lastInteraction;
        if (killer >= 0 && killer < Main.maxPlayers && Main.player[killer].active) {
            Player player = Main.player[killer];
            player.GetModPlayer<MythologyPlayer>().RecordKill(npc);
            player.GetModPlayer<ZodiacPityPlayer>().OnMobKill(npc);
        }
    }

    private static void MarkBossDowned(NPC npc) {
        if (npc.type == ModContent.NPCType<KyuubiKitsune>()) {
            DownedBossSystem.downedKyuubi = true;
        }
        else if (npc.type == ModContent.NPCType<CelestialDragonsHead>()) {
            DownedBossSystem.downedCelestialDragon = true;
        }
        else if (npc.type == ModContent.NPCType<AzureDragonHead>()) {
            DownedBossSystem.downedAzureDragon = true;
        }
        else if (npc.type == ModContent.NPCType<YinEmperor>()) {
            DownedBossSystem.downedYinEmperor = true;
        }
    }
}
