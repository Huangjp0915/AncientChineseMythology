using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    /// <summary>
    /// 东海龙王辅助类 - 颜色、专属着色器缓存与绘制工具
    /// </summary>
    public static class AoGuangHelper
    {
        #region 专属着色器缓存 (每个 Effect 只 Request 一次)

        private const string EffectPath = "AncientChineseMythology/Effects/";

        private static Asset<Effect> _waterSerpent;
        private static Asset<Effect> _tidalWall;
        private static Asset<Effect> _abyssalSea;

        /// <summary>龙躯水流 ribbon (TriangleStrip 像素着色器, s0=噪声)。</summary>
        public static Effect WaterSerpentEffect => GetEffect(ref _waterSerpent, "AoGuangWaterSerpent");
        /// <summary>浪墙屏幕空间 decal (s0=噪声): 整面巨浪 + 穿越缺口。</summary>
        public static Effect TidalWallEffect => GetEffect(ref _tidalWall, "AoGuangTidalWall");
        /// <summary>沧海沉浸全屏后处理 (s0=screenTarget, s1=噪声): 折射+水位线+吸入+impact frame。</summary>
        public static Effect AbyssalSeaEffect => GetEffect(ref _abyssalSea, "AoGuangAbyssalSea");

        private static Effect GetEffect(ref Asset<Effect> slot, string name) {
            if (Main.dedServ)
                return null;
            slot ??= ModContent.Request<Effect>(EffectPath + name, AssetRequestMode.ImmediateLoad);
            return slot?.Value;
        }

        #endregion

        #region 浪墙 decal 绘制

        /// <summary>
        /// 绘制一面屏幕空间浪墙 (AoGuangTidalWall 着色器, 经 <see cref="ACMShaders.DrawScreenSpaceDecal"/>)。
        /// 坐标为世界系, 内部做缩放感知的世界→屏幕 UV 换算 (两轴均以屏幕高度为单位)。
        /// </summary>
        /// <param name="worldLinePoint">浪墙中心线上一点 (世界坐标)。</param>
        /// <param name="dir">行进方向单位向量 (世界系, 前沿朝向)。</param>
        /// <param name="halfThickWorld">浪体半厚 (世界像素)。</param>
        /// <param name="gapWorldPos">缺口中心 (世界坐标; 无缺口时传 worldLinePoint 且 gapHalfWorld=0)。</param>
        /// <param name="gapHalfWorld">缺口半宽 (世界像素, 0=无缺口)。</param>
        /// <param name="intensity">整体强度 0~1。</param>
        /// <param name="warnOnly">true = 半场预警幕布模式 (dir 指向安全侧)。</param>
        /// <param name="halfDir">半场遮罩方向 (指向危险半场, Zero=不启用) — 天倾竖落用。</param>
        public static void DrawTidalWallDecal(SpriteBatch sb, Vector2 worldLinePoint, Vector2 dir,
            float halfThickWorld, Vector2 gapWorldPos, float gapHalfWorld, float intensity,
            bool warnOnly = false, Vector2 halfDir = default) {
            if (Main.dedServ || intensity <= 0.01f)
                return;
            Effect fx = TidalWallEffect;
            if (fx == null)
                return;

            // 缩放感知换算: 与 ACMShaders.WorldDecalParams 同约定
            float zoom = Main.GameViewMatrix.Zoom.X;
            Vector2 halfScreen = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
            Vector2 screenPt = (worldLinePoint - Main.screenPosition - halfScreen) * zoom + halfScreen;
            Vector2 uvPoint = screenPt / new Vector2(Main.screenWidth, Main.screenHeight);

            Vector2 dirN = dir.SafeNormalize(Vector2.UnitX);
            Vector2 perp = dirN.RotatedBy(MathHelper.PiOver2);
            float gapCenter = Vector2.Dot(gapWorldPos - worldLinePoint, perp) * zoom / Main.screenHeight;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uDir"]?.SetValue(dirN);
            fx.Parameters["uLinePoint"]?.SetValue(uvPoint);
            fx.Parameters["uHalfThick"]?.SetValue(halfThickWorld * zoom / Main.screenHeight);
            fx.Parameters["uGapCenter"]?.SetValue(gapCenter);
            fx.Parameters["uGapHalf"]?.SetValue(gapHalfWorld * zoom / Main.screenHeight);
            fx.Parameters["uWarnOnly"]?.SetValue(warnOnly ? 1f : 0f);
            fx.Parameters["uHalfDir"]?.SetValue(halfDir);
            fx.Parameters["uColorDeep"]?.SetValue(new Vector4(DeepSeaBlue.ToVector3(), 0.85f));
            fx.Parameters["uColorCrest"]?.SetValue(new Vector4(FoamWhite.ToVector3(), 1f));
            fx.Parameters["uColorSafe"]?.SetValue(new Vector4(TelegraphColors.Safe.ToVector3(), 1f));
            fx.Parameters["uColorLethal"]?.SetValue(new Vector4(TelegraphColors.Lethal.ToVector3(), 1f));

            ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.AlphaBlend);
        }

        #endregion

        #region 主题颜色

        /// <summary>龙王蓝 - 主色调</summary>
        public static Color DragonBlue => new Color(50, 130, 200);

        /// <summary>海洋青 - 辅助色</summary>
        public static Color OceanTeal => new Color(70, 180, 190);

        /// <summary>水光白 - 高光色</summary>
        public static Color WaterGlow => new Color(180, 230, 255);

        /// <summary>纯白 - 核心高光</summary>
        public static Color PureWhite => new Color(255, 255, 255);

        /// <summary>深海蓝 - 暗部</summary>
        public static Color DeepSeaBlue => new Color(30, 80, 140);

        /// <summary>泡沫白 - 气泡色</summary>
        public static Color FoamWhite => new Color(220, 245, 255);

        #endregion

        #region 缓动函数

        public static float QuadOut(float t) {
            t = Math.Clamp(t, 0f, 1f);
            return 1f - (1f - t) * (1f - t);
        }

        public static float QuadIn(float t) {
            t = Math.Clamp(t, 0f, 1f);
            return t * t;
        }

        public static float SineInOut(float t) {
            t = Math.Clamp(t, 0f, 1f);
            return 0.5f - 0.5f * MathF.Cos(MathF.PI * t);
        }

        /// <summary>
        /// 获取LightShot纹理的正确旋转角度
        /// LightShot纹理正面朝右，此方法返回修正后的旋转角度
        /// </summary>
        /// <param name="targetDirection">目标方向的弧度</param>
        /// <returns>修正后的旋转角度</returns>
        public static float GetLightShotRotation(float targetDirection) {
            // LightShot默认朝右(0度)，直接返回目标方向即可
            return targetDirection;
        }

        /// <summary>
        /// 获取LightShot纹理朝上时的旋转角度
        /// </summary>
        public static float LightShotUpRotation => -MathHelper.PiOver2;

        /// <summary>
        /// 获取LightShot纹理朝下时的旋转角度
        /// </summary>
        public static float LightShotDownRotation => MathHelper.PiOver2;

        #endregion

        #region 绘制方法

        /// <summary>
        /// 绘制水漩涡
        /// </summary>
        public static void DrawWaterVortex(SpriteBatch sb, Vector2 center, float radius, float rotation, float alpha) {
            Vector2 screenPos = center - Main.screenPosition;

            if (ACMAsset.LightShot == null) return;

            Texture2D tex = ACMAsset.LightShot;
            Vector2 origin = tex.Size() / 2f;

            // 绘制多层旋转圆环
            int ringCount = 3;
            for (int ring = 0; ring < ringCount; ring++) {
                float ringRadius = radius * (0.5f + ring * 0.25f);
                float ringRot = rotation * (1f + ring * 0.3f) * (ring % 2 == 0 ? 1 : -1);
                int particleCount = 8 + ring * 4;

                for (int i = 0; i < particleCount; i++) {
                    float angle = ringRot + MathHelper.TwoPi * i / particleCount;
                    Vector2 pos = screenPos + angle.ToRotationVector2() * ringRadius;

                    float particleAlpha = alpha * (0.6f - ring * 0.15f);
                    Color color = Color.Lerp(OceanTeal, DragonBlue, ring / (float)ringCount);
                    color *= particleAlpha;
                    color.A = 0;

                    float scale = 0.3f - ring * 0.05f;
                    sb.Draw(tex, pos, null, color, 0f, origin, scale, SpriteEffects.None, 0f);
                }
            }

            // 中心发光
            Color centerColor = WaterGlow * alpha * 0.5f;
            centerColor.A = 0;
            sb.Draw(tex, screenPos, null, centerColor, 0f, origin, 0.6f * alpha, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制潮汐波
        /// </summary>
        public static void DrawTidalWave(SpriteBatch sb, Vector2 center, float radius, float alpha) {
            Vector2 screenPos = center - Main.screenPosition;

            if (ACMAsset.LightShot == null) return;

            Texture2D tex = ACMAsset.LightShot;
            Vector2 origin = tex.Size() / 2f;

            // 绘制扩散环
            int particleCount = 24;
            for (int i = 0; i < particleCount; i++) {
                float angle = MathHelper.TwoPi * i / particleCount;
                Vector2 pos = screenPos + angle.ToRotationVector2() * radius;

                Color color = Color.Lerp(DragonBlue, WaterGlow, 0.5f);
                color *= alpha * 0.7f;
                color.A = 0;

                sb.Draw(tex, pos, null, color, 0f, origin, 0.4f * alpha, SpriteEffects.None, 0f);
            }

            // 内圈
            for (int i = 0; i < particleCount / 2; i++) {
                float angle = MathHelper.TwoPi * i / (particleCount / 2) + 0.1f;
                Vector2 pos = screenPos + angle.ToRotationVector2() * (radius * 0.85f);

                Color color = OceanTeal * alpha * 0.5f;
                color.A = 0;

                sb.Draw(tex, pos, null, color, 0f, origin, 0.3f * alpha, SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 绘制巨型漩涡
        /// </summary>
        public static void DrawGiantWhirlpool(SpriteBatch sb, Vector2 center, float radius, float rotation, float alpha) {
            Vector2 screenPos = center - Main.screenPosition;

            if (ACMAsset.LightShot == null) return;

            Texture2D tex = ACMAsset.LightShot;
            Vector2 origin = tex.Size() / 2f;

            // 多层大型旋转环
            int ringCount = 5;
            for (int ring = 0; ring < ringCount; ring++) {
                float ringRadius = radius * (0.3f + ring * 0.18f);
                float ringRot = rotation * (1.5f - ring * 0.2f) * (ring % 2 == 0 ? 1 : -1);
                int particleCount = 12 + ring * 4;

                for (int i = 0; i < particleCount; i++) {
                    float angle = ringRot + MathHelper.TwoPi * i / particleCount;
                    Vector2 pos = screenPos + angle.ToRotationVector2() * ringRadius;

                    float particleAlpha = alpha * (0.7f - ring * 0.1f);
                    Color color = Color.Lerp(DeepSeaBlue, OceanTeal, ring / (float)ringCount);
                    color = Color.Lerp(color, DragonBlue, 0.3f);
                    color *= particleAlpha;
                    color.A = 0;

                    float scale = 0.5f - ring * 0.06f;
                    sb.Draw(tex, pos, null, color, 0f, origin, scale, SpriteEffects.None, 0f);
                }
            }

            // 中心黑洞效果
            Color centerDark = DeepSeaBlue * alpha * 0.8f;
            centerDark.A = 0;
            sb.Draw(tex, screenPos, null, centerDark, 0f, origin, 1.2f * alpha, SpriteEffects.None, 0f);

            // 中心发光点
            Color centerGlow = WaterGlow * alpha * 0.4f;
            centerGlow.A = 0;
            sb.Draw(tex, screenPos, null, centerGlow, 0f, origin, 0.5f * alpha, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制水光球
        /// </summary>
        public static void DrawWaterOrb(SpriteBatch sb, Vector2 center, Color baseColor, Color glowColor, float scale, float phase) {
            Vector2 screenPos = center - Main.screenPosition;

            if (ACMAsset.LightShot == null) return;

            Texture2D tex = ACMAsset.LightShot;
            Vector2 origin = tex.Size() / 2f;

            float pulse = 1f + MathF.Sin(phase) * 0.15f;

            // 外光晕
            Color outerColor = baseColor * 0.4f * pulse;
            outerColor.A = 0;
            sb.Draw(tex, screenPos, null, outerColor, 0f, origin, scale * 1.4f * pulse, SpriteEffects.None, 0f);

            // 中层
            Color midColor = Color.Lerp(baseColor, glowColor, 0.5f) * 0.6f * pulse;
            midColor.A = 0;
            sb.Draw(tex, screenPos, null, midColor, 0f, origin, scale * pulse, SpriteEffects.None, 0f);

            // 核心
            Color coreColor = glowColor * 0.8f;
            coreColor.A = 0;
            sb.Draw(tex, screenPos, null, coreColor, 0f, origin, scale * 0.6f * pulse, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制水龙光环
        /// </summary>
        public static void DrawDragonAura(SpriteBatch sb, Vector2 center, float radius, float rotation, float alpha) {
            Vector2 screenPos = center - Main.screenPosition;

            if (ACMAsset.LightShot == null) return;

            Texture2D tex = ACMAsset.LightShot;
            Vector2 origin = tex.Size() / 2f;

            // 龙形光环 - 双层旋转
            int orbCount = 8;
            for (int layer = 0; layer < 2; layer++) {
                float layerRadius = radius * (1f + layer * 0.3f);
                float layerRot = rotation * (layer == 0 ? 1 : -0.7f);

                for (int i = 0; i < orbCount; i++) {
                    float angle = layerRot + MathHelper.TwoPi * i / orbCount;
                    Vector2 pos = screenPos + angle.ToRotationVector2() * layerRadius;

                    Color color = layer == 0 ? DragonBlue : OceanTeal;
                    color *= alpha * (0.6f - layer * 0.2f);
                    color.A = 0;

                    float scale = 0.4f - layer * 0.1f;
                    sb.Draw(tex, pos, null, color, 0f, origin, scale, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion
    }
}
