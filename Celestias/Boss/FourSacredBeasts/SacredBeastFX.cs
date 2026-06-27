using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts
{
    /// <summary>
    /// 四圣兽着色器/VFX 间接层 —— 对接 V2 着色器地基（由另一 agent 并行授权）的统一入口。
    /// Guarded indirection over the V2 shader foundation (authored concurrently by another agent).
    ///
    /// 设计目的 Why this exists:
    ///  - 规范着色器（DissolveBurn / GenericWarp / ElementalScreenTint / GroundDecal / BeamGrad /
    ///    RadialBloom / ReflectWard，见 BOSS_REDO_PLAN_V2 §3.1）可能尚未编译。所有访问都经
    ///    <see cref="TryGetShader"/> 用 <c>ModContent.HasAsset</c> 守卫，缺失即安全降级（no-op 或 CPU fallback），
    ///    保证 SacredBeastBase 与各圣兽在着色器地基落地前后都能编译运行。
    ///  - 当全局着色器 C# 包装 API 落地后，这里是**唯一替换点**：把各方法体改为转调全局封装即可，
    ///    上层圣兽代码无需改动（见各方法 TODO）。
    ///
    /// 性能/MP 护栏（工具箱 §C.4 / §C.5）：所有方法服务端零绘制；全屏后处理 intensity&lt;0.01 立即返回；
    /// SpriteBatch 自定义 Begin 后必恢复项目默认（Deferred / PointClamp / GameViewMatrix）。
    /// </summary>
    public static class SacredBeastFX
    {
        // ---- 规范着色器名 Canonical .fx names (BOSS_REDO_PLAN_V2 §3.1) ----
        public const string DissolveBurn = "DissolveBurn";
        public const string GenericWarp = "GenericWarp";
        public const string ElementalScreenTintShader = "ElementalScreenTint";
        public const string PaletteLUT = "PaletteLUT";
        public const string GroundDecal = "GroundDecal";
        public const string BeamGrad = "BeamGrad";
        public const string RadialBloomShader = "RadialBloom";
        public const string ReflectWard = "ReflectWard";

        private const string EffectRoot = "AncientChineseMythology/Effects/";

        private static readonly Dictionary<string, Asset<Effect>> _cache = new();
        private static readonly HashSet<string> _missing = new();

        /// <summary>
        /// 守卫式取着色器。缺失（尚未编译/不存在）返回 null，并记忆为缺失避免重复探测。仅客户端有效。
        /// </summary>
        public static Effect TryGetShader(string canonicalName) {
            if (Main.dedServ || string.IsNullOrEmpty(canonicalName)) return null;
            if (_missing.Contains(canonicalName)) return null;
            if (_cache.TryGetValue(canonicalName, out Asset<Effect> cached)) return cached?.Value;

            string path = EffectRoot + canonicalName;
            if (!ModContent.HasAsset(path)) { _missing.Add(canonicalName); return null; }
            try {
                Asset<Effect> asset = ModContent.Request<Effect>(path, AssetRequestMode.ImmediateLoad);
                _cache[canonicalName] = asset;
                return asset?.Value;
            }
            catch {
                _missing.Add(canonicalName);
                return null;
            }
        }

        /// <summary>规范着色器是否已可用（已编译并加载成功）。</summary>
        public static bool ShaderReady(string canonicalName) => TryGetShader(canonicalName) != null;

        /// <summary>
        /// 通用全屏后处理骨架（喂 <c>Main.screenTarget</c>）。规范着色器未就绪则 no-op。
        /// 供 GenericWarp / PaletteLUT / ElementalScreenTint 等共用，遵守工具箱 §A.3 SpriteBatch 配对。
        /// </summary>
        /// <param name="sb">当前主 SpriteBatch（PostDraw/绘制 System 内）。</param>
        /// <param name="canonicalName">规范着色器名（见常量）。</param>
        /// <param name="configure">设参回调（设 uTime/uIntensity/uTint/...）。</param>
        /// <param name="noise">可选噪声纹理（走采样器槽位 1，LinearWrap）。</param>
        public static void FullscreenPost(SpriteBatch sb, string canonicalName, Action<Effect> configure, Texture2D noise = null) {
            if (Main.dedServ || sb == null) return;
            Effect fx = TryGetShader(canonicalName);
            if (fx == null) return; // 着色器地基尚未落地 → 安全降级

            configure?.Invoke(fx);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            if (noise != null) {
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();
            // 恢复项目默认状态（§C.4#5）
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// 元素屏幕染色（全屏氛围 overlay）。规范着色器 <see cref="ElementalScreenTintShader"/> 就绪时启用，
        /// 否则 no-op —— 此时仍由各圣兽的 CustomSky + <c>Filters.Scene</c> 提供底色染色，画面不缺失。
        /// 标准 uniform 命名见工具箱 §A.1。
        /// </summary>
        public static void ElementalScreenTint(SpriteBatch sb, Vector2 centerUV, Color tint, float intensity, Texture2D noise = null) {
            if (Main.dedServ || intensity <= 0.01f) return;
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            float i = MathHelper.Clamp(intensity, 0f, 1f);
            FullscreenPost(sb, ElementalScreenTintShader, fx => {
                fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                fx.Parameters["uIntensity"]?.SetValue(i);
                fx.Parameters["uCenter"]?.SetValue(centerUV);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uTint"]?.SetValue(tint.ToVector4());
            }, noise);
        }

        /// <summary>
        /// 径向元素泛光（蓄力/爆发/相变通用）。CPU fallback：加性 SoftGlow 双层，始终可用。
        /// TODO: <see cref="RadialBloomShader"/>.fx 落地后改走 <see cref="TryGetShader"/> 路线以获更优阈值 bloom。
        /// 自带 Begin/End 配对并恢复项目默认，调用方无需关心混合状态。
        /// </summary>
        public static void RadialBloom(SpriteBatch sb, Vector2 worldCenter, Color color, float worldRadius, float intensity) {
            if (Main.dedServ || sb == null || intensity <= 0.01f) return;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null) return;

            Vector2 pos = worldCenter - Main.screenPosition;
            Vector2 origin = glow.Size() / 2f;
            float i = MathHelper.Clamp(intensity, 0f, 1f);
            float scale = worldRadius * 2f / glow.Width;

            Color inner = color * i; inner.A = 0;
            Color outer = color * (i * 0.5f); outer.A = 0;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(glow, pos, null, outer, 0f, origin, scale * 1.6f, SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, inner, 0f, origin, scale, SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// 地面落点/范围预警圈（形状=圆，§6.2）。CPU fallback：加性 SoftGlow 圆。
        /// TODO: <see cref="GroundDecal"/>.fx 落地后改走 SDF 着色器版（更清晰边缘/符文）。
        /// 预警色请用 <c>SacredBeastColors.Telegraph(element, lethal)</c>（致命=红）。
        /// </summary>
        public static void TelegraphCircle(SpriteBatch sb, Vector2 worldCenter, Color color, float worldRadius, float intensity) {
            if (Main.dedServ || sb == null || intensity <= 0.01f) return;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null) return;

            Vector2 pos = worldCenter - Main.screenPosition;
            Vector2 origin = glow.Size() / 2f;
            float i = MathHelper.Clamp(intensity, 0f, 1f);
            float scale = worldRadius * 2f / glow.Width;
            Color c = color * (i * 0.7f); c.A = 0;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(glow, pos, null, c, 0f, origin, scale, SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
