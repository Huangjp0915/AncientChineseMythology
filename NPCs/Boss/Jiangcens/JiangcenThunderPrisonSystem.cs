using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Jiangcens
{
    /// <summary>
    /// 将臣 V2 「雷牢降临」屏幕氛围系统（对位 <see cref="Celestias.Boss.Aokins.AokinHeatScreenSystem"/>）。
    /// 由 <see cref="Jiangcen"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动, 集中绘制三类非 screenTarget overlay:
    ///   ● <b>ElementalScreenTint</b> —— 雷暴压暗底色(雷青 + 焦黑暗角), 雷狱阶段常驻, 始终能看清弹幕。
    ///   ● <b>ArenaRunic</b>(牢笼罩 uMode=1) —— 可见的环形雷牢: 进入雷狱时合拢, 之后常驻标出"别贴墙"边界。
    ///   ● <b>RadialBloom</b> —— 雷牢降临 / 重锤猛砸 / 落地震波等事件的加性泛光脉冲。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/>(无活动批, 实体之下): 危险弹幕在其上层 → 不遮挡躲避信息(§6.6)。
    /// 三类 overlay 均**不读 <see cref="Main.screenTarget"/>**, 故不占用全屏后处理名额
    /// (<see cref="ACMShaders.RequestFullscreenSlot"/> 留给真正需要 screenTarget 的 Boss)。
    /// 纯本地视觉, 服务端零绘制, 受 <see cref="MythologyConfig"/> 降级。
    /// </summary>
    public class JiangcenThunderPrisonSystem : ModSystem
    {
        private static Vector2 _center;
        private static float _prisonRadiusWorld;
        private static float _prison;   // 牢笼可见度 0~1
        private static float _storm;    // 雷暴压暗 0~1
        private static bool _phase2;
        private static float _time;
        private static ulong _lastPublishFrame;

        // 事件型泛光通道(取 max, 逐帧衰减)
        private static float _bloom;
        private static Vector2 _bloomCenter;
        private static Vector4 _bloomColor = Vector4.One;

        /// <summary>由 <see cref="Jiangcen"/> 每帧调用, 发布当前雷牢氛围标量（纯本地视觉）。</summary>
        public static void Publish(Vector2 center, float prisonRadiusWorld, float prison, float storm, bool phase2, float time) {
            _center = center;
            _prisonRadiusWorld = prisonRadiusWorld;
            _prison = prison;
            _storm = storm;
            _phase2 = phase2;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        /// <summary>追加一次事件泛光脉冲(取 max 不累加)。供雷牢降临 / 猛砸 / 落地 / 落雷调用。</summary>
        public static void Pulse(Vector2 worldCenter, float strength, Color color) {
            if (Main.dedServ || strength <= _bloom)
                return;
            _bloom = strength;
            _bloomCenter = worldCenter;
            _bloomColor = color.ToVector4();
        }

        public override void OnWorldUnload() {
            _prison = _storm = _bloom = 0f;
            _phase2 = false;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // 发布停止(Boss 消失)后平滑淡出, 避免氛围/牢笼骤断。
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _prison = MathHelper.Lerp(_prison, 0f, 0.1f);
                _storm = MathHelper.Lerp(_storm, 0f, 0.1f);
            }

            DrawStormTint();
            DrawThunderPrison();
            DrawBloom();

            _bloom *= 0.88f;
            if (_bloom < 0.01f)
                _bloom = 0f;
        }

        // ===== ElementalScreenTint: 雷暴压暗底色 =====
        private static void DrawStormTint() {
            if (_storm <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            // 雷狱阶段偶发频闪: 让底色短促提亮(雷闪感), 但不刺眼。
            float flash = 0f;
            if (_phase2) {
                float f = (float)System.Math.Sin(_time * 1.7f) * (float)System.Math.Sin(_time * 5.3f);
                flash = MathHelper.Clamp((f - 0.82f) * 5f, 0f, 1f) * 0.18f;
            }

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_storm, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 上=雷青冷雾, 下=焦黑压暗; 覆盖度保守, 始终能看清致命落雷柱/链电。
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.Lightning.ToVector3(), 0.16f + flash));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(new Vector3(0.02f, 0.02f, 0.05f), 0f));
            fx.Parameters["uVignette"]?.SetValue(0.5f);
            fx.Parameters["uFogScale"]?.SetValue(2.2f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== ArenaRunic(牢笼罩): 可见的环形雷牢 =====
        private static void DrawThunderPrison() {
            if (_prison <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_center, _prisonRadiusWorld,
                out Vector2 uv, out float radiusFrac, out float aspect);

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(MathHelper.Clamp(radiusFrac, 0.2f, 1.1f));
            // 牢笼整体可见度保守, 实体弹幕在其上层 → 不糊住躲避信息。
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_prison, 0f, 1f) * 0.6f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.Lightning.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(0.35f, 0.55f, 0.95f, 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(11f);
            fx.Parameters["uMode"]?.SetValue(1f);   // 牢笼罩(prison-overlay)
            fx.Parameters["uShape"]?.SetValue(0f);  // 圆形, 与 P0 圆形边界雷霆判定一致(逃出半径=被劈)

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== RadialBloom: 事件泛光脉冲 =====
        private static void DrawBloom() {
            if (_bloom <= 0.01f)
                return;
            Effect fx = ACMShaders.RadialBloom;
            if (fx == null)
                return;

            Vector2 uv = (_bloomCenter - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_bloom, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(0.32f + (1f - _bloom) * 0.4f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColor"]?.SetValue(_bloomColor);
            fx.Parameters["uRayCount"]?.SetValue(0f);
            fx.Parameters["uFalloff"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
        }
    }
}
