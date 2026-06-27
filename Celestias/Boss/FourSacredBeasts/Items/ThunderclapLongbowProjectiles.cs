using AncientChineseMythology.Celestias.Boss.Aoshuns;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Items
{
    /// <summary>雷鼓穿透箭 — 雷鼓长弓普通射击的雷电箭矢。</summary>
    public class ThunderclapArrow : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.JestersArrow;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.rand.NextBool(2)) {
                AoshunHelper.CreateLightningTrail(Projectile.Center, Projectile.velocity, 1.1f);
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.45f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Electrified, 180);

            if (Main.rand.NextBool(3)) {
                AoshunHelper.CreateThunderBurst(target.Center, 70f, 2, 10);
            }

            if (Main.rand.NextBool(3)) {
                ThunderclapSkyStrike.Summon(Projectile, target.Center, 0.7f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, 0);

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoshunHelper.ThunderPurple, AoshunHelper.LightningBlue, 1f - progress);
                trailColor *= progress * 0.55f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i] - MathHelper.PiOver2,
                    origin, new Vector2(0.35f * progress, 0.55f * progress), SpriteEffects.None, 0f);
            }

            Color glow = AoshunHelper.LightningBlue * 0.65f;
            glow.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, glow,
                Projectile.rotation - MathHelper.PiOver2, origin, new Vector2(0.42f, 0.62f), SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.4f);
                d.noGravity = true;
            }
        }
    }

    /// <summary>雷鼓天矢 — 雷鼓长弓满蓄力释放的穿透雷柱箭。</summary>
    public class ThunderclapBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.JestersArrow;

        private float pulsePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 20;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.scale = 1.35f;
        }

        public override void AI() {
            pulsePhase += 0.16f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            Projectile.position += perpendicular * MathF.Sin(pulsePhase * 2.8f) * 1.6f;

            if (Main.rand.NextBool()) {
                AoshunHelper.CreateLightningTrail(Projectile.Center, Projectile.velocity, 1.35f);
            }

            if (Projectile.timeLeft % 6 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(12f, 28f);
                Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Electric, 0, 0, 70, default, 1.8f);
                d.noGravity = true;
                d.velocity = (Projectile.Center - dustPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 6f);
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.75f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Electrified, 300);
            AoshunHelper.CreateThunderBurst(target.Center, 95f, 2, 12);
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.45f, Pitch = 0.2f }, target.Center);

            ThunderclapSkyStrike.Summon(Projectile, target.Center, 1f);

            NPC chain = FindChainTarget(target, 360f);
            if (chain != null) {
                ThunderclapSkyStrike.Summon(Projectile, chain.Center, 0.8f);
            }
        }

        private static NPC FindChainTarget(NPC source, float maxDistance) {
            NPC closest = null;
            float closestDist = maxDistance;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == source.whoAmI || !npc.CanBeChasedBy() || npc.friendly)
                    continue;
                float dist = Vector2.Distance(npc.Center, source.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }
            return closest;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, 0);
            float pulse = 1f + MathF.Sin(pulsePhase * 3f) * 0.08f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoshunHelper.ThunderPurple, AoshunHelper.ElectricWhite, 1f - progress);
                trailColor *= progress * 0.65f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i] - MathHelper.PiOver2,
                    origin, new Vector2(0.55f * progress * pulse, 0.85f * progress * pulse), SpriteEffects.None, 0f);
            }

            Color outerGlow = AoshunHelper.ThunderPurple * 0.45f * pulse;
            outerGlow.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, outerGlow,
                Projectile.rotation - MathHelper.PiOver2, origin, new Vector2(0.75f * pulse, 1.05f * pulse), SpriteEffects.None, 0f);

            Color coreGlow = AoshunHelper.ElectricWhite * 0.55f;
            coreGlow.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, coreGlow,
                Projectile.rotation - MathHelper.PiOver2, origin, new Vector2(0.45f * pulse, 0.65f * pulse), SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            AoshunHelper.CreateThunderBurst(Projectile.Center, 120f, 3, 14);
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.6f, Pitch = 0.35f }, Projectile.Center);
        }
    }

    /// <summary>雷鼓天落 — 雷箭命中后自天而降的穿透落雷柱。</summary>
    public class ThunderclapSkyStrike : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/LightShot";

        private const float StrikeHeight = 640f;
        private const float ColumnWidth = 46f;
        private const int Lifetime = 22;

        private ref float Age => ref Projectile.ai[0];
        private float boltSeed;

        public static void Summon(Projectile source, Vector2 groundPos, float damageScale) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = (int)(source.damage * damageScale);
            Projectile.NewProjectile(source.GetSource_FromThis(), groundPos, Vector2.Zero,
                ModContent.ProjectileType<ThunderclapSkyStrike>(), damage, source.knockBack * 0.5f, source.owner);
        }

        public override void SetDefaults() {
            Projectile.width = (int)ColumnWidth;
            Projectile.height = (int)StrikeHeight;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void OnSpawn(IEntitySource source) {
            boltSeed = Main.rand.NextFloat(1000f);
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.45f, Pitch = 0.4f }, Projectile.Center);
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 16; i++) {
                    Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-ColumnWidth, ColumnWidth), -Main.rand.NextFloat(0f, StrikeHeight));
                    Dust d = Dust.NewDustPerfect(pos, DustID.Electric, Main.rand.NextVector2Circular(2f, 2f), 60, default, 1.6f);
                    d.noGravity = true;
                }
            }
        }

        public override void AI() {
            Age++;
            if (Main.netMode != NetmodeID.Server) {
                Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 1.1f);
                Lighting.AddLight(Projectile.Center - Vector2.UnitY * StrikeHeight * 0.5f, AoshunHelper.ElectricWhite.ToVector3() * 0.8f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Age > 8)
                return false;
            float distX = MathF.Abs(targetHitbox.Center.X - Projectile.Center.X);
            return distX < ColumnWidth * 0.85f && targetHitbox.Center.Y < Projectile.Center.Y + 24f && targetHitbox.Center.Y > Projectile.Center.Y - StrikeHeight;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Electrified, 240);
            AoshunHelper.CreateThunderBurst(target.Center, 80f, 2, 10);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightningBranch;
            if (tex == null)
                return false;

            float fade = 1f - Age / (float)Lifetime;
            float flicker = 0.6f + MathF.Sin((Age + boltSeed) * 1.7f) * 0.4f;
            float alpha = fade * flicker;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 bottom = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height);
            float yScale = StrikeHeight / tex.Height;

            Color outer = AoshunHelper.ThunderPurple * alpha * 0.55f;
            outer.A = 0;
            sb.Draw(tex, bottom, null, outer, 0f, origin, new Vector2(ColumnWidth / tex.Width * 1.6f, yScale), SpriteEffects.None, 0f);

            Color mid = AoshunHelper.LightningBlue * alpha * 0.75f;
            mid.A = 0;
            sb.Draw(tex, bottom, null, mid, 0f, origin, new Vector2(ColumnWidth / tex.Width, yScale), SpriteEffects.None, 0f);

            Color core = AoshunHelper.ElectricWhite * alpha;
            core.A = 0;
            sb.Draw(tex, bottom, null, core, 0f, origin, new Vector2(ColumnWidth / tex.Width * 0.45f, yScale), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
