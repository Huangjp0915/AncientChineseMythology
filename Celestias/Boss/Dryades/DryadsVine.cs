using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dryades
{
    /// <summary>
    /// 树精藤蔓/根须弹幕 — 复用原版荨麻纹理
    /// 用于根须爆发、藤蔓抽打、根须风暴等穿地攻击
    /// </summary>
    public class DryadsVine : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.NettleBurstRight}";

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false; // 根须需穿过地面
            Projectile.penetrate = 2;
            Projectile.timeLeft = 240;
            Projectile.ignoreWater = true;
            Projectile.alpha = 30;
        }

        public override void AI() {
            // 朝运动方向旋转
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 轻微减速
            if (Projectile.velocity.Length() > 2f)
                Projectile.velocity *= 0.985f;

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.JungleGrass,
                    -Projectile.velocity.X * 0.05f, -Projectile.velocity.Y * 0.05f,
                    100, default, 1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.08f, 0.18f, 0.04f);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.JungleGrass, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f),
                    100, default, 1.2f);
                d.noGravity = true;
            }
        }

        public override Color? GetAlpha(Color lightColor) => new Color(60, 140, 45, 180);
    }
}
