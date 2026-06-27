using Microsoft.Xna.Framework;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts
{
    /// <summary>
    /// 四圣兽五行属性 / Five-element identity of the four sacred beasts.
    /// 青龙=Wood(东/风雷) · 白虎=Metal(西) · 朱雀=Fire(南) · 玄武=Water(北).
    /// </summary>
    public enum SacredElement
    {
        Wood,   // 青龙 Qinglong
        Metal,  // 白虎 Baihu
        Fire,   // 朱雀 Suzaku
        Water   // 玄武 Xuanwu
    }

    /// <summary>四方方位 / Cardinal direction of a sacred beast.</summary>
    public enum SacredCardinal
    {
        East,   // 青龙
        West,   // 白虎
        South,  // 朱雀
        North   // 玄武
    }

    /// <summary>
    /// 单个圣兽的视觉主题包：主题色 / 预警色 / 方位。供地纹、光束、天幕、屏幕染色统一取色。
    /// A per-beast visual theme bundle (primary/secondary/accent/telegraph colours + cardinal),
    /// the single source of truth for decals, beams, sky tints and screen overlays.
    /// </summary>
    public readonly struct SacredElementTheme
    {
        public readonly SacredElement Element;
        public readonly SacredCardinal Cardinal;

        /// <summary>主题主色（本体辉光 / 天幕底色）。</summary>
        public readonly Color Primary;
        /// <summary>主题深色（阴影 / 渐变暗端）。</summary>
        public readonly Color Secondary;
        /// <summary>高光强调色（蓄力泛光 / 火花）。</summary>
        public readonly Color Accent;
        /// <summary>非致命预警色（本兽元素色，遵守全局观感契约 §6.1：红色只留给致命源）。</summary>
        public readonly Color Telegraph;

        public SacredElementTheme(SacredElement element, SacredCardinal cardinal,
            Color primary, Color secondary, Color accent, Color telegraph) {
            Element = element;
            Cardinal = cardinal;
            Primary = primary;
            Secondary = secondary;
            Accent = accent;
            Telegraph = telegraph;
        }
    }

    /// <summary>
    /// 四圣兽统一可读性 / 主题色板 —— 实现《全局观感契约》§6.1 预警色彩语言。
    /// Unified readability palette implementing the Global Presentation Contract §6.1.
    /// 红色 <see cref="LethalRed"/> 专属"即将造成伤害的致命预警"，其余主题色不与红冲突。
    /// </summary>
    public static class SacredBeastColors
    {
        // ---- 全局共享语义色 Global semantic colours (§6.1) ----

        /// <summary>致命攻击预警 #FF2838 —— 落点 / 激光路径 / 冲刺线，命中前 0.3–0.6s 必须可读。</summary>
        public static readonly Color LethalRed = new(250, 40, 56);
        /// <summary>安全 / 神圣（天庭）金白 #FFFAD0。</summary>
        public static readonly Color SafeGold = new(255, 250, 208);
        /// <summary>安全 / 护盾 翠玉 #DCFFE6。</summary>
        public static readonly Color SafeJade = new(220, 255, 230);

        // ---- 各圣兽元素色板 Per-element palettes ----

        // 青龙 Wood/East —— 翠青 + 雷黄
        private static readonly SacredElementTheme Wood = new(
            SacredElement.Wood, SacredCardinal.East,
            primary: new Color(60, 220, 150),
            secondary: new Color(12, 70, 50),
            accent: new Color(200, 220, 100),
            telegraph: new Color(70, 230, 160));

        // 白虎 Metal/West —— 银白
        private static readonly SacredElementTheme Metal = new(
            SacredElement.Metal, SacredCardinal.West,
            primary: new Color(225, 235, 245),
            secondary: new Color(120, 132, 150),
            accent: new Color(255, 255, 255),
            telegraph: new Color(210, 225, 240));

        // 朱雀 Fire/South —— 赤焰 + 金芒
        private static readonly SacredElementTheme Fire = new(
            SacredElement.Fire, SacredCardinal.South,
            primary: new Color(255, 110, 60),
            secondary: new Color(135, 25, 18),
            accent: new Color(255, 205, 90),
            telegraph: new Color(255, 130, 70));

        // 玄武 Water/North —— 冰蓝 #8CC7F2 / 深冰 #264073 + 玉
        private static readonly SacredElementTheme Water = new(
            SacredElement.Water, SacredCardinal.North,
            primary: new Color(140, 199, 242),
            secondary: new Color(38, 64, 115),
            accent: new Color(150, 220, 190),
            telegraph: new Color(140, 199, 242));

        /// <summary>按元素取主题包。</summary>
        public static SacredElementTheme GetTheme(SacredElement element) => element switch {
            SacredElement.Wood => Wood,
            SacredElement.Metal => Metal,
            SacredElement.Fire => Fire,
            _ => Water,
        };

        /// <summary>
        /// 取预警色：致命攻击恒用纯红 <see cref="LethalRed"/>；非致命用本兽元素色。
        /// </summary>
        public static Color Telegraph(SacredElement element, bool lethal)
            => lethal ? LethalRed : GetTheme(element).Telegraph;
    }
}
