using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Yingous
{
    /// <summary>
    /// 赢勾尸火 —— 血色鬼火弹。三段个性: 螺旋散开 (ai[2]=旋向/强度) → 脉冲减速定身 → 缓慢追踪。
    /// 追踪段速度封顶且尾段自灭, 保证走位即可甩脱 (压制弹, 非处决弹)。
    /// </summary>
    internal class YingouFireBall : ModProjectile
    {
        private float coreRot;
        private float auraRot;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.timeLeft = 150;
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
                    Dust dCore = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.FireworkFountain_Yellow, 0, 0, 140, default, Main.rand.NextFloat(0.6f, 1f));
                    dCore.noGravity = true;
                    dCore.velocity = Main.rand.NextVector2Circular(1.6f, 1.6f);
                }
                Dust dTail = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.PurpleTorch, Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f, 150,
                    default, Main.rand.NextFloat(0.9f, 1.8f));
                dTail.noGravity = true;
                dTail.velocity *= 0.4f;
            }
            Projectile.ai[0]++;
            if (Projectile.ai[0] < 70) { //螺旋散开
                float jitter = (float)Math.Sin(Projectile.ai[0] * 0.3f) * 0.1f;
                Projectile.velocity = Projectile.velocity.RotatedBy((0.025f + jitter) * Projectile.ai[2]);
            }
            else if (Projectile.ai[0] == 70) { //脉冲减速 — 悬停一拍再扑
                Projectile.velocity *= 0.3f;
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 22; i++) {
                        Vector2 offset = Main.rand.NextVector2Circular(1f, 1f) * 42f;
                        Dust d = Dust.NewDustDirect(Projectile.Center + offset, 0, 0,
                            DustID.GoldFlame, 0f, 0f, 0, default, 1.5f);
                        d.noGravity = true;
                        d.velocity = offset.SafeNormalize(Vector2.Zero) * 3f;
                    }
                }
            }
            else { //缓慢追踪 (封顶 16, 尾段淡出自灭)
                Player player = Projectile.Center.FindClosestPlayer(3200, true);
                if (player != null) {
                    float speedFactor = 0.65f + 0.25f * (float)Math.Sin(Projectile.ai[0] * 0.12f);
                    Vector2 targetSpeed = Projectile.SafeDirectionTo(player.Center) * MathHelper.Lerp(9f, 16f, speedFactor);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetSpeed, 0.055f);
                }
                if (Projectile.timeLeft < 20)
                    Projectile.alpha = (int)MathHelper.Clamp(255f * (1f - Projectile.timeLeft / 20f), 0, 200);
            }
            VaultUtils.ClockFrame(ref Projectile.frame, 6, 6);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) return;
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.PurpleTorch, 0, 0, 140, default, Main.rand.NextFloat(1f, 1.8f));
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(3.5f, 3.5f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = Yingou.SoftGlow;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float fade = 1f - Projectile.alpha / 255f;
            float pulse = 1f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6 + Projectile.whoAmI);
            Color coreColor = Color.Lerp(Color.DarkRed, Color.OrangeRed, 0.5f + 0.5f * (float)Math.Sin(Projectile.ai[0] * 0.1f));
            coreColor.A = 0;
            //拖尾
            float trailAlpha = 0.5f * fade;
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                Vector2 old = Projectile.oldPos[i];
                if (old == Vector2.Zero) continue;
                float t = i / (float)Projectile.oldPos.Length;
                float scale = MathHelper.Lerp(0.9f, 0.2f, t) * pulse;
                Main.spriteBatch.Draw(tex, old + Projectile.Size / 2 - Main.screenPosition, null,
                    coreColor * (trailAlpha * (1 - t)), coreRot + t, tex.Size() / 2, scale, SpriteEffects.None, 0f);
            }
            //外层双光圈
            for (int layer = 0; layer < 2; layer++) {
                float scale = (1.7f + layer * 0.4f) * pulse;
                Color aura = (layer == 0 ? Color.Crimson : Color.Red) * (0.35f * fade);
                aura.A = 0;
                Main.spriteBatch.Draw(tex, drawPos, null, aura, auraRot * (1 + layer), tex.Size() / 2, scale, SpriteEffects.None, 0f);
            }
            //核心
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor * fade, coreRot, tex.Size() / 2, 1.15f * pulse, SpriteEffects.None, 0f);

            tex = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = tex.GetRectangle(Projectile.frame, 7);
            SpriteEffects spriteEffects = Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            Main.spriteBatch.Draw(tex, drawPos, rectangle, Color.White * fade, Projectile.velocity.ToRotation()
                , rectangle.Size() / 2, Projectile.scale * 1.25f, spriteEffects, 0f);
            return false;
        }
    }
}
