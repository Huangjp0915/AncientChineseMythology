using AncientChineseMythology.Buffs;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.GlobalItems
{
    public class PoJunDanGlobalItem : GlobalItem
    {
        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
        {
            if (player.HasBuff(ModContent.BuffType<PoJunDanBuff>()))
            {
                // 正面效果：伤害提升 30%
                damage *= 1.3f;
            }
            else if (player.HasBuff(ModContent.BuffType<PoJunDanDebuff>()))
            {
                // 后遗症：伤害降低 50%
                damage *= 0.5f;
            }
        }
    }
}
