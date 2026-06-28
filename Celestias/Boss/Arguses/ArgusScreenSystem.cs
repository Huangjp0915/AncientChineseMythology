using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Arguses
{
    /// <summary>
    /// 百目·天目 V2 屏幕氛围系统 (对位 <see cref="Aokins.AokinHeatScreenSystem"/> 等)。
    /// 由 <see cref="Argus.UpdateVisuals"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动, 集中绘制两类
    /// **不占全屏后处理名额**的装饰层 (DrawFullscreenOverlay / DrawScreenSpaceDecalStandalone):
    ///   ● <b>ElementalScreenTint</b> —— 虚空"被注视"压迫染色 (三阶段常驻轻染, 全视之域加强)。
    ///   ● <b>ArenaRunic</b>(法阵) —— 全视之域签名: 以玩家为心的紫蓝虹环地纹 (眼形球阵的场地化预告)。
    /// 昂贵的全屏 screenTarget 折射 (GenericWarp · scrying) 由 <see cref="Argus.PostDraw"/> 单独申请名额绘制。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/> (无活动批): 氛围/地纹在实体之下, 致命弹幕在其上 → 不遮挡躲避信息 (§6.6)。
    /// 纯本地视觉, 服务端零绘制, 受 <see cref="MythologyConfig"/> 降级。
    /// </summary>
    public class ArgusScreenSystem : ModSystem
    {
        private static float _tint;
        private static float _domain;          // 全视之域进度 (驱动 ArenaRunic 虹环)
        private static Vector2 _domainCenter;   // 域中心 (= 玩家)
        private static float _time;
        private static ulong _lastPublishFrame;

        private static readonly Color VoidTint = new(60, 30, 110);
        private static readonly Color VoidDeep = new(10, 6, 28);
        private static readonly Color IrisPurple = new(180, 80, 255);
        private static readonly Color IrisBlue = new(80, 150, 255);

        /// <summary>由 <see cref="Argus"/> 每帧调用, 发布当前氛围标量 (纯本地视觉)。</summary>
        public static void Publish(float tint, float domain, Vector2 domainCenter, float time) {
            _tint = tint;
            _domain = domain;
            _domainCenter = domainCenter;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _tint = _domain = 0f;
            Argus.DomainSignal = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // 发布断流时自然回落 (Boss 消失/暂离)
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _tint = MathHelper.Lerp(_tint, 0f, 0.10f);
                _domain = MathHelper.Lerp(_domain, 0f, 0.12f);
            }

            DrawVoidTint();
            DrawDomainIris();
        }

        // ===== ElementalScreenTint: 虚空"被注视"压迫染色 =====
        private static void DrawVoidTint() {
            if (_tint <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_tint, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 上=幽紫雾, 下=深空墨, 覆盖度保守 — 始终看得清致命箭/光刃
            fx.Parameters["uTint"]?.SetValue(new Vector4(VoidTint.ToVector3(), 0.22f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(VoidDeep.ToVector3(), 0f));
            fx.Parameters["uVignette"]?.SetValue(0.5f);
            fx.Parameters["uFogScale"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== ArenaRunic: 全视之域虹环地纹 (签名) =====
        private static void DrawDomainIris() {
            if (_domain <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_domainCenter, 360f, out Vector2 uv, out float radiusFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(MathHelper.Clamp(radiusFrac, 0.15f, 0.9f));
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_domain, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(IrisPurple.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(IrisBlue.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(10f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }
    }
}
