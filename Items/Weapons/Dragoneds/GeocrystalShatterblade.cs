using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Dragoneds
{
    /// <summary>
    /// 地晶裂碐大剑 —— 超级毕业大剑，熔岩主题，每次挥击在落点引发熔岩爆裂
    /// </summary>
    public class GeocrystalShatterblade : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 530;
            Item.DamageType = DamageClass.Melee;
            Item.width = 90;
            Item.height = 90;
            Item.useTime = 26;
            Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 16;
            Item.crit = 20;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<GeocrystalShatterbladeSwing>();
            Item.shootSpeed = 3f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    public class GeocrystalShatterbladeSwing : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Items/Weapons/Dragoneds/GeocrystalShatterblade";
        private const float SWING_RANGE = (float)Math.PI * 1.6f;
        private const float PREP_FRAC = 0.22f;
        private const float EXEC_FRAC = 0.52f;

        private enum Stage { Prepare, Execute, Unwind }
        private ref float Timer => ref Projectile.ai[0];
        private ref float InitAngle => ref Projectile.ai[1];
        private ref float RawProgress => ref Projectile.localAI[0];
        private Stage CurrentStage {
            get => (Stage)Projectile.localAI[1];
            set { Projectile.localAI[1] = (float)value; Timer = 0f; }
        }
        private bool _burstSpawned = false;
        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
        }

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
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
            if (Projectile.spriteDirection == 1)
                InitAngle = MathHelper.Clamp(toMouse, -(float)Math.PI / 2.8f, (float)Math.PI / 5f)
                            - SWING_RANGE * 0.55f;
            else {
                if (toMouse < 0) toMouse += MathHelper.TwoPi;
                InitAngle = MathHelper.Clamp(toMouse, (float)Math.PI * 0.78f, (float)Math.PI * 1.4f)
                            + SWING_RANGE * 0.55f;
            }
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) { Projectile.Kill(); return; }
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            float totalTime = Owner.itemAnimationMax;
            float prepEnd = totalTime * PREP_FRAC;
            float execDur = totalTime * EXEC_FRAC;
            float unwindDur = totalTime * (1f - PREP_FRAC - EXEC_FRAC);

            switch (CurrentStage) {
                case Stage.Prepare:
                    RawProgress = 0f;
                    if (Timer >= prepEnd) {
                        SoundEngine.PlaySound(SoundID.Item71, Owner.position);
                        CurrentStage = Stage.Execute;
                    }
                    break;
                case Stage.Execute:
                    RawProgress = MathHelper.SmoothStep(0f, SWING_RANGE, Math.Min(Timer / execDur, 1f));
                    if (!_burstSpawned && Timer >= execDur * 0.78f) {
                        _burstSpawned = true;
                        Owner.GetModPlayer<ScreenShakePlayer>().ShakeScreen(14f, 20);
                        SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode, Owner.position);
                        Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem),
                            Owner.Center
                            + Owner.DirectionTo(Main.MouseWorld) * 180f * Owner.GetAdjustedItemScale(Owner.HeldItem),
                            Vector2.Zero,
                            ModContent.ProjectileType<GeocrystalBurst>(),
                            (int)(Owner.HeldItem.damage * 0.85f),
                            Owner.HeldItem.knockBack, Owner.whoAmI);
                    }
                    if (Timer >= execDur) CurrentStage = Stage.Unwind;
                    break;
                case Stage.Unwind:
                    RawProgress = MathHelper.Lerp(SWING_RANGE, SWING_RANGE * 1.04f,
                        Math.Min(Timer / unwindDur, 1f));
                    if (Timer >= unwindDur) Projectile.Kill();
                    break;
            }
            float dir = Projectile.spriteDirection;
            Projectile.rotation = InitAngle + dir * RawProgress;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
                Projectile.rotation - MathHelper.PiOver2);
            Vector2 arm = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full,
                Projectile.rotation - MathHelper.PiOver2);
            arm.Y += Owner.gfxOffY;
            Projectile.Center = arm;
            Projectile.scale = 1.3f * Owner.GetAdjustedItemScale(Owner.HeldItem);
            Owner.heldProj = Projectile.whoAmI;
            Timer++;
        }

        public override bool? CanDamage() => CurrentStage == Stage.Execute ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 s = Owner.MountedCenter;
            Vector2 e = s + Projectile.rotation.ToRotationVector2()
                        * Projectile.Size.Length() * Projectile.scale * 1.1f;
            float col = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                s, e, 24f * Projectile.scale, ref col);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            float rotOff = Projectile.spriteDirection > 0
                ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            if (CurrentStage == Stage.Execute) {
                Texture2D wave = ACMAsset.GlaciateWave;
                for (int i = 1; i < 12 && i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                    float a = (1f - i / 12f) * 0.72f;
                    float rot = Projectile.oldRot[i] + rotOff;
                    sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                        new Color(255, 70, 20) * a, rot,
                        new Vector2(wave.Width * 0.5f, wave.Height * 0.5f),
                        Projectile.scale * 0.52f, SpriteEffects.None, 0);
                    sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                        new Color(255, 200, 30) * (a * 0.45f), rot + 0.10f,
                        new Vector2(wave.Width * 0.5f, wave.Height * 0.5f),
                        Projectile.scale * 0.36f, SpriteEffects.None, 0);
                }
                float pulse = 0.8f + 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 0.22f);
                Texture2D sg = ACMAsset.SoftGlow;
                sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 100, 20) * 0.70f * pulse, Projectile.rotation + rotOff,
                    new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                    Projectile.scale * 2.4f, SpriteEffects.None, 0);
                Texture2D sparkle = ACMAsset.Sparkle;
                Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2()
                              * Projectile.Size.Length() * Projectile.scale * 0.6f;
                sb.Draw(sparkle, tip - Main.screenPosition, null,
                    new Color(255, 200, 60) * 0.55f,
                    (float)Main.timeForVisualEffects * 0.06f,
                    new Vector2(sparkle.Width * 0.5f, sparkle.Height * 0.5f),
                    Projectile.scale * 0.75f, SpriteEffects.None, 0);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            SpriteEffects fx = Projectile.spriteDirection < 0
                ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 origin = Projectile.spriteDirection > 0
                ? new Vector2(0, tex.Height) : new Vector2(tex.Width, tex.Height);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                lightColor, Projectile.rotation + rotOff, origin,
                Projectile.scale, fx, 0);
            return false;
        }
    }

    public class GeocrystalBurst : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Items/Weapons/Dragoneds/GeocrystalShatterblade";

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 55;
            Projectile.alpha = 255;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) {
            float prog = 1f - Projectile.timeLeft / 55f;
            float alpha = MathHelper.SmoothStep(0.9f, 0f, prog);
            float scale = MathHelper.SmoothStep(0f, 20f, ACMUtils.QuadOut(prog));
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D burst = ACMAsset.SlashBurst;
            Texture2D em = ACMAsset.EmberShards;
            Texture2D sg = ACMAsset.SoftGlow;
            Texture2D spark = ACMAsset.Sparkle;

            for (int k = 0; k < 4; k++) {
                sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 80, 20) * (alpha * 0.65f), k * MathHelper.PiOver2,
                    new Vector2(burst.Width * 0.5f, burst.Height),
                    scale * 0.52f, SpriteEffects.None, 0);
            }
            sb.Draw(spark, Projectile.Center - Main.screenPosition, null,
                new Color(255, 180, 30) * alpha,
                (float)Main.timeForVisualEffects * 0.015f,
                new Vector2(spark.Width * 0.5f, spark.Height * 0.5f),
                scale * 0.78f, SpriteEffects.None, 0);
            if (prog < 0.5f)
                sb.Draw(em, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 120, 20) * (alpha * 0.55f),
                    prog * MathHelper.TwoPi,
                    new Vector2(em.Width * 0.5f, em.Height * 0.5f),
                    scale * 0.40f, SpriteEffects.None, 0);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(255, 240, 120) * alpha, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                scale * 0.28f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
