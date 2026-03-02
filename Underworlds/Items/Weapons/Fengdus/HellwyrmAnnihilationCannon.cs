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
    /// 地狱冥龙吐纳寂灭炮 - 终极远程炮
    /// 无需弹药，发射一道膨胀的冥龙息吐纳波
    /// 龙息沿途灼烧一切，命中后标记敌人，2秒后延迟引爆
    /// 不同于EX版本的散弹模式，本体是单发大型扩散弹
    /// </summary>
    public class HellwyrmAnnihilationCannon : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 12200;
            Item.crit = 20;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 76;
            Item.height = 34;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 14f;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item36 with { Volume = 1.5f, Pitch = -0.6f };
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<DragonBreathWave>();
            Item.shootSpeed = 16f;
            Item.staff[Type] = true;
        }

        public override Vector2? HoldoutOffset() => new Vector2(-14, 2);

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 muzzlePos = position + muzzleDir * 50f;

            Projectile.NewProjectile(source, muzzlePos, velocity, type, damage, knockback, player.whoAmI);

            for (int i = 0; i < 40; i++) {
                Vector2 smokeVel = muzzleDir.RotatedByRandom(MathHelper.ToRadians(35)) * Main.rand.NextFloat(4f, 12f);
                Dust smoke = Dust.NewDustPerfect(muzzlePos, DustID.Smoke, smokeVel, 180, new Color(40, 20, 10), Main.rand.NextFloat(2.5f, 4f));
                smoke.noGravity = true;
            }
            for (int i = 0; i < 25; i++) {
                Vector2 sparkVel = muzzleDir.RotatedByRandom(MathHelper.ToRadians(25)) * Main.rand.NextFloat(6f, 16f);
                Dust spark = Dust.NewDustPerfect(muzzlePos, DustID.Torch, sparkVel, 60, new Color(255, 120, 30), Main.rand.NextFloat(2.5f, 4f));
                spark.noGravity = true;
            }

            player.velocity -= muzzleDir * 5f;
            return false;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            position += velocity.SafeNormalize(Vector2.Zero) * 30f;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<SoulEatingCannon>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 20)
                .AddIngredient<SoulFragment>(50)
                .AddIngredient<UmbralStoneItem>(100)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 冥龙吐纳波 - 巨大的膨胀龙息弹
    /// 随时间膨胀，沿途持续造成伤害和灼烧
    /// </summary>
    public class DragonBreathWave : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/HellwyrmAnnihilationCannon";
        private ref float Timer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.97f;

            float expansion = MathHelper.Clamp(Timer / 30f, 0f, 3f);
            Projectile.scale = 1f + expansion;

            float brightness = MathHelper.Clamp(1f - Timer / 90f, 0.2f, 1f);
            Lighting.AddLight(Projectile.Center, 2f * brightness, 0.8f * brightness, 0.2f * brightness);

            int particleCount = 3 + (int)(expansion * 3);
            for (int i = 0; i < particleCount; i++) {
                float radius = 15f * Projectile.scale;
                Vector2 offset = Main.rand.NextVector2Circular(radius, radius);
                Dust fire = Dust.NewDustDirect(
                    Projectile.Center + offset, 4, 4, DustID.Torch,
                    -Projectile.velocity.X * 0.3f + Main.rand.NextFloat(-2f, 2f),
                    -Projectile.velocity.Y * 0.3f + Main.rand.NextFloat(-2f, 2f),
                    60, new Color(255, Main.rand.Next(80, 180), 20), Main.rand.NextFloat(2f, 3.5f));
                fire.noGravity = true;
            }

            if (Timer > 10) {
                for (int i = 0; i < 2; i++) {
                    float smokeRadius = 20f * Projectile.scale;
                    Vector2 smokeOffset = Main.rand.NextVector2Circular(smokeRadius, smokeRadius);
                    Dust smoke = Dust.NewDustDirect(
                        Projectile.Center + smokeOffset, 8, 8, DustID.Smoke,
                        Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, 0f),
                        200, new Color(30, 10, 5), Main.rand.NextFloat(2f, 4f));
                    smoke.noGravity = true;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = 20f * Projectile.scale + 20f;
            Vector2 closestPoint = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
            return Vector2.Distance(Projectile.Center, closestPoint) < radius;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 600);
            target.AddBuff(BuffID.Ichor, 600);

            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.Torch, vel, 60, new Color(255, 140, 30), Main.rand.NextFloat(2f, 3f));
                burst.noGravity = true;
            }

            int delayedBoom = ModContent.ProjectileType<DragonAnnihilationMark>();
            bool alreadyMarked = false;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && Main.projectile[i].type == delayedBoom
                    && Main.projectile[i].owner == Projectile.owner && Main.projectile[i].ai[1] == target.whoAmI) {
                    alreadyMarked = true;
                    break;
                }
            }

            if (!alreadyMarked) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                    delayedBoom, Projectile.damage * 2, 0f, Projectile.owner, 0f, target.whoAmI);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = Timer / 90f;
            float opacity = 1f - progress * 0.6f;

            Texture2D smoke = ACMAsset.Smoke;
            if (smoke != null) {
                int frame = (int)(Timer * 0.4f) % 16;
                int frameX = frame % 4;
                int frameY = frame / 4;
                int frameW = smoke.Width / 4;
                int frameH = smoke.Height / 4;
                Rectangle sourceRect = new Rectangle(frameX * frameW, frameY * frameH, frameW, frameH);
                Vector2 smokeOrigin = new Vector2(frameW / 2f, frameH / 2f);

                Color fireColor = Color.Lerp(new Color(255, 180, 40), new Color(200, 60, 10), progress) * opacity * 0.7f;
                fireColor.A = 0;
                float smokeScale = 0.15f * Projectile.scale;
                Main.EntitySpriteDraw(smoke, Projectile.Center - Main.screenPosition, sourceRect, fireColor, Projectile.rotation, smokeOrigin, smokeScale, SpriteEffects.None, 0);

                Color darkSmoke = new Color(40, 10, 5) * opacity * 0.4f;
                darkSmoke.A = 0;
                Main.EntitySpriteDraw(smoke, Projectile.Center - Main.screenPosition, sourceRect, darkSmoke, Projectile.rotation + 0.5f, smokeOrigin, smokeScale * 1.3f, SpriteEffects.None, 0);
            }

            Texture2D emberShards = ACMAsset.EmberShards;
            if (emberShards != null) {
                Vector2 emberOrigin = emberShards.Size() / 2f;
                Color emberColor = new Color(255, 120, 20) * opacity * 0.6f;
                emberColor.A = 0;
                float emberScale = 0.2f * Projectile.scale;
                Main.EntitySpriteDraw(emberShards, Projectile.Center - Main.screenPosition, null, emberColor, Timer * 0.1f, emberOrigin, emberScale, SpriteEffects.None, 0);
            }

            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 glowOrigin = softGlow.Size() / 2f;
                Color coreGlow = new Color(255, 200, 80) * opacity * 0.9f;
                coreGlow.A = 0;
                float pulse = 1f + MathF.Sin(Timer * 0.3f) * 0.2f;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, coreGlow, 0f, glowOrigin, Projectile.scale * pulse, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = -0.3f }, Projectile.Center);
            for (int i = 0; i < 40; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(14f, 14f);
                Dust fire = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 60, new Color(255, 140, 30), Main.rand.NextFloat(2.5f, 4f));
                fire.noGravity = true;
            }
            for (int i = 0; i < 20; i++) {
                Vector2 smokeVel = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-8f, -2f));
                Dust smoke = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, smokeVel, 200, new Color(30, 10, 5), Main.rand.NextFloat(3f, 5f));
                smoke.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 毁灭印记 - 附着在敌人身上，2秒后引爆
    /// </summary>
    public class DragonAnnihilationMark : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/HellwyrmAnnihilationCannon";
        private ref float Timer => ref Projectile.ai[0];
        private ref float TargetWhoAmI => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Timer++;
            int targetIdx = (int)TargetWhoAmI;
            if (targetIdx < 0 || targetIdx >= Main.maxNPCs || !Main.npc[targetIdx].active) {
                Projectile.Kill();
                return;
            }

            NPC target = Main.npc[targetIdx];
            Projectile.Center = target.Center;

            float pulse = MathF.Sin(Timer * 0.3f) * 0.5f + 0.5f;
            Lighting.AddLight(target.Center, 1.5f * pulse, 0.5f * pulse, 0.1f * pulse);

            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 30f;
                Dust mark = Dust.NewDustPerfect(pos, DustID.Torch, (target.Center - pos).SafeNormalize(Vector2.Zero) * 2f, 60, new Color(255, 100, 20), 1.5f);
                mark.noGravity = true;
            }

            if (Timer >= 120) {
                Projectile.friendly = true;
                Projectile.position -= new Vector2(120, 120);
                Projectile.width = 240;
                Projectile.height = 240;
                Projectile.Damage();

                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.8f, Pitch = -0.8f }, target.Center);

                for (int i = 0; i < 50; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(16f, 16f);
                    Dust ring = Dust.NewDustPerfect(target.Center, DustID.Torch, vel, 40, new Color(255, 80, 10), Main.rand.NextFloat(3f, 5f));
                    ring.noGravity = true;
                }
                for (int i = 0; i < 30; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(12f, 12f);
                    vel.Y -= 4f;
                    Dust smoke = Dust.NewDustPerfect(target.Center, DustID.Smoke, vel, 200, new Color(40, 15, 5), Main.rand.NextFloat(3f, 5f));
                    smoke.noGravity = true;
                }

                Lighting.AddLight(target.Center, 4f, 2f, 0.5f);
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = Timer / 120f;
            Texture2D sparkle = ACMAsset.Sparkle;
            if (sparkle != null) {
                Vector2 origin = sparkle.Size() / 2f;
                Color sparkColor = Color.Lerp(new Color(255, 180, 40), new Color(255, 40, 10), progress) * (0.4f + progress * 0.6f);
                sparkColor.A = 0;
                float scale = 0.3f + progress * 0.5f;
                Main.EntitySpriteDraw(sparkle, Projectile.Center - Main.screenPosition, null, sparkColor, Timer * 0.2f, origin, scale, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
