using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dazhengs
{
    /// <summary>
    /// 大椿藤蔓弹幕 — 使用原版藤蔓链条纹理复合绘制
    /// 通过顶点绘制 TriangleStrip 配合原版 Chain 纹理实现藤蔓编织效果
    /// ai[1] = 1: 鞭笞模式（更快）
    /// ai[2] = 1: 金色藤蔓
    /// </summary>
    public class DazhengVine : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private float wavePhase;
        private bool IsWhipMode => Projectile.ai[1] == 1f;
        private bool IsGolden => Projectile.ai[2] == 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 30;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 240;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            wavePhase += 0.2f;

            // 藤蔓特有的蜿蜒运动
            if (!IsWhipMode) {
                float waveAmp = 0.8f;
                float wave = MathF.Sin(wavePhase) * waveAmp;
                Vector2 perp = new(-Projectile.velocity.Y, Projectile.velocity.X);
                perp = perp.SafeNormalize(Vector2.Zero);
                Projectile.Center += perp * wave;
            }

            // 粒子效果
            if (Main.rand.NextBool(3)) {
                int dustType = IsGolden ? DustID.GoldFlame : DustID.JungleGrass;
                Dust d = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(10, 10),
                    0, 0, dustType,
                    -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f,
                    100, default, 1.2f);
                d.noGravity = true;
                d.fadeIn = 1.3f;
            }

            // 藤蔓自然光照
            if (IsGolden)
                Lighting.AddLight(Projectile.Center, 0.4f, 0.35f, 0.1f);
            else
                Lighting.AddLight(Projectile.Center, 0.1f, 0.3f, 0.05f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;

            // 使用原版藤蔓链条纹理
            Texture2D chainTex = TextureAssets.Chain.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // === 1. 顶点 TriangleStrip 藤蔓拖尾 ===
            if (Projectile.oldPos[1] != Vector2.Zero) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);

                // 外层藤蔓拖尾
                DrawVineTrail(gd, chainTex, outerLayer: true);
                // 内层亮芯
                DrawVineTrail(gd, chainTex, outerLayer: false);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
            }

            // === 2. 链段式藤蔓绘制 — 沿路径用Chain纹理绞绕 ===
            {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);

                DrawChainSegments(sb, chainTex);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
            }

            // === 3. 头部光效 ===
            {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);

                Texture2D glowTex = ACMAsset.SoftGlow;
                if (glowTex != null) {
                    Vector2 glowOrigin = glowTex.Size() / 2f;
                    float pulse = 1f + MathF.Sin(wavePhase * 2f) * 0.15f;
                    Color glowColor = IsGolden
                        ? new Color(255, 200, 50, 0) * (0.5f * pulse)
                        : new Color(80, 200, 60, 0) * (0.4f * pulse);
                    sb.Draw(glowTex, drawPos, null, glowColor, 0f, glowOrigin, 0.8f * pulse, SpriteEffects.None, 0f);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
            }

            return false;
        }

        private void DrawVineTrail(GraphicsDevice gd, Texture2D tex, bool outerLayer) {
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

                Color c;
                float width;

                if (outerLayer) {
                    width = 16f * scaleFactor;
                    if (IsGolden)
                        c = Color.Lerp(new Color(255, 200, 80) * 0.6f, new Color(180, 120, 30) * 0.2f, t);
                    else
                        c = Color.Lerp(new Color(60, 180, 40) * 0.7f, new Color(30, 100, 20) * 0.2f, t);
                } else {
                    width = 8f * scaleFactor;
                    if (IsGolden)
                        c = new Color(255, 240, 180) * (0.5f * (1f - t));
                    else
                        c = new Color(140, 220, 100) * (0.4f * (1f - t));
                }
                c.A = 0;

                vertices.Add(new ColoredVertex(basePos + perp * width, new Vector3(t * 3f, 0, 1), c));
                vertices.Add(new ColoredVertex(basePos - perp * width, new Vector3(t * 3f, 1, 1), c));
            }

            if (vertices.Count >= 3) {
                gd.Textures[0] = tex;
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
            }
        }

        /// <summary>
        /// 沿轨迹绘制链条段，模拟藤蔓编织
        /// </summary>
        private void DrawChainSegments(SpriteBatch sb, Texture2D chainTex) {
            Vector2 chainOrigin = new(chainTex.Width / 2f, chainTex.Height / 2f);
            float segmentLength = chainTex.Height;

            for (int i = 0; i < Projectile.oldPos.Length - 1; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero || Projectile.oldPos[i + 1] == Vector2.Zero) break;

                Vector2 start = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Vector2 end = Projectile.oldPos[i + 1] + Projectile.Size / 2f - Main.screenPosition;
                Vector2 diff = end - start;
                float dist = diff.Length();

                if (dist < 2f) continue;

                float rot = diff.ToRotation() - MathHelper.PiOver2;
                float t = (float)i / Projectile.oldPos.Length;
                float alpha = 1f - t * 0.7f;

                // 双层编织 - 微偏移模拟拧绞
                float twistOffset = MathF.Sin(i * 1.2f + wavePhase) * 3f;
                Vector2 perp = new(-diff.Y, diff.X);
                perp = perp.SafeNormalize(Vector2.Zero) * twistOffset;

                Color chainColor;
                if (IsGolden)
                    chainColor = new Color(255, 220, 100) * alpha;
                else
                    chainColor = new Color(80, 160, 50) * alpha;

                float scale = MathHelper.Lerp(1.2f, 0.6f, t);
                sb.Draw(chainTex, start + perp, null, chainColor, rot, chainOrigin, new Vector2(scale, dist / segmentLength), SpriteEffects.None, 0f);

                // 第二层链条（偏移，形成编织感）
                Color chain2Color = chainColor * 0.6f;
                sb.Draw(chainTex, start - perp, null, chain2Color, rot, chainOrigin, new Vector2(scale * 0.8f, dist / segmentLength), SpriteEffects.None, 0f);
            }
        }

        public override void OnKill(int timeLeft) {
            int dustType = IsGolden ? DustID.GoldFlame : DustID.JungleGrass;
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    dustType, Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(-3, 3),
                    80, default, 1.4f);
                d.noGravity = true;
            }
        }
    }
}
