using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Corpseses
{
    /// <summary>
    /// 魂灯球 —— 头颅"魂火吐息"的追魂灯 (V3 重做)。
    /// 公平阀门: 限速 9、近身 60px 停止转向 (可甩掉)、5s 自熄;
    /// 本体用 CorpsesSoulFlame 程序化鬼火绘制, 无贴图依赖。
    /// </summary>
    public class CorpsesShadowOrb : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float MaxSpeed = 9f;
        private const int LifeTime = 300;

        private ref float Timer => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
        }

        public override void AI() {
            Timer++;

            // 前 20 帧缓漂 (换招 wind-up, 防 telefrag); 之后加速追魂
            if (Timer > 20f) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    // 近身停止转向: 贴脸即可甩掉
                    if (toTarget.Length() > 60f) {
                        float speed = MathHelper.Min(MaxSpeed, Projectile.velocity.Length() + 0.14f);
                        Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                            toTarget.SafeNormalize(Vector2.Zero) * speed, 0.045f);
                    }
                }
            }

            // 末段自熄渐灭 (预告消失)
            if (Projectile.timeLeft < 40)
                Projectile.velocity *= 0.95f;

            // 灯芯余烬
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f), DustID.CursedTorch);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = -Projectile.velocity * 0.15f + new Vector2(0f, -0.8f);
            }

            Lighting.AddLight(Projectile.Center, TelegraphColors.GhostGreen.ToVector3() * 0.6f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            // 地府身份层: 魂灯命中叠魂蚀
            UnderworldField.AddSoulErosion(target, 1);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = -0.5f, Volume = 0.7f }, Projectile.position);
            if (!Main.dedServ) {
                for (int i = 0; i < 14; i++) {
                    var d = Dust.NewDustPerfect(Projectile.Center, DustID.CursedTorch, Main.rand.NextVector2Circular(5f, 5f));
                    d.noGravity = true;
                    d.scale = Main.rand.NextFloat(1.4f, 2.2f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            // 灯痕拖尾 (柔光渐隐)
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Vector2 origin = glow.Size() * 0.5f;
                Vector2 back = -Projectile.velocity;
                for (int i = 1; i <= 4; i++) {
                    float p = 1f - i / 5f;
                    Color c = TelegraphColors.GhostGreen with { A = 0 } * (0.28f * p);
                    Main.EntitySpriteDraw(glow, Projectile.Center + back * i * 1.6f - Main.screenPosition, null,
                        c, 0f, origin, 0.7f * p + 0.3f, SpriteEffects.None);
                }
            }

            // 灯体: 程序化魂火 (自熄末段渐灭)
            float fade = MathHelper.Clamp(Projectile.timeLeft / 40f, 0f, 1f);
            float pulse = 1f + 0.1f * MathF.Sin(Timer * 0.2f);
            Corpses.DrawSoulFlame(Main.spriteBatch, Projectile.Center, 0.85f * pulse, fade, Projectile.whoAmI * 1.37f);
            return false;
        }
    }
}
