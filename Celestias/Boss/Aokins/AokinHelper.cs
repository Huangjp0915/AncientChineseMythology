using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    /// <summary>
    /// 南海龙王敖钦 - 辅助工具类
    /// 火属性龙王主题：颜色规范、粒子配方、专属着色器缓存与条带绘制原语
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

        /// <summary>蒸汽白 - 沸海蒸腾（无伤演出色, 与致命红区分）</summary>
        public static Color SteamWhite => new Color(255, 244, 224);

        #endregion

        #region 专属着色器缓存（Aokin 前缀, 不注册 ACMShaders; 参考玄武写法）

        private static Asset<Effect> breathConeRef;
        private static Asset<Effect> shockRingRef;
        private static Asset<Effect> moltenScaleRef;
        private static Asset<Effect> firePillarRef;
        private static Asset<Effect> fireTornadoRef;
        private static Asset<Effect> heatHazeRef;

        /// <summary>赤炎龙息锥形火舌（TriangleStrip 条带, Additive）。</summary>
        public static Effect BreathConeEffect {
            get {
                if (Main.dedServ) return null;
                breathConeRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/AokinBreathCone", AssetRequestMode.ImmediateLoad);
                return breathConeRef?.Value;
            }
        }

        /// <summary>冲击火环（屏幕空间 SDF 环带; uMode 0=炼狱火环带缺口 1=无伤蒸汽波）。</summary>
        public static Effect ShockRingEffect {
            get {
                if (Main.dedServ) return null;
                shockRingRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/AokinShockRing", AssetRequestMode.ImmediateLoad);
                return shockRingRef?.Value;
            }
        }

        /// <summary>龙身熔鳞（贴图 pass; uHeat 温度熔纹 / uRage 狂暴泛白 / uDeath 逐段熄灭）。</summary>
        public static Effect MoltenScaleEffect {
            get {
                if (Main.dedServ) return null;
                moltenScaleRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/AokinMoltenScale", AssetRequestMode.ImmediateLoad);
                return moltenScaleRef?.Value;
            }
        }

        /// <summary>熔火/蒸汽喷柱（程序化 FBM 焰体; uMode 0=熔火 1=蒸汽 2=死亡金白）。</summary>
        public static Effect FirePillarEffect {
            get {
                if (Main.dedServ) return null;
                firePillarRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/AokinFirePillar", AssetRequestMode.ImmediateLoad);
                return firePillarRef?.Value;
            }
        }

        /// <summary>火龙卷（双螺旋条纹炎旋柱, 一次 quad 取代分段贴图叠绘）。</summary>
        public static Effect FireTornadoEffect {
            get {
                if (Main.dedServ) return null;
                fireTornadoRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/AokinFireTornado", AssetRequestMode.ImmediateLoad);
                return fireTornadoRef?.Value;
            }
        }

        /// <summary>热浪蜃景（专属全屏后处理: 垂直对流 + 余烬亮点 + vent 冲击环）。</summary>
        public static Effect HeatHazeEffect {
            get {
                if (Main.dedServ) return null;
                heatHazeRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/AokinHeatHaze", AssetRequestMode.ImmediateLoad);
                return heatHazeRef?.Value;
            }
        }

        #endregion

        #region 四边形绘制 — 熔火柱 / 火龙卷（着色器 quad）

        /// <summary>
        /// 绘制熔火/蒸汽喷柱（AokinFirePillar 着色器, 单 quad）。须在已有活动批阶段调用（End→重开→恢复默认批）。
        /// </summary>
        /// <param name="bottom">柱底世界坐标。</param>
        /// <param name="height">柱高（像素）。</param>
        /// <param name="halfWidth">柱半宽（像素）。</param>
        /// <param name="growth">生长进度 0~1（自底向上显露）。</param>
        /// <param name="fade">存续度 1→0 噪声侵蚀消散。</param>
        /// <param name="intensity">整体亮度 0~1。</param>
        /// <param name="seed">每柱相位差。</param>
        /// <param name="mode">0=熔火 1=蒸汽 2=死亡金白。</param>
        /// <param name="rotation">整柱旋转（弧度, 绕柱底; ±PiOver2 = 横置火河）。</param>
        public static void DrawFirePillar(Vector2 bottom, float height, float halfWidth,
            float growth, float fade, float intensity, float seed, int mode, float rotation = 0f) {
            if (Main.dedServ || intensity <= 0.02f || height < 8f)
                return;

            Effect fx = FirePillarEffect;
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            if (fx == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uGrowth"]?.SetValue(MathHelper.Clamp(growth, 0f, 1f));
            fx.Parameters["uFade"]?.SetValue(MathHelper.Clamp(fade, 0f, 1f));
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uMode"]?.SetValue((float)mode);
            fx.Parameters["uWidth"]?.SetValue(1f);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            // MagicPixel 拉伸为 quad: origin 取柱底中点, 向上(-Y)伸展; rotation 绕柱底旋转
            Vector2 drawPos = bottom - Main.screenPosition;
            Rectangle src = new Rectangle(0, 0, pixel.Width, pixel.Height);
            Vector2 scale = new Vector2(halfWidth * 2f / pixel.Width, height / pixel.Height);
            Vector2 origin = new Vector2(pixel.Width / 2f, pixel.Height); // 底部中点
            sb.Draw(pixel, drawPos, src, Color.White, rotation, origin, scale, SpriteEffects.None, 0f);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 绘制火龙卷（AokinFireTornado 着色器, 单 quad, 中心锚点）。须在已有活动批阶段调用。
        /// </summary>
        /// <param name="center">龙卷中心世界坐标。</param>
        /// <param name="height">总高（像素）。</param>
        /// <param name="halfWidth">半宽（像素）。</param>
        /// <param name="intensity">强度 0~1。</param>
        /// <param name="ignite">点燃闪 0~1。</param>
        /// <param name="seed">相位差。</param>
        /// <param name="spin">旋速系数。</param>
        public static void DrawFireTornado(Vector2 center, float height, float halfWidth,
            float intensity, float ignite, float seed, float spin = 1f) {
            if (Main.dedServ || intensity <= 0.02f)
                return;

            Effect fx = FireTornadoEffect;
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            if (fx == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uIgnite"]?.SetValue(MathHelper.Clamp(ignite, 0f, 1f));
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uSpin"]?.SetValue(spin);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            Vector2 drawPos = center - Main.screenPosition;
            Rectangle src = new Rectangle(0, 0, pixel.Width, pixel.Height);
            Vector2 scale = new Vector2(halfWidth * 2f / pixel.Width, height / pixel.Height);
            Vector2 origin = new Vector2(pixel.Width / 2f, pixel.Height / 2f);
            sb.Draw(pixel, drawPos, src, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        #endregion

        #region 条带绘制 — 龙息锥 / 冲击环

        /// <summary>
        /// 绘制赤炎龙息锥形火舌（AokinBreathCone 条带）。须在已有活动批的阶段调用（会 End→重开→恢复默认批）。
        /// 顶点契约与 ACMShaders.DrawBeam 相同：世界坐标 - screenPosition + GameViewMatrix。
        /// </summary>
        /// <param name="mouth">口部世界坐标。</param>
        /// <param name="direction">喷息方向（单位向量）。</param>
        /// <param name="length">火舌长度（像素）。</param>
        /// <param name="endHalfWidth">尖端半宽（口部自动收窄）。</param>
        /// <param name="intensity">强度 0~1。</param>
        /// <param name="time">动画时间（秒）。</param>
        public static void DrawBreathCone(Vector2 mouth, Vector2 direction, float length,
            float endHalfWidth, float intensity, float time) {
            if (Main.dedServ || intensity <= 0.02f || length < 24f)
                return;

            Effect fx = BreathConeEffect;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            // 中心线取 4 个点让 CatmullRom 细分出轻微弧度（尾端下垂 = 火舌重量感）
            Vector2 perp = direction.RotatedBy(MathHelper.PiOver2);
            float sag = MathF.Sin(time * 5.3f) * length * 0.03f;
            Vector2[] spine = [
                mouth - Main.screenPosition,
                mouth + direction * length * 0.35f + perp * sag * 0.4f - Main.screenPosition,
                mouth + direction * length * 0.7f + perp * sag * 0.9f - Main.screenPosition,
                mouth + direction * length + perp * sag * 1.4f - Main.screenPosition,
            ];

            var verts = ACMUtils.BuildRibbonStrip(spine,
                p => MathHelper.Lerp(endHalfWidth * 0.16f, endHalfWidth, p),
                _ => Color.White, 0f, 3);
            if (verts.Length < 4)
                return;

            fx.Parameters["uTime"]?.SetValue(time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uFlowSpeed"]?.SetValue(2.1f);
            fx.Parameters["uNoiseScale"]?.SetValue(1.9f);
            fx.Parameters["uCoreSharp"]?.SetValue(3.1f);
            fx.Parameters["uColorCore"]?.SetValue(new Vector4(1f, 0.97f, 0.86f, 1f));
            fx.Parameters["uColorMid"]?.SetValue(MoltenOrange.ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(DragonFlameRed.ToVector4());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[0] = noise;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 绘制冲击火环（AokinShockRing 屏幕空间 SDF, 自行开合批, 可在 PreDraw 等已有批阶段调用）。
        /// </summary>
        /// <param name="worldCenter">环心世界坐标。</param>
        /// <param name="worldRadius">半径（世界像素）。</param>
        /// <param name="worldBand">环带半宽（世界像素）。</param>
        /// <param name="gapAngle">缺口中心角（弧度）。</param>
        /// <param name="gapHalf">缺口半宽（弧度, ≤0 = 无缺口）。</param>
        /// <param name="intensity">强度 0~1。</param>
        /// <param name="steamMode">true = 无伤蒸汽冲击观感（提白降饱和）。</param>
        /// <param name="time">动画时间（秒）。</param>
        public static void DrawShockRing(Vector2 worldCenter, float worldRadius, float worldBand,
            float gapAngle, float gapHalf, float intensity, bool steamMode, float time) {
            if (Main.dedServ || intensity <= 0.02f)
                return;

            Effect fx = ShockRingEffect;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(worldCenter, worldRadius,
                out Vector2 uvCenter, out float radiusFrac, out float aspect);
            float bandFrac = worldBand * Main.GameViewMatrix.Zoom.X / Main.screenHeight;

            fx.Parameters["uTime"]?.SetValue(time);
            fx.Parameters["uCenter"]?.SetValue(uvCenter);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uBand"]?.SetValue(MathF.Max(bandFrac, 0.004f));
            fx.Parameters["uGapAngle"]?.SetValue(gapAngle);
            fx.Parameters["uGapHalf"]?.SetValue(gapHalf);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uMode"]?.SetValue(steamMode ? 1f : 0f);
            fx.Parameters["uColorCore"]?.SetValue(new Vector4(1f, 0.96f, 0.82f, 1f));
            fx.Parameters["uColorEdge"]?.SetValue(
                Color.Lerp(MoltenOrange, TelegraphColors.Lethal, steamMode ? 0f : 0.45f).ToVector4());
            fx.Parameters["uColorSafe"]?.SetValue(TelegraphColors.Safe.ToVector4());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
            ACMShaders.RestoreDefaultBatch(sb);
        }

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

        /// <summary>
        /// 向心聚气粒子（蓄力语法: 100~350px 外向口部/核心汇聚, 带切向漩流）。
        /// density 0~1 控制生成概率, 蓄力后段应削减至静默（调用方裁剪）。
        /// </summary>
        public static void CreateConvergingEmbers(Vector2 focus, float density, float radius = 260f, float speedMul = 1f) {
            if (density <= 0.01f) return;
            int count = 1 + (int)(density * 3f);
            for (int i = 0; i < count; i++) {
                if (!Main.rand.NextBool(Math.Max(1, (int)(1f / MathF.Max(density, 0.05f)))))
                    continue;
                Vector2 pos = focus + Main.rand.NextVector2CircularEdge(radius, radius) * Main.rand.NextFloat(0.5f, 1f);
                Vector2 toFocus = (focus - pos) * 0.085f * speedMul;
                Vector2 swirl = new Vector2(-toFocus.Y, toFocus.X) * 0.5f;
                int dustType = Main.rand.NextBool(3) ? DustID.Torch : DustID.SolarFlare;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 1.2f + density * 1.3f;
                d.velocity = toFocus + swirl;
                d.alpha = 90;
            }
        }

        /// <summary>
        /// 蒸汽喷涌粒子（沸海蒸腾: 白热烟雾向上升腾）。
        /// </summary>
        public static void CreateSteamBurst(Vector2 center, float radius, int count) {
            for (int i = 0; i < count; i++) {
                Vector2 pos = center + Main.rand.NextVector2Circular(radius, radius * 0.5f);
                int dustType = Main.rand.NextBool(3) ? DustID.Smoke : DustID.Torch;
                var d = Dust.NewDustPerfect(pos, dustType);
                d.noGravity = true;
                d.scale = 2.2f + Main.rand.NextFloat(1.4f);
                d.velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(4f, 11f));
                d.alpha = dustType == DustID.Smoke ? 120 : 60;
                if (dustType == DustID.Smoke)
                    d.color = SteamWhite;
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

        /// <summary>
        /// 绘制预警直线（LightShot 双层拉伸: 外层主题色 + 细芯）。用于冲刺线 / 俯冲垂直红线。
        /// </summary>
        public static void DrawTelegraphLine(SpriteBatch sb, Vector2 worldStart, Vector2 worldEnd,
            Color color, float intensity, float coreWidth = 0.1f) {
            if (ACMAsset.LightShot == null || intensity <= 0.02f) return;

            Texture2D tex = ACMAsset.LightShot;
            Vector2 a = worldStart - Main.screenPosition;
            Vector2 b = worldEnd - Main.screenPosition;
            Vector2 mid = (a + b) / 2f;
            float rot = (b - a).ToRotation();
            float len = Vector2.Distance(a, b);
            if (len < 8f) return;

            Color outer = color * (0.42f * intensity);
            outer.A = 0;
            sb.Draw(tex, mid, null, outer, rot, tex.Size() / 2f,
                new Vector2(len / tex.Width, coreWidth * 2.6f), SpriteEffects.None, 0f);

            Color core = Color.Lerp(color, Color.White, 0.55f) * (0.7f * intensity);
            core.A = 0;
            sb.Draw(tex, mid, null, core, rot, tex.Size() / 2f,
                new Vector2(len / tex.Width, coreWidth), SpriteEffects.None, 0f);
        }

        #endregion
    }
}
