using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas
{
    /// <summary>
    /// 毗沙门天王Boss辅助类
    /// 仙气类白色主题视觉效果
    /// </summary>
    public static class VaisravanaHelper
    {
        public static string Path => typeof(VaisravanaHelper).Namespace.Replace(".", "/") + "/";

        #region 纹理资源

        private static Asset<Texture2D> _towerTexture;
        private static Asset<Texture2D> _dustTexture;

        /// <summary>宝塔纹理</summary>
        public static Texture2D TowerTexture => (_towerTexture ??= ModContent.Request<Texture2D>(Path + "VaisravanaTower")).Value;

        /// <summary>通用粒子纹理</summary>
        public static Texture2D DustTexture => (_dustTexture ??= ModContent.Request<Texture2D>(Path + "VaisravanaDust")).Value;

        #endregion

        #region 颜色定义 - 仙气白色主题

        /// <summary>主色调 - 圣洁白</summary>
        public static Color PureWhite => new Color(255, 255, 255);

        /// <summary>辅色调 - 仙光金</summary>
        public static Color ImmortalGold => new Color(255, 245, 220);

        /// <summary>光晕色 - 天青白</summary>
        public static Color CelestialAzure => new Color(230, 245, 255);

        /// <summary>能量色 - 灵光银</summary>
        public static Color SpiritSilver => new Color(240, 248, 255);

        /// <summary>宝塔色 - 琉璃金</summary>
        public static Color TowerGold => new Color(255, 230, 180);

        /// <summary>神威色 - 圣域白金</summary>
        public static Color DivineWhite => new Color(255, 252, 248);

        #endregion

        #region 随机工具

        public static float RandFloat(double a, double b = 0) {
            var max = (float)Math.Max(a, b);
            var min = (float)Math.Min(a, b);
            return Main.rand.NextFloat(min, max);
        }

        public static int RandInt(double a, double b = 0) {
            var max = (int)Math.Max(a, b);
            var min = (int)Math.Min(a, b);
            return Main.rand.Next(min, max + 1);
        }

        #endregion

        #region 绘制辅助 - 仙气主题

        /// <summary>
        /// 绘制仙气光球效果
        /// </summary>
        public static void DrawImmortalOrb(SpriteBatch sb, Vector2 position, Color coreColor, Color glowColor,
            float scale, float pulsePhase) {
            var tex = ACMAsset.LightShot;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.12f;

            // 外层光晕（多层渐变）
            Color glow = glowColor;
            glow.A = 0;
            for (int i = 4; i >= 0; i--) {
                float layerScale = scale * pulse * (2f + i * 0.5f);
                float layerAlpha = 0.12f / (i + 1);
                sb.Draw(tex, position - Main.screenPosition, null, glow * layerAlpha,
                    0f, origin, layerScale, SpriteEffects.None, 0);
            }

            // 核心
            Color core = coreColor;
            core.A = 0;
            sb.Draw(tex, position - Main.screenPosition, null, core,
                0f, origin, scale * pulse, SpriteEffects.None, 0);

            // 中心高光
            Color highlight = PureWhite;
            highlight.A = 0;
            sb.Draw(tex, position - Main.screenPosition, null, highlight * 0.6f,
                0f, origin, scale * pulse * 0.4f, SpriteEffects.None, 0);
        }

        /// <summary>
        /// 绘制仙气光环
        /// </summary>
        public static void DrawImmortalHalo(SpriteBatch sb, Vector2 center, float radius, Color color,
            float rotation, float alpha = 1f) {
            var tex = ACMAsset.BlankStar;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;

            // 光环外圈
            Color haloColor = color;
            haloColor.A = 0;

            int segments = 16;
            for (int i = 0; i < segments; i++) {
                float angle = rotation + MathHelper.TwoPi * i / segments;
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

                float pulseScale = 0.3f + MathF.Sin(angle * 2 + rotation * 3) * 0.1f;
                sb.Draw(tex, pos - Main.screenPosition, null, haloColor * alpha * 0.5f,
                    angle, origin, pulseScale, SpriteEffects.None, 0);
            }

            // 内圈连接线效果
            if (ACMAsset.LightShot != null) {
                for (int i = 0; i < segments / 2; i++) {
                    float angle = rotation * 0.5f + MathHelper.TwoPi * i / (segments / 2);
                    Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * 0.7f;
                    sb.Draw(ACMAsset.LightShot, pos - Main.screenPosition, null, haloColor * alpha * 0.3f,
                        0f, ACMAsset.LightShot.Size() / 2f, 0.4f, SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>
        /// 绘制宝塔光柱
        /// </summary>
        public static void DrawTowerBeam(SpriteBatch sb, Vector2 start, Vector2 end, Color color,
            float width, float timeOffset) {
            var tex = ACMAsset.GlaciateWave;
            if (tex == null) return;

            Vector2 direction = (end - start).SafeNormalize(Vector2.Zero);
            float distance = Vector2.Distance(start, end);
            float rotation = direction.ToRotation();

            Vector2 origin = new Vector2(0, tex.Height / 2f);
            Vector2 scale = new Vector2(distance / tex.Width, width / tex.Height);

            // 多层光柱
            Color beamColor = color;
            beamColor.A = 0;

            for (int i = 3; i >= 0; i--) {
                float layerWidth = 1f + i * 0.4f;
                float layerAlpha = 0.8f - i * 0.15f;
                float pulse = 1f + MathF.Sin(timeOffset * 0.1f + i) * 0.1f;

                sb.Draw(tex, start - Main.screenPosition, null, beamColor * layerAlpha,
                    rotation, origin, scale * new Vector2(1f, layerWidth * pulse), SpriteEffects.None, 0);
            }

            // 起点光球
            if (ACMAsset.LightShot != null) {
                Color orbColor = PureWhite;
                orbColor.A = 0;
                sb.Draw(ACMAsset.LightShot, start - Main.screenPosition, null, orbColor * 0.8f,
                    0f, ACMAsset.LightShot.Size() / 2f, width * 0.03f, SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绘制飘逸仙气拖尾
        /// </summary>
        public static void DrawImmortalTrail(SpriteBatch sb, Vector2[] oldPositions, float[] oldRotations,
            Texture2D texture, Color color, float baseScale, float alpha = 1f) {
            if (texture == null || oldPositions == null) return;

            Vector2 origin = texture.Size() / 2f;
            Color trailColor = color;
            trailColor.A = 0;

            for (int i = oldPositions.Length - 1; i >= 0; i--) {
                if (oldPositions[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / oldPositions.Length;
                float trailAlpha = progress * 0.5f * alpha;
                float trailScale = baseScale * (0.4f + progress * 0.6f);

                // 飘动偏移
                float wobble = MathF.Sin(Main.GameUpdateCount * 0.05f + i * 0.4f) * 3f;
                Vector2 drawPos = oldPositions[i] - Main.screenPosition;
                drawPos.Y += wobble;

                float rot = oldRotations != null && i < oldRotations.Length ? oldRotations[i] : 0f;

                sb.Draw(texture, drawPos, null, trailColor * trailAlpha,
                    rot, origin, trailScale, SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绘制星芒爆发效果
        /// </summary>
        public static void DrawStarBurst(SpriteBatch sb, Vector2 center, Color color, float scale, float rotation) {
            var tex = ACMAsset.Sparkle;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;
            Color burstColor = color;
            burstColor.A = 0;

            // 多层星芒
            for (int i = 0; i < 3; i++) {
                float layerRot = rotation + i * MathHelper.TwoPi / 6f;
                float layerScale = scale * (1f + i * 0.3f);
                float layerAlpha = 0.6f - i * 0.15f;

                sb.Draw(tex, center - Main.screenPosition, null, burstColor * layerAlpha,
                    layerRot, origin, layerScale, SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绘制神圣光环（圆形）
        /// </summary>
        public static void DrawDivineCircle(SpriteBatch sb, Vector2 center, float radius, Color color,
            float rotation, float alpha = 1f) {
            var tex = ACMAsset.LightShot;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;
            Color circleColor = color;
            circleColor.A = 0;

            int points = 24;
            for (int i = 0; i < points; i++) {
                float angle = rotation + MathHelper.TwoPi * i / points;
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

                float pointScale = 0.5f + MathF.Sin(angle * 4 + rotation * 2) * 0.15f;
                sb.Draw(tex, pos - Main.screenPosition, null, circleColor * alpha * 0.4f,
                    0f, origin, pointScale, SpriteEffects.None, 0);
            }

            // 中心大光球
            sb.Draw(tex, center - Main.screenPosition, null, circleColor * alpha * 0.2f,
                0f, origin, radius / 32f, SpriteEffects.None, 0);
        }

        #endregion

        #region 缓动函数

        public static float SmoothStep(float t) => t * t * (3f - 2f * t);

        public static float ElasticOut(float t) {
            if (t == 0 || t == 1) return t;
            float p = 0.3f;
            return MathF.Pow(2, -10 * t) * MathF.Sin((t - p / 4) * (2 * MathF.PI) / p) + 1;
        }

        public static float QuadIn(float t) => t * t;

        public static float QuadOut(float t) => 1f - (1f - t) * (1f - t);

        public static float BackOut(float t) {
            float c1 = 1.70158f;
            float c3 = c1 + 1;
            return 1 + c3 * MathF.Pow(t - 1, 3) + c1 * MathF.Pow(t - 1, 2);
        }

        #endregion
    }

    /// <summary>
    /// 毗沙门天王专用粒子 - 仙气白色主题
    /// </summary>
    public class VaisravanaDust : ModDust
    {
        public override string Texture => VaisravanaHelper.Path + "VaisravanaDust";

        public override void OnSpawn(Dust dust) {
            dust.noLight = true;
            dust.noGravity = true;
            dust.alpha = 200;
            dust.scale = VaisravanaHelper.RandFloat(0.8f, 1.4f);
            dust.velocity = new Vector2(VaisravanaHelper.RandFloat(1, 3)).RotatedByRandom(MathHelper.TwoPi);
            dust.color = VaisravanaHelper.PureWhite;
        }

        public override bool Update(Dust dust) {
            dust.position += dust.velocity;
            dust.scale -= 0.015f;
            dust.velocity *= 0.96f;
            dust.alpha -= 4;

            // 上飘效果（仙气飘逸感）
            dust.velocity.Y -= 0.02f;

            // 白色仙光
            Lighting.AddLight(dust.position, new Vector3(0.9f, 0.95f, 1f) * dust.scale * 0.3f);

            if (dust.scale <= 0 || dust.alpha < 0)
                dust.active = false;

            return false;
        }
    }

    /// <summary>
    /// 宝塔金光粒子
    /// </summary>
    public class TowerGoldDust : ModDust
    {
        public override string Texture => VaisravanaHelper.Path + "VaisravanaDust";

        public override void OnSpawn(Dust dust) {
            dust.noLight = true;
            dust.noGravity = true;
            dust.alpha = 180;
            dust.scale = VaisravanaHelper.RandFloat(0.6f, 1.1f);
            dust.color = VaisravanaHelper.TowerGold;
        }

        public override bool Update(Dust dust) {
            dust.position += dust.velocity;
            dust.scale -= 0.012f;
            dust.velocity *= 0.97f;
            dust.alpha -= 3;

            // 金色宝光
            Lighting.AddLight(dust.position, new Vector3(1f, 0.95f, 0.8f) * dust.scale * 0.4f);

            if (dust.scale <= 0 || dust.alpha < 0)
                dust.active = false;

            return false;
        }
    }

    /// <summary>
    /// 灵光银尘 - 飘逸的仙气粒子
    /// </summary>
    public class SpiritSilverDust : ModDust
    {
        public override string Texture => VaisravanaHelper.Path + "VaisravanaDust";

        public override void OnSpawn(Dust dust) {
            dust.noLight = true;
            dust.noGravity = true;
            dust.alpha = 220;
            dust.scale = VaisravanaHelper.RandFloat(0.7f, 1.2f);
            dust.color = VaisravanaHelper.SpiritSilver;
        }

        public override bool Update(Dust dust) {
            dust.position += dust.velocity;
            dust.scale -= 0.01f;
            dust.velocity *= 0.98f;
            dust.alpha -= 3;

            // 飘逸的波动
            dust.velocity.X += MathF.Sin(Main.GameUpdateCount * 0.08f + dust.position.Y * 0.01f) * 0.03f;
            dust.velocity.Y -= 0.015f;

            // 银白灵光
            Lighting.AddLight(dust.position, new Vector3(0.85f, 0.9f, 1f) * dust.scale * 0.35f);

            if (dust.scale <= 0 || dust.alpha < 0)
                dust.active = false;

            return false;
        }
    }
}
