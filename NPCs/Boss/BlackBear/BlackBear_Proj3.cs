using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace AncientChineseMythology.NPCs.Boss.BlackBear
{
    public class BlackBear_Proj3 : ModProjectile
    {
        private int attackTimer = 300;
        private int attackDuration = 0; //攻击持续时间
        private Vector2 targetPosition;
        private bool isAttacking = false;
        private int opacityTimer = 0; //透明度计时器

        public override string Texture => "AncientChineseMythology/Textures/NPCs/Boss/BlackBear/BlackBear_Proj3"; //使用物品的纹理作为投射物的纹理

        public override void SetDefaults() {
            Projectile.hostile = true; //敌方伤害
            Projectile.width = 80; //弹幕宽度
            Projectile.height = 56; //弹幕高度
            Projectile.friendly = false; //友方弹幕
            Projectile.tileCollide = false; //不与瓷砖碰撞
            Projectile.DamageType = DamageClass.Default; //伤害类型
            Projectile.penetrate = 1; //穿透
            Projectile.ignoreWater = true; //无视液体
            Projectile.timeLeft = 360; //存在时间，单位为帧
            Projectile.alpha = 1; //透明度
            Projectile.light = 0f; //发光亮度
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.damage = 0; //弹幕伤害为 0
        }

        public override void AI() {
            //透明度变化逻辑
            opacityTimer++;
            Projectile.alpha = (int)(1 + 100 * Math.Sin(opacityTimer * 0.1));

            //使弹幕时刻跟随敌人 BlackBear
            NPC owner = Main.npc[Projectile.owner];
            Player player = Main.player[Main.myPlayer];
            targetPosition = player.Center;
            Vector2 direction = targetPosition - Projectile.Center + new Vector2(0, -100);
            direction.Normalize();
            if (Projectile.Distance(targetPosition + new Vector2(0, -100)) < 20 && !isAttacking) {
                isAttacking = true;
                Projectile.Center = player.Center + new Vector2(0, -100);
            }
            else if (!isAttacking)
                Projectile.velocity = direction * 36f; //设置速度
            else
                Projectile.velocity = Vector2.Zero; //停止移动

            if (owner.life > 1) {
                Projectile.timeLeft = 10;
                attackDuration++;
            }
            if (owner.life <= 1 || owner.type != ModContent.NPCType<BlackBear>() || !owner.active) {
                //扩散的金色粒子
                for (int i = 0; i < 10; i++) {
                    Vector2 position = Projectile.position + new Vector2(Main.rand.Next(-10, 10), Main.rand.Next(-10, 10));
                    int dustType = DustID.Gold;
                    int dustIndex = Dust.NewDust(position, 0, 0, dustType, 0, 0, 100, default);
                    Main.dust[dustIndex].noGravity = true;
                    //Main.dust[dustIndex].velocity *= 0.2f;
                }
                Projectile.Kill(); //如果敌人不再存在，销毁弹幕
            }

            if (isAttacking && attackTimer >= 240) {
                attackTimer--;
            }

            if (isAttacking && attackTimer == 240) {
                int projectileCount = Main.rand.Next(6, 12);
                for (int i = 0; i < projectileCount; i++) {
                    int projectileType = ModContent.ProjectileType<BlackBear_Proj4>();
                    Vector2 spawnPosition = Projectile.Center + new Vector2(Main.rand.NextFloat(-Projectile.width / 2, Projectile.width / 2), -Projectile.height / 2);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawnPosition, Vector2.Zero, projectileType, Projectile.originalDamage, 0, Main.myPlayer);
                }
            }

            if (attackDuration >= 240) {
                isAttacking = false;
                attackDuration = 0;
                attackTimer = 300;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //attackTimer = 0;
            //isAttacking = false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            ProjectileID.Sets.TrailingMode[Type] = 2; //设置尾迹模式为2，即尾迹为圆形
            ProjectileID.Sets.TrailCacheLength[Type] = 8; //设置尾迹缓存长度为8，即最多保留8个尾迹

            Rectangle rectangle = new Rectangle(
               0,
               texture.Height / Main.projFrames[Type] * Projectile.frame,
               texture.Width,
               texture.Height / Main.projFrames[Type]
           );
            if (Projectile.velocity.Length() > 0.1f)
                for (int i = 0; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                    float factor = 1 - (float)i / ProjectileID.Sets.TrailCacheLength[Type];
                    Vector2 oldcenter = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                    Main.EntitySpriteDraw(texture, oldcenter, rectangle, Color.White * factor * 0.8f * Projectile.Opacity,
                        Projectile.oldRot[i],
                        new Vector2(texture.Width / 2, texture.Height / 2 / Main.projFrames[Type]),
                        Projectile.scale * 0.8f,
                        SpriteEffects.None, 0);
                }

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                rectangle,
                Color.White * Projectile.Opacity, //使用纯白颜色
                Projectile.rotation,
                new Vector2(texture.Width / 2, texture.Height / Main.projFrames[Type] / 2),
                Projectile.scale * 0.8f,
                SpriteEffects.None,
                0);
            return false;
        }
    }
}
