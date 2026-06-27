using AncientChineseMythology.Helpers;
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
    /// 混元吞天万羽坠神弓 - 终极远程弓
    /// 无需箭矢，拉弓凝聚混元祖气成弑神冰矢
    /// 主箭命中后在空中撕裂混元之门，降下大量堕天羽箭持续轰击
    /// </summary>
    public class PrimordialChaosDeicideBow : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 5400;
            Item.crit = 24;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 36;
            Item.height = 80;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 10f;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item5 with { Pitch = -0.4f };
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<ChaosDeicideArrow>();
            Item.shootSpeed = 28f;
        }

        public override Vector2? HoldoutOffset() => new Vector2(-4, 0);

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);

            for (int i = 0; i < 20; i++) {
                Vector2 vel = velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(MathHelper.ToRadians(25)) * Main.rand.NextFloat(5f, 14f);
                Dust d = Dust.NewDustPerfect(position + velocity.SafeNormalize(Vector2.Zero) * 30f, DustID.PurpleTorch, vel, 60, default, Main.rand.NextFloat(1.5f, 2.5f));
                d.noGravity = true;
            }
            for (int i = 0; i < 10; i++) {
                Vector2 vel = velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(MathHelper.ToRadians(15)) * Main.rand.NextFloat(3f, 8f);
                Dust d = Dust.NewDustPerfect(position, DustID.BlueTorch, vel, 40, default, Main.rand.NextFloat(1.2f, 2f));
                d.noGravity = true;
            }
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<DamnedSoulguide>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 20)
                .AddIngredient<SoulFragment>(50)
                .AddIngredient<UmbralStoneItem>(100)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    /// <summary>
    /// 混元弑神冰矢 - 主弹幕，命中后撕裂混元之门
    /// </summary>
    public class ChaosDeicideArrow : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/PrimordialChaosDeicideBow";
        private ref float Timer => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 22;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.5f, 0.3f, 1.5f);

            for (int i = 0; i < 3; i++) {
                Dust trail = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(8, 8),
                    4, 4, DustID.PurpleTorch,
                    -Projectile.velocity.X * 0.3f, -Projectile.velocity.Y * 0.3f,
                    80, default, Main.rand.NextFloat(1.5f, 2.5f));
                trail.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(12, 12),
                    4, 4, DustID.BlueTorch, 0f, -1f, 60, default, 1.5f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 600);
            target.AddBuff(BuffID.ShadowFlame, 600);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center, ACMWeaponBurst.AbyssPurple, 1f, Projectile.owner);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.PurpleTorch, vel, 60, default, Main.rand.NextFloat(2f, 3f));
                burst.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = 0.2f }, Projectile.Center);

            // 撕裂混元之门的"破口"泛光
            ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center, ACMWeaponBurst.AbyssPurple, 2.2f, Projectile.owner);

            int rainZone = ModContent.ProjectileType<ChaosRainZone>();
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                rainZone, Projectile.damage, 0f, Projectile.owner);

            for (int i = 0; i < 30; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(12f, 12f);
                Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, vel, 60, default, Main.rand.NextFloat(2f, 3.5f));
                ring.noGravity = true;
            }
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                vel.Y -= 4f;
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueTorch, vel, 40, default, Main.rand.NextFloat(1.5f, 2.5f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 弑神冰矢: 双层 ribbon 拖尾 (外暗紫 + 内冰白)
            WeaponVFX.DrawProjectileTrail(Projectile, 18f,
                new Color(80, 30, 180) * 0.9f, new Color(190, 150, 255),
                ACMAsset.SoftGlow, uvScroll: 0.06f, subdivisions: 3);

            // BeamGrad 主箭锋线 (沿飞行方向的冰紫锐锋)
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            ACMShaders.DrawBeam(Projectile.Center - dir * 46f, Projectile.Center + dir * 14f, 11f,
                new Color(200, 170, 255), new Color(50, 30, 170), 0.95f,
                flowSpeed: 2.8f, flowScale: 2.2f, coreSharp: 3f);

            WeaponVFX.DrawGlowBurst(Projectile.Center, 1.2f, new Color(150, 90, 255) * 0.8f);
            return false;
        }
    }

    /// <summary>
    /// 混元之门 - 在命中点持续降下堕天羽箭
    /// </summary>
    public class ChaosRainZone : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/PrimordialChaosDeicideBow";
        private ref float Timer => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Timer++;

            if (Timer % 6 == 0) {
                int arrowType = ModContent.ProjectileType<ChaosRainArrow>();
                for (int i = 0; i < 2; i++) {
                    Vector2 spawnPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-200f, 200f), -600f);
                    Vector2 vel = (Projectile.Center + Main.rand.NextVector2Circular(80f, 30f) - spawnPos).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(18f, 26f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawnPos, vel, arrowType,
                        (int)(Projectile.damage * 0.4f), 2f, Projectile.owner);
                }
            }

            float progress = Timer / 180f;
            Lighting.AddLight(Projectile.Center, 0.6f * (1f - progress), 0.3f * (1f - progress), 1.2f * (1f - progress));

            for (int i = 0; i < 3; i++) {
                float angle = Timer * 0.1f + i * MathHelper.TwoPi / 3f;
                float radius = 80f + MathF.Sin(Timer * 0.05f) * 30f;
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.3f) * radius;
                Dust vortex = Dust.NewDustPerfect(pos, DustID.PurpleTorch, (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 2f, 80, default, 1.5f);
                vortex.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = Timer / 180f;
            float fadeIn = MathHelper.Clamp(Timer / 18f, 0f, 1f);
            float opacity = fadeIn * (1f - progress * 0.5f);

            // 混元之门: ArenaRunic 法阵符环地纹 (世界对齐的混元结界口)
            Effect runic = ACMShaders.ArenaRunic;
            if (runic != null) {
                ACMShaders.WorldDecalParams(Projectile.Center, 160f, out Vector2 uv, out float rFrac, out float aspect);
                runic.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                runic.Parameters["uCenter"]?.SetValue(uv);
                runic.Parameters["uRadius"]?.SetValue(rFrac);
                runic.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(opacity, 0f, 1f));
                runic.Parameters["uAspect"]?.SetValue(aspect);
                runic.Parameters["uColorPrimary"]?.SetValue(new Color(150, 90, 255).ToVector4());
                runic.Parameters["uColorSecondary"]?.SetValue(new Color(120, 200, 255).ToVector4());
                runic.Parameters["uRuneFreq"]?.SetValue(13f);
                runic.Parameters["uMode"]?.SetValue(0f);  // 法阵地纹
                runic.Parameters["uShape"]?.SetValue(0f);
                ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, runic);
            }

            // 门心暗芯 + 紫晕
            Texture2D softGlow = ACMAsset.SoftGlow;
            if (softGlow != null) {
                Vector2 origin = softGlow.Size() / 2f;
                Color dark = new Color(14, 6, 30) * (0.8f * opacity);
                dark.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, dark, 0f, origin, 2.4f + MathF.Sin(Timer * 0.12f) * 0.3f, SpriteEffects.None, 0);
                Color halo = new Color(150, 90, 255) * (opacity * 0.5f);
                halo.A = 0;
                Main.EntitySpriteDraw(softGlow, Projectile.Center - Main.screenPosition, null, halo, 0f, origin, 3.4f, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 堕天羽箭 - 从天而降的混元箭雨
    /// </summary>
    public class ChaosRainArrow : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Fengdus/PrimordialChaosDeicideBow";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.3f, 0.15f, 0.7f);

            Dust trail = Dust.NewDustDirect(
                Projectile.Center - Projectile.velocity * 0.3f, 4, 4, DustID.PurpleTorch,
                -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                100, default, Main.rand.NextFloat(1f, 1.5f));
            trail.noGravity = true;

            if (Main.rand.NextBool(3)) {
                Dust feather = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(6, 6),
                    4, 4, DustID.BlueTorch, 0f, -0.5f, 80, default, 1f);
                feather.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 300);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.PurpleTorch, vel, 60, default, Main.rand.NextFloat(1.2f, 2f));
                burst.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 堕天羽箭: 轻量双层 ribbon 飘带 (外暗紫 + 内冰白)
            WeaponVFX.DrawProjectileTrail(Projectile, 9f,
                new Color(90, 40, 200) * 0.8f, new Color(180, 140, 255),
                ACMAsset.SoftGlow, uvScroll: 0.08f, subdivisions: 1);

            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.7f, new Color(140, 60, 220) * 0.6f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.PurpleTorch,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f),
                    80, default, Main.rand.NextFloat(1f, 1.8f));
                death.noGravity = true;
            }
        }
    }
}
