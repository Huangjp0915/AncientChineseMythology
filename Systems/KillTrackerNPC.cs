using Terraria;
using Terraria.ModLoader;
using AncientChineseMythology.Players;

namespace AncientChineseMythology.Systems;

public class KillTrackerNPC : GlobalNPC
{
    public override void OnKill(NPC npc)
    {
        int killer = npc.lastInteraction;
        if (killer >= 0 && killer < Main.maxPlayers && Main.player[killer].active)
            Main.player[killer]
                .GetModPlayer<MythologyPlayer>()
                .RecordKill(npc);
    }
}
