using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles
{
    class WoodenStickSpearProjectile_2 : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/WoodenStickSpearProjectile_2";

        public override void SetDefaults()
        {
            Projectile.width = 86;
            Projectile.height = 86;
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

        public override void OnSpawn(IEntitySource source)
        {
            Projectile.damage += Projectile.damage / 2;
            Projectile.knockBack *= 1.5f;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            Vector2 direction = Vector2.Normalize(Projectile.velocity);
            if(player.direction == -1)
            Projectile.rotation -= MathHelper.ToRadians(20f);
            else
            Projectile.rotation += MathHelper.ToRadians(20f);
            if (Projectile.timeLeft >= 3)
            {
                player.velocity = direction * 26f; // 12f * 10 = 120像素

                Projectile.direction = player.direction;
                player.heldProj = Projectile.whoAmI;// 玩家持有弹道

                Projectile.position = player.Center + direction * 0f - new Vector2(Projectile.width / 2, Projectile.height / 2);
               
                for (int i = 0; i < 2; i++)
                {
                    int dust = Dust.NewDust(player.Center, 10, 10, DustID.Enchanted_Gold, 0, 0, 1, default(Color), 1f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity *= 0.2f;
                }

                for (int i = 0; i < 2; i++)
                {
                    int dust = Dust.NewDust(player.oldPosition + new Vector2(player.width / 2, player.height / 2), 10, 10, DustID.GreenMoss, 0, 0, 1, default(Color), 1f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity *= 0.2f;
                }
            }
            else
            {
                player.velocity *= 0.8f;
            }
            player.immune = true;// 玩家无敌
            player.immuneTime = 2; // 确保无敌时间短于冲刺持续时间
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

            ProjectileID.Sets.TrailingMode[Type] = 2; // 设置尾迹模式为2，即尾迹为圆形
            ProjectileID.Sets.TrailCacheLength[Type] = 8; // 设置尾迹缓存长度为6，即最多保留6个尾迹

            Rectangle rectangle = new Rectangle(
                0,
                texture.Height / Main.projFrames[Type] * Projectile.frame,
                texture.Width,
                texture.Height / Main.projFrames[Type]
            );

            for (int i = 0; i < ProjectileID.Sets.TrailCacheLength[Type]; i++)
            {
                float factor = 1 - (float)i / ProjectileID.Sets.TrailCacheLength[Type];
                Vector2 oldcenter = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                Main.EntitySpriteDraw(texture, oldcenter, rectangle, Color.White * factor,
                    Projectile.oldRot[i],
                    new Vector2(texture.Width / 2, texture.Height / 2 / Main.projFrames[Type]),
                    Projectile.scale * 1.2f,
                    SpriteEffects.None, 0);
            }
            return true;
        }
    }
}
