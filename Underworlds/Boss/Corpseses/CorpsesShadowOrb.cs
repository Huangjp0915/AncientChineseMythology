using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses
{
    /// <summary>
    /// 暗影能量球 - 追踪型弹幕
    /// </summary>
    public class CorpsesShadowOrb : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private float rotationSpeed = 0f;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
        }

        public override void AI() {
            // 追踪玩家
            Projectile.ai[0]++;

            if (Projectile.ai[0] > 20f) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float distance = toTarget.Length();

                    if (distance > 50f) {
                        toTarget.Normalize();
                        float speed = MathHelper.Min(12f, Projectile.velocity.Length() + 0.15f);
                        Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * speed, 0.04f);
                    }
                }
            }

            // 旋转加速
            rotationSpeed += 0.02f;
            Projectile.rotation += rotationSpeed;

            // 动画
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= Main.projFrames[Projectile.type])
                    Projectile.frame = 0;
            }

            // 环绕粒子
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 20f;
                int dust = Dust.NewDust(Projectile.Center + offset, 0, 0, DustID.Shadowflame, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -offset.SafeNormalize(Vector2.Zero) * 2f;
            }

            // 发光
            Lighting.AddLight(Projectile.Center, 0.6f, 0.2f, 0.9f);

            // 脉冲效果
            float pulse = (float)Math.Sin(Projectile.ai[0] * 0.2f) * 0.1f + 1f;
            Projectile.scale = pulse;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = -0.5f }, Projectile.position);

            // 爆炸效果
            for (int i = 0; i < 20; i++) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame, 0, 0, 100, default, 2.5f);
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(8, 8);
                Main.dust[dust].noGravity = true;
            }

            // 产生小型冲击波
            if (Main.myPlayer == Projectile.owner) {
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6f;
                    Vector2 velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 4f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity,
                        ModContent.ProjectileType<CorpsesClapWave>(), Projectile.damage / 2, 0f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 使用多层绘制创建能量球效果
            Texture2D glowTexture = ModContent.Request<Texture2D>("Terraria/Images/Misc/Perlin").Value;
            Vector2 origin = glowTexture.Size() / 2f;

            // 外层发光
            for (int i = 0; i < 4; i++) {
                float angle = Projectile.ai[0] * 0.1f + i * MathHelper.PiOver2;
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 6f;
                Color glowColor = new Color(100, 30, 150, 0) * 0.4f;

                Main.EntitySpriteDraw(glowTexture, Projectile.Center + offset - Main.screenPosition, null,
                    glowColor, Projectile.rotation, origin, Projectile.scale * 0.3f, SpriteEffects.None);
            }

            // 绘制拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float progress = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = new Color(150, 50, 200) * progress * 0.5f;

                Main.EntitySpriteDraw(glowTexture, drawPos, null, trailColor,
                    Projectile.oldRot[i], origin, Projectile.scale * 0.25f * progress, SpriteEffects.None);
            }

            // 核心
            Color coreColor = new Color(200, 100, 255);
            Main.EntitySpriteDraw(glowTexture, Projectile.Center - Main.screenPosition, null,
                coreColor, Projectile.rotation, origin, Projectile.scale * 0.25f, SpriteEffects.None);

            return false;
        }

        public override Color? GetAlpha(Color lightColor) {
            return new Color(200, 100, 255, 150);
        }
    }
}
