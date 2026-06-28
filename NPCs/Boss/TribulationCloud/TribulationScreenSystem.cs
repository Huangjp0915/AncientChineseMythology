using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.TribulationCloud
{
    /// <summary>
    /// 劫云渡劫·风暴压暗氛围 (ElementalScreenTint) —— 乌云压顶的沉郁染屏, 颜色按三色主题传参。
    /// 由 <see cref="TribulationCloudBase"/> 每帧 <see cref="Publish"/> (同帧多源取 max)。
    /// 在 <see cref="PostDrawTiles"/> (无活动批, 实体之下) 绘制 —— <b>不读 screenTarget, 不占全屏后处理名额</b>
    /// (§C.4#2 廉价装饰层), 与任何全屏后处理可共存; 覆盖保守, 始终看得清落雷预警 (§6.6)。
    /// 纯本地视觉: 服务端零绘制, 受 MythologyConfig 降级。
    /// </summary>
    public class TribulationScreenSystem : ModSystem
    {
        private static float _target;
        private static float _draw;
        private static Vector3 _tint = new Vector3(0.3f, 0.33f, 0.5f);
        private static ulong _frame;
        private static ulong _lastPublish;

        /// <summary>劫云每帧调用: 发布风暴压暗强度与主题色 (同帧多源取 max)。</summary>
        public static void Publish(Color theme, float intensity) {
            if (Main.dedServ)
                return;
            if (Main.GameUpdateCount != _frame) {
                _frame = Main.GameUpdateCount;
                _target = 0f;
            }
            if (intensity > _target) {
                _target = intensity;
                _tint = theme.ToVector3();
            }
            _lastPublish = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _target = _draw = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu || !MythologyConfig.FullscreenShadersEnabled)
                return;

            float aim = (Main.GameUpdateCount - _lastPublish > 2) ? 0f : _target;
            _draw = MathHelper.Lerp(_draw, aim, aim > _draw ? 0.03f : 0.06f);
            if (_draw <= 0.01f)
                return;

            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_draw, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 上=主题风暴色, 下=压暗 (乌云压顶); 覆盖保守
            fx.Parameters["uTint"]?.SetValue(new Vector4(_tint, 0.30f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(_tint * 0.18f, 0f));
            fx.Parameters["uVignette"]?.SetValue(0.5f);
            fx.Parameters["uFogScale"]?.SetValue(2.6f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }
    }
}
