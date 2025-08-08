using AncientChineseMythology.Buffs;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Players
{
    public class NingShenDanDebuffPlayer : ModPlayer
    {
        public int ningShenTimer = 0;

        public override void ResetEffects() {
            if (!Player.HasBuff(ModContent.BuffType<NingShenDanDebuff>())) {
                ningShenTimer = 0;
            }
        }

        public override void PostUpdate() {
            if (Player.HasBuff(ModContent.BuffType<NingShenDanDebuff>())) {
                ningShenTimer++;
                if (ningShenTimer >= 60) //每秒 (60 ticks) 扣除 5 点魔力
                {
                    ningShenTimer = 0;
                    //扣除魔力，但不让魔力低于0
                    Player.statMana -= 5;
                    if (Player.statMana < 0)
                        Player.statMana = 0;
                    //显示扣魔文本（可选）
                    CombatText.NewText(Player.Hitbox, Color.Blue, "5", true);
                }
            }
        }
    }
}
