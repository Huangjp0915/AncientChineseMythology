using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vigors
{
    /// <summary>
    /// 符文横扫光刃 — 大型弧形金色+蓝色能量波，带符文图案和火焰边缘
    /// </summary>
    public class RunicCleaveWaves : ModProjectile
    {
        private float glowPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            glowPhase += 0.12f;

            // 金色+蓝色火焰拖尾粒子
            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(16, 16),
                    0, 0, dustType,
                    -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f,
                    100, default, 1.4f);
                d.noGravity = true;
                d.fadeIn = 1.3f;
            }

            Lighting.AddLight(Projectile.Center, 0.6f, 0.45f, 0.15f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            float pulse = 1f + MathF.Sin(glowPhase * 3f) * 0.1f;

            // 金色残影拖尾
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float t = (float)i / Projectile.oldPos.Length;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = Color.Lerp(new Color(255, 200, 60), new Color(60, 120, 255), t) * (0.5f * (1f - t));
                sb.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * (1f - t * 0.3f) * pulse, SpriteEffects.None, 0f);
            }

            // 主体
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color mainColor = Color.Lerp(new Color(255, 220, 100), new Color(100, 160, 255), MathF.Sin(glowPhase) * 0.5f + 0.5f);
            sb.Draw(texture, drawPos, null, mainColor, Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0f);

            return false;
        }
    }
}
