using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    public class YuChangSwordProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/YuChangSwordProjectile";

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 100;
            Projectile.ownerHitCheck = true;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            Vector2 handPosition = player.RotatedRelativePoint(player.MountedCenter, true);

            //突刺长度与动画进度
            float maxThrustLength = 50f;
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            Projectile.ai[0] = MathHelper.Lerp(0f, maxThrustLength, progress);

            //核心修正：基础旋转 = 速度方向 + 方向补偿
            float baseRotation = Projectile.velocity.ToRotation();

            //设置最终旋转（基础旋转 + 45°倾斜）
            Projectile.rotation = baseRotation + MathHelper.PiOver4;
            Projectile.spriteDirection = player.direction;

            //沿修正后的方向延伸（基于基础旋转）
            Vector2 thrustDirection = Vector2.UnitX.RotatedBy(baseRotation);
            Projectile.Center = handPosition + thrustDirection * Projectile.ai[0];


            //结束逻辑
            if (player.itemAnimation <= 1)
                Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;

            Vector2 origin = new Vector2(texture.Width / 2, texture.Height / 2);//统一使用中心锚点


            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None, //禁用镜像
                0
            );
            return false;
        }
    }
}