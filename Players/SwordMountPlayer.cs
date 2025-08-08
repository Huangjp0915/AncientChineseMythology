using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Players
{
    public class SwordMountPlayer : ModPlayer
    {
        public override void PostUpdate() {
            int swordMountType = ModContent.MountType<Mounts.ChengYingMount>();

            //玩家没骑承影剑 ⇒ 查杀残留判定盒
            if (Player.mount.Type != swordMountType) {
                int hitboxProjType = ModContent.ProjectileType<Projectiles.ChengYingHitbox>();

                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.type == hitboxProjType && p.owner == Player.whoAmI)
                        p.Kill();     //立即删除
                }
            }
        }
    }
}
