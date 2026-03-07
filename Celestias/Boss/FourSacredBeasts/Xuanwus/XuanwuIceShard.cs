using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Xuanwus
{
    /// <summary>
    /// 玄武冰锥 — 结晶冰刺弹幕，BlankStar叠加LightShot构成的通透冰晶
    /// BlankStar以相反方向缓慢旋转，制造折射棱光效果
    /// 行进中留下渐隐的冰蓝色霜迹
    /// 渲染技术：BlankStar反向旋转叠加 + LightShot方向锚定 + 霜迹残影
    /// </summary>
    public class XuanwuIceShard : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private float crystalSpin;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
            Projectile.coldDamage = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            crystalSpin -= 0.06f; // 反向旋转

            // 霜迹粒子
            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                    0, 0, DustID.IceTorch,
                    -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f,
                    120, default, 0.9f);
                d.noGravity = true;
                d.fadeIn = 1.3f;
            }

            // 冰晶碎屑
            if (Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Ice,
                    Main.rand.NextFloat(-1, 1), Main.rand.NextFloat(-1, 1),
                    100, default, 0.7f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.1f, 0.2f, 0.4f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 霜迹残影（AlphaBlend层）
            Texture2D shotTex = ACMAsset.LightShot;
            Vector2 shotOrigin = shotTex.Size() / 2f;
            int trailLen = ProjectileID.Sets.TrailCacheLength[Type];

            for (int i = trailLen - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = (float)i / trailLen;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float trailRot = Projectile.oldRot[i];
                float alpha = 0.3f * (1f - progress);

                Color frostColor = Color.Lerp(new Color(120, 200, 255), new Color(60, 100, 180), progress) * alpha;
                float trailScale = 0.4f * (1f - progress * 0.5f);

                sb.Draw(shotTex, trailPos, null, frostColor with { A = 0 }, trailRot,
                    shotOrigin, trailScale, SpriteEffects.None, 0f);
            }

            // 第1层：BlankStar反向旋转（折射棱光效果）
            Texture2D starTex = ACMAsset.BlankStar;
            Vector2 starOrigin = starTex.Size() / 2f;
            Color starColor = new Color(100, 180, 255, 0) * 0.45f;
            sb.Draw(starTex, drawPos, null, starColor, crystalSpin,
                starOrigin, 0.35f, SpriteEffects.None, 0f);

            // 第2层：BlankStar正向旋转（叠加形成六芒冰晶感）
            Color star2Color = new Color(160, 220, 255, 0) * 0.3f;
            sb.Draw(starTex, drawPos, null, star2Color, -crystalSpin * 0.7f,
                starOrigin, 0.28f, SpriteEffects.None, 0f);

            // 第3层：LightShot方向性光芒（锚定飞行方向）
            Color shotColor = new Color(140, 210, 255, 0) * 0.5f;
            sb.Draw(shotTex, drawPos, null, shotColor, Projectile.rotation,
                shotOrigin, 0.5f, SpriteEffects.None, 0f);

            // 第4层：内核SoftGlow高亮
            Texture2D glowTex = ACMAsset.SoftGlow;
            Vector2 glowOrigin = glowTex.Size() / 2f;
            Color coreColor = new Color(200, 240, 255, 0) * 0.6f;
            sb.Draw(glowTex, drawPos, null, coreColor, 0f,
                glowOrigin, 0.5f, SpriteEffects.None, 0f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.IceTorch, Main.rand.NextFloat(-4, 4), Main.rand.NextFloat(-4, 4),
                    80, default, 1.2f);
                d.noGravity = true;
            }
        }
    }
}
