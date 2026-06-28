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
    internal static class WhiteTigerPalette
    {
        public static readonly Color SilverGlow = new(220, 225, 245);
        public static readonly Color PaleGold = new(255, 235, 175);
        public static readonly Color RipCrimson = new(210, 35, 45);
    }

    /// <summary>白虎爪 — 四段持握爪击：直刺、反爪、双爪撕裂、终结爪波。</summary>
    public class WhiteTigerClawsSwing : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.FeralClaws;

        private const float PrepFrac = 0.12f;
        private const float ExecFrac = 0.62f;

        private enum Stage { Prepare, Execute, Unwind }

        private ref float Timer => ref Projectile.ai[1];
        private ref float InitAngle => ref Projectile.ai[2];
        private ref float RawProgress => ref Projectile.localAI[0];
        private int AttackType => (int)Projectile.ai[0];

        private Stage CurrentStage {
            get => (Stage)Projectile.localAI[1];
            set { Projectile.localAI[1] = (float)value; Timer = 0f; }
        }

        private bool _ripLaunched;
        private Player Owner => Main.player[Projectile.owner];

        private float SwingRange => AttackType switch {
            0 or 1 => MathF.PI * 0.72f,
            2 => MathF.PI * 1.05f,
            _ => MathF.PI * 1.35f
        };

        private int SwingDir {
            get {
                int side = AttackType switch {
                    0 => 1,
                    1 => -1,
                    _ => Projectile.spriteDirection
                };
                return side;
            }
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
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
            int dir = SwingDir;

            if (dir > 0) {
                toMouse = MathHelper.Clamp(toMouse, -MathF.PI / 3f, MathF.PI / 6f);
                InitAngle = toMouse - SwingRange * 0.5f;
            }
            else {
                if (toMouse < 0) toMouse += MathHelper.TwoPi;
                toMouse = MathHelper.Clamp(toMouse, MathF.PI * 0.82f, MathF.PI * 1.28f);
                InitAngle = toMouse + SwingRange * 0.5f;
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
            int dir = SwingDir;

            switch (CurrentStage) {
                case Stage.Prepare:
                    RawProgress = 0f;
                    if (Timer >= prepEnd) {
                        float pitch = AttackType switch {
                            3 => 0.35f,
                            2 => 0.15f,
                            _ => 0.55f
                        };
                        SoundEngine.PlaySound(SoundID.Item1 with { Pitch = pitch, Volume = 0.8f }, Owner.position);
                        CurrentStage = Stage.Execute;
                    }
                    break;

                case Stage.Execute:
                    RawProgress = MathHelper.SmoothStep(0f, SwingRange, Math.Min(Timer / execDur, 1f));

                    if (AttackType == 3 && !_ripLaunched && Timer >= execDur * 0.38f) {
                        _ripLaunched = true;
                        LaunchClawRips();
                    }

                    if (Timer % 2 == 0) {
                        SpawnClawDust();
                    }

                    if (Timer >= execDur) {
                        CurrentStage = Stage.Unwind;
                    }
                    break;

                case Stage.Unwind:
                    RawProgress = MathHelper.Lerp(SwingRange, SwingRange * 1.02f, Math.Min(Timer / unwindDur, 1f));
                    if (Timer >= unwindDur) {
                        Projectile.Kill();
                    }
                    break;
            }

            Projectile.rotation = InitAngle + dir * RawProgress;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            Vector2 arm = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            arm.Y += Owner.gfxOffY;
            arm += Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * (AttackType % 2 == 0 ? -6f : 6f);
            Projectile.Center = arm;
            Projectile.scale = (AttackType == 3 ? 1.05f : 0.9f) * Owner.GetAdjustedItemScale(Owner.HeldItem);
            Owner.heldProj = Projectile.whoAmI;
            Timer++;

            Lighting.AddLight(Projectile.Center, WhiteTigerPalette.PaleGold.ToVector3() * 0.4f);
        }

        private void LaunchClawRips() {
            Vector2 aim = Owner.DirectionTo(Main.MouseWorld);
            int ripType = ModContent.ProjectileType<WhiteTigerClawRip>();
            int ripDamage = (int)(Owner.GetTotalDamage(DamageClass.Melee).ApplyTo(Owner.HeldItem.damage) * 0.75f);

            for (int i = -1; i <= 1; i++) {
                Vector2 vel = aim.RotatedBy(MathHelper.ToRadians(14f * i)) * 20f;
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem), Owner.Center + aim * 18f, vel,
                    ripType, ripDamage, Owner.HeldItem.knockBack * 0.55f, Owner.whoAmI);
            }

            SoundEngine.PlaySound(SoundID.NPCHit7 with { Pitch = 0.2f, Volume = 0.85f }, Owner.Center);

            if (Owner.whoAmI == Main.myPlayer) {
                Owner.GetModPlayer<ScreenShakePlayer>().ShakeScreen(5, 10);
            }
        }

        private void SpawnClawDust() {
            Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2() * Projectile.Size.Length() * Projectile.scale * 0.55f;
            int dustType = Main.rand.NextBool() ? DustID.Silver : DustID.Blood;
            Color color = dustType == DustID.Blood ? WhiteTigerPalette.RipCrimson : WhiteTigerPalette.SilverGlow;
            Dust d = Dust.NewDustPerfect(tip + Main.rand.NextVector2Circular(6, 6), dustType,
                Main.rand.NextVector2Circular(2f, 2f), 60, color, 1.2f);
            d.noGravity = true;
        }

        public override bool? CanDamage() => CurrentStage == Stage.Execute ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * Projectile.Size.Length() * Projectile.scale * 0.95f;
            float collisionPoint = 0f;
            float hitWidth = AttackType switch {
                3 => 28f,
                2 => 22f,
                _ => 18f
            };
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end,
                hitWidth * Projectile.scale, ref collisionPoint);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
            if (AttackType == 3) {
                modifiers.SourceDamage += 0.25f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int bleedDuration = AttackType switch {
                0 => 180,
                1 => 240,
                2 => 300,
                _ => 420
            };
            target.AddBuff(BuffID.Bleeding, bleedDuration);

            for (int i = 0; i < 6 + AttackType * 2; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood, vel, 50, WhiteTigerPalette.RipCrimson, 1.5f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            int dir = SwingDir;
            float rotOff = dir > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            if (CurrentStage == Stage.Execute && ACMAsset.GlaciateWave != null) {
                Texture2D wave = ACMAsset.GlaciateWave;
                Color trailColor = AttackType == 3
                    ? Color.Lerp(WhiteTigerPalette.PaleGold, WhiteTigerPalette.RipCrimson, 0.35f)
                    : WhiteTigerPalette.SilverGlow;

                for (int i = 1; i < 9 && i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                    float alpha = (1f - i / 9f) * 0.5f;
                    sb.Draw(wave, Projectile.Center - Main.screenPosition, null, trailColor * alpha,
                        Projectile.oldRot[i] + rotOff, wave.Size() * 0.5f,
                        Projectile.scale * 0.32f, SpriteEffects.None, 0);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D tex = TextureAssets.Item[ItemID.FeralClaws].Value;
            SpriteEffects fx = dir < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 origin = dir > 0 ? new Vector2(0, tex.Height) : new Vector2(tex.Width, tex.Height);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation + rotOff, origin, Projectile.scale, fx, 0);
            return false;
        }
    }

    /// <summary>银纹爪波 — 白虎爪终结连击释放的撕裂爪气。</summary>
    public class WhiteTigerClawRip : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.FeralClaws;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 45;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            if (Projectile.timeLeft < 12) {
                Projectile.scale *= 0.94f;
            }

            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool(3) ? DustID.Blood : DustID.Silver;
                Color color = dustType == DustID.Blood ? WhiteTigerPalette.RipCrimson : WhiteTigerPalette.PaleGold;
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10, 10), dustType,
                    -Projectile.velocity * 0.12f, 70, color, 1.3f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, WhiteTigerPalette.PaleGold.ToVector3() * 0.45f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Bleeding, 360);

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood, vel, 50, WhiteTigerPalette.RipCrimson, 1.6f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trail = Color.Lerp(WhiteTigerPalette.SilverGlow, WhiteTigerPalette.RipCrimson, 1f - progress);
                trail *= progress * 0.45f;
                trail.A = 0;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trail, Projectile.oldRot[i], origin,
                    Projectile.scale * progress * 0.85f, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
