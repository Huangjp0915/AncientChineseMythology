using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace AncientChineseMythology.NPCs.Boss.BlackBear
{
    public class BlackBear_Proj4 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Head_Boss"; //使用物品的纹理作为投射物的纹理

        public override void SetDefaults() {
            Projectile.hostile = true; //敌方伤害
            Projectile.width = 32; //弹幕宽度
            Projectile.height = 32; //弹幕高度
            Projectile.friendly = false; //友方弹幕
            Projectile.tileCollide = false; //不与瓷砖碰撞
            Projectile.DamageType = DamageClass.Default; //伤害类型
            Projectile.penetrate = 1; //穿透
            Projectile.ignoreWater = true; //无视液体
            Projectile.timeLeft = 360; //存在时间，单位为帧
            Projectile.alpha = 1; //透明度
            Projectile.light = 0.25f; //发光亮度
        }

        public override void OnSpawn(IEntitySource source) {
            //获取玩家的位置
            Player player = Main.player[Projectile.owner];
            SoundEngine.PlaySound(new SoundStyle("AncientChineseMythology/Sounds/BlackBear/BlackBear_Attack_2"), player.Center);
            //随机化速度
            float speed = Main.rand.NextFloat(12f, 16f);
            float angle = Main.rand.NextFloat(-MathHelper.Pi, 0); //180度范围内随机
            Projectile.velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;

            //随机化大小
            Projectile.scale = Main.rand.NextFloat(0.5f, 1.5f);

            //初始化旋转
            Projectile.rotation = Main.rand.NextFloat(0, MathHelper.TwoPi);
        }

        public override void AI() {
            //模拟重力
            Projectile.velocity.Y += 0.2f; //向下的速度影响
            Projectile.rotation += 0.5f; //旋转速度
        }

        public override bool PreDraw(ref Color lightColor) {
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

