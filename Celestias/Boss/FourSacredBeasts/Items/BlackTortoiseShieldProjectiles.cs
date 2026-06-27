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
    /// <summary>玄龟盾格挡反伤逻辑。</summary>
    public class BlackTortoiseShieldPlayer : ModPlayer
    {
        private int reflectCooldown;
        private int guardNovaCooldown;

        public bool HasShieldEquipped => Player.HeldItem.type == ModContent.ItemType<BlackTortoiseShield>();

        public override void PreUpdate() {
            if (reflectCooldown > 0)
                reflectCooldown--;
            if (guardNovaCooldown > 0)
                guardNovaCooldown--;
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            if (!HasShieldEquipped || !Player.shieldRaised)
                return;

            modifiers.FinalDamage *= 1f - BlackTortoiseShield.BlockDamageReduction;
        }

        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            if (!HasShieldEquipped || !Player.shieldRaised)
                return;

            modifiers.FinalDamage *= 1f - BlackTortoiseShield.BlockDamageReduction;
        }

        public override void OnHitByNPC(NPC npc, Player.HurtInfo hurtInfo) {
            TryBlockReflect(npc, hurtInfo);
        }

        public override void OnHitByProjectile(Projectile proj, Player.HurtInfo hurtInfo) {
            if (!HasShieldEquipped || !Player.shieldRaised || proj.friendly || !proj.hostile)
                return;

            int npcIndex = ResolveShooterIndex(proj);
            if (npcIndex >= 0)
                TryBlockReflect(Main.npc[npcIndex], hurtInfo);
        }

        private void TryBlockReflect(NPC npc, Player.HurtInfo hurtInfo) {
            if (!HasShieldEquipped || !Player.shieldRaised || reflectCooldown > 0)
                return;

            if (!npc.active || npc.friendly || npc.dontTakeDamage)
                return;

            reflectCooldown = 10;

            int reflectDamage = Math.Max(
                Player.HeldItem.damage / 2,
                (int)(hurtInfo.SourceDamage * BlackTortoiseShield.ReflectMultiplier));

            int hitDir = npc.Center.X >= Player.Center.X ? 1 : -1;
            npc.SimpleStrikeNPC(reflectDamage, hitDir, false, Player.HeldItem.knockBack, null, false, 0, true);

            if (guardNovaCooldown <= 0 && Player.whoAmI == Main.myPlayer) {
                guardNovaCooldown = 45;
                Projectile.NewProjectile(Player.GetSource_Misc("BlackTortoiseGuard"), Player.Center, Vector2.Zero,
                    ModContent.ProjectileType<BlackTortoiseGuardNova>(), Math.Max(Player.HeldItem.damage, reflectDamage),
                    Player.HeldItem.knockBack * 1.5f, Player.whoAmI);
            }

            if (VaultUtils.isServer)
                return;

            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.65f, Pitch = -0.1f }, Player.Center);

            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 7f);
                Dust d = Dust.NewDustDirect(Player.Center, 0, 0, DustID.IceTorch, vel.X, vel.Y, 70, default, Main.rand.NextFloat(1.2f, 1.8f));
                d.noGravity = true;
            }

            for (int i = 0; i < 6; i++) {
                Vector2 vel = (npc.Center - Player.Center).SafeNormalize(Vector2.UnitX).RotatedByRandom(0.35f) * Main.rand.NextFloat(5f, 9f);
                Dust d = Dust.NewDustDirect(Player.Center, 0, 0, DustID.Stone, vel.X, vel.Y, 60, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
            }
        }

        private static int ResolveShooterIndex(Projectile proj) {
            if (proj.npcProj && proj.owner >= 0 && proj.owner < Main.maxNPCs)
                return proj.owner;

            int ai0 = (int)proj.ai[0];
            if (ai0 >= 0 && ai0 < Main.maxNPCs)
                return ai0;

            int ai1 = (int)proj.ai[1];
            if (ai1 >= 0 && ai1 < Main.maxNPCs)
                return ai1;

            return -1;
        }
    }

    /// <summary>玄龟盾击 — 左键冲刺的龟甲盾刃弹幕。</summary>
    public class BlackTortoiseShieldBash : ModProjectile
    {
        private enum BashState { Charging, Returning }

        private BashState State {
            get => (BashState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float TravelTimer => ref Projectile.ai[1];

        private const float MaxChargeDistance = 520f;
        private const float ReturnSpeed = 18f;

        public override string Texture => "Terraria/Images/Item_" + ItemID.AnkhShield;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            TravelTimer++;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            switch (State) {
                case BashState.Charging:
                    HandleCharging(owner);
                    break;
                case BashState.Returning:
                    HandleReturning(owner);
                    break;
            }

            SpawnShellDust();
            Lighting.AddLight(Projectile.Center, 0.25f, 0.45f, 0.65f);
        }

        private void HandleCharging(Player owner) {
            Projectile.velocity *= 0.985f;

            if (TravelTimer > 8 && Vector2.Distance(Projectile.Center, owner.Center) > MaxChargeDistance) {
                State = BashState.Returning;
                TravelTimer = 0;
                SoundEngine.PlaySound(SoundID.Item7 with { Pitch = 0.1f, Volume = 0.55f }, Projectile.Center);
            }
        }

        private void HandleReturning(Player owner) {
            Vector2 toOwner = owner.Center - Projectile.Center;
            float distance = toOwner.Length();
            Vector2 direction = toOwner.SafeNormalize(Vector2.Zero);

            float speed = MathHelper.Lerp(ReturnSpeed, ReturnSpeed * 1.35f, 1f - MathHelper.Clamp(distance / MaxChargeDistance, 0f, 1f));
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * speed, 0.14f);

            if (distance < 36f)
                Projectile.Kill();
        }

        private void SpawnShellDust() {
            if (!Main.rand.NextBool(2))
                return;

            Dust d = Dust.NewDustDirect(
                Projectile.Center + Main.rand.NextVector2Circular(14, 14),
                0, 0, DustID.IceTorch,
                -Projectile.velocity.X * 0.08f, -Projectile.velocity.Y * 0.08f,
                80, default, Main.rand.NextFloat(0.9f, 1.3f));
            d.noGravity = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn, 90);

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                Dust d = Dust.NewDustDirect(target.Center, 0, 0, DustID.Stone, vel.X, vel.Y, 70, default, Main.rand.NextFloat(1.2f, 1.7f));
                d.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item70 with { Volume = 0.45f, Pitch = 0.15f }, target.Center);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == BashState.Charging) {
                State = BashState.Returning;
                TravelTimer = 0;
            }

            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Item[ItemID.AnkhShield].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(new Color(60, 140, 210), new Color(180, 230, 255), progress);
                trailColor *= progress * 0.45f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress, SpriteEffects.None, 0f);
            }

            Color glow = new Color(120, 190, 255) * 0.35f;
            glow.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, glow, Projectile.rotation, origin, Projectile.scale * 1.15f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(texture, drawPos, null, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.IceTorch, vel.X, vel.Y, 70, default, Main.rand.NextFloat(1f, 1.4f));
                d.noGravity = true;
            }
        }
    }

    /// <summary>玄武结界 — 成功格挡反震时，自玩家迸发的龟甲反震环波。</summary>
    public class BlackTortoiseGuardNova : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float MaxRadius = 260f;

        private ref float Age => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 34;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void OnSpawn(IEntitySource source) {
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.8f, Pitch = -0.25f }, Projectile.Center);
        }

        public override void AI() {
            Age++;
            Player owner = Main.player[Projectile.owner];
            if (owner.active && !owner.dead)
                Projectile.Center = owner.Center;

            if (Main.netMode != NetmodeID.Server) {
                float radius = CurrentRadius();
                for (int i = 0; i < 4; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = Projectile.Center + ang.ToRotationVector2() * radius;
                    int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.Stone;
                    Dust d = Dust.NewDustPerfect(pos, dustType, ang.ToRotationVector2() * 4f, 60, default, 1.5f);
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, new Vector3(0.3f, 0.55f, 0.75f));
            }
        }

        private float CurrentRadius() => MathHelper.SmoothStep(0f, MaxRadius, Math.Min(Age / 22f, 1f));

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn, 120);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, CurrentRadius(), targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.SoftGlow == null)
                return false;

            float prog = Age / 34f;
            float alpha = ACMUtils.QuadOut(1f - prog) * 0.8f;
            float scale = CurrentRadius() / (ACMAsset.SoftGlow.Width * 0.5f);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = ACMAsset.SoftGlow.Size() * 0.5f;

            Color outer = new Color(60, 140, 210) * alpha * 0.6f;
            outer.A = 0;
            sb.Draw(ACMAsset.SoftGlow, drawPos, null, outer, 0f, origin, scale, SpriteEffects.None, 0f);

            Color inner = new Color(180, 230, 255) * alpha * 0.7f;
            inner.A = 0;
            sb.Draw(ACMAsset.SoftGlow, drawPos, null, inner, 0f, origin, scale * 0.55f, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
