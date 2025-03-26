using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    public class WoodenStickSpearProjectile : ModProjectile
    {


        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("棍类刺击");
        }

        public override void SetDefaults()
        {
            // 以下尺寸为示例（默认木棍贴图为100×10像素），各武器可自行调整
            Projectile.width = 100;   
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.ownerHitCheck = true;
            Projectile.hide = true;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Player player = Main.player[Projectile.owner];
            // 锚点：玩家挂载中心向下偏移10像素（即手部位置）
            Vector2 ownerCenter = player.RotatedRelativePoint(player.MountedCenter, true);
            Vector2 anchor = ownerCenter + new Vector2(0f, 8f);

            // 使用玩家攻击动画进度计算延伸比例
            float progress = (float)player.itemAnimation / player.itemAnimationMax;
            // 前半段0~0.5 => scale 0~1，后半段0.5~1 => scale 1~0
            float scale = (progress <= 0.5f) ? (progress * 2f) : ((1f - progress) * 2f);
            // 最大延伸距离（可以根据各武器特性调整），例如50像素
            float maxExtension = 40f;
            float extension = maxExtension * scale;

            // 计算朝向（从锚点指向鼠标）
            Vector2 direction = Main.MouseWorld - anchor;
            if (direction == Vector2.Zero)
                direction = Vector2.UnitX;
            direction.Normalize();

            // 棍尾位置即锚点
            Vector2 spearTail = anchor;
            // 棍头位置 = 棍尾 + (延伸距离 + 贴图全长) * 朝向
            Vector2 spearTip = spearTail + direction * (extension + Projectile.width);

            float dummy = 0f;
            // 使用贴图高度（这里为10像素）作为碰撞线宽
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(),
                new Vector2(targetHitbox.Width, targetHitbox.Height),
                spearTail, spearTip, Projectile.height, ref dummy);
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            // 当玩家攻击动画结束时销毁投射物
            if (player.itemAnimation <= 0)
            {
                Projectile.Kill();
                return;
            }

            // 同步玩家攻击动画状态，使抛射物寿命与动画一致
            player.heldProj = Projectile.whoAmI;
            player.itemTime = player.itemAnimation;
            Projectile.timeLeft = player.itemAnimation;

            // 设定朝向：根据鼠标位置判断左右
            Projectile.direction = (Main.MouseWorld.X < player.Center.X) ? -1 : 1;
            Projectile.spriteDirection = Projectile.direction;

            // 锚点：玩家挂载中心向下偏移10像素
            Vector2 ownerCenter = player.RotatedRelativePoint(player.MountedCenter, true);
            Vector2 anchor = ownerCenter + new Vector2(0f, 8f);

            // 计算动画进度与延伸距离
            float progress = (float)player.itemAnimation / player.itemAnimationMax;
            float scale = (progress <= 0.5f) ? (progress * 2f) : ((1f - progress) * 2f);
            float maxExtension = 55f;  // 最大延伸距离（可各武器调整）
            float extension = maxExtension * scale;

            // 计算朝向单位向量（从锚点朝向鼠标）
            Vector2 direction = Main.MouseWorld - anchor;
            if (direction == Vector2.Zero)
                direction = new Vector2(1f, 0f);
            direction.Normalize();

            // 为了让贴图左端（棍尾）与锚点+延伸位置对齐，
            // 贴图默认原点在 (width/2, height/2) = (50, 5)
            // 所以计算贴图中心与左端的偏移量，即 (50,5)，经过朝向旋转后加上
            float directionAngle = direction.ToRotation();
            Vector2 centerOffset = new Vector2(10f, 5f).RotatedBy(directionAngle);
            // 计算贴图左边缘位置：锚点 + 延伸
            Vector2 leftEdge = anchor + direction * extension;
            // 最终设置抛射物中心 = 左边缘 + centerOffset
            Projectile.Center = leftEdge + centerOffset;

            // 设置旋转，使贴图正确指向
            Projectile.rotation = directionAngle;
            if (Projectile.spriteDirection == -1)
                Projectile.rotation += MathHelper.Pi;

            // 同步玩家手臂旋转
            player.itemRotation = directionAngle * Projectile.direction;
        }
    }
}
