using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Hoqings
{
    /// <summary>
    /// 后卿 V3「万鬼夜行」演出标量中枢 + 非 screenTarget 全屏 overlay 绘制。
    ///
    /// 由 <see cref="Hoqing"/> 每帧发布两组 0~1 标量：
    ///   ● <see cref="Publish"/> —— 幕三祭坛体系:
    ///     ArenaRunic ×4 四祭坛地纹（随幕三推进累积成鬼域阵）、
    ///     BeamGrad 祭坛间「疫气经络」尸火光带、
    ///     RadialBloom 当前蓄力祭坛辉光（扇=赤橙致命 / 360=鬼绿）。
    ///   ● <see cref="PublishGate"/> —— 鬼门（专属着色器 HoqingGhostGate）:
    ///     入场门缝 / 幕三大招「鬼门开」/ 死亡演出「鬼门收葬」共用一层 decal，
    ///     uOpen 控开度、uFlash 死亡白闪。
    ///
    /// 昂贵的全屏 screenTarget 雾扭曲 (GenericWarp · fog) 不在本系统, 由 Hoqing.PostDraw
    /// 单独申请本帧唯一全屏名额绘制。本系统各层 overlay 都不读 screenTarget, 不占全屏名额。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/> (实体之下), 危险弹幕在其上层 → 不遮挡需躲避信息。
    /// 纯本地视觉, 服务端零绘制, 受 MythologyConfig 降级。
    /// </summary>
    public class HoqingScreenSystem : ModSystem
    {
        private static Vector2 _arenaCenter;
        private static float _altarRing;        // 祭坛环世界半径 (与 Hoqing.GetAltarPos 一致)
        private static float _plagueAccum;      // 0~1 累积主控 (地纹 + 经络 强度)
        private static int _activeAltar;        // 0..3 当前蓄力祭坛
        private static float _channelGlow;      // 0~1 当前祭坛蓄力进度
        private static bool _channelLethal;     // true=扇形(赤橙致命) false=360(鬼绿)
        private static float _time;
        private static ulong _lastPublishFrame;

        private static Vector2 _gateCenter;     // 鬼门世界坐标
        private static float _gateHalfH;        // 鬼门世界半高(px)
        private static float _gateOpen;         // 0~1 开度
        private static float _gateFlash;        // 0~1 死亡白闪
        private static ulong _lastGateFrame;

        private const float AltarDecalRadius = 230f; // 单个祭坛地纹的世界半径

        //专属着色器: 静态缓存 (不注册 ACMShaders)
        private static Asset<Effect> gateFxRef;
        private static Effect GateFX {
            get {
                if (Main.dedServ) {
                    return null;
                }
                gateFxRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/HoqingGhostGate", AssetRequestMode.ImmediateLoad);
                return gateFxRef?.Value;
            }
        }

        /// <summary>由 Hoqing 每帧 (幕三) 调用, 发布祭坛演出标量 (纯本地视觉, 各端各算)。</summary>
        public static void Publish(Vector2 arenaCenter, float altarRing, float plagueAccum,
            int activeAltar, float channelGlow, bool channelLethal, float time) {
            _arenaCenter = arenaCenter;
            _altarRing = altarRing;
            _plagueAccum = plagueAccum;
            _activeAltar = activeAltar;
            _channelGlow = channelGlow;
            _channelLethal = channelLethal;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        /// <summary>由 Hoqing 每帧调用, 发布鬼门标量 (入场缝隙 / 大招 / 死亡演出)。</summary>
        public static void PublishGate(Vector2 gateCenter, float gateHalfHeight, float open, float flash) {
            _gateCenter = gateCenter;
            _gateHalfH = gateHalfHeight;
            _gateOpen = open;
            _gateFlash = flash;
            _lastGateFrame = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _plagueAccum = 0f;
            _channelGlow = 0f;
            _gateOpen = 0f;
            _gateFlash = 0f;
        }

        private static Vector2 AltarPos(int index) =>
            _arenaCenter + (MathHelper.PiOver2 * index + MathHelper.PiOver4).ToRotationVector2() * _altarRing;

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 不在场/未发布时平滑淡出, 避免状态残留
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _plagueAccum = MathHelper.Lerp(_plagueAccum, 0f, 0.12f);
                _channelGlow = MathHelper.Lerp(_channelGlow, 0f, 0.15f);
            }
            if (Main.GameUpdateCount - _lastGateFrame > 2) {
                _gateOpen = MathHelper.Lerp(_gateOpen, 0f, 0.12f);
                _gateFlash = 0f;
            }

            if (_plagueAccum > 0.01f || _channelGlow > 0.01f) {
                DrawAltarDecals();
                DrawLeyLines();
                DrawChannelBloom();
            }

            if (_gateOpen > 0.015f || _gateFlash > 0.02f) {
                DrawGhostGate();
            }
        }

        // ===== ArenaRunic ×4: 四祭坛地纹, 幕三累积成鬼域阵 =====
        private static void DrawAltarDecals() {
            if (_plagueAccum <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            for (int i = 0; i < 4; i++) {
                bool lethal = i % 2 == 0;
                // 当前蓄力祭坛额外随蓄力提亮; 其余维持累积基底
                float baseI = _plagueAccum * 0.7f;
                float intensity = i == _activeAltar ? MathHelper.Clamp(baseI + _channelGlow * 0.5f, 0f, 1f) : baseI;
                if (intensity <= 0.01f)
                    continue;

                ACMShaders.WorldDecalParams(AltarPos(i), AltarDecalRadius, out Vector2 uv, out float radiusFrac, out float aspect);
                Color primary = lethal ? TelegraphColors.Flame : TelegraphColors.GhostGreen;
                Color secondary = TelegraphColors.NetherViolet;

                fx.Parameters["uTime"]?.SetValue(_time + i * 1.7f);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(radiusFrac);
                fx.Parameters["uIntensity"]?.SetValue(intensity);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
                fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
                fx.Parameters["uRuneFreq"]?.SetValue(10f);
                fx.Parameters["uMode"]?.SetValue(0f);   // 法阵地纹
                fx.Parameters["uShape"]?.SetValue(0f);  // 圆

                ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
            }
        }

        // ===== BeamGrad: 祭坛间「疫气经络」尸火光带 (环连 0-1-2-3-0) =====
        private static void DrawLeyLines() {
            if (_plagueAccum <= 0.02f)
                return;
            if (ACMShaders.BeamGrad == null)
                return;

            // 经络强度: 累积越高越亮; 预告期暗经络 → 高潮点亮
            float ley = _plagueAccum * _plagueAccum; // 偏后段加亮
            Color core = TelegraphColors.GhostGreen;
            Color edge = TelegraphColors.NetherViolet;

            SpriteBatch sb = Main.spriteBatch;
            ACMShaders.RestoreDefaultBatch(sb); // 起一个活动批供 DrawBeam 的首个 End 消费
            for (int i = 0; i < 4; i++) {
                Vector2 a = AltarPos(i);
                Vector2 b = AltarPos((i + 1) % 4);
                float halfWidth = 14f + 10f * _channelGlow;
                ACMShaders.DrawBeam(a, b, halfWidth, core, edge, ley,
                    flowSpeed: 1.1f, flowScale: 2.4f, coreSharp: 2.0f);
            }
            //大招撕门期: 四经络额外汇入门心 (gateOpen 驱动)
            if (_gateOpen > 0.05f && Main.GameUpdateCount - _lastGateFrame <= 2) {
                for (int i = 0; i < 4; i++) {
                    ACMShaders.DrawBeam(AltarPos(i), _gateCenter, 8f + 8f * _gateOpen, core, edge,
                        ley * _gateOpen * 0.8f, flowSpeed: 1.8f, flowScale: 2.0f, coreSharp: 2.2f);
                }
            }
            sb.End(); // 收掉最后一次 DrawBeam 的 RestoreDefaultBatch
        }

        // ===== RadialBloom: 当前蓄力祭坛辉光 (不读 screenTarget, 不占全屏名额) =====
        private static void DrawChannelBloom() {
            if (_channelGlow <= 0.02f)
                return;
            Effect fx = ACMShaders.RadialBloom;
            if (fx == null)
                return;

            Vector2 center = AltarPos(_activeAltar);
            Vector2 uv = (center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            Color glow = _channelLethal ? TelegraphColors.Flame : TelegraphColors.GhostGreen;

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_channelGlow, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(0.14f + 0.05f * _channelGlow);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColor"]?.SetValue(new Vector4(glow.ToVector3(), 1f));
            fx.Parameters["uRayCount"]?.SetValue(_channelLethal ? 12f : 0f);
            fx.Parameters["uFalloff"]?.SetValue(2.6f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
        }

        // ===== HoqingGhostGate: 鬼门 decal (专属着色器, 满屏噪声载体) =====
        private static void DrawGhostGate() {
            Effect fx = GateFX;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            ACMShaders.WorldDecalParams(_gateCenter, _gateHalfH, out Vector2 uv, out float halfHFrac, out float aspect);

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(MathHelper.Max(_gateOpen * 1.4f, _gateFlash), 0f, 1f));
            fx.Parameters["uOpen"]?.SetValue(MathHelper.Clamp(_gateOpen, 0f, 1f));
            fx.Parameters["uHalfHeight"]?.SetValue(halfHFrac);
            fx.Parameters["uColorEdge"]?.SetValue(TelegraphColors.GhostGreen.ToVector4());
            fx.Parameters["uColorDeep"]?.SetValue(TelegraphColors.NetherViolet.ToVector4());
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(_gateFlash, 0f, 1f));

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);
            sb.Draw(noise, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
            sb.End();
        }
    }
}
