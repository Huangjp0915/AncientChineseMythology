using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Jiangcens
{
    /// <summary>
    /// 将臣 V3 「雷牢降临」屏幕氛围系统。
    /// 由 <see cref="Jiangcen"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动, 集中绘制非 screenTarget overlay:
    ///   ● <b>ElementalScreenTint</b> —— 雷暴压暗底色(雷青 + 焦黑暗角), 雷狱阶段常驻, 始终能看清弹幕。
    ///   ● <b>ArenaRunic</b>(牢笼罩 uMode=1) —— 可见的环形雷牢; V3 新增<b>失稳通道</b>:
    ///     终章/死亡时牢体闪断 + 色偏血红(雷牢正在崩塌的体感)。
    ///   ● <b>RadialBloom</b> —— 雷牢合拢 / 猛砸 / 落地震波等事件的加性泛光脉冲。
    ///   ● <b>白闪通道</b>(V3 新增) —— <see cref="FlashWhite"/> 全屏白 impact 拍(雷牢合拢 / 死亡天罚),
    ///     纯色矩形 AlphaBlend, 不读 screenTarget, 指数衰减(约 12 帧)。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/>(无活动批, 实体之下): 危险弹幕在其上层 → 不遮挡躲避信息。
    /// 三类 overlay 均**不读 <see cref="Main.screenTarget"/>**, 故不占用全屏后处理名额
    /// (<see cref="ACMShaders.RequestFullscreenSlot"/> 留给真正需要 screenTarget 的 Boss)。
    /// 纯本地视觉, 服务端零绘制, 受 <see cref="MythologyConfig"/> 降级。
    /// </summary>
    public class JiangcenThunderPrisonSystem : ModSystem
    {
        private static Vector2 _center;
        private static float _prisonRadiusWorld;
        private static float _prison;      // 牢笼可见度 0~1
        private static float _storm;       // 雷暴压暗 0~1
        private static float _instability; // 牢体失稳 0~1 (终章/死亡: 闪断+偏红)
        private static bool _phase2;
        private static float _time;
        private static ulong _lastPublishFrame;

        // 事件型泛光通道(取 max, 逐帧衰减)
        private static float _bloom;
        private static Vector2 _bloomCenter;
        private static Vector4 _bloomColor = Vector4.One;

        // 白闪通道(impact 拍, 取 max, 指数衰减)
        private static float _white;

        /// <summary>由 <see cref="Jiangcen"/> 每帧调用, 发布当前雷牢氛围标量（纯本地视觉）。</summary>
        public static void Publish(Vector2 center, float prisonRadiusWorld, float prison, float storm,
            bool phase2, float time, float instability = 0f) {
            _center = center;
            _prisonRadiusWorld = prisonRadiusWorld;
            _prison = prison;
            _storm = storm;
            _phase2 = phase2;
            _time = time;
            _instability = instability;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        /// <summary>追加一次事件泛光脉冲(取 max 不累加)。供雷牢降临 / 猛砸 / 落地 / 落雷调用。</summary>
        public static void Pulse(Vector2 worldCenter, float strength, Color color) {
            if (Main.dedServ || strength <= _bloom)
                return;
            _bloom = strength;
            _bloomCenter = worldCenter;
            _bloomColor = color.ToVector4();
        }

        /// <summary>
        /// 全屏白 impact 拍(取 max 不累加, ~12 帧指数衰减)。
        /// 只留给一场战斗最重的两拍: 雷牢合拢 (~0.75) 与死亡天罚轰顶 (~0.95)。
        /// </summary>
        public static void FlashWhite(float strength) {
            if (Main.dedServ)
                return;
            strength = MathHelper.Clamp(strength, 0f, 0.95f);
            if (strength > _white)
                _white = strength;
        }

        public override void OnWorldUnload() {
            _prison = _storm = _bloom = _white = _instability = 0f;
            _phase2 = false;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // 发布停止(Boss 消失)后平滑淡出, 避免氛围/牢笼骤断。
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _prison = MathHelper.Lerp(_prison, 0f, 0.1f);
                _storm = MathHelper.Lerp(_storm, 0f, 0.1f);
                _instability = MathHelper.Lerp(_instability, 0f, 0.1f);
            }

            DrawStormTint();
            DrawThunderPrison();
            DrawBloom();
            DrawWhiteFlash();

            _bloom *= 0.88f;
            if (_bloom < 0.01f)
                _bloom = 0f;
            _white *= 0.84f;
            if (_white < 0.01f)
                _white = 0f;
        }

        // ===== ElementalScreenTint: 雷暴压暗底色 =====
        private static void DrawStormTint() {
            if (_storm <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            // 雷狱阶段偶发频闪: 让底色短促提亮(雷闪感), 但不刺眼。失稳期更频。
            float flash = 0f;
            if (_phase2) {
                float f = (float)System.Math.Sin(_time * 1.7f) * (float)System.Math.Sin(_time * 5.3f);
                flash = MathHelper.Clamp((f - 0.82f + _instability * 0.25f) * 5f, 0f, 1f) * 0.18f;
            }

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_storm, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 上=雷青冷雾, 下=焦黑压暗; 覆盖度保守, 始终能看清致命落雷柱/链电。失稳时冷雾偏血。
            Vector3 tint = Vector3.Lerp(TelegraphColors.Lightning.ToVector3(),
                JiangcenVFX.CorpseRed.ToVector3(), _instability * 0.45f);
            fx.Parameters["uTint"]?.SetValue(new Vector4(tint, 0.16f + flash));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(new Vector3(0.02f, 0.02f, 0.05f), 0f));
            fx.Parameters["uVignette"]?.SetValue(0.5f);
            fx.Parameters["uFogScale"]?.SetValue(2.2f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== ArenaRunic(牢笼罩): 可见的环形雷牢 (+失稳闪断) =====
        private static void DrawThunderPrison() {
            if (_prison <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            // 失稳闪断: 随 instability 加深, 牢体以不规则节奏整体断亮(将崩塌的体感)
            float instFlick = 1f;
            if (_instability > 0.02f) {
                float n = (float)System.Math.Sin(_time * 23f) * (float)System.Math.Sin(_time * 7.7f + 1.3f);
                instFlick = 1f - MathHelper.Clamp((n - (0.92f - _instability)) * 4f, 0f, 1f) * 0.85f;
            }

            ACMShaders.WorldDecalParams(_center, _prisonRadiusWorld,
                out Vector2 uv, out float radiusFrac, out float aspect);

            Vector3 primary = Vector3.Lerp(TelegraphColors.Lightning.ToVector3(),
                JiangcenVFX.CorpseRed.ToVector3(), _instability * 0.55f);

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(MathHelper.Clamp(radiusFrac, 0.2f, 1.1f));
            // 牢笼整体可见度保守, 实体弹幕在其上层 → 不糊住躲避信息。
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_prison, 0f, 1f) * 0.6f * instFlick);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(primary, 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(0.35f, 0.55f, 0.95f, 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(11f);
            fx.Parameters["uMode"]?.SetValue(1f);   // 牢笼罩(prison-overlay)
            fx.Parameters["uShape"]?.SetValue(0f);  // 圆形, 与边界雷霆判定一致(逃出半径=被劈)

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== RadialBloom: 事件泛光脉冲 =====
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
            fx.Parameters["uRadius"]?.SetValue(0.32f + (1f - _bloom) * 0.4f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColor"]?.SetValue(_bloomColor);
            fx.Parameters["uRayCount"]?.SetValue(0f);
            fx.Parameters["uFalloff"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
        }

        // ===== 白闪: 全屏 impact 拍 (纯色矩形, 无着色器) =====
        private static void DrawWhiteFlash() {
            if (_white <= 0.01f)
                return;
            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            sb.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White * _white);
            sb.End();
        }
    }
}
