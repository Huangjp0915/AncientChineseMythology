using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers
{
    /// <summary>
    /// 觉醒-冥府尽头-幽冥龙 终局Boss专用绘制工具类
    /// 提供极致的视觉特效支持
    /// </summary>
    public static class AwakeningNetherHelper
    {
        public static string Path => typeof(AwakeningNetherHelper).Namespace.Replace(".", "/") + "/";

        #region 纹理资源

        private static Asset<Texture2D> _voidCoreTexture;
        private static Asset<Texture2D> _energyRingTexture;
        private static Asset<Texture2D> _riftTexture;

        /// <summary>虚空核心纹理</summary>
        public static Texture2D VoidCoreTexture => (_voidCoreTexture ??= ModContent.Request<Texture2D>(Path + "VoidCore")).Value;

        /// <summary>能量环纹理</summary>
        public static Texture2D EnergyRingTexture => (_energyRingTexture ??= ModContent.Request<Texture2D>(Path + "EnergyRing")).Value;

        /// <summary>裂隙纹理</summary>
        public static Texture2D RiftTexture => (_riftTexture ??= ModContent.Request<Texture2D>(Path + "Rift")).Value;

        // 复用BAWHelper的基础纹理
        private static Texture2D DustTexture => BAWImpermanences.BAWHelper.DustTexture;

        #endregion

        #region 终局级特效颜色

        /// <summary>觉醒紫色 - 主色调</summary>
        public static Color AwakeningPurple => new Color(180, 60, 255);

        /// <summary>虚空黑紫 - 深色</summary>
        public static Color VoidDarkPurple => new Color(80, 20, 140);

        /// <summary>幽冥青 - 辅助色</summary>
        public static Color NetherCyan => new Color(100, 200, 255);

        /// <summary>灵魂粉 - 高光色</summary>
        public static Color SoulPink => new Color(255, 120, 200);

        /// <summary>毁灭红 - 狂暴色</summary>
        public static Color DestructionRed => new Color(255, 50, 80);

        #endregion

        #region 极致粒子效果

        /// <summary>
        /// 创建虚空漩涡粒子 - 大范围的吸入效果
        /// </summary>
        public static void CreateVoidVortex(Vector2 center, float radius, float intensity, int particleCount = 30) {
            for (int i = 0; i < particleCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = radius * (0.3f + Main.rand.NextFloat(0.7f));
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                // 向中心吸入
                Vector2 toCenter = (center - pos).SafeNormalize(Vector2.Zero);
                float speed = intensity * (1f - dist / radius) * 8f;

                int dustType = Main.rand.NextBool(3) ? DustID.Shadowflame : DustID.PurpleTorch;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 1.5f + Main.rand.NextFloat(1f);
                d.velocity = toCenter * speed + new Vector2(-toCenter.Y, toCenter.X) * speed * 0.5f;
                d.alpha = 100;

                // 添加发光粒子
                if (Main.rand.NextBool(3)) {
                    var glow = Dust.NewDustPerfect(pos, DustID.PurpleCrystalShard);
                    glow.noGravity = true;
                    glow.scale = 0.8f;
                    glow.velocity = toCenter * speed * 0.5f;
                }
            }
        }

        /// <summary>
        /// 创建次元撕裂粒子 - 空间撕裂的视觉效果
        /// </summary>
        public static void CreateDimensionTear(Vector2 start, Vector2 end, float intensity) {
            Vector2 direction = (end - start).SafeNormalize(Vector2.Zero);
            float distance = Vector2.Distance(start, end);
            Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);

            int segments = (int)(distance / 15);
            for (int i = 0; i < segments; i++) {
                float progress = i / (float)segments;
                Vector2 basePos = Vector2.Lerp(start, end, progress);

                // 锯齿形撕裂
                float zigzag = MathF.Sin(progress * MathHelper.Pi * 6) * 20f * intensity;
                Vector2 pos = basePos + perpendicular * zigzag;

                // 主撕裂粒子
                var d = Dust.NewDustPerfect(pos, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 2f * intensity * (1f - MathF.Abs(progress - 0.5f) * 2f);
                d.velocity = perpendicular * zigzag * 0.1f;

                // 边缘能量
                if (Main.rand.NextBool(2)) {
                    var edge = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(10, 10), DustID.PurpleTorch);
                    edge.noGravity = true;
                    edge.scale = 1.2f;
                    edge.velocity = Main.rand.NextVector2Circular(3, 3);
                }
            }
        }

        /// <summary>
        /// 创建灵魂爆发粒子 - 环形扩散
        /// </summary>
        public static void CreateSoulBurst(Vector2 center, float radius, int rings = 3, int particlesPerRing = 16) {
            for (int ring = 0; ring < rings; ring++) {
                float ringRadius = radius * (ring + 1) / rings;
                float ringDelay = ring * 0.1f;

                for (int i = 0; i < particlesPerRing; i++) {
                    float angle = MathHelper.TwoPi * i / particlesPerRing + ring * 0.2f;
                    Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 pos = center + direction * ringRadius * 0.3f;

                    int dustType = ring % 2 == 0 ? DustID.Shadowflame : DustID.SpectreStaff;
                    var d = Dust.NewDustPerfect(pos, dustType);
                    d.noGravity = true;
                    d.scale = 2f - ring * 0.3f;
                    d.velocity = direction * (ringRadius / 10f);
                    d.alpha = 50;
                }
            }
        }

        /// <summary>
        /// 创建虚空拖尾粒子 - 用于高速移动
        /// </summary>
        public static void CreateVoidTrail(Vector2 position, Vector2 velocity, float scale = 1f) {
            // 主拖尾
            for (int i = 0; i < 3; i++) {
                Vector2 offset = Main.rand.NextVector2Circular(10 * scale, 10 * scale);
                var d = Dust.NewDustPerfect(position + offset, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = (1.5f - i * 0.3f) * scale;
                d.velocity = -velocity * 0.2f + Main.rand.NextVector2Circular(2, 2);
                d.alpha = 80;
            }

            // 能量残影
            if (Main.rand.NextBool(2)) {
                var glow = Dust.NewDustPerfect(position, DustID.PurpleCrystalShard);
                glow.noGravity = true;
                glow.scale = 0.6f * scale;
                glow.velocity = -velocity * 0.1f;
            }
        }

        #endregion

        #region 终局级绘制方法

        /// <summary>
        /// 绘制虚空核心 - 带有多层光晕和脉动效果
        /// </summary>
        public static void DrawVoidCore(SpriteBatch sb, Vector2 position, Color coreColor, Color glowColor,
            float scale, float pulsePhase, bool isEnraged = false) {
            var tex = DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.2f + MathF.Sin(pulsePhase * 2.3f) * 0.1f;

            // 狂暴时额外的不稳定效果
            if (isEnraged) {
                pulse += MathF.Sin(pulsePhase * 5f) * 0.15f;
                position += Main.rand.NextVector2Circular(3, 3);
            }

            // 外层光晕（多层叠加）
            Color glow = glowColor;
            glow.A = 0;
            for (int i = 5; i >= 0; i--) {
                float layerScale = scale * pulse * (2f + i * 0.5f);
                float layerAlpha = 0.1f / (i + 1);

                // 每层轻微偏移创造不稳定感
                Vector2 layerOffset = new Vector2(
                    MathF.Sin(pulsePhase + i * 0.5f) * 2f,
                    MathF.Cos(pulsePhase * 1.3f + i * 0.5f) * 2f
                );

                sb.Draw(tex, position + layerOffset - Main.screenPosition, null, glow * layerAlpha,
                    pulsePhase * 0.1f * i, origin, layerScale, SpriteEffects.None, 0);
            }

            // 能量漩涡层
            for (int i = 0; i < 3; i++) {
                float swirl = pulsePhase * (0.5f + i * 0.3f);
                float swirlScale = scale * pulse * (1.3f - i * 0.15f);
                Color swirlColor = Color.Lerp(glowColor, coreColor, i / 3f);
                swirlColor.A = 0;

                sb.Draw(tex, position - Main.screenPosition, null, swirlColor * (0.3f - i * 0.08f),
                    swirl, origin, swirlScale, SpriteEffects.None, 0);
            }

            // 核心
            sb.Draw(tex, position - Main.screenPosition, null, coreColor,
                0f, origin, scale * pulse, SpriteEffects.None, 0);

            // 中心高光
            Color highlight = Color.White;
            highlight.A = 0;
            sb.Draw(tex, position - Main.screenPosition, null, highlight * 0.6f,
                0f, origin, scale * pulse * 0.4f, SpriteEffects.None, 0);

            // 狂暴时的额外能量环
            if (isEnraged) {
                for (int i = 0; i < 2; i++) {
                    float ringAngle = pulsePhase * 2f + i * MathHelper.Pi;
                    float ringDist = scale * 30f * pulse;
                    Vector2 ringPos = position + new Vector2(MathF.Cos(ringAngle), MathF.Sin(ringAngle)) * ringDist;

                    Color ringColor = DestructionRed;
                    ringColor.A = 0;
                    sb.Draw(tex, ringPos - Main.screenPosition, null, ringColor * 0.5f,
                        ringAngle, origin, scale * 0.5f, SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>
        /// 绘制能量光束 - 带有波动和粒子效果的激光
        /// </summary>
        public static void DrawEnergyBeam(SpriteBatch sb, Vector2 start, Vector2 end, Color color,
            float width, float timeOffset, bool intense = false) {
            var tex = DustTexture;
            if (tex == null) return;

            Vector2 direction = (end - start).SafeNormalize(Vector2.Zero);
            float distance = Vector2.Distance(start, end);
            float rotation = direction.ToRotation();
            Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);

            int segments = (int)(distance / 6);
            float intensityMod = intense ? 1.5f : 1f;

            // 外层光晕
            Color glowColor = color;
            glowColor.A = 0;

            for (int layer = 0; layer < (intense ? 3 : 2); layer++) {
                float layerWidth = width * (1.5f + layer * 0.8f) * intensityMod;

                for (int i = 0; i < segments; i++) {
                    float progress = i / (float)segments;
                    Vector2 basePos = start + direction * (progress * distance);

                    // 多重波动
                    float wave1 = MathF.Sin(progress * MathHelper.TwoPi * 3f + timeOffset * 0.15f) * layerWidth * 0.4f;
                    float wave2 = MathF.Sin(progress * MathHelper.TwoPi * 7f + timeOffset * 0.25f) * layerWidth * 0.15f;
                    Vector2 pos = basePos + perpendicular * (wave1 + wave2);

                    float pulse = 0.5f + MathF.Sin(progress * MathHelper.Pi * 5f + timeOffset * 0.2f) * 0.5f;
                    float widthMod = MathF.Sin(progress * MathHelper.Pi) * (0.4f + pulse * 0.6f);

                    Color segColor = glowColor * (0.2f / (layer + 1)) * pulse;

                    sb.Draw(tex, pos - Main.screenPosition, null, segColor,
                        rotation, tex.Size() / 2f, new Vector2(layerWidth * widthMod / tex.Width, 0.8f), SpriteEffects.None, 0);
                }
            }

            // 核心光束
            for (int i = 0; i < segments; i++) {
                float progress = i / (float)segments;
                Vector2 basePos = start + direction * (progress * distance);

                float wave = MathF.Sin(progress * MathHelper.TwoPi * 3f + timeOffset * 0.15f) * width * 0.3f;
                Vector2 pos = basePos + perpendicular * wave;

                float pulse = 0.7f + MathF.Sin(progress * MathHelper.Pi * 4f + timeOffset * 0.3f) * 0.3f;
                float widthMod = MathF.Sin(progress * MathHelper.Pi);

                Color coreColor = Color.Lerp(color, Color.White, 0.3f) * pulse;
                coreColor.A = 0;

                sb.Draw(tex, pos - Main.screenPosition, null, coreColor,
                    rotation, tex.Size() / 2f, new Vector2(width * widthMod / tex.Width * intensityMod, 0.6f), SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绘制次元裂隙 - 空间撕裂的视觉效果
        /// </summary>
        public static void DrawDimensionRift(SpriteBatch sb, Vector2 center, float scale, float rotation,
            float pulsePhase, bool closing = false) {
            var tex = DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;
            float closingMod = closing ? (1f - pulsePhase * 0.01f) : 1f;
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.15f;

            // 裂隙边缘的不稳定能量
            int edgeCount = 12;
            for (int i = 0; i < edgeCount; i++) {
                float angle = rotation + MathHelper.TwoPi * i / edgeCount;
                float edgeDist = scale * 40f * pulse * closingMod;
                float wobble = MathF.Sin(pulsePhase * 2f + i * 0.8f) * 15f;
                Vector2 edgePos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.4f) * (edgeDist + wobble);

                Color edgeColor = VoidDarkPurple;
                edgeColor.A = 0;
                float edgeScale = 1.2f + MathF.Sin(pulsePhase + i) * 0.3f;

                sb.Draw(tex, edgePos - Main.screenPosition, null, edgeColor * 0.6f,
                    angle + pulsePhase * 0.5f, origin, edgeScale * closingMod, SpriteEffects.None, 0);
            }

            // 中心黑洞效果
            for (int layer = 4; layer >= 0; layer--) {
                float layerScale = scale * (0.5f + layer * 0.3f) * pulse * closingMod;
                Color layerColor = Color.Lerp(VoidDarkPurple, Color.Black, layer / 5f);
                layerColor.A = (byte)(200 - layer * 30);

                // 椭圆形裂隙
                Vector2 riftScale = new Vector2(layerScale, layerScale * 0.35f);

                sb.Draw(tex, center - Main.screenPosition, null, layerColor,
                    rotation, origin, riftScale, SpriteEffects.None, 0);
            }

            // 裂隙内部的能量涌动
            for (int i = 0; i < 6; i++) {
                float innerAngle = pulsePhase * 1.5f + i * MathHelper.TwoPi / 6f;
                float innerDist = scale * 15f * MathF.Sin(pulsePhase + i) * closingMod;
                Vector2 innerPos = center + new Vector2(MathF.Cos(innerAngle) * innerDist, MathF.Sin(innerAngle) * innerDist * 0.3f);

                Color innerColor = AwakeningPurple;
                innerColor.A = 0;

                sb.Draw(tex, innerPos - Main.screenPosition, null, innerColor * 0.7f,
                    innerAngle, origin, 0.8f * closingMod, SpriteEffects.None, 0);
            }

            // 边缘高光
            Color highlightColor = NetherCyan;
            highlightColor.A = 0;
            sb.Draw(tex, center - Main.screenPosition, null, highlightColor * 0.3f * closingMod,
                rotation, origin, new Vector2(scale * pulse * 1.1f, scale * pulse * 0.4f) * closingMod, SpriteEffects.None, 0);
        }

        /// <summary>
        /// 绘制灵魂环绕效果 - 多个灵魂球围绕中心旋转
        /// </summary>
        public static void DrawSoulOrbit(SpriteBatch sb, Vector2 center, float radius, int count,
            float rotation, float pulsePhase, Color[] colors = null) {
            var tex = DustTexture;
            if (tex == null) return;

            colors ??= [AwakeningPurple, NetherCyan, SoulPink];

            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < count; i++) {
                float angle = rotation + MathHelper.TwoPi * i / count;
                float orbitRadius = radius + MathF.Sin(pulsePhase * 2f + i * 1.5f) * 10f;
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * orbitRadius;

                Color soulColor = colors[i % colors.Length];
                float soulPulse = 0.8f + MathF.Sin(pulsePhase + i * MathHelper.Pi / count) * 0.2f;

                // 灵魂拖尾
                for (int t = 1; t <= 5; t++) {
                    float trailAngle = angle - t * 0.15f;
                    Vector2 trailPos = center + new Vector2(MathF.Cos(trailAngle), MathF.Sin(trailAngle)) * orbitRadius;

                    Color trailColor = soulColor;
                    trailColor.A = 0;
                    float trailAlpha = (1f - t / 6f) * 0.4f;

                    sb.Draw(tex, trailPos - Main.screenPosition, null, trailColor * trailAlpha,
                        0f, origin, soulPulse * (1f - t * 0.1f), SpriteEffects.None, 0);
                }

                // 灵魂核心
                DrawVoidCore(sb, pos, soulColor, Color.Lerp(soulColor, Color.White, 0.3f),
                    soulPulse * 0.8f, pulsePhase + i);
            }
        }

        /// <summary>
        /// 绘制能量波纹 - 扩散的冲击波效果
        /// </summary>
        public static void DrawEnergyWave(SpriteBatch sb, Vector2 center, float radius, float width,
            Color color, float alpha) {
            var tex = DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;
            int segments = 36;

            Color waveColor = color;
            waveColor.A = 0;

            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

                // 波纹粒子
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

        #endregion

        #region 屏幕效果

        /// <summary>
        /// 创建屏幕闪烁效果（通过粒子模拟）
        /// </summary>
        public static void CreateScreenFlash(Vector2 center, Color color, float intensity) {
            // 在屏幕边缘创建大量粒子模拟闪烁
            int particleCount = (int)(50 * intensity);
            for (int i = 0; i < particleCount; i++) {
                Vector2 pos = center + Main.rand.NextVector2Circular(800, 600);
                var d = Dust.NewDustPerfect(pos, DustID.PurpleCrystalShard);
                d.noGravity = true;
                d.scale = 2f * intensity;
                d.velocity = (center - pos).SafeNormalize(Vector2.Zero) * 5f;
                d.alpha = 200;
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 平滑步进
        /// </summary>
        public static float SmoothStep(float t) => t * t * (3f - 2f * t);

        /// <summary>
        /// 弹性缓出
        /// </summary>
        public static float ElasticOut(float t) {
            if (t == 0 || t == 1) return t;
            float p = 0.3f;
            return MathF.Pow(2, -10 * t) * MathF.Sin((t - p / 4) * (2 * MathF.PI) / p) + 1;
        }

        /// <summary>
        /// 获取基于难度的伤害
        /// </summary>
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
