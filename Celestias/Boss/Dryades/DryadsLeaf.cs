using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dryades
{
    /// <summary>
    /// 树精落叶弹幕 — 复用原版叶子纹理
    /// 用于落叶弹幕、待机散射、阶段转换爆发等
    /// </summary>
    public class DryadsLeaf : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.Leaf}";

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 5;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.ignoreWater = true;
            Projectile.alpha = 50;
        }

        public override void AI() {
            // 帧动画
            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                if (++Projectile.frame >= Main.projFrames[Type])
                    Projectile.frame = 0;
            }

            Projectile.rotation += Projectile.velocity.X * 0.03f;

            // 轻重力 — 飘叶感
            if (Projectile.velocity.Y < 12f)
                Projectile.velocity.Y += 0.05f;

            // 左右微摆
            Projectile.velocity.X += MathF.Sin(Projectile.ai[0]) * 0.03f;
            Projectile.ai[0] += 0.1f;

            if (Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.GrassBlades,
                    0, 0, 100, default, 0.8f);
                d.noGravity = true;
                d.velocity *= 0.3f;
            }

            Lighting.AddLight(Projectile.Center, 0.05f, 0.15f, 0.03f);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GrassBlades, Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f),
                    80, default, 1f);
                d.noGravity = true;
            }
        }

        public override Color? GetAlpha(Color lightColor) => new Color(100, 200, 80, 150);
    }
}
