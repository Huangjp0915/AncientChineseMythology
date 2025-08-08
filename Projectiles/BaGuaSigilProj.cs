using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    public class BaGuaSigilProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BaGuaSigilProj";

        public override void SetStaticDefaults() {
        }

        public override void SetDefaults() {
            Projectile.width = 64;
            Projectile.height = 64;
            Projectile.friendly = false;
            Projectile.penetrate = -1;        // 不消失
            Projectile.timeLeft = 3600;        // 理论上由 Buff 管理
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead || !owner.HasBuff(ModContent.BuffType<Buffs.BaGuaBuff>())) {
                Projectile.Kill();
                return;
            }

            // 固定在玩家头顶 40 像素处
            Projectile.Center = owner.Center + new Vector2(0, -15f);
            Projectile.rotation += 0.03f;      // 缓慢旋转
        }

        public override bool? CanDamage() => false; // 纯装饰
    }
}
