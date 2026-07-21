using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// 西海龙王敖闰 - 辅助工具类
    /// 冰霜/寒水主题配色、缓动曲线、预警声音、粒子与冰晶绘制辅助
    /// （专属着色器缓存见 <see cref="AoyuanShaders"/>）
    /// </summary>
    public static class AoyuanHelper
    {
        #region 主题配色 - 冰霜/西海色系

        /// <summary>深海蓝 - 核心冰息</summary>
        public static Color DeepSeaBlue => new Color(20, 80, 160);

        /// <summary>寒冰青 - 龙息寒气</summary>
        public static Color FrostCyan => new Color(100, 200, 230);

        /// <summary>冰晶白 - 高光色</summary>
        public static Color IceCrystalWhite => new Color(220, 240, 255);

        /// <summary>暴风紫 - 二阶段怒气</summary>
        public static Color StormViolet => new Color(100, 60, 180);

        /// <summary>纯白 - 核心高光</summary>
        public static Color PureWhite => new Color(255, 255, 255);

        /// <summary>深渊黑蓝 - 深海压迫</summary>
        public static Color AbyssBlack => new Color(10, 20, 40);

        /// <summary>西海碧 - 龙王尊贵色</summary>
        public static Color WestSeaTeal => new Color(40, 140, 170);

        #endregion

        #region 缓动与声音

        public static float QuadOut(float t) {
            t = Math.Clamp(t, 0f, 1f);
            return 1f - (1f - t) * (1f - t);
        }

        public static float SineInOut(float t) {
            t = Math.Clamp(t, 0f, 1f);
            return 0.5f - 0.5f * MathF.Cos(MathF.PI * t);
        }

        /// <summary>高次幂 ease-out: 前几帧走完几乎全部行程 — "斩击"曲线（幂次 8+ 读作打击）</summary>
        public static float PolyOut(float t, int power = 8) {
            t = Math.Clamp(t, 0f, 1f);
            return 1f - MathF.Pow(1f - t, power);
        }

        /// <summary>late-snap 前摇: 大半时间纹丝不动, 最后几帧猛然吸入（pow(t,8)）</summary>
        public static float LateSnap(float t, int power = 8) {
            t = Math.Clamp(t, 0f, 1f);
            return MathF.Pow(t, power);
        }

        /// <summary>0→1→0 山峰脉冲, peak 处为 1</summary>
        public static float Bump(float t, float peak = 0.5f) {
            t = Math.Clamp(t, 0f, 1f);
            return t < peak
                ? SineInOut(t / peak)
                : 1f - SineInOut((t - peak) / (1f - peak));
        }

        /// <summary>角度插值（处理环绕）</summary>
        public static float LerpAngle(float from, float to, float amount) {
            float delta = MathHelper.WrapAngle(to - from);
            return from + delta * amount;
        }

        /// <summary>冰铃/剑鸣 — 固定预警音（telegraph 常数音色, 玩家可内化）</summary>
        public static void PlayChime(Vector2 pos, float pitch = 0f, float volume = 1f) {
            Terraria.Audio.SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = pitch, Volume = volume }, pos);
        }

        /// <summary>镜面/冰晶碎裂</summary>
        public static void PlayShatter(Vector2 pos, float pitch = 0f, float volume = 1f) {
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Shatter with { Pitch = pitch, Volume = volume }, pos);
        }

        /// <summary>向指定点汇聚的各向异性流光尘 — 蓄力语法（比例吸入, 尖端向内）</summary>
        public static void CreateConvergingStreak(Vector2 focus, float minDist, float maxDist, float pull = 0.085f) {
            float ang = Main.rand.NextFloat(MathHelper.TwoPi);
            float dist = minDist + Main.rand.NextFloat(maxDist - minDist);
            Vector2 pos = focus + ang.ToRotationVector2() * dist;
            var d = Dust.NewDustPerfect(pos, DustID.FrostStaff);
            d.noGravity = true;
            d.scale = 1.2f + dist / maxDist * 1.4f;
            d.velocity = (focus - pos) * pull;
            d.alpha = 60;
        }

        /// <summary>镜面碎裂冰片喷泉（纯视觉）</summary>
        public static void CreateMirrorShards(Vector2 center, float power = 1f, int count = 26) {
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(7f, 7f) * power;
                int dustType = Main.rand.NextBool(3) ? DustID.FrostStaff : DustID.IceTorch;
                var d = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(24, 40), dustType);
                d.noGravity = Main.rand.NextBool();
                d.scale = 1.4f + Main.rand.NextFloat(1.2f);
                d.velocity = vel + new Vector2(0, -2f * power);
                d.alpha = 40;
            }
        }

        #endregion

        #region 粒子效果

        /// <summary>
        /// 创建冰霜旋涡粒子 - 阶段转换/攻击使用
        /// </summary>
        public static void CreateFrostVortex(Vector2 center, float radius, float intensity, int particleCount = 40) {
            for (int i = 0; i < particleCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = radius * (0.2f + Main.rand.NextFloat(0.8f));
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                Vector2 toCenter = (center - pos).SafeNormalize(Vector2.Zero);
                float speed = intensity * (1f - dist / radius) * 10f;

                int dustType = Main.rand.NextBool(3) ? DustID.IceTorch : DustID.FrostStaff;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 1.8f + Main.rand.NextFloat(1.2f);
                d.velocity = toCenter * speed + new Vector2(-toCenter.Y, toCenter.X) * speed * 0.6f;
                d.alpha = 80;
            }
        }

        /// <summary>
        /// 创建冰晶爆发 - 冲刺/击中时使用
        /// </summary>
        public static void CreateIceBurst(Vector2 center, float radius, int rings = 3, int particlesPerRing = 16) {
            for (int ring = 0; ring < rings; ring++) {
                float ringRadius = radius * (ring + 1) / rings;

                for (int i = 0; i < particlesPerRing; i++) {
                    float angle = MathHelper.TwoPi * i / particlesPerRing + ring * 0.3f;
                    Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 pos = center + direction * ringRadius * 0.3f;

                    int dustType = ring % 2 == 0 ? DustID.IceTorch : DustID.FrostStaff;
                    var d = Dust.NewDustPerfect(pos, dustType);
                    d.noGravity = true;
                    d.scale = 2.5f - ring * 0.4f;
                    d.velocity = direction * (8f + ring * 3f);
                    d.alpha = 60;
                }
            }
        }

        /// <summary>
        /// 创建冰霜尾迹粒子
        /// </summary>
        public static void CreateFrostTrail(Vector2 position, Vector2 velocity, float scale = 1f) {
            for (int i = 0; i < 3; i++) {
                Vector2 dustPos = position + Main.rand.NextVector2Circular(20, 20);
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
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
        /// 绘制冰霜光环
        /// </summary>
        public static void DrawFrostAura(SpriteBatch sb, Vector2 center, float radius, float rotation, float alpha) {
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
                    Color color = Color.Lerp(FrostCyan, DeepSeaBlue, ring / (float)ringCount);
                    color *= particleAlpha;
                    color.A = 0;

                    float particleScale = (0.5f - ring * 0.1f) * (1f + MathF.Sin(angle * 3f + rotation * 5f) * 0.2f);
                    sb.Draw(tex, pos, null, color, 0f, origin, particleScale, SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>
        /// 冰晶菱形绘制 — 小型飞行冰晶的 CPU 视觉（GlaciateWave 十字交叠 + Sparkle 高光），
        /// 供高数量弹幕使用（不打断批次）。rotation 为菱形长轴朝向。
        /// </summary>
        public static void DrawCrystalShard(SpriteBatch sb, Vector2 worldPos, float rotation, float scale, Color tint, float glint = 0.6f) {
            Texture2D wave = ACMAsset.GlaciateWave;
            if (wave == null) return;
            Vector2 origin = wave.Size() / 2f;
            Vector2 drawPos = worldPos - Main.screenPosition;

            Color body = tint; body.A = 0;
            // 长轴主晶
            sb.Draw(wave, drawPos, null, body, rotation, origin, new Vector2(scale * 0.34f, scale * 0.10f), SpriteEffects.None, 0f);
            // 短轴交叠（十字晶）
            sb.Draw(wave, drawPos, null, body * 0.7f, rotation + MathHelper.PiOver2, origin, new Vector2(scale * 0.16f, scale * 0.07f), SpriteEffects.None, 0f);

            if (glint > 0.01f && ACMAsset.Sparkle != null) {
                Color gc = IceCrystalWhite * glint; gc.A = 0;
                sb.Draw(ACMAsset.Sparkle, drawPos, null, gc, rotation * 0.5f, ACMAsset.Sparkle.Size() / 2f, scale * 0.16f, SpriteEffects.None, 0f);
            }
        }

        #endregion
    }
}
