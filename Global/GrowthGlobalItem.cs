using Terraria;
using Terraria.ModLoader;
using Terraria.Utilities;
using AncientChineseMythology.Players;
using AncientChineseMythology.Items;

namespace AncientChineseMythology
{
    public class GrowthGlobalItem : GlobalItem
    {
        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
        {
            if (item.ModItem is GrowthWeapon gw && gw.IsGrowthWeapon)
            {
                GrowthPlayer modPlayer = player.GetModPlayer<GrowthPlayer>();
                damage *= 1f + modPlayer.growthBonus;
            }
        }
    }
}
