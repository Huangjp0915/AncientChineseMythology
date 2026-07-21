using AncientChineseMythology.Helpers;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 金棍左键: 横扫 → 回扫 → 重抡; 每挥 4 次后下一击变"金光三连突"
    /// (三次快速刺击 0.65x/0.65x/1.5x, 第三突金辉爆裂 + 屏震)。
    /// </summary>
    internal class GoldenStickSpearProjectile : StickComboSwingBase
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/GoldenStickSpearProjectile";

        private static readonly SwingStep[] _steps = {
            SwingStep.Sweep(3.7f, 1f),
            SwingStep.Sweep(3.7f, 1.05f, sign: -1),
            SwingStep.Sweep(4.5f, 1.3f, sign: 1, timeMul: 1.25f, scaleMul: 1.1f, impact: true),
            SwingStep.Thrust(1.7f, 1f, reps: 3, timeMul: 1.35f), // 金光三连突 (伤害按突序分配)
        };

        protected override SwingStep[] Steps => _steps;
        protected override int CycleFrames => 30;
        protected override Color TrailOuter => new(160, 110, 30, 150);
        protected override Color TrailInner => new(255, 230, 150, 205);
        protected override float TipLength => 98f;
        protected override float Overshoot => 0.14f;
        protected override int BurstTheme => ACMWeaponBurst.Gold;
        protected override float HitShake => 2f;
        protected override int HitDustType => DustID.GoldFlame;
        protected override Vector3 GlowLight => new(0.5f, 0.4f, 0.1f);

        private bool IsTripleThrust => StepIndex == 3;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
            if (IsTripleThrust)
                modifiers.FinalDamage *= CurrentRep >= 2 ? 1.5f : 0.65f; // 突序决定伤害: 轻·轻·重
            else
                modifiers.FinalDamage *= Step.DamageMul;
        }

        protected override void OnStickHitNPC(NPC target, NPC.HitInfo hit) {
            // 第三突命中: 金辉大爆裂 + 重屏震 (三连突的节奏高点)
            if (IsTripleThrust && CurrentRep >= 2) {
                WeaponVFX.AddScreenShake(target.Center, 2.5f);
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.Gold, 1.4f, Projectile.owner);
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.7f, Pitch = 0.3f }, target.Center);
            }
        }

        protected override void OnStrikeStart(SwingStep step) {
            if (IsTripleThrust) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = 0.25f }, Projectile.Center);
                return;
            }
            base.OnStrikeStart(step);
        }
    }
}
