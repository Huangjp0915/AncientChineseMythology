using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs
{
    #region ÁúÍõË®µ¯

    /// <summary>
    /// ÁúÍõË®µ¯ - »ù´¡×·×ÙË®µ¯
    /// </summary>
    public class DragonWaterBolt : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float waterPhase;

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
            waterPhase += 0.12f;

            // ÇáÎ¢×·×Ù
            if (Projectile.timeLeft > 220) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float targetAngle = toTarget.ToRotation();
                    float currentAngle = Projectile.velocity.ToRotation();
                    float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.018f);
                    Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            // ²¨¶¯Ð§¹û
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            float drift = MathF.Sin(waterPhase * 2f) * 0.6f;
            Projectile.position += perpendicular * drift;

            // Ë®»¨Á£×Ó
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.Wet;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 180, default, 1.3f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.5f, 0.5f);
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(waterPhase * 2f) * 0.2f;

            // ÍÏÎ²
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoGuangHelper.OceanTeal, AoGuangHelper.DragonBlue, 1f - progress);
                trailColor *= progress * 0.4f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.rotation, origin, 0.5f * progress, SpriteEffects.None, 0f);
            }

            // Íâ¹â
            Color outerColor = AoGuangHelper.DragonBlue * 0.5f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, Projectile.rotation, origin, 0.8f * pulse, SpriteEffects.None, 0f);

            // ºËÐÄ
            Color coreColor = AoGuangHelper.WaterGlow * 0.8f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, Projectile.rotation, origin, 0.5f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.Wet;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 150, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region Ë®äöÎÐ

    /// <summary>
    /// Ë®äöÎÐ - ×·×ÙÍæ¼ÒµÄÐýÎÐ
    /// </summary>
    public class WaterVortex : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float TargetPlayer => ref Projectile.ai[0];
        private float vortexAngle;
        private float vortexAlpha = 0f;

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

            // ×·×ÙÍæ¼Ò
            Player target = Main.player[(int)TargetPlayer];
            if (target.active && !target.dead) {
                Vector2 toTarget = target.Center - Projectile.Center;
                if (toTarget.Length() > 30f) {
                    Projectile.velocity = toTarget.SafeNormalize(Vector2.Zero) * 4f;
                }
                else {
                    Projectile.velocity *= 0.9f;
                }
            }

            // ÐýÎÐÁ£×Ó
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 4; i++) {
                    float angle = vortexAngle + MathHelper.TwoPi * i / 4;
                    float radius = 60f + MathF.Sin(vortexAngle * 2f + i) * 20f;
                    Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * radius;
                    int dustType = Main.rand.NextBool() ? DustID.Water : DustID.Wet;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 5f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.OceanTeal.ToVector3() * 0.6f * vortexAlpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            float distance = Vector2.Distance(Projectile.Center, targetCenter);
            return distance < 70f;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;

            // »æÖÆÐýÎÐÔ²»·
            AoGuangHelper.DrawWaterVortex(sb, Projectile.Center, 70f, vortexAngle, vortexAlpha);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20;
                Vector2 vel = angle.ToRotationVector2() * 5f;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Water, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region ³±Ï«²¨

    /// <summary>
    /// ³±Ï«²¨ - À©É¢Ë®»·
    /// </summary>
    public class TidalWave : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float waveRadius = 0f;
        private float waveAlpha = 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 80;
        }

        public override void AI() {
            // À©É¢
            float maxRadius = 700f;
            float progress = 1f - (float)Projectile.timeLeft / 80f;
            waveRadius = maxRadius * ACMUtils.QuadOut(progress);
            waveAlpha = 1f - progress * 0.7f;

            // ²¨ÀËÁ£×Ó
            if (Main.netMode != NetmodeID.Server && Projectile.timeLeft % 2 == 0) {
                int particleCount = 12;
                for (int i = 0; i < particleCount; i++) {
                    float angle = MathHelper.TwoPi * i / particleCount + Main.rand.NextFloat(-0.2f, 0.2f);
                    Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * waveRadius;
                    int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 120, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = angle.ToRotationVector2() * 3f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * waveAlpha * 0.8f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // »·ÐÎÅö×²
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            float distance = Vector2.Distance(Projectile.Center, targetCenter);
            float ringWidth = 50f;
            return distance > waveRadius - ringWidth && distance < waveRadius + ringWidth;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            // »æÖÆÀ©É¢²¨
            AoGuangHelper.DrawTidalWave(sb, Projectile.Center, waveRadius, waveAlpha);

            return false;
        }
    }

    #endregion

    #region Èý²æêªµ¯Ä»

    /// <summary>
    /// Èý²æêªµ¯Ä»
    /// </summary>
    public class TridentProjectile : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float tridentRot;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 200;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            tridentRot += 0.05f;

            // ¼ÓËÙ
            if (Projectile.velocity.Length() < 18f) {
                Projectile.velocity *= 1.02f;
            }

            // Á£×Ó
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.BlueTorch, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.OceanTeal.ToVector3() * 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width * 0.1f, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // ÍÏÎ²
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = AoGuangHelper.DragonBlue * progress * 0.4f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin,
                    new Vector2(0.4f * progress, 0.08f * progress), SpriteEffects.None, 0f);
            }

            // Ö÷Ìå
            Color mainColor = AoGuangHelper.WaterGlow;
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mainColor, Projectile.rotation, origin,
                new Vector2(0.5f, 0.1f), SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.BlueTorch, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region ×·×ÙË®Çò

    /// <summary>
    /// ×·×ÙË®Çò - Ç¿×·×Ù
    /// </summary>
    public class HomingWaterOrb : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float orbPhase;
        private bool isHoming = true;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 15;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            orbPhase += 0.1f;

            // Ç¿×·×Ù
            if (isHoming && Projectile.timeLeft > 120) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float targetAngle = toTarget.ToRotation();
                    float currentAngle = Projectile.velocity.ToRotation();
                    float turnSpeed = Main.expertMode ? 0.07f : 0.05f;
                    float newAngle = MathHelper.Lerp(currentAngle, targetAngle, turnSpeed);

                    float speed = Projectile.velocity.Length();
                    if (speed < 14f) speed += 0.15f;

                    Projectile.velocity = newAngle.ToRotationVector2() * speed;

                    if (toTarget.Length() < 40f) {
                        isHoming = false;
                    }
                }
            }

            Projectile.rotation += 0.12f;

            // Ë®¹âÁ£×Ó
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 150, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(orbPhase * 3f) * 0.15f;

            // ÍÏÎ²
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoGuangHelper.WaterGlow, AoGuangHelper.DragonBlue, 1f - progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, 0.4f * progress, SpriteEffects.None, 0f);
            }

            // Íâ¹â
            Color outerColor = AoGuangHelper.OceanTeal * 0.5f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, 0f, origin, 0.6f * pulse, SpriteEffects.None, 0f);

            // ºËÐÄ
            Color coreColor = AoGuangHelper.WaterGlow * 0.9f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, 0f, origin, 0.35f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 120, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.2f, Volume = 0.5f }, Projectile.Center);
        }
    }

    #endregion
}
