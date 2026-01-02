using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.BAWImpermanences
{
    /// <summary>
    /// 黑白无常Boss辅助类
    /// </summary>
    public static class BAWHelper
    {
        public static string Path = typeof(BAWHelper).Namespace.Replace(".", "/") + "/";

        #region 纹理资源

        private static Asset<Texture2D> _chainTexture;
        private static Asset<Texture2D> _sickleTexture;
        private static Asset<Texture2D> _dustTexture;

        /// <summary>锁链体节纹理</summary>
        public static Texture2D ChainTexture => (_chainTexture ??= ModContent.Request<Texture2D>(Path + "Chain")).Value;

        /// <summary>镰刀纹理</summary>
        public static Texture2D SickleTexture => (_sickleTexture ??= ModContent.Request<Texture2D>(Path + "Sickle")).Value;

        /// <summary>粒子纹理</summary>
        public static Texture2D DustTexture => (_dustTexture ??= ModContent.Request<Texture2D>(Path + "BAWDust")).Value;

        #endregion

        #region 随机工具

        /// <summary>
        /// 获取随机浮点数
        /// </summary>
        public static float RandFloat(double a, double b = 0) {
            var max = (float)Math.Max(a, b);
            var min = (float)Math.Min(a, b);
            return Main.rand.NextFloat(min, max);
        }

        /// <summary>
        /// 获取随机整数
        /// </summary>
        public static int RandInt(double a, double b = 0, int? withOut = null) {
            var max = (int)Math.Max(a, b);
            var min = (int)Math.Min(a, b);

            var f = Main.rand.Next(min, max + 1);
            if (withOut.HasValue && f == withOut.Value)
                return RandInt(min, max, withOut);
            return f;
        }

        /// <summary>
        /// 安全归一化向量
        /// </summary>
        public static Vector2 NormalizeVector(this Vector2 v, Vector2 safe = default) {
            return v.SafeNormalize(safe);
        }

        #endregion

        #region Boss辅助

        /// <summary>
        /// 获取伙伴NPC（黑无常找白无常，白无常找黑无常）
        /// </summary>
        public static NPC FindPartner(NPC self, int partnerType) {
            foreach (var npc in Main.npc) {
                if (npc != null && npc.active && npc.type == partnerType && npc.whoAmI != self.whoAmI) {
                    return npc;
                }
            }
            return null;
        }

        /// <summary>
        /// 检查两个Boss是否都处于半血以下
        /// </summary>
        public static bool BothHalfHealth(NPC black, NPC white) {
            if (black == null || white == null || !black.active || !white.active)
                return false;
            return black.life < black.lifeMax * 0.5f && white.life < white.lifeMax * 0.5f;
        }

        /// <summary>
        /// 获取两个Boss的中点
        /// </summary>
        public static Vector2 GetMidpoint(NPC npc1, NPC npc2) {
            if (npc1 == null || npc2 == null)
                return Vector2.Zero;
            return (npc1.Center + npc2.Center) / 2f;
        }

        #endregion

        #region 高级绘制工具

        /// <summary>
        /// 绘制拼接式锁链（使用体节纹理）
        /// </summary>
        /// <param name="sb">SpriteBatch</param>
        /// <param name="start">起始位置（世界坐标）</param>
        /// <param name="end">结束位置（世界坐标）</param>
        /// <param name="color">颜色</param>
        /// <param name="scale">缩放</param>
        /// <param name="waveAmplitude">波动幅度（0为直线）</param>
        /// <param name="waveFrequency">波动频率</param>
        /// <param name="timeOffset">时间偏移（用于动画）</param>
        public static void DrawSegmentedChain(SpriteBatch sb, Vector2 start, Vector2 end, Color color,
            float scale = 1f, float waveAmplitude = 0f, float waveFrequency = 0.1f, float timeOffset = 0f) {
            var chainTex = ChainTexture;
            if (chainTex == null) return;

            Vector2 direction = end - start;
            float distance = direction.Length();
            if (distance < 1f) return;

            direction.Normalize();
            Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);
            float baseRotation = direction.ToRotation();

            float segmentHeight = chainTex.Height * scale;
            int segmentCount = (int)Math.Ceiling(distance / segmentHeight);

            for (int i = 0; i < segmentCount; i++) {
                float progress = i / (float)segmentCount;
                float actualDist = i * segmentHeight;

                // 波动效果
                float wave = waveAmplitude * MathF.Sin((progress * MathHelper.TwoPi * 3f) + timeOffset * waveFrequency);
                Vector2 waveOffset = perpendicular * wave;

                Vector2 pos = start + direction * actualDist + waveOffset;

                // 颜色渐变（两端略暗）
                float colorMod = 1f - MathF.Abs(progress - 0.5f) * 0.3f;
                Color segColor = color * colorMod;

                // 轻微旋转变化
                float rotOffset = wave * 0.02f;

                sb.Draw(chainTex, pos - Main.screenPosition, null, segColor,
                    baseRotation + MathHelper.PiOver2 + rotOffset,
                    new Vector2(chainTex.Width / 2f, chainTex.Height / 2f),
                    scale, SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绘制带发光效果的锁链
        /// </summary>
        public static void DrawGlowingChain(SpriteBatch sb, Vector2 start, Vector2 end, Color chainColor, Color glowColor,
            float scale = 1f, float glowScale = 1.5f, float waveAmplitude = 0f, float timeOffset = 0f) {
            // 先绘制发光层
            Color glow = glowColor;
            glow.A = 0;
            DrawSegmentedChain(sb, start, end, glow * 0.4f, scale * glowScale, waveAmplitude * 1.2f, 0.1f, timeOffset);
            DrawSegmentedChain(sb, start, end, glow * 0.2f, scale * glowScale * 1.3f, waveAmplitude * 1.5f, 0.1f, timeOffset);

            // 再绘制主体
            DrawSegmentedChain(sb, start, end, chainColor, scale, waveAmplitude, 0.1f, timeOffset);
        }

        /// <summary>
        /// 绘制镰刀弹幕（带拖尾）
        /// </summary>
        /// <param name="sb">SpriteBatch</param>
        /// <param name="position">位置（世界坐标）</param>
        /// <param name="rotation">旋转角度</param>
        /// <param name="color">颜色</param>
        /// <param name="scale">缩放</param>
        /// <param name="oldPositions">历史位置数组（用于拖尾）</param>
        /// <param name="oldRotations">历史旋转数组</param>
        public static void DrawSickleWithTrail(SpriteBatch sb, Vector2 position, float rotation, Color color, float scale,
            Vector2[] oldPositions, float[] oldRotations) {
            var sickleTex = SickleTexture;
            if (sickleTex == null) return;

            Vector2 origin = sickleTex.Size() / 2f;

            // 绘制拖尾
            if (oldPositions != null) {
                for (int i = oldPositions.Length - 1; i >= 0; i--) {
                    if (oldPositions[i] == Vector2.Zero) continue;

                    float progress = 1f - (float)i / oldPositions.Length;
                    float trailAlpha = progress * 0.5f;
                    float trailScale = scale * (0.6f + progress * 0.4f);

                    // 拖尾颜色（渐变为暗色）
                    Color trailColor = Color.Lerp(Color.DarkSlateGray, color, progress) * trailAlpha;
                    trailColor.A = 0;

                    float rot = oldRotations != null && i < oldRotations.Length ? oldRotations[i] : rotation;

                    sb.Draw(sickleTex, oldPositions[i] - Main.screenPosition, null, trailColor,
                        rot, origin, trailScale, SpriteEffects.None, 0);
                }
            }

            // 发光效果
            Color glowColor = color;
            glowColor.A = 0;
            sb.Draw(sickleTex, position - Main.screenPosition, null, glowColor * 0.3f,
                rotation, origin, scale * 1.2f, SpriteEffects.None, 0);
            sb.Draw(sickleTex, position - Main.screenPosition, null, glowColor * 0.15f,
                rotation, origin, scale * 1.5f, SpriteEffects.None, 0);

            // 主体
            sb.Draw(sickleTex, position - Main.screenPosition, null, color,
                rotation, origin, scale, SpriteEffects.None, 0);
        }

        /// <summary>
        /// 绘制幽灵光球效果
        /// </summary>
        public static void DrawGhostOrb(SpriteBatch sb, Vector2 position, Color coreColor, Color glowColor,
            float scale, float pulsePhase) {
            var tex = DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;

            // 脉动效果
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.15f;

            // 外层光晕（多层叠加）
            Color glow = glowColor;
            glow.A = 0;
            for (int i = 3; i >= 0; i--) {
                float layerScale = scale * pulse * (1.5f + i * 0.4f);
                float layerAlpha = 0.15f / (i + 1);
                sb.Draw(tex, position - Main.screenPosition, null, glow * layerAlpha,
                    0f, origin, layerScale, SpriteEffects.None, 0);
            }

            // 核心
            sb.Draw(tex, position - Main.screenPosition, null, coreColor,
                0f, origin, scale * pulse, SpriteEffects.None, 0);

            // 核心高光
            Color highlight = Color.White;
            highlight.A = 0;
            sb.Draw(tex, position - Main.screenPosition, null, highlight * 0.4f,
                0f, origin, scale * pulse * 0.5f, SpriteEffects.None, 0);
        }

        /// <summary>
        /// 绘制能量射线（用于灵魂吸取等效果）
        /// </summary>
        public static void DrawEnergyBeam(SpriteBatch sb, Vector2 start, Vector2 end, Color color,
            float width, float timeOffset) {
            var tex = DustTexture;
            if (tex == null) return;

            Vector2 direction = (end - start).SafeNormalize(Vector2.Zero);
            float distance = Vector2.Distance(start, end);
            float rotation = direction.ToRotation();
            Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);

            int segments = (int)(distance / 8);

            for (int i = 0; i < segments; i++) {
                float progress = i / (float)segments;
                Vector2 basePos = start + direction * (progress * distance);

                // 波动效果
                float wave = MathF.Sin(progress * MathHelper.TwoPi * 2f + timeOffset * 0.2f) * width * 0.3f;
                Vector2 pos = basePos + perpendicular * wave;

                // 脉冲亮度
                float pulse = 0.6f + MathF.Sin(progress * MathHelper.Pi * 4f + timeOffset * 0.3f) * 0.4f;

                // 宽度渐变（两端细，中间粗）
                float widthMod = MathF.Sin(progress * MathHelper.Pi);
                float currentWidth = width * (0.3f + widthMod * 0.7f);

                Color segColor = color * pulse;
                segColor.A = 0;

                sb.Draw(tex, pos - Main.screenPosition, null, segColor,
                    rotation, tex.Size() / 2f, new Vector2(currentWidth / tex.Width, 0.5f), SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绘制旋转法阵效果
        /// </summary>
        public static void DrawMagicCircle(SpriteBatch sb, Vector2 center, float radius, Color color,
            float rotation, int segments = 12) {
            var tex = DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;

            // 外圈符文
            for (int i = 0; i < segments; i++) {
                float angle = rotation + MathHelper.TwoPi * i / segments;
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

                Color runeColor = color;
                runeColor.A = 0;
                float runeScale = 0.8f + MathF.Sin(angle * 3 + rotation * 2) * 0.2f;

                sb.Draw(tex, pos - Main.screenPosition, null, runeColor * 0.7f,
                    angle + MathHelper.PiOver2, origin, runeScale, SpriteEffects.None, 0);
            }

            // 内圈连接线
            for (int i = 0; i < segments; i++) {
                float angle1 = rotation + MathHelper.TwoPi * i / segments;
                float angle2 = rotation + MathHelper.TwoPi * ((i + segments / 3) % segments) / segments;

                Vector2 pos1 = center + new Vector2(MathF.Cos(angle1), MathF.Sin(angle1)) * radius * 0.8f;
                Vector2 pos2 = center + new Vector2(MathF.Cos(angle2), MathF.Sin(angle2)) * radius * 0.8f;

                Color lineColor = color * 0.3f;
                lineColor.A = 0;

                DrawEnergyBeam(sb, pos1, pos2, lineColor, 3f, rotation * 60f);
            }
        }

        #endregion

        #region 缓动函数

        /// <summary>
        /// 线性插值
        /// </summary>
        public static float Lerp(float a, float b, float t) {
            return MathHelper.Lerp(a, b, t);
        }

        /// <summary>
        /// 平滑缓动
        /// </summary>
        public static float SmoothStep(float t) {
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// 弹性缓动
        /// </summary>
        public static float ElasticOut(float t) {
            if (t == 0 || t == 1) return t;
            float p = 0.3f;
            return MathF.Pow(2, -10 * t) * MathF.Sin((t - p / 4) * (2 * MathF.PI) / p) + 1;
        }

        /// <summary>
        /// 二次缓入
        /// </summary>
        public static float QuadIn(float t) => t * t;

        /// <summary>
        /// 二次缓出
        /// </summary>
        public static float QuadOut(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>
        /// 反弹缓动
        /// </summary>
        public static float BounceOut(float t) {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1 / d1)
                return n1 * t * t;
            else if (t < 2 / d1)
                return n1 * (t -= 1.5f / d1) * t + 0.75f;
            else if (t < 2.5 / d1)
                return n1 * (t -= 2.25f / d1) * t + 0.9375f;
            else
                return n1 * (t -= 2.625f / d1) * t + 0.984375f;
        }

        #endregion
    }

    /// <summary>
    /// 黑白无常专用粒子效果
    /// </summary>
    public class BAWDust : ModDust
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        public override void OnSpawn(Dust dust) {
            dust.noLight = true;
            dust.noGravity = true;
            dust.alpha = 240;
            dust.scale = BAWHelper.RandFloat(0.9f, 1.3f);
            dust.velocity = new Vector2(BAWHelper.RandFloat(1, 3)).RotatedByRandom(MathHelper.TwoPi);
        }

        public override bool Update(Dust dust) {
            dust.position += dust.velocity;
            dust.scale -= 0.02f;
            dust.velocity *= 0.97f;
            dust.alpha -= 5;

            if (dust.scale <= 0 || dust.velocity.Length() < 0.04f || dust.alpha < 0)
                dust.active = false;

            return false;
        }
    }

    /// <summary>
    /// 黑无常锁链粒子
    /// </summary>
    public class BlackChainDust : ModDust
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        public override void OnSpawn(Dust dust) {
            dust.noLight = true;
            dust.noGravity = true;
            dust.alpha = 255;
            dust.scale = BAWHelper.RandFloat(0.6f, 1.0f);
            dust.color = new Color(30, 30, 35);
        }

        public override bool Update(Dust dust) {
            dust.position += dust.velocity;
            dust.scale -= 0.015f;
            dust.velocity *= 0.95f;
            dust.alpha -= 4;

            Lighting.AddLight(dust.position, new Vector3(0.1f, 0.1f, 0.15f));

            if (dust.scale <= 0 || dust.alpha < 0)
                dust.active = false;

            return false;
        }
    }

    /// <summary>
    /// 白无常幽灵粒子
    /// </summary>
    public class WhiteGhostDust : ModDust
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        public override void OnSpawn(Dust dust) {
            dust.noLight = true;
            dust.noGravity = true;
            dust.alpha = 200;
            dust.scale = BAWHelper.RandFloat(0.8f, 1.2f);
            dust.color = new Color(220, 220, 255);
        }

        public override bool Update(Dust dust) {
            dust.position += dust.velocity;
            dust.scale -= 0.01f;
            dust.velocity *= 0.98f;
            dust.alpha -= 3;

            // 幽灵般的上下飘动
            dust.velocity.Y += MathF.Sin(Main.GameUpdateCount * 0.1f + dust.position.X * 0.01f) * 0.05f;

            Lighting.AddLight(dust.position, new Vector3(0.4f, 0.4f, 0.5f));

            if (dust.scale <= 0 || dust.alpha < 0)
                dust.active = false;

            return false;
        }
    }
}
