using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    /// <summary>
    /// 南海龙王敖钦 - 辅助工具类
    /// 火属性龙王主题，颜色和视觉特效工具
    /// </summary>
    public static class AokinHelper
    {
        #region 主题颜色 - 火焰/南海色系

        /// <summary>龙焰红 - 核心火焰</summary>
        public static Color DragonFlameRed => new Color(220, 60, 30);

        /// <summary>熔岩橙 - 炽热龙息</summary>
        public static Color MoltenOrange => new Color(255, 140, 30);

        /// <summary>烈焰金 - 高光色</summary>
        public static Color BlazingGold => new Color(255, 210, 80);

        /// <summary>深焰紫 - 龙王威严</summary>
        public static Color DeepFlamePurple => new Color(160, 40, 80);

        /// <summary>纯白 - 核心高光</summary>
        public static Color PureWhite => new Color(255, 255, 255);

        /// <summary>焦炭黑 - 暗部色</summary>
        public static Color EmberBlack => new Color(40, 15, 10);

        /// <summary>南海碧 - 龙王海域底色</summary>
        public static Color SouthSeaTeal => new Color(50, 160, 140);

        #endregion

        #region 缓动函数

        public static float QuadOut(float t) {
            t = Math.Clamp(t, 0f, 1f);
            return 1f - (1f - t) * (1f - t);
        }

        public static float SineInOut(float t) {
            t = Math.Clamp(t, 0f, 1f);
            return 0.5f - 0.5f * MathF.Cos(MathF.PI * t);
        }

        #endregion

        #region 粒子特效

        /// <summary>
        /// 创建火焰漩涡粒子 - 阶段转换/出场使用
        /// </summary>
        public static void CreateFlameVortex(Vector2 center, float radius, float intensity, int particleCount = 40) {
            for (int i = 0; i < particleCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = radius * (0.2f + Main.rand.NextFloat(0.8f));
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                Vector2 toCenter = (center - pos).SafeNormalize(Vector2.Zero);
                float speed = intensity * (1f - dist / radius) * 10f;

                int dustType = Main.rand.NextBool(3) ? DustID.Torch : DustID.SolarFlare;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 1.8f + Main.rand.NextFloat(1.2f);
                d.velocity = toCenter * speed + new Vector2(-toCenter.Y, toCenter.X) * speed * 0.6f;
                d.alpha = 80;
            }
        }

        /// <summary>
        /// 创建龙焰爆发 - 冲刺/咆哮时使用
        /// </summary>
        public static void CreateDragonFireBurst(Vector2 center, float radius, int rings = 3, int particlesPerRing = 16) {
            for (int ring = 0; ring < rings; ring++) {
                float ringRadius = radius * (ring + 1) / rings;

                for (int i = 0; i < particlesPerRing; i++) {
                    float angle = MathHelper.TwoPi * i / particlesPerRing + ring * 0.3f;
                    Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 pos = center + direction * ringRadius * 0.3f;

                    int dustType = ring % 2 == 0 ? DustID.Torch : DustID.SolarFlare;
                    var d = Dust.NewDustPerfect(pos, dustType);
                    d.noGravity = true;
                    d.scale = 2.5f - ring * 0.4f;
                    d.velocity = direction * (8f + ring * 3f);
                    d.alpha = 60;
                }
            }
        }

        /// <summary>
        /// 创建火焰拖尾粒子
        /// </summary>
        public static void CreateFireTrail(Vector2 position, Vector2 velocity, float scale = 1f) {
            for (int i = 0; i < 3; i++) {
                Vector2 dustPos = position + Main.rand.NextVector2Circular(20, 20);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                var d = Dust.NewDustPerfect(dustPos, dustType);
                d.noGravity = true;
                d.scale = (1.5f + Main.rand.NextFloat(0.8f)) * scale;
                d.velocity = -velocity * 0.2f + Main.rand.NextVector2Circular(2, 2);
                d.alpha = 100;
            }
        }

        #endregion

        #region 绘制辅助

        /// <summary>
        /// 绘制火焰光环
        /// </summary>
        public static void DrawFlameAura(SpriteBatch sb, Vector2 center, float radius, float rotation, float alpha) {
            if (ACMAsset.SoftGlow == null) return;

            Texture2D tex = ACMAsset.SoftGlow;
            Vector2 origin = tex.Size() / 2f;
            Vector2 screenPos = center - Main.screenPosition;

            int ringCount = 3;
            for (int ring = 0; ring < ringCount; ring++) {
                float ringRadius = radius * (0.5f + ring * 0.25f);
                float ringRot = rotation * (1f + ring * 0.3f) * (ring % 2 == 0 ? 1 : -1);
                int particleCount = 8 + ring * 4;

                for (int i = 0; i < particleCount; i++) {
                    float angle = ringRot + MathHelper.TwoPi * i / particleCount;
                    Vector2 pos = screenPos + angle.ToRotationVector2() * ringRadius;

                    float particleAlpha = alpha * (0.6f - ring * 0.15f);
                    Color color = Color.Lerp(MoltenOrange, DragonFlameRed, ring / (float)ringCount);
                    color *= particleAlpha;
                    color.A = 0;

                    float particleScale = (0.5f - ring * 0.1f) * (1f + MathF.Sin(angle * 3f + rotation * 5f) * 0.2f);
                    sb.Draw(tex, pos, null, color, 0f, origin, particleScale, SpriteEffects.None, 0);
                }
            }
        }

        #endregion
    }
}
