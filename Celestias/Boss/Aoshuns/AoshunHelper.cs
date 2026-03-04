using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    /// <summary>
    /// 北海龙王敖顺 - 辅助工具类
    /// 雷电/风暴主题配色，视觉效果辅助
    /// </summary>
    public static class AoshunHelper
    {
        #region 主题配色 - 雷电/北海色系

        /// <summary>雷霆紫 - 核心雷电</summary>
        public static Color ThunderPurple => new Color(120, 50, 200);

        /// <summary>闪电蓝 - 电弧主色</summary>
        public static Color LightningBlue => new Color(80, 160, 255);

        /// <summary>电光白 - 高光色</summary>
        public static Color ElectricWhite => new Color(230, 240, 255);

        /// <summary>风暴灰 - 乌云压迫</summary>
        public static Color StormGray => new Color(50, 55, 70);

        /// <summary>纯白 - 核心高光</summary>
        public static Color PureWhite => new Color(255, 255, 255);

        /// <summary>深渊墨紫 - 深沉压迫</summary>
        public static Color AbyssPurple => new Color(20, 10, 40);

        /// <summary>北海青 - 龙王尊贵色</summary>
        public static Color NorthSeaCyan => new Color(30, 120, 180);

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

        #region 粒子效果

        /// <summary>
        /// 创建雷电旋涡粒子 - 阶段转换/攻击使用
        /// </summary>
        public static void CreateThunderVortex(Vector2 center, float radius, float intensity, int particleCount = 40) {
            for (int i = 0; i < particleCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = radius * (0.2f + Main.rand.NextFloat(0.8f));
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                Vector2 toCenter = (center - pos).SafeNormalize(Vector2.Zero);
                float speed = intensity * (1f - dist / radius) * 10f;

                int dustType = Main.rand.NextBool(3) ? DustID.Electric : DustID.PurpleTorch;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 1.8f + Main.rand.NextFloat(1.2f);
                d.velocity = toCenter * speed + new Vector2(-toCenter.Y, toCenter.X) * speed * 0.6f;
                d.alpha = 80;
            }
        }

        /// <summary>
        /// 创建雷电爆发 - 冲刺/击中时使用
        /// </summary>
        public static void CreateThunderBurst(Vector2 center, float radius, int rings = 3, int particlesPerRing = 16) {
            for (int ring = 0; ring < rings; ring++) {
                float ringRadius = radius * (ring + 1) / rings;

                for (int i = 0; i < particlesPerRing; i++) {
                    float angle = MathHelper.TwoPi * i / particlesPerRing + ring * 0.3f;
                    Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 pos = center + direction * ringRadius * 0.3f;

                    int dustType = ring % 2 == 0 ? DustID.Electric : DustID.PurpleTorch;
                    var d = Dust.NewDustPerfect(pos, dustType);
                    d.noGravity = true;
                    d.scale = 2.5f - ring * 0.4f;
                    d.velocity = direction * (8f + ring * 3f);
                    d.alpha = 60;
                }
            }
        }

        /// <summary>
        /// 创建雷电尾迹粒子
        /// </summary>
        public static void CreateLightningTrail(Vector2 position, Vector2 velocity, float scale = 1f) {
            for (int i = 0; i < 3; i++) {
                Vector2 dustPos = position + Main.rand.NextVector2Circular(20, 20);
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
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
        /// 绘制雷电光环
        /// </summary>
        public static void DrawThunderAura(SpriteBatch sb, Vector2 center, float radius, float rotation, float alpha) {
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
                    Color color = Color.Lerp(LightningBlue, ThunderPurple, ring / (float)ringCount);
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
