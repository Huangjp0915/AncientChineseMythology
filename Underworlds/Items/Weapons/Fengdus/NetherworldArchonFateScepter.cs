using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Fengdus
{
    /// <summary>
    /// 冥府至尊判官司命杖 - 终极魔法权杖
    /// 发射5枚命运法令追踪灵球
    /// 命中敌人后施加"命运烙印"，3秒后引爆造成额外伤害
    /// 烙印期间敌人受到的所有伤害都被记录，引爆时以1.5倍回响
    /// </summary>
    public class NetherworldArchonFateScepter : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 3800;
            Item.crit = 18;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 12;
            Item.width = 50;
            Item.height = 50;
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6f;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item8;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<FateDecreeOrb>();
            Item.shootSpeed = 16f;
            Item.staff[Item.type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            for (int i = 0; i < 5; i++) {
                Vector2 perturbedVel = velocity.RotatedBy(MathHelper.ToRadians(Main.rand.NextFloat(-18f, 18f)));
                perturbedVel *= Main.rand.NextFloat(0.85f, 1.15f);
                Vector2 spawnOffset = player.Center + velocity.SafeNormalize(Vector2.UnitX) * 30f + Main.rand.NextVector2Circular(15f, 15f);
                Projectile.NewProjectile(source, spawnOffset, perturbedVel, type, damage, knockback, player.whoAmI);
            }
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<StaveofNetherEclipse>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 20)
                .AddIngredient<SoulFragment>(50)
                .AddIngredient<UmbralStoneItem>(100)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 命运法令灵球 - 追踪敌人的审判之球
    /// </summary>
    public class FateDecreeOrb : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/NetherworldArchonFateScepter";

        private ref float Timer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 16;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.alpha = 80;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation += 0.12f;

            // Homing after 10 frames
            if (Timer > 10f) {
                NPC target = FindTarget(1200f);
                if (target != null) {
                    Vector2 desiredVel = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 22f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, 0.06f);
                }
            }

            if (Projectile.velocity.Length() > 24f)
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 24f;

            // Trail particles
            for (int i = 0; i < 2; i++) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(6, 6),
                    4, 4, DustID.GreenTorch,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    80, default, Main.rand.NextFloat(1.5f, 2.5f));
                trail.noGravity = true;
            }
            if (Main.rand.NextBool(3)) {
                Dust gold = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(10, 10), 4, 4,
                    DustID.GoldFlame, 0f, -1.5f, 100, default, 1.5f);
                gold.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.3f, 0.7f, 0.2f);
        }

        private NPC FindTarget(float maxDist) {
            NPC closest = null;
            float best = maxDist;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < best) { best = dist; closest = npc; }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.BrokenArmor, 600);
            target.AddBuff(BuffID.Ichor, 600);

            // Apply Fate Mark (spawn mark projectile on the enemy)
            bool alreadyMarked = false;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == ModContent.ProjectileType<FateMarkProj>()
                    && p.owner == Projectile.owner && (int)p.ai[0] == target.whoAmI) {
                    alreadyMarked = true;
                    p.timeLeft = 180; // refresh
                    p.ai[1] += damageDone; // add damage
                    break;
                }
            }
            if (!alreadyMarked) {
                int markType = ModContent.ProjectileType<FateMarkProj>();
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                    markType, Projectile.damage, 0f, Projectile.owner, target.whoAmI, damageDone);
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = 0.5f }, target.Center);
            }

            // Impact burst
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.GreenTorch, vel, 60, default, Main.rand.NextFloat(2f, 3f));
                burst.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 origin = softGlow.Size() / 2f;

                // Trail
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float progress = 1f - (float)i / Projectile.oldPos.Length;
                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Color trailColor = Color.Lerp(new Color(80, 200, 80), new Color(200, 180, 50), 1f - progress) * progress * 0.5f;
                    trailColor.A = 0;
                    Main.EntitySpriteDraw(softGlow, drawPos, null, trailColor, 0f, origin, 0.5f * progress, SpriteEffects.None, 0);
                }

                // Core glow
                float pulse = 0.7f + MathF.Sin(Timer * 0.3f) * 0.15f;
                Color coreColor = new Color(100, 220, 80) * 0.8f;
                coreColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, coreColor, 0f, origin, pulse, SpriteEffects.None, 0);

                // Gold halo
                Color haloColor = new Color(220, 190, 60) * 0.4f;
                haloColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, haloColor, 0f, origin, pulse * 1.5f, SpriteEffects.None, 0);
            }

            // BlankStar rotating overlay
            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 starOrigin = blankStar.Size() / 2f;
                Color starColor = new Color(180, 220, 80) * 0.5f;
                starColor.A = 0;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, starColor,
                    Projectile.rotation, starOrigin, 0.12f, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Dust death = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.GreenTorch, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 80, default, 2f);
                death.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 命运烙印 - 附着在敌人身上的持久烙印
    /// 3秒后引爆，造成累积伤害×1.5的额外伤害
    /// </summary>
    public class FateMarkProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/NetherworldArchonFateScepter";

        private ref float TargetNPC => ref Projectile.ai[0];
        private ref float AccumulatedDamage => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];
        private const int DetonationTime = 180; // 3 seconds

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = DetonationTime;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
        }

        public override bool? CanDamage() => false;
        public override bool? CanCutTiles() => false;

        public override void AI() {
            Timer++;
            int targetIdx = (int)TargetNPC;
            if (targetIdx < 0 || targetIdx >= Main.maxNPCs || !Main.npc[targetIdx].active) {
                Projectile.Kill();
                return;
            }

            NPC target = Main.npc[targetIdx];
            Projectile.Center = target.Center + new Vector2(0, -target.height * 0.7f);
            Projectile.rotation += 0.05f;

            float progress = Timer / DetonationTime;
            float opacity = MathHelper.Clamp(Timer / 15f, 0f, 1f);

            // Countdown visual: increasing particles and glow
            int particleCount = (int)(1 + progress * 5);
            for (int i = 0; i < particleCount; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(15f, 35f) * (1f - progress * 0.5f);
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 2f;
                Dust countdown = Dust.NewDustPerfect(pos, DustID.GreenTorch, vel, 60,
                    default, Main.rand.NextFloat(1f, 2f) * opacity);
                countdown.noGravity = true;
            }

            if (Main.rand.NextBool(3)) {
                Dust gold = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10, 10),
                    DustID.GoldFlame, new Vector2(0, -Main.rand.NextFloat(1f, 2f)), 80, default, 1.5f * opacity);
                gold.noGravity = true;
            }

            // Pulsing light effect as detonation approaches
            float lightIntensity = 0.3f + progress * 0.7f;
            Lighting.AddLight(Projectile.Center, 0.2f * lightIntensity, 0.8f * lightIntensity, 0.2f * lightIntensity);
        }

        public override void OnKill(int timeLeft) {
            int targetIdx = (int)TargetNPC;
            if (targetIdx < 0 || targetIdx >= Main.maxNPCs) return;
            NPC target = Main.npc[targetIdx];
            if (!target.active) return;

            // Detonate! Deal accumulated damage × 1.5
            int bonusDamage = (int)(AccumulatedDamage * 1.5f);
            if (bonusDamage < Projectile.damage) bonusDamage = Projectile.damage * 3;

            target.SimpleStrikeNPC(bonusDamage, 0, false, 0f, null, false, 0, true);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.5f, Pitch = -0.3f }, target.Center);

            // AOE detonation to nearby enemies
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC nearby = Main.npc[i];
                if (!nearby.CanBeChasedBy() || nearby.whoAmI == targetIdx) continue;
                if (Vector2.Distance(target.Center, nearby.Center) < 300f) {
                    nearby.SimpleStrikeNPC(bonusDamage / 2, 0, false, 0f, null, false, 0, true);
                    nearby.AddBuff(BuffID.BrokenArmor, 300);
                }
            }

            // Detonation visual: Sparkle explosion + judgment ring
            Texture2D sparkle = ACMAsset.Sparkle;
            // particles
            for (int i = 0; i < 50; i++) {
                float angle = MathHelper.TwoPi / 50f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(8f, 18f);
                Dust ring = Dust.NewDustPerfect(target.Center, DustID.GreenTorch, vel, 40, default, Main.rand.NextFloat(2.5f, 4f));
                ring.noGravity = true;
            }
            for (int i = 0; i < 30; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.GoldFlame, vel, 60, default, Main.rand.NextFloat(2f, 3.5f));
                burst.noGravity = true;
            }
            for (int i = 0; i < 20; i++) {
                Dust pillar = Dust.NewDustPerfect(target.Center,
                    DustID.GreenTorch, new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(8f, 20f)),
                    40, default, Main.rand.NextFloat(2f, 3.5f));
                pillar.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = Timer / DetonationTime;
            float opacity = MathHelper.Clamp(Timer / 15f, 0f, 1f);

            // Fate mark symbol - BlankStar
            Texture2D blankStar = ACMAsset.BlankStar;
            if (blankStar != null) {
                Vector2 origin = blankStar.Size() / 2f;
                float pulse = 0.15f + MathF.Sin(Timer * 0.2f) * 0.03f + progress * 0.08f;
                Color markColor = Color.Lerp(new Color(80, 200, 80), new Color(220, 200, 50), progress) * opacity * 0.7f;
                markColor.A = 0;
                Main.EntitySpriteDraw(blankStar, Projectile.Center - Main.screenPosition, null, markColor,
                    Projectile.rotation, origin, pulse, SpriteEffects.None, 0);
            }

            // SoftGlow aura
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 origin = softGlow.Size() / 2f;
                float auraSize = 0.8f + progress * 0.5f + MathF.Sin(Timer * 0.15f) * 0.2f;
                Color auraColor = new Color(60, 180, 60) * opacity * 0.4f;
                auraColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, auraColor,
                    0f, origin, auraSize, SpriteEffects.None, 0);

                // Gold ring approaching detonation
                Color ringColor = new Color(220, 190, 60) * opacity * progress * 0.5f;
                ringColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, ringColor,
                    0f, origin, auraSize * 1.5f, SpriteEffects.None, 0);
            }

            // Sparkle overlay near detonation
            if (progress > 0.6f) {
                Texture2D sparkle = ACMAsset.Sparkle;
                if (sparkle != null) {
                    Vector2 origin = sparkle.Size() / 2f;
                    float sparkleOpacity = (progress - 0.6f) / 0.4f;
                    Color sparkleColor = new Color(220, 220, 100) * sparkleOpacity * opacity * 0.4f;
                    sparkleColor.A = 0;
                    float scale = 0.1f + sparkleOpacity * 0.1f;
                    Main.EntitySpriteDraw(sparkle, Projectile.Center - Main.screenPosition, null, sparkleColor,
                        Timer * 0.1f, origin, scale, SpriteEffects.None, 0);
                }
            }

            return false;
        }
    }
}
