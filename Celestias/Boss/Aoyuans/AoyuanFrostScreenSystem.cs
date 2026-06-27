using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// 敖闰 V2 霜冻屏幕氛围系统（着色器验证层）。
    /// 由 <see cref="Aoyuan"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动, 集中绘制三类非 screenTarget 后处理:
    ///   ● <b>ElementalScreenTint</b> —— 二阶段常驻的霜白氛围底色(随绝对零度蓄力加浓)。
    ///   ● <b>RadialBloom</b> —— 绝对零度·放射冻结释放瞬间的加性冻爆泛光。
    ///   ● <b>ArenaRunic</b>(法阵模式) —— 蓄力期向心收口的霜冻法阵地纹(收口=即将全屏冻结的可读预警)。
    /// 昂贵的全屏 screenTarget 扭曲(GenericWarp frost)由 <see cref="Aoyuan.PostDraw"/> 单独申请名额绘制。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/>(无活动批): 氛围/泛光/地纹位于实体之下,
    /// 危险弹幕在其上层绘制 → 不遮挡需躲避信息(§6.6)。纯本地视觉, 服务端零绘制, 受 MythologyConfig 降级。
    /// </summary>
    public class AoyuanFrostScreenSystem : ModSystem
    {
        // 由 Boss 每帧发布的 0~1 标量(纯本地视觉)
        private static float _tint;       // ElementalScreenTint 强度
        private static float _bloom;      // RadialBloom 强度(冻爆)
        private static float _runic;      // ArenaRunic 法阵强度
        private static Vector2 _center;   // 世界坐标中心(Boss)
        private static float _time;       // 着色器时间(秒)
        private static ulong _lastPublishFrame;

        /// <summary>由 Aoyuan 每帧调用, 发布当前霜冻氛围标量(纯本地视觉)。</summary>
        public static void Publish(Vector2 center, float tint, float bloom, float runic, float time) {
            _center = center;
            _tint = tint;
            _bloom = bloom;
            _runic = runic;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _tint = _bloom = _runic = 0f;
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
                _runic = MathHelper.Lerp(_runic, 0f, 0.15f);
            }

            DrawAmbientTint();
            DrawArenaRunic();
            DrawFreezeBloom();
        }

        // ===== ElementalScreenTint: 二阶段霜白氛围底色 =====
        private static void DrawAmbientTint() {
            if (_tint <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_tint, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 上=冰白霜雾, 下=深冰蓝压暗; 覆盖度刻意保守, 始终能看清弹幕
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.Frost.ToVector3(), 0.30f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(TelegraphColors.DeepFrost.ToVector3(), 0f));
            fx.Parameters["uVignette"]?.SetValue(0.45f);
            fx.Parameters["uFogScale"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== ArenaRunic(法阵): 蓄力向心收口的霜冻地纹预警 =====
        private static void DrawArenaRunic() {
            if (_runic <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            Vector2 uv = (_center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(0.5f);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_runic, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.Frost.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(TelegraphColors.IceWhite.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(10f);
            fx.Parameters["uMode"]?.SetValue(0f);

            // V2: 自管批次的屏幕空间地纹绘制走共享 DrawScreenSpaceDecalStandalone (替代手抄 Begin/Draw/End)。
            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== RadialBloom: 绝对零度释放冻爆泛光 =====
        private static void DrawFreezeBloom() {
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
            fx.Parameters["uRadius"]?.SetValue(0.55f + (1f - _bloom) * 0.5f); // 冻爆向外扩张
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColor"]?.SetValue(new Vector4(TelegraphColors.IceWhite.ToVector3(), 1f));
            fx.Parameters["uRayCount"]?.SetValue(0f);
            fx.Parameters["uFalloff"]?.SetValue(2.2f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
        }
    }
}
