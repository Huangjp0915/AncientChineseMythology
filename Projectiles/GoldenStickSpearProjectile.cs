using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    public class GoldenStickSpearProjectile : ModProjectile
    {


        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("棍类刺击");
        }

        public override void SetDefaults()
        {
            // 以下尺寸为示例（默认木棍贴图为100×10像素），各武器可自行调整
            Projectile.width = 62;   
            Projectile.height = 62;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.ownerHitCheck = true;
            Projectile.hide = true;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            
            // 立即销毁机制优化
            if (player.itemAnimation <= 1 || !player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            // 同步状态
            player.heldProj = Projectile.whoAmI;
            player.itemTime = 2; // 强制保持激活
            Projectile.timeLeft = 2;

            // 计算标准化进度（0→1→0）
            float totalAnimationTime = player.itemAnimationMax;
            float currentProgress = 1f - (float)player.itemAnimation / totalAnimationTime;
            float phase = currentProgress < 0.5f ? 
                currentProgress * 2f :       // 刺出阶段：0→1
                (1f - (currentProgress - 0.5f) * 2f); // 收回阶段：1→0

            // 方向控制
            Projectile.direction = Main.MouseWorld.X < player.Center.X ? -1 : 1;
            player.ChangeDir(Projectile.direction);
            Projectile.spriteDirection = Projectile.direction;

            // 手部定位（精确校准）
            Vector2 handPosition = player.RotatedRelativePoint(player.MountedCenter) + 
                                new Vector2(4f * Projectile.direction, // 水平偏移
                                            player.gfxOffY - 8f * player.gravDir);

            // 匀速运动参数
            float maxReach = 48f; // 最大延伸距离
            float movementFactor = phase * maxReach;

            // 攻击方向计算（带安全保护）
            Vector2 attackDirection = Main.MouseWorld - handPosition;
            if (attackDirection == Vector2.Zero) attackDirection = Vector2.UnitX;
            attackDirection.Normalize();

            // 位置计算（包含惯性缓冲）
            Vector2 targetPosition = handPosition + 
                                attackDirection * movementFactor + 
                                player.velocity * 0.5f; // 跟随玩家速度
            
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPosition, 0.9f);

            // 旋转系统（保持45度基础倾斜）
            float baseAngle = MathHelper.ToRadians(45f) * Projectile.direction;
            Projectile.rotation = attackDirection.ToRotation() + baseAngle;

            // 玩家手臂同步
            player.itemRotation = (attackDirection * Projectile.direction).ToRotation();
            if (attackDirection.Y * Projectile.direction < 0f)
            {
                player.itemRotation -= MathHelper.Pi * Projectile.direction;
            }
            player.itemLocation = Projectile.Center;

            // 视觉平滑处理
            Projectile.position -= Projectile.velocity;
            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.4f, 0.2f)); // 添加武器光效
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            // 矛类武器碰撞检测
            float collisionPoint = 0f;
            Vector2 start = Main.player[Projectile.owner].RotatedRelativePoint(Main.player[Projectile.owner].MountedCenter);
            Vector2 end = start + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 100f; // 调整100f为你的攻击距离
            
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(),
                targetHitbox.Size(),
                start,
                end,
                16f, // 碰撞线宽度
                ref collisionPoint);
        }

        public override void ModifyDamageHitbox(ref Rectangle hitbox)
        {
            // 根据旋转调整伤害框
            int expand = (int)(62 * Math.Abs(Math.Cos(Projectile.rotation)));
            hitbox.Inflate(expand, 0);
        }
    }
}
