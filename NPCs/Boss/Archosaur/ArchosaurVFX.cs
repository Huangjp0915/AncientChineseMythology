using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Archosaur
{
    /// <summary>
    /// 祖龙残魂 V3 专属视觉中枢 — 三个专属着色器的静态缓存 (Xuanwu 模式: 惰性 ImmediateLoad 一次,
    /// 不注册进 ACMShaders) + 闪电顶点条带绘制原语 + 主题配色。
    /// 闪电形态完全在 <c>ArchosaurLightning.fx</c> 内程序化 (直带 + 带内折线核心), C# 侧只提供两端点。
    /// </summary>
    internal class ArchosaurVFX : IACMLoader
    {
        private const string Path = "AncientChineseMythology/Effects/";

        // ===== 主题配色 =====
        /// <summary>真身金 (鎏金瞳 / 破绽窗口 / 死亡金屑)。</summary>
        public static readonly Color GoldSoul = new(255, 214, 120);
        /// <summary>幻影灰蓝 (分身识别色, 永不与致命红混用)。</summary>
        public static readonly Color PhantomBlue = new(150, 180, 220);
        /// <summary>雷暴深底色 (天幕压暗)。</summary>
        public static readonly Color StormDeep = new(16, 28, 56);
        /// <summary>雷芯青白 (与 TelegraphColors.Lightning 同族的亮芯)。</summary>
        public static readonly Color BoltCore = new(225, 245, 255);

        // ===== Effect 缓存 =====
        private static Asset<Effect> _stormSky;
        private static Asset<Effect> _lightning;
        private static Asset<Effect> _phantom;

        /// <summary>全屏雷暴天幕 overlay (云海压暗 + 雨丝 + 天空白闪 + 破绽金化)。占位像素喂图, s1=共享噪声。</summary>
        public static Effect StormSky => Get(ref _stormSky, "ArchosaurStormSky");
        /// <summary>顶点条带闪电 (带内程序化折线, uSeed 控形态)。</summary>
        public static Effect Lightning => Get(ref _lightning, "ArchosaurLightning");
        /// <summary>幻影显形 (去饱和灰蓝 + 扫描线撕裂 + 边缘溶解)。s0=NPC 贴图, s1=共享噪声。</summary>
        public static Effect Phantom => Get(ref _phantom, "ArchosaurPhantom");

        private static Effect Get(ref Asset<Effect> slot, string name) {
            if (Main.dedServ)
                return null;
            slot ??= ModContent.Request<Effect>(Path + name, AssetRequestMode.ImmediateLoad);
            return slot?.Value;
        }

        /// <summary>
        /// 绘制一道程序化闪电 (直线条带, 折线/分叉/闪烁全在 PS 内)。顶点契约与
        /// <see cref="ACMShaders.DrawBeam"/> 相同: 世界坐标 - screenPosition + GameViewMatrix。
        /// </summary>
        /// <param name="worldStart">起点(世界坐标, 通常为天/上端)。</param>
        /// <param name="worldEnd">终点(世界坐标, 通常为落点)。</param>
        /// <param name="halfWidth">条带半宽(px)。折线在带内游走, 建议 ≥ 28。</param>
        /// <param name="core">雷芯色。</param>
        /// <param name="edge">辉光边色。</param>
        /// <param name="intensity">强度 0~1。</param>
        /// <param name="seed">形态种子 (同一道雷保持恒定, 不同雷相异)。</param>
        /// <param name="jagAmp">折线振幅 0~1 (0.55 为标准闪电)。</param>
        /// <param name="flicker">高频闪烁强度 0~1。</param>
        /// <param name="hasActiveBatch">调用点是否已有活动 SpriteBatch (弹幕 PreDraw=true, PostDrawTiles=false)。</param>
        public static void DrawLightningStrip(Vector2 worldStart, Vector2 worldEnd, float halfWidth,
            Color core, Color edge, float intensity, float seed,
            float jagAmp = 0.55f, float flicker = 0.5f, bool hasActiveBatch = true) {
            if (Main.dedServ || intensity <= 0.01f || halfWidth < 1f)
                return;

            Effect fx = Lightning;
            if (fx == null)
                return;

            Vector2 a = worldStart - Main.screenPosition;
            Vector2 b = worldEnd - Main.screenPosition;
            if ((b - a).LengthSquared() < 4f)
                return;

            var verts = ACMUtils.BuildRibbonStrip([a, b], _ => halfWidth, _ => Color.White, 0f, 1);
            if (verts.Length < 4)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uColorCore"]?.SetValue(core.ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(edge.ToVector4());
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uJagAmp"]?.SetValue(jagAmp);
            fx.Parameters["uFlicker"]?.SetValue(flicker);

            SpriteBatch sb = Main.spriteBatch;
            if (hasActiveBatch)
                sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Texture2D noise = ACMShaders.NoiseTexture;
            gd.Textures[0] = noise;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

            sb.End();
            if (hasActiveBatch)
                ACMShaders.RestoreDefaultBatch(sb);
        }

        void IACMLoader.UnLoadData() {
            _stormSky = null;
            _lightning = null;
            _phantom = null;
        }
    }
}
