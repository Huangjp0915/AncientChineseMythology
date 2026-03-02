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
    /// 噩梦罗生门万咒葬神典 - 终极魔法典籍
    /// 在光标位置召唤罗生门，持续4秒
    /// 门会吸引并持续伤害周围敌人，每秒释放噩梦触手追踪敌人
    /// 同一时间仅允许一扇门存在
    /// </summary>
    public class NightmareRashomonMyriadCurseTome : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 5660;
            Item.crit = 20;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 15;
            Item.width = 38;
            Item.height = 38;
            Item.useTime = 28;
            Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 8f;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item103;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<RashomonGateProj>();
            Item.shootSpeed = 0f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, Main.MouseWorld, Vector2.Zero, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<RashomonGateProj>()] < 3;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<CodexofMyriadDemons>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 20)
                .AddIngredient<SoulFragment>(50)
                .AddIngredient<UmbralStoneItem>(100)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 罗生门 - 持续4秒的虚空之门
    /// 吸引敌人 + 持续伤害 + 定期释放噩梦触手
    /// </summary>
    public class RashomonGateProj : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/NightmareRashomonMyriadCurseTome";

        private ref float Timer => ref Projectile.ai[0];
        private const int Duration = 240; // 4 seconds
        private const float PullRadius = 500f;
        private const float DamageRadius = 200f;
        private const float PullStrength = 6f;

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Duration;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;

            float lifeProgress = Timer / Duration;
            float fadeIn = MathHelper.Clamp(Timer / 20f, 0f, 1f);
            float fadeOut = MathHelper.Clamp((Duration - Timer) / 30f, 0f, 1f);
            float opacity = fadeIn * fadeOut;

            // Pull enemies toward gate
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < PullRadius && dist > 20f) {
                    float pullMult = 1f - (dist / PullRadius);
                    Vector2 pull = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * PullStrength * pullMult;
                    npc.velocity += pull;
                }
            }

            // Spawn nightmare tendrils every 60 frames
            if (Timer % 60 == 0 && Timer < Duration - 30) {
                NPC target = FindNearestTarget(800f);
                if (target != null) {
                    int tendrilType = ModContent.ProjectileType<NightmareTendril>();
                    for (int i = 0; i < 3; i++) {
                        float angle = MathHelper.TwoPi / 3f * i + Main.rand.NextFloat(-0.3f, 0.3f);
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f;
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                            tendrilType, Projectile.damage, Projectile.knockBack * 0.5f, Projectile.owner);
                    }
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
                }
            }

            // Spawn gate particles
            SpawnGateParticles(opacity);

            // Rotation effect
            Projectile.rotation += 0.03f;

            // Lighting
            Lighting.AddLight(Projectile.Center, 0.8f * opacity, 0.2f * opacity, 0.4f * opacity);
        }

        private NPC FindNearestTarget(float maxDist) {
            NPC closest = null;
            float bestDist = maxDist;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < bestDist) { bestDist = dist; closest = npc; }
            }
            return closest;
        }

        private void SpawnGateParticles(float opacity) {
            // Vortex particles spiraling inward
            for (int i = 0; i < 6; i++) {
                float angle = Timer * 0.08f + MathHelper.TwoPi / 6f * i;
                float radius = 80f + Main.rand.NextFloat(-10f, 30f);
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 3f;
                vel = vel.RotatedBy(MathHelper.PiOver4);
                Dust vortex = Dust.NewDustPerfect(pos, DustID.Shadowflame, vel, 80,
                    default, Main.rand.NextFloat(1.5f, 2.5f) * opacity);
                vortex.noGravity = true;
            }

            // Inner crimson fire
            for (int i = 0; i < 3; i++) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(40, 40);
                Dust fire = Dust.NewDustPerfect(pos, DustID.Torch, Main.rand.NextVector2Circular(2f, 2f),
                    60, new Color(180, 20, 60), Main.rand.NextFloat(2f, 3f) * opacity);
                fire.noGravity = true;
            }

            // Outer eldritch green mist
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(60f, 120f);
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                Dust mist = Dust.NewDustPerfect(pos, DustID.CursedTorch,
                    (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 2f, 100,
                    default, Main.rand.NextFloat(1.5f, 2.5f) * opacity);
                mist.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 600);
            target.AddBuff(BuffID.CursedInferno, 600);
            target.AddBuff(BuffID.Slow, 300);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp(Timer / 20f, 0f, 1f);
            float fadeOut = MathHelper.Clamp((Duration - Timer) / 30f, 0f, 1f);
            float opacity = fadeIn * fadeOut;

            // Draw the Rashomon Gate using Smoke spritesheet as rotating vortex
            Texture2D smoke = ACMAsset.Smoke;
            if (smoke != null) {
                int frameSize = smoke.Width / 4;
                int frame = ((int)(Timer * 0.3f)) % 16;
                int frameX = frame % 4;
                int frameY = frame / 4;
                Rectangle sourceRect = new Rectangle(frameX * frameSize, frameY * frameSize, frameSize, frameSize);
                Vector2 origin = new Vector2(frameSize / 2f, frameSize / 2f);

                // Outer vortex layer (green-tinted)
                Color outerColor = new Color(60, 180, 80) * opacity * 0.4f;
                outerColor.A = 0;
                Main.EntitySpriteDraw(smoke, Projectile.Center - Main.screenPosition, sourceRect, outerColor,
                    Projectile.rotation, origin, 0.8f, SpriteEffects.None, 0);

                // Inner vortex layer (crimson)
                int frame2 = ((int)(Timer * 0.3f) + 8) % 16;
                int f2X = frame2 % 4;
                int f2Y = frame2 / 4;
                Rectangle src2 = new Rectangle(f2X * frameSize, f2Y * frameSize, frameSize, frameSize);
                Color innerColor = new Color(200, 30, 60) * opacity * 0.5f;
                innerColor.A = 0;
                Main.EntitySpriteDraw(smoke, Projectile.Center - Main.screenPosition, src2, innerColor,
                    -Projectile.rotation * 1.3f, origin, 0.5f, SpriteEffects.None, 0);
            }

            // SoftGlow core - pulsing aura
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                float pulse = 1.5f + MathF.Sin(Timer * 0.15f) * 0.4f;

                // Dark crimson core
                Color coreColor = new Color(180, 20, 50) * opacity * 0.7f;
                coreColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, coreColor,
                    0f, glowOrigin, pulse, SpriteEffects.None, 0);

                // Green outer halo
                Color haloColor = new Color(40, 120, 60) * opacity * 0.35f;
                haloColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, haloColor,
                    0f, glowOrigin, pulse * 2f, SpriteEffects.None, 0);
            }

            // ElectricArcSheet for tendril-like energy radiating outward
            Texture2D arcSheet = ACMAsset.ElectricArcSheet;
            if (arcSheet != null) {
                int arcRows = 4;
                int arcFrameHeight = arcSheet.Height / arcRows;
                int currentRow = ((int)(Timer * 0.2f)) % arcRows;
                Rectangle arcRect = new Rectangle(0, currentRow * arcFrameHeight, arcSheet.Width, arcFrameHeight);
                Vector2 arcOrigin = new Vector2(0, arcFrameHeight / 2f);

                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi / 6f * i + Timer * 0.02f;
                    Color arcColor = Color.Lerp(new Color(200, 30, 60), new Color(60, 180, 80), MathF.Sin(Timer * 0.05f + i) * 0.5f + 0.5f) * opacity * 0.35f;
                    arcColor.A = 0;
                    float arcScale = 0.15f + MathF.Sin(Timer * 0.1f + i * 0.7f) * 0.03f;
                    Main.EntitySpriteDraw(arcSheet, Projectile.Center - Main.screenPosition, arcRect, arcColor,
                        angle, arcOrigin, new Vector2(arcScale, arcScale * 0.8f), SpriteEffects.None, 0);
                }
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = 0.3f }, Projectile.Center);
            // Gate collapse implosion
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.TwoPi / 40f * i;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Main.rand.NextFloat(8f, 16f);
                Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame, vel, 60, default, Main.rand.NextFloat(2f, 3.5f));
                ring.noGravity = true;
            }
            for (int i = 0; i < 20; i++) {
                Dust fire = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30, 30),
                    DustID.CursedTorch, Main.rand.NextVector2Circular(6f, 6f), 60, default, Main.rand.NextFloat(2f, 3f));
                fire.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 噩梦触手 - 从罗生门释放的追踪触手
    /// </summary>
    public class NightmareTendril : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/NightmareRashomonMyriadCurseTome";

        private ref float Timer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.alpha = 80;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // Initial scatter for 15 frames, then home
            if (Timer > 15f) {
                NPC target = FindTarget(900f);
                if (target != null) {
                    Vector2 desiredVel = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 18f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, 0.08f);
                }
            }

            // Speed cap
            if (Projectile.velocity.Length() > 20f)
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;

            // Trail particles
            for (int i = 0; i < 2; i++) {
                Dust trail = Dust.NewDustDirect(Projectile.Center - Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(8, 8),
                    4, 4, Main.rand.NextBool() ? DustID.Shadowflame : DustID.CursedTorch,
                    -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    100, default, Main.rand.NextFloat(1.5f, 2.5f));
                trail.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.15f, 0.3f);
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
            target.AddBuff(BuffID.CursedInferno, 300);
            target.AddBuff(BuffID.ShadowFlame, 300);

            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.Shadowflame, vel, 60, default, Main.rand.NextFloat(1.5f, 2.5f));
                burst.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D arcSheet = ACMAsset.ElectricArcSheet;
            if (arcSheet != null) {
                int arcRows = 4;
                int arcFrameHeight = arcSheet.Height / arcRows;
                int currentRow = ((int)(Timer * 0.3f)) % arcRows;
                Rectangle arcRect = new Rectangle(0, currentRow * arcFrameHeight, arcSheet.Width, arcFrameHeight);
                Vector2 arcOrigin = new Vector2(0, arcFrameHeight / 2f);

                // Draw trail with arc sheet
                for (int i = 0; i < Projectile.oldPos.Length; i += 2) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float progress = 1f - (float)i / Projectile.oldPos.Length;
                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Color trailColor = Color.Lerp(new Color(200, 30, 60), new Color(40, 180, 80), 1f - progress) * progress * 0.5f;
                    trailColor.A = 0;
                    float scale = 0.06f * progress;
                    Main.EntitySpriteDraw(arcSheet, drawPos, arcRect, trailColor,
                        Projectile.oldRot[i], arcOrigin, new Vector2(scale, scale * 0.6f), SpriteEffects.None, 0);
                }
            }

            // SoftGlow for the tendril head
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 origin = softGlow.Size() / 2f;
                Color headColor = new Color(220, 40, 80) * 0.7f;
                headColor.A = 0;
                float pulse = 0.6f + MathF.Sin(Timer * 0.3f) * 0.15f;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, headColor,
                    0f, origin, pulse, SpriteEffects.None, 0);

                Color outerColor = new Color(80, 200, 100) * 0.3f;
                outerColor.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, outerColor,
                    0f, origin, pulse * 1.5f, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 15; i++) {
                Dust death = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Shadowflame, Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f), 80, default, 2f);
                death.noGravity = true;
            }
        }
    }
}
