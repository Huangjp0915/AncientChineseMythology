using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AncientChineseMythology.Systems
{
    public class PostMoonLordSystem : ModSystem
    {
        public static bool MoonLordDefeated = false;

        public override void Load()
        {
            // 正确的事件监听方式
            On_NPC.NPCLoot += OnNPCLoot;
        }

        public override void Unload()
        {
            On_NPC.NPCLoot -= OnNPCLoot;
        }

        private void OnNPCLoot(On_NPC.orig_NPCLoot orig, NPC npc)
        {
            orig(npc);
            if (npc.type == NPCID.MoonLordCore)
            {
                MoonLordDefeated = true;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.WorldData);
            }
        }

        public override void SaveWorldData(TagCompound tag)
        {
            tag["MoonLordDefeated"] = MoonLordDefeated;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            MoonLordDefeated = tag.GetBool("MoonLordDefeated");
        }
    }
}