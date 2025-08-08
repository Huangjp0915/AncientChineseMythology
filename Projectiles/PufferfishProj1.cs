using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    public class PufferfishProj1 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/PufferfishProj1";
        private Player player => Main.player[Projectile.owner]; //玩家实例
        private float LaserLength = 0; //激光的长度

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 200; //超过屏幕外多少可以绘制
            base.SetStaticDefaults();
        }
        private void SetLaserPosition()//不穿墙的判断
        {
            LaserLength = 20;
            Vector2 unit = Projectile.velocity.SafeNormalize(Vector2.Zero);
            while (LaserLength <= 1200)//长度还没超过1500时进行循环
            {
                Vector2 range = Projectile.Center + unit * LaserLength;//这是当前激光最远端
                if (!Collision.CanHit(Projectile.Center, 1, 1, range, 1, 1))//如果远端和起点隔着墙
                {
                    LaserLength -= 5;//距离-5
                    return;//跳出该函数
                }
                LaserLength += 2;//距离+2
            }
        }
        public override void SetDefaults() {
            Projectile.width = 32; //弹幕宽度
            Projectile.height = 32; //弹幕高度
            Projectile.friendly = true; //友方弹幕
            Projectile.tileCollide = false; //不与瓷砖碰撞
            Projectile.DamageType = DamageClass.Magic; //伤害类型
            Projectile.penetrate = -1; //穿透
            Projectile.ignoreWater = true; //无视液体
            Projectile.timeLeft = 60; //存在时间，单位为帧
            Projectile.alpha = 100; //透明度
            Projectile.light = 0.75f; //发光亮度
        }
        public override bool ShouldUpdatePosition()//不更新位置
        {
            return false;
        }
        public override void AI()//激光AI主要是控制方向和源点位置
        {
            //这一段是为了视觉效果设置的AI,localai0将被用来控制激光宽度
            if (Projectile.localAI[0] < 25 && Projectile.timeLeft > 26)//弹幕出现时增加
                Projectile.localAI[0]++;
            if (Projectile.timeLeft < 26) Projectile.localAI[0]--;//弹幕快要消失时减少
            SetLaserPosition();//进行碰撞判断
            if (player.channel) {
                if (player.direction == 1)//如果玩家朝着右边
                {
                    player.itemRotation = Projectile.velocity.ToRotation();//获取玩家到弹幕向量的方向
                }
                else {
                    player.itemRotation = Projectile.velocity.ToRotation() + 3.1415926f;//反之需要+半圈
                }
                player.heldProj = Projectile.whoAmI;//之前漏讲了，手持弹幕要写这个
                player.itemAnimation = player.itemTime = 25;//手持弹幕的动画时间
                if (Projectile.timeLeft < 30)
                    Projectile.timeLeft = 30;//保持激光不衰减
            }
            Projectile.Center = player.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 40;//弹幕位置在玩家位置的右边，距离40像素
            //让弹幕的位置保持在距离玩家80的地方，这样能有武器的感觉
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, Main.MouseWorld - player.Center, 0.8f);//0.05f是平滑度

            if (Projectile.timeLeft % 30 == 0)
                //让激光方向追着鼠标走
                Dust.NewDustDirect(Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * LaserLength//激光头部的位置
                    + Main.rand.NextVector2Circular(100, 100), 0, 0, DustID.GreenFairy, 1, 1, 0).scale = 2f;//激光头部的头发
            if (Projectile.timeLeft % 30 == 0)
                Dust.NewDustDirect(Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * LaserLength//尾巴的位置
                    + Main.rand.NextVector2Circular(80, 80), 0, 0, DustID.Confetti_Green, 1, 1, 0).scale = 1.5f;//激光尾部的尾巴
        }
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //粒子效果
            int dustIndex = Dust.NewDust(target.position, target.width, target.height, DustID.GreenFairy, 0f, 0f, 100, default(Color), 1f);
            Main.dust[dustIndex].velocity *= 2f;
            if (Main.rand.NextBool(2)) {
                Main.dust[dustIndex].scale = 0.5f;
                Main.dust[dustIndex].fadeIn = 1f + Main.rand.Next(10) * 0.05f;
            }
        }
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)//重写碰撞判定
        {
            if (Projectile.localAI[0] < 25) return false;//激光不成形时不判定
            int Length = (int)LaserLength;//定义激光长度
            //这个函数用于控制弹幕碰撞判断，符合你的碰撞条件时返回真即可
            float point = 0f;//这个照抄就行
            Vector2 startPoint = Projectile.Center;//起点在弹幕位置
            Vector2 endPoint = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * Length;//结束点在弹幕速度方向上距离Length像素处的位置
            //结束点在弹幕速度方向上距离1500像素处的位置
            bool K =
                Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), //对方碰撞箱的位置
                targetHitbox.Size(),//对方碰撞箱的大小
                startPoint,//线形碰撞箱起始点
                endPoint,//结束点
                32//线的宽度
                , ref point);
            if (K) return true;//如果满足这个碰撞判断，返回真，也就是进行碰撞伤害
            return base.Colliding(projHitbox, targetHitbox);//如果不满足，调用基类默认的碰撞判断
        }

        public override bool PreDraw(ref Color lightColor)//predraw返回false即可禁用原版绘制
        {
            int Length = (int)LaserLength;//定义激光长度
            //黑色背景的图片如果不对A值赋予0，或者启动Additive模式的话，画出来是黑色，效果很差
            //接下来是简单的延长绘制
            Color color1 = Color.White;//白色绘制就是图片原色
            color1.A = 0;//A赋予0使得图片颜色变为加算,可以去掉黑色部分
            //下面是激光头部的绘制
            Texture2D head = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Projectiles/PufferfishProj2").Value;//获取头部材质
            Main.EntitySpriteDraw(head, Projectile.Center - Main.screenPosition, null,//不需要选框
            color1,//修改后的颜色
            Projectile.velocity.ToRotation(),//让图片朝向为弹幕速度方向
            new Vector2(0, head.Height / 2),//参考原点选择图片左边中点
            new Vector2(1, Projectile.localAI[0] / 25f),//为使得激光更加自然，调整激光宽度
            SpriteEffects.None, 0);//SpriteEffects.None表示不旋转图片
            //下面是激光身体的绘制
            Texture2D tex = TextureAssets.Projectile[Type].Value;//获取材质，这是激光中部
            //Texture2D tex = ModContent.Request<Texture2D>("MyMod2/Projectiles/MyBoss1Proj52").Value;//获取中部材质
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition
                + Projectile.velocity.SafeNormalize(Vector2.Zero) * head.Width,//接在头部后面，所以加上头部长度的方向向量
                new Rectangle(0, 0, Length, tex.Height),//在高度不变的基础上，X轴延长到length
                color1,//修改后的颜色
                Projectile.velocity.ToRotation(),//让图片朝向为弹幕速度方向
                new Vector2(0, tex.Height / 2),//参考原点选择图片左边中点
                new Vector2(1, Projectile.localAI[0] / 25f),//为使得激光更加自然，调整激光宽度
                SpriteEffects.None, 0);//SpriteEffects.None表示不旋转图片
            //下面是激光尾部的绘制
            Texture2D Tail = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Projectiles/PufferfishProj3").Value;//获取尾部材质
            Main.EntitySpriteDraw(Tail, Projectile.Center - Main.screenPosition
             + Projectile.velocity.SafeNormalize(Vector2.Zero) * (head.Width + Length),//接在身体末端的后面
            null,//不需要选框
            color1,//修改后的颜色
            Projectile.velocity.ToRotation(),//让图片朝向为弹幕速度方向
            new Vector2(0, Tail.Height / 2),//参考原点选择图片左边中点
           new Vector2(1, Projectile.localAI[0] / 25f),//为使得激光更加自然，调整激光宽度
            SpriteEffects.None, 0);//尾部不用选框，所以为null
            return false;//return false阻止自动绘制
        }
    }
}
