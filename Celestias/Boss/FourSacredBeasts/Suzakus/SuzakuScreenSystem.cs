using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Suzakus
{
    /// <summary>
    /// 朱雀 V2 屏幕氛围系统（着色器表现层，硬化 ACMShaders 验证用例）。
    /// 由 <see cref="Suzaku"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动，集中绘制三类非 screenTarget overlay：
    ///   ● <b>ElementalScreenTint</b> —— 战场常驻的赤焰氛围底色（随阶段加浓）。
    ///   ● <b>ArenaRunic</b>(法阵) —— 火柱"棋局"/涅槃点燃时向心的太阳法阵地纹（向心收口=可读预警）。
    ///   ● <b>RadialBloom</b> —— 俯冲落点/审判/涅槃点燃瞬间的加性赤金泛光。
    /// 昂贵的全屏 screenTarget 调色（PaletteLUT 灰→赤 涅槃 grade）由 <see cref="Suzaku.PostDraw"/> 单独申请名额。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/>(实体下层) → 不遮挡需躲避的弹幕信息(§6.6)。纯本地视觉、
    /// 服务端零绘制、受 <see cref="MythologyConfig.FullscreenShadersEnabled"/> 降级。
    /// </summary>
    public class SuzakuScreenSystem : ModSystem
    {
        private static float _tint;     // ElementalScreenTint 强度
        private static float _bloom;    // RadialBloom 强度
        private static float _runic;    // ArenaRunic 法阵强度
        private static float _ashen;    // 涅槃灰烬去饱和（SuzakuSky 联动消费）
        private static float _sunBurst; // 天幕日轮爆亮（入场/涅槃爆燃, SuzakuSky 联动消费）
        private static Vector2 _center; // 世界坐标中心（Boss / 落点）
        private static float _time;     // 着色器时间(秒)
        private static ulong _lastPublishFrame;

        /// <summary>涅槃灰烬期天幕去饱和标量（0~1, 由 SuzakuSky 读取）。</summary>
        public static float AshenLevel => _ashen;
        /// <summary>天幕日轮爆亮标量（0~1, 由 SuzakuSky 读取）。</summary>
        public static float SunBurstLevel => _sunBurst;

        /// <summary>由 Suzaku 每帧调用，发布当前赤焰氛围标量（纯本地视觉）。</summary>
        public static void Publish(Vector2 center, float tint, float bloom, float runic, float time,
            float ashen = 0f, float sunBurst = 0f) {
            _center = center;
            _tint = tint;
            _bloom = bloom;
            _runic = runic;
            _ashen = ashen;
            _sunBurst = sunBurst;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _tint = _bloom = _runic = _ashen = _sunBurst = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 不在场/未发布时平滑淡出
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _tint = MathHelper.Lerp(_tint, 0f, 0.1f);
                _bloom = MathHelper.Lerp(_bloom, 0f, 0.18f);
                _runic = MathHelper.Lerp(_runic, 0f, 0.15f);
                _ashen = MathHelper.Lerp(_ashen, 0f, 0.06f);
                _sunBurst = MathHelper.Lerp(_sunBurst, 0f, 0.12f);
            }

            DrawAmbientTint();
            DrawSolarRunic();
            DrawSolarBloom();
        }

        private static void DrawAmbientTint() {
            // 灰烬寂静期赤焰氛围让位给去饱和（PaletteLUT 主导）, 火幕淡出
            float tint = _tint * (1f - _ashen * 0.8f);
            if (tint <= 0.01f) return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null) return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(tint, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 上=金橙日照，下=暗赤压底；覆盖度保守，始终看得清弹幕
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.Flame.ToVector3(), 0.26f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(new Color(120, 20, 14).ToVector3(), 0f));
            fx.Parameters["uVignette"]?.SetValue(0.42f);
            fx.Parameters["uFogScale"]?.SetValue(2.2f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        private static void DrawSolarRunic() {
            if (_runic <= 0.01f) return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null) return;

            Vector2 uv = (_center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(0.55f);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_runic, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.Vermilion.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(TelegraphColors.Gold.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(12f);
            fx.Parameters["uMode"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        private static void DrawSolarBloom() {
            if (_bloom <= 0.01f) return;
            Effect fx = ACMShaders.RadialBloom;
            if (fx == null) return;

            Vector2 uv = (_center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_bloom, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(0.30f + (1f - _bloom) * 0.45f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColor"]?.SetValue(new Vector4(new Color(255, 190, 90).ToVector3(), 1f));
            fx.Parameters["uRayCount"]?.SetValue(12f);
            fx.Parameters["uFalloff"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
        }
    }
}
