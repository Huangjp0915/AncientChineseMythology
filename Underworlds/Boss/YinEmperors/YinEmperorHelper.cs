using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors
{
    /// <summary>
    /// 阴天子专用绘制工具类
    /// 提供帝王级幽冥视觉特效支持
    /// </summary>
    public static class YinEmperorHelper
    {
        #region 帝冥配色方案

        /// <summary>帝冥金 - 腐朽皇权</summary>
        public static Color ImperialGold => new Color(220, 180, 60);

        /// <summary>冥渊紫 - 深邃幽暗</summary>
        public static Color AbyssPurple => new Color(100, 30, 160);

        /// <summary>魂灯青 - 幽冥灯火</summary>
        public static Color SoulLanternCyan => new Color(80, 200, 220);

        /// <summary>冥血红 - 帝王之怒</summary>
        public static Color NetherBloodRed => new Color(200, 30, 50);

        /// <summary>阴影黑 - 纯粹暗域</summary>
        public static Color ShadowBlack => new Color(15, 10, 25);

        /// <summary>龙脉金 - 帝王龙气</summary>
        public static Color DragonVeinGold => new Color(255, 210, 80);

        /// <summary>灵符白 - 封印符文</summary>
        public static Color TalismanWhite => new Color(230, 220, 255);

        #endregion

        #region 帝王级粒子特效

        /// <summary>
        /// 创建帝冥漩涡 - 阴天子出场/阶段转换用大型特效
        /// </summary>
        public static void CreateImperialVortex(Vector2 center, float radius, float intensity, int particleCount = 40) {
            for (int i = 0; i < particleCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = radius * (0.2f + Main.rand.NextFloat(0.8f));
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                Vector2 toCenter = (center - pos).SafeNormalize(Vector2.Zero);
                float speed = intensity * (1f - dist / radius) * 10f;

                // 帝王金焰+冥紫双色粒子
                int dustType = Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.Shadowflame;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 1.8f + Main.rand.NextFloat(1.2f);
                d.velocity = toCenter * speed + new Vector2(-toCenter.Y, toCenter.X) * speed * 0.6f;
                d.alpha = 80;

                // 帝冥碎光
                if (Main.rand.NextBool(4)) {
                    var glow = Dust.NewDustPerfect(pos, DustID.YellowTorch);
                    glow.noGravity = true;
                    glow.scale = 0.9f;
                    glow.velocity = toCenter * speed * 0.4f;
                }
            }
        }

        /// <summary>
        /// 创建龙气爆发 - 帝王龙脉能量释放
        /// </summary>
        public static void CreateDragonBurst(Vector2 center, float radius, int rings = 3, int particlesPerRing = 16) {
            for (int ring = 0; ring < rings; ring++) {
                float ringRadius = radius * (ring + 1) / rings;

                for (int i = 0; i < particlesPerRing; i++) {
                    float angle = MathHelper.TwoPi * i / particlesPerRing + ring * 0.3f;
                    Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 pos = center + direction * ringRadius * 0.3f;

                    // 金色与紫色交替
                    int dustType = ring % 2 == 0 ? DustID.GoldFlame : DustID.Shadowflame;
                    var d = Dust.NewDustPerfect(pos, dustType);
                    d.noGravity = true;
                    d.scale = 2.2f - ring * 0.4f;
                    d.velocity = direction * (ringRadius / 8f);
                    d.alpha = 40;
                }
            }
        }

        /// <summary>
        /// 创建帝冥拖尾 - 移动时的幽金尾迹
        /// </summary>
        public static void CreateImperialTrail(Vector2 position, Vector2 velocity, float scale = 1f) {
            for (int i = 0; i < 4; i++) {
                Vector2 offset = Main.rand.NextVector2Circular(12 * scale, 12 * scale);
                int dustType = i % 2 == 0 ? DustID.GoldFlame : DustID.Shadowflame;
                var d = Dust.NewDustPerfect(position + offset, dustType);
                d.noGravity = true;
                d.scale = (1.6f - i * 0.25f) * scale;
                d.velocity = -velocity * 0.15f + Main.rand.NextVector2Circular(2, 2);
                d.alpha = 60;
            }

            if (Main.rand.NextBool(3)) {
                var glow = Dust.NewDustPerfect(position, DustID.YellowTorch);
                glow.noGravity = true;
                glow.scale = 0.7f * scale;
                glow.velocity = -velocity * 0.1f;
            }
        }

        /// <summary>
        /// 创建符文爆破 - 封印解除时的符文碎裂效果
        /// </summary>
        public static void CreateTalismanBurst(Vector2 center, float radius, int count = 20) {
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                Vector2 pos = center + direction * Main.rand.NextFloat(20f, radius * 0.5f);

                var d = Dust.NewDustPerfect(pos, DustID.GoldFlame);
                d.noGravity = true;
                d.scale = 1.5f + Main.rand.NextFloat(1f);
                d.velocity = direction * Main.rand.NextFloat(4f, 10f);
                d.alpha = 50;

                // 符文碎片光点
                if (Main.rand.NextBool(2)) {
                    var shard = Dust.NewDustPerfect(pos, DustID.YellowTorch);
                    shard.noGravity = true;
                    shard.scale = 0.8f;
                    shard.velocity = direction * Main.rand.NextFloat(6f, 12f) + Main.rand.NextVector2Circular(2, 2);
                }
            }
        }

        /// <summary>
        /// 创建冥雷柱 - 天降帝冥雷霆柱效果
        /// </summary>
        public static void CreateNetherLightningPillar(Vector2 top, Vector2 bottom, float intensity) {
            Vector2 direction = (bottom - top).SafeNormalize(Vector2.UnitY);
            float distance = Vector2.Distance(top, bottom);
            Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);

            int segments = (int)(distance / 12);
            for (int i = 0; i < segments; i++) {
                float progress = i / (float)segments;
                Vector2 basePos = Vector2.Lerp(top, bottom, progress);

                // 闪电锯齿
                float zigzag = MathF.Sin(progress * MathHelper.Pi * 8 + Main.GlobalTimeWrappedHourly * 20f) * 25f * intensity;
                Vector2 pos = basePos + perpendicular * zigzag;

                int dustType = Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.PurpleTorch;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 2f * intensity * (1f - MathF.Abs(progress - 0.5f) * 1.5f);
                d.velocity = perpendicular * zigzag * 0.05f;

                if (Main.rand.NextBool(3)) {
                    var spark = Dust.NewDustPerfect(pos, DustID.YellowTorch);
                    spark.noGravity = true;
                    spark.scale = 1f;
                    spark.velocity = Main.rand.NextVector2Circular(4, 4);
                }
            }
        }

        /// <summary>
        /// 创建屏幕闪烁（通过粒子模拟）
        /// </summary>
        public static void CreateScreenFlash(Vector2 center, Color color, float intensity) {
            int particleCount = (int)(60 * intensity);
            int dustType = color == ImperialGold || color == DragonVeinGold
                ? DustID.GoldFlame
                : DustID.PurpleCrystalShard;

            for (int i = 0; i < particleCount; i++) {
                Vector2 pos = center + Main.rand.NextVector2Circular(800, 600);
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 2.2f * intensity;
                d.velocity = (center - pos).SafeNormalize(Vector2.Zero) * 6f;
                d.alpha = 180;
            }
        }

        #endregion

        #region 帝冥绘制方法

        /// <summary>
        /// 绘制帝冥光环 - 环绕Boss的帝王符文光环
        /// </summary>
        public static void DrawImperialAura(SpriteBatch sb, Vector2 center, float radius, int count,
            float rotation, float pulsePhase, float intensity) {
            var tex = BAWImpermanences.BAWHelper.DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < count; i++) {
                float angle = rotation + MathHelper.TwoPi * i / count;
                float dist = radius + MathF.Sin(pulsePhase * 2f + i * 0.8f) * 12f;
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                Color auraColor = i % 2 == 0 ? ImperialGold : AbyssPurple;
                auraColor.A = 0;
                float auraScale = 0.9f + MathF.Sin(pulsePhase + i * 0.5f) * 0.2f;

                sb.Draw(tex, pos - Main.screenPosition, null, auraColor * 0.5f * intensity,
                    angle + pulsePhase, origin, auraScale, SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绘制能量波 - 扩散的帝冥冲击波
        /// </summary>
        public static void DrawEnergyWave(SpriteBatch sb, Vector2 center, float radius, float width,
            Color color, float alpha) {
            var tex = BAWImpermanences.BAWHelper.DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;
            int segments = 40;

            Color waveColor = color;
            waveColor.A = 0;

            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

                sb.Draw(tex, pos - Main.screenPosition, null, waveColor * alpha,
                    angle, origin, width / 20f, SpriteEffects.None, 0);
            }

            // 内外边缘
            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                Vector2 innerPos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (radius - width / 2);
                Vector2 outerPos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (radius + width / 2);

                sb.Draw(tex, innerPos - Main.screenPosition, null, waveColor * alpha * 0.5f,
                    angle, origin, width / 30f, SpriteEffects.None, 0);
                sb.Draw(tex, outerPos - Main.screenPosition, null, waveColor * alpha * 0.5f,
                    angle, origin, width / 30f, SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绘制龙气光柱 - 帝王出场时的冲天光柱
        /// </summary>
        public static void DrawDragonPillar(SpriteBatch sb, Vector2 basePos, float height, float width,
            float pulsePhase, float alpha) {
            var tex = BAWImpermanences.BAWHelper.DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;

            // 从底部到顶部的光柱
            int segments = (int)(height / 15);
            for (int i = 0; i < segments; i++) {
                float progress = i / (float)segments;
                Vector2 pos = basePos - new Vector2(0, height * progress);

                float pulse = 1f + MathF.Sin(pulsePhase * 3f + progress * MathHelper.Pi * 4f) * 0.3f;
                float segWidth = width * (1f - progress * 0.6f) * pulse;
                float segAlpha = alpha * (1f - progress * 0.7f);

                // 金色核心
                Color coreColor = DragonVeinGold;
                coreColor.A = 0;
                sb.Draw(tex, pos - Main.screenPosition, null, coreColor * segAlpha,
                    0f, origin, new Vector2(segWidth / tex.Width, 1.2f), SpriteEffects.None, 0);

                // 紫色外晕
                Color glowColor = AbyssPurple;
                glowColor.A = 0;
                sb.Draw(tex, pos - Main.screenPosition, null, glowColor * segAlpha * 0.4f,
                    0f, origin, new Vector2(segWidth * 1.8f / tex.Width, 1.5f), SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绘制帝王龙气环绕球
        /// </summary>
        public static void DrawDragonOrbs(SpriteBatch sb, Vector2 center, float radius, int count,
            float rotation, float pulsePhase) {
            var tex = BAWImpermanences.BAWHelper.DustTexture;
            if (tex == null) return;

            Color[] colors = [ImperialGold, AbyssPurple, SoulLanternCyan, DragonVeinGold];
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < count; i++) {
                float angle = rotation + MathHelper.TwoPi * i / count;
                float orbitRadius = radius + MathF.Sin(pulsePhase * 2f + i * 1.2f) * 8f;
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * orbitRadius;

                Color orbColor = colors[i % colors.Length];
                float orbPulse = 0.8f + MathF.Sin(pulsePhase + i * MathHelper.Pi / count) * 0.2f;

                // 拖尾
                for (int t = 1; t <= 4; t++) {
                    float trailAngle = angle - t * 0.12f;
                    Vector2 trailPos = center + new Vector2(MathF.Cos(trailAngle), MathF.Sin(trailAngle)) * orbitRadius;

                    Color trailColor = orbColor;
                    trailColor.A = 0;
                    float trailAlpha = (1f - t / 5f) * 0.35f;

                    sb.Draw(tex, trailPos - Main.screenPosition, null, trailColor * trailAlpha,
                        0f, origin, orbPulse * (1f - t * 0.12f), SpriteEffects.None, 0);
                }

                // 核心
                Color core = orbColor;
                core.A = 0;
                sb.Draw(tex, pos - Main.screenPosition, null, core * 0.7f,
                    0f, origin, orbPulse, SpriteEffects.None, 0);

                // 高光
                Color highlight = Color.White;
                highlight.A = 0;
                sb.Draw(tex, pos - Main.screenPosition, null, highlight * 0.4f,
                    0f, origin, orbPulse * 0.4f, SpriteEffects.None, 0);
            }
        }

        #endregion

        #region 工具方法

        /// <summary>平滑插值</summary>
        public static float SmoothStep(float t) => t * t * (3f - 2f * t);

        /// <summary>弹性缓出</summary>
        public static float ElasticOut(float t) {
            if (t == 0 || t == 1) return t;
            float p = 0.3f;
            return MathF.Pow(2, -10 * t) * MathF.Sin((t - p / 4) * (2 * MathF.PI) / p) + 1;
        }

        /// <summary>根据难度缩放伤害</summary>
        public static int GetScaledDamage(int baseDamage) {
            if (Main.masterMode)
                return (int)(baseDamage * 1.5f);
            if (Main.expertMode)
                return (int)(baseDamage * 1.25f);
            return baseDamage;
        }

        #endregion
    }
}
