using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.Weapons.NiuMa;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.NiutouMamian
{
    internal static class NiuMaLoot
    {
        public static void AddBossLoot(NPCLoot npcLoot, int partnerNpcType) {
            LeadingConditionRule partnerDead = new LeadingConditionRule(new NiuMaPartnerDead(partnerNpcType));
            partnerDead.OnSuccess(ItemDropRule.Common(ItemID.GoldCoin, 1, 8, 15));
            partnerDead.OnSuccess(ItemDropRule.Common(ModContent.ItemType<NiuTouSeal>(), 1, 2, 4));
            partnerDead.OnSuccess(ItemDropRule.Common(ModContent.ItemType<MaMianSeal>(), 1, 2, 4));
            partnerDead.OnSuccess(ItemDropRule.Common(ModContent.ItemType<NetherChainBlade>(), 2));
            partnerDead.OnSuccess(ItemDropRule.Common(ModContent.ItemType<SoulHookWhip>(), 2));
            npcLoot.Add(partnerDead);
        }
    }

    internal sealed class NiuMaPartnerDead : IItemDropRuleCondition, IProvideItemConditionDescription
    {
        private readonly int _partnerType;

        public NiuMaPartnerDead(int partnerType) {
            _partnerType = partnerType;
        }

        public bool CanDrop(DropAttemptInfo info) => !NPC.AnyNPCs(_partnerType);

        public bool CanShowItemDropInUI() => true;

        public string GetConditionDescription() => null;
    }
}
