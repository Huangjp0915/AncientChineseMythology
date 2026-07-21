using AncientChineseMythology.Helpers;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 如意棍左键: 横扫 → 回扫 → 重抡; 每挥 5 次后下一击变"如意巨大化"横扫
    /// (scale 1.9x / 范围 1.25x / 2x 伤害, 扫击路径击落敌方弹幕, 每挥上限 6 枚)。
    /// 致命纯红 #FF2838 语言。击落改为服务器权威销毁, 替换旧版客户端直改 hostile→friendly 的多人危险实现。
    /// </summary>
    internal class RuyiStickSpearProjectile : StickComboSwingBase
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/RuyiStickSpearProjectile";

        private static readonly SwingStep[] _steps = {
            SwingStep.Sweep(3.8f, 1f),
            SwingStep.Sweep(3.8f, 1.08f, sign: -1),
            SwingStep.Sweep(4.6f, 1.3f, sign: 1, timeMul: 1.25f, scaleMul: 1.12f, impact: true),
            SwingStep.Sweep(5.2f, 2f, sign: 1, timeMul: 1.5f, scaleMul: 1.65f, impact: true), // 如意巨大化
        };

        protected override SwingStep[] Steps => _steps;
        protected override int CycleFrames => 22;
        protected override Color TrailOuter => new(120, 10, 20, 160);
        protected override Color TrailInner => new(250, 40, 56, 210);
        protected override float TipLength => 106f;
        protected override float Overshoot => 0.18f;
        protected override int BurstTheme => ACMWeaponBurst.Fatal;
        protected override float HitShake => 2f;
        protected override int HitDustType => DustID.RedTorch;
        protected override Vector3 GlowLight => new(0.5f, 0.12f, 0.14f);

        private bool IsGiant => StepIndex == 3;
        private int _culled; // 本次巨大化已击落的敌弹数

        public override void AI() {
            base.AI();
            if (!Projectile.active)
                return;

            // 巨大化横扫: 爆发段击落敌方弹幕 (服务器/单机权威, 同步销毁)
            if (IsGiant && InStrike && _culled < 6 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 start = Owner.MountedCenter;
                Vector2 end = start + Projectile.rotation.ToRotationVector2() * EffectiveReach;
                for (int i = 0; i < Main.maxProjectiles && _culled < 6; i++) {
                    Projectile other = Main.projectile[i];
                    if (!other.active || !other.hostile || other.friendly || other.damage <= 0)
                        continue;
                    float point = 0f;
                    if (!Collision.CheckAABBvLineCollision(other.Hitbox.TopLeft(), other.Hitbox.Size(), start, end, 26f, ref point))
                        continue;
                    _culled++;
                    if (!Main.dedServ) {
                        for (int j = 0; j < 6; j++) {
                            Dust d = Dust.NewDustPerfect(other.Center, DustID.RedTorch,
                                Main.rand.NextVector2Circular(3.5f, 3.5f), 0, default, Main.rand.NextFloat(1f, 1.5f));
                            d.noGravity = true;
                        }
                        SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.6f, Pitch = 0.3f }, other.Center);
                    }
                    other.Kill();
                }
            }
        }

        protected override void OnStrikeStart(SwingStep step) {
            if (IsGiant) {
                // 巨大化的重量: 双层音 + 起手屏震
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 1f, Pitch = -0.3f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = -0.4f }, Projectile.Center);
                WeaponVFX.AddScreenShake(Owner.Center, 2.5f);
                return;
            }
            base.OnStrikeStart(step);
        }

        protected override void DoGroundImpact(Vector2 tip) {
            if (IsGiant) {
                WeaponVFX.AddScreenShake(tip, 4f);
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 1f, Pitch = -0.25f }, tip);
                ACMWeaponBurst.Spawn(Projectile.GetSource_FromAI(), tip, ACMWeaponBurst.Fatal, 1.2f, Projectile.owner);
                for (int i = 0; i < 14; i++) {
                    Dust d = Dust.NewDustPerfect(tip, DustID.RedTorch,
                        new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-5f, -1f)), 0, default, Main.rand.NextFloat(1.1f, 1.7f));
                    d.noGravity = Main.rand.NextBool();
                }
                return;
            }
            base.DoGroundImpact(tip);
        }
    }
}
