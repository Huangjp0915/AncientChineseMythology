using AncientChineseMythology.Celestias.Boss.Vaisravanas;
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

namespace AncientChineseMythology.Celestias.Boss.Vaisravanas.Items
{
    /// <summary>
    /// 宝塔法杖 - 毗沙门天王掉落的魔法武器
    /// 在光标处召唤层叠宝塔，同一位置可叠至六层；拾取钱币为宝塔增伤
    /// </summary>
    public class TreasurePagodaStaff : ModItem
    {
        private const int MaxActivePagodas = 4;
        private const float StackMergeRange = 72f;

        public override void SetDefaults() {
            Item.damage = 1320;
            Item.DamageType = DamageClass.Magic;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.mana = 18;
            Item.shoot = ModContent.ProjectileType<TreasurePagodaStack>();
            Item.shootSpeed = 0f;
            Item.staff[Item.type] = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.RainbowRod;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return false;

            Vector2 targetPos = Main.MouseWorld;

            if (TryStackExistingPagoda(player, targetPos))
                return false;

            if (player.ownedProjectileCounts[type] >= MaxActivePagodas)
                KillOldestPagoda(player, type);

            Projectile.NewProjectile(source, targetPos, Vector2.Zero, type, damage, knockback, player.whoAmI, 1f);
            SpawnCastEffects(player, targetPos);
            return false;
        }

        private static bool TryStackExistingPagoda(Player player, Vector2 targetPos) {
            float mergeRangeSq = StackMergeRange * StackMergeRange;
            int projType = ModContent.ProjectileType<TreasurePagodaStack>();

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != player.whoAmI || proj.type != projType)
                    continue;

                if (Vector2.DistanceSquared(proj.Center, targetPos) > mergeRangeSq)
                    continue;

                if (proj.ai[0] >= TreasurePagodaStack.MaxStack)
                    return false;

                proj.ai[0]++;
                proj.timeLeft = Math.Max(proj.timeLeft, TreasurePagodaStack.GetLifetimeForStack((int)proj.ai[0]));
                proj.netUpdate = true;

                SpawnStackEffects(proj.Center, (int)proj.ai[0]);

                if (player.whoAmI == Main.myPlayer && proj.ai[0] >= TreasurePagodaStack.MaxStack)
                    player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 14);

