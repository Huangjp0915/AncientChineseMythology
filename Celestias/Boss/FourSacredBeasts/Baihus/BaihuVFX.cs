using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Baihus
{
    /// <summary>
    /// 白虎专属着色器绘制助手 —— 三个专属 .fx 的静态缓存(照抄 Xuanwu 写法, 不注册 ACMShaders)与顶点带绘制原语。
    /// 全部方法服务端零绘制; 顶点契约与 <see cref="ACMShaders.DrawBeam"/> 一致:
    /// 顶点位置 = 世界坐标 - Main.screenPosition, 变换走 GameViewMatrix。
    /// </summary>
    internal static class BaihuVFX
    {
        private static Asset<Effect> clawRendRef;
        private static Asset<Effect> sonicRingRef;
        private static Asset<Effect> metalSheenRef;

        public static Effect ClawRend {
            get {
                if (Main.dedServ) return null;
                clawRendRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/BaihuClawRend", AssetRequestMode.ImmediateLoad);
                return clawRendRef?.Value;
            }
        }

        public static Effect SonicRing {
            get {
                if (Main.dedServ) return null;
                sonicRingRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/BaihuSonicRing", AssetRequestMode.ImmediateLoad);
                return sonicRingRef?.Value;
            }
        }

        public static Effect MetalSheen {
            get {
                if (Main.dedServ) return null;
                metalSheenRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/BaihuMetalSheen", AssetRequestMode.ImmediateLoad);
                return metalSheenRef?.Value;
            }
        }

        /// <summary>
        /// 绘制一道爪裂撕痕带 (BaihuClawRend.fx, 三道平行耙痕在着色器内部分带)。
        /// 须在已有活动批的阶段调用 (PreDraw 等); 内部 End→Immediate→恢复默认批。
        /// </summary>
        /// <param name="worldStart">痕起点(世界坐标)。</param>
        /// <param name="worldEnd">痕终点(世界坐标)。</param>
        /// <param name="halfWidth">带半宽(屏幕像素, 三道耙痕总幅)。</param>
        /// <param name="intensity">整体强度 0~1。</param>
        /// <param name="progress">撕裂揭示进度 0~1 (从起点向终点撕开)。</param>
        /// <param name="release">true=银白释放, false=红色预告。</param>
        public static void DrawClawRend(Vector2 worldStart, Vector2 worldEnd, float halfWidth,
            float intensity, float progress, bool release) {
            if (Main.dedServ || intensity <= 0.01f || halfWidth < 1f)
                return;
            Effect fx = ClawRend;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            Vector2 a = worldStart - Main.screenPosition;
            Vector2 b = worldEnd - Main.screenPosition;
            float len = (b - a).Length();
            if (len < 4f)
                return;

            var verts = ACMUtils.BuildRibbonStrip(new[] { a, b }, _ => halfWidth, _ => Color.White, 0f, 1);
            if (verts.Length < 4)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(progress, 0f, 1f));
            fx.Parameters["uMode"]?.SetValue(release ? 1f : 0f);
            fx.Parameters["uLenScale"]?.SetValue(len / 300f);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[0] = noise;
            gd.SamplerStates[0] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 绘制一圈音波环 (BaihuSonicRing.fx, 环形 TriangleStrip, 参考 Xuanwu.DrawShockwaveRing 顶点法)。
        /// uv.x=角向(角度/2π, 与 gapAngle 世界弧度同约定), uv.y=0 外缘 / 1 内缘。
        /// 须在已有活动批的阶段调用; 内部 End→Immediate→恢复默认批。
        /// </summary>
        /// <param name="worldCenter">环心(世界坐标)。</param>
        /// <param name="radius">环半径(世界像素)。</param>
        /// <param name="halfWidth">带半宽(世界像素, 视觉主亮区 ≈ ±0.75×)。</param>
        /// <param name="alpha">整体强度 0~1。</param>
        /// <param name="gapAngle">缺口中心角(世界弧度); &lt;-100 表示无缺口。</param>
        /// <param name="gapHalf">缺口半宽(弧度)。</param>
        public static void DrawSonicRing(Vector2 worldCenter, float radius, float halfWidth,
            float alpha, float gapAngle, float gapHalf) {
            if (Main.dedServ || alpha <= 0.01f || radius < 4f)
                return;
            Effect fx = SonicRing;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            const int segments = 47; // 96 顶点/环 (性能预算 §7)
            float innerR = MathF.Max(radius - halfWidth, 1f);
            float outerR = radius + halfWidth;

            var verts = new ColoredVertex[segments * 2 + 2];
            Vector2 center = worldCenter - Main.screenPosition;
            Color white = Color.White;

            for (int i = 0; i <= segments; i++) {
                float angle = MathHelper.TwoPi / segments * i;
                Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle));
                float u = (float)i / segments;
                verts[i * 2] = new ColoredVertex(center + dir * outerR, new Vector3(u, 0f, 1f), white);
                verts[i * 2 + 1] = new ColoredVertex(center + dir * innerR, new Vector3(u, 1f, 1f), white);
            }

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(alpha, 0f, 1f));
            fx.Parameters["uGapAngle"]?.SetValue(MathHelper.WrapAngle(gapAngle));
            fx.Parameters["uGapHalf"]?.SetValue(gapAngle < -100f ? 0f : gapHalf);
            fx.Parameters["uRadius"]?.SetValue(radius);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[0] = noise;
            gd.SamplerStates[0] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 用 BaihuMetalSheen.fx 重绘一份本体贴图 (Immediate batch 套 effect):
        /// 各向异性高光扫过 + 银 rim + uFlash 蓄势闪白。须在已有活动批的阶段调用。
        /// </summary>
        public static void DrawMetalSheenBody(Texture2D tex, Vector2 worldPos, Rectangle? srcRect, Color color,
            float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects,
            float intensity, float sheenPos, float flash) {
            if (Main.dedServ || tex == null || (intensity <= 0.01f && flash <= 0.01f))
                return;
            Effect fx = MetalSheen;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uSheenAngle"]?.SetValue(-0.5f); // 沿躯干微斜扫掠
            fx.Parameters["uSheenPos"]?.SetValue(sheenPos);
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1f));
            fx.Parameters["uTexelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            fx.Parameters["uFlip"]?.SetValue(effects == SpriteEffects.FlipHorizontally ? 1f : 0f);

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            sb.Draw(tex, worldPos - Main.screenPosition, srcRect, color, rotation, origin, scale, effects, 0f);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }
    }
}
