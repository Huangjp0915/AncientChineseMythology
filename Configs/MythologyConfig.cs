using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace AncientChineseMythology
{
    /// <summary>
    /// 拖尾/VFX 质量档(低端 + 多人安全): 控制图元拖尾段数与细分。
    /// </summary>
    public enum TrailQualityLevel
    {
        Off,
        Med,
        High
    }

    /// <summary>
    /// 模组客户端表现配置 — Boss V2 全局观感契约 §6.4/§6.5 的降级开关。
    /// 纯本地视觉, 不影响伤害/相变/同步逻辑(逻辑服务器权威)。
    /// </summary>
    public class MythologyConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        public static MythologyConfig Instance => ModContent.GetInstance<MythologyConfig>();

        [DefaultValue(true)]
        public bool EnableFullscreenShaders;

        [DefaultValue(1f)]
        [Range(0f, 1f)]
        [Increment(0.05f)]
        [Slider]
        public float ScreenShakeScale;

        [DefaultValue(TrailQualityLevel.High)]
        [DrawTicks]
        public TrailQualityLevel TrailQuality;

        // —— 空安全静态访问器(供 ACMShaders / ACMUtils 等无需取实例即可读取) ——

        /// <summary>全屏后处理着色器是否启用(关后退化为 dust/粒子 fallback)。</summary>
        public static bool FullscreenShadersEnabled => Instance?.EnableFullscreenShaders ?? true;

        /// <summary>屏幕震动缩放 0~1(0=完全关闭)。</summary>
        public static float ShakeScale => Instance?.ScreenShakeScale ?? 1f;

        /// <summary>拖尾/VFX 质量档。</summary>
        public static TrailQualityLevel Trail => Instance?.TrailQuality ?? TrailQualityLevel.High;
    }
}
