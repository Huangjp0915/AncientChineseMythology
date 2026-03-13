using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Arguses
{
    /// <summary>
    /// 羽翼光刃 — 白色和蓝色像素构成的羽翼状光刃
    /// </summary>
    public class AetherealWingblades : ModProjectile
    {
        private float glidePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            // ai[0]==1: 光刃地雷模式——静止等待，玩家接近时激活追踪
            if (Projectile.ai[0] == 1f) {
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation += 0.03f;
                glidePhase += 0.08f;

                // 休眠脉冲粒子
                if (Main.rand.NextBool(4)) {
                    Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(16, 16),
                        0, 0, DustID.BlueTorch, 0, 0, 150, default, 0.8f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }

                Lighting.AddLight(Projectile.Center, 0.2f, 0.2f, 0.4f);

                // 检测玩家接近——激活追踪
                float detectionRange = 160f;
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player p = Main.player[i];
                    if (p.active && !p.dead && Projectile.Distance(p.Center) < detectionRange) {
                        // 激活——切换为追踪模式
                        Projectile.ai[0] = 2f;
                        Projectile.ai[1] = i; // 追踪目标
                        Projectile.netUpdate = true;

                        SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.6f }, Projectile.Center);
                        for (int d = 0; d < 8; d++) {
                            Dust dust = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.BlueTorch, 0, 0, 60, default, 1.8f);
                            dust.noGravity = true;
                            dust.velocity = Main.rand.NextVector2Circular(5, 5);
                        }
                        break;
                    }
                }
                return;
            }

            // ai[0]==2: 追踪模式——被激活后追踪目标玩家
            if (Projectile.ai[0] == 2f) {
                int targetIdx = (int)Projectile.ai[1];
                if (targetIdx >= 0 && targetIdx < Main.maxPlayers) {
                    Player p = Main.player[targetIdx];
                    if (p.active && !p.dead) {
                        Vector2 toTarget = (p.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                        Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 14f, 0.06f);
                    }
                }
                Projectile.rotation = Projectile.velocity.ToRotation();
                glidePhase += 0.15f;

                if (Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(14, 14),
                        0, 0, DustID.BlueTorch, 0, 0, 80, default, 1.5f);
                    d.noGravity = true;
                }

                Lighting.AddLight(Projectile.Center, 0.5f, 0.5f, 0.8f);
                return;
            }

            // 默认: 飞行光刃
            Projectile.rotation = Projectile.velocity.ToRotation();
            glidePhase += 0.12f;

            // 白蓝羽翼粒子
            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool(3) ? DustID.Ice : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(14, 14),
                    0, 0, dustType,
                    -Projectile.velocity.X * 0.12f, -Projectile.velocity.Y * 0.12f,
                    100, default, 1.3f);
                d.noGravity = true;
                d.fadeIn = 1.3f;
            }

            Lighting.AddLight(Projectile.Center, 0.4f, 0.4f, 0.7f);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                int dustType = i % 3 == 0 ? DustID.Ice : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, 0, 0, 80, default, 1.4f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(5, 5);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;

            float pulse = 1f + MathF.Sin(glidePhase * 3f) * 0.1f;

            // 白蓝残影
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float t = (float)i / Projectile.oldPos.Length;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = Color.Lerp(new Color(220, 230, 255), new Color(80, 140, 255), t) * (0.5f * (1f - t));
                sb.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * (1f - t * 0.3f), SpriteEffects.None, 0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color mainColor = Color.Lerp(new Color(230, 240, 255), new Color(130, 170, 255), MathF.Sin(glidePhase) * 0.5f + 0.5f);
            sb.Draw(texture, drawPos, null, mainColor, Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0f);

            return false;
        }
    }
}
