using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    /// <summary>
    /// 鱼肠剑突刺 (左键) — 可见质变 (纯表现): 宝石青紫双层拖尾 + 刃身 SoftGlow 呼吸辉光。机制/伤害不变。
    /// </summary>
    public class YuChangSwordProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/YuChangSwordProjectile";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

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
            // 宝石青紫双层拖尾 + 刃身呼吸辉光 (纯表现)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 7f,
                outerColor: new Color(95, 60, 185, 140), innerColor: new Color(150, 230, 255, 200),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);
            float breathe = 0.5f + 0.5f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 4f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.3f + 0.12f * breathe, new Color(160, 130, 255));

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