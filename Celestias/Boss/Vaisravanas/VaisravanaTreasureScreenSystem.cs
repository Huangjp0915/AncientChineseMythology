using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas
{
    /// <summary>
    /// 毗沙门天王 V3 库藏·宝塔屏幕氛围系统。
    /// 由 <see cref="Vaisravana.PublishScreenState"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动,
    /// 集中绘制四类 overlay（前三类**非 screenTarget**, 不占 <see cref="ACMShaders.RequestFullscreenSlot"/> 名额）:
    ///   ● <b>ElementalScreenTint</b> —— 财神金幕底色（= 库藏开启度, 蓄力/三阶段加深, 暖金=财气而非危险）。
    ///   ● <b>VaisravanaMandala</b>(专属佛纹坛城) —— 大招/演出的地面金色坛城, uReveal 逐圈点亮的预告地纹。
    ///   ● <b>RadialBloom</b> —— 赐福窃取金闪 / 终极金柱蓄满与发射的财宝泛光（取 max 脉冲, 逐帧衰减）。
    ///   ● <b>白闪 impact frame</b> —— 死亡终爆/换阶段爆点的单帧过曝白（PostDrawInterface 纯像素, 一次性脉冲）。
    ///
    /// 前三类绘制位于 <see cref="PostDrawTiles"/>(无活动批): 氛围/泛光/地纹位于实体之下,
    /// 危险弹幕在其上层 → 不遮挡躲避信息。全部纯本地视觉, 服务端零绘制, 受 <see cref="MythologyConfig"/> 降级。
    /// </summary>
    public class VaisravanaTreasureScreenSystem : ModSystem
    {
        private static Vector2 _center;       // Boss 中心(世界)
        private static float _goldTint;       // 库藏金幕强度 0~1
        private static float _bloom;          // 瞬时财宝泛光 0~1（赐福窃取 / 终极金柱）
        private static float _whiteFlash;     // 白闪 impact frame 0~1（死亡终爆, 快衰减）

        // 坛城法阵（专属 VaisravanaMandala 着色器）
        private static bool _runeActive;
        private static Vector2 _runeCenter;
        private static float _runeRadius;
        private static float _runeIntensity;  // 总亮度 0~1
        private static float _runeReveal;     // 0~1 由内向外逐圈点亮（蓄力语法）

        private static float _time;
        private static ulong _lastPublishFrame;

        /// <summary>由毗沙门每帧调用, 发布当前库藏金幕氛围标量（纯本地视觉）。</summary>
        public static void Publish(Vector2 center, float goldTint, bool runeActive, Vector2 runeCenter,
            float runeRadius, float runeIntensity, float runeReveal, float time) {
            _center = center;
            _goldTint = goldTint;
            _runeActive = runeActive;
            _runeCenter = runeCenter;
            _runeRadius = runeRadius;
            _runeIntensity = runeIntensity;
            _runeReveal = runeReveal;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        /// <summary>瞬时财宝泛光脉冲（取 max, 不累加）—— 赐福窃取金闪 / 终极金柱蓄满发射。</summary>
        public static void PulseBloom(float amount) {
            if (amount > _bloom)
                _bloom = amount;
        }

        /// <summary>
        /// 白闪 impact frame 脉冲（取 max）。1f 档只留给死亡终爆——每场战斗唯一一次;
        /// 换阶段爆点用 ≤0.35f 的低档。
        /// </summary>
        public static void PulseWhiteFlash(float amount) {
            if (amount > _whiteFlash)
                _whiteFlash = amount;
        }

        public override void OnWorldUnload() {
            _goldTint = _bloom = _whiteFlash = 0f;
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
            DrawMandalaRune();
            DrawTreasuryBloom();
        }

        /// <summary>白闪 impact frame：覆于一切之上的单帧过曝白（UI 层, 10 帧内衰竭）。</summary>
        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            if (Main.dedServ || Main.gameMenu || _whiteFlash <= 0.02f)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled) {
                _whiteFlash = 0f;
                return;
            }

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                Color.White * MathHelper.Clamp(_whiteFlash, 0f, 1f) * 0.92f);

            _whiteFlash *= 0.80f; // 快速衰竭：一次冲击帧，不是持续白屏
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

        // ===== VaisravanaMandala: 专属佛纹坛城（蓄力逐圈点亮的地纹预告）=====
        private static void DrawMandalaRune() {
            if (!_runeActive || _runeRadius < 16f || _runeIntensity <= 0.01f)
                return;

            VaisravanaHelper.DrawMandalaStandalone(_runeCenter, _runeRadius,
                MathHelper.Clamp(_runeIntensity, 0f, 1f), _runeReveal, _time * 0.35f);
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
