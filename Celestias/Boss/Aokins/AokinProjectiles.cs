using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    #region ÁúÑæ»ðÇò

    /// <summary>
    /// °½ÇÕ»ðÇò - ´ø×·×ÙµÄ»ðÑæµ¯Ä»
    /// </summary>
    public class AokinFireball : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float firePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
            Projectile.alpha = 0;
        }

        public override void AI() {
            firePhase += 0.12f;

            // Î¢Èõ×·×Ù
            if (Projectile.timeLeft > 220) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float targetAngle = toTarget.ToRotation();
                    float currentAngle = Projectile.velocity.ToRotation();
                    float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.015f);
                    Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            // »ðÑæÁ£×Ó
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 180, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.5f, 0.5f);
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.DragonFlameRed.ToVector3() * 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(firePhase * 2f) * 0.2f;

            // ÍÏÎ²
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AokinHelper.MoltenOrange, AokinHelper.DragonFlameRed, 1f - progress);
                trailColor *= progress * 0.4f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, 0.5f * progress * pulse, SpriteEffects.None, 0f);
            }

            // Íâ²ã¹âÔÎ
            Color outerColor = AokinHelper.DragonFlameRed * 0.35f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, 0f, origin, 0.9f * pulse, SpriteEffects.None, 0f);

            // ÖÐ²ã
            Color midColor = AokinHelper.MoltenOrange * 0.5f * pulse;
            midColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, midColor, 0f, origin, 0.55f * pulse, SpriteEffects.None, 0f);

            // ºËÐÄ
            Color coreColor = AokinHelper.BlazingGold * 0.8f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, 0f, origin, 0.3f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 150, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region ÔÉÊ¯

    /// <summary>
    /// °½ÇÕÔÉÊ¯ - ´Ó¿ÕÖÐÏÂÂäµÄ»ðÑæÔÉÊ¯
    /// </summary>
    public class AokinMeteor : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float firePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            firePhase += 0.1f;
            Projectile.rotation += 0.1f;

            // ¼ÓËÙÏÂÂä
            if (Projectile.velocity.Y < 18f)
                Projectile.velocity.Y += 0.3f;

            // »ðÑæÎ²¼£
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 2; i++) {
                    Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(10, 10);
                    int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, -2, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
                }
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.DragonFlameRed.ToVector3() * 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(firePhase * 3f) * 0.15f;

            // ÍÏÎ²
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AokinHelper.BlazingGold, AokinHelper.DragonFlameRed, 1f - progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, (0.6f + progress * 0.4f) * pulse, SpriteEffects.None, 0f);
            }

            // Íâ²ã¹âÔÎ
            Color outerColor = AokinHelper.DragonFlameRed * 0.4f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, 0f, origin, 1.4f * pulse, SpriteEffects.None, 0f);

            // ÖÐ²ã
            Color midColor = AokinHelper.MoltenOrange * 0.6f * pulse;
            midColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, midColor, 0f, origin, 0.9f * pulse, SpriteEffects.None, 0f);

            // ºËÐÄ
            Color coreColor = AokinHelper.BlazingGold * 0.9f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, 0f, origin, 0.5f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            // ÂäµØ±¬Õ¨Ð§¹û
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }

            // »ðÐÇ·É½¦
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3, 6);
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch);
                d.noGravity = false;
                d.scale = 1.5f;
                d.velocity = vel;
            }
        }
    }

    #endregion

    #region »ðÑæÐýÎÐ

    /// <summary>
    /// °½ÇÕ»ðÑæÐýÎÐ - Í£ÁôÔÚÔ­µØµÄÐý×ª»ðÑæÇøÓò
    /// </summary>
    public class AokinFlameVortex : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float vortexAngle;
        private float vortexAlpha;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            vortexAngle += 0.15f;
            vortexAlpha = MathHelper.Lerp(vortexAlpha, 1f, 0.05f);

            Projectile.velocity *= 0.95f;

            // Ðý×ª»ðÑæÁ£×Ó
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 4; i++) {
                    float angle = vortexAngle + MathHelper.TwoPi * i / 4;
                    float radius = 60f + MathF.Sin(vortexAngle * 2f + i) * 20f;
                    Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * radius;
                    int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 5f;
                }
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.MoltenOrange.ToVector3() * 0.6f * vortexAlpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            float distance = Vector2.Distance(Projectile.Center, targetCenter);
            return distance < 70f;
        }

        public override bool PreDraw(ref Color lightColor) {
            AokinHelper.DrawFlameAura(Main.spriteBatch, Projectile.Center, 70f, vortexAngle, vortexAlpha);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20;
                Vector2 vel = angle.ToRotationVector2() * 5f;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Torch, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region »ðÑæ·âÂ·Áú¾í

    /// <summary>
    /// °½ÇÕ»ðÑæ·âÂ·Áú¾í - Õ½³¡±ß½ç£¬Ê¹ÓÃÔ­°æÉ³³¾±©ÎÆÀí
    /// ¸úËæBoss´æÔÚ£¬ÏÞÖÆÍæ¼Ò»î¶¯·¶Î§
    /// </summary>
    public class AokinBarrierTornado : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float OwnerIndex => ref Projectile.ai[0];
        private ref float Side => ref Projectile.ai[1]; // -1×ó, 1ÓÒ

        private float tornadoRotation;
        private float tornadoAlpha;
        private float tornadoHeight;
        private const float MaxHeight = 1200f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 100;
            Projectile.height = 100;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 99999;
        }

        public override void AI() {
            // ¼ì²éBossÊÇ·ñ´æ»î
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active || owner.type != ModContent.NPCType<Aokin>()) {
                tornadoAlpha -= 0.02f;
                if (tornadoAlpha <= 0f) {
                    Projectile.Kill();
                }
                return;
            }

            // ¸úËæÍæ¼ÒÎ»ÖÃ£¨Á½²à¹Ì¶¨¾àÀë£©
            Player target = Main.player[owner.target];
            if (target.active && !target.dead) {
                float targetX = target.Center.X + Side * 800f;
                Projectile.Center = new Vector2(
                    MathHelper.Lerp(Projectile.Center.X, targetX, 0.02f),
                    target.Center.Y
                );
            }

            // ½¥ÈëÐ§¹û
            tornadoAlpha = MathHelper.Lerp(tornadoAlpha, 1f, 0.02f);
            tornadoHeight = MathHelper.Lerp(tornadoHeight, MaxHeight, 0.03f);
            tornadoRotation += 0.15f;

            // »ðÑæÁ£×ÓÐ§¹û
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 8; i++) {
                    float heightOffset = Main.rand.NextFloat(-tornadoHeight / 2, tornadoHeight / 2);
                    float angle = tornadoRotation + Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = 40f + MathF.Abs(heightOffset / tornadoHeight) * 60f;

                    Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(angle) * radius, heightOffset);
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Torch,
                        1 => DustID.SolarFlare,
                        _ => DustID.Smoke
                    };
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = new Vector2(MathF.Cos(angle + MathHelper.PiOver2) * 6f, Main.rand.NextFloat(-2, 2));
                }
            }

            // ÍÆ¿ªÍæ¼Ò
            foreach (Player player in Main.player) {
                if (!player.active || player.dead) continue;
                float distance = MathF.Abs(player.Center.X - Projectile.Center.X);
                if (distance < 120f) {
                    float pushDirection = player.Center.X > Projectile.Center.X ? 1 : -1;
                    player.velocity.X += pushDirection * 1.5f;
                }
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.DragonFlameRed.ToVector3() * tornadoAlpha * 0.8f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float targetX = targetHitbox.Center.X;
            float distance = MathF.Abs(targetX - Projectile.Center.X);
            float targetY = targetHitbox.Center.Y;
            float heightDiff = MathF.Abs(targetY - Projectile.Center.Y);
            return distance < 60f && heightDiff < tornadoHeight / 2;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            Main.instance.LoadProjectile(ProjectileID.SandnadoHostile);
            Texture2D tornadoTex = TextureAssets.Projectile[ProjectileID.SandnadoHostile].Value;
            Vector2 origin = new Vector2(tornadoTex.Width / 2f, tornadoTex.Height / 2f);

            // »æÖÆ¶à²ã»ðÁú¾í
            int segments = 162;
            for (int seg = 0; seg < segments; seg++) {
                float heightPercent = (float)seg / segments;
                float yOffset = (heightPercent - 0.5f) * tornadoHeight;
                float segRadius = 2.6f + MathF.Abs(heightPercent - 0.5f) * 0.8f - seg * 0.01f;
                float segRot = tornadoRotation + seg * 0.3f;

                Vector2 segPos = screenPos + new Vector2(0, yOffset);

                // Íâ²ã - °µºì
                Color outerColor = AokinHelper.DragonFlameRed * tornadoAlpha * 0.4f;
                outerColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, outerColor, segRot, origin, segRadius * 1.3f, SpriteEffects.None, 0f);

                // ÖÐ²ã - ÈÛÑÒ³È
                Color midColor = AokinHelper.MoltenOrange * tornadoAlpha * 0.6f;
                midColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, midColor, segRot * 1.2f, origin, segRadius, SpriteEffects.None, 0f);

                // ÄÚ²ã - ÁÒÑæ½ð
                Color innerColor = AokinHelper.BlazingGold * tornadoAlpha * 0.3f;
                innerColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, innerColor, segRot * 1.5f, origin, segRadius * 0.7f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    #endregion
}
