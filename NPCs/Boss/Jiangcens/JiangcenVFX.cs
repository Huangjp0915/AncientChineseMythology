using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Jiangcens
{
    /// <summary>
    /// 将臣 V3 视觉工具箱：专属着色器缓存（Xuanwu 模式，不注册进 ACMShaders）+ 电弧条带绘制原语。
    /// <para><b>电弧批量契约</b>：<see cref="BeginArcs"/> → 多次 <see cref="Arc"/> → <see cref="EndArcs"/>，
    /// 在已有活动批的阶段调用（PreDraw 等）；Begin 会 End 当前批，End 恢复项目默认批。
    /// 每条弧仅 4 顶点，折线形状全部由 JiangcenLightningArc.fx 在像素端生成。</para>
    /// </summary>
    internal class JiangcenVFX : IACMLoader
    {
        // ===== 专属着色器缓存 =====
        private static Asset<Effect> _arcRef;
        private static Asset<Effect> _skyRef;

        /// <summary>电弧条带着色器（TriangleStrip，与 BeamGrad 同顶点契约）。</summary>
        public static Effect ArcEffect {
            get {
                if (Main.dedServ)
                    return null;
                _arcRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/JiangcenLightningArc", AssetRequestMode.ImmediateLoad);
                return _arcRef?.Value;
            }
        }

        /// <summary>雷暴天幕着色器（全屏程序化）。</summary>
        public static Effect SkyEffect {
            get {
                if (Main.dedServ)
                    return null;
                _skyRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/JiangcenStormSky", AssetRequestMode.ImmediateLoad);
                return _skyRef?.Value;
            }
        }

        void IACMLoader.UnLoadData() {
            _arcRef = null;
            _skyRef = null;
        }

        // ===== 将臣配色语言（§设计文档 2）=====
        /// <summary>尸暗红：尸气氛围（坟/尸手/残影），非预警色。</summary>
        public static readonly Color CorpseRed = new(180, 42, 36);
        /// <summary>深尸红：暗部。</summary>
        public static readonly Color DeepCorpse = new(96, 14, 20);
        /// <summary>电弧辉光蓝（电弧 halo 用，芯用 TelegraphColors.Lightning）。</summary>
        public static readonly Color ArcBlue = new(58, 110, 210);
        /// <summary>军金：将令符印/点将播报（身份色，用量克制）。</summary>
        public static readonly Color GeneralGold = new(255, 214, 120);

        // ===== 电弧批量绘制 =====
        private static bool _arcBatchActive;
        private static readonly ColoredVertex[] _quad = new ColoredVertex[4];

        /// <summary>
        /// 开启电弧批：End 当前批 → Immediate + Additive + GameViewMatrix，绑共享噪声到 s0/s1。
        /// 必须与 <see cref="EndArcs"/> 配对；批内多次 <see cref="Arc"/> 仅重设 uniform，无开合销。
        /// </summary>
        public static void BeginArcs(SpriteBatch sb) {
            if (Main.dedServ || _arcBatchActive)
                return;
            Effect fx = ArcEffect;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[0] = noise;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            _arcBatchActive = true;
        }

        /// <summary>
        /// 画一条电弧（世界坐标两端点）。halfWidth 为屏幕像素半宽（含辉光域，芯远窄于此）。
        /// seed 决定折线形状（同 seed 同帧形状一致）；jagAmp 0~0.42 折幅；flickerHz=0 不频闪。
        /// </summary>
        public static void Arc(Vector2 worldStart, Vector2 worldEnd, float halfWidth,
            Color core, Color edge, float intensity, float seed,
            float jagAmp = 0.30f, float jagScale = 10f, float flickerHz = 24f, float rerollHz = 13f, float coreGlow = 0.9f) {
            if (!_arcBatchActive || intensity <= 0.01f || halfWidth < 0.5f)
                return;
            Effect fx = ArcEffect;
            if (fx == null)
                return;

            Vector2 a = worldStart - Main.screenPosition;
            Vector2 b = worldEnd - Main.screenPosition;
            Vector2 dir = b - a;
            if (dir.LengthSquared() < 4f)
                return;
            Vector2 n = dir.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * halfWidth;

            _quad[0] = new ColoredVertex(a + n, new Vector3(0f, 0f, 1f), Color.White);
            _quad[1] = new ColoredVertex(a - n, new Vector3(0f, 1f, 1f), Color.White);
            _quad[2] = new ColoredVertex(b + n, new Vector3(1f, 0f, 1f), Color.White);
            _quad[3] = new ColoredVertex(b - n, new Vector3(1f, 1f, 1f), Color.White);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uColorCore"]?.SetValue(core.ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(edge.ToVector4());
            fx.Parameters["uCoreGlow"]?.SetValue(coreGlow);
            fx.Parameters["uJagAmp"]?.SetValue(jagAmp);
            fx.Parameters["uJagScale"]?.SetValue(jagScale);
            fx.Parameters["uFlickerHz"]?.SetValue(flickerHz);
            fx.Parameters["uRerollHz"]?.SetValue(rerollHz);
            fx.CurrentTechnique.Passes[0].Apply();

            Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, _quad, 0, 2);
        }

        /// <summary>结束电弧批并恢复项目默认批。</summary>
        public static void EndArcs(SpriteBatch sb) {
            if (!_arcBatchActive)
                return;
            _arcBatchActive = false;
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 单条电弧便捷入口（内部自开合批）。多条相邻请手动 Begin/End 省开合。
        /// </summary>
        public static void DrawArcStandalone(Vector2 worldStart, Vector2 worldEnd, float halfWidth,
            Color core, Color edge, float intensity, float seed, float jagAmp = 0.30f, float jagScale = 10f) {
            BeginArcs(Main.spriteBatch);
            Arc(worldStart, worldEnd, halfWidth, core, edge, intensity, seed, jagAmp, jagScale);
            EndArcs(Main.spriteBatch);
        }

        // ===== ElectricArcSheet 缠电贴段（便宜的实体缠电，活动批内直接画）=====

        /// <summary>
        /// 在实体周身贴随机电弧段（ElectricArcSheet 四行随机帧，additive 色 A=0）。
        /// 每 4 帧换一次帧/角度组合（frameSalt 参与），intensity 控制段数与亮度。活动批内调用。
        /// </summary>
        public static void DrawBodyArcs(SpriteBatch sb, Vector2 worldCenter, float radius, float intensity, int frameSalt) {
            if (Main.dedServ || intensity <= 0.03f)
                return;
            Texture2D sheet = ACMAsset.ElectricArcSheet;
            if (sheet == null)
                return;

            int rows = 4;
            int frameH = sheet.Height / rows;
            int count = 1 + (int)(intensity * 2.9f);
            //时间片驱动的伪随机: 所有端一致, 且每 4 帧跳变(电的抖动感)
            int slice = (int)(Main.GameUpdateCount / 4) * 131 + frameSalt * 17;

            for (int i = 0; i < count; i++) {
                int h = slice + i * 977;
                float ang = (h % 628) * 0.01f;
                int row = (h / 7) % rows;
                float flick = 0.55f + 0.45f * (((h / 11) % 100) * 0.01f);
                Vector2 off = ang.ToRotationVector2() * radius * (0.35f + ((h / 13) % 60) * 0.01f);
                Rectangle src = new(0, row * frameH, sheet.Width, frameH);
                Color c = TelegraphColors.Lightning with { A = 0 } * (intensity * flick * 0.8f);
                float scale = 0.30f + ((h / 17) % 40) * 0.004f;
                sb.Draw(sheet, worldCenter + off - Main.screenPosition, src, c,
                    ang + MathHelper.PiOver2, new Vector2(sheet.Width / 2f, frameH / 2f), scale,
                    (h & 1) == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
            }
        }

        /// <summary>0→1→0 冲击包络（snap 用）。</summary>
        public static float Bump(float t) => MathF.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi);
    }
}
