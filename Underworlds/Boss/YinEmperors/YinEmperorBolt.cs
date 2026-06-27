using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors
{
    /// <summary>
    /// 阴天子 - 帝冥追踪弹
    /// 金紫色追踪能量弹，由冥眼或Boss本体发射
    /// </summary>
    public class YinEmperorBolt : ModProjectile
    {
        public override string Texture => YinEmperorHelper.Path + "ArenaEdge";

        private float pulsePhase;
        private float homingStrength = 0.025f;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 4;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.alpha = 60;
            Projectile.scale = 0.6f;
        }

        public override void AI() {
            pulsePhase += 0.12f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 帧动画
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % 4;
            }

            // 轻微追踪
            Player target = FindTarget();
            if (target != null && Projectile.timeLeft > 120) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), homingStrength);
            }

            // 粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Shadowflame;
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6, 6), dustType);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = -Projectile.velocity * 0.1f;
                d.alpha = 100;
            }

            Lighting.AddLight(Projectile.Center, YinEmperorHelper.ImperialGold.ToVector3() * 0.25f);
        }

        private Player FindTarget() {
            Player closest = null;
            float closestDist = 800f;
            foreach (var p in Main.player) {
                if (p != null && p.active && !p.dead) {
                    float dist = p.Distance(Projectile.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = p;
                    }
                }
            }
            return closest;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<YinJudgmentPlayer>().AddDecreeStack();
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Shadowflame;
                var d = Dust.NewDustPerfect(Projectile.Center, dustType);
                d.noGravity = true;
                d.scale = 1f;
                d.velocity = vel;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            int frameHeight = tex.Height / 4;
            Rectangle sourceRect = new Rectangle(0, Projectile.frame * frameHeight, tex.Width, frameHeight);
            Vector2 origin = new Vector2(tex.Width / 2f, frameHeight / 2f);
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.1f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = YinEmperorHelper.ImperialGold * progress * 0.3f;
                trailColor.A = 0;
                sb.Draw(tex, pos, sourceRect, trailColor, Projectile.oldRot[i], origin,
                    Projectile.scale * (0.4f + progress * 0.6f), SpriteEffects.None, 0);
            }

            // 外发光
            Color glowColor = YinEmperorHelper.ImperialGold;
            glowColor.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, sourceRect,
                glowColor * 0.2f * ((255 - Projectile.alpha) / 255f),
                Projectile.rotation, origin, Projectile.scale * 1.3f * pulse, SpriteEffects.None, 0);

            // 主体
            Color mainColor = Color.Lerp(lightColor, YinEmperorHelper.ImperialGold, 0.4f);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, sourceRect,
                mainColor * ((255 - Projectile.alpha) / 255f),
                Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0);

            return false;
        }
    }
}
