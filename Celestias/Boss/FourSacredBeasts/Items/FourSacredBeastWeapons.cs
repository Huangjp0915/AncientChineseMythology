using AncientChineseMythology;
using AncientChineseMythology.Celestias.Boss.Aoshuns;
using AncientChineseMythology.Celestias.PillarofTheHeavenes.Items;
using AncientChineseMythology.Celestias.PillarofTheHeavenes.Tiles;
using AncientChineseMythology.Items.Materials;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Items
{
    /// <summary>
    /// 青涛双流刃 — 青龙 apex 双短刃
    /// 高速交替斩击，鞘中飞出旋转激流剑；右键展开蔚蓝剑群协战
    /// </summary>
    public class AzureTorrentBlades : ModItem
    {
        private int attackType;

        public override void SetDefaults() {
            Item.damage = 1480;
            Item.crit = 14;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 40;
            Item.useTime = Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<AzureTorrentBladesSwing>();
            Item.shootSpeed = 3f;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                return player.ownedProjectileCounts[ModContent.ProjectileType<AzureTorrentBladesOrbit>()] < 2;
            }

            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                player.AddBuff(ModContent.BuffType<AzureTorrentBladesBuff>(), 480);
                int orbitType = ModContent.ProjectileType<AzureTorrentBladesOrbit>();
                int existing = player.ownedProjectileCounts[orbitType];
                for (int i = existing; i < 2; i++) {
                    Projectile.NewProjectile(source, player.Center, Vector2.Zero, orbitType,
                        (int)(damage * 0.8f), knockback * 0.45f, player.whoAmI, i);
                }

                SoundEngine.PlaySound(SoundID.Item72 with { Pitch = 0.35f, Volume = 0.85f }, player.Center);
                return false;
            }

            Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI, attackType);
            attackType = (attackType + 1) % 2;
            return false;
        }

        public override void HoldItem(Player player) {
            if (Main.rand.NextBool(6)) {
                Vector2 pos = player.Center + Main.rand.NextVector2Circular(42f, 42f);
                int dust = Dust.NewDust(pos, 0, 0, DustID.Water, 0f, -0.4f, 70, AzureTorrentPalette.FlowGlow, 1.4f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "QinglongLore", "「青鳞藏鞘，激流双刃」"));
            tooltips.Add(new TooltipLine(Mod, "QinglongEffect", "极快交替双刀斩击，鞘中飞出旋转激流剑"));
            tooltips.Add(new TooltipLine(Mod, "QinglongEffect2", "右键展开蔚蓝剑群，环绕协战并突袭敌人"));
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<QingLongSpirit>(8)
                .AddIngredient<EmpyriteBar>(15)
                .AddIngredient<HeavenFragment>(12)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.Excalibur;
    }

    internal static class AzureTorrentPalette
    {
        public static readonly Color FlowGlow = new(90, 210, 255);
        public static readonly Color AzureStream = new(35, 165, 225);
        public static readonly Color JadeRipple = new(55, 205, 145);
    }

    /// <summary>青涛双流刃 — 交替持握短刃挥砍。</summary>
    public class AzureTorrentBladesSwing : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Excalibur;

        private const float SWING_RANGE = MathF.PI * 1.25f;
        private const float PREP_FRAC = 0.14f;
        private const float EXEC_FRAC = 0.58f;

        private enum Stage { Prepare, Execute, Unwind }

        private ref float Timer => ref Projectile.ai[1];
        private ref float InitAngle => ref Projectile.ai[2];
        private ref float RawProgress => ref Projectile.localAI[0];
        private int BladeSide => (int)Projectile.ai[0];

        private Stage CurrentStage {
            get => (Stage)Projectile.localAI[1];
            set { Projectile.localAI[1] = (float)value; Timer = 0f; }
        }

        private bool _torrentLaunched;
        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 44;
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
            int dir = Projectile.spriteDirection * (BladeSide == 0 ? 1 : -1);

            if (dir > 0) {
                toMouse = MathHelper.Clamp(toMouse, -MathF.PI / 2.4f, MathF.PI / 4.5f);
                InitAngle = toMouse - SWING_RANGE * 0.52f;
            }
            else {
                if (toMouse < 0) toMouse += MathHelper.TwoPi;
                toMouse = MathHelper.Clamp(toMouse, MathF.PI * 0.82f, MathF.PI * 1.35f);
                InitAngle = toMouse + SWING_RANGE * 0.52f;
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
            int dir = Projectile.spriteDirection * (BladeSide == 0 ? 1 : -1);

            switch (CurrentStage) {
                case Stage.Prepare:
                    RawProgress = 0f;
                    if (Timer >= prepEnd) {
                        SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.45f, Volume = 0.85f }, Owner.position);
                        CurrentStage = Stage.Execute;
                    }
                    break;

                case Stage.Execute:
                    RawProgress = MathHelper.SmoothStep(0f, SWING_RANGE, Math.Min(Timer / execDur, 1f));

                    if (!_torrentLaunched && Timer >= execDur * 0.42f) {
                        _torrentLaunched = true;
                        LaunchSheathTorrents();
                    }

                    if (Timer % 2 == 0)
                        SpawnSwingDust();

                    if (Timer >= execDur) CurrentStage = Stage.Unwind;
                    break;

                case Stage.Unwind:
                    RawProgress = MathHelper.Lerp(SWING_RANGE, SWING_RANGE * 1.03f,
                        Math.Min(Timer / unwindDur, 1f));
                    if (Timer >= unwindDur) Projectile.Kill();
                    break;
            }

            Projectile.rotation = InitAngle + dir * RawProgress;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
                Projectile.rotation - MathHelper.PiOver2);
            Vector2 arm = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full,
                Projectile.rotation - MathHelper.PiOver2);
            arm.Y += Owner.gfxOffY;
            arm += Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * (BladeSide == 0 ? -10f : 10f);
            Projectile.Center = arm;
            Projectile.scale = 0.92f * Owner.GetAdjustedItemScale(Owner.HeldItem);
            Owner.heldProj = Projectile.whoAmI;
            Timer++;
        }

        private void LaunchSheathTorrents() {
            Vector2 aim = Owner.DirectionTo(Main.MouseWorld);
            Vector2 back = Owner.Center - aim * 28f;
            int projType = ModContent.ProjectileType<AzureTorrentBladesProj>();
            int torrentDamage = (int)(Owner.GetTotalDamage(DamageClass.Melee).ApplyTo(Owner.HeldItem.damage) * 0.85f);

            for (int i = 0; i < 2; i++) {
                float side = i == 0 ? -1f : 1f;
                Vector2 spawn = back + aim.RotatedBy(MathHelper.PiOver2) * side * 14f;
                Vector2 vel = aim.RotatedBy(MathHelper.ToRadians(8f * side)) * 18f;
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem), spawn, vel,
                    projType, torrentDamage, Owner.HeldItem.knockBack * 0.55f, Owner.whoAmI, i);
            }

            SoundEngine.PlaySound(SoundID.Item73 with { Pitch = 0.25f, Volume = 0.75f }, Owner.Center);
        }

        private void SpawnSwingDust() {
            Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2()
                          * Projectile.Size.Length() * Projectile.scale * 0.65f;
            int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
            Dust d = Dust.NewDustPerfect(tip + Main.rand.NextVector2Circular(8, 8), dustType,
                Main.rand.NextVector2Circular(2.5f, 2.5f), 60, AzureTorrentPalette.FlowGlow, 1.3f);
            d.noGravity = true;
        }

        public override bool? CanDamage() => CurrentStage == Stage.Execute ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = start + Projectile.rotation.ToRotationVector2()
                        * Projectile.Size.Length() * Projectile.scale * 1.05f;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, 22f * Projectile.scale, ref collisionPoint);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Wet, 240);
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Water,
                    Main.rand.NextVector2Circular(5f, 5f), 50, AzureTorrentPalette.AzureStream, 1.6f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            int dir = Projectile.spriteDirection * (BladeSide == 0 ? 1 : -1);
            float rotOff = dir > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            if (CurrentStage == Stage.Execute) {
                Texture2D wave = ACMAsset.GlaciateWave;
                if (wave != null) {
                    for (int i = 1; i < 10 && i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                        float alpha = (1f - i / 10f) * 0.55f;
                        sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                            AzureTorrentPalette.JadeRipple * alpha,
                            Projectile.oldRot[i] + rotOff, wave.Size() * 0.5f,
                            Projectile.scale * 0.38f, SpriteEffects.None, 0);
                    }
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D tex = TextureAssets.Item[ItemID.Excalibur].Value;
            SpriteEffects fx = dir < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 origin = dir > 0 ? new Vector2(0, tex.Height) : new Vector2(tex.Width, tex.Height);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation + rotOff, origin, Projectile.scale, fx, 0);
            return false;
        }
    }

    /// <summary>蔚蓝激流剑 — 自鞘飞出、torrent 螺旋后追踪的旋转短刃。</summary>
    public class AzureTorrentBladesProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Excalibur;

        private ref float SpiralTimer => ref Projectile.ai[1];
        private int BladeIndex => (int)Projectile.ai[0];

        private bool _homing;
        private const float SpiralDuration = 34f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            SpiralTimer++;
            Projectile.rotation += 0.42f * (BladeIndex == 0 ? 1f : -1f);

            if (!_homing && SpiralTimer < SpiralDuration) {
                Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Vector2 perp = new(-forward.Y, forward.X);
                float spiralAngle = SpiralTimer * 0.22f + BladeIndex * MathHelper.Pi;
                float spiralRadius = MathHelper.Lerp(42f, 8f, SpiralTimer / SpiralDuration);
                Projectile.Center += perp * MathF.Sin(spiralAngle) * spiralRadius * 0.12f;
                Projectile.velocity *= 0.985f;
            }
            else {
                _homing = true;
                NPC target = FindClosestNPC(520f);
                if (target != null) {
                    Vector2 dir = Projectile.DirectionTo(target.Center);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 22f, 0.11f);
                }
            }

            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10, 10),
                    DustID.Water, -Projectile.velocity * 0.15f, 70, AzureTorrentPalette.FlowGlow, 1.2f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, AzureTorrentPalette.AzureStream.ToVector3() * 0.55f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Wet, 300);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                Dust d = Dust.NewDustPerfect(target.Center, DustID.BlueTorch, vel, 60, AzureTorrentPalette.JadeRipple, 1.5f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trail = Color.Lerp(AzureTorrentPalette.AzureStream, AzureTorrentPalette.JadeRipple, progress) * progress * 0.55f;
                trail.A = 0;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trail, Projectile.oldRot[i], origin, Projectile.scale * progress, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            return false;
        }

        private NPC FindClosestNPC(float maxDistance) {
            NPC closest = null;
            float best = maxDistance;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(this)) continue;
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < best) {
                    best = dist;
                    closest = npc;
                }
            }
            return closest;
        }
    }

    /// <summary>蔚蓝剑群 — 右键展开的环绕协战短刃。</summary>
    public class AzureTorrentBladesOrbit : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Excalibur;

        private ref float OrbitAngle => ref Projectile.ai[1];
        private ref float LungeTimer => ref Projectile.localAI[0];
        private int Slot => (int)Projectile.ai[0];

        private const float OrbitRadius = 92f;
        private Vector2 _lungeTarget;
        private bool _lunging;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 480;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) { Projectile.Kill(); return; }

            if (Owner.HasBuff(ModContent.BuffType<AzureTorrentBladesBuff>()))
                Projectile.timeLeft = 2;

            OrbitAngle += 0.09f * (Slot == 0 ? 1f : -1f);
            Projectile.rotation += 0.35f;

            if (!_lunging) {
                float baseAngle = Slot * MathHelper.Pi + OrbitAngle;
                Vector2 orbitPos = Owner.Center + baseAngle.ToRotationVector2() * OrbitRadius;
                Projectile.Center = Vector2.Lerp(Projectile.Center, orbitPos, 0.22f);
                Projectile.velocity = Vector2.Zero;

                LungeTimer++;
                if (LungeTimer >= 36f) {
                    NPC target = FindClosestNPC(420f);
                    if (target != null) {
                        _lunging = true;
                        LungeTimer = 0f;
                        _lungeTarget = target.Center;
                        SoundEngine.PlaySound(SoundID.Item73 with { Pitch = 0.5f, Volume = 0.55f }, Projectile.Center);
                    }
                    else {
                        LungeTimer = 24f;
                    }
                }
            }
            else {
                Vector2 toTarget = _lungeTarget - Projectile.Center;
                if (toTarget.Length() < 24f || LungeTimer > 28f) {
                    _lunging = false;
                    LungeTimer = 0f;
                }
                else {
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget.SafeNormalize(Vector2.Zero) * 26f, 0.18f);
                    Projectile.Center += Projectile.velocity;
                    LungeTimer++;
                }
            }

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Water,
                    Main.rand.NextVector2Circular(1.5f, 1.5f), 80, AzureTorrentPalette.FlowGlow, 1.1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, AzureTorrentPalette.JadeRipple.ToVector3() * 0.45f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Wet, 180);
            _lunging = false;
            LungeTimer = 0f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            float pulse = 1f + MathF.Sin(OrbitAngle * 2f) * 0.08f;

            Color glow = AzureTorrentPalette.FlowGlow * 0.35f;
            glow.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, glow,
                Projectile.rotation, origin, Projectile.scale * pulse * 1.15f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0f);
            return false;
        }

        private NPC FindClosestNPC(float maxDistance) {
            NPC closest = null;
            float best = maxDistance;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(this)) continue;
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < best) {
                    best = dist;
                    closest = npc;
                }
            }
            return closest;
        }
    }

    public class AzureTorrentBladesBuff : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<AzureTorrentBladesOrbit>()] > 0)
                player.buffTime[buffIndex] = 18000;
            else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }

    /// <summary>风蛇长刀 — 挥砍释放风蛇刀气，每四刀横扫释放追踪龙卷。</summary>
    public class WindserpentDao : ModItem
    {
        private int slashCounter;

        public override void SetDefaults() {
            Item.damage = 1520;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<WindserpentSlash>();
            Item.shootSpeed = 16f;
            Item.crit = 10;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            slashCounter++;

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);

            if (slashCounter >= 4) {
                slashCounter = 0;
                Vector2 direction = velocity.SafeNormalize(Vector2.UnitX);
                Vector2 tornadoPos = player.Center + direction * 120f;

                Projectile.NewProjectile(source, tornadoPos, direction * 6f,
                    ModContent.ProjectileType<WindserpentSweepTornado>(), (int)(damage * 1.5f), knockback * 1.2f, player.whoAmI);

                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.15f, Volume = 0.9f }, player.Center);

                if (player.whoAmI == Main.myPlayer) {
                    player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 12);
                }
            }

            return false;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = new Vector2(hitbox.X + Main.rand.Next(hitbox.Width), hitbox.Y + Main.rand.Next(hitbox.Height));
                Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GreenTorch, player.velocity.X * 0.15f, player.velocity.Y * 0.15f, 100, default, 1.4f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.rand.NextBool(5)) {
                target.AddBuff(BuffID.Slow, 90);
            }

            for (int i = 0; i < 4; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4, 4);
                Dust d = Dust.NewDustDirect(target.Center, 0, 0, DustID.GreenTorch, vel.X, vel.Y, 80, default, 1.5f);
                d.noGravity = true;
            }
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<QingLongSpirit>(8)
                .AddIngredient<EmpyriteBar>(15)
                .AddIngredient<HeavenFragment>(12)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.BreakerBlade;
    }

    /// <summary>雷鼓长弓 — 青龙 apex 远程弓，雷鼓蓄力后释放穿透天雷箭。</summary>
    public class ThunderclapLongbow : ModItem
    {
        private int chargeTime;
        private bool isFullyCharged;

        private const int MinCharge = 8;
        private const int MaxCharge = 45;

        public override void SetDefaults() {
            Item.damage = 1550;
            Item.crit = 12;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 18;
            Item.height = 46;
            Item.useTime = Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3.5f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<ThunderclapArrow>();
            Item.shootSpeed = 22f;
            Item.useAmmo = AmmoID.Arrow;
            Item.channel = true;
        }

        public override void HoldItem(Player player) {
            if (player.channel && player.HasAmmo(Item)) {
                chargeTime++;

                float chargeProgress = MathHelper.Clamp((chargeTime - MinCharge) / (float)(MaxCharge - MinCharge), 0f, 1f);
                SpawnDrumChargeFx(player, chargeProgress);

                if (chargeTime == MaxCharge) {
                    isFullyCharged = true;
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.35f, Volume = 0.85f }, player.Center);
                }

                if (chargeTime > MaxCharge)
                    chargeTime = MaxCharge;
            }
            else if (chargeTime > 0 && !player.channel) {
                if (isFullyCharged)
                    FireThunderclapBolt(player);
                else if (chargeTime > MinCharge)
                    FireThunderArrow(player, damageMultiplier: 1f);

                chargeTime = 0;
                isFullyCharged = false;
            }
        }

        private static void SpawnDrumChargeFx(Player player, float chargeProgress) {
            if (Main.netMode == NetmodeID.Server || chargeProgress <= 0f)
                return;

            if (Main.rand.NextBool(2)) {
                float ringRadius = MathHelper.Lerp(28f, 62f, 1f - chargeProgress * 0.65f);
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dustPos = player.Center + angle.ToRotationVector2() * ringRadius;
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 100, default, 1.2f + chargeProgress);
                d.noGravity = true;
                d.velocity = (player.Center - dustPos).SafeNormalize(Vector2.Zero) * (3f + chargeProgress * 5f);
            }

            if (chargeProgress > 0.45f)
                Lighting.AddLight(player.Center, Vector3.Lerp(AoshunHelper.ThunderPurple.ToVector3(), AoshunHelper.LightningBlue.ToVector3(), chargeProgress) * chargeProgress * 0.75f);
        }

        private void FireThunderArrow(Player player, float damageMultiplier) {
            if (Main.netMode == NetmodeID.MultiplayerClient || !player.HasAmmo(Item))
                return;

            player.PickAmmo(Item, out _, out float speed, out int damage, out float knockback, out _);

            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Vector2 muzzle = player.Center + direction * 18f;
            int arrowDamage = (int)((damage + Item.damage) * damageMultiplier);
            float arrowKnockback = knockback + Item.knockBack;

            Projectile.NewProjectile(
                player.GetSource_ItemUse(Item),
                muzzle,
                direction * (Item.shootSpeed + speed),
                ModContent.ProjectileType<ThunderclapArrow>(),
                arrowDamage,
                arrowKnockback,
                player.whoAmI);

            SoundEngine.PlaySound(SoundID.Item5 with { Pitch = 0.15f, Volume = 0.9f }, player.Center);
        }

        private void FireThunderclapBolt(Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient || !player.HasAmmo(Item))
                return;

            player.PickAmmo(Item, out _, out float speed, out int damage, out float knockback, out _);

            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Vector2 muzzle = player.Center + direction * 22f;
            int boltDamage = (int)((damage + Item.damage) * 1.8f);
            float boltKnockback = (knockback + Item.knockBack) * 1.35f;

            Projectile.NewProjectile(
                player.GetSource_ItemUse(Item),
                muzzle,
                direction * (Item.shootSpeed * 1.35f + speed),
                ModContent.ProjectileType<ThunderclapBolt>(),
                boltDamage,
                boltKnockback,
                player.whoAmI);

            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.75f, Pitch = 0.25f }, player.Center);
            SoundEngine.PlaySound(SoundID.Item5 with { Pitch = 0.45f, Volume = 1.05f }, player.Center);

            if (player.whoAmI == Main.myPlayer) {
                player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 12);
            }

            for (int i = 0; i < 14; i++) {
                Vector2 vel = direction.RotatedByRandom(MathHelper.PiOver4) * Main.rand.NextFloat(4f, 9f);
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                Dust d = Dust.NewDustDirect(muzzle, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.8f);
                d.noGravity = true;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "QinglongBowLore", "「雷鼓一响，天矢穿云」"));
            tooltips.Add(new TooltipLine(Mod, "QinglongBowEffect", "将箭矢化为穿透雷电箭，命中时释放雷爆"));
            tooltips.Add(new TooltipLine(Mod, "QinglongBowEffect2", "长按雷鼓蓄力，松手释放无限穿透的天雷矢"));
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<QingLongSpirit>(8)
                .AddIngredient<EmpyriteBar>(15)
                .AddIngredient<HeavenFragment>(12)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.PulseBow;
    }

    /// <summary>万劫狂金震碎者 — 白虎金纹灾锤，掷出后裂地释放金纹冲击波。</summary>
    public class AurelianCataclysmSmasher : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1580;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 12f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<AurelianCataclysmSmasherProj>();
            Item.shootSpeed = 22f;
            Item.crit = 8;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<AurelianCataclysmSmasherProj>()] < 1;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<BaihuSpirit>(8)
                .AddIngredient<EmpyriteBar>(15)
                .AddIngredient<HeavenFragment>(12)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.PaladinsHammer;
    }

    /// <summary>银脉湮灭枪 — 白虎 apex 速射枪，银脉冲弹丸；每第八发释放三重脉冲爆发。</summary>
    public class ArgentPulseObliterator : ModItem
    {
        private int pulseCounter;

        public override void SetDefaults() {
            Item.damage = 1450;
            Item.crit = 10;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 48;
            Item.height = 24;
            Item.useTime = Item.useAnimation = 6;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 2f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item11;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<ArgentPulseBullet>();
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.Bullet;
        }

        public override Vector2? HoldoutOffset() => new Vector2(-8, 0);

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            type = ModContent.ProjectileType<ArgentPulseBullet>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            pulseCounter++;
            Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 muzzlePos = position + muzzleDir * 46f;

            if (pulseCounter % 8 == 0) {
                for (int i = -1; i <= 1; i++) {
                    Vector2 burstVel = velocity.RotatedBy(MathHelper.ToRadians(7f * i)) * 1.08f;
                    Projectile.NewProjectile(source, muzzlePos, burstVel,
                        ModContent.ProjectileType<ArgentPulseBurstShot>(),
                        (int)(damage * 1.35f), knockback * 1.4f, player.whoAmI);
                }

                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f, Volume = 0.85f }, muzzlePos);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 14; i++) {
                        Vector2 gatherVel = (muzzlePos - (muzzlePos + Main.rand.NextVector2CircularEdge(52f, 52f))).SafeNormalize(Vector2.Zero)
                            * Main.rand.NextFloat(5f, 12f);
                        Dust d = Dust.NewDustPerfect(muzzlePos + Main.rand.NextVector2Circular(8f, 8f), DustID.Silver, gatherVel, 90, default, 1.8f);
                        d.noGravity = true;
                    }
                }

                if (player.whoAmI == Main.myPlayer)
                    player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(4, 8);
            }
            else {
                Vector2 perturbedVel = velocity.RotatedByRandom(MathHelper.ToRadians(2.5f));
                Projectile.NewProjectile(source, muzzlePos, perturbedVel, type, damage, knockback, player.whoAmI);
            }

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 3; i++) {
                    Vector2 dustVel = -muzzleDir.RotatedByRandom(0.35f) * Main.rand.NextFloat(2f, 5f);
                    int dustType = Main.rand.NextBool() ? DustID.Silver : DustID.PlatinumCoin;
                    Dust d = Dust.NewDustPerfect(muzzlePos, dustType, dustVel, 80, default, 1.2f);
                    d.noGravity = true;
                }
            }

            return false;
        }

        public override void HoldItem(Player player) {
            if (Main.rand.NextBool(7)) {
                Vector2 pos = player.Center + Main.rand.NextVector2Circular(40f, 40f);
                int dust = Dust.NewDust(pos, 0, 0, DustID.Silver, 0f, 0f, 80, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0.35f;
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "ArgentPulseLore", "「银脉贯膛，脉冲湮灭」"));
            tooltips.Add(new TooltipLine(Mod, "ArgentPulseEffect", "极快连射银脉冲弹丸"));
            tooltips.Add(new TooltipLine(Mod, "ArgentPulseEffect2", "每第八发释放三重脉冲爆发，命中绽放银纹冲击"));
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<BaihuSpirit>(8)
                .AddIngredient<EmpyriteBar>(15)
                .AddIngredient<HeavenFragment>(12)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.VortexBeater;
    }

    /// <summary>银脉冲弹 — 高速穿透银光弹丸。</summary>
    public class ArgentPulseBullet : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/LightShot";

        private static readonly Color SilverCore = new(210, 220, 235);
        private static readonly Color ArgentGlow = new(170, 185, 210);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f), 0, 0, DustID.Silver,
                    -Projectile.velocity.X * 0.08f, -Projectile.velocity.Y * 0.08f, 100, default, 1.1f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, SilverCore.ToVector3() * 0.45f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Silver, Main.rand.NextVector2Circular(3f, 3f), 60, default, 1.3f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(ArgentGlow, SilverCore, progress) * (0.5f * progress);
                trailColor.A = 0;

                Main.spriteBatch.Draw(
                    tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null,
                    trailColor,
                    Projectile.oldRot[i],
                    origin,
                    new Vector2(0.32f * progress, 0.1f),
                    SpriteEffects.None,
                    0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color glow = Color.Lerp(ArgentGlow, Color.White, 0.35f);
            glow.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, glow * 0.5f, Projectile.rotation, origin, new Vector2(0.55f, 0.14f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos, null, Color.White * 0.9f, Projectile.rotation, origin, new Vector2(0.42f, 0.11f), SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>银脉冲爆发弹 — 三重 burst 射出的强化脉冲弹，命中后绽放银纹冲击。</summary>
    public class ArgentPulseBurstShot : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/LightShot";

        private static readonly Color SilverCore = new(220, 230, 245);
        private static readonly Color ArgentGlow = new(185, 200, 225);

        private ref float RippleSpawned => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Silver : DustID.PlatinumCoin;
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f), 0, 0, dustType,
                    -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f, 90, default, 1.4f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, SilverCore.ToVector3() * 0.6f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            TrySpawnRipple(target.Center);
        }

        public override void OnKill(int timeLeft) {
            TrySpawnRipple(Projectile.Center);
        }

        private void TrySpawnRipple(Vector2 position) {
            if (RippleSpawned >= 1f || Projectile.owner != Main.myPlayer)
                return;

            RippleSpawned = 1f;

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 12; i++) {
                    Dust d = Dust.NewDustDirect(position, 0, 0, DustID.Silver, 0, 0, 80, default, 1.5f);
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2CircularEdge(5f, 5f);
                }
            }

            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.35f, Volume = 0.65f }, position);

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                position,
                Vector2.Zero,
                ModContent.ProjectileType<ArgentPulseRipple>(),
                (int)(Projectile.damage * 0.5f),
                Projectile.knockBack * 0.4f,
                Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float pulse = 1f + MathF.Sin(Projectile.localAI[0] * 0.35f) * 0.12f;
            Projectile.localAI[0]++;

            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(ArgentGlow, SilverCore, progress) * (0.6f * progress);
                trailColor.A = 0;

                Main.spriteBatch.Draw(
                    tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null,
                    trailColor,
                    Projectile.oldRot[i],
                    origin,
                    new Vector2(0.42f * progress, 0.14f) * pulse,
                    SpriteEffects.None,
                    0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color glow = Color.Lerp(ArgentGlow, Color.White, 0.45f);
            glow.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, glow * 0.65f, Projectile.rotation, origin, new Vector2(0.72f, 0.18f) * pulse, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos, null, Color.White * 0.95f, Projectile.rotation, origin, new Vector2(0.58f, 0.15f) * pulse, SpriteEffects.None, 0f);

            if (ACMAsset.Sparkle != null) {
                Color sparkleColor = new Color(235, 240, 255) * 0.35f;
                sparkleColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.Sparkle, drawPos, null, sparkleColor, Projectile.rotation * 0.5f,
                    ACMAsset.Sparkle.Size() * 0.5f, 0.55f * pulse, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    /// <summary>银脉冲冲击 — burst 弹命中后绽放的银纹冲击环。</summary>
    public class ArgentPulseRipple : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private static readonly Color SilverCore = new(210, 220, 235);
        private static readonly Color ArgentGlow = new(175, 190, 215);

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 34;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.ai[0]++;

            if (Main.netMode == NetmodeID.Server)
                return;

            float radius = Projectile.ai[0] * 8.5f;
            for (int i = 0; i < 4; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.45f, radius);
                int dustType = Main.rand.NextBool() ? DustID.Silver : DustID.PlatinumCoin;
                Dust d = Dust.NewDustPerfect(pos, dustType, Main.rand.NextVector2Circular(1.5f, 1.5f), 70, default, 1.4f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, SilverCore.ToVector3() * 0.55f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = Projectile.ai[0] * 8.5f;
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.SoftGlow == null)
                return false;

            float prog = 1f - Projectile.timeLeft / 34f;
            float alpha = ACMUtils.QuadOut(1f - prog) * 0.85f;
            float scale = MathHelper.SmoothStep(0f, 10f, ACMUtils.QuadOut(prog));

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D glow = ACMAsset.SoftGlow;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color ringColor = Color.Lerp(ArgentGlow, SilverCore, prog) * alpha;
            ringColor.A = 0;
            sb.Draw(glow, drawPos, null, ringColor, 0f, glow.Size() * 0.5f, scale * 0.18f, SpriteEffects.None, 0f);

            if (ACMAsset.Sparkle != null) {
                Color sparkleColor = Color.White * (alpha * 0.45f);
                sparkleColor.A = 0;
                sb.Draw(ACMAsset.Sparkle, drawPos, null, sparkleColor, prog * MathHelper.TwoPi,
                    ACMAsset.Sparkle.Size() * 0.5f, scale * 0.08f, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }

    /// <summary>白虎爪 — 白虎 apex 拳套，四段虎爪连击并撕裂流血。</summary>
    public class WhiteTigerClaws : ModItem
    {
        private int attackType;

        public override void SetDefaults() {
            Item.damage = 1500;
            Item.crit = 12;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 8;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<WhiteTigerClawsSwing>();
            Item.shootSpeed = 1f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI, attackType);
            attackType = (attackType + 1) % 4;
            return false;
        }

        public override void HoldItem(Player player) {
            if (Main.rand.NextBool(5)) {
                Vector2 pos = player.Center + Main.rand.NextVector2Circular(36f, 36f);
                int dust = Dust.NewDust(pos, 0, 0, DustID.Silver, 0f, 0f, 80, default, 1.3f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0.35f;
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "WhiteTigerLore", "「金纹虎爪，撕裂万物」"));
            tooltips.Add(new TooltipLine(Mod, "WhiteTigerEffect", "四段虎爪连击，逐段加深撕裂流血"));
            tooltips.Add(new TooltipLine(Mod, "WhiteTigerEffect2", "终结爪击释放三道银纹爪波"));
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<BaihuSpirit>(8)
                .AddIngredient<EmpyriteBar>(15)
                .AddIngredient<HeavenFragment>(12)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.FeralClaws;
    }

    /// <summary>星火灭杀枪 — 朱雀掉落，发射穿透珊瑚星火弹，贯穿后绽放星火星海爆裂。</summary>
    public class StarfireAnnihilator : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1520;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 56;
            Item.height = 24;
            Item.useTime = Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item11;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<StarfireShell>();
            Item.shootSpeed = 22f;
            Item.useAmmo = AmmoID.Bullet;
            Item.crit = 8;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            type = ModContent.ProjectileType<StarfireShell>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 muzzlePos = position + muzzleDir * 48f;

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 10; i++) {
                    int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                    Dust d = Dust.NewDustDirect(muzzlePos + Main.rand.NextVector2Circular(6f, 6f), 0, 0, dustType,
                        muzzleDir.X * Main.rand.NextFloat(2f, 5f), muzzleDir.Y * Main.rand.NextFloat(2f, 5f), 80, default, 1.4f);
                    d.noGravity = true;
                }
            }

            Projectile.NewProjectile(source, muzzlePos, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "StarfireLore", "「珊瑚作芯，星火贯渊」"));
            tooltips.Add(new TooltipLine(Mod, "StarfireEffect", "发射穿透珊瑚星火弹"));
            tooltips.Add(new TooltipLine(Mod, "StarfireEffect2", "贯穿敌人后绽放星火星海爆裂"));
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<SuzakuSpirit>(8)
                .AddIngredient<EmpyriteBar>(15)
                .AddIngredient<HeavenFragment>(12)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.VortexBeater;
    }

    /// <summary>珊瑚星火弹 — 穿透飞行，耗尽贯穿或撞墙后触发星火星海爆裂。</summary>
    public class StarfireShell : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/LightShot";

        private static readonly Color CoralCore = new(255, 130, 90);
        private static readonly Color StarfireGlow = new(255, 210, 80);
        private static readonly Color SeaTeal = new(70, 210, 195);

        private ref float ExplosionSpawned => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f), 0, 0, dustType,
                    -Projectile.velocity.X * 0.08f, -Projectile.velocity.Y * 0.08f, 100, default, 1.3f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, Vector3.Lerp(CoralCore.ToVector3(), StarfireGlow.ToVector3(), 0.55f) * 0.55f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 180);
        }

        public override void OnKill(int timeLeft) {
            TrySpawnExplosion();
        }

        private void TrySpawnExplosion() {
            if (ExplosionSpawned >= 1f || Projectile.owner != Main.myPlayer)
                return;

            ExplosionSpawned = 1f;

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 14; i++) {
                    int dustType = i % 3 == 0 ? DustID.SolarFlare : (i % 2 == 0 ? DustID.Torch : DustID.Water);
                    Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, 0, 0, 80, default, 1.6f);
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2CircularEdge(6f, 6f);
                }
            }

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.15f, Volume = 0.7f }, Projectile.Center);

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<StarfireExplosion>(),
                (int)(Projectile.damage * 0.55f),
                Projectile.knockBack * 0.35f,
                Projectile.owner);

            if (Main.myPlayer == Projectile.owner) {
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(4, 8);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(SeaTeal, CoralCore, progress) * (0.55f * progress);
                trailColor.A = 0;

                Main.spriteBatch.Draw(
                    tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null,
                    trailColor,
                    Projectile.oldRot[i],
                    origin,
                    new Vector2(0.35f * progress, 0.12f),
                    SpriteEffects.None,
                    0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color glow = Color.Lerp(StarfireGlow, Color.White, 0.25f);
            glow.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, glow * 0.55f, Projectile.rotation, origin, new Vector2(0.65f, 0.16f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos, null, Color.White * 0.9f, Projectile.rotation, origin, new Vector2(0.5f, 0.12f), SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>星火星海爆裂 — 珊瑚星火弹贯穿或撞墙后绽放的火焰冲击环。</summary>
    public class StarfireExplosion : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private static readonly Color CoralCore = new(255, 130, 90);
        private static readonly Color StarfireGlow = new(255, 210, 80);
        private static readonly Color SeaTeal = new(70, 210, 195);

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 36;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.ai[0]++;

            if (Main.netMode == NetmodeID.Server)
                return;

            float radius = Projectile.ai[0] * 9f;
            for (int i = 0; i < 5; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.45f, radius);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                Dust d = Dust.NewDustPerfect(pos, dustType, Main.rand.NextVector2Circular(1.5f, 1.5f), 70, default, 1.6f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, Vector3.Lerp(CoralCore.ToVector3(), StarfireGlow.ToVector3(), 0.65f) * 0.7f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 120);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = Projectile.ai[0] * 9f;
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.SoftGlow == null)
                return false;

            float progress = 1f - Projectile.timeLeft / 36f;
            float alpha = ACMUtils.QuadOut(1f - progress) * 0.85f;
            float scale = MathHelper.SmoothStep(0f, 10f, ACMUtils.QuadOut(progress));

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D glow = ACMAsset.SoftGlow;
            Color outerColor = Color.Lerp(SeaTeal, CoralCore, 0.35f) * (alpha * 0.55f);
            outerColor.A = 0;
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, outerColor, 0f, glow.Size() * 0.5f, scale * 0.55f, SpriteEffects.None, 0f);

            Color innerColor = StarfireGlow * (alpha * 0.75f);
            innerColor.A = 0;
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, innerColor, 0f, glow.Size() * 0.5f, scale * 0.28f, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null,
                Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }

    /// <summary>日轮永恒审判 — 召唤悬浮日轮眼，向敌人发射穿透阳光射线。</summary>
    public class SolarisEternalVerdict : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1600;
            Item.DamageType = DamageClass.Summon;
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item44;
            Item.autoReuse = false;
            Item.noMelee = true;
            Item.mana = 10;
            Item.shoot = ModContent.ProjectileType<SolarisEternalVerdictProj>();
            Item.shootSpeed = 0f;
            Item.buffType = ModContent.BuffType<SolarisEternalVerdictBuff>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(Item.buffType, 2);

            var projectile = Projectile.NewProjectileDirect(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI);
            projectile.originalDamage = Item.damage;

            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.2f }, player.Center);
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16;
                Vector2 vel = angle.ToRotationVector2() * 4f;
                int dust = Dust.NewDust(player.Center, 0, 0, DustID.SolarFlare, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            return false;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.OpticStaff;
    }

    public class SolarisEternalVerdictBuff : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<SolarisEternalVerdictProj>()] > 0) {
                player.buffTime[buffIndex] = 18000;
            }
            else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }

    /// <summary>日轮眼召唤物 — 悬浮于玩家身旁，周期性发射穿透阳光射线。</summary>
    public class SolarisEternalVerdictProj : ModProjectile
    {
        private ref float AttackCooldown => ref Projectile.localAI[0];
        private ref float HoverPhase => ref Projectile.localAI[1];

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.EyeLaser;

        public override void SetStaticDefaults() {
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            if (!player.active || player.dead) {
                player.ClearBuff(ModContent.BuffType<SolarisEternalVerdictBuff>());
                return;
            }

            if (player.HasBuff(ModContent.BuffType<SolarisEternalVerdictBuff>())) {
                Projectile.timeLeft = 2;
            }

            HoverPhase += 0.08f;
            if (AttackCooldown > 0) {
                AttackCooldown--;
            }

            NPC target = FindTarget(player, 800f);
            if (target != null && AttackCooldown <= 0) {
                FireSunbeam(target);
                AttackCooldown = 28;
            }

            Vector2 hoverPos = player.Center + new Vector2(
                MathF.Cos(HoverPhase) * 90f,
                MathF.Sin(HoverPhase * 0.7f) * 50f - 60f);

            Vector2 toHover = hoverPos - Projectile.Center;
            float moveSpeed = MathHelper.Clamp(toHover.Length() * 0.1f, 4f, 16f);
            Projectile.velocity = toHover.Length() > 8f
                ? Vector2.Lerp(Projectile.velocity, toHover.SafeNormalize(Vector2.Zero) * moveSpeed, 0.12f)
                : Projectile.velocity * 0.92f;

            if (target != null) {
                float aimAngle = (target.Center - Projectile.Center).ToRotation();
                Projectile.rotation = MathHelper.Lerp(Projectile.rotation, aimAngle, 0.18f);
            }
            else {
                Projectile.rotation += 0.04f;
            }

            if (Main.rand.NextBool(4)) {
                int dust = Dust.NewDust(Projectile.Center + Main.rand.NextVector2Circular(8, 8), 0, 0, DustID.SolarFlare, 0, 0, 80, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0.3f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.78f, 0.3f) * 0.6f);
        }

        private void FireSunbeam(NPC target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }

            Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
            Projectile.NewProjectile(
                Projectile.GetSource_FromAI(),
                Projectile.Center,
                direction * 18f,
                ModContent.ProjectileType<SolarisRay>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner);

            SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.4f, Volume = 0.5f }, Projectile.Center);
        }

        private static NPC FindTarget(Player player, float maxDistance) {
            if (player.HasMinionAttackTargetNPC) {
                NPC targeted = Main.npc[player.MinionAttackTargetNPC];
                if (targeted.active && targeted.CanBeChasedBy() && !targeted.friendly) {
                    if (Vector2.Distance(targeted.Center, player.Center) < maxDistance * 1.5f) {
                        return targeted;
                    }
                }
            }

            NPC closest = null;
            float closestDist = maxDistance;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.friendly) {
                    continue;
                }

                float dist = Vector2.Distance(npc.Center, player.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }

            return closest;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[ProjectileID.EyeLaser].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(HoverPhase * 2f) * 0.1f;

            Color glow = new Color(255, 200, 80) * 0.45f;
            glow.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, glow, Projectile.rotation, origin, Projectile.scale * 1.25f * pulse, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(texture, drawPos, null, Color.White, Projectile.rotation, origin, Projectile.scale * pulse, SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>阳光射线 — 日轮眼发射的穿透光束。</summary>
    public class SolarisRay : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.MinionShot[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 6;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.SolarFlare, 0, 0, 100, default, 1.4f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.15f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.75f, 0.25f) * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 120);

            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3, 3);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.SolarFlare, vel.X, vel.Y, 100, default, 1.6f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) {
                return true;
            }

            Texture2D beamTex = ACMAsset.GlaciateWave;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0, beamTex.Height / 2f);
            float length = Projectile.velocity.Length() * 4f;
            Vector2 scale = new Vector2(length / beamTex.Width, 0.12f);

            Color core = new Color(255, 240, 180);
            core.A = 0;
            Color glow = new Color(255, 180, 60);
            glow.A = 0;

            Main.spriteBatch.Draw(beamTex, drawPos, null, glow * 0.5f, Projectile.rotation, origin, scale * new Vector2(1f, 1.6f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(beamTex, drawPos, null, core * 0.85f, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);

            if (ACMAsset.LightShot != null) {
                Color orb = new Color(255, 220, 120);
                orb.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos, null, orb, 0f, ACMAsset.LightShot.Size() / 2f, 0.55f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    /// <summary>
    /// 凤凰焰杖 - 朱雀掉落的火焰法杖
    /// 释放涅槃凤凰焰弹，命中或消散后浴火重生为追踪余烬
    /// </summary>
    public class PhoenixFlameStaff : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1480;
            Item.DamageType = DamageClass.Magic;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.mana = 18;
            Item.shoot = ModContent.ProjectileType<PhoenixFlameRebirth>();
            Item.shootSpeed = 12f;
            Item.staff[Item.type] = true;
            Item.crit = 8;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.RainbowRod;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 direction = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 spawnPos = player.Center + direction * 28f;

            Projectile.NewProjectile(source, spawnPos, velocity, type, damage, knockback, player.whoAmI);

            if (Main.rand.NextBool(3)) {
                float spread = Main.rand.NextFloat(-0.12f, 0.12f);
                Vector2 twinVel = direction.RotatedBy(spread) * Item.shootSpeed * 0.85f;
                Projectile.NewProjectile(source, spawnPos, twinVel, type, (int)(damage * 0.75f), knockback * 0.75f, player.whoAmI);
            }

            return false;
        }
    }

    /// <summary>
    /// 涅槃凤凰焰 - 朱雀焰杖主弹幕，命中后浴火重生
    /// </summary>
    public class PhoenixFlameRebirth : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float HasRebirthed => ref Projectile.ai[0];
        private ref float FlamePhase => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            FlamePhase += 0.14f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            NPC target = FindClosestNPC(480f);
            if (target != null && Projectile.timeLeft > 30) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float targetAngle = toTarget.ToRotation();
                float currentAngle = Projectile.velocity.ToRotation();
                float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.06f);
                Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
            }

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.OrangeTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(0.8f, 0.8f);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.55f, 0.15f) * 0.75f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 300);
            TriggerRebirth();
        }

        public override void OnKill(int timeLeft) {
            TriggerRebirth();

            if (Main.netMode == NetmodeID.Server)
                return;

            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.GoldCoin;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }

        private void TriggerRebirth() {
            if (HasRebirthed >= 1f || Main.netMode == NetmodeID.MultiplayerClient)
                return;

            HasRebirthed = 1f;

            int emberType = ModContent.ProjectileType<PhoenixFlameRebirthEmber>();
            const int emberCount = 6;
            for (int i = 0; i < emberCount; i++) {
                float angle = MathHelper.TwoPi * i / emberCount + Main.rand.NextFloat(-0.15f, 0.15f);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(9f, 14f);
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    vel,
                    emberType,
                    (int)(Projectile.damage * 0.55f),
                    Projectile.knockBack * 0.6f,
                    Projectile.owner);
            }

            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.35f, Volume = 0.75f }, Projectile.Center);
        }

        private NPC FindClosestNPC(float maxDist) {
            NPC closest = null;
            float closestDist = maxDist;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy())
                    continue;

                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }

            return closest;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new(0f, texture.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(FlamePhase * 2f) * 0.15f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(new Color(255, 210, 90), new Color(255, 90, 40), 1f - progress);
                trailColor *= progress * 0.45f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i],
                    origin, new Vector2(0.9f * progress * pulse, 0.35f * progress), SpriteEffects.None, 0f);
            }

            Color bodyColor = new Color(255, 180, 70) * 0.75f * pulse;
            bodyColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, bodyColor, Projectile.rotation, origin,
                new Vector2(1.1f * pulse, 0.45f * pulse), SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>
    /// 涅槃余烬 - 凤凰焰重生后分裂的追踪火羽
    /// </summary>
    public class PhoenixFlameRebirthEmber : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 50;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.985f;

            NPC target = FindClosestNPC(360f);
            if (target != null && Projectile.timeLeft > 15) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float targetAngle = toTarget.ToRotation();
                float currentAngle = Projectile.velocity.ToRotation();
                float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.08f);
                Projectile.velocity = newAngle.ToRotationVector2() * Math.Max(Projectile.velocity.Length(), 8f);
            }

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Torch, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.12f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.5f, 0.15f) * 0.45f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 180);
        }

        private NPC FindClosestNPC(float maxDist) {
            NPC closest = null;
            float closestDist = maxDist;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy())
                    continue;

                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }

            return closest;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ACMAsset.LightShot ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(new Color(255, 200, 100), new Color(255, 120, 50), 1f - progress);
                trailColor *= progress * 0.4f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, 0f, origin, 0.55f * progress, SpriteEffects.None, 0f);
            }

            Color coreColor = new Color(255, 220, 120);
            coreColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, coreColor, 0f, origin, 0.65f, SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>地晶裂碎大剑 — 玄武掉落，挥砍命中时迸射熔岩地晶爆裂。</summary>
    public class GeocrystalShatterblade : ModItem
    {
        private int attackType;

        public override void SetDefaults() {
            Item.damage = 1450;
            Item.crit = 8;
            Item.DamageType = DamageClass.Melee;
            Item.width = Item.height = 60;
            Item.useTime = Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 8f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<GeocrystalShatterbladeSwing>();
            Item.shootSpeed = 3f;
        }

        public override void HoldItem(Player player) {
            if (Main.rand.NextBool(6)) {
                Vector2 pos = player.Center + Main.rand.NextVector2Circular(52f, 52f);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.Stone;
                Dust d = Dust.NewDustDirect(pos, 0, 0, dustType, 0f, -0.5f, 80, default, Main.rand.NextFloat(1.2f, 1.8f));
                d.noGravity = true;
            }

            if (Main.rand.NextBool(10)) {
                Vector2 pos = player.Center + Main.rand.NextVector2Circular(44f, 44f);
                Dust d = Dust.NewDustDirect(pos, 0, 0, DustID.CopperCoin, 0f, 0f, 60, default, 1.4f);
                d.noGravity = true;
                d.velocity *= 0.4f;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI, attackType);
            attackType = (attackType + 1) % 2;
            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "GeocrystalLore", "玄武壳甲深处凝结的地晶熔刃"));
            tooltips.Add(new TooltipLine(Mod, "GeocrystalEffect", "挥砍命中敌人时迸射熔岩地晶爆裂"));
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.BreakerBlade;
    }

    /// <summary>
    /// 坤铉破渊权杖 — 玄武地尊裂岩杖，在光标处裂地召唤七柱地能岩柱。
    /// </summary>
    public class GeoarchonRupturer : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1500;
            Item.DamageType = DamageClass.Magic;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.mana = 20;
            Item.shoot = ModContent.ProjectileType<GeoarchonMarker>();
            Item.shootSpeed = 0f;
            Item.staff[Item.type] = true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<XuanwuSpirit>(8)
                .AddIngredient<EmpyriteBar>(15)
                .AddIngredient<HeavenFragment>(12)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 target = Main.MouseWorld;

            Projectile.NewProjectile(source, target, Vector2.Zero, type, damage, knockback, player.whoAmI);

            if (player.whoAmI == Main.myPlayer) {
                for (int i = 0; i < 10; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                    int dust = Dust.NewDust(target + new Vector2(Main.rand.NextFloat(-24f, 24f), -2f), 0, 0, DustID.Stone, vel.X, vel.Y, 80, default, 1.2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.2f, Volume = 0.7f }, target);
            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "GeoarchonLore", "「坤脉既裂，七柱镇渊」"));
            tooltips.Add(new TooltipLine(Mod, "GeoarchonEffect", "在光标处刻印地脉裂穴阵"));
            tooltips.Add(new TooltipLine(Mod, "GeoarchonEffect2", "预兆后自地面刺出七柱地能岩柱"));
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.StaffofEarth;
    }

    /// <summary>
    /// 玄龟盾 — 玄武龟甲盾刃，盾击冲刺与格挡反伤。
    /// </summary>
    public class BlackTortoiseShield : ModItem
    {
        public const float BlockDamageReduction = 0.25f;
        public const float ReflectMultiplier = 1.15f;

        public override void SetDefaults() {
            Item.damage = 1550;
            Item.crit = 8;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 10f;
            Item.value = Item.sellPrice(platinum: 2, gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shieldSlot = 1;
            Item.shoot = ModContent.ProjectileType<BlackTortoiseShieldBash>();
            Item.shootSpeed = 16f;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<XuanwuSpirit>(8)
                .AddIngredient<EmpyriteBar>(15)
                .AddIngredient<HeavenFragment>(12)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.Center + velocity.SafeNormalize(Vector2.UnitX) * 24f, velocity, type, damage, knockback, player.whoAmI);

            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.15f, Volume = 0.85f }, player.Center);

            if (player.whoAmI == Main.myPlayer) {
                player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(5, 10);
            }

            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "BlackTortoiseLore", "「龟甲为盾，格挡即反刃」"));
            tooltips.Add(new TooltipLine(Mod, "BlackTortoiseEffect", "左键释放玄龟盾击冲刺"));
            tooltips.Add(new TooltipLine(Mod, "BlackTortoiseEffect2", "右键举盾：龟甲纹减伤25%，格挡反还115%所受伤害"));
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.AnkhShield;
    }
}
