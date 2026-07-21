using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors
{
    /// <summary>
    /// 阴天子专用绘制工具类
    /// 提供帝王级幽冥视觉特效支持
    /// </summary>
    public static class YinEmperorHelper
    {
        public static string Path => typeof(YinEmperorHelper).Namespace.Replace(".", "/") + "/";

        #region 纹理资源

        private static Asset<Texture2D> _ringTexture;

        /// <summary>帝冥法环纹理</summary>
        public static Texture2D RingTexture => (_ringTexture ??= ModContent.Request<Texture2D>(Path + "YinEmperorRing")).Value;

        #endregion

        #region 专属着色器（V3 · 静态缓存，参考 Xuanwu 写法，不注册 ACMShaders）

        private static Asset<Effect> _bannerFx;
        private static Asset<Effect> _gateFx;
        private static Asset<Effect> _courtFx;

        /// <summary>冥幡布幔着色器（入场仪仗 / 常驻围场 / 死亡逐杆熄灭）。</summary>
        public static Effect BannerEffect {
            get {
                if (Main.dedServ) return null;
                _bannerFx ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/YinEmperorBanner", AssetRequestMode.ImmediateLoad);
                return _bannerFx?.Value;
            }
        }

        /// <summary>鬼门着色器（门洞深渊 + 开阖动画）。</summary>
        public static Effect GateEffect {
            get {
                if (Main.dedServ) return null;
                _gateFx ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/YinEmperorGate", AssetRequestMode.ImmediateLoad);
                return _gateFx?.Value;
            }
        }

        /// <summary>酆都法庭结界着色器（屏幕空间 SDF，参数约定同 ArenaRunic）。</summary>
        public static Effect CourtEffect {
            get {
                if (Main.dedServ) return null;
                _courtFx ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/YinEmperorCourt", AssetRequestMode.ImmediateLoad);
                return _courtFx?.Value;
            }
        }

        /// <summary>单杆冥幡的绘制描述（世界坐标顶端挂点 + 布幔尺寸 + 演出标量）。</summary>
        public struct BannerDraw
        {
            public Vector2 Top;
            public float Width;
            public float Height;
            public float Wave;
            public float Burn;
            public float Intensity;
            public float Seed;
        }

        /// <summary>
        /// 批量绘制冥幡（一次开合批画全部，PreDraw 等已有活动批的阶段调用）。
        /// 幡杆用像素线绘制在当前批内完成后再切着色器批画布幔。
        /// </summary>
        public static void DrawBannerSet(SpriteBatch sb, BannerDraw[] banners, int count) {
            if (Main.dedServ || banners == null || count <= 0)
                return;

            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;

            // 幡杆 + 杆顶横梁（当前批，普通像素绘制）
            for (int i = 0; i < count; i++) {
                ref BannerDraw b = ref banners[i];
                if (b.Intensity <= 0.01f) continue;
                float lit = 1f - b.Burn;
                Vector2 topScreen = b.Top - Main.screenPosition;
                Color pole = Color.Lerp(ShadowBlack, ImperialGold, 0.35f * lit) * (0.85f * b.Intensity);
                // 杆体：从布幔顶再向下延伸至底部基座
                sb.Draw(pixel, topScreen + new Vector2(-2f, -14f), new Rectangle(0, 0, 1, 1), pole, 0f,
                    Vector2.Zero, new Vector2(4f, b.Height + 60f), SpriteEffects.None, 0f);
                // 横梁
                sb.Draw(pixel, topScreen + new Vector2(-b.Width * 0.5f - 8f, -12f), new Rectangle(0, 0, 1, 1), pole, 0f,
                    Vector2.Zero, new Vector2(b.Width + 16f, 5f), SpriteEffects.None, 0f);
                // 杆顶金饰
                Color finial = ImperialGold * (0.8f * lit * b.Intensity);
                finial.A = 0;
                sb.Draw(pixel, topScreen + new Vector2(-4f, -22f), new Rectangle(0, 0, 1, 1), finial, 0f,
                    Vector2.Zero, new Vector2(8f, 10f), SpriteEffects.None, 0f);
            }

            Effect fx = BannerEffect;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);

            float zoom = Main.GameViewMatrix.Zoom.X;
            Vector2 halfScreen = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);

            for (int i = 0; i < count; i++) {
                ref BannerDraw b = ref banners[i];
                if (b.Intensity <= 0.01f || b.Burn >= 0.999f) continue;

                fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                fx.Parameters["uIntensity"]?.SetValue(b.Intensity);
                fx.Parameters["uWave"]?.SetValue(b.Wave);
                fx.Parameters["uBurn"]?.SetValue(b.Burn);
                fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(new Vector3(0.10f, 0.06f, 0.16f), 1f));
                fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(ImperialGold.ToVector3(), 1f));
                fx.Parameters["uSeed"]?.SetValue(b.Seed);
                fx.CurrentTechnique.Passes[0].Apply();

                // 世界 → 屏幕（含缩放），布幔以顶端挂点为锚
                Vector2 screenTop = (b.Top - Main.screenPosition - halfScreen) * zoom + halfScreen;
                Vector2 size = new Vector2(b.Width, b.Height) * zoom;
                sb.Draw(noise, new Rectangle((int)(screenTop.X - size.X * 0.5f), (int)screenTop.Y,
                    (int)size.X, (int)size.Y), Color.White);
            }

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 绘制一扇鬼门（弹幕 PreDraw 内调用，自动开合批）。size = 门体全宽高（世界像素）。
        /// </summary>
        public static void DrawGate(SpriteBatch sb, Vector2 worldCenter, Vector2 size,
            float open, float intensity, float seed) {
            if (Main.dedServ || intensity <= 0.01f)
                return;
            Effect fx = GateEffect;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uOpen"]?.SetValue(MathHelper.Clamp(open, 0f, 1f));
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(new Vector3(0.16f, 0.05f, 0.28f), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(DragonVeinGold.ToVector3(), 1f));
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.CurrentTechnique.Passes[0].Apply();

            float zoom = Main.GameViewMatrix.Zoom.X;
            Vector2 halfScreen = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
            Vector2 screenC = (worldCenter - Main.screenPosition - halfScreen) * zoom + halfScreen;
            Vector2 half = size * zoom * 0.5f;
            sb.Draw(noise, new Rectangle((int)(screenC.X - half.X), (int)(screenC.Y - half.Y),
                (int)(half.X * 2f), (int)(half.Y * 2f)), Color.White);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        #endregion

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

        /// <summary>
        /// 绘制帝冥法环 - Boss背后旋转的巨大圆环
        /// 使用YinEmperorRing.png纹理，多层叠加营造威压感
        /// </summary>
        public static void DrawImperialRing(SpriteBatch sb, Vector2 center, float scale,
            float rotation, float pulsePhase, float alpha) {
            var tex = RingTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;

            // === 第1层：最外层暗紫晕染 ===
            Color outerGlow = AbyssPurple;
            outerGlow.A = 0;
            float outerPulse = 1f + MathF.Sin(pulsePhase * 0.8f) * 0.05f;
            sb.Draw(tex, center - Main.screenPosition, null, outerGlow * alpha * 0.15f,
                rotation * 0.3f, origin, scale * 1.15f * outerPulse, SpriteEffects.None, 0);

            // === 第2层：金色主环 ===
            Color goldRing = ImperialGold;
            goldRing.A = 0;
            float mainPulse = 1f + MathF.Sin(pulsePhase) * 0.03f;
            sb.Draw(tex, center - Main.screenPosition, null, goldRing * alpha * 0.6f,
                rotation, origin, scale * mainPulse, SpriteEffects.None, 0);

            // === 第3层：反向旋转的幽暗层 ===
            Color darkRing = ShadowBlack;
            darkRing.A = 0;
            sb.Draw(tex, center - Main.screenPosition, null, darkRing * alpha * 0.25f,
                -rotation * 0.6f, origin, scale * 0.95f, SpriteEffects.None, 0);

            // === 第4层：内层高亮边缘 ===
            Color highlight = DragonVeinGold;
            highlight.A = 0;
            float innerPulse = 1f + MathF.Sin(pulsePhase * 1.5f) * 0.06f;
            sb.Draw(tex, center - Main.screenPosition, null, highlight * alpha * 0.3f,
                rotation * 1.2f, origin, scale * 0.88f * innerPulse, SpriteEffects.None, 0);

            // === 第5层：白色呼吸高光 ===
            Color white = Color.White;
            white.A = 0;
            float breathAlpha = (MathF.Sin(pulsePhase * 0.6f) * 0.5f + 0.5f) * 0.08f;
            sb.Draw(tex, center - Main.screenPosition, null, white * alpha * breathAlpha,
                rotation, origin, scale * mainPulse, SpriteEffects.None, 0);
        }

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
