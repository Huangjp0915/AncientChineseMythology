using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Arguses
{
    /// <summary>
    /// 旋转星系球 — 旋转的紫色和蓝色像素球，中心是发光的星点
    /// </summary>
    public class SpinningGalacticOrbs : ModProjectile
    {
        private float spinPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 220;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.rotation += 0.12f;
            spinPhase += 0.15f;

            // 星系旋转粒子
            if (Main.rand.NextBool(3)) {
                float angle = spinPhase * 2f + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 10f;
                int dustType = Main.rand.NextBool() ? DustID.PurpleTorch : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center + offset, 0, 0, dustType, 0, 0, 130, default, 1.1f);
                d.noGravity = true;
                d.velocity = new Vector2(-offset.Y, offset.X) * 0.15f;
            }

            Lighting.AddLight(Projectile.Center, 0.35f, 0.2f, 0.55f);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                int dustType = i % 2 == 0 ? DustID.PurpleTorch : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, 0, 0, 80, default, 1.3f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(4, 4);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            float pulse = 1f + MathF.Sin(spinPhase * 4f) * 0.12f;

            // 旋转残影
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float t = (float)i / Projectile.oldPos.Length;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = Color.Lerp(new Color(160, 80, 220), new Color(60, 80, 200), t) * (0.35f * (1f - t));
                sb.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * (1f - t * 0.25f), SpriteEffects.None, 0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color mainColor = Color.Lerp(new Color(180, 100, 240), new Color(80, 120, 255), MathF.Sin(spinPhase) * 0.5f + 0.5f);
            sb.Draw(texture, drawPos, null, mainColor, Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0f);

            return false;
        }
    }
}
