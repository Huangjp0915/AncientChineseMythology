using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    public class BlackBearStaffProj1 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Items/Weapons/Summoning Staffs/BlackBearStaff"; // 使用物品的纹理作为投射物的纹理

        Player player => Main.player[Projectile.owner];

        public override void SetDefaults() {
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = true;
            Projectile.width = 22; // 弹幕宽度
            Projectile.height = 18; // 弹幕高度
            Projectile.friendly = true; // 友方弹幕
            Projectile.tileCollide = false; // 不与瓷砖碰撞
            Projectile.DamageType = DamageClass.Summon; // 伤害类型改为召唤伤害
            Projectile.penetrate = -1; // 无限穿透
            Projectile.ignoreWater = true; // 无视液体
            Projectile.timeLeft = 120; // 存在时间无限
            Projectile.alpha = 100; // 透明度
            Projectile.light = 0.75f; // 发光亮度
            Projectile.minion = true; // 设置为召唤物
            Projectile.minionSlots = 0.5f; // 占用一个召唤栏位
            Projectile.aiStyle = -1;//不使用原版AI
            Projectile.rotation = Projectile.velocity.ToRotation(); // 设置初始旋转角度
            base.SetDefaults();
        }
        public override bool? CanCutTiles() {
            return false;//我们不想召唤兽会割草
        }

        void AttackShooting(NPC target) {

            Projectile.ai[0]++;//随便拿一个ai0当计时器
            if (Projectile.ai[0] == 60)//每半秒攻击一次
            {
                Projectile.ai[0] = 0;
                // 获取玩家的位置
                Player player = Main.player[Projectile.owner];
                // 计算方向向量
                Vector2 direction = target.Center - player.Center;
                direction.Normalize();
                int projectileCount = Main.rand.Next(4, 6);
                for (int i = 0; i < projectileCount; i++) {
                    int projectileType = ModContent.ProjectileType<BlackBearStaffProj2>();
                    Vector2 spawnPosition = Projectile.Center;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawnPosition, direction * 16f, projectileType, Projectile.originalDamage, 0, Main.myPlayer);
                }
            }
        }

        public override void AI() {

            if (player.HasBuff<Buffs.BuffsBlackBearStaff>()) // 如果玩家有召唤物BUFF
                Projectile.timeLeft = 2; // 维持住弹幕的时间

            NPC target = null; // 先设出目标NPC，默认为空

            // 这一段是当你的召唤兽设定了右键锁敌情况下必须要写的部分,防止进行寻敌判定
            if (player.HasMinionAttackTargetNPC) {
                target = Main.npc[player.MinionAttackTargetNPC]; // 让目标为鼠标锁住的敌人
                float between = Vector2.Distance(target.Center, Projectile.Center);
                // 小于2000防止锁住太远的敌人
                if (between < 2000f) {
                    target = null;
                }
            }

            if (target == null || !target.active) // 如果目标是空的或者失活的，那么重新寻找敌人
            {
                int t = Projectile.FindTargetWithLineOfSight(1500); // 寻找1500像素范围内最近敌人号码（不隔墙）
                                                                    // 这个方法如果在没有敌怪时会返回-1，用来检测是否能找到敌人
                if (t >= 0)
                    target = Main.npc[t]; // 定义这个NPC为目标
            }

            if (target != null && target.active) // 如果目标不为空且存活在此处执行攻击性AI
            {
                if (target.active) {
                    if (Vector2.Distance(player.Center, target.Center) > 2000)//如果找到的目标距离玩家太远了
                    {
                        Vector2 p = Vector2.Lerp(Projectile.Center, player.Center, 0.1f);
                        Projectile.velocity = p - Projectile.Center;//直接强制回归，不要继续攻击了
                        return;//我们的AI就不需要继续往下走了
                    }
                    AttackShooting(target);//进行攻击AI
                }
            }
            Projectile.velocity = Vector2.Zero; // 弹幕速度清零
            // 使用正弦波使弹幕上下浮动
            float floatAmplitude = 4f; // 漂浮幅度
            float floatSpeed = 0.05f; // 漂浮速度，可以调整以加快或减慢浮动效果

            // 计算浮动的偏移量
            Projectile.position.Y = player.Center.Y - 60 + (float)Math.Sin(Main.GameUpdateCount * floatSpeed) * floatAmplitude;
            Projectile.rotation = 0;
            Projectile.position.X = player.Center.X - 12;
        }


        public override bool PreDraw(ref Color lightColor)//predraw返回false即可禁用原版绘制
        {
            Main.projFrames[Type] = 1;//设置帧数为1，因为我们只需要一个帧的弹幕
            ProjectileID.Sets.TrailingMode[Type] = 2;//设置尾迹模式为2，即尾迹为圆形
            ProjectileID.Sets.TrailCacheLength[Type] = 6;//设置尾迹缓存长度为5，即最多保留5个尾迹
            //同时，需要进行的绘制在这里面写就好

            Texture2D texture = TextureAssets.Projectile[Type].Value;//声明本弹幕的材质
            Rectangle rectangle = new Rectangle(//因为手动绘制需要自己填写帧图框,所以要先算出来
                0,//这个框的左上角的水平坐标(填0就好)
                texture.Height / Main.projFrames[Type] * Projectile.frame,//框的左上角的纵向坐标
                texture.Width, //框的宽度(材质宽度即可)
                texture.Height / Main.projFrames[Type]//框的高度（用材质高度除以帧数得到单帧高度）
                );

            //要制作拖尾，首先要建立一个for循环语句，从0一直走到轨迹末端
            //这里我们介绍一个能产生高亮叠加绘制的办法（A=0）
            Color MyColor = Color.White;
            MyColor.A = 0;//让A=0是为了能直接叠加颜色
            if (player.velocity.X != 0)
                for (int i = 0; i < ProjectileID.Sets.TrailCacheLength[Type]; i++)//循环上限小于轨迹长度
                {
                    float factor = 1 - (float)i / ProjectileID.Sets.TrailCacheLength[Type];//计算当前位置的透明度因子
                                                                                           //定义一个从新到旧由1逐渐减少到0的变量，比如i = 0时，factor = 1
                    Vector2 oldcenter = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;//获取旧位置的中心点
                                                                                                         //由于轨迹只能记录弹幕碰撞箱左上角位置，我们要手动加上弹幕宽高一半来获取中心
                    Main.EntitySpriteDraw(texture, oldcenter, rectangle, MyColor * factor,//颜色逐渐变淡
                        Projectile.oldRot[i],//弹幕轨迹上的曾经的方向
                        new Vector2(texture.Width / 2, texture.Height / 2 / Main.projFrames[Type]),
                         new Vector2(1f),
                         SpriteEffects.None, 0);//最后两个参数是贴图缩放和旋转，这里不用管
                }
            //由于tr绘制是先执行的先绘制，所以要想残影不覆盖到本体上面，就要先写残影绘制

            Main.EntitySpriteDraw(  //entityspritedraw是弹幕，NPC等常用的绘制方法
                texture,//第一个参数是材质
                Projectile.Center - Main.screenPosition,//注意，绘制时的位置是以屏幕左上角为0点
                                                        //因此要用弹幕世界坐标减去屏幕左上角的坐标
                rectangle,//第三个参数就是帧图选框了
                Color.White,//第四个参数是颜色，这里我们用自带的lightcolor，可以受到自然光照影响
                Projectile.rotation,//第五个参数是贴图旋转方向
                new Vector2(texture.Width / 2, texture.Height / 2 / Main.projFrames[Type]),
                //第六个参数是贴图参照原点的坐标，这里写为贴图单帧的中心坐标，这样旋转和缩放都是围绕中心
                new Vector2(1f),//第七个参数是缩放，X是水平倍率，Y是竖直倍率
                SpriteEffects.None,
                //第八个参数是设置图片翻转效果，需要手动判定并设置spriteeffects
                0//第九个参数是绘制层级，但填0就行了，不太好使
                );

            return false;//return false阻止自动绘制
        }
    }
}

