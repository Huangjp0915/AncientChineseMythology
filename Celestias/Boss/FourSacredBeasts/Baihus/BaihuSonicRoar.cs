using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Baihus
{
    /// <summary>
    /// 白虎音波咆哮 — 虎啸产生的扩散音波环
    /// 以Boss中心为起点向外扩散的半透明银白色环形冲击波
    /// 判定范围随环扩大而增长，透明度随扩散而降低
    /// 渲染技术：SoftGlow环形排列 + 动态缩放扩散 + 环形亮度梯度
    /// </summary>
    public class BaihuSonicRoar : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        public ref float RingRadius => ref Projectile.ai[0];
        public ref float MaxRadius => ref Projectile.ai[1];

        private const float ExpansionSpeed = 6f;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            if (MaxRadius == 0) MaxRadius = 400f;

            RingRadius += ExpansionSpeed;
            Projectile.velocity = Vector2.Zero;

            // 动态碰撞箱跟随环半径
            int newSize = (int)(RingRadius * 2);
            if (newSize > 10) {
                Projectile.Resize(newSize, newSize);
            }

            // 音波扰动粒子
            if (Main.rand.NextBool(3)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * RingRadius;
                Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Silver,
                    MathF.Cos(angle) * 2f, MathF.Sin(angle) * 2f, 150, default, 0.8f);
                d.noGravity = true;
            }

            if (RingRadius > MaxRadius) Projectile.Kill();

            Lighting.AddLight(Projectile.Center, 0.2f, 0.2f, 0.25f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 环形碰撞判定：只有环的边缘附近才造成伤害
            float ringThickness = 35f;
            Vector2 closestPoint = new Vector2(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            float dist = Vector2.Distance(Projectile.Center, closestPoint);
            return dist >= RingRadius - ringThickness && dist <= RingRadius + ringThickness;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float progress = RingRadius / MaxRadius;
            float alpha = 1f - progress;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D glowTex = ACMAsset.SoftGlow;
            Vector2 glowOrigin = glowTex.Size() / 2f;

            // 环形排列SoftGlow光点构成音波环
            int ringPoints = Math.Max(24, (int)(RingRadius / 8f));
            for (int i = 0; i < ringPoints; i++) {
                float angle = MathHelper.TwoPi / ringPoints * i;
                Vector2 pointPos = drawPos + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * RingRadius;

                // 环的透明度随扩散递减，边缘带有呼吸脉动
                float pointAlpha = alpha * (0.6f + 0.4f * MathF.Sin(angle * 3f + RingRadius * 0.05f));
                Color ringColor = new Color(220, 220, 255, 0) * pointAlpha * 0.5f;
                float pointScale = 0.4f + 0.2f * (1f - progress);

                sb.Draw(glowTex, pointPos, null, ringColor, 0f,
                    glowOrigin, pointScale, SpriteEffects.None, 0f);
            }

            // 中心残留光晕（渐隐）
            if (progress < 0.5f) {
                Color centerGlow = new Color(180, 180, 220, 0) * (0.5f - progress) * 0.6f;
                sb.Draw(glowTex, drawPos, null, centerGlow, 0f,
                    glowOrigin, RingRadius / 40f, SpriteEffects.None, 0f);
            }

            // 外圈GlaciateWave光晕环（更大尺度的半透明环）
            Texture2D starTex = ACMAsset.BlankStar;
            Vector2 starOrigin = starTex.Size() / 2f;
            Color outerColor = new Color(200, 200, 240, 0) * alpha * 0.25f;
            sb.Draw(starTex, drawPos, null, outerColor, RingRadius * 0.02f,
                starOrigin, RingRadius / 50f, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi / 15 * i;
                Dust d = Dust.NewDustDirect(
                    Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * RingRadius * 0.5f,
                    0, 0, DustID.Silver,
                    MathF.Cos(angle) * 3f, MathF.Sin(angle) * 3f, 100, default, 1f);
                d.noGravity = true;
            }
        }
    }
}
