using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Weapons;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 雷龙灵 (龙符咒普通掷出): 自燃尽的符纸中化形, 蜿蜒扑向目标点,
    /// 沿身爬行电弧; 命中/抵达时落雷印起爆 (二段 60% AoE)。
    /// ai[0]/ai[1] = 目标点。类名保留 (本地化/兼容), 职能由直线激光重铸为雷龙。
    /// </summary>
    public class DragonCharmLaser : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int GrowTime = 8;
        private const float TurnLimit = 0.09f;

        private Vector2 TargetPos => new(Projectile.ai[0], Projectile.ai[1]);
        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 22;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 90;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override void AI() {
            Age++;

            //化形期: 定身生长, 雷光渐盛
            if (Age <= GrowTime) {
                Projectile.velocity *= 0.6f;
                if (Age == 1)
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.6f, Pitch = 0.25f }, Projectile.Center);
            }
            else {
                //蜿蜒扑向目标: 追踪方向 + 正弦蛇行, 速度递增 (加速的俯冲)
                Vector2 toTarget = TargetPos - Projectile.Center;
                Vector2 desiredDir = toTarget.SafeNormalize(Projectile.velocity.SafeNormalize(Vector2.UnitX));
                float currentRot = Projectile.velocity.ToRotation();
                float desiredRot = desiredDir.ToRotation() + MathF.Sin(Age * 0.32f) * 0.42f;
                float turn = MathHelper.Clamp(MathHelper.WrapAngle(desiredRot - currentRot), -TurnLimit, TurnLimit);
                float speed = MathF.Min(26f, 10f + (Age - GrowTime) * 0.55f);
                Projectile.velocity = (currentRot + turn).ToRotationVector2() * speed;

                //抵达目标点 → 雷印起爆
                if (toTarget.LengthSquared() < 32f * 32f) {
                    SealDetonate(Projectile.Center);
                    return;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.Electric, -Projectile.velocity * 0.06f, 120, default, 0.8f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.55f, 0.5f, 0.25f));
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            SealDetonate(Projectile.Center);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damage) {
            WeaponVFX.AddScreenShake(target.Center, 2f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gold, 1.3f, Projectile.owner);
            //首咬即引爆雷印 (龙没入雷光)
            SealDetonate(target.Center);
        }

        /// <summary>雷印起爆: 朱印演出 + 60% 二段 AoE, 龙身散形。</summary>
        private void SealDetonate(Vector2 pos) {
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.8f, Pitch = -0.1f }, pos);
            if (Projectile.owner == Main.myPlayer) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), pos, Vector2.Zero,
                    ModContent.ProjectileType<CharmSealFX>(), 0, 0f, Projectile.owner, CharmVFX.Dragon, 1.2f);
                //二段 AoE: 60% 伤害, 远程管线 (flags=2) + charmId 3 (×16)
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), pos, Vector2.Zero,
                    ModContent.ProjectileType<CharmNovaProj>(), (int)(Projectile.damage * 0.6f),
                    Projectile.knockBack, Projectile.owner, 160f, 2f + CharmVFX.Dragon * 16f);
            }
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float growth = MathHelper.Clamp(Age / GrowTime, 0f, 1f);

            //龙身: 历史点 + 沿身正弦摆幅 → 专属雷龙条带着色器
            var pts = new System.Collections.Generic.List<Vector2>(Projectile.oldPos.Length + 1);
            Vector2 half = Projectile.Size * 0.5f;
            pts.Add(Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 14f); // 龙首前探
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                float rot = Projectile.oldRot.Length > i ? Projectile.oldRot[i] : Projectile.rotation;
                Vector2 perp = rot.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                pts.Add(Projectile.oldPos[i] + half + perp * MathF.Sin(Age * 0.32f - i * 0.45f) * 7f * growth);
            }
            if (pts.Count >= 2) {
                CharmVFX.DrawDragonRibbon(pts.ToArray(), 26f * (0.35f + 0.65f * growth),
                    new Color(255, 235, 150, 220), new Color(118, 88, 215, 140),
                    energy: 0.7f, pulse: 0f, intensity: growth);
            }

            //化形期的符纸余烬闪
            if (Age <= GrowTime + 4)
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.8f * (1f - growth) + 0.3f,
                    new Color(255, 180, 90) * (1.2f - growth));

            //龙首辉光
            WeaponVFX.DrawGlowBurst(Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 10f,
                0.5f * growth, new Color(255, 235, 150) * growth);
            return false;
        }
    }
}
