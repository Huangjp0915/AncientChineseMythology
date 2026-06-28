using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas
{
    /// <summary>
    /// 毗沙门天王 V2 库藏·宝塔屏幕氛围系统（对位 <see cref="Aoshuns.AoshunStormScreenSystem"/> 等）。
    /// 由 <see cref="Vaisravana.PublishScreenState"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动,
    /// 集中绘制三类**非 screenTarget** overlay（不占 <see cref="ACMShaders.RequestFullscreenSlot"/> 名额）:
    ///   ● <b>ElementalScreenTint</b> —— 财神金幕底色（= 库藏开启度, 三阶段/终极宝塔蓄力时加深, 暖金=财气而非危险）。
    ///   ● <b>ArenaRunic</b>(法阵环) —— 终极宝塔（Pagoda Apex）地面金色坛城符文, 随 70 tick 蓄力逐圈点亮的预告地纹。
    ///   ● <b>RadialBloom</b> —— 赐福窃取金闪 / 终极金柱蓄满与发射的财宝泛光（取 max 脉冲, 逐帧衰减）。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/>(无活动批): 氛围/泛光/地纹位于实体之下, 危险弹幕在其上层 → 不遮挡躲避信息(§6.6)。
    /// 全部占位像素 overlay, 纯本地视觉, 服务端零绘制, 受 <see cref="MythologyConfig"/> 降级。
    /// </summary>
    public class VaisravanaTreasureScreenSystem : ModSystem
    {
        private static Vector2 _center;       // Boss 中心(世界)
        private static float _goldTint;       // 库藏金幕强度 0~1
        private static float _bloom;          // 瞬时财宝泛光 0~1（赐福窃取 / 终极金柱）

        // 终极宝塔（Pagoda Apex）地面坛城符文
        private static bool _runeActive;
        private static Vector2 _runeCenter;
        private static float _runeRadius;
        private static float _runeIntensity;  // 蓄力进度 0~1（逐圈点亮）

        private static float _time;
        private static ulong _lastPublishFrame;

        /// <summary>由毗沙门每帧调用, 发布当前库藏金幕氛围标量（纯本地视觉）。</summary>
        public static void Publish(Vector2 center, float goldTint, bool runeActive, Vector2 runeCenter,
            float runeRadius, float runeIntensity, float time) {
            _center = center;
            _goldTint = goldTint;
            _runeActive = runeActive;
            _runeCenter = runeCenter;
            _runeRadius = runeRadius;
            _runeIntensity = runeIntensity;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        /// <summary>瞬时财宝泛光脉冲（取 max, 不累加）—— 赐福窃取金闪 / 终极金柱蓄满发射。</summary>
        public static void PulseBloom(float amount) {
            if (amount > _bloom)
                _bloom = amount;
        }

        public override void OnWorldUnload() {
            _goldTint = _bloom = 0f;
            _runeActive = false;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 失活/不再发布时平滑收尾, 防止氛围残留
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _goldTint = MathHelper.Lerp(_goldTint, 0f, 0.1f);
                _bloom = MathHelper.Lerp(_bloom, 0f, 0.15f);
                _runeActive = false;
            }

            DrawTreasuryGoldVeil();
            DrawApexMandalaRune();
            DrawTreasuryBloom();
        }

        // ===== ElementalScreenTint: 财神金幕底色 = 库藏开启度 =====
        private static void DrawTreasuryGoldVeil() {
            if (_goldTint <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            // 暖金财气：覆盖度保守, 始终能看清致命金柱/收缩金环（金=财气氛围, 非致命）
            float coverage = 0.10f + _goldTint * 0.26f;
            float vignette = 0.30f + _goldTint * 0.16f;

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_goldTint, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 上=琉璃金宫幕, 下=暖金库藏（近暖橙）; 整体偏亮暖, 营造"库藏开启"金光笼罩
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.Gold.ToVector3() * 0.55f, coverage));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(new Vector3(0.30f, 0.20f, 0.06f), 0f));
            fx.Parameters["uVignette"]?.SetValue(vignette);
            fx.Parameters["uFogScale"]?.SetValue(2.6f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== ArenaRunic: 终极宝塔地面金色坛城符文（70 tick 蓄力地纹预告）=====
        private static void DrawApexMandalaRune() {
            if (!_runeActive || _runeRadius < 16f || _runeIntensity <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_runeCenter, _runeRadius, out Vector2 uv, out float radiusFrac, out float aspect);

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(MathHelper.Clamp(radiusFrac, 0.05f, 0.95f));
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_runeIntensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 暖金坛城（财神镇压地纹）。双色皆金系: 内琉璃金 / 外暖金边, 暖金=蓄力危险预告
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.Gold.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(VaisravanaHelper.TowerGold.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(12f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== RadialBloom: 赐福窃取 / 终极金柱泛光 =====
        private static void DrawTreasuryBloom() {
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
            fx.Parameters["uRadius"]?.SetValue(0.32f + (1f - _bloom) * 0.4f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColor"]?.SetValue(new Vector4(TelegraphColors.Gold.ToVector3(), 1f));
            fx.Parameters["uRayCount"]?.SetValue(12f);
            fx.Parameters["uFalloff"]?.SetValue(2.3f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);

            // 逐帧衰减(由 PulseBloom 取 max 续命)
            _bloom = MathHelper.Lerp(_bloom, 0f, 0.12f);
        }
    }
}
