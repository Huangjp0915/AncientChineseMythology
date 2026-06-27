using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vigors
{
    /// <summary>
    /// 神威·断罪刃 V2 屏幕演出系统（着色器表现层，硬化 <see cref="ACMShaders"/> 复用）。
    /// 由 <see cref="Vigor"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动，集中绘制三类非 screenTarget overlay：
    ///   ● <b>ElementalScreenTint</b> —— 罪名色金幕（按阶段 素金→金蓝→赤金），格挡架势("断罪预兆")时
    ///     升级为暖金暗角脉冲，把"现在别打"提到屏幕级。
    ///   ● <b>ArenaRunic</b>(法阵) —— 符印封锁区地纹（<see cref="ACMShaders.WorldDecalParams"/> 缩放对齐，
    ///     引爆将近时由暗转亮 = 可读预警）。
    ///   ● <b>RadialBloom</b> —— 断罪判决处决砸落瞬间的加性金色收束泛光。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/>(实体下层) → 不遮挡需躲避的弹幕信息。纯本地视觉、
    /// 服务端零绘制、受 <see cref="MythologyConfig.FullscreenShadersEnabled"/> 降级。
    /// 判决光束(DrawBeam)/格挡反击护罩环(DrawScreenSpaceDecal) 需活动批，由 <see cref="Vigor.PreDraw"/> 直接绘制。
    /// </summary>
    public class VigorVerdictSystem : ModSystem
    {
        private static int _phaseTier;       // 0=试炼 素金 / 1=裁决 金蓝 / 2=天刑 赤金
        private static float _counterTell;   // 格挡架势暗角脉冲 0~1
        private static Vector2 _sealCenter;  // 符印封锁区中心(世界)
        private static float _sealRadius;    // 符印封锁区半径(世界像素)
        private static float _sealRunic;     // 符印地纹强度 0~1
        private static Vector2 _bloomCenter; // 处决泛光中心(世界)
        private static float _bloomRadius;   // 处决泛光半径(屏幕高度比例)
        private static float _bloom;         // 处决泛光强度 0~1
        private static float _time;          // 着色器时间(秒)
        private static ulong _lastPublishFrame;

        /// <summary>由 Vigor 每帧调用，发布当前断罪演出标量（纯本地视觉）。</summary>
        public static void Publish(int phaseTier, float counterTell,
            Vector2 sealCenter, float sealRadius, float sealRunic,
            Vector2 bloomCenter, float bloomRadius, float bloom, float time) {
            _phaseTier = phaseTier;
            _counterTell = counterTell;
            _sealCenter = sealCenter;
            _sealRadius = sealRadius;
            _sealRunic = sealRunic;
            _bloomCenter = bloomCenter;
            _bloomRadius = bloomRadius;
            _bloom = bloom;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _counterTell = _sealRunic = _bloom = 0f;
        }

        /// <summary>罪名色：试炼=素金 / 裁决=金蓝 / 天刑=赤金(暖琥珀，非纯红，遵守红=致命专用)。</summary>
        private static Color VerdictColor() => _phaseTier switch {
            2 => Color.Lerp(TelegraphColors.Gold, new Color(255, 120, 40), 0.5f),
            1 => Color.Lerp(TelegraphColors.Gold, new Color(120, 170, 255), 0.4f),
            _ => new Color(224, 184, 92)
        };

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 不在场/未发布时平滑淡出
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _counterTell = MathHelper.Lerp(_counterTell, 0f, 0.12f);
                _sealRunic = MathHelper.Lerp(_sealRunic, 0f, 0.15f);
                _bloom = MathHelper.Lerp(_bloom, 0f, 0.18f);
            }

            DrawSealRunic();
            DrawVerdictTint();
            DrawVerdictBloom();
        }

        // —— 符印封锁区地纹(ArenaRunic, WorldDecalParams 缩放对齐) ——
        private static void DrawSealRunic() {
            if (_sealRunic <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_sealCenter, _sealRadius, out Vector2 uv, out float radFrac, out float aspect);
            Color warm = Color.Lerp(TelegraphColors.Gold, new Color(255, 140, 40), 0.45f);
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_sealRunic, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.Gold.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(warm.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(11f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
        }

        // —— 罪名色金幕 + 格挡暗角脉冲(ElementalScreenTint) ——
        private static void DrawVerdictTint() {
            float baseAccent = _phaseTier == 2 ? 0.15f : _phaseTier == 1 ? 0.11f : 0.07f;
            float intensity = MathHelper.Clamp(baseAccent + _counterTell * 0.55f + _bloom * 0.3f, 0f, 1f);
            if (intensity <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            Color tint = VerdictColor();
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            float coverage = 0.08f + _counterTell * 0.20f + _bloom * 0.10f;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(intensity);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uTint"]?.SetValue(new Vector4(tint.ToVector3(), coverage));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(new Color(40, 30, 12).ToVector3(), 0f));
            // 格挡架势时暗角收紧 = "现在别打"屏幕级信号
            fx.Parameters["uVignette"]?.SetValue(0.32f + _counterTell * 0.42f);
            fx.Parameters["uFogScale"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // —— 断罪处决砸落泛光(RadialBloom) ——
        private static void DrawVerdictBloom() {
            if (_bloom <= 0.01f)
                return;
            Effect fx = ACMShaders.RadialBloom;
            if (fx == null)
                return;

            Vector2 uv = (_bloomCenter - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            Color bloomC = Color.Lerp(new Color(255, 220, 130), VerdictColor(), 0.5f);
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_bloom, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(_bloomRadius);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColor"]?.SetValue(new Vector4(bloomC.ToVector3(), 1f));
            fx.Parameters["uRayCount"]?.SetValue(14f);
            fx.Parameters["uFalloff"]?.SetValue(2.3f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
        }
    }
}
