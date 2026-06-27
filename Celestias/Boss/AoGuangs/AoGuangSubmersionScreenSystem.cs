using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    /// <summary>
    /// 敖广 V2「海之沉浸」屏幕氛围系统（着色器验证层）。
    /// 由 <see cref="AoGuang"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动, 集中绘制两类非 screenTarget overlay:
    ///   ● <b>ElementalScreenTint</b> —— 潮汐氛围底色(水位叙事: P1 涨潮浅蓝 → P2 没顶 → P3 深渊, 越深越浓)。
    ///   ● <b>RadialBloom</b> —— 相变/大招潮涌瞬间的加性水爆泛光。
    /// 昂贵的全屏 screenTarget 折射(GenericWarp refraction)由 <see cref="AoGuang.PostDraw"/> 单独申请名额绘制。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/>(无活动批): 氛围/泛光位于实体与危险弹幕之下 → 不遮挡需躲避信息(§6.6)。
    /// 纯本地视觉, 服务端零绘制, 受 MythologyConfig 降级。
    /// </summary>
    public class AoGuangSubmersionScreenSystem : ModSystem
    {
        private static float _tint;       // ElementalScreenTint 强度 (潮汐底色)
        private static float _bloom;      // RadialBloom 强度 (潮涌)
        private static Vector2 _center;   // 世界坐标中心 (Boss)
        private static float _time;       // 着色器时间(秒)
        private static ulong _lastPublishFrame;

        /// <summary>由 AoGuang 每帧调用, 发布当前潮汐氛围标量(纯本地视觉)。</summary>
        public static void Publish(Vector2 center, float tint, float bloom, float time) {
            _center = center;
            _tint = tint;
            _bloom = bloom;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _tint = _bloom = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 不在场/未发布时平滑淡出, 避免状态残留
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _tint = MathHelper.Lerp(_tint, 0f, 0.1f);
                _bloom = MathHelper.Lerp(_bloom, 0f, 0.15f);
            }

            DrawTidalTint();
            DrawSurgeBloom();
        }

        // ===== ElementalScreenTint: 潮汐氛围底色 (上=海面水蓝, 下=深海蓝压暗) =====
        private static void DrawTidalTint() {
            if (_tint <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_tint, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 上=海面水光, 下=深海蓝压暗; 覆盖度保守, 始终能看清弹幕
            fx.Parameters["uTint"]?.SetValue(new Vector4(AoGuangHelper.OceanTeal.ToVector3(), 0.26f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(AoGuangHelper.DeepSeaBlue.ToVector3(), 0f));
            fx.Parameters["uVignette"]?.SetValue(0.4f);
            fx.Parameters["uFogScale"]?.SetValue(2.0f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== RadialBloom: 相变/大招潮涌水爆泛光 =====
        private static void DrawSurgeBloom() {
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
            fx.Parameters["uRadius"]?.SetValue(0.45f + (1f - _bloom) * 0.5f); // 潮涌向外扩张
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColor"]?.SetValue(new Vector4(AoGuangHelper.WaterGlow.ToVector3(), 1f));
            fx.Parameters["uRayCount"]?.SetValue(0f);
            fx.Parameters["uFalloff"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
        }
    }
}
