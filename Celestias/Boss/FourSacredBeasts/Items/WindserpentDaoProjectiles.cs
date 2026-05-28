using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Items
{
    /// <summary>风蛇刀气 — 风蛇长刀普通挥砍释放的蛇形风刃。</summary>
    public class WindserpentSlash : ModProjectile
    {
        private float serpentPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            serpentPhase += 0.18f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            Projectile.position += perpendicular * MathF.Sin(serpentPhase * 2.5f) * 2.2f;

            if (Projectile.timeLeft < 18) {
                Projectile.scale *= 0.94f;
            }

            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(12, 12),
                    0, 0, DustID.GreenTorch, 0, 0, 100, default, 1.3f);
                d.noGravity = true;
                d.velocity = -Projectile.velocity * 0.12f;
            }

            Lighting.AddLight(Projectile.Center, 0.25f, 0.55f, 0.2f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                Dust d = Dust.NewDustDirect(target.Center, 0, 0, DustID.GreenTorch, vel.X, vel.Y, 80, default, 1.5f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, texture.Height / 2f);

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(new Color(100, 255, 150), new Color(30, 160, 60), 1f - progress);
                trailColor *= progress * 0.45f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i],
                    origin, new Vector2(0.9f * progress, 0.18f * progress * Projectile.scale), SpriteEffects.None, 0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color mainColor = new Color(120, 255, 160);
            mainColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, mainColor, Projectile.rotation,
                origin, new Vector2(0.85f, 0.2f) * Projectile.scale, SpriteEffects.None, 0f);

            Color coreColor = new Color(210, 255, 220);
            coreColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, coreColor * 0.75f, Projectile.rotation,
                origin, new Vector2(0.55f, 0.1f) * Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3, 3);
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.GreenTorch, vel.X, vel.Y, 80, default, 1.3f);
                d.noGravity = true;
            }
        }
    }

    /// <summary>横扫龙卷 — 风蛇长刀每四刀释放的横扫风龙卷。</summary>
    public class WindserpentSweepTornado : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float tornadoRotation;
        private float tornadoAlpha;
        private float tornadoHeight;
        private const float MaxHeight = 420f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;
        }

        public override void SetDefaults() {
            Projectile.width = 56;
            Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            tornadoRotation += 0.22f;
            tornadoAlpha = MathHelper.Lerp(tornadoAlpha, 1f, 0.05f);
            tornadoHeight = MathHelper.Lerp(tornadoHeight, MaxHeight, 0.06f);

            NPC target = FindClosestNPC(520f);
            if (target != null) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Vector2 desired = toTarget * 7f;
                desired += Projectile.velocity.SafeNormalize(Vector2.Zero) * 2f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.04f);
            }
            else {
                Projectile.velocity *= 0.97f;
            }

            foreach (NPC npc in Main.npc) {
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                float distance = Vector2.Distance(npc.Center, Projectile.Center);
                if (distance < 170f && distance > 25f) {
                    Vector2 pullDir = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    npc.velocity += pullDir * 0.55f;
                }
            }

            if (Main.rand.NextBool(2)) {
                float heightOffset = Main.rand.NextFloat(-tornadoHeight / 2, tornadoHeight / 2);
                float angle = tornadoRotation + Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = 24f + MathF.Abs(heightOffset / tornadoHeight) * 42f;
                Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(angle) * radius, heightOffset);
                Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GreenTorch, 0, 0, 120, default, 1.7f);
                d.noGravity = true;
                d.velocity = new Vector2(MathF.Cos(angle + MathHelper.PiOver2) * 4f, Main.rand.NextFloat(-1.5f, 1.5f));
            }

            Lighting.AddLight(Projectile.Center, 0.2f, 0.5f, 0.18f);
        }

        private NPC FindClosestNPC(float maxDistance) {
            NPC closest = null;
            float closestDist = maxDistance;

            foreach (NPC npc in Main.npc) {
                if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.CanBeChasedBy()) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float distance = MathF.Abs(targetHitbox.Center.X - Projectile.Center.X);
            float heightDiff = MathF.Abs(targetHitbox.Center.Y - Projectile.Center.Y);
            return distance < 48f && heightDiff < tornadoHeight / 2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 120);

            for (int i = 0; i < 10; i++) {
                float angle = tornadoRotation + MathHelper.TwoPi * i / 10;
                Vector2 vel = angle.ToRotationVector2() * 5f;
                Dust d = Dust.NewDustDirect(target.Center, 0, 0, DustID.GreenTorch, vel.X, vel.Y, 90, default, 2f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;

            Main.instance.LoadProjectile(ProjectileID.SandnadoHostile);
            Texture2D tornadoTex = TextureAssets.Projectile[ProjectileID.SandnadoHostile].Value;
            Vector2 origin = tornadoTex.Size() / 2f;

            int segments = 12;
            for (int seg = 0; seg < segments; seg++) {
                float heightPercent = (float)seg / segments;
                float yOffset = (heightPercent - 0.5f) * tornadoHeight;
                float segRadius = 0.45f + MathF.Abs(heightPercent - 0.5f) * 0.55f;
                float segRot = tornadoRotation + seg * 0.32f;
                Vector2 segPos = screenPos + new Vector2(0, yOffset);

                Color outerColor = new Color(60, 220, 100) * tornadoAlpha * 0.4f;
                outerColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, outerColor, segRot, origin, segRadius * 1.25f, SpriteEffects.None, 0f);

                Color midColor = new Color(100, 255, 140) * tornadoAlpha * 0.55f;
                midColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, midColor, segRot * 1.2f, origin, segRadius, SpriteEffects.None, 0f);

                Color innerColor = new Color(180, 255, 200) * tornadoAlpha * 0.3f;
                innerColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, innerColor, segRot * 1.45f, origin, segRadius * 0.65f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.1f, Volume = 0.7f }, Projectile.Center);

            for (int i = 0; i < 18; i++) {
                float angle = MathHelper.TwoPi * i / 18;
                Vector2 vel = angle.ToRotationVector2() * 6f;
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.GreenTorch, vel.X, vel.Y, 90, default, 2f);
                d.noGravity = true;
            }
        }
    }
}
