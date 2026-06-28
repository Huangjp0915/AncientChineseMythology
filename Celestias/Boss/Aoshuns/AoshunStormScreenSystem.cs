using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    /// <summary>
    /// 敖顺 V2 风暴屏幕氛围系统（对位 <see cref="Aokins.AokinHeatScreenSystem"/> / <see cref="Aoyuans.AoyuanFrostScreenSystem"/>）。
    /// 由 <see cref="Aoshun"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动, 集中绘制三类**非 screenTarget** overlay 后处理:
    ///   ● <b>ElementalScreenTint</b> —— 风暴压暗底色(= StormCharge 电量, 上墨蓝下深渊紫; 满电"雷暴临界"加深暗角)。
    ///   ● <b>ArenaRunic</b>(法阵环) —— 风暴之眼缩小安全区的边界环(安全/致命双色提示)。
    ///   ● <b>RadialBloom</b> —— 深渊伏击爆出 / 相变 / 满电临界 / 连环冲的加性雷击泛光。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/>(无活动批): 氛围/泛光/地纹位于实体之下, 危险弹幕在其上层 → 不遮挡躲避信息(§6.6)。
    /// 全部为占位像素 overlay, 不占 RequestFullscreenSlot 名额(不读 screenTarget); 纯本地视觉, 服务端零绘制, 受 MythologyConfig 降级。
    /// </summary>
    public class AoshunStormScreenSystem : ModSystem
    {
        private static float _tint;          // 风暴压暗强度 0~1
        private static float _bloom;         // 瞬时雷击泛光 0~1
        private static bool _critical;       // 满电"雷暴临界"
        private static Vector2 _center;      // Boss 中心(世界)

        // 风暴之眼安全区
        private static bool _eyeActive;
        private static Vector2 _eyeCenter;
        private static float _eyeRadius;

        private static float _time;
        private static ulong _lastPublishFrame;

        /// <summary>由 Aoshun 每帧调用, 发布当前风暴氛围标量（纯本地视觉）。</summary>
        public static void Publish(Vector2 center, float tint, float bloom, bool critical,
            bool eyeActive, Vector2 eyeCenter, float eyeRadius, float time) {
            _center = center;
            _tint = tint;
            _bloom = bloom;
            _critical = critical;
            _eyeActive = eyeActive;
            _eyeCenter = eyeCenter;
            _eyeRadius = eyeRadius;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        /// <summary>瞬时雷击泛光脉冲(取 max, 不累加)。</summary>
        public static void PulseBloom(float amount) {
            if (amount > _bloom)
                _bloom = amount;
        }

        public override void OnWorldUnload() {
            _tint = _bloom = 0f;
            _eyeActive = false;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 失活/不再发布时平滑收尾, 防止氛围残留
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _tint = MathHelper.Lerp(_tint, 0f, 0.1f);
                _bloom = MathHelper.Lerp(_bloom, 0f, 0.15f);
                _eyeActive = false;
            }

            DrawStormDarkening();
            DrawStormEyeRing();
            DrawStrikeBloom();
        }

        // ===== ElementalScreenTint: 风暴压暗底色 = 电量条 =====
        private static void DrawStormDarkening() {
            if (_tint <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            // 满电临界时覆盖度/暗角进一步加深, 给"它要放大招了"的视觉前兆
            float coverage = 0.20f + _tint * 0.34f + (_critical ? 0.06f : 0f);
            float vignette = 0.44f + (_critical ? 0.18f : 0f);

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_tint, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 上=墨蓝风暴云(压暗), 下=深渊墨紫(近黑); 覆盖度保守, 始终能看清致命雷柱/电网
            fx.Parameters["uTint"]?.SetValue(new Vector4(new Vector3(0.11f, 0.10f, 0.22f), coverage));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(AoshunHelper.AbyssPurple.ToVector3(), 0f));
            fx.Parameters["uVignette"]?.SetValue(vignette);
            fx.Parameters["uFogScale"]?.SetValue(3.2f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== ArenaRunic: 风暴之眼安全区边界环 =====
        private static void DrawStormEyeRing() {
            if (!_eyeActive || _eyeRadius < 16f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_eyeCenter, _eyeRadius, out Vector2 uv, out float radiusFrac, out float aspect);

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(MathHelper.Clamp(radiusFrac, 0.1f, 0.95f));
            fx.Parameters["uIntensity"]?.SetValue(0.85f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 内安全(翠玉) / 外致命(纯红) 双色: 一眼读出"圈内活、圈外死"
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.Safe.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(TelegraphColors.Lethal.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(10f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== RadialBloom: 雷击 / 临界 / 泄压泛光 =====
        private static void DrawStrikeBloom() {
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
            fx.Parameters["uRadius"]?.SetValue(0.35f + (1f - _bloom) * 0.4f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColor"]?.SetValue(new Vector4(TelegraphColors.Lightning.ToVector3(), 1f));
            fx.Parameters["uRayCount"]?.SetValue(8f);
            fx.Parameters["uFalloff"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);

            // 逐帧衰减(由 PulseBloom 取 max 续命)
            _bloom = MathHelper.Lerp(_bloom, 0f, 0.12f);
        }
    }
}
