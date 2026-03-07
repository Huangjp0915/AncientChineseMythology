using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Qinlongs
{
    /// <summary>
    /// 青龙雷柱 — 顶点绘制锯齿闪电拖尾 + LightningBranch/ElectricArcSheet灰度图叠加
    /// 渲染技术：ColoredVertex TriangleStrip锯齿电弧拖尾 + 双层灰度图弹体 + 抖动偏移
    /// </summary>
    public class QinglongThunderBolt : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private float jitterSeed;
        private float trailOffset;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            jitterSeed += 0.3f;
            trailOffset += 0.02f;

            if (Main.rand.NextBool(2)) {
                Vector2 offset = Main.rand.NextVector2Circular(18, 18);
                Dust d = Dust.NewDustDirect(Projectile.Center + offset, 0, 0,
                    DustID.Electric, Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(-3, 3),
                    60, default, 1.0f);
                d.noGravity = true;
                d.fadeIn = 1.3f;
            }

            Lighting.AddLight(Projectile.Center, 0.35f, 0.25f, 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(jitterSeed * 4f) * 0.15f;

            // === 1. 顶点TriangleStrip锯齿闪电拖尾 ===
            if (Projectile.oldPos[1] != Vector2.Zero) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);

                // 外层宽电弧 — 紫蓝色
                DrawLightningStrip(gd, new Color(100, 80, 255) * 0.65f, new Color(60, 40, 180) * 0.2f,
                    20f, 1.5f, 0f);

                // 内层窄亮芯 — 白紫色
                DrawLightningStrip(gd, new Color(200, 180, 255) * 0.5f, new Color(160, 140, 255) * 0.15f,
                    8f, 0.8f, 0.3f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
            }

            // === 2. 灰度图弹体叠加（Additive混合） ===
            {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);

                // 电弧抖动偏移
                Vector2 jitter = new(
                    MathF.Sin(jitterSeed * 7.3f) * 5f,
                    MathF.Cos(jitterSeed * 5.7f) * 5f);

                // 底层：LightningBranch 主电弧（大尺寸，紫蓝色）
                Texture2D boltTex = ACMAsset.LightningBranch;
                Vector2 boltOrigin = boltTex.Size() / 2f;

                Color boltOuter = new Color(80, 60, 200, 0) * (0.5f * pulse);
                sb.Draw(boltTex, drawPos + jitter, null, boltOuter, Projectile.rotation,
                    boltOrigin, new Vector2(0.12f, 0.18f) * pulse, SpriteEffects.None, 0f);

                Color boltMain = new Color(130, 110, 255, 0) * 0.7f;
                sb.Draw(boltTex, drawPos + jitter * 0.7f, null, boltMain, Projectile.rotation,
                    boltOrigin, new Vector2(0.08f, 0.14f), SpriteEffects.None, 0f);

                // 第二层：镜像分叉闪电
                Vector2 jitter2 = new(
                    MathF.Sin(jitterSeed * 11.1f) * 7f,
                    MathF.Cos(jitterSeed * 9.3f) * 7f);
                Color bolt2Color = new Color(180, 150, 255, 0) * 0.4f;
                sb.Draw(boltTex, drawPos + jitter2, null, bolt2Color, Projectile.rotation + 0.2f,
                    boltOrigin, new Vector2(0.07f, 0.12f), SpriteEffects.FlipHorizontally, 0f);

                // 第三层：ElectricArcSheet 电弧缠绕 — 随机取段
                Texture2D arcTex = ACMAsset.ElectricArcSheet;
                if (arcTex != null) {
                    int arcSection = (int)(jitterSeed * 2f) % 4;
                    int sectionHeight = arcTex.Height / 4;
                    Rectangle arcFrame = new(0, arcSection * sectionHeight, arcTex.Width, sectionHeight);
                    Vector2 arcOrigin = new(arcFrame.Width / 2f, arcFrame.Height / 2f);
                    Color arcColor = new Color(200, 170, 255, 0) * (0.4f * pulse);
                    sb.Draw(arcTex, drawPos + jitter * 0.4f, arcFrame, arcColor,
                        Projectile.rotation - MathHelper.PiOver2 + MathF.Sin(jitterSeed) * 0.25f,
                        arcOrigin, new Vector2(0.09f, 0.07f), SpriteEffects.None, 0f);
                }

                // 中心SoftGlow高亮 — 白紫核心
                Texture2D glowTex = ACMAsset.SoftGlow;
                Vector2 glowOrigin = glowTex.Size() / 2f;
                Color coreGlow = new Color(200, 180, 255, 0) * (0.8f * pulse);
                sb.Draw(glowTex, drawPos, null, coreGlow, 0f,
                    glowOrigin, 1.3f * pulse, SpriteEffects.None, 0f);

                Color whiteCore = new Color(240, 230, 255, 0) * 0.45f;
                sb.Draw(glowTex, drawPos, null, whiteCore, 0f,
                    glowOrigin, 0.5f, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
            }

            return false;
        }

        /// <summary>
        /// 绘制锯齿闪电TriangleStrip拖尾 — 每个节点添加随机横向偏移模拟电弧折线
        /// </summary>
        private void DrawLightningStrip(GraphicsDevice gd, Color headColor, Color tailColor,
            float baseWidth, float zigzagAmplitude, float phaseOffset) {
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

                // 锯齿偏移：奇偶节点交替偏移，模拟闪电折线
                float zigzag = MathF.Sin((i * 3.7f + jitterSeed * 5f + phaseOffset) * 2.1f) * zigzagAmplitude * baseWidth * scaleFactor;
                basePos += perp * zigzag;

                float width = baseWidth * scaleFactor;
                Color c = Color.Lerp(headColor, tailColor, t);
                c.A = 0;

                vertices.Add(new ColoredVertex(basePos + perp * width, new Vector3(t + trailOffset, 0, 1), c));
                vertices.Add(new ColoredVertex(basePos - perp * width, new Vector3(t + trailOffset, 1, 1), c));
            }

            if (vertices.Count >= 3) {
                Texture2D tex = ACMAsset.LightningBranch ?? VaultAsset.placeholder2.Value;
                gd.Textures[0] = tex;
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            for (int i = 0; i < 15; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Electric, Main.rand.NextFloat(-5, 5), Main.rand.NextFloat(-5, 5),
                    50, default, 1.4f);
                d.noGravity = true;
            }
        }
    }
}
