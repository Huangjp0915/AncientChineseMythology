using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Spectres
{
    /// <summary>
    /// 怨灵Boss辅助工具类
    /// 青色/黄色主题的视觉效果
    /// </summary>
    public static class SpectreHelper
    {
        public static string Path => typeof(SpectreHelper).Namespace.Replace(".", "/") + "/";

        #region 纹理资源

        private static Asset<Texture2D> _coreTexture;
        private static Asset<Texture2D> _soulTexture;

        /// <summary>核心纹理</summary>
        public static Texture2D CoreTexture => (_coreTexture ??= ModContent.Request<Texture2D>(Path + "SpectreCore")).Value;

        /// <summary>灵魂纹理</summary>
        public static Texture2D SoulTexture => (_soulTexture ??= ModContent.Request<Texture2D>(Path + "SpectreSoul")).Value;

        // 复用基础纹理
        private static Texture2D DustTexture => BAWImpermanences.BAWHelper.DustTexture;

        #endregion

        #region 怨灵主题颜色

        /// <summary>怨灵青色 - 主色调</summary>
        public static Color SpectreCyan => new Color(80, 220, 200);

        /// <summary>怨灵深青 - 深色</summary>
        public static Color SpectreDeepCyan => new Color(30, 120, 110);

        /// <summary>怨灵黄色 - 辅助色</summary>
        public static Color SpectreYellow => new Color(255, 220, 100);

        /// <summary>怨灵金色 - 高光色</summary>
        public static Color SpectreGold => new Color(255, 200, 50);

        /// <summary>怨灵暗绿 - 阴影色</summary>
        public static Color SpectreDarkGreen => new Color(40, 80, 60);

        /// <summary>愤怒红色 - 狂暴时使用</summary>
        public static Color SpectreRage => new Color(255, 100, 80);

        /// <summary>魂火橙 - 贴图鬼火芯的暖色 (内焰/灯芯)</summary>
        public static Color SpectreEmber => new Color(255, 150, 60);

        /// <summary>鬼焰绿 - 本体贴图的青绿鬼火 (魂缕/凝形)</summary>
        public static Color SpectreGhostFlame => new Color(120, 235, 140);

        #endregion

        #region 鬼相着色器 SpectreVeil (专属, 静态缓存)

        private static Asset<Effect> _veilRef;

        /// <summary>怨灵鬼相着色器 — 本体/残影/分身统一 sprite pass。惰性 ImmediateLoad 一次。</summary>
        public static Effect VeilEffect {
            get {
                if (Main.dedServ)
                    return null;
                _veilRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/SpectreVeil", AssetRequestMode.ImmediateLoad);
                return _veilRef?.Value;
            }
        }

        /// <summary>
        /// 开启鬼相绘制批 (Immediate + AlphaBlend, 噪声绑 s1)。返回 false 表示着色器不可用,
        /// 调用方应走普通 sb.Draw 退化。成功后逐 sprite 调 <see cref="ApplyVeilParams"/> 再 Draw,
        /// 全部画完必须调 <see cref="EndVeilBatch"/> 恢复默认批。
        /// </summary>
        public static bool BeginVeilBatch(SpriteBatch sb) {
            Effect fx = VeilEffect;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null || sb == null)
                return false;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            return true;
        }

        /// <summary>逐 sprite 设置鬼相参数并 Apply (Immediate 批内, 每次 Draw 前调用一次)。</summary>
        /// <param name="veil">虚相度 0=实体 1=鬼相。</param>
        /// <param name="dissolve">聚散进度 0=成形 1=全散。</param>
        /// <param name="opacity">主不透明度 (残影逐级递减)。</param>
        /// <param name="flame">内焰强度 (蓄力时抬, 0~1.5)。</param>
        /// <param name="dashDir">UV 空间冲刺方向 (拖影)。</param>
        /// <param name="dashBlur">拖影强度 0~1。</param>
        /// <param name="tint">主题染色 (rgb)。</param>
        /// <param name="tintAmount">染色强度 0~1。</param>
        /// <param name="edge">灼边/轮辉色。</param>
        /// <param name="edgeAmount">灼边强度 0~1。</param>
        public static void ApplyVeilParams(float veil, float dissolve, float opacity, float flame,
            Vector2 dashDir, float dashBlur, Color tint, float tintAmount, Color edge, float edgeAmount,
            float wisp = 0.6f, float noiseScale = 2.2f) {
            Effect fx = VeilEffect;
            if (fx == null)
                return;
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uVeil"]?.SetValue(MathHelper.Clamp(veil, 0f, 1f));
            fx.Parameters["uDissolve"]?.SetValue(MathHelper.Clamp(dissolve, 0f, 1f));
            fx.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(opacity, 0f, 1f));
            fx.Parameters["uWisp"]?.SetValue(wisp);
            fx.Parameters["uNoiseScale"]?.SetValue(noiseScale);
            fx.Parameters["uFlame"]?.SetValue(flame);
            fx.Parameters["uDashDir"]?.SetValue(dashDir);
            fx.Parameters["uDashBlur"]?.SetValue(MathHelper.Clamp(dashBlur, 0f, 1f));
            fx.Parameters["uTint"]?.SetValue(new Vector4(tint.ToVector3(), MathHelper.Clamp(tintAmount, 0f, 1f)));
            fx.Parameters["uEdgeColor"]?.SetValue(new Vector4(edge.ToVector3(), MathHelper.Clamp(edgeAmount, 0f, 2f)));
            fx.CurrentTechnique.Passes[0].Apply();
        }

        /// <summary>结束鬼相批并恢复项目默认批。</summary>
        public static void EndVeilBatch(SpriteBatch sb) {
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        #endregion

        #region 粒子效果

        /// <summary>
        /// 创建怨灵漩涡粒子
        /// </summary>
        public static void CreateSpectreVortex(Vector2 center, float radius, float intensity, int particleCount = 25) {
            for (int i = 0; i < particleCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = radius * (0.3f + Main.rand.NextFloat(0.7f));
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                Vector2 toCenter = (center - pos).SafeNormalize(Vector2.Zero);
                float speed = intensity * (1f - dist / radius) * 6f;

                // 青色和黄色混合粒子
                int dustType = Main.rand.NextBool(3) ? DustID.YellowTorch : DustID.IceTorch;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 1.3f + Main.rand.NextFloat(0.8f);
                d.velocity = toCenter * speed + new Vector2(-toCenter.Y, toCenter.X) * speed * 0.4f;
                d.alpha = 80;
            }
        }

        /// <summary>
        /// 创建怨灵爆发粒子
        /// </summary>
        public static void CreateSpectreBurst(Vector2 center, float radius, int rings = 3, int particlesPerRing = 14) {
            for (int ring = 0; ring < rings; ring++) {
                float ringRadius = radius * (ring + 1) / rings;

                for (int i = 0; i < particlesPerRing; i++) {
                    float angle = MathHelper.TwoPi * i / particlesPerRing + ring * 0.2f;
                    Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 pos = center + direction * ringRadius * 0.3f;

                    // 交替使用青色和黄色
                    int dustType = (ring + i) % 2 == 0 ? DustID.IceTorch : DustID.YellowTorch;
                    var d = Dust.NewDustPerfect(pos, dustType);
                    d.noGravity = true;
                    d.scale = 1.8f - ring * 0.3f;
                    d.velocity = direction * (ringRadius / 12f);
                    d.alpha = 60;
                }
            }
        }

        /// <summary>
        /// 创建怨气拖尾
        /// </summary>
        public static void CreateSpectreTrail(Vector2 position, Vector2 velocity, float scale = 1f) {
            for (int i = 0; i < 2; i++) {
                Vector2 offset = Main.rand.NextVector2Circular(8 * scale, 8 * scale);
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch;
                var d = Dust.NewDustPerfect(position + offset, dustType);
                d.noGravity = true;
                d.scale = (1.2f - i * 0.2f) * scale;
                d.velocity = -velocity * 0.15f + Main.rand.NextVector2Circular(1.5f, 1.5f);
                d.alpha = 100;
            }
        }

        /// <summary>
        /// 创建灵魂链接粒子
        /// </summary>
        public static void CreateSoulChainParticles(Vector2 start, Vector2 end, float intensity) {
            Vector2 direction = (end - start).SafeNormalize(Vector2.Zero);
            float distance = Vector2.Distance(start, end);
            int segments = (int)(distance / 20);

            for (int i = 0; i < segments; i++) {
                float progress = i / (float)segments;
                Vector2 pos = Vector2.Lerp(start, end, progress);

                // 添加波动
                Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);
                float wave = MathF.Sin(progress * MathHelper.TwoPi * 3 + Main.GlobalTimeWrappedHourly * 5) * 10f * intensity;
                pos += perpendicular * wave;

                if (Main.rand.NextBool(3)) {
                    int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch;
                    var d = Dust.NewDustPerfect(pos, dustType);
                    d.noGravity = true;
                    d.scale = 0.8f * intensity;
                    d.velocity = Main.rand.NextVector2Circular(1, 1);
                    d.alpha = 120;
                }
            }
        }

        /// <summary>
        /// 创建怨念波动
        /// </summary>
        public static void CreateGrudgeWave(Vector2 center, float angle, float length, float width) {
            Vector2 direction = angle.ToRotationVector2();
            Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);

            int segments = (int)(length / 15);
            for (int i = 0; i < segments; i++) {
                float progress = i / (float)segments;
                Vector2 basePos = center + direction * (progress * length);

                for (int side = -1; side <= 1; side += 2) {
                    float waveWidth = width * MathF.Sin(progress * MathHelper.Pi);
                    Vector2 pos = basePos + perpendicular * (side * waveWidth * 0.5f);

                    int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch;
                    var d = Dust.NewDustPerfect(pos, dustType);
                    d.noGravity = true;
                    d.scale = 1.2f * (1f - progress * 0.5f);
                    d.velocity = direction * 2f;
                    d.alpha = 80;
                }
            }
        }

        #endregion

        #region 绘制方法

        /// <summary>
        /// 绘制怨灵核心
        /// </summary>
        public static void DrawSpectreCore(SpriteBatch sb, Vector2 position, Color coreColor, Color glowColor,
            float scale, float pulsePhase, bool isEnraged = false) {
            var tex = DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.15f + MathF.Sin(pulsePhase * 1.7f) * 0.08f;

            if (isEnraged) {
                pulse += MathF.Sin(pulsePhase * 4f) * 0.1f;
                position += Main.rand.NextVector2Circular(2, 2);
            }

            // 外层光晕
            Color glow = glowColor;
            glow.A = 0;
            for (int i = 4; i >= 0; i--) {
                float layerScale = scale * pulse * (1.8f + i * 0.4f);
                float layerAlpha = 0.12f / (i + 1);

                Vector2 layerOffset = new Vector2(
                    MathF.Sin(pulsePhase + i * 0.4f) * 2f,
                    MathF.Cos(pulsePhase * 1.2f + i * 0.4f) * 2f
                );

                sb.Draw(tex, position + layerOffset - Main.screenPosition, null, glow * layerAlpha,
                    pulsePhase * 0.08f * i, origin, layerScale, SpriteEffects.None, 0);
            }

            // 能量漩涡层
            for (int i = 0; i < 2; i++) {
                float swirl = pulsePhase * (0.4f + i * 0.25f);
                float swirlScale = scale * pulse * (1.2f - i * 0.1f);
                Color swirlColor = Color.Lerp(glowColor, coreColor, i / 2f);
                swirlColor.A = 0;

                sb.Draw(tex, position - Main.screenPosition, null, swirlColor * (0.25f - i * 0.06f),
                    swirl, origin, swirlScale, SpriteEffects.None, 0);
            }

            // 核心
            sb.Draw(tex, position - Main.screenPosition, null, coreColor,
                0f, origin, scale * pulse, SpriteEffects.None, 0);

            // 中心高光
            Color highlight = Color.White;
            highlight.A = 0;
            sb.Draw(tex, position - Main.screenPosition, null, highlight * 0.5f,
                0f, origin, scale * pulse * 0.35f, SpriteEffects.None, 0);

            // 狂暴时的红色能量环
            if (isEnraged) {
                for (int i = 0; i < 3; i++) {
                    float ringAngle = pulsePhase * 2.5f + i * MathHelper.TwoPi / 3f;
                    float ringDist = scale * 25f * pulse;
                    Vector2 ringPos = position + new Vector2(MathF.Cos(ringAngle), MathF.Sin(ringAngle)) * ringDist;

                    Color ringColor = SpectreRage;
                    ringColor.A = 0;
                    sb.Draw(tex, ringPos - Main.screenPosition, null, ringColor * 0.4f,
                        ringAngle, origin, scale * 0.4f, SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>
        /// 绘制灵魂链条
        /// </summary>
        public static void DrawSoulChain(SpriteBatch sb, Vector2 start, Vector2 end, Color color,
            float width, float timeOffset) {
            var tex = DustTexture;
            if (tex == null) return;

            Vector2 direction = (end - start).SafeNormalize(Vector2.Zero);
            float distance = Vector2.Distance(start, end);
            float rotation = direction.ToRotation();
            Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);

            int segments = (int)(distance / 8);
            Color glowColor = color;
            glowColor.A = 0;

            // 外层光晕
            for (int layer = 0; layer < 2; layer++) {
                float layerWidth = width * (1.4f + layer * 0.6f);

                for (int i = 0; i < segments; i++) {
                    float progress = i / (float)segments;
                    Vector2 basePos = start + direction * (progress * distance);

                    // 波动效果
                    float wave = MathF.Sin(progress * MathHelper.TwoPi * 4f + timeOffset * 0.12f) * layerWidth * 0.3f;
                    Vector2 pos = basePos + perpendicular * wave;

                    float pulse = 0.6f + MathF.Sin(progress * MathHelper.Pi * 3f + timeOffset * 0.15f) * 0.4f;
                    float widthMod = MathF.Sin(progress * MathHelper.Pi) * (0.5f + pulse * 0.5f);

                    Color segColor = glowColor * (0.15f / (layer + 1)) * pulse;

                    sb.Draw(tex, pos - Main.screenPosition, null, segColor,
                        rotation, tex.Size() / 2f, new Vector2(layerWidth * widthMod / tex.Width, 0.6f), SpriteEffects.None, 0);
                }
            }

            // 核心链条
            for (int i = 0; i < segments; i++) {
                float progress = i / (float)segments;
                Vector2 basePos = start + direction * (progress * distance);

                float wave = MathF.Sin(progress * MathHelper.TwoPi * 4f + timeOffset * 0.12f) * width * 0.2f;
                Vector2 pos = basePos + perpendicular * wave;

                float pulse = 0.7f + MathF.Sin(progress * MathHelper.Pi * 2f + timeOffset * 0.2f) * 0.3f;
                float widthMod = MathF.Sin(progress * MathHelper.Pi);

                Color coreColor = Color.Lerp(color, Color.White, 0.2f) * pulse;
                coreColor.A = 0;

                sb.Draw(tex, pos - Main.screenPosition, null, coreColor,
                    rotation, tex.Size() / 2f, new Vector2(width * widthMod / tex.Width, 0.5f), SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绘制怨念光环
        /// </summary>
        public static void DrawGrudgeAura(SpriteBatch sb, Vector2 center, float radius, int segments,
            float rotation, float pulsePhase) {
            var tex = DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;

            // 外圈
            for (int i = 0; i < segments; i++) {
                float angle = rotation + MathHelper.TwoPi * i / segments;
                float pulse = MathF.Sin(pulsePhase + angle * 2) * 0.3f + 0.7f;
                float dist = radius + MathF.Sin(pulsePhase * 1.5f + i * 0.5f) * 8f;
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                // 交替青色和黄色
                Color auraColor = i % 2 == 0 ? SpectreCyan : SpectreYellow;
                auraColor.A = 0;
                float auraScale = 0.7f + pulse * 0.2f;

                sb.Draw(tex, pos - Main.screenPosition, null, auraColor * 0.5f * pulse,
                    angle + MathHelper.PiOver2, origin, auraScale, SpriteEffects.None, 0);
            }

            // 内圈连线效果
            for (int i = 0; i < segments / 2; i++) {
                float angle1 = rotation + MathHelper.TwoPi * i * 2 / segments;
                float angle2 = rotation + MathHelper.TwoPi * ((i * 2 + segments / 3) % segments) / segments;

                Vector2 pos1 = center + new Vector2(MathF.Cos(angle1), MathF.Sin(angle1)) * radius * 0.7f;
                Vector2 pos2 = center + new Vector2(MathF.Cos(angle2), MathF.Sin(angle2)) * radius * 0.7f;

                Color lineColor = Color.Lerp(SpectreCyan, SpectreYellow, MathF.Sin(pulsePhase + i) * 0.5f + 0.5f) * 0.25f;
                DrawSoulChain(sb, pos1, pos2, lineColor, 3f, pulsePhase * 60f);
            }
        }

        /// <summary>
        /// 绘制灵魂环绕
        /// </summary>
        public static void DrawSoulOrbit(SpriteBatch sb, Vector2 center, float radius, int count,
            float rotation, float pulsePhase) {
            var tex = DustTexture;
            if (tex == null) return;

            Color[] colors = [SpectreCyan, SpectreYellow, SpectreGold, SpectreDeepCyan];
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < count; i++) {
                float angle = rotation + MathHelper.TwoPi * i / count;
                float orbitRadius = radius + MathF.Sin(pulsePhase * 1.5f + i * 1.2f) * 8f;
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * orbitRadius;

                Color soulColor = colors[i % colors.Length];
                float soulPulse = 0.8f + MathF.Sin(pulsePhase + i * MathHelper.Pi / count) * 0.2f;

                // 灵魂拖尾
                for (int t = 1; t <= 4; t++) {
                    float trailAngle = angle - t * 0.12f;
                    Vector2 trailPos = center + new Vector2(MathF.Cos(trailAngle), MathF.Sin(trailAngle)) * orbitRadius;

                    Color trailColor = soulColor;
                    trailColor.A = 0;
                    float trailAlpha = (1f - t / 5f) * 0.35f;

                    sb.Draw(tex, trailPos - Main.screenPosition, null, trailColor * trailAlpha,
                        0f, origin, soulPulse * (1f - t * 0.08f), SpriteEffects.None, 0);
                }

                // 灵魂核心
                DrawSpectreCore(sb, pos, soulColor, Color.Lerp(soulColor, Color.White, 0.3f),
                    soulPulse * 0.7f, pulsePhase + i);
            }
        }

        /// <summary>
        /// 绘制能量波纹
        /// </summary>
        public static void DrawEnergyWave(SpriteBatch sb, Vector2 center, float radius, float width,
            Color color, float alpha) {
            var tex = DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;
            int segments = 32;

            Color waveColor = color;
            waveColor.A = 0;

            for (int i = 0; i < segments; i++) {
                float angle = MathHelper.TwoPi * i / segments;
                Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

                sb.Draw(tex, pos - Main.screenPosition, null, waveColor * alpha,
                    angle, origin, width / 18f, SpriteEffects.None, 0);
            }
        }

        #endregion

        #region 屏幕效果

        /// <summary>
        /// 创建屏幕闪烁
        /// </summary>
        public static void CreateScreenFlash(Vector2 center, Color color, float intensity) {
            int particleCount = (int)(40 * intensity);
            for (int i = 0; i < particleCount; i++) {
                Vector2 pos = center + Main.rand.NextVector2Circular(700, 500);
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 1.8f * intensity;
                d.velocity = (center - pos).SafeNormalize(Vector2.Zero) * 4f;
                d.alpha = 180;
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 平滑步进
        /// </summary>
        public static float SmoothStep(float t) => t * t * (3f - 2f * t);

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
