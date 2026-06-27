using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Hoqings
{
    /// <summary>
    /// 后卿 V2「万鬼夜行」演出标量中枢 + 非 screenTarget 全屏 overlay 绘制 (着色器工具箱 §A.6)。
    ///
    /// 由 <see cref="Hoqing.AI"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动幕三高潮:
    ///   ● <b>ArenaRunic</b> ×4 —— 四祭坛地纹法阵, 随幕三推进**累积**为成型鬼域阵 (疫源场地化)。
    ///   ● <b>BeamGrad (DrawBeam)</b> —— 祭坛之间相连的「疫气经络」尸火光带 (全场记忆点)。
    ///   ● <b>RadialBloom</b> —— 当前蓄力祭坛的加性辉光 (扇=赤橙致命 / 360=鬼绿)。
    ///
    /// 昂贵的全屏 screenTarget 雾扭曲 (GenericWarp · fog) 不在本系统, 由 <see cref="Hoqing.PostDraw"/>
    /// 单独申请本帧唯一全屏名额绘制 (§C.4#2 性能契约)。本系统三层 overlay 都<b>不读 screenTarget</b>,
    /// 不占全屏名额, 作廉价第二层。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/> (实体之下), 危险弹幕在其上层 → 不遮挡需躲避信息 (§6.6)。
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

        private const float AltarDecalRadius = 230f; // 单个祭坛地纹的世界半径

        /// <summary>由 Hoqing 每帧 (幕三) 调用, 发布演出标量 (纯本地视觉, 各端各算)。</summary>
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

        public override void OnWorldUnload() {
            _plagueAccum = 0f;
            _channelGlow = 0f;
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

            if (_plagueAccum <= 0.01f && _channelGlow <= 0.01f)
                return;

            DrawAltarDecals();
            DrawLeyLines();
            DrawChannelBloom();
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
    }
}
