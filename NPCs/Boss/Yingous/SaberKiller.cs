using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Yingous
{
    internal class SaberKiller : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/Yingous/YingouHand";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 3;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 84;
            Projectile.tileCollide = false;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.timeLeft = 360;
            Projectile.alpha = 255;
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 1;
        }
        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Vector2 targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
            if (targetPos.Distance(Projectile.Center) < 120) {
                Projectile.velocity /= 2f;
                if (Projectile.ai[2] == 0) {
                    SoundEngine.PlaySound(SoundID.Item89, targetPos);
                    for (int i = 0; i < 115; i++) {
                        Vector2 sparkPos = targetPos + Main.rand.NextVector2Circular(60, 60);
                        int dust = Dust.NewDust(sparkPos, 0, 0, DustID.Torch, 0, 0);
                        Main.dust[dust].velocity = Main.rand.NextVector2Circular(6, 6) * 1.5f;
                        Main.dust[dust].scale = Main.rand.NextFloat(1.2f, 3f);
                        Main.dust[dust].noGravity = true;
                    }
                }
                Projectile.ai[2] = 1f;
            }
            if (Projectile.ai[2] == 1f) {
                Projectile.alpha -= 5;
                if (Projectile.alpha <= 0f) Projectile.Kill();
                Projectile.alpha = (int)MathHelper.Clamp(Projectile.alpha, 0, 255);
            }
        }
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 仅赢勾刀(武器·friendly)命中 NPC 时触发; Boss 自用为 hostile 不会进此分支。
            WeaponVFX.AddScreenShake(target.Center, 2f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Fatal, scale: 1.4f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Color drawColor = Color.White * (Projectile.alpha / 255f);
            float sengs = 0.3f;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                Main.spriteBatch.Draw(value, oldPos, null, drawColor * sengs,
                    Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
                sengs *= 0.9f;
            }
            Main.spriteBatch.Draw(value, Projectile.Center - Main.screenPosition, null, drawColor,
                Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
