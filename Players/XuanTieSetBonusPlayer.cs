using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Players
{
    public class XuanTieSetBonusPlayer : ModPlayer
    {
        public bool xuanTieSet;

        public override void ResetEffects() {
            xuanTieSet = false;
        }

        public override void UpdateEquips() {
            if (xuanTieSet) {
                Player.moveSpeed += 0.10f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            TryApplyBleed(target);
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {
            if (proj.owner == Player.whoAmI) {
                TryApplyBleed(target);
            }
        }

        public void TryApplyBleed(NPC target) {
            if (!xuanTieSet || !target.active || target.friendly) {
                return;
            }

            target.AddBuff(ModContent.BuffType<Buffs.XuanTieBleed>(), 240);

            var global = target.GetGlobalNPC<Global.XuanTieBleedGlobalNPC>();
            global.bleedStacks++;
            if (global.bleedStacks < 3) {
                return;
            }

            global.bleedStacks = 0;
            int weaponDamage = Player.HeldItem?.damage > 0 ? Player.HeldItem.damage : 20;
            int aoeDamage = (int)(weaponDamage * 0.08f);
            if (aoeDamage < 1) {
                aoeDamage = 1;
            }

            foreach (NPC npc in Main.npc) {
                if (!npc.active || npc.friendly || !npc.CanBeChasedBy(Player)) {
                    continue;
                }
                if (Vector2.DistanceSquared(npc.Center, target.Center) > 160f * 160f) {
                    continue;
                }
                npc.StrikeNPC(new NPC.HitInfo {
                    Damage = aoeDamage,
                    Knockback = 0f,
                    HitDirection = Player.direction
                });
            }
        }
    }
}
