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
    /// 青龙雷矢 (V3) — 顶点绘制锯齿闪电拖尾 + LightningBranch/ElectricArcSheet 灰度图叠加。
    /// V3 修订: 配色从离板的紫色改为青龙板式「青白雷」(TelegraphColors.Lightning 系),
    /// 并新增 <b>ambient 氛围模式</b> (ai[1]=1): 零伤害纯视觉余弹, 供雷暴天气与死亡演出
    /// 「化雨升天」当远景落雷氛围复用 — 由 Qinlong.SpawnAmbientBolt 服务器统一生成。
    /// 渲染技术: ColoredVertex TriangleStrip 锯齿电弧拖尾 + 双层灰度图弹体 + 抖动偏移。
    /// </summary>
    public class QinglongThunderBolt : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private float jitterSeed;
        private float trailOffset;

        /// <summary>ai[1]=1: 氛围模式 (零伤害, 视觉更淡, 落地自灭)。</summary>
        private bool Ambient => Projectile.ai[1] == 1f;

        // 青白雷配色 (青龙板式, 替代旧紫)
        private static readonly Color BoltOuter = new(70, 170, 235);
        private static readonly Color BoltMain = new(140, 215, 255);
        private static readonly Color BoltBright = new(200, 240, 255);
        private static readonly Color BoltCore = new(235, 250, 255);

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

            if (Ambient) {
                // 氛围余弹: 永不致伤; 加速下坠, 靠近地面/瓦片即自灭 (远景落雷感)
                Projectile.hostile = false;
                Projectile.damage = 0;
                Projectile.velocity.Y += 0.35f;
                if (Projectile.velocity.Y > 26f)
                    Projectile.velocity.Y = 26f;

                Point tile = Projectile.Center.ToTileCoordinates();
                if (tile.X >= 0 && tile.X < Main.maxTilesX && tile.Y >= 0 && tile.Y < Main.maxTilesY &&
                    WorldGen.SolidTile(tile.X, tile.Y)) {
                    Projectile.Kill();
                    return;
                }
            }

            if (Main.rand.NextBool(2)) {
                Vector2 offset = Main.rand.NextVector2Circular(18, 18);
                Dust d = Dust.NewDustDirect(Projectile.Center + offset, 0, 0,
                    DustID.Electric, Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(-3, 3),
                    60, default, Ambient ? 0.8f : 1.0f);
                d.noGravity = true;
                d.fadeIn = 1.3f;
            }

            Lighting.AddLight(Projectile.Center, 0.22f, 0.42f, 0.60f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(jitterSeed * 4f) * 0.15f;
            float dim = Ambient ? 0.72f : 1f; // 氛围模式整体更淡 (远景层)

            // === 1. 顶点TriangleStrip锯齿闪电拖尾 ===
            if (Projectile.oldPos[1] != Vector2.Zero) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);

                // 外层宽电弧 — 青蓝
                DrawLightningStrip(gd, BoltOuter * (0.65f * dim), new Color(30, 90, 160) * (0.2f * dim),
                    20f, 1.5f, 0f);

                // 内层窄亮芯 — 青白
                DrawLightningStrip(gd, BoltBright * (0.5f * dim), BoltMain * (0.15f * dim),
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

                // 底层：LightningBranch 主电弧（大尺寸，青蓝）
                Texture2D boltTex = ACMAsset.LightningBranch;
                Vector2 boltOrigin = boltTex.Size() / 2f;

                Color boltOuterC = BoltOuter with { A = 0 } * (0.5f * pulse * dim);
                sb.Draw(boltTex, drawPos + jitter, null, boltOuterC, Projectile.rotation,
                    boltOrigin, new Vector2(0.12f, 0.18f) * pulse, SpriteEffects.None, 0f);

                Color boltMainC = BoltMain with { A = 0 } * (0.7f * dim);
                sb.Draw(boltTex, drawPos + jitter * 0.7f, null, boltMainC, Projectile.rotation,
                    boltOrigin, new Vector2(0.08f, 0.14f), SpriteEffects.None, 0f);

                // 第二层：镜像分叉闪电
                Vector2 jitter2 = new(
                    MathF.Sin(jitterSeed * 11.1f) * 7f,
                    MathF.Cos(jitterSeed * 9.3f) * 7f);
                Color bolt2Color = BoltBright with { A = 0 } * (0.4f * dim);
                sb.Draw(boltTex, drawPos + jitter2, null, bolt2Color, Projectile.rotation + 0.2f,
                    boltOrigin, new Vector2(0.07f, 0.12f), SpriteEffects.FlipHorizontally, 0f);

                // 第三层：ElectricArcSheet 电弧缠绕 — 随机取段
                Texture2D arcTex = ACMAsset.ElectricArcSheet;
                if (arcTex != null) {
                    int arcSection = (int)(jitterSeed * 2f) % 4;
                    int sectionHeight = arcTex.Height / 4;
                    Rectangle arcFrame = new(0, arcSection * sectionHeight, arcTex.Width, sectionHeight);
                    Vector2 arcOrigin = new(arcFrame.Width / 2f, arcFrame.Height / 2f);
                    Color arcColor = BoltBright with { A = 0 } * (0.4f * pulse * dim);
                    sb.Draw(arcTex, drawPos + jitter * 0.4f, arcFrame, arcColor,
                        Projectile.rotation - MathHelper.PiOver2 + MathF.Sin(jitterSeed) * 0.25f,
                        arcOrigin, new Vector2(0.09f, 0.07f), SpriteEffects.None, 0f);
                }

                // 中心SoftGlow高亮 — 青白核心
                Texture2D glowTex = ACMAsset.SoftGlow;
                Vector2 glowOrigin = glowTex.Size() / 2f;
                Color coreGlow = BoltBright with { A = 0 } * (0.8f * pulse * dim);
                sb.Draw(glowTex, drawPos, null, coreGlow, 0f,
                    glowOrigin, 1.3f * pulse, SpriteEffects.None, 0f);

                Color whiteCore = BoltCore with { A = 0 } * (0.45f * dim);
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
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = Ambient ? 0.35f : 0.5f, Pitch = 0.3f }, Projectile.Center);
            int dustCount = Ambient ? 9 : 15;
            for (int i = 0; i < dustCount; i++) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Electric, Main.rand.NextFloat(-5, 5), Main.rand.NextFloat(-5, 5),
                    50, default, 1.4f);
                d.noGravity = true;
            }
            // 氛围余弹落地: 触发天幕微闪 (远雷感)
            if (Ambient && !Main.dedServ)
                QinglongSky.FlashLightning(0.30f);
        }
    }
}
