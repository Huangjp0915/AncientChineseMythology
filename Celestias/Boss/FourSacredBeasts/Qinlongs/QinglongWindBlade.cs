using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Qinlongs
{
    /// <summary>
    /// 青龙风刃 — 顶点绘制流线型风刃拖尾 + GlaciateWave月牙弹体 + SoftGlow光晕
    /// 渲染技术：ColoredVertex TriangleStrip拖尾(GlaciateWave纹理) + 多层Additive灰度图叠加
    /// </summary>
    public class QinglongWindBlade : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private float windPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 20;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            windPhase += 0.15f;

            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(14, 14),
                    0, 0, DustID.GreenTorch,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    100, default, 1.3f);
                d.noGravity = true;
                d.fadeIn = 1.5f;
            }

            Lighting.AddLight(Projectile.Center, 0.2f, 0.45f, 0.15f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(windPhase * 3f) * 0.12f;

            // === 1. 顶点TriangleStrip风刃拖尾 ===
            if (Projectile.oldPos[1] != Vector2.Zero) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);

                List<ColoredVertex> vertices = new();
                int count = Projectile.oldPos.Length;
                for (int i = 0; i < count; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) break;
                    float t = (float)i / count;
                    float scaleFactor = 1f - t;
                    Vector2 basePos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                    Vector2 dir = (i < count - 1 && Projectile.oldPos[i + 1] != Vector2.Zero)
                        ? (Projectile.oldPos[i] - Projectile.oldPos[i + 1]).SafeNormalize(Vector2.UnitX)
                        : Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    Vector2 perp = new(-dir.Y, dir.X);

                    // 宽度从头到尾递减，带呼吸脉冲
                    float width = 22f * scaleFactor * pulse;
                    // 翠绿 → 深绿渐变
                    Color c = Color.Lerp(new Color(100, 255, 140) * 0.8f, new Color(30, 160, 60) * 0.3f, t);
                    c.A = 0;

                    vertices.Add(new ColoredVertex(basePos + perp * width, new Vector3(t, 0, 1), c));
                    vertices.Add(new ColoredVertex(basePos - perp * width, new Vector3(t, 1, 1), c));
                }

                if (vertices.Count >= 3) {
                    Texture2D trailTex = ACMAsset.GlaciateWave ?? VaultAsset.placeholder2.Value;
                    gd.Textures[0] = trailTex;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
                }

                // 第二层更窄的亮芯拖尾
                List<ColoredVertex> innerVerts = new();
                for (int i = 0; i < count; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) break;
                    float t = (float)i / count;
                    float scaleFactor = 1f - t;
                    Vector2 basePos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                    Vector2 dir = (i < count - 1 && Projectile.oldPos[i + 1] != Vector2.Zero)
                        ? (Projectile.oldPos[i] - Projectile.oldPos[i + 1]).SafeNormalize(Vector2.UnitX)
                        : Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    Vector2 perp = new(-dir.Y, dir.X);

                    float innerWidth = 10f * scaleFactor;
                    Color ic = new Color(180, 255, 200) * (0.6f * (1f - t));
                    ic.A = 0;

                    innerVerts.Add(new ColoredVertex(basePos + perp * innerWidth, new Vector3(t * 2f, 0, 1), ic));
                    innerVerts.Add(new ColoredVertex(basePos - perp * innerWidth, new Vector3(t * 2f, 1, 1), ic));
                }

                if (innerVerts.Count >= 3) {
                    Texture2D trailTex = ACMAsset.GlaciateWave ?? VaultAsset.placeholder2.Value;
                    gd.Textures[0] = trailTex;
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, innerVerts.ToArray(), 0, innerVerts.Count - 2);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
            }

            // === 2. GlaciateWave月牙形弹体（大尺寸Additive叠加） ===
            {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);

                Texture2D bladeTex = ACMAsset.GlaciateWave;
                Vector2 bladeOrigin = bladeTex.Size() / 2f;

                // 外层翠绿光晕 — 大尺寸，半透明
                Color outerColor = new Color(60, 220, 100, 0) * (0.45f * pulse);
                sb.Draw(bladeTex, drawPos, null, outerColor, Projectile.rotation,
                    bladeOrigin, new Vector2(0.22f, 0.12f) * pulse, SpriteEffects.None, 0f);

                // 中层亮绿主体
                Color mainColor = new Color(100, 255, 130, 0) * 0.7f;
                sb.Draw(bladeTex, drawPos, null, mainColor, Projectile.rotation,
                    bladeOrigin, new Vector2(0.18f, 0.08f), SpriteEffects.None, 0f);

                // 内层白芯 — 高亮
                Color coreColor = new Color(200, 255, 220, 0) * 0.55f;
                sb.Draw(bladeTex, drawPos, null, coreColor, Projectile.rotation,
                    bladeOrigin, new Vector2(0.12f, 0.05f), SpriteEffects.None, 0f);

                // === 3. SoftGlow中心光点 ===
                Texture2D glowTex = ACMAsset.SoftGlow;
                Vector2 glowOrigin = glowTex.Size() / 2f;
                Color glowColor = new Color(140, 255, 170, 0) * (0.7f * pulse);
                sb.Draw(glowTex, drawPos, null, glowColor, 0f,
                    glowOrigin, 1.2f * pulse, SpriteEffects.None, 0f);

                // 白色高亮核心
                Color whiteCore = new Color(220, 255, 230, 0) * 0.4f;
                sb.Draw(glowTex, drawPos, null, whiteCore, 0f,
                    glowOrigin, 0.5f, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 12; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GreenTorch, Main.rand.NextFloat(-4, 4), Main.rand.NextFloat(-4, 4),
                    80, default, 1.6f);
                d.noGravity = true;
            }
        }
    }
}
