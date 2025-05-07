using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    public class YuChangSwordBeanProjectile : ModProjectile
    {

        public override string Texture => "AncientChineseMythology/Textures/Projectiles/YuChangSwordBean";
        public override void SetDefaults()
        {
            Projectile.width = 50;  // 贴图宽度
            Projectile.height = 50; // 贴图高度
            Projectile.friendly = true; //
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 60; // 存在时间（帧）
            Projectile.penetrate = 1; // 穿透次数
            Projectile.tileCollide = false; // 不碰撞物块
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1; // 确保射弹击中后消失（触发OnHitNPC）
        }

        public override void AI()
        {
            // 旋转贴图以匹配飞行方向
            Projectile.rotation = Projectile.velocity.ToRotation() + Projectile.ai[0];
            
            if (Main.rand.NextFloat() < 0.3f)
           {
                Dust dust = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.WhiteTorch, // 白色粒子ID
                    Scale: 1.5f
                );
                dust.noGravity = true; // 粒子无重力
                dust.velocity = Projectile.velocity * 0.5f; // 粒子速度减半
            }

        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 生成金色爆炸粒子
            SpawnGoldenExplosion(target.Center);
        }

        private void SpawnGoldenExplosion(Vector2 position)
        {
            // 生成20个金色粒子
            for (int i = 0; i < 20; i++)
            {
                // 随机方向和速度
                Vector2 speed = Main.rand.NextVector2Circular(5f, 5f);
                Dust dust = Dust.NewDustPerfect(
                    position,
                    DustID.GoldCoin, // 金色粒子类型
                    speed,
                    0,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f) // 粒子大小
                );
                dust.noGravity = true; // 粒子无重力
            }
        }
    }
}