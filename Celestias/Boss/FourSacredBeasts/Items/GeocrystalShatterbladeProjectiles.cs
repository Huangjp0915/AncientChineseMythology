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
    /// <summary>地晶共鸣 — 地晶剑连击积攒，迸射愈烈，满层触发地晶共鸣超载。</summary>
    public class GeocrystalPlayer : ModPlayer
    {
        public const int MaxResonance = 6;
        public int Resonance;
        private int _decayTimer;

        public void AddResonance() {
            Resonance = Math.Min(MaxResonance, Resonance + 1);
            _decayTimer = 120;
        }

        public override void PostUpdate() {
            if (_decayTimer > 0)
                _decayTimer--;
            else if (Resonance > 0) {
                Resonance--;
                _decayTimer = 45;
            }
        }
    }

    internal static class GeocrystalShatterbladeHelper
    {
        public static readonly Color LavaCore = new(255, 130, 35);
        public static readonly Color CrystalGlow = new(110, 200, 255);

        public static void SpawnBurst(IEntitySource source, Vector2 center, int owner, int damage, float knockback, int bonusShards = 0) {
            int count = 5 + bonusShards;

            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi / count * i + Main.rand.NextFloat(-0.22f, 0.22f);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(9f, 13f);
                Projectile.NewProjectile(source, center, vel,
                    ModContent.ProjectileType<GeocrystalBurst>(),
                    (int)(damage * 0.42f),
                    knockback * 0.45f,
                    owner);
            }

            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.1f, Volume = 0.75f }, center);

            for (int i = 0; i < 18; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(7f, 7f);
                int dustType = Main.rand.NextBool(3) ? DustID.Torch : DustID.Stone;
                Dust d = Dust.NewDustDirect(center, 0, 0, dustType, vel.X, vel.Y, 70, default, Main.rand.NextFloat(1.6f, 2.4f));
                d.noGravity = true;
            }

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust d = Dust.NewDustDirect(center, 0, 0, DustID.CopperCoin, vel.X, vel.Y, 50, default, Main.rand.NextFloat(1.2f, 1.8f));
                d.noGravity = true;
            }
        }
    }

    /// <summary>地晶裂碎大剑挥砍 — 持握旋转，命中时触发熔岩晶爆裂。</summary>
    public class GeocrystalShatterbladeSwing : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.BreakerBlade;

        private const float SwingRange = MathF.PI * 1.55f;
        private const float PrepFrac = 0.18f;
        private const float ExecFrac = 0.55f;

        private enum Stage { Prepare, Execute, Unwind }

        private ref float Timer => ref Projectile.ai[1];
        private ref float InitAngle => ref Projectile.ai[2];
        private ref float RawProgress => ref Projectile.localAI[0];
        private int AttackDir => (int)Projectile.ai[0] == 0 ? 1 : -1;

        private Stage CurrentStage {
            get => (Stage)Projectile.localAI[1];
            set { Projectile.localAI[1] = (float)value; Timer = 0f; }
        }

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.timeLeft = 10000;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
            float toMouse = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
            int dir = Projectile.spriteDirection * AttackDir;

            if (dir > 0) {
                toMouse = MathHelper.Clamp(toMouse, -MathF.PI / 2.8f, MathF.PI / 5f);
                InitAngle = toMouse - SwingRange * 0.55f;
            }
            else {
                if (toMouse < 0) toMouse += MathHelper.TwoPi;
                toMouse = MathHelper.Clamp(toMouse, MathF.PI * 0.78f, MathF.PI * 1.4f);
                InitAngle = toMouse + SwingRange * 0.55f;
            }
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;

            float totalTime = Owner.itemAnimationMax;
            float prepEnd = totalTime * PrepFrac;
            float execDur = totalTime * ExecFrac;
            float unwindDur = totalTime * (1f - PrepFrac - ExecFrac);
            int dir = Projectile.spriteDirection * AttackDir;

            switch (CurrentStage) {
                case Stage.Prepare:
                    RawProgress = 0f;
                    if (Timer >= prepEnd) {
                        SoundEngine.PlaySound(SoundID.Item70 with { Pitch = -0.15f, Volume = 0.85f }, Owner.position);
                        CurrentStage = Stage.Execute;
                    }
                    break;

                case Stage.Execute:
                    RawProgress = MathHelper.SmoothStep(0f, SwingRange, Math.Min(Timer / execDur, 1f));

                    if (Main.rand.NextBool(3)) {
                        Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2() * 42f;
                        int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.Stone;
                        Dust d = Dust.NewDustDirect(tip, 0, 0, dustType, 0f, 0f, 80, default, 1.5f);
                        d.noGravity = true;
                        d.velocity = Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2 * dir) * 2.5f;
                    }

                    if (Timer >= execDur) {
                        CurrentStage = Stage.Unwind;
                    }
                    break;

                case Stage.Unwind:
                    RawProgress = MathHelper.Lerp(SwingRange, SwingRange * 1.04f, Math.Min(Timer / unwindDur, 1f));
                    if (Timer >= unwindDur) {
                        Projectile.Kill();
                    }
                    break;
            }

            Projectile.rotation = InitAngle + dir * RawProgress;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            Vector2 arm = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            arm.Y += Owner.gfxOffY;
            Projectile.Center = arm;
            Projectile.scale = 1.25f * Owner.GetAdjustedItemScale(Owner.HeldItem);
            Owner.heldProj = Projectile.whoAmI;
            Timer++;

            Lighting.AddLight(Projectile.Center, GeocrystalShatterbladeHelper.LavaCore.ToVector3() * 0.35f);
        }

        public override bool? CanDamage() => CurrentStage == Stage.Execute ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * Projectile.Size.Length() * Projectile.scale * 1.05f;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 24f * Projectile.scale, ref collisionPoint);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 240);

            GeocrystalPlayer resonance = Owner.GetModPlayer<GeocrystalPlayer>();
            int bonusShards = resonance.Resonance;
            resonance.AddResonance();

            GeocrystalShatterbladeHelper.SpawnBurst(
                Owner.GetSource_ItemUse(Owner.HeldItem),
                target.Center,
                Owner.whoAmI,
                damageDone,
                Projectile.knockBack,
                bonusShards);

            if (bonusShards >= GeocrystalPlayer.MaxResonance) {
                SpawnResonanceOverload(target.Center, damageDone);
            }

            if (Owner.whoAmI == Main.myPlayer) {
                Owner.GetModPlayer<ScreenShakePlayer>().ShakeScreen(5, 10);
            }
        }

        private void SpawnResonanceOverload(Vector2 center, int damageDone) {
            SoundEngine.PlaySound(SoundID.Item70 with { Pitch = -0.4f, Volume = 1f }, center);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int shardType = ModContent.ProjectileType<GeocrystalBurst>();
                for (int i = 0; i < 10; i++) {
                    float ang = MathHelper.TwoPi * i / 10f;
                    Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem), center,
                        ang.ToRotationVector2() * Main.rand.NextFloat(11f, 16f), shardType,
                        (int)(damageDone * 0.5f), Projectile.knockBack * 0.6f, Owner.whoAmI);
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 26; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
                    int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.GemDiamond;
                    Dust d = Dust.NewDustPerfect(center, dustType, vel, 60, default, 2f);
                    d.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int dir = Projectile.spriteDirection * AttackDir;
            float rotOff = dir > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi;

            if (CurrentStage == Stage.Execute && ACMAsset.GlaciateWave != null) {
                Texture2D wave = ACMAsset.GlaciateWave;
                for (int i = 1; i < 12 && i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                    float alpha = (1f - i / 12f) * 0.55f;
                    float rot = Projectile.oldRot[i] + rotOff;
                    Vector2 drawPos = Projectile.Center - Main.screenPosition;

                    Color lavaTrail = GeocrystalShatterbladeHelper.LavaCore * alpha;
                    lavaTrail.A = 0;
                    Main.spriteBatch.Draw(wave, drawPos, null, lavaTrail, rot, wave.Size() * 0.5f,
                        Projectile.scale * 0.42f, SpriteEffects.None, 0f);

                    Color crystalTrail = GeocrystalShatterbladeHelper.CrystalGlow * (alpha * 0.45f);
                    crystalTrail.A = 0;
                    Main.spriteBatch.Draw(wave, drawPos, null, crystalTrail, rot + 0.08f, wave.Size() * 0.5f,
                        Projectile.scale * 0.28f, SpriteEffects.None, 0f);
                }
            }

            Texture2D tex = TextureAssets.Item[ItemID.BreakerBlade].Value;
            SpriteEffects fx = dir < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 origin = dir > 0 ? new Vector2(0, tex.Height) : new Vector2(tex.Width, tex.Height);
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation + rotOff, origin, Projectile.scale, fx, 0f);

            return false;
        }
    }

    /// <summary>地晶熔岩爆裂 — 命中时迸射的熔岩晶碎片。</summary>
    public class GeocrystalBurst : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 36;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            Projectile.velocity *= 0.94f;

            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool(3) ? DustID.Torch : DustID.Stone;
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, 0f, 0f, 80, default, Main.rand.NextFloat(1.1f, 1.7f));
                d.noGravity = true;
                d.velocity = -Projectile.velocity * 0.15f;
            }

            if (Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.CopperCoin, 0f, 0f, 60, default, 1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, GeocrystalShatterbladeHelper.LavaCore.ToVector3() * 0.4f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 120);

            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.Stone;
                Dust d = Dust.NewDustDirect(target.Center, 0, 0, dustType, vel.X, vel.Y, 60, default, 1.5f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Color outer = GeocrystalShatterbladeHelper.LavaCore * 0.55f;
            outer.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outer, Projectile.rotation, origin, 1.1f, SpriteEffects.None, 0f);

            Color core = GeocrystalShatterbladeHelper.CrystalGlow * 0.75f;
            core.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, core, Projectile.rotation * 1.2f, origin, 0.65f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3f, 3f);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.Stone;
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 70, default, 1.3f);
                d.noGravity = true;
            }
        }
    }
}
