using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dazhengs
{
    /// <summary>
    /// 大椿树叶弹幕 — 飘落的树叶，带有旋转和飘荡效果
    /// 使用原版树叶 Gore 风格 + SoftGlow 光效
    /// </summary>
    public class DazhengLeaf : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private float leafSpin;
        private float sway; // 左右飘荡
        private float swaySpeed;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            // 初始化飘荡参数
            if (Projectile.localAI[0] == 0f) {
                swaySpeed = Main.rand.NextFloat(0.03f, 0.08f);
                Projectile.localAI[0] = 1f;
                Projectile.localAI[1] = Main.rand.NextFloat(MathHelper.TwoPi); // 随机初始相位
            }

            sway += swaySpeed;
            leafSpin += 0.12f;

            // 树叶飘荡 — 正弦波横向运动
            float swayAmount = MathF.Sin(sway + Projectile.localAI[1]) * 2.5f;
            Projectile.velocity.X += swayAmount * 0.02f;

            // 限制水平速度，防止漂移过远
            Projectile.velocity.X = MathHelper.Clamp(Projectile.velocity.X, -8f, 8f);

            // 旋转跟随运动方向，叠加自旋
            Projectile.rotation = Projectile.velocity.ToRotation() + leafSpin;

            // 微弱阻力
            Projectile.velocity *= 0.995f;

            // 粒子效果 — 偶尔释放小树叶粒子
            if (Main.rand.NextBool(6)) {
                Dust d = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                    0, 0, DustID.GrassBlades,
                    -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f,
                    120, default, 1f);
                d.noGravity = true;
                d.fadeIn = 1.2f;
            }

            Lighting.AddLight(Projectile.Center, 0.15f, 0.25f, 0.05f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // === 拖尾残影 ===
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float t = (float)i / Projectile.oldPos.Length;
                float alpha = 0.4f * (1f - t);
                Color trailColor = Color.Lerp(new Color(100, 200, 60), new Color(180, 150, 40), t) * alpha;

                // 使用 SoftGlow 绘制叶片残影
                Texture2D glowTex = ACMAsset.SoftGlow;
                if (glowTex != null) {
                    Vector2 glowOrigin = glowTex.Size() / 2f;
                    float trailScale = 0.4f * (1f - t * 0.5f);
                    sb.Draw(glowTex, trailPos, null, trailColor with { A = 0 }, Projectile.oldRot[i], glowOrigin, trailScale, SpriteEffects.None, 0f);
                }
            }

            // === 主体：多层叠加模拟树叶 ===
            {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);

                Texture2D glowTex = ACMAsset.SoftGlow;
                if (glowTex != null) {
                    Vector2 glowOrigin = glowTex.Size() / 2f;
                    float pulse = 1f + MathF.Sin(sway * 4f) * 0.1f;

                    // 外层绿色光晕
                    Color outerColor = new Color(60, 180, 50, 0) * (0.5f * pulse);
                    sb.Draw(glowTex, drawPos, null, outerColor, Projectile.rotation,
                        glowOrigin, 0.7f * pulse, SpriteEffects.None, 0f);

                    // 中层亮绿
                    Color mainColor = new Color(120, 220, 80, 0) * 0.6f;
                    sb.Draw(glowTex, drawPos, null, mainColor, Projectile.rotation + 0.3f,
                        glowOrigin, new Vector2(0.5f, 0.3f), SpriteEffects.None, 0f);

                    // 内层黄绿芯
                    Color coreColor = new Color(200, 230, 100, 0) * 0.45f;
                    sb.Draw(glowTex, drawPos, null, coreColor, Projectile.rotation + 0.1f,
                        glowOrigin, new Vector2(0.35f, 0.2f), SpriteEffects.None, 0f);
                }

                // 使用 GlaciateWave 绘制叶片形状
                Texture2D bladeTex = ACMAsset.GlaciateWave;
                if (bladeTex != null) {
                    Vector2 bladeOrigin = bladeTex.Size() / 2f;
                    float leafAngle = Projectile.rotation;

                    // 叶片主体
                    Color leafColor = new Color(80, 200, 60, 0) * 0.55f;
                    sb.Draw(bladeTex, drawPos, null, leafColor, leafAngle,
                        bladeOrigin, new Vector2(0.06f, 0.03f), SpriteEffects.None, 0f);

                    // 叶脉高光
                    Color veinColor = new Color(160, 240, 120, 0) * 0.3f;
                    sb.Draw(bladeTex, drawPos, null, veinColor, leafAngle,
                        bladeOrigin, new Vector2(0.04f, 0.015f), SpriteEffects.None, 0f);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GrassBlades, Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(-3, 3),
                    80, default, 1.3f);
                d.noGravity = true;
            }
        }
    }
}
