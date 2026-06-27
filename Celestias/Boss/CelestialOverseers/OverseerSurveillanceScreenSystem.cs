using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialOverseers
{
    /// <summary>
    /// 天庭观察者 V2 监视/全视屏幕氛围系统（对位 <see cref="Aokins.AokinHeatScreenSystem"/>）。
    /// 由 <see cref="CelestialOverseer"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动, 集中绘制两类非 screenTarget 后处理:
    ///   ● <b>ElementalScreenTint</b> —— 监视压迫暗角(随监视槽升高而收紧, 冷蓝→暖金权柄色; 始终能看清弹幕)。
    ///   ● <b>ArenaRunic</b> —— "全视眼穹/审判庭法阵": 窥视相位扫描环(uMode=0) / 陪审团终局眼穹(uMode=1 dome)。
    /// 昂贵的全屏 screenTarget 扭曲(GenericWarp · rift 主题, 被"扫描"折射)由 <see cref="CelestialOverseer.PostDraw"/>
    /// 单独申请名额绘制; 处决/十字开火加性泛光由 <see cref="CelestialOverseer.PreDraw"/> 经 DrawRadialBloomAt 仲裁。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/>(无活动批): 氛围/地纹位于实体之下, 危险弹幕在其上层 → 不遮挡躲避信息(§6.6)。
    /// 纯本地视觉, 服务端零绘制, 受 MythologyConfig 降级。
    /// </summary>
    public class OverseerSurveillanceScreenSystem : ModSystem
    {
        private static float _vignette;     // 监视压迫暗角 0~1
        private static float _warm;         // 冷蓝→暖金权柄 比例 0~1
        private static float _runic;        // 全视眼穹/审判庭法阵 强度 0~1
        private static float _runicRadius;  // 法阵世界半径(px)
        private static bool _dome;          // true=陪审终局眼穹(dome) false=扫描环
        private static Vector2 _center;
        private static float _time;
        private static ulong _lastPublishFrame;

        /// <summary>由 <see cref="CelestialOverseer"/> 每帧调用, 发布当前监视/全视氛围标量（纯本地视觉）。</summary>
        public static void Publish(Vector2 center, float time, float vignette, float warm, float runic, float runicRadius, bool dome) {
            _center = center;
            _time = time;
            _vignette = vignette;
            _warm = warm;
            _runic = runic;
            _runicRadius = runicRadius;
            _dome = dome;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _vignette = _warm = _runic = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // 发布过期(Boss 消失)时平滑淡出
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _vignette = MathHelper.Lerp(_vignette, 0f, 0.1f);
                _runic = MathHelper.Lerp(_runic, 0f, 0.15f);
            }

            DrawSurveillanceVignette();
            DrawAllSeeingRing();
        }

        // ===== ElementalScreenTint: 监视压迫暗角(冷蓝→暖金, 随槽收紧) =====
        private static void DrawSurveillanceVignette() {
            if (_vignette <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            // 冷钢监视蓝 → 暖金权柄(非红: 纯红仅留给致命审判射线)
            Color cold = new(70, 120, 190);
            Color warm = TelegraphColors.Gold;
            Color tint = Color.Lerp(cold, warm, _warm);

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(0.25f + _vignette * 0.55f, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uTint"]?.SetValue(new Vector4(tint.ToVector3(), 0.12f + _vignette * 0.12f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(new Color(15, 18, 35).ToVector3(), 0f));
            fx.Parameters["uVignette"]?.SetValue(0.35f + _vignette * 0.5f); // 随槽升高暗角收紧
            fx.Parameters["uFogScale"]?.SetValue(2.6f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== ArenaRunic: 全视眼穹(陪审终局 dome) / 窥视扫描环 =====
        private static void DrawAllSeeingRing() {
            if (_runic <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_center, _runicRadius, out Vector2 uv, out float radiusFrac, out float aspect);

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(MathHelper.Clamp(radiusFrac, 0.15f, 0.95f));
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_runic, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.Holy.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(TelegraphColors.Gold.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(11f);
            fx.Parameters["uMode"]?.SetValue(_dome ? 1f : 0f); // dome=陪审眼穹(罩) / 0=扫描法阵环
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }
    }
}
