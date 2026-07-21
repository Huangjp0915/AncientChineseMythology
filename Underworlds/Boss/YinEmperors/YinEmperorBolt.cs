using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors
{
    /// <summary>
    /// 阴天子 - 帝冥弹（多模式）
    /// ai[0] = 模式：0=帝冥追踪弹（原行为）；1=阴兵魂弹（鬼门涌出，直线+正弦漂移，幽魂拉长）；
    ///               2=金符雨（垂直下落的符箓，阴阳诏书危险半场压力）。
    /// ai[1] = 相位种子（模式1/2 的漂移错拍）。
    /// </summary>
    public class YinEmperorBolt : ModProjectile
    {
        public override string Texture => YinEmperorHelper.Path + "ArenaEdge";

        private int Mode => (int)Projectile.ai[0];
        private ref float Phase => ref Projectile.ai[1];

        private float pulsePhase;
        private float homingStrength = 0.025f;
        private float driftTimer;

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

            switch (Mode) {
                case 1: AI_GhostSoldier(); break;
                case 2: AI_TalismanRain(); break;
                default: AI_HomingBolt(); break;
            }

            Lighting.AddLight(Projectile.Center, YinEmperorHelper.ImperialGold.ToVector3() * 0.25f);
        }

        /// <summary>模式0：原帝冥追踪弹。</summary>
        private void AI_HomingBolt() {
            Player target = FindTarget();
            if (target != null && Projectile.timeLeft > 120) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), homingStrength);
            }

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Shadowflame;
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6, 6), dustType);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = -Projectile.velocity * 0.1f;
                d.alpha = 100;
            }
        }

        /// <summary>模式1：阴兵魂弹 —— 直线行进 + 垂直于速度的正弦漂移（队列摆动），不追踪。</summary>
        private void AI_GhostSoldier() {
            driftTimer += 0.09f;
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = forward.RotatedBy(MathHelper.PiOver2);
            // 漂移只影响位置不改航向（弹道可预判）
            Projectile.position += perp * MathF.Sin(driftTimer + Phase) * 1.2f;

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8, 8), DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 1.1f;
                d.velocity = -Projectile.velocity * 0.12f;
                d.alpha = 120;
            }
        }

        /// <summary>模式2：金符雨 —— 垂直缓落，轻微横向摇曳（视觉压力为主）。</summary>
        private void AI_TalismanRain() {
            driftTimer += 0.06f;
            Projectile.velocity.X = MathF.Sin(driftTimer + Phase) * 1.4f;
            if (Projectile.velocity.Y < 7.5f)
                Projectile.velocity.Y += 0.12f;
            Projectile.rotation = Projectile.velocity.X * 0.08f;

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4)) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame);
                d.noGravity = true;
                d.scale = 0.9f;
                d.velocity = new Vector2(0, -1.2f);
                d.alpha = 130;
            }
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
                int dustType = Mode == 1
                    ? DustID.PurpleTorch
                    : (Main.rand.NextBool() ? DustID.GoldFlame : DustID.Shadowflame);
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

            bool ghost = Mode == 1;
            Color themeCol = ghost ? YinEmperorHelper.AbyssPurple : YinEmperorHelper.ImperialGold;

            // 拖尾（阴兵拖尾更长更淡 = 幽魂拉长）
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = themeCol * progress * (ghost ? 0.45f : 0.3f);
                trailColor.A = 0;
                Vector2 trailScale = ghost
                    ? new Vector2(Projectile.scale * (0.3f + progress * 0.5f), Projectile.scale * (0.5f + progress * 0.9f))
                    : new Vector2(Projectile.scale * (0.4f + progress * 0.6f));
                sb.Draw(tex, pos, sourceRect, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
            }

            // 外发光
            Color glowColor = themeCol;
            glowColor.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition, sourceRect,
                glowColor * 0.25f * ((255 - Projectile.alpha) / 255f),
                Projectile.rotation, origin, Projectile.scale * 1.3f * pulse, SpriteEffects.None, 0);

            // 主体（阴兵纵向拉长成魂影）
            Color mainColor = Color.Lerp(lightColor, themeCol, ghost ? 0.55f : 0.4f);
            Vector2 bodyScale = ghost
                ? new Vector2(Projectile.scale * 0.8f, Projectile.scale * 1.5f * pulse)
                : new Vector2(Projectile.scale * pulse);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, sourceRect,
                mainColor * ((255 - Projectile.alpha) / 255f),
                Projectile.rotation, origin, bodyScale, SpriteEffects.None, 0);

            return false;
        }
    }
}
