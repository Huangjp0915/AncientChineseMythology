using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses
{
    /// <summary>
    /// 骨头弹幕 - 受重力影响的泼洒攻击
    /// </summary>
    public class CorpsesBoneShower : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.alpha = 0;
        }

        public override void AI() {
            // 重力效果
            Projectile.velocity.Y += 0.3f;
            if (Projectile.velocity.Y > 16f)
                Projectile.velocity.Y = 16f;

            // 旋转
            Projectile.rotation += Projectile.velocity.X * 0.05f;

            // 动画
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 8) {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= Main.projFrames[Projectile.type])
                    Projectile.frame = 0;
            }

            // 紫色粒子效果
            if (Main.rand.NextBool(4)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Projectile.velocity * 0.3f;
            }

            // 轻微的左右摇摆
            Projectile.ai[0] += 0.1f;
            Projectile.velocity.X += MathF.Sin(Projectile.ai[0]) * 0.1f;
            Projectile.scale += 0.01f;

            // 发光
            Lighting.AddLight(Projectile.Center, Color.BlueViolet.ToVector3() * 3);
        }

        public override void OnKill(int timeLeft) {
            // 落地音效
            SoundEngine.PlaySound(SoundID.Dig, Projectile.position);

            // 爆发粒子
            for (int i = 0; i < 10; i++) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame, 0, 0, 100, default, 1.5f);
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(3, 3);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 绘制拖尾
            Texture2D texture = ModContent.Request<Texture2D>("Terraria/Images/Projectile_" + ProjectileID.Bone).Value;
            Vector2 origin = texture.Size() / 2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float progress = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = new Color(150, 50, 200) * progress * 0.5f;

                Main.EntitySpriteDraw(texture, drawPos, null, trailColor,
                    Projectile.oldRot[i], origin, Projectile.scale * 0.9f, SpriteEffects.None);
            }

            // 绘制主体（使用原版骨头纹理并染成紫色）
            Color mainColor = Color.Lerp(lightColor, new Color(150, 50, 200), 0.6f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None);

            return false;
        }
    }
}
