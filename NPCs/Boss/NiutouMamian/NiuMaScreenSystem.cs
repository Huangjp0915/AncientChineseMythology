using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.NiutouMamian
{
    /// <summary>
    /// 牛头马面 V2 地府氛围染屏 (ElementalScreenTint, 幽蓝紫鬼绿)。
    /// 由 <see cref="NiuTou"/> / <see cref="MaMian"/> 每帧 <see cref="Publish"/> 一个 0~1 标量驱动 (同帧取 max);
    /// 在 <see cref="PostDrawTiles"/>(无活动批, 实体之下) 绘制 —— <b>不读 screenTarget, 不占全屏后处理名额</b>
    /// (§C.4#2 廉价装饰层), 故与任何全屏后处理可共存。覆盖度保守, 始终看得清弹幕 (§6.6)。
    /// 纯本地视觉: 服务端零绘制, 受 MythologyConfig 降级。
    /// </summary>
    public class NiuMaScreenSystem : ModSystem
    {
        private static float _target;       // 本帧目标强度 (取 max)
        private static float _draw;          // 平滑后的实际绘制强度
        private static ulong _frame;         // 本帧标识 (用于同帧取 max)
        private static ulong _lastPublish;   // 最近发布帧 (用于淡出)

        /// <summary>由 Boss 每帧调用, 发布地府氛围强度 (同帧多源取 max)。center 仅语义对齐, 全屏 overlay 不取中心。</summary>
        public static void Publish(Vector2 center, float intensity) {
            if (Main.dedServ)
                return;
            if (Main.GameUpdateCount != _frame) {
                _frame = Main.GameUpdateCount;
                _target = 0f;
            }
            if (intensity > _target)
                _target = intensity;
            _lastPublish = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _target = _draw = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu || !MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 不在场时平滑淡出
            float aim = (Main.GameUpdateCount - _lastPublish > 2) ? 0f : _target;
            _draw = MathHelper.Lerp(_draw, aim, aim > _draw ? 0.04f : 0.08f);
            if (_draw <= 0.01f)
                return;

            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_draw, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 上=幽蓝紫鬼气, 下=鬼绿压暗; 覆盖度保守
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.NetherViolet.ToVector3(), 0.26f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(TelegraphColors.GhostGreen.ToVector3() * 0.4f, 0f));
            fx.Parameters["uVignette"]?.SetValue(0.42f);
            fx.Parameters["uFogScale"]?.SetValue(2.2f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }
    }
}
