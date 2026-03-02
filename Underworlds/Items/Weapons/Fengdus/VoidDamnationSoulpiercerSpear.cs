using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Fengdus
{
    /// <summary>
    /// 虚空断罪永劫穿心矛 - 终极近战矛
    /// 超远距离突刺(MaxThrustDistance: 180)，突刺路径上留下虚空裂隙
    /// 裂隙持续2秒对经过的敌人造成伤害
    /// 命中时无视防御，击杀后虚空爆发+大量回血
    /// </summary>
    public class VoidDamnationSoulpiercerSpear : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 22000;
            Item.crit = 22;
            Item.DamageType = DamageClass.Melee;
            Item.width = 70;
            Item.height = 70;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 12f;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<VoidSoulpiercerProjectile>();
            Item.shootSpeed = 6f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<VoidSoulpiercerProjectile>()] < 1;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<OblivionSoulhook>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 20)
                .AddIngredient<SoulFragment>(50)
                .AddIngredient<UmbralStoneItem>(100)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    public class VoidSoulpiercerProjectile : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/VoidDamnationSoulpiercerSpear";

        private enum AttackStage { Prepare, Thrust, Retract }
        private AttackStage CurrentStage {
            get => (AttackStage)Projectile.ai[0];
            set { Projectile.ai[0] = (float)value; Timer = 0; }
        }
        private ref float Timer => ref Projectile.ai[1];
        private ref float ThrustDistance => ref Projectile.localAI[0];
        private const float MaxThrustDistance = 180f;
        private const float BaseOffset = 6f;
        private Player Owner => Main.player[Projectile.owner];
        private float PrepareTime => 2f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float ThrustTime => 5f / Owner.GetTotalAttackSpeed(Projectile.DamageType);
        private float RetractTime => 3f / Owner.GetTotalAttackSpeed(Projectile.DamageType);

        private bool riftSpawned = false;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed) { Projectile.Kill(); return; }
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;

            switch (CurrentStage) {
                case AttackStage.Prepare: HandlePrepare(); break;
                case AttackStage.Thrust: HandleThrust(); break;
                case AttackStage.Retract: HandleRetract(); break;
            }

            UpdatePositionAndRotation();
            SpawnVoidParticles();
            Lighting.AddLight(Projectile.Center, 0.2f, 0.3f, 1.0f);
            Timer++;
        }

        private void HandlePrepare() {
            ThrustDistance = MathHelper.Lerp(0, -16f, Timer / PrepareTime);
            if (Timer >= PrepareTime) {
                CurrentStage = AttackStage.Thrust;
                riftSpawned = false;
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.3f, Volume = 1.5f }, Projectile.Center);
            }
        }

        private void HandleThrust() {
            float progress = Timer / ThrustTime;
            ThrustDistance = MathHelper.SmoothStep(-16f, MaxThrustDistance, progress);

            if (!riftSpawned && progress > 0.5f) {
                riftSpawned = true;
                Vector2 direction = (Main.MouseWorld - Owner.MountedCenter).SafeNormalize(Vector2.UnitX);
                Vector2 riftStart = Owner.MountedCenter + direction * BaseOffset;
                int riftType = ModContent.ProjectileType<VoidRiftLine>();
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), riftStart, direction,
                    riftType, Projectile.damage / 3, 0f, Projectile.owner, MaxThrustDistance);
            }

            if (Timer >= ThrustTime) CurrentStage = AttackStage.Retract;
        }

        private void HandleRetract() {
            float progress = Timer / RetractTime;
            ThrustDistance = MathHelper.SmoothStep(MaxThrustDistance, 0, progress);
            if (Timer >= RetractTime) Projectile.Kill();
        }

        private void UpdatePositionAndRotation() {
            Vector2 direction = (Main.MouseWorld - Owner.MountedCenter).SafeNormalize(Vector2.UnitX);
            Projectile.rotation = direction.ToRotation();
            Projectile.spriteDirection = direction.X > 0 ? 1 : -1;
            Owner.direction = Projectile.spriteDirection;
            float armRotation = Projectile.rotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);
            Vector2 handPosition = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, armRotation);
            handPosition.Y += Owner.gfxOffY;
            Projectile.Center = handPosition + direction * (BaseOffset + ThrustDistance);
        }

        private void SpawnVoidParticles() {
            Vector2 tipPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 55f;

            if (CurrentStage == AttackStage.Thrust) {
                for (int i = 0; i < 5; i++) {
                    Dust void_d = Dust.NewDustDirect(
                        tipPos + Main.rand.NextVector2Circular(15, 15), 4, 4, DustID.BlueTorch,
                        -Projectile.rotation.ToRotationVector2().X * 4f + Main.rand.NextFloat(-2f, 2f),
                        -Projectile.rotation.ToRotationVector2().Y * 4f + Main.rand.NextFloat(-2f, 2f),
                        80, default, Main.rand.NextFloat(2f, 3f));
                    void_d.noGravity = true;
                }
                for (int i = 0; i < 2; i++) {
                    Dust crack = Dust.NewDustDirect(
                        tipPos + Main.rand.NextVector2Circular(20, 20), 4, 4, DustID.Shadowflame,
                        0f, -2f, 120, default, 2.5f);
                    crack.noGravity = true;
                }
            }

            if (Main.rand.NextBool(2)) {
                Dust ambient = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(15, 15), 4, 4, DustID.Wraith,
                    0f, -1f, 100, default, 1.5f);
                ambient.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int healAmount = Main.rand.Next(30, 80);
            Owner.Heal(healAmount);

            target.AddBuff(BuffID.ShadowFlame, 600);
            target.AddBuff(BuffID.BrokenArmor, 600);
            target.AddBuff(BuffID.Slow, 600);

            for (int i = 0; i < 25; i++) {
                Vector2 vel = (Owner.Center - target.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(6f, 16f);
                vel = vel.RotatedByRandom(MathHelper.ToRadians(35));
                Dust soul = Dust.NewDustPerfect(target.Center, DustID.BlueTorch, vel, 60, default, Main.rand.NextFloat(2f, 3.5f));
                soul.noGravity = true;
            }

            if (target.life <= 0) {
                Owner.Heal(Main.rand.Next(50, 100));
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.5f, Pitch = -0.5f }, target.Center);

                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC nearby = Main.npc[i];
                    if (!nearby.CanBeChasedBy() || nearby.whoAmI == target.whoAmI) continue;
                    if (Vector2.Distance(target.Center, nearby.Center) < 600f) {
                        nearby.SimpleStrikeNPC(damageDone, hit.HitDirection, false, 0f, null, false, 0, true);
                        nearby.AddBuff(BuffID.ShadowFlame, 600);
                    }
                }

                for (int i = 0; i < 50; i++) {
                    float angle = MathHelper.TwoPi / 50f * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(10f, 20f);
                    Dust ring = Dust.NewDustPerfect(target.Center, DustID.BlueTorch, vel, 40, default, Main.rand.NextFloat(2.5f, 4f));
                    ring.noGravity = true;
                }
            }

            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.7f, Pitch = -0.2f }, target.Center);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
            modifiers.Defense.Flat -= target.defense;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (BaseOffset + ThrustDistance + 90f);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 35f, ref collisionPoint);
        }

        public override void CutTiles() {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * (BaseOffset + ThrustDistance + 90f);
            Utils.PlotTileLine(start, end, 35f, DelegateMethods.CutTiles);
        }

        public override bool? CanDamage() => CurrentStage == AttackStage.Thrust;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2;
            float rotationOffset;
            SpriteEffects effects;
            if (Projectile.spriteDirection > 0) { rotationOffset = MathHelper.PiOver4; effects = SpriteEffects.None; }
            else { rotationOffset = MathHelper.Pi - MathHelper.PiOver4; effects = SpriteEffects.FlipHorizontally; }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation + rotationOffset, origin, Projectile.scale, effects, 0);

            if (CurrentStage == AttackStage.Thrust) {
                Color glowColor = new Color(40, 80, 255) * 0.6f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glowColor,
                    Projectile.rotation + rotationOffset, origin, Projectile.scale * 1.15f, effects, 0);

                Texture2D slashBurst = ACMAsset.SlashBurst;
                if (slashBurst != null) {
                    Vector2 tipPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 55f - Main.screenPosition;
                    Vector2 sbOrigin = slashBurst.Size() / 2f;
                    Color tipGlow = new Color(80, 120, 255) * 0.6f;
                    tipGlow.A = 0;
                    float pulse = 0.15f + MathF.Sin(Timer * 0.4f) * 0.05f;
                    Main.EntitySpriteDraw(slashBurst, tipPos, null, tipGlow, Projectile.rotation + MathHelper.PiOver2, sbOrigin, new Vector2(pulse * 0.4f, pulse), SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 虚空裂隙 - 突刺路径上的持续伤害区域
    /// </summary>
    public class VoidRiftLine : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/VoidDamnationSoulpiercerSpear";
        private ref float RiftLength => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;

            float opacity = MathHelper.Clamp(1f - Timer / 120f, 0f, 1f);
            Vector2 dir = Projectile.rotation.ToRotationVector2();

            int crackCount = (int)(RiftLength / 20f);
            for (int i = 0; i < Math.Min(crackCount / 3, 5); i++) {
                float t = Main.rand.NextFloat();
                Vector2 pos = Projectile.Center + dir * t * RiftLength + Main.rand.NextVector2Circular(10, 10);
                Dust crack = Dust.NewDustPerfect(pos, DustID.BlueTorch,
                    Main.rand.NextVector2Circular(2f, 2f) + new Vector2(0, -1f),
                    80, default, Main.rand.NextFloat(1.5f, 2.5f) * opacity);
                crack.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                float t = Main.rand.NextFloat();
                Vector2 pos = Projectile.Center + dir * t * RiftLength;
                Dust shadow = Dust.NewDustPerfect(pos, DustID.Shadowflame,
                    new Vector2(0, -Main.rand.NextFloat(1f, 3f)), 120, default, 1.5f * opacity);
                shadow.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center + dir * RiftLength * 0.5f, 0.2f * opacity, 0.3f * opacity, 0.8f * opacity);
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 start = Projectile.Center;
            Vector2 end = start + dir * RiftLength;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 25f, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 300);
        }

        public override bool PreDraw(ref Color lightColor) {
            float opacity = MathHelper.Clamp(1f - Timer / 120f, 0f, 1f);
            Vector2 dir = Projectile.rotation.ToRotationVector2();

            Texture2D lightningBranch = ACMAsset.LightningBranch;
            if (lightningBranch != null) {
                Vector2 origin = new Vector2(lightningBranch.Width / 2f, lightningBranch.Height);
                int segments = Math.Max(1, (int)(RiftLength / 100f));
                float segLen = RiftLength / segments;

                for (int s = 0; s < segments; s++) {
                    Vector2 segPos = Projectile.Center + dir * (s * segLen + segLen * 0.5f) - Main.screenPosition;
                    Color riftColor = Color.Lerp(new Color(30, 60, 200), new Color(120, 180, 255), (float)s / segments) * opacity * 0.6f;
                    riftColor.A = 0;
                    float scaleX = 0.06f;
                    float scaleY = segLen / lightningBranch.Height * 1.2f;
                    float flickerOffset = MathF.Sin(Timer * 0.3f + s * 1.5f) * 0.02f;
                    Main.EntitySpriteDraw(lightningBranch, segPos, null, riftColor,
                        Projectile.rotation + MathHelper.PiOver2, origin, new Vector2(scaleX + flickerOffset, scaleY), SpriteEffects.None, 0);
                }
            }

            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                for (int i = 0; i < 3; i++) {
                    float t = (i + 0.5f) / 3f;
                    Vector2 pos = Projectile.Center + dir * t * RiftLength - Main.screenPosition;
                    Color glow = new Color(60, 100, 255) * opacity * 0.4f;
                    glow.A = 0;
                    float pulse = 0.6f + MathF.Sin(Timer * 0.2f + i) * 0.15f;
                    Main.EntitySpriteDraw(softGlow, pos, null, glow, 0f, glowOrigin, pulse, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
