using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    /// <summary>
    /// 东海龙王辅助类 - 颜色和绘制工具
    /// </summary>
    public static class AoGuangHelper
    {
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
