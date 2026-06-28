using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dazhengs
{
    /// <summary>
    /// 大椿「四季轮转」季节身份与色板权威表 (全 Boss 共享语汇, 见 TelegraphColors / §6.1)。
    /// 四季驱动 PaletteLUT 重映射 + ElementalScreenTint 氛围 + 天幕 / 地纹换色, 一处定义、各处复用。
    /// </summary>
    public static class DazhengSeasons
    {
        public const int Spring = 0; // 春·根·绿   — 活体竞技场(根须内蚀+常青安全岛)
        public const int Summer = 1; // 夏·叶·金绿 — 落叶雨
        public const int Autumn = 2; // 秋·金·橙   — 黄金幻象(诱饵树 DPS 谜题)
        public const int Winter = 3; // 冬·冰·蓝白 — 减速 + 冰藤 + 生命汲取(治疗线)

        /// <summary>各季氛围主色 (ElementalScreenTint 上层 + 地纹主色)。</summary>
        public static Color Tint(int season) => season switch {
            Spring => new Color(60, 170, 70),
            Summer => new Color(180, 200, 70),
            Autumn => new Color(220, 140, 45),
            Winter => new Color(150, 200, 240),
            _ => new Color(60, 170, 70),
        };

        /// <summary>各季辅色 (地纹辅色 / 安全岛收边)。</summary>
        public static Color Accent(int season) => season switch {
            Spring => new Color(200, 230, 120),
            Summer => new Color(245, 220, 110),
            Autumn => new Color(255, 200, 90),
            Winter => new Color(225, 245, 255),
            _ => new Color(200, 230, 120),
        };

        /// <summary>PaletteLUT 阴影染色 (rgb, a=权重)。</summary>
        public static Vector4 LutShadow(int season) => season switch {
            Spring => new Vector4(0.45f, 0.85f, 0.45f, 0.7f),
            Summer => new Vector4(0.70f, 0.80f, 0.35f, 0.6f),
            Autumn => new Vector4(0.85f, 0.55f, 0.25f, 0.8f),
            Winter => new Vector4(0.45f, 0.60f, 0.95f, 0.85f),
            _ => new Vector4(0.45f, 0.85f, 0.45f, 0.7f),
        };

        /// <summary>PaletteLUT 高光染色 (rgb, a=权重)。</summary>
        public static Vector4 LutHighlight(int season) => season switch {
            Spring => new Vector4(0.80f, 1.00f, 0.65f, 0.55f),
            Summer => new Vector4(1.00f, 0.95f, 0.55f, 0.65f),
            Autumn => new Vector4(1.00f, 0.80f, 0.45f, 0.7f),
            Winter => new Vector4(0.85f, 0.95f, 1.00f, 0.7f),
            _ => new Vector4(0.80f, 1.00f, 0.65f, 0.55f),
        };

        /// <summary>PaletteLUT 饱和度 (冬季去饱和体现"凛冬")。</summary>
        public static float LutSaturation(int season) => season switch {
            Summer => 1.18f,
            Autumn => 1.10f,
            Winter => 0.72f,
            _ => 1.06f,
        };
    }

    /// <summary>
    /// 大椿 V2 季节屏幕氛围系统 (硬化 ACMShaders 验证用例, 仿朱雀 SuzakuScreenSystem)。
    /// 由 <see cref="Dazheng"/> 每帧 <see cref="Publish"/> 一组季节标量, 在 <see cref="PostDrawTiles"/>
    /// (实体下层 → 不遮挡需躲避的弹幕信息 §6.6) 绘制廉价的非 screenTarget overlay:
    ///   ● ElementalScreenTint —— 当前季节氛围底色 (季节间平滑过渡)。
    /// 昂贵的全屏 screenTarget 调色 (PaletteLUT 四季 grade) 由 <see cref="Dazheng.PostDraw"/> 单独申请名额。
    /// 纯本地视觉、服务端零绘制、受 <see cref="MythologyConfig.FullscreenShadersEnabled"/> 降级。
    /// </summary>
    public class DazhengSeasonScreenSystem : ModSystem
    {
        private static Color _tintColor = DazhengSeasons.Tint(0);
        private static float _tintStrength;
        private static float _time;
        private static ulong _lastPublishFrame;

        /// <summary>由 Dazheng 每帧调用, 发布当前季节氛围 (纯本地视觉)。</summary>
        public static void Publish(Color tintColor, float strength, float time) {
            _tintColor = tintColor;
            _tintStrength = strength;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        public override void OnWorldUnload() {
            _tintStrength = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 不在场/未发布时平滑淡出
            if (Main.GameUpdateCount - _lastPublishFrame > 2)
                _tintStrength = MathHelper.Lerp(_tintStrength, 0f, 0.08f);

            if (_tintStrength <= 0.01f)
                return;

            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_tintStrength, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            // 覆盖度保守, 始终看得清弹幕; 上=季节主色, 下=略压暗同色
            fx.Parameters["uTint"]?.SetValue(new Vector4(_tintColor.ToVector3(), 0.22f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(_tintColor.ToVector3() * 0.4f, 0f));
            fx.Parameters["uVignette"]?.SetValue(0.4f);
            fx.Parameters["uFogScale"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }
    }
}
