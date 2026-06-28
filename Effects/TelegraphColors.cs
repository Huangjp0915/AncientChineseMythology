namespace AncientChineseMythology
{
    /// <summary>
    /// 全模组统一预警色彩语言 (全局观感契约 §6.1)。
    /// **红色 = 即将造成伤害的致命预警, 只留给真正的伤害源; 其余为主题色, 不与红冲突。**
    /// 预警必有「形状 + 颜色 + 渐强时间」三要素。所有 Boss V2 telegraph 一律取自本表, 禁止自造红。
    /// </summary>
    public static class TelegraphColors
    {
        // ===== 致命预警 (唯一红) =====
        /// <summary>致命攻击预警(落点/激光路径/冲刺线)。纯红, 命中前 0.3~0.6s 必须可读。</summary>
        public static readonly Color Lethal = new(250, 40, 56);

        // ===== 天庭 / 安全 / 神圣 =====
        /// <summary>安全/治疗/神圣: 金白。</summary>
        public static readonly Color Holy = new(255, 250, 208);
        /// <summary>安全缝/护盾/赐福区: 翠玉。</summary>
        public static readonly Color Safe = new(220, 255, 230);

        // ===== 地府 / 阴 =====
        /// <summary>地府氛围/冥律标记: 幽蓝紫。</summary>
        public static readonly Color NetherViolet = new(120, 90, 200);
        /// <summary>地府 DoT/魂蚀: 鬼绿。</summary>
        public static readonly Color GhostGreen = new(110, 230, 150);
        /// <summary>处决/定罪: 赤红(配 decree-vignette)。</summary>
        public static readonly Color Execution = new(200, 30, 40);

        // ===== 冰 / 水 (玄武四海) =====
        /// <summary>冰蓝(冰系预警, 用冰白做高光边)。</summary>
        public static readonly Color Frost = new(140, 199, 242);
        /// <summary>深冰蓝。</summary>
        public static readonly Color DeepFrost = new(38, 64, 115);
        /// <summary>冰白高光。</summary>
        public static readonly Color IceWhite = new(217, 235, 255);

        // ===== 雷 =====
        /// <summary>雷电: 青白电弧(高频闪)。</summary>
        public static readonly Color Lightning = new(180, 230, 255);

        // ===== 元素方位 (四圣兽) =====
        /// <summary>青龙青(东·木)。</summary>
        public static readonly Color AzureDragon = new(64, 200, 180);
        /// <summary>白虎银白(西·金)。</summary>
        public static readonly Color WhiteTiger = new(225, 235, 245);
        /// <summary>朱雀赤(南·火)。</summary>
        public static readonly Color Vermilion = new(235, 70, 45);
        /// <summary>玄武玉黑(北·水)。</summary>
        public static readonly Color BlackTortoise = new(40, 70, 70);

        // ===== 通用主题火/金 =====
        /// <summary>火焰/熔心暖橙。</summary>
        public static readonly Color Flame = new(255, 140, 50);
        /// <summary>金芒(天御/毗沙门)。</summary>
        public static readonly Color Gold = new(255, 215, 120);

        /// <summary>
        /// 预警渐强时长建议(tick) ∝ 伤害量 (§6.1 时间编码)。返回该威胁级别的最小预告时长。
        /// </summary>
        public static int TelegraphTicks(ThreatTier tier) => tier switch {
            ThreatTier.Minor => 20,      // 小压制弹 ≤20
            ThreatTier.Medium => 45,     // 中等攻击 ~35~55
            ThreatTier.Execution => 75,  // 处决级 60~90 (+渐强震屏 + 蓄力泛光)
            _ => 45
        };
    }

    /// <summary>预警威胁等级 — 决定预告时长与镜头配方强度 (§6.1/§6.3)。</summary>
    public enum ThreatTier
    {
        Minor,
        Medium,
        Execution
    }
}
