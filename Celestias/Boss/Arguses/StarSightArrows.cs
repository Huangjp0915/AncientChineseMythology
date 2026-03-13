using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Arguses
{
    /// <summary>
    /// 星芒追踪箭 — 带星瞳图案的箭矢，紫色和蓝色星系轨迹
    /// </summary>
    public class StarSightArrows : ModProjectile
    {
        private float trailPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 18;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 200;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            trailPhase += 0.12f;

            // 紫蓝星系轨迹粒子
            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.PurpleTorch : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                    0, 0, dustType,
                    -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f,
                    120, default, 1.2f);
                d.noGravity = true;
                d.fadeIn = 1.2f;
            }

            Lighting.AddLight(Projectile.Center, 0.3f, 0.2f, 0.6f);
        }

        public override void OnKill(int timeLeft) {
            // 击中爆炸星光
            for (int i = 0; i < 8; i++) {
                int dustType = i % 2 == 0 ? DustID.PurpleTorch : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, 0, 0, 80, default, 1.5f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(6, 6);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            // 紫蓝渐变拖尾
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float t = (float)i / Projectile.oldPos.Length;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = Color.Lerp(new Color(180, 100, 255), new Color(60, 100, 255), t) * (0.5f * (1f - t));
                sb.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * (1f - t * 0.4f), SpriteEffects.None, 0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(trailPhase * 3f) * 0.08f;
            Color mainColor = Color.Lerp(new Color(200, 140, 255), new Color(100, 140, 255), MathF.Sin(trailPhase) * 0.5f + 0.5f);
            sb.Draw(texture, drawPos, null, mainColor, Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0f);

            return false;
        }
    }
}
