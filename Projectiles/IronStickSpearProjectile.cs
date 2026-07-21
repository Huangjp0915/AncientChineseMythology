using Terraria.ID;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 铁棍左键: 横扫 → 回扫 → 重抡 (过顶大回环, 前摇 +30%, 1.35x, 落点冲击环+屏震)。
    /// 钢蓝拖尾; "千斤之铁"的重量课程。
    /// </summary>
    internal class IronStickSpearProjectile : StickComboSwingBase
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/IronStickSpearProjectile";

        private static readonly SwingStep[] _steps = {
            SwingStep.Sweep(3.7f, 1f),
            SwingStep.Sweep(3.7f, 1.05f, sign: -1),
            SwingStep.Sweep(4.6f, 1.35f, sign: 1, timeMul: 1.3f, scaleMul: 1.12f, impact: true),
        };

        protected override SwingStep[] Steps => _steps;
        protected override int CycleFrames => 26;
        protected override Color TrailOuter => new(70, 90, 120, 150);
        protected override Color TrailInner => new(180, 200, 230, 200);
        protected override float TipLength => 96f;
        protected override float HitShake => 1.8f;
        protected override int HitDustType => DustID.Silver;
    }
}
