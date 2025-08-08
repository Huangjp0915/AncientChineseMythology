using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    public class BlackBearSwordProj1 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Head_Boss"; //使用物品的纹理作为投射物的纹理
        private Vector2 mouseposition; //鼠标目标位置
        public override void SetDefaults() {
            Projectile.width = 20; //弹幕宽度
            Projectile.height = 20; //弹幕高度
            Projectile.friendly = true; //友方弹幕
            Projectile.tileCollide = false; //不与瓷砖碰撞
            Projectile.DamageType = DamageClass.Melee; //伤害类型
            Projectile.penetrate = -1; //穿透
            Projectile.ignoreWater = true; //无视液体
            Projectile.timeLeft = 90; //存在时间，单位为帧
            Projectile.alpha = 1; //透明度
            Projectile.aiStyle = -1;//自定义ai
            Projectile.light = 0.25f; //发光亮度
            Projectile.usesLocalNPCImmunity = true; //独立无敌帧
            Projectile.localNPCHitCooldown = 10; //独立无敌帧时间
        }
        public override void OnSpawn(IEntitySource source) {
            //获取当前显示屏的宽度和高度
            int screenWidth = Main.screenWidth;
            int screenHeight = Main.screenHeight;

            //获取玩家的中心位置
            Vector2 playerCenter = Main.player[Projectile.owner].Center;

            //获取鼠标的位置
            Vector2 mousePosition = Main.MouseWorld;
            mouseposition = mousePosition;//保存
            //计算鼠标位置到玩家位置的连线方向
            Vector2 direction = mousePosition - playerCenter;
            direction.Normalize();

            //计算弹幕生成位置的范围
            float spawnRange = -1000f; //生成位置的范围，可以根据需要调整

            //计算生成位置
            Vector2 spawnPosition = playerCenter + direction * spawnRange;

            //确保生成位置在屏幕边缘
            if (spawnPosition.X < Main.screenPosition.X) {
                spawnPosition.X = Main.screenPosition.X;
            }
            else if (spawnPosition.X > Main.screenPosition.X + screenWidth) {
                spawnPosition.X = Main.screenPosition.X + screenWidth;
            }

            if (spawnPosition.Y < Main.screenPosition.Y) {
                spawnPosition.Y = Main.screenPosition.Y;
            }
            else if (spawnPosition.Y > Main.screenPosition.Y + screenHeight) {
                spawnPosition.Y = Main.screenPosition.Y + screenHeight;
            }

            //设置弹幕的位置
            Projectile.position = spawnPosition;

            //设置弹幕的速度为向鼠标方向，大小为28
            direction = mousePosition - Projectile.position;
            direction.Normalize();
            Projectile.velocity = direction * 26f;

        }
        public override void AI() {
            Projectile.rotation += Projectile.velocity.X * 0.05f; //旋转速度为弹幕速度的 0.05倍

            ////获取鼠标的位置
            //Vector2 mousePosition = Main.MouseWorld;

            //判断弹幕是否到达鼠标位置
            if (Math.Abs(Projectile.velocity.X) > Math.Abs(Projectile.velocity.Y)) {
                //弹幕是从屏幕两边中的一边为起始点出发的
                if (Projectile.velocity.X > 0) {
                    //弹幕是从屏幕左边出发的
                    if (Projectile.position.X >= mouseposition.X) {
                        Projectile.tileCollide = true;
                    }
                }
                else {
                    //弹幕是从屏幕右边出发的
                    if (Projectile.position.X <= mouseposition.X) {
                        Projectile.tileCollide = true;
                    }
                }
            }
            else {
                //弹幕是从屏幕上下中的一边为起始点出发的
                if (Projectile.velocity.Y > 0) {
                    //弹幕是从屏幕上边出发的
                    if (Projectile.position.Y >= mouseposition.Y) {
                        Projectile.tileCollide = true;
                    }
                }
                else {
                    //弹幕是从屏幕下边出发的
                    if (Projectile.position.Y <= mouseposition.Y) {
                        Projectile.tileCollide = true;
                    }
                }
            }
        }
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int dustType = DustID.YellowTorch;
            int dustIndex = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustType);
            Dust dust = Main.dust[dustIndex];
            dust.velocity = Projectile.velocity * 0.2f;
            dust.noGravity = true;
            dust.color = Color.White;
            dust.scale = 1.5f;

            if (Projectile.damage > 1)
                Projectile.damage -= (int)(Projectile.damage * 0.25f);
        }

        [Obsolete]
        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 3; i++) {
                int dustType = DustID.YellowTorch;
                int dustIndex = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustType);
                Dust dust = Main.dust[dustIndex];
                dust.velocity = Projectile.velocity * 1f * Main.rand.NextVector2Circular(0.5f, 1f);
                dust.noGravity = true;
                dust.color = Color.White;
                dust.scale = 1.5f;
            }
        }

        public override bool PreDraw(ref Color lightColor)//predraw返回false即可禁用原版绘制
        {
            Main.projFrames[Type] = 1;//设置帧数为1，因为我们只需要一个帧的弹幕
            ProjectileID.Sets.TrailingMode[Type] = 2;//设置尾迹模式为2，即尾迹为圆形
            ProjectileID.Sets.TrailCacheLength[Type] = 6;//设置尾迹缓存长度为5，即最多保留5个尾迹
            //同时，需要进行的绘制在这里面写就好

            Texture2D texture = TextureAssets.Projectile[Type].Value;//声明本弹幕的材质
            Texture2D texture2 = ModContent.Request<Texture2D>("AncientChineseMythology/Textures/Projectiles/BlackBearSwordProj1").Value;//声明尾迹材质
            Rectangle rectangle = new Rectangle(//因为手动绘制需要自己填写帧图框,所以要先算出来
                0,//这个框的左上角的水平坐标(填0就好)
                texture.Height / Main.projFrames[Type] * Projectile.frame,//框的左上角的纵向坐标
                texture.Width, //框的宽度(材质宽度即可)
                texture.Height / Main.projFrames[Type]//框的高度（用材质高度除以帧数得到单帧高度）
                );
            Rectangle rectangle2 = new Rectangle(//因为手动绘制需要自己填写帧图框,所以要先算出来
                0,//这个框的左上角的水平坐标(填0就好)
                texture2.Height / Main.projFrames[Type] * Projectile.frame,//框的左上角的纵向坐标
                texture2.Width, //框的宽度(材质宽度即可)
                texture2.Height / Main.projFrames[Type]//框的高度（用材质高度除以帧数得到单帧高度）
                );
            //要制作拖尾，首先要建立一个for循环语句，从0一直走到轨迹末端
            //这里我们介绍一个能产生高亮叠加绘制的办法（A=0）
            Color MyColor = Color.White;
            MyColor.A = 0;//让A=0是为了能直接叠加颜色
            for (int i = 0; i < ProjectileID.Sets.TrailCacheLength[Type]; i++)//循环上限小于轨迹长度
            {
                float factor = 1 - (float)i / ProjectileID.Sets.TrailCacheLength[Type];//计算当前位置的透明度因子
                                                                                       //定义一个从新到旧由1逐渐减少到0的变量，比如i = 0时，factor = 1
                Vector2 oldcenter = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;//获取旧位置的中心点
                //由于轨迹只能记录弹幕碰撞箱左上角位置，我们要手动加上弹幕宽高一半来获取中心
                //使用弹幕的速度方向来计算旋转角度
                float rotation = Projectile.velocity.ToRotation();

                Main.EntitySpriteDraw(texture2, oldcenter, rectangle2, Color.White * factor * 0.72f,//颜色逐渐变淡
                    rotation,//使用速度方向的旋转角度
                    new Vector2(texture2.Width / 2, texture2.Height / 2 / Main.projFrames[Type]),
                    new Vector2(0.8f),
                    SpriteEffects.None, 0);//最后两个参数是贴图缩放和旋转，这里不用管
            }

            //由于tr绘制是先执行的先绘制，所以要想残影不覆盖到本体上面，就要先写残影绘制

            Main.EntitySpriteDraw(  //entityspritedraw是弹幕，NPC等常用的绘制方法
                texture,//第一个参数是材质
                Projectile.Center - Main.screenPosition,//注意，绘制时的位置是以屏幕左上角为0点
                                                        //因此要用弹幕世界坐标减去屏幕左上角的坐标
                rectangle,//第三个参数就是帧图选框了
                Color.White,//第四个参数是颜色，这里我们用自带的lightcolor，可以受到自然光照影响
                            //Color.White,
                Projectile.rotation,//第五个参数是贴图旋转方向
                new Vector2(texture.Width / 2, texture.Height / 2 / Main.projFrames[Type]),
                //第六个参数是贴图参照原点的坐标，这里写为贴图单帧的中心坐标，这样旋转和缩放都是围绕中心
                new Vector2(0.8f),//第七个参数是缩放，X是水平倍率，Y是竖直倍率
                SpriteEffects.None,
                //第八个参数是设置图片翻转效果，需要手动判定并设置spriteeffects
                0//第九个参数是绘制层级，但填0就行了，不太好使
                );

            return false;//return false阻止自动绘制
        }
    }
}
