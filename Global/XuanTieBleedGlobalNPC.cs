using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Global
{
    public class XuanTieBleedGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (npc.HasBuff(ModContent.BuffType<Buffs.XuanTieBleed>())) {
                if (npc.lifeRegen > 0) npc.lifeRegen = 0;
                npc.lifeRegen -= 2;      // −2 → 1 HP/秒
                if (damage < 1) damage = 1;
            }
        }
    }
}