using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers
{
    /// <summary>
    /// 觉醒冥龙 V2 演出标量中枢 + 非 screenTarget 全屏 overlay 绘制 (着色器工具箱 §A.6, 仿 NetherDragonScreenSystem)。
    ///
    /// 由 <see cref="AwakeningNetherHead"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动:
    ///   ● <b>ElementalScreenTint</b> (nether-fog) —— 冥雾氛围, 每幕递进加深 (巡游→裂隙→吞噬)。
    ///   ● <b>ArenaRunic</b> (rift/vortex tell) —— 次元裂隙门 / 第三幕中央漩涡的向心收口符阵预警 (可读落点)。
    ///   ● <b>RadialBloom</b> (breath/laser/finality bloom) —— 吐息走廊 / 觉醒终末激光帘幕 / 喷发的加性紫泛光。
    ///
    /// 三者皆走 <b>不读 screenTarget</b> 的占位像素 / 噪声载体绘制 → <b>不占全屏后处理名额</b>, 可作廉价叠层 (§C.4#2)。
    /// 昂贵的全屏 screenTarget 扭曲 (GenericWarp · rift/void) 不在本系统, 由
    /// <see cref="AwakeningNetherHead.PostDraw"/> 单独申请唯一全屏名额绘制。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/> (实体之下), 危险弹幕在其上层 → 不遮挡需躲避信息 (§6.6)。
    /// 纯本地视觉, 服务端零绘制, 受 MythologyConfig 降级。
    /// </summary>
    public class AwakeningNetherScreenSystem : ModSystem
    {
        private static float _fog;          // ElementalScreenTint 强度 (冥雾, 每幕加深)
        private static float _bloom;        // RadialBloom 强度 (吐息/激光/喷发)
        private static float _runic;        // ArenaRunic 强度 (裂隙门/漩涡向心收口预警)
        private static Vector2 _bloomCenter;
        private static Vector2 _runicCenter;
        private static float _runicRadius;  // 世界半径 → 着色器
        private static bool _runicLethal;   // true=红色致命落点 false=主题色(幽蓝紫)预备
        private static float _time;
        private static ulong _lastPublishFrame;

        /// <summary>由 AwakeningNetherHead 每帧调用, 发布演出标量 (纯本地视觉)。</summary>
        public static void Publish(float fog, float bloom, Vector2 bloomCenter,
            float runic, Vector2 runicCenter, float runicRadius, bool runicLethal, float time) {
            _fog = fog;
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
            _fog = _bloom = _runic = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 不在场/未发布时平滑淡出, 避免状态残留
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _fog = MathHelper.Lerp(_fog, 0f, 0.05f);
                _bloom = MathHelper.Lerp(_bloom, 0f, 0.15f);
                _runic = MathHelper.Lerp(_runic, 0f, 0.15f);
            }

            DrawNetherFog();
            DrawTell();
            DrawBloom();
        }

        // ===== ElementalScreenTint(冥雾): 每幕递进加深的氛围底色 =====
        private static void DrawNetherFog() {
            if (_fog <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_fog, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.NetherViolet.ToVector3(), 0.5f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(UnderworldField.DecreeColor.ToVector3(), 1f));
            fx.Parameters["uVignette"]?.SetValue(0.45f);
            fx.Parameters["uFogScale"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== ArenaRunic(法阵): 裂隙门 / 中央漩涡的向心收口预警(可读落点) =====
        private static void DrawTell() {
            if (_runic <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_runicCenter, _runicRadius, out Vector2 uv, out float radiusFrac, out float aspect);
            // 预备以主题色(幽蓝紫); 落点将至(_runicLethal)切纯红致命 (§6.1 红=致命)
            Color primary = _runicLethal ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
            Color secondary = _runicLethal ? TelegraphColors.Execution : UnderworldField.DecreeColor;

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_runic, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(12f);
            fx.Parameters["uMode"]?.SetValue(0f);   // 法阵(非牢笼)
            fx.Parameters["uShape"]?.SetValue(0f);  // 圆

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== RadialBloom: 吐息/激光帘幕/终末喷发的紫泛光 =====
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
            fx.Parameters["uRadius"]?.SetValue(0.3f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColor"]?.SetValue(new Vector4(TelegraphColors.NetherViolet.ToVector3(), 1f));
            fx.Parameters["uRayCount"]?.SetValue(10f);
            fx.Parameters["uFalloff"]?.SetValue(2.6f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
        }
    }
}
