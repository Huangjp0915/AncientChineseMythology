using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialOverseers
{
    /// <summary>
    /// 天庭监察者 V3 监视/全视屏幕氛围系统。
    /// 由 <see cref="CelestialOverseer"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动, 集中绘制三类非 screenTarget 效果:
    ///   ● <b>ElementalScreenTint</b> —— 监视压迫暗角(随监视槽升高而收紧, 冷蓝→暖金权柄色; 始终能看清弹幕)。
    ///   ● <b>ArenaRunic</b> —— "全视眼穹/审判庭法阵": 窥视相位扫描环(uMode=0) / 陪审团终局眼穹(uMode=1 dome)。
    ///   ● <b>锁定框 UI</b> —— 对本地玩家绘制的四角监察括号: 间距随监视槽收紧, 冷蓝→金→审判红,
    ///     四角带伺服抖动; 满槽审判时咬合快闪 —— "系统已锁定你" 的 UI 化视觉。
    /// 昂贵的全屏 screenTarget 后处理(OverseerScanline 扫描线/故障/开机)由 <see cref="CelestialOverseer"/>
    /// 的 PostDraw 单独申请名额绘制; 处决/开火加性泛光由其 PreDraw 经 DrawRadialBloomAt 仲裁。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/>(无活动批): 氛围/地纹位于实体之下, 危险弹幕在其上层 → 不遮挡躲避信息。
    /// 纯本地视觉, 服务端零绘制, 受 MythologyConfig 降级。
    /// </summary>
    public class OverseerSurveillanceScreenSystem : ModSystem
    {
        private static float _vignette;     // 监视压迫暗角 0~1
        private static float _warm;         // 冷蓝→暖金权柄 比例 0~1
        private static float _runic;        // 全视眼穹/审判庭法阵 强度 0~1
        private static float _runicRadius;  // 法阵世界半径(px)
        private static bool _dome;          // true=陪审终局眼穹(dome) false=扫描环
        private static Vector2 _center;
        private static float _time;
        private static ulong _lastPublishFrame;

        // 锁定框 UI (V3)
        private static float _lockFrac;     // 监视槽比例 0~1 (间距收紧)
        private static float _lockAlpha;    // 锁定框整体透明度 0~1
        private static float _lockLethal;   // 审判红化/咬合 0~1

        /// <summary>由 <see cref="CelestialOverseer"/> 每帧调用, 发布当前监视/全视氛围标量（纯本地视觉）。</summary>
        public static void Publish(Vector2 center, float time, float vignette, float warm, float runic, float runicRadius, bool dome,
            float lockFrac, float lockAlpha, float lockLethal) {
            _center = center;
            _time = time;
            _vignette = vignette;
            _warm = warm;
            _runic = runic;
            _runicRadius = runicRadius;
            _dome = dome;
            _lockFrac = lockFrac;
            _lockAlpha = lockAlpha;
            _lockLethal = lockLethal;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _vignette = _warm = _runic = 0f;
            _lockFrac = _lockAlpha = _lockLethal = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // 发布过期(Boss 消失)时平滑淡出
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _vignette = MathHelper.Lerp(_vignette, 0f, 0.1f);
                _runic = MathHelper.Lerp(_runic, 0f, 0.15f);
                _lockAlpha = MathHelper.Lerp(_lockAlpha, 0f, 0.12f);
            }

            DrawSurveillanceVignette();
            DrawAllSeeingRing();
            DrawLockOnBrackets();
        }

        // ===== ElementalScreenTint: 监视压迫暗角(冷蓝→暖金, 随槽收紧) =====
        private static void DrawSurveillanceVignette() {
            if (_vignette <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            // 冷钢监视蓝 → 暖金权柄(非红: 纯红仅留给致命审判射线)
            Color cold = new(70, 120, 190);
            Color warm = TelegraphColors.Gold;
            Color tint = Color.Lerp(cold, warm, _warm);

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(0.25f + _vignette * 0.55f, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uTint"]?.SetValue(new Vector4(tint.ToVector3(), 0.12f + _vignette * 0.12f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(new Color(15, 18, 35).ToVector3(), 0f));
            fx.Parameters["uVignette"]?.SetValue(0.35f + _vignette * 0.5f); // 随槽升高暗角收紧
            fx.Parameters["uFogScale"]?.SetValue(2.6f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== ArenaRunic: 全视眼穹(陪审终局 dome) / 窥视扫描环 =====
        private static void DrawAllSeeingRing() {
            if (_runic <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_center, _runicRadius, out Vector2 uv, out float radiusFrac, out float aspect);

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(MathHelper.Clamp(radiusFrac, 0.15f, 0.95f));
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_runic, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.Holy.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(TelegraphColors.Gold.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(11f);
            fx.Parameters["uMode"]?.SetValue(_dome ? 1f : 0f); // dome=陪审眼穹(罩) / 0=扫描法阵环
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== 锁定框 UI: 本地玩家四角监察括号 (纯 CPU 线段, 无着色器依赖) =====
        private static void DrawLockOnBrackets() {
            if (_lockAlpha <= 0.02f)
                return;
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            if (pixel == null)
                return;

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 center = player.Center - Main.screenPosition;

            // 间距随监视槽收紧; 审判时高频咬合
            float gap = MathHelper.Lerp(150f, 56f, MathHelper.Clamp(_lockFrac, 0f, 1f));
            if (_lockLethal > 0.01f)
                gap *= 1f - _lockLethal * 0.22f * (0.5f + 0.5f * MathF.Sin(_time * 26f));

            // 冷蓝 → 金 → 审判红
            Color col = Color.Lerp(new Color(90, 150, 215), TelegraphColors.Gold, _lockFrac);
            col = Color.Lerp(col, TelegraphColors.Lethal, MathHelper.Clamp(_lockLethal, 0f, 1f));
            col *= 0.85f * MathHelper.Clamp(_lockAlpha, 0f, 1f);

            const int arm = 14;   // 括号臂长
            const int th = 2;     // 线宽
            Rectangle src = new(0, 0, 1, 1);
            // 伺服抖动: 每 6 帧每角刷新一次 ±1px 量化偏移 (机械对位感)
            int jSeed = (int)(Main.GameUpdateCount / 6);

            for (int cx = -1; cx <= 1; cx += 2) {
                for (int cy = -1; cy <= 1; cy += 2) {
                    int h = (jSeed * 73856093) ^ ((cx + 2) * 19349663) ^ ((cy + 2) * 83492791);
                    Vector2 jitter = new((h & 3) - 1.5f, ((h >> 2) & 3) - 1.5f);
                    Vector2 corner = center + new Vector2(cx * gap, cy * gap) + jitter * 0.8f;

                    // 水平臂 (从角向内)
                    int hx = (int)(cx > 0 ? corner.X - arm : corner.X);
                    sb.Draw(pixel, new Rectangle(hx, (int)corner.Y - th / 2, arm, th), src, col);
                    // 垂直臂
                    int vy = (int)(cy > 0 ? corner.Y - arm : corner.Y);
                    sb.Draw(pixel, new Rectangle((int)corner.X - th / 2, vy, th, arm), src, col);
                }
            }

            // 中心测微十字 (极淡)
            Color cross = col * 0.4f;
            sb.Draw(pixel, new Rectangle((int)center.X - 5, (int)center.Y, 11, 1), src, cross);
            sb.Draw(pixel, new Rectangle((int)center.X, (int)center.Y - 5, 1, 11), src, cross);

            sb.End();
        }
    }
}