                return true;
            }

            return false;
        }

        private static void KillOldestPagoda(Player player, int type) {
            Projectile oldest = null;
            int oldestTimeLeft = int.MaxValue;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != player.whoAmI || proj.type != type)
                    continue;

                if (proj.timeLeft < oldestTimeLeft) {
                    oldestTimeLeft = proj.timeLeft;
                    oldest = proj;
                }
            }

            oldest?.Kill();
        }

        private static void SpawnCastEffects(Player player, Vector2 targetPos) {
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.15f, Volume = 0.85f }, targetPos);

            for (int i = 0; i < 14; i++) {
                float angle = MathHelper.TwoPi * i / 14f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 6f);
                int dust = Dust.NewDust(targetPos, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 1.6f);
                Main.dust[dust].noGravity = true;
            }

            Vector2 toTarget = targetPos - player.Center;
            int lineCount = Math.Min(12, (int)(toTarget.Length() / 36f));
            for (int i = 0; i < lineCount; i++) {
                float progress = (float)i / Math.Max(1, lineCount);
                Vector2 linePos = Vector2.Lerp(player.Center, targetPos, progress);
                int dust = Dust.NewDust(linePos, 0, 0, DustID.GoldFlame, 0, 0, 120, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }
        }

        private static void SpawnStackEffects(Vector2 center, int stackTier) {
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.1f + stackTier * 0.05f, Volume = 0.75f }, center);

            for (int i = 0; i < 10 + stackTier * 2; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f + stackTier, 4f + stackTier);
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.GoldFlame;
                int dust = Dust.NewDust(center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.4f + stackTier * 0.15f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "PagodaLore", "「宝塔层叠，财宝聚灵」"));
            tooltips.Add(new TooltipLine(Mod, "PagodaEffect", "在光标处召唤宝塔，同处可叠至六层"));
            tooltips.Add(new TooltipLine(Mod, "PagodaEffect2", "拾取钱币为宝塔增伤，层数越高范围与伤害越强"));
        }
    }

    /// <summary>
    /// 宝塔法杖 - 拾取钱币增伤
    /// </summary>
    public class TreasurePagodaStaffPlayer : ModPlayer
    {
        public const int MaxFortuneStacks = 24;
        private const int FortuneDecayInterval = 240;

        public int FortuneStacks { get; private set; }
        private int fortuneDecayTimer;

        public float GetFortuneDamageMultiplier() => 1f + FortuneStacks * 0.04f;

        public override void ResetEffects() {
            fortuneDecayTimer++;
            if (fortuneDecayTimer >= FortuneDecayInterval && FortuneStacks > 0) {
                fortuneDecayTimer = 0;
                FortuneStacks--;
            }
        }

        public override bool OnPickup(Item item) {
            if (!HasTreasurePagodaStaff())
                return true;

            int gained = item.type switch {
                ItemID.CopperCoin => 1,
                ItemID.SilverCoin => 3,
                ItemID.GoldCoin => 8,
                ItemID.PlatinumCoin => 20,
                _ => 0
            };

            if (gained <= 0)
                return true;

            FortuneStacks = Math.Min(MaxFortuneStacks, FortuneStacks + gained);
            fortuneDecayTimer = 0;

            if (Player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.CoinPickup with { Pitch = MathHelper.Clamp(FortuneStacks * 0.02f, 0f, 0.6f), Volume = 0.55f }, Player.Center);

                for (int i = 0; i < Math.Min(8, gained); i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(3f, 3f);
                    int dust = Dust.NewDust(Player.Center, 0, 0, DustID.GoldCoin, vel.X, vel.Y - 2f, 100, default, 1.3f);
                    Main.dust[dust].noGravity = true;
                }
            }

            return true;
        }

        private bool HasTreasurePagodaStaff() {
            int staffType = ModContent.ItemType<TreasurePagodaStaff>();

            for (int i = 0; i < Player.inventory.Length; i++) {
                if (Player.inventory[i].type == staffType)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 层叠宝塔 - 驻留于目标处，层数越高范围与伤害越强
    /// </summary>
    public class TreasurePagodaStack : ModProjectile
    {
        public const int MaxStack = 6;
        private const int BaseLifetime = 360;
        private const int LifetimePerStack = 60;

        private ref float StackTier => ref Projectile.ai[0];
        private ref float PulseTimer => ref Projectile.localAI[0];
        private ref float RiseProgress => ref Projectile.localAI[1];

        private Player Owner => Main.player[Projectile.owner];

        public static int GetLifetimeForStack(int stackTier) => BaseLifetime + stackTier * LifetimePerStack;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 96;
            Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = BaseLifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
        }

        public override void OnSpawn(IEntitySource source) {
            StackTier = MathHelper.Clamp(StackTier <= 0f ? 1f : StackTier, 1f, MaxStack);
            Projectile.timeLeft = GetLifetimeForStack((int)StackTier);
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            RiseProgress = MathHelper.Clamp(RiseProgress + 0.08f, 0f, 1f);
            PulseTimer++;

            float radius = GetRadius();
            Projectile.width = Projectile.height = (int)(radius * 2.2f);

            if (PulseTimer >= 20f) {
                PulseTimer = 0f;
                PulseAreaDamage(radius);
            }

            SpawnPagodaParticles(radius);
            Lighting.AddLight(Projectile.Center, VaisravanaHelper.TowerGold.ToVector3() * (0.35f + StackTier * 0.08f));

            if (Projectile.timeLeft < 30)
                Projectile.alpha = (int)(255 * (1f - Projectile.timeLeft / 30f));
        }

        private float GetRadius() => 56f + StackTier * 18f;

        private float GetDamageMultiplier() {
            float fortuneMult = Owner.GetModPlayer<TreasurePagodaStaffPlayer>().GetFortuneDamageMultiplier();
            return StackTier * fortuneMult;
        }

        private void PulseAreaDamage(float radius) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int damage = Math.Max(1, (int)(Projectile.damage * GetDamageMultiplier() * 0.35f));
            float radiusSq = radius * radius;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy())
                    continue;

                if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > radiusSq)
                    continue;

                npc.SimpleStrikeNPC(damage, Owner.direction, false, Projectile.knockBack);
            }

            if (Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f + StackTier * 0.05f, Volume = 0.35f }, Projectile.Center);
            }
        }

        private void SpawnPagodaParticles(float radius) {
            if (Main.netMode == NetmodeID.Server)
                return;

            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.4f, radius);
                int dustType = Main.rand.NextBool(3) ? DustID.GoldCoin : DustID.GoldFlame;
                int dust = Dust.NewDust(pos, 0, 0, dustType, 0, 0, 100, default, 1.2f + StackTier * 0.1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(1.5f, 3.5f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D towerTex = VaisravanaHelper.TowerTexture ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = towerTex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float alpha = 1f - Projectile.alpha / 255f;
            float riseOffset = MathHelper.Lerp(24f, 0f, RiseProgress);
            drawPos.Y -= riseOffset;

            float baseScale = 0.55f + StackTier * 0.12f;
            float pulse = 1f + MathF.Sin(PulseTimer * 0.18f) * 0.06f;
            float scale = baseScale * pulse;

            Color glow = VaisravanaHelper.TowerGold;
            glow.A = 0;
            sb.Draw(towerTex, drawPos, null, glow * alpha * 0.45f, 0f, origin, scale * 1.15f, SpriteEffects.None, 0f);

            Color core = VaisravanaHelper.PureWhite;
            core.A = 0;
            sb.Draw(towerTex, drawPos, null, core * alpha * 0.75f, 0f, origin, scale, SpriteEffects.None, 0f);

            sb.Draw(towerTex, drawPos, null, lightColor * alpha, 0f, origin, scale * 0.92f, SpriteEffects.None, 0f);

            VaisravanaHelper.DrawDivineCircle(sb, Projectile.Center - new Vector2(0f, riseOffset), GetRadius() * 0.85f,
                VaisravanaHelper.ImmortalGold, Main.GameUpdateCount * 0.02f, alpha * 0.35f);

            for (int tier = 1; tier < (int)StackTier; tier++) {
                float ghostScale = scale * (0.55f + tier * 0.08f);
                Vector2 ghostPos = drawPos - new Vector2(0f, tier * 10f * RiseProgress);
                Color ghostColor = VaisravanaHelper.CelestialAzure;
                ghostColor.A = 0;
                sb.Draw(towerTex, ghostPos, null, ghostColor * alpha * 0.25f, 0f, origin, ghostScale, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 12 + (int)StackTier * 3; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f + StackTier, 4f + StackTier);
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.GoldFlame;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 库藏虚空狙 - 毗沙门天王掉落的狙击枪
    /// 发射生长型虚空弹，命中后坍缩并吸引附近敌人
    /// </summary>
    public class VaultshadeVoidshot : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1280;
            Item.crit = 8;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 56;
            Item.height = 24;
            Item.useTime = Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item36;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<VaultshadeVoidBolt>();
            Item.shootSpeed = 24f;
            Item.useAmmo = AmmoID.Bullet;
        }

        public override Vector2? HoldoutOffset() => new Vector2(-10, 0);

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity,
            ref int type, ref int damage, ref float knockback) {
            type = ModContent.ProjectileType<VaultshadeVoidBolt>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 muzzlePos = position + muzzleDir * 52f;

            Projectile.NewProjectile(source, muzzlePos, velocity, type, damage, knockback, player.whoAmI);

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 5; i++) {
                    Vector2 dustVel = -muzzleDir.RotatedByRandom(0.25f) * Main.rand.NextFloat(2f, 5f);
                    Dust d = Dust.NewDustPerfect(muzzlePos, DustID.Shadowflame, dustVel, 60,
                        new Color(120, 80, 200), Main.rand.NextFloat(1f, 1.6f));
                    d.noGravity = true;
                }
            }

            return false;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.SniperRifle;
    }

    /// <summary>
    /// 天冠权杖 — 毗沙门天王掉落的法师武器
    /// 释放五枚耀能环，螺旋收束后追踪冲刺
    /// </summary>
    public class CelestialCircletScepter : ModItem
    {
        private const int RingCount = 5;

        public override void SetDefaults() {
            Item.damage = 1300;
            Item.DamageType = DamageClass.Magic;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.mana = 16;
            Item.shoot = ModContent.ProjectileType<CelestialCircletOrb>();
            Item.shootSpeed = 12f;
            Item.staff[Item.type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 direction = velocity.SafeNormalize(Vector2.UnitX);
            float baseAngle = direction.ToRotation();

            for (int i = 0; i < RingCount; i++) {
                float ringAngle = baseAngle + MathHelper.TwoPi * i / RingCount;
                Vector2 ringDir = ringAngle.ToRotationVector2();
                Vector2 spawnPos = position + ringDir * 28f;
                Vector2 ringVel = ringDir * Item.shootSpeed * 0.55f;

                Projectile.NewProjectile(source, spawnPos, ringVel, type, damage, knockback, player.whoAmI, ai0: MathHelper.TwoPi * i / RingCount);
            }

            return false;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.StaffofRegrowth;
    }

    /// <summary>
    /// 天枢耀能光环 — 螺旋收束后追踪冲刺的耀能环弹幕
    /// </summary>
    public class CelestialCircletOrb : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float RingPhase => ref Projectile.ai[0];
        private ref float SpiralTimer => ref Projectile.localAI[0];
        private ref float IsHoming => ref Projectile.localAI[1];

        private float pulsePhase;

        private const float SpiralDuration = 48f;
        private const float LaunchSpeed = 6.6f;
        private const float DashSpeed = 22f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 200;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            pulsePhase += 0.14f;
            Projectile.rotation += 0.22f;

            if (IsHoming < 0.5f) {
                SpiralTimer++;

                if (SpiralTimer < SpiralDuration) {
                    float progress = SpiralTimer / SpiralDuration;
                    float spiralAngle = RingPhase + SpiralTimer * 0.18f;
                    float spiralRadius = MathHelper.Lerp(72f, 6f, progress);
                    Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    Vector2 perp = forward.RotatedBy(MathHelper.PiOver2);
                    Projectile.Center += perp * MathF.Sin(spiralAngle) * spiralRadius * 0.14f;

                    float targetSpeed = MathHelper.Lerp(LaunchSpeed, 10f, progress);
                    if (Projectile.velocity.Length() < targetSpeed) {
                        Projectile.velocity = Vector2.Lerp(Projectile.velocity, forward * targetSpeed, 0.06f);
                    }
                }
                else {
                    IsHoming = 1f;
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.4f, Volume = 0.55f }, Projectile.Center);
                        for (int i = 0; i < 8; i++) {
                            float ang = MathHelper.TwoPi * i / 8f;
                            Vector2 vel = ang.ToRotationVector2() * 4f;
                            int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.WhiteTorch, vel.X, vel.Y, 100, default, 1.4f);
                            Main.dust[dust].noGravity = true;
                        }
                    }
                }
            }
            else {
                NPC target = FindClosestNPC(720f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float currentAngle = Projectile.velocity.ToRotation();
                    float targetAngle = toTarget.ToRotation();
                    float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.14f);
                    float speed = MathHelper.Clamp(Projectile.velocity.Length() + 0.35f, DashSpeed * 0.6f, DashSpeed);
                    Projectile.velocity = newAngle.ToRotationVector2() * speed;
                }
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.98f, 0.92f) * 0.55f);
        }

        private NPC FindClosestNPC(float maxRange) {
            NPC closest = null;
            float closestDist = maxRange;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile)) continue;

                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }

            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 6; i++) {
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.WhiteTorch, 0, 0, 80, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(4f, 4f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            float alpha = Projectile.timeLeft < 20 ? Projectile.timeLeft / 20f : 1f;

            VaisravanaHelper.DrawImmortalOrb(sb, Projectile.Center,
                VaisravanaHelper.PureWhite * alpha,
                VaisravanaHelper.ImmortalGold,
                0.55f, pulsePhase);

            if (ACMAsset.LightShot != null) {
                Color trailColor = VaisravanaHelper.SpiritSilver * 0.45f * alpha;
                trailColor.A = 0;

                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float fade = alpha * (1f - i / (float)Projectile.oldPos.Length);
                    Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    float scale = 0.35f * (1f - i * 0.05f);
                    sb.Draw(ACMAsset.LightShot, pos, null, trailColor * fade, 0f,
                        ACMAsset.LightShot.Size() / 2f, scale, SpriteEffects.None, 0);
                }
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.WhiteTorch, vel.X, vel.Y, 100, default, 1.3f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 宝塔护符 — 毗沙门 25% 掉落
    /// 减伤 + 荆棘反震，受击时召唤宝塔虚影反击
    /// </summary>
    public class TreasurePagodaCharm : ModItem
    {
        public const float DamageReduction = 0.15f;
        public const float ThornsStrength = 1f;
        public const int BonusDefense = 10;

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 32;
            Item.value = Item.sellPrice(platinum: 1);
            Item.rare = ItemRarityID.Red;
            Item.accessory = true;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.endurance += DamageReduction;
            player.statDefense += BonusDefense;
            player.thorns = MathHelper.Max(player.thorns, ThornsStrength);
            player.GetModPlayer<TreasurePagodaCharmPlayer>().pagodaWard = true;
        }

        public override string Texture => "Terraria/Images/Item_" + ItemID.PaladinsShield;
    }

    /// <summary>宝塔护符受击反震逻辑。</summary>
    public class TreasurePagodaCharmPlayer : ModPlayer
    {
        public bool pagodaWard;
        private int retaliateCooldown;

        public override void ResetEffects() {
            pagodaWard = false;
        }

        public override void PreUpdate() {
            if (retaliateCooldown > 0)
                retaliateCooldown--;
        }

        public override void OnHitByNPC(NPC npc, Player.HurtInfo hurtInfo) {
            TryRetaliate(npc, hurtInfo);
        }

        public override void OnHitByProjectile(Projectile proj, Player.HurtInfo hurtInfo) {
            if (!pagodaWard || proj.friendly || !proj.hostile)
                return;

            int npcIndex = ResolveShooterIndex(proj);
            if (npcIndex >= 0)
                TryRetaliate(Main.npc[npcIndex], hurtInfo, proj.Center);
        }

        private void TryRetaliate(NPC npc, Player.HurtInfo hurtInfo, Vector2? origin = null) {
            if (!pagodaWard || retaliateCooldown > 0 || !npc.active || npc.friendly)
                return;

            retaliateCooldown = 12;

            Vector2 spawn = origin ?? Player.Center;
            Vector2 direction = (npc.Center - spawn).SafeNormalize(Vector2.UnitY);
            int damage = Math.Max(80, (int)(hurtInfo.SourceDamage * TreasurePagodaCharm.ThornsStrength * 0.35f + Player.statDefense * 2));

            Projectile.NewProjectile(
                Player.GetSource_OnHurt(hurtInfo.DamageSource),
                spawn,
                direction * 18f,
                ModContent.ProjectileType<TreasurePagodaPhantom>(),
                damage,
                4f,
                Player.whoAmI,
                ai0: npc.whoAmI);

            if (VaultUtils.isServer)
                return;

            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.55f, Pitch = 0.25f }, spawn);

            for (int i = 0; i < 8; i++) {
                Vector2 dustVel = direction.RotatedByRandom(0.6f) * Main.rand.NextFloat(2f, 5f);
                int dust = Dust.NewDust(spawn, 0, 0, DustID.GoldFlame, dustVel.X, dustVel.Y, 80, default, 1.2f);
                Main.dust[dust].noGravity = true;
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

    /// <summary>
    /// 宝塔虚影 — 受击反震时飞向攻击者的护体反击弹幕
    /// </summary>
    public class TreasurePagodaPhantom : ModProjectile
    {
        public override string Texture => VaisravanaHelper.Path + "VaisravanaTower";

        private float glowPhase;
        private float phantomAlpha;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 48;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            glowPhase += 0.14f;
            phantomAlpha = MathHelper.Lerp(phantomAlpha, 1f, 0.16f);

            int targetIndex = (int)Projectile.ai[0];
            if (targetIndex >= 0 && targetIndex < Main.maxNPCs) {
                NPC target = Main.npc[targetIndex];
                if (target.active && !target.friendly) {
                    Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 20f, 0.12f);
                }
            }

            Projectile.rotation = MathHelper.WrapAngle(MathF.Sin(glowPhase * 0.8f) * 0.08f);

            if (Projectile.timeLeft < 12)
                phantomAlpha = Projectile.timeLeft / 12f;

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(12, 16);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.WhiteTorch, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.12f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.85f) * 0.55f * phantomAlpha);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer)
                return;

            for (int i = 0; i < 6; i++) {
                Vector2 dustVel = Main.rand.NextVector2Circular(4f, 4f);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.GoldFlame, dustVel.X, dustVel.Y, 70, default, 1.3f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer)
                return;

            for (int i = 0; i < 10; i++) {
                Vector2 dustVel = Main.rand.NextVector2CircularEdge(4f, 4f);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.WhiteTorch, dustVel.X, dustVel.Y, 90, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D towerTex = VaisravanaHelper.TowerTexture ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = towerTex.Size() / 2f;
            float pulse = 1f + MathF.Sin(glowPhase * 2.5f) * 0.1f;

            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = VaisravanaHelper.SpiritSilver * progress * phantomAlpha * 0.35f;
                trailColor.A = 0;

                sb.Draw(towerTex, trailPos, null, trailColor, Projectile.oldRot[i], origin,
                    0.55f * pulse * (0.7f + progress * 0.3f), SpriteEffects.None, 0f);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            if (ACMAsset.SoftGlow != null) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);

                Color outerGlow = VaisravanaHelper.TowerGold * phantomAlpha * 0.35f;
                outerGlow.A = 0;
                sb.Draw(ACMAsset.SoftGlow, drawPos, null, outerGlow, 0f,
                    ACMAsset.SoftGlow.Size() / 2f, 1.4f * pulse, SpriteEffects.None, 0f);

                Color coreGlow = VaisravanaHelper.PureWhite * phantomAlpha * 0.25f;
                coreGlow.A = 0;
                sb.Draw(ACMAsset.SoftGlow, drawPos, null, coreGlow, 0f,
                    ACMAsset.SoftGlow.Size() / 2f, 0.8f * pulse, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
            }

            Color bodyColor = Color.Lerp(VaisravanaHelper.TowerGold, VaisravanaHelper.PureWhite, 0.35f) * phantomAlpha * 0.85f;
            bodyColor.A = 0;
            sb.Draw(towerTex, drawPos, null, bodyColor, Projectile.rotation, origin, 0.65f * pulse, SpriteEffects.None, 0f);

            Color highlight = VaisravanaHelper.DivineWhite * phantomAlpha * 0.55f;
            highlight.A = 0;
            sb.Draw(towerTex, drawPos, null, highlight, Projectile.rotation, origin, 0.42f * pulse, SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>
    /// 库藏虚空弹 - 飞行中持续生长，命中后触发虚空坍缩
    /// </summary>
    public class VaultshadeVoidBolt : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/LightShot";

        private ref float GrowProgress => ref Projectile.ai[0];

        private const float MaxGrow = 1f;
        private const int GrowDuration = 72;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            GrowProgress = MathHelper.Clamp(GrowProgress + 1f / GrowDuration, 0f, MaxGrow);
            Projectile.rotation = Projectile.velocity.ToRotation();

            float scale = GetVisualScale();
            Projectile.width = Projectile.height = (int)MathHelper.Lerp(12f, 34f, GrowProgress);

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    DustID.Shadowflame, -Projectile.velocity * 0.08f, 80,
                    Color.Lerp(new Color(90, 50, 160), VaisravanaHelper.SpiritSilver, GrowProgress),
                    scale * 0.45f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center,
                new Vector3(0.35f, 0.25f, 0.55f) * (0.4f + GrowProgress * 0.6f));
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.SourceDamage *= 1f + GrowProgress * 0.35f;
        }

        public override void OnKill(int timeLeft) {
            SpawnCollapse();
        }

        private void SpawnCollapse() {
            if (Projectile.owner != Main.myPlayer)
                return;

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<VaultshadeVoidCollapse>(),
                (int)(Projectile.damage * 0.55f), Projectile.knockBack * 0.5f, Projectile.owner,
                ai0: GrowProgress);

            SoundEngine.PlaySound(SoundID.Item104 with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
        }

        private float GetVisualScale() => MathHelper.Lerp(0.35f, 1.1f, GrowProgress);

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            float scale = GetVisualScale();
            Color voidCore = Color.Lerp(new Color(110, 60, 200), VaisravanaHelper.PureWhite, GrowProgress * 0.45f);
            Color voidGlow = Color.Lerp(new Color(70, 35, 130), VaisravanaHelper.CelestialAzure, GrowProgress * 0.35f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D lsh = ACMAsset.LightShot;
            Texture2D sg = ACMAsset.SoftGlow;

            for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float fade = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * (0.35f + GrowProgress * 0.35f);
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                sb.Draw(lsh, pos, null, voidGlow * fade, Projectile.oldRot[i],
                    lsh.Size() * 0.5f, new Vector2(scale * 0.55f, scale * 0.12f), SpriteEffects.None, 0);
            }

            sb.Draw(lsh, Projectile.Center - Main.screenPosition, null, voidCore, Projectile.rotation,
                lsh.Size() * 0.5f, new Vector2(scale * 0.75f, scale * 0.14f), SpriteEffects.None, 0);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null, voidGlow * 0.55f, 0f,
                sg.Size() * 0.5f, scale * 0.55f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    /// <summary>
    /// 虚空坍缩 - 命中后展开引力场，将敌人拉向中心并造成范围伤害
    /// </summary>
    public class VaultshadeVoidCollapse : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

        private ref float SourceGrow => ref Projectile.ai[0];

        private const int Duration = 50;
        private const float MaxRadius = 200f;
        private const float PullStrength = 6.5f;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Duration;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() {
            float progress = 1f - Projectile.timeLeft / (float)Duration;
            return progress > 0.15f && progress < 0.85f;
        }

        public override void AI() {
            float progress = 1f - Projectile.timeLeft / (float)Duration;
            float radius = MaxRadius * MathHelper.SmoothStep(0f, 1f, progress) * (0.85f + SourceGrow * 0.25f);

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;

                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist >= radius || dist < 12f) continue;

                float pullMult = (1f - dist / radius) * (0.45f + progress * 0.55f);
                Vector2 pull = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * PullStrength * pullMult;
                npc.velocity += pull;
            }

            if (Main.netMode != NetmodeID.Server) {
                int particleCount = (int)(3 + progress * 8);
                for (int i = 0; i < particleCount; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float ringRadius = Main.rand.NextFloat(radius * 0.35f, radius);
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * ringRadius;
                    Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 5f);
                    vel = vel.RotatedBy(MathHelper.PiOver4 * 0.35f);

                    Dust d = Dust.NewDustPerfect(pos, DustID.Shadowflame, vel, 70,
                        Color.Lerp(new Color(80, 45, 150), VaisravanaHelper.SpiritSilver, progress), Main.rand.NextFloat(1.2f, 2.2f));
                    d.noGravity = true;
                }
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.45f, 0.35f, 0.7f) * (1f - progress * 0.5f));

            if (Projectile.timeLeft == 1 && Projectile.owner == Main.myPlayer)
                Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(4, 8);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float progress = 1f - Projectile.timeLeft / (float)Duration;
            float radius = MaxRadius * MathHelper.SmoothStep(0f, 1f, progress) * (0.85f + SourceGrow * 0.25f);
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius * 0.55f, targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = 1f - Projectile.timeLeft / (float)Duration;
            float radius = MaxRadius * MathHelper.SmoothStep(0f, 1f, progress) * (0.85f + SourceGrow * 0.25f);
            float alpha = MathHelper.SmoothStep(1f, 0f, progress) * 0.85f;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D sg = ACMAsset.SoftGlow;
            Texture2D sparkle = ACMAsset.Sparkle;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Color voidColor = Color.Lerp(new Color(90, 50, 180), VaisravanaHelper.CelestialAzure, progress * 0.4f);
            sb.Draw(sg, drawPos, null, voidColor * (alpha * 0.45f), 0f,
                sg.Size() * 0.5f, radius / 64f, SpriteEffects.None, 0);

            if (sparkle != null) {
                sb.Draw(sparkle, drawPos, null, VaisravanaHelper.PureWhite * (alpha * 0.35f),
                    Main.GlobalTimeWrappedHourly * 2f, sparkle.Size() * 0.5f, radius / 90f, SpriteEffects.None, 0);
            }

            VaisravanaHelper.DrawImmortalHalo(sb, Projectile.Center, radius * 0.75f,
                VaisravanaHelper.SpiritSilver, -Main.GameUpdateCount * 0.02f, alpha * 0.55f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
