using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Yingous
{
    internal class YingouFireBall : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/SoftGlow";//基础核芯贴图
        private float coreRot;
        private float auraRot;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.timeLeft = 220;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.hostile = true;
        }

        public static void KillAll() {
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != ModContent.ProjectileType<YingouFireBall>()) continue;
                proj.Kill();
                proj.netUpdate = true;
            }
        }

        public override void AI() {
            coreRot += 0.12f * Projectile.direction;
            auraRot -= 0.06f * Projectile.direction;
            if (!VaultUtils.isServer) {
                if (Main.rand.NextBool(2)) {
                    int dustCore = Dust.NewDust(Projectile.Center, 0, 0, DustID.FireworkFountain_Yellow, 0, 0, 140, default, Main.rand.NextFloat(0.6f, 1f));
                    Main.dust[dustCore].noGravity = true;
                    Main.dust[dustCore].velocity = Main.rand.NextVector2Circular(1.6f, 1.6f);
                }
                int dustTail = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.PurpleTorch, Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f, 150,
                    default, Main.rand.NextFloat(0.9f, 1.8f));
                Main.dust[dustTail].noGravity = true;
                Main.dust[dustTail].velocity *= 0.4f;
            }
            Projectile.ai[0]++;
            if (Projectile.ai[0] < 80) { //螺旋阶段
                float jitter = (float)Math.Sin(Projectile.ai[0] * 0.3f) * 0.1f;
                Projectile.velocity = Projectile.velocity.RotatedBy((0.025f + jitter) * Projectile.ai[2]);
            }
            else if (Projectile.ai[0] == 80) { //脉冲减速
                Projectile.velocity *= 0.3f;
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 28; i++) {
                        Vector2 offset = Main.rand.NextVector2Circular(1f, 1f) * 46f;
                        int dust = Dust.NewDust(Projectile.Center + offset, 0, 0,
                            DustID.GoldFlame, 0f, 0f, 0, default, 1.6f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].velocity = offset.SafeNormalize(Vector2.Zero) * 3.2f;
                    }
                }
            }
            else { //追踪 / 加速脉动
                Player player = Projectile.Center.FindClosestPlayer(3200, true);
                if (player != null) {
                    float speedFactor = 0.65f + 0.25f * (float)Math.Sin(Projectile.ai[0] * 0.12f);
                    Vector2 targetSpeed = Projectile.SafeDirectionTo(player.Center) * MathHelper.Lerp(10f, 22f, speedFactor * 0.3f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetSpeed, 0.06f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = Yingou.SoftGlow;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6 + Projectile.whoAmI);
            Color coreColor = Color.Lerp(Color.Gold, Color.OrangeRed, 0.5f + 0.5f * (float)Math.Sin(Projectile.ai[0] * 0.1f));
            coreColor.A = 0;
            // 画拖尾
            float trailAlpha = 0.5f;
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                Vector2 old = Projectile.oldPos[i];
                if (old == Vector2.Zero) continue;
                float t = i / (float)Projectile.oldPos.Length;
                float scale = MathHelper.Lerp(0.9f, 0.2f, t) * pulse;
                Main.spriteBatch.Draw(tex, old + Projectile.Size / 2 - Main.screenPosition, null,
                    coreColor * (trailAlpha * (1 - t)), coreRot + t, tex.Size() / 2, scale, SpriteEffects.None, 0f);
            }
            // 外层双层光圈
            for (int layer = 0; layer < 2; layer++) {
                float scale = (1.7f + layer * 0.4f) * pulse;
                Color aura = (layer == 0 ? Color.Crimson : Color.White) * 0.35f;
                aura.A = 0;
                Main.spriteBatch.Draw(tex, drawPos, null, aura, auraRot * (1 + layer), tex.Size() / 2, scale, SpriteEffects.None, 0f);
            }
            // 核心
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, coreRot, tex.Size() / 2, 1.15f * pulse, SpriteEffects.None, 0f);
            return false;
        }
    }
}
