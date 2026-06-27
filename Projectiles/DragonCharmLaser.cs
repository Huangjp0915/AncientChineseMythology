using AncientChineseMythology.Helpers;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    public class DragonCharmLaser : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/DragonCharmLaser";

        public override void SetStaticDefaults() {
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.aiStyle = 0; //自定义行为
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600; //最长存在10秒
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.localAI[0]++; // 计龄 (表现层起手血光)

            //简单直线运动，并生成火焰烟尘效果
            int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Flare);
            Main.dust[dust].velocity *= 0.5f;
            Main.dust[dust].scale = 1.2f;

            //如果速度不为零，则根据速度方向更新旋转角度
            if (Projectile.velocity.Length() > 0.1f) {
                //由于贴图默认朝左，所以我们需要加上 Pi（180度）使其正确对齐
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
            }
        }

        //当激光弹与地形碰撞时触发爆炸
        public override bool OnTileCollide(Vector2 oldVelocity) {
            Explode();
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damage) {
            // 金辉命中演出 (径向辉光 + 冲击环) + 轻屏震
            WeaponVFX.AddScreenShake(target.Center, 2f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Gold, scale: 1.3f, owner: Projectile.owner);
            Explode();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);

            // 龙血献祭: 起手数帧的血光 (player-side 血色献祭脉冲)
            if (Projectile.localAI[0] < 14f) {
                float c = 1f - Projectile.localAI[0] / 14f;
                WeaponVFX.DrawGlowBurst(Projectile.Center, 1.2f * c + 0.3f, new Color(200, 30, 20) * c);
            }

            // 金龙激光芯 (BeamGrad 渐变直带, 取代默认贴图)
            Vector2 start = Projectile.Center - dir * 34f;
            Vector2 end = Projectile.Center + dir * 14f;
            ACMShaders.DrawBeam(start, end, 9f,
                new Color(255, 230, 140, 200), new Color(200, 120, 30, 120), 1f, coreSharp: 2.4f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.5f, new Color(255, 210, 110));
            return false;
        }

        private void Explode() {
            if (Projectile.owner == Main.myPlayer) {
                //发射爆炸弹，伤害与击退同激光弹一致
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<DragonCharmExplosion>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
            }
            Projectile.Kill();
        }
    }
}
