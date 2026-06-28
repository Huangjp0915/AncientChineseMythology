using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Yingous
{
    /// <summary>
    /// 赢勾 V2 屏幕氛围演出系统 (廉价 overlay 层)。
    /// 由 <see cref="Yingou"/> 每帧 <see cref="Publish"/> 一个 0~1 标量, 在 <see cref="PostDrawTiles"/>
    /// (实体之下 → 危险弹幕在其上层, 不遮挡躲避信息, §6.6) 绘制一层<b>不读 screenTarget</b> 的
    /// <see cref="ACMShaders.ElementalScreenTint"/> 冥刃染屏 (不占全屏后处理名额, toolkit §C.4#2)。
    /// 仅在大刀地狱 / 环形散射 / 狂暴冲刺高潮渐浓, 覆盖度刻意保守 (红只留给致命预警)。
    /// 纯本地视觉, 服务端零绘制, 受 <see cref="MythologyConfig"/> 降级。
    /// </summary>
    public class YingouScreenSystem : ModSystem
    {
        // 冥刃氛围色: 幽紫 (主) + 暗赤底 (低处), 与天幕玄青↔赤色板呼应
        private static readonly Color BladeNether = TelegraphColors.NetherViolet;
        private static readonly Color BladeDeep = new(60, 10, 30);

        private static float _tint;     // 当前已平滑强度
        private static float _target;   // Boss 发布的目标强度
        private static float _time;
        private static ulong _lastPublishFrame;

        /// <summary>由 Boss 每帧调用, 发布冥刃染屏目标强度 (纯本地视觉)。</summary>
        public static void Publish(float tint, float time) {
            _target = MathHelper.Clamp(tint, 0f, 1f);
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _tint = 0f;
            _target = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 离场/未发布时平滑淡出, 避免残留
            if (Main.GameUpdateCount - _lastPublishFrame > 2)
                _target = 0f;
            _tint = MathHelper.Lerp(_tint, _target, 0.08f);
            if (_tint <= 0.01f)
                return;

            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_tint, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 覆盖度保守 (≤0.2): 始终能看清弹幕与红色致命预警
            fx.Parameters["uTint"]?.SetValue(new Vector4(BladeNether.ToVector3(), 0.20f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(BladeDeep.ToVector3(), 0f));
            fx.Parameters["uVignette"]?.SetValue(0.42f);
            fx.Parameters["uFogScale"]?.SetValue(2.0f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }
    }
}
