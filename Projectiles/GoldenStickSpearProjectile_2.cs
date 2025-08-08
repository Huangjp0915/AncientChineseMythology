using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace AncientChineseMythology.Projectiles
{
    internal class GoldenStickSpearProjectile_2 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/GoldenStickSpearProjectile";
        private bool isReturning = false;//是否正在返回

        public override void SetDefaults() {
            Projectile.width = 142;
            Projectile.height = 142;
            Projectile.aiStyle = -1;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 13;
            Projectile.scale = 1.2f;
            Projectile.alpha = 0;
            Projectile.ownerHitCheck = true;
            Projectile.hide = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.damage = Projectile.damage / 2;
            Projectile.knockBack *= 0.2f;
        }

        private void MoveToTarget(Vector2 target)//移动到目标位置
        {
            Vector2 move = target - Projectile.Center;
            float distance = move.Length();
            move.Normalize();
            move *= distance / 20f + 16f;
            Projectile.velocity = move;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            Projectile.direction = player.direction;
            player.heldProj = Projectile.whoAmI;//玩家持有弹道

            if (isReturning) {
                Projectile.timeLeft = 13;
                //回到玩家身边销毁
                MoveToTarget(player.Center); //移动到鼠标位置
                if (Projectile.Distance(player.Center) < 10f) {
                    Projectile.Kill(); //销毁弹道
                }
            }
            if (Main.mouseRight) {
                Projectile.timeLeft = 13;
                if (Main.mouseLeft)
                    isReturning = true;
                if (!isReturning) {
                    MoveToTarget(Main.MouseWorld); //移动到鼠标位置

                    if (Projectile.Distance(Main.MouseWorld) < 10f && Main.mouseRight) {
                        //停止移动以便旋转
                        Projectile.velocity = Vector2.Zero;
                        Projectile.Center = Main.MouseWorld;
                    }
                }
            }
            else if (!isReturning) {
                player.immune = true;//玩家无敌
                player.immuneTime = 30; //确保无敌时间短于冲刺持续时间

                //瞬移玩家到弹幕位置
                player.Teleport(Projectile.Center, 12);
                for (int i = 0; i < 60; i++) //创建50个粒子
                {
                    //使用 Main.dust 来创建粒子
                    Dust dust = Dust.NewDustPerfect(player.Center, DustID.GoldFlame, Main.rand.NextVector2Unit() * 12f, 1, default, 1f);
                    dust.noGravity = true; //使粒子无重力，保持在空中
                    dust.noLight = true; //无光照
                    dust.scale = 2f; //设置粒子大小
                }
                Projectile.Kill(); //销毁弹幕
            }
            if (player.direction == 1)//玩家朝向右侧
            {
                Projectile.rotation += 0.4f; //左右旋转
            }
            else {
                Projectile.rotation -= 0.4f; //左右旋转
            }
            //计算右上角位置并生成粒子
            Vector2 dustOffset = new Vector2(60, -60);
            Vector2 rotatedDustOffset = dustOffset.RotatedBy(Projectile.rotation);
            Vector2 dustPosition = Projectile.Center + rotatedDustOffset - new Vector2(8, 5);

            int dust_1 = Dust.NewDust(dustPosition, 10, 10, DustID.GoldFlame, 0, 0, 1, default, 1f);
            Main.dust[dust_1].noGravity = true;
            Main.dust[dust_1].velocity *= 0.2f;
            Main.dust[dust_1].scale = 1.2f;
            Main.dust[dust_1].alpha = 100;

            Vector2 dustOffset_2 = new Vector2(-60, 60);
            Vector2 rotatedDustOffset_2 = dustOffset_2.RotatedBy(Projectile.rotation);
            Vector2 dustPosition_2 = Projectile.Center + rotatedDustOffset_2 - new Vector2(8, 5);

            int dust_2 = Dust.NewDust(dustPosition_2, 10, 10, DustID.GoldFlame, 0, 0, 1, default, 1f);
            Main.dust[dust_2].noGravity = true;
            Main.dust[dust_2].velocity *= 0.2f;
            Main.dust[dust_2].scale = 1.2f;
            Main.dust[dust_2].alpha = 100;
        }

        [Obsolete]
        public override void OnKill(int timeLeft) {
            Player player = Main.player[Projectile.owner];
            player.velocity *= 0.8f;
        }
        public override bool PreDraw(ref Microsoft.Xna.Framework.Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = new Rectangle(
                0,
                texture.Height / Main.projFrames[Type] * Projectile.frame,
                texture.Width,
                texture.Height / Main.projFrames[Type]
            );

            Vector2 origin = new Vector2(texture.Width / 2, texture.Height / Main.projFrames[Type] / 2); //设置原点为中心
            Main.EntitySpriteDraw(
                texture, //第一个参数是材质
                Projectile.Center - Main.screenPosition,
                rectangle, //第三个参数是帧图选框
                Color.White, //第四个参数是颜色
                Projectile.rotation, //第五个参数是贴图旋转方向
                origin,
                Projectile.scale * 1.2f, //第七个参数是缩放
                SpriteEffects.None,
                0);
            return false;
        }
    }
}