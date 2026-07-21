using Terraria;
using Terraria.ID;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 木棍右键"如意戳": 前摇缩回 0.75 → 爆发 4 帧伸长至 1.6 倍直线刺出 → 收招回落。
    /// 教学系列签名"伸缩"的第一课; 替换旧版危险的玩家冲刺+无敌帧。
    /// </summary>
    internal class WoodenStickSpearProjectile_2 : StickComboSwingBase
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/WoodenStickSpearProjectile";

        private static readonly SwingStep[] _steps = {
            SwingStep.Thrust(1.6f, 1.4f),
        };

        protected override SwingStep[] Steps => _steps;
        protected override int CycleFrames => 20;
        protected override Color TrailOuter => new(90, 70, 40, 150);
        protected override Color TrailInner => new(170, 140, 90, 200);
        protected override float TipLength => 92f;
        protected override float Overshoot => 0.14f;
        protected override int HitDustType => DustID.WoodFurniture;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            base.ModifyHitNPC(target, ref modifiers);
            modifiers.Knockback *= 1.5f;
        }
    }
}
