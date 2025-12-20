using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses
{
    /// <summary>
    /// 拍掌冲击波弹幕 - 双手合击产生的环形射弹
    /// </summary>
    public class CorpsesClapWave : ModProjectile
    {
        // 使用原版纹理
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ShadowFlame;
        
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.alpha = 50;
        }

        public override void AI()
        {
            // 初始加速
            if (Projectile.ai[0] < 30f)
            {
                Projectile.velocity *= 1.02f;
                Projectile.ai[0]++;
            }

            // 旋转
            Projectile.rotation += 0.3f;

            // 紫色能量粒子
            if (Main.rand.NextBool(3))
            {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, 
                    DustID.PurpleTorch, 0, 0, 150, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.2f;
            }

            // 发光效果
            Lighting.AddLight(Projectile.Center, 0.5f, 0.2f, 0.8f);

            // 追踪效果（轻微）
            if (Projectile.ai[1] == 1f && Projectile.ai[0] > 30f)
            {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead)
                {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    toTarget.Normalize();
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), 0.03f);
                }
            }
        }

        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.NPCHit54, Projectile.position);

            // 爆发效果
            for (int i = 0; i < 15; i++)
            {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, 
                    DustID.PurpleTorch, 0, 0, 100, default, 2f);
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(5, 5);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // 直接使用已经指定的Texture
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;

            // 绘制发光层
            for (int i = 0; i < 3; i++)
            {
                Vector2 offset = new Vector2(MathF.Cos(Main.GlobalTimeWrappedHourly * 3f + i * MathHelper.TwoPi / 3f), 
                                            MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + i * MathHelper.TwoPi / 3f)) * 4f;
                Color glowColor = new Color(150, 50, 200, 0) * 0.5f;
                Main.EntitySpriteDraw(texture, Projectile.Center + offset - Main.screenPosition, null, 
                    glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None);
            }

            // 绘制拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                float progress = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = new Color(100, 50, 150) * progress * 0.6f;
                
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor, 
                    Projectile.oldRot[i], origin, Projectile.scale * 0.8f, SpriteEffects.None);
            }

            // 绘制主体
            Color mainColor = new Color(180, 80, 255);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, 
                mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None);

            return false;
        }

        public override Color? GetAlpha(Color lightColor)
        {
            return new Color(200, 100, 255, 200);
        }
    }
}
