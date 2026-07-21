using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.TribulationCloud
{
    /// <summary>
    /// 劫云渡劫·屏幕氛围系统 —— 两个通道:
    /// <list type="bullet">
    ///   <item><b>风暴压暗</b> (ElementalScreenTint): 乌云压顶的沉郁染屏, 颜色按三色主题/结算金色传参,
    ///   由 <see cref="TribulationCloudBase"/> 每帧 <see cref="Publish"/> (同帧多源取 max)。</item>
    ///   <item><b>雷光白闪</b> (<see cref="Flash"/>): 轰落瞬间的全屏亮白一帧, ×0.72/f 指数衰减;
    ///   峰值钳制 ≤0.55 (光敏保护), 纯白 quad 叠加, 无着色器。</item>
    /// </list>
    /// 均在 <see cref="PostDrawTiles"/> (无活动批, 实体之下) 绘制 —— <b>不读 screenTarget, 不占全屏后处理名额</b>
    /// (§C.4#2 廉价装饰层), 与任何全屏后处理可共存; 覆盖保守, 始终看得清落雷预警 (§6.6)。
    /// 纯本地视觉: 服务端零绘制; 染屏受 MythologyConfig 降级 (白闪为节拍反馈, 保留但受钳制)。
    /// </summary>
    public class TribulationScreenSystem : ModSystem
    {
        private static float _target;
        private static float _draw;
        private static Vector3 _tint = new(0.3f, 0.33f, 0.5f);
        private static ulong _frame;
        private static ulong _lastPublish;
        private static float _flash;

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

        /// <summary>轰落白闪: 全屏亮白一瞬 (取 max 不累加), ×0.72/f 衰减, 峰值钳制 0.55。</summary>
        public static void Flash(float amount) {
            if (Main.dedServ)
                return;
            amount = MathHelper.Clamp(amount, 0f, 0.55f);
            if (amount > _flash)
                _flash = amount;
        }

        public override void OnWorldUnload() {
            _target = _draw = _flash = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;

            DrawStormTint();
            DrawLightningFlash();
        }

        private static void DrawStormTint() {
            if (!MythologyConfig.FullscreenShadersEnabled)
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

        private static void DrawLightningFlash() {
            if (_flash <= 0.012f) {
                _flash = 0f;
                return;
            }
            _flash *= 0.72f;

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            sb.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                Color.White * _flash);
            sb.End();
        }
    }
}
