using System.Linq;
using AncientChineseMythology.Players;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Global
{
    public class BaGuaKillRewardGlobalNPC : GlobalNPC
    {
        public override void OnKill(NPC npc) {
            // 只对非友好NPC生效
            if (npc.friendly || npc.boss) return;
            
            // 获取击杀者
            int killerIndex = npc.lastInteraction;
            if (killerIndex < 0 || killerIndex >= Main.maxPlayers || !Main.player[killerIndex].active) return;
            
            Player killer = Main.player[killerIndex];
            var baGuaPlayer = killer.GetModPlayer<BaGuaPlayer>();
            
            // 检查是否有吞噬万物阵
            if (killer.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) {
                var cur = baGuaPlayer.BaGuaItems?.Where(it => it != null && !it.IsAir).Select(it => it.type).ToArray();
                if (cur != null && baGuaPlayer.CheckTunShiWanWuFormation(cur)) {
                    // 回复3点生命值
                    if (killer.statLife < killer.statLifeMax2) {
                        int healAmount = 3;
                        int newLife = System.Math.Min(killer.statLife + healAmount, killer.statLifeMax2);
                        if (newLife > killer.statLife) {
                            killer.statLife = newLife;
                            if (Main.netMode != NetmodeID.Server && Main.myPlayer == killer.whoAmI) {
                                killer.HealEffect(healAmount, true);
                            }
                        }
                    }
                    
                    // 回复2点魔力值
                    if (killer.statMana < killer.statManaMax2) {
                        int manaAmount = 2;
                        int newMana = System.Math.Min(killer.statMana + manaAmount, killer.statManaMax2);
                        if (newMana > killer.statMana) {
                            killer.statMana = newMana;
                            if (Main.netMode != NetmodeID.Server && Main.myPlayer == killer.whoAmI) {
                                CombatText.NewText(killer.Hitbox, Microsoft.Xna.Framework.Color.Blue, $"+{manaAmount}", true);
                            }
                        }
                    }
                }
            }
        }
    }
}
