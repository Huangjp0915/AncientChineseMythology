using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialDragons
{
    /// <summary>
    /// 天御金龙 V2 金芒屏幕氛围系统。由 <see cref="CelestialDragons"/> 头部每帧 <see cref="Publish"/> 一组 0~1 标量驱动:
    ///   ● <b>ElementalScreenTint</b> —— 常驻金芒底色(随幕 巡天→敕令→天罚 加浓, 过场脉冲), 天界权威的恒定可读底色。
    ///   ● <b>ArenaRunic</b>(法阵模式) —— 敕令幕的金色天规法阵地纹(以玩家为中心, 法标环半径), 标示场地规则。
    /// 金芒径向泛光由头部 <see cref="CelestialDragons.PostDraw"/> 走 <see cref="ACMShaders.DrawRadialBloomAt"/> 在事件瞬间绘制。
    /// 绘制位于 <see cref="PostDrawTiles"/>(实体之下) → 不遮挡需躲避的弹幕(§6.6)。纯本地视觉, 服务端零绘制, 受 MythologyConfig 降级。
    /// </summary>
    public class CelestialScreenSystem : ModSystem
    {
        private static float _tint;
        private static float _runic;
        private static float _runicRadius;
        private static Vector2 _arenaCenter;
        private static float _time;
        private static ulong _lastPublishFrame;

        /// <summary>由金龙头部每帧调用, 发布当前金芒氛围标量(纯本地视觉)。</summary>
        public static void Publish(Vector2 arenaCenter, float tint, float runic, float runicRadius, float time) {
            _arenaCenter = arenaCenter;
            _tint = tint;
            _runic = runic;
            _runicRadius = runicRadius;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _tint = _runic = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 不在场/未发布时平滑淡出, 避免状态残留
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _tint = MathHelper.Lerp(_tint, 0f, 0.1f);
                _runic = MathHelper.Lerp(_runic, 0f, 0.15f);
            }

            DrawTint();
            DrawEdictRunic();
        }

        // ===== ElementalScreenTint: 金芒氛围底色 =====
        private static void DrawTint() {
            if (_tint <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_tint, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 上=暖金, 下=琥珀压暗; 覆盖度保守, 始终看得清弹幕
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.Gold.ToVector3(), 0.26f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(TelegraphColors.Flame.ToVector3() * 0.45f, 0f));
            fx.Parameters["uVignette"]?.SetValue(0.4f);
            fx.Parameters["uFogScale"]?.SetValue(2.2f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== ArenaRunic: 敕令幕天规法阵地纹 =====
        private static void DrawEdictRunic() {
            if (_runic <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_arenaCenter, _runicRadius, out Vector2 uv, out float radFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_runic, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(TelegraphColors.Gold.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(new Color(255, 150, 60).ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(12f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.NonPremultiplied);
        }
    }
}
