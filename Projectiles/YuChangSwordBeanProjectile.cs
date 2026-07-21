using AncientChineseMythology.Helpers;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 鱼肠剑·鱼影飞刃 (透骨刺放出) — 重做: 由"白尘直线弹"改为游鱼身法 —
    /// 正弦游动 + 300px 内轻微追踪; 配色统一寒银白青, 命中银青碎光 (弃用旧金币粒子)。
    /// </summary>
    public class YuChangSwordBeanProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/YuChangSwordBean";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 60;
            Projectile.penetrate = 1; //击中即散 (鱼影一闪)
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            // 游鱼身法: 垂直于速度的正弦摆尾
            float age = 60 - Projectile.timeLeft;
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 side = forward.RotatedBy(MathHelper.PiOver2);
            Projectile.position += side * MathF.Sin(age * 0.55f) * 2.2f;

            // 300px 内轻微追踪 (鱼影咬向猎物)
            NPC prey = null;
            float best = 300f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy())
                    continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < best) {
                    best = d;
                    prey = npc;
                }
            }
            if (prey != null) {
                Vector2 want = (prey.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * Projectile.velocity.Length();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.05f);
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + Projectile.ai[0];

            if (!Main.dedServ && Main.rand.NextFloat() < 0.4f) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    Main.rand.NextBool(3) ? DustID.IceTorch : DustID.WhiteTorch, Scale: 1.2f);
                dust.noGravity = true;
                dust.velocity = Projectile.velocity * 0.4f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 银青碎光 + 寒水爆发
            if (!Main.dedServ) {
                for (int i = 0; i < 14; i++) {
                    Vector2 speed = Main.rand.NextVector2Circular(5f, 5f);
                    Dust dust = Dust.NewDustPerfect(target.Center,
                        Main.rand.NextBool() ? DustID.IceTorch : DustID.WhiteTorch,
                        speed, 0, default, Main.rand.NextFloat(1.2f, 1.9f));
                    dust.noGravity = true;
                }
            }
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Water, scale: 0.9f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 7f,
                outerColor: new Color(60, 95, 130, 150),
                innerColor: new Color(225, 240, 255, 210),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);
            return true;
        }
    }
}
