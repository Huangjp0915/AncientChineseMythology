using AncientChineseMythology.Buffs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology
{
    public class XuePoDanDebuffPlayer : ModPlayer
    {
        public int xuePoTimer = 0;

        public override void ResetEffects() {
            // 若没有该 debuff，则重置计时器
            if (!Player.HasBuff(ModContent.BuffType<XuePoDanDebuff>())) {
                xuePoTimer = 0;
            }
        }

        public override void PostUpdate() {
            if (Player.HasBuff(ModContent.BuffType<XuePoDanDebuff>())) {
                xuePoTimer++;
                if (xuePoTimer >= 60) // 每秒扣1 HP
                {
                    xuePoTimer = 0;
                    // 防止扣血致死（至少保留1点生命）
                    if (Player.statLife > 1) {
                        Player.statLife -= 5;
                        CombatText.NewText(Player.Hitbox, Color.Red, "5");
                    }
                }
            }
        }
    }
}
