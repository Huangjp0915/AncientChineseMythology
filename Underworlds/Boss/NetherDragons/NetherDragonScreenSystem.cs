using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙 V2 演出标量中枢 + 非 screenTarget 全屏 overlay 绘制 (着色器工具箱 §A.6/§A.7)。
    ///
    /// 由 <see cref="NetherDragonHead"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动:
    ///   ● <b>RadialBloom</b> (breath bloom) —— 头部吐息锥喷发瞬间的加性鬼绿泛光。
    ///   ● <b>ArenaRunic</b> (portal/trail tell) —— 传送门出口 / 穿墓出口的向心收口符阵预警(可读落点)。
    ///
    /// 昂贵的全屏 screenTarget 扭曲 (GenericWarp · fog/rift) 不在本系统, 而由
    /// <see cref="NetherDragonHead.PostDraw"/> 单独申请唯一名额绘制 (§C.4#2 性能契约)。
    /// 本系统两个 overlay 都走 <b>不读 screenTarget</b> 的占位像素绘制, 不占全屏名额, 可作廉价第二层。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/> (实体之下), 危险弹幕在其上层 → 不遮挡需躲避信息 (§6.6)。
    /// 纯本地视觉, 服务端零绘制, 受 MythologyConfig 降级。
    /// </summary>
    public class NetherDragonScreenSystem : ModSystem
    {
        private static float _bloom;       // RadialBloom 强度 (吐息喷发)
        private static float _runic;       // ArenaRunic 强度 (出口符阵预警)
        private static Vector2 _bloomCenter;
        private static Vector2 _runicCenter;
        private static float _runicRadius; // 世界半径 → 着色器
        private static bool _runicLethal;  // true=红色致命落点(出口将至) false=主题色预备
        private static float _time;
        private static ulong _lastPublishFrame;

        /// <summary>由 NetherDragonHead 每帧调用, 发布演出标量 (纯本地视觉)。</summary>
        public static void Publish(float bloom, Vector2 bloomCenter,
            float runic, Vector2 runicCenter, float runicRadius, bool runicLethal, float time) {
            _bloom = bloom;
            _bloomCenter = bloomCenter;
            _runic = runic;
            _runicCenter = runicCenter;
            _runicRadius = runicRadius;
            _runicLethal = runicLethal;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _bloom = _runic = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 不在场/未发布时平滑淡出, 避免状态残留
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _bloom = MathHelper.Lerp(_bloom, 0f, 0.15f);
                _runic = MathHelper.Lerp(_runic, 0f, 0.15f);
            }

            DrawOutletRunic();
            DrawBreathBloom();
        }

        // ===== ArenaRunic(法阵): 传送门 / 穿墓出口的向心收口预警(可读落点) =====
        private static void DrawOutletRunic() {
            if (_runic <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_runicCenter, _runicRadius, out Vector2 uv, out float radiusFrac, out float aspect);
            // 收口前以主题色(幽蓝紫)预备; 出口将至(_runicLethal)切纯红致命落点 (§6.1 红=致命)
            Color primary = _runicLethal ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
            Color secondary = _runicLethal ? TelegraphColors.Execution : UnderworldField.DecreeColor;

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_runic, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(11f);
            fx.Parameters["uMode"]?.SetValue(0f);   // 法阵(非牢笼)
            fx.Parameters["uShape"]?.SetValue(0f);  // 圆

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== RadialBloom: 吐息锥喷发的鬼绿泛光 =====
        private static void DrawBreathBloom() {
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
            fx.Parameters["uRadius"]?.SetValue(0.28f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColor"]?.SetValue(new Vector4(TelegraphColors.GhostGreen.ToVector3(), 1f));
            fx.Parameters["uRayCount"]?.SetValue(8f);
            fx.Parameters["uFalloff"]?.SetValue(2.6f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
        }
    }
}
