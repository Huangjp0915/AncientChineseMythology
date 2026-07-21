using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls
{
    /// <summary>
    /// 祖龙残魂 V2 太初屏幕演出系统 (着色器表现层)。
    /// 由 <see cref="AncestralDragonSoulHead"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动, 集中绘制三类
    /// **不读 screenTarget** 的廉价 overlay (不占全屏后处理名额, 见 toolkit §C.4#2):
    ///   ● <b>ElementalScreenTint</b> —— 太初灰白雾氛围底色 (狂暴终曲常驻, 越接近合体越浓)。
    ///   ● <b>ArenaRunic</b>(法阵模式) —— 「刀魂碎片场」谜题环地纹 (告知玩家"先破碎片再破龙")。
    ///   ● <b>RadialBloom</b> —— 双魂回拢合体 / 碎片解锁 / 终极释放的加性太初泛光。
    /// 昂贵的全屏 screenTarget 扭曲(GenericWarp 太初雾)由 <see cref="AncestralDragonSoulHead.PostDraw"/>
    /// 单独申请名额绘制。绘制位于 <see cref="PostDrawTiles"/> (实体之下): 危险弹幕在其上层 → 不遮挡躲避信息 (§6.6)。
    /// 纯本地视觉, 服务端零绘制, 受 <see cref="MythologyConfig"/> 降级。
    /// </summary>
    public class AncestralSoulScreenSystem : ModSystem
    {
        // 太初色: 灰青白 (主) + 深玄青 (低处)
        private static readonly Color Primordial = new(196, 214, 232);
        private static readonly Color PrimordialDeep = new(54, 74, 112);

        private static float _tint;     // ElementalScreenTint 强度
        private static float _runic;    // ArenaRunic 谜题环强度
        private static float _bloom;    // RadialBloom 泛光强度
        private static float _flash;    // 白屏顿帧 (合体/终爆, 全场限量节拍), 自衰减
        private static Vector2 _center;
        private static float _time;
        private static ulong _lastPublishFrame;

        /// <summary>由 Boss 每帧调用, 发布太初氛围标量 (纯本地视觉)。flash 为一次性白屏顿帧脉冲。</summary>
        public static void Publish(Vector2 center, float tint, float runic, float bloom, float time, float flash = 0f) {
            _center = center;
            _tint = tint;
            _runic = runic;
            _bloom = bloom;
            _time = time;
            if (flash > _flash)
                _flash = flash;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _tint = _runic = _bloom = _flash = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;

            // 顿帧脉冲无论配置开关都要衰减 (防配置关闭期间滞留, 重开时凭空白屏)
            _flash *= 0.86f;

            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 离场/未发布时平滑淡出, 避免残留
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _tint = MathHelper.Lerp(_tint, 0f, 0.1f);
                _runic = MathHelper.Lerp(_runic, 0f, 0.15f);
                _bloom = MathHelper.Lerp(_bloom, 0f, 0.15f);
            }

            DrawAmbientTint();
            DrawDaoRunic();
            DrawBloom();
            DrawWhiteFlash();
        }

        // ===== 白屏顿帧: 合体 / 终爆的一次性冲击帧 (纯色白, ~12f 衰减) =====
        private static void DrawWhiteFlash() {
            if (_flash <= 0.02f)
                return;

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                Color.White * MathHelper.Clamp(_flash, 0f, 1f));
            sb.End();
        }

        // ===== ElementalScreenTint: 太初灰白雾氛围 =====
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
            // 覆盖度刻意保守, 始终能看清弹幕 (红色只留给致命预警)
            fx.Parameters["uTint"]?.SetValue(new Vector4(Primordial.ToVector3(), 0.26f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(PrimordialDeep.ToVector3(), 0f));
            fx.Parameters["uVignette"]?.SetValue(0.40f);
            fx.Parameters["uFogScale"]?.SetValue(2.2f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== ArenaRunic(法阵): 刀魂碎片场谜题环 =====
        private static void DrawDaoRunic() {
            if (_runic <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            Vector2 uv = (_center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(0.46f);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_runic, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(Primordial.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(TelegraphColors.Holy.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(12f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== RadialBloom: 合体/解锁/终极的太初泛光 =====
        private static void DrawBloom() {
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
            fx.Parameters["uRadius"]?.SetValue(0.42f + (1f - _bloom) * 0.45f); // 释放向外扩张
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColor"]?.SetValue(new Vector4(Primordial.ToVector3(), 1f));
            fx.Parameters["uRayCount"]?.SetValue(8f);
            fx.Parameters["uFalloff"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
        }
    }
}
