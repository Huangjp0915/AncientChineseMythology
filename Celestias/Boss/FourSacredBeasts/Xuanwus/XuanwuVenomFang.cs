using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Xuanwus
{
    /// <summary>
    /// 玄武蛇毒牙 — 玄武蛇首释放的毒牙弹幕
    /// LightShot箭头形状 + SoftGlow毒绿外晕，伴随毒雾粒子拖尾
    /// 命中后施加缓速效果（通过Dust视觉暗示毒性）
    /// 渲染技术：LightShot方向锚定 + SoftGlow毒雾光晕 + 毒雾Dust拖尾
    /// </summary>
    public class XuanwuVenomFang : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private float venomPulse;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            venomPulse += 0.15f;

            // 毒雾拖尾：密度较高的绿色烟雾
            if (Main.rand.NextBool(2)) {
                Vector2 offset = Main.rand.NextVector2Circular(6, 6);
                Dust d = Dust.NewDustDirect(Projectile.Center + offset - Projectile.velocity * 0.3f,
                    0, 0, DustID.CursedTorch,
                    -Projectile.velocity.X * 0.1f + Main.rand.NextFloat(-0.5f, 0.5f),
                    -Projectile.velocity.Y * 0.1f + Main.rand.NextFloat(-0.5f, 0.5f),
                    100, default, 1f);
                d.noGravity = true;
                d.fadeIn = 1.4f;
            }

            // 小型毒液滴
            if (Main.rand.NextBool(6)) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Venom,
                    Main.rand.NextFloat(-1, 1), Main.rand.NextFloat(0, 2),
                    80, default, 0.6f);
            }

            Lighting.AddLight(Projectile.Center, 0.1f, 0.3f, 0.05f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            int trailLen = ProjectileID.Sets.TrailCacheLength[Type];

            // 毒牙残影（AlphaBlend层，绿色渐隐）
            Texture2D shotTex = ACMAsset.LightShot;
            Vector2 shotOrigin = shotTex.Size() / 2f;

            for (int i = trailLen - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = (float)i / trailLen;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float trailRot = Projectile.oldRot[i];
                float alpha = 0.35f * (1f - progress);

                Color trailColor = Color.Lerp(new Color(80, 200, 60), new Color(40, 120, 30), progress) * alpha;
                float trailScale = 0.35f * (1f - progress * 0.4f);

                sb.Draw(shotTex, trailPos, null, trailColor with { A = 0 }, trailRot,
                    shotOrigin, trailScale, SpriteEffects.None, 0f);
            }

            float pulse = MathF.Sin(venomPulse);

            // 第1层：毒绿外晕（SoftGlow大范围）
            Texture2D glowTex = ACMAsset.SoftGlow;
            Vector2 glowOrigin = glowTex.Size() / 2f;
            Color outerGlow = new Color(60, 200, 40, 0) * (0.3f + pulse * 0.1f);
            sb.Draw(glowTex, drawPos, null, outerGlow, 0f,
                glowOrigin, 1.0f + pulse * 0.15f, SpriteEffects.None, 0f);

            // 第2层：LightShot毒牙主体（亮绿色箭头）
            Color fangColor = new Color(100, 255, 70, 0) * 0.7f;
            sb.Draw(shotTex, drawPos, null, fangColor, Projectile.rotation,
                shotOrigin, 0.45f, SpriteEffects.None, 0f);

            // 第3层：内核高亮（黄绿色白芯）
            Color coreColor = new Color(180, 255, 120, 0) * 0.5f;
            sb.Draw(glowTex, drawPos, null, coreColor, 0f,
                glowOrigin, 0.35f, SpriteEffects.None, 0f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            // 毒液飞溅
            for (int i = 0; i < 8; i++) {
                int dustType = Main.rand.NextBool() ? DustID.CursedTorch : DustID.Venom;
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    dustType, Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(-3, 3),
                    80, default, 1.1f);
                d.noGravity = true;
            }
        }
    }
}
