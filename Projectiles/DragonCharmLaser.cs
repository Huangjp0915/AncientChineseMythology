using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    public class DragonCharmLaser : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/DragonCharmLaser";

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.aiStyle = 0; // 自定义行为
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600; // 最长存在10秒
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI()
        {
            // 简单直线运动，并生成火焰烟尘效果
            int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Flare);
            Main.dust[dust].velocity *= 0.5f;
            Main.dust[dust].scale = 1.2f;
            
            // 如果速度不为零，则根据速度方向更新旋转角度
            if (Projectile.velocity.Length() > 0.1f)
            {
                // 由于贴图默认朝左，所以我们需要加上 Pi（180度）使其正确对齐
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
            }
        }

        // 当激光弹与地形碰撞时触发爆炸
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Explode();
            return true;
        }

        // 使用新版 NPC.HitInfo 重写 OnHitNPC
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damage)
        {
            Explode();
        }

        private void Explode()
        {
            if (Projectile.owner == Main.myPlayer)
            {
                // 发射爆炸弹，伤害与击退同激光弹一致
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<DragonCharmExplosion>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
            }
            Projectile.Kill();
        }
    }
}
