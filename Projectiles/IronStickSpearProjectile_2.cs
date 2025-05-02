using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace AncientChineseMythology.Projectiles
{
    class IronStickSpearProjectile_2 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/IronStickSpearProjectile";
        private bool isRush = false;

        public override void SetDefaults()
        {
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
        private Player Owner => Main.player[Projectile.owner];
        public override void OnSpawn(IEntitySource source)
        {
            Projectile.knockBack *= 1.5f;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            Vector2 direction = Vector2.Normalize(Main.MouseWorld - player.Center);
            Vector2 mousPos = Main.MouseWorld;

            Projectile.direction = player.direction;
            player.heldProj = Projectile.whoAmI;// 玩家持有弹道

            Projectile.position = player.Center + direction * 0f - new Vector2(Projectile.width / 2, Projectile.height / 2);

            if (!Main.mouseRight)
            {
                Projectile.height = 30;
                Projectile.width = 30;

                Projectile.damage = Projectile.originalDamage * 2;
                Projectile.knockBack = 10f;
                player.immune = true;// 玩家无敌
                player.immuneTime = 2; // 确保无敌时间短于冲刺持续时间

                if (!isRush)
                    Projectile.rotation = direction.ToRotation() + MathHelper.ToRadians(45f);
                isRush = true;
                if (Projectile.timeLeft >= 3)
                {
                    player.velocity = direction * 36f; // 12f * 10 = 120像素

                    Projectile.direction = player.direction;
                    player.heldProj = Projectile.whoAmI;// 玩家持有弹道

                    Projectile.position = player.Center + direction * 0f - new Vector2(Projectile.width / 2, Projectile.height / 2);
                    
                    for (int i = 0; i < 2; i++)
                    {
                        int dust = Dust.NewDust(player.Center, 10, 10, DustID.Silver, 0, 0, 1, default(Color), 1f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].velocity *= 0.2f;
                    }
                }
                else
                {
                    player.velocity *= 0.8f;
                }
            }
            else if (Main.mouseRight && !isRush)
            {
                if (mousPos.X < player.Center.X)
                    Projectile.rotation -= MathHelper.ToRadians(18f);
                else
                    Projectile.rotation += MathHelper.ToRadians(18f);
                Projectile.timeLeft = 13;
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
                    Projectile.rotation - MathHelper.ToRadians(-45f)); // 设置手臂位置（由于手臂起始时低下，所以有 90 度偏移）
                Projectile.damage = Projectile.originalDamage / 2;
                Projectile.knockBack = Projectile.knockBack * 0.99f;

                // 计算右上角位置并生成粒子
                Vector2 dustOffset = new Vector2(60, -60);
                Vector2 rotatedDustOffset = dustOffset.RotatedBy(Projectile.rotation);
                Vector2 dustPosition = Projectile.Center + rotatedDustOffset - new Vector2(8, 5);

                int dust = Dust.NewDust(dustPosition, 10, 10, DustID.Silver, 0, 0, 1, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0.2f;
                Main.dust[dust].scale = 1.2f;
                Main.dust[dust].alpha = 100;

                Vector2 dustOffset_2 = new Vector2(-60, 60);
                Vector2 rotatedDustOffset_2 = dustOffset_2.RotatedBy(Projectile.rotation);
                Vector2 dustPosition_2 = Projectile.Center + rotatedDustOffset_2 - new Vector2(8, 5);

                int dust_2 = Dust.NewDust(dustPosition_2, 10, 10, DustID.Silver, 0, 0, 1, default, 1f);
                Main.dust[dust_2].noGravity = true;
                Main.dust[dust_2].velocity *= 0.2f;
                Main.dust[dust_2].scale = 1.2f;
                Main.dust[dust_2].alpha = 100;
            }
        }
        
        [Obsolete]
        public override void OnKill(int timeLeft)
        {
            Player player = Main.player[Projectile.owner];
            player.velocity *= 0.1f;
        }
        public override bool PreDraw(ref Microsoft.Xna.Framework.Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = new Rectangle(
                0,
                texture.Height / Main.projFrames[Type] * Projectile.frame,
                texture.Width,
                texture.Height / Main.projFrames[Type]
            );

            Vector2 origin = new Vector2(texture.Width / 2, texture.Height / Main.projFrames[Type] / 2); // 设置原点为左下方
            if(!Main.mouseRight)
            {
                origin = new Vector2(32, texture.Height / Main.projFrames[Type] - 32); // 设置原点为左下方
            }
            Main.EntitySpriteDraw(  
                texture,//第一个参数是材质
                Projectile.Center - Main.screenPosition,
                rectangle,//第三个参数是帧图选框
                Color.White,//第四个参数是颜色
                Projectile.rotation,//第五个参数是贴图旋转方向
                origin,
                Projectile.scale*1.2f,//第七个参数是缩放
                SpriteEffects.None,
                0);
            return false;
        }
    }
}
