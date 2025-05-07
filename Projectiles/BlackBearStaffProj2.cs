using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Terraria.GameContent;
using System;
using Terraria.DataStructures;

namespace AncientChineseMythology.Projectiles
{
    public class BlackBearStaffProj2 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Head_Boss"; // 使用物品的纹理作为投射物的纹理

        public override void SetDefaults()
        {
            Projectile.width = 20; // 弹幕宽度
            Projectile.height = 20; // 弹幕高度
            Projectile.friendly = true; // 友方弹幕
            Projectile.tileCollide = true; // 与瓷砖碰撞
            Projectile.DamageType = DamageClass.Summon; // 伤害类型
            Projectile.penetrate = 1; // 穿透
            Projectile.ignoreWater = true; // 无视液体
            Projectile.timeLeft = 360; // 存在时间，单位为帧
            Projectile.alpha = 1; // 透明度
            Projectile.light = 0.25f; // 发光亮度
        }

        public override void OnSpawn(IEntitySource source)
        {
            // 在玩家方向上进行一定的偏移
            float angleOffset = Main.rand.NextFloat(-MathHelper.PiOver4/4, MathHelper.PiOver4/4); // 偏移角度范围为 -45 到 45 度
            Projectile.velocity = Projectile.velocity.RotatedBy(angleOffset);

            // 随机化大小
            Projectile.scale = Main.rand.NextFloat(0.4f, 0.8f);

            // 初始化旋转
            Projectile.rotation = Main.rand.NextFloat(0, MathHelper.TwoPi);
        }

        public override void AI()
        {
            // 模拟重力
            Projectile.velocity.Y += 0.2f; // 向下的速度影响
            Projectile.rotation += 0.5f; // 旋转速度
        }

        [Obsolete]
        public override void OnKill(int timeLeft)
        {
            //粒子效果
            for (int i = 0; i < 3; i++)
            {
                int dustIndex = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, 
                    DustID.YellowTorch, Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f, 100, Color.White, 1.5f);
                Main.dust[dustIndex].noGravity = true;
            }
        }
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;

            Rectangle sourceRectangle = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 origin = new Vector2(texture.Width / 2, texture.Height / 2);
            Vector2 position = Projectile.Center;
            SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Color drawColor = Projectile.GetAlpha(lightColor);
            Main.spriteBatch.Draw(texture, position - Main.screenPosition, sourceRectangle, drawColor, Projectile.rotation, origin, Projectile.scale, effects, 0);
            return false;
        }
    }
}

