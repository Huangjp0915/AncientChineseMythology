using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    /// <summary>
    /// 敖钦 V2 热浪屏幕氛围系统（对位 <see cref="Aoyuans.AoyuanFrostScreenSystem"/>）。
    /// 由 <see cref="Aokin"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动, 集中绘制三类非 screenTarget 后处理:
    ///   ● <b>ElementalScreenTint</b> —— 温度条氛围底色(= HeatRatio, 上暖橙下焦黑, 始终能看清弹幕)。
    ///   ● <b>ArenaRunic</b>(法阵) —— 炼狱茧蓄力向心收口 / 焚海劫常驻的熔火场地纹预警。
    ///   ● <b>RadialBloom</b> —— 出场/相变/泄压瞬间的加性熔岩泛光。
    /// 昂贵的全屏 screenTarget 扭曲(GenericWarp heat)由 <see cref="Aokin.PostDraw"/> 单独申请名额绘制。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/>(无活动批): 氛围/泛光/地纹位于实体之下, 危险弹幕在其上层 → 不遮挡躲避信息(§6.6)。
    /// 纯本地视觉, 服务端零绘制, 受 MythologyConfig 降级。
    /// </summary>
    public class AokinHeatScreenSystem : ModSystem
    {
        private static float _tint;
        private static float _runic;
        private static float _bloom;
        private static float _arenaHalf;
        private static bool _phase3;
        private static Vector2 _center;
        private static float _time;
        private static ulong _lastPublishFrame;

        /// <summary>由 Aokin 每帧调用, 发布当前热浪氛围标量（纯本地视觉）。</summary>
        public static void Publish(Vector2 center, float tint, float runic, float bloom, float arenaHalf, bool phase3, float time) {
            _center = center;
            _tint = tint;
            _runic = runic;
            _bloom = bloom;
            _arenaHalf = arenaHalf;
            _phase3 = phase3;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _tint = _runic = _bloom = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _tint = MathHelper.Lerp(_tint, 0f, 0.1f);
                _runic = MathHelper.Lerp(_runic, 0f, 0.15f);
                _bloom = MathHelper.Lerp(_bloom, 0f, 0.15f);
            }

            DrawAmbientTint();
            DrawArenaRunic();
            DrawLavaBloom();
        }

        // ===== ElementalScreenTint: 温度条热浪底色 =====
        private static void DrawAmbientTint() {
            if (_tint <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_tint, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 上=暖橙热雾, 下=焦黑压暗; 覆盖度保守, 始终能看清致命火柱/熔岩
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.Flame.ToVector3(), 0.28f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(AokinHelper.EmberBlack.ToVector3(), 0f));
            fx.Parameters["uVignette"]?.SetValue(0.42f);
            fx.Parameters["uFogScale"]?.SetValue(2.6f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== ArenaRunic: 炼狱茧蓄力 / 焚海劫熔火场地纹 =====
        private static void DrawArenaRunic() {
            if (_runic <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            // 焚海劫常驻地纹半径与竞技场挂钩
            ACMShaders.WorldDecalParams(_center, _phase3 ? _arenaHalf : 360f,
                out Vector2 uv, out float radiusFrac, out float aspect);

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(MathHelper.Clamp(radiusFrac, 0.2f, 0.9f));
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_runic, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.Flame.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(TelegraphColors.Gold.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(9f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== RadialBloom: 熔岩 / 泄压泛光 =====
        private static void DrawLavaBloom() {
            if (_bloom <= 0.01f)
                return;
            Effect fx = ACMShaders.RadialBloom;
            if (fx == null)
                return;

            Vector2 uv = (_center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_bloom, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(0.4f + (1f - _bloom) * 0.45f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColor"]?.SetValue(new Vector4(AokinHelper.MoltenOrange.ToVector3(), 1f));
            fx.Parameters["uRayCount"]?.SetValue(0f);
            fx.Parameters["uFalloff"]?.SetValue(2.3f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
        }
    }
}
