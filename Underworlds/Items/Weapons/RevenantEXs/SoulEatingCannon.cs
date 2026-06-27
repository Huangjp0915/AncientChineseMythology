using AncientChineseMythology.Helpers;
using AncientChineseMythology.Underworlds.Boss.Corpseses.Items;
using AncientChineseMythology.Underworlds.Items.Weapons.Revenants;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.RevenantEXs
{
    /// <summary>
    /// 冥府噬魂幽火炮 - NetherfireBlunderbuss的终极升级版
    /// 能吞噬灵魂的重型幽冥火器，散射大量噬魂弹药
    /// 特殊机制：发射10-15发噬魂弹，命中后灵魂爆裂，击杀后吸收灵魂回复生命
    /// </summary>
    public class SoulEatingCannon : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 880;
            Item.crit = 18;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 68;
            Item.height = 30;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 12f;
            Item.value = Item.buyPrice(gold: 80);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item36;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<SoulEatingBullet>();
            Item.shootSpeed = 14f;
            Item.useAmmo = AmmoID.Bullet;
            Item.staff[Type] = true;
        }

        public override Vector2? HoldoutOffset() { return new Vector2(-12, 2); }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int soulBullet = ModContent.ProjectileType<SoulEatingBullet>();
            int count = Main.rand.Next(10, 16);
            for (int i = 0; i < count; i++) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(15));
                perturbedSpeed *= Main.rand.NextFloat(0.75f, 1.3f);
                Projectile.NewProjectile(source, position, perturbedSpeed, soulBullet, damage, knockback, player.whoAmI);
            }
            // 巨大的炮口焰效果
            Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 muzzlePos = position + muzzleDir * 40f;
            for (int i = 0; i < 30; i++) {
                Vector2 smokeVel = muzzleDir.RotatedByRandom(MathHelper.ToRadians(40)) * Main.rand.NextFloat(3f, 10f);
                Dust smoke = Dust.NewDustPerfect(muzzlePos, DustID.Smoke, smokeVel, 150, new Color(100, 40, 160), Main.rand.NextFloat(2f, 3.5f));
                smoke.noGravity = true;
            }
            for (int i = 0; i < 20; i++) {
                Vector2 sparkVel = muzzleDir.RotatedByRandom(MathHelper.ToRadians(30)) * Main.rand.NextFloat(5f, 14f);
                Dust spark = Dust.NewDustPerfect(muzzlePos, DustID.PurpleTorch, sparkVel, 80, default, Main.rand.NextFloat(2f, 3.5f));
                spark.noGravity = true;
            }
            for (int i = 0; i < 10; i++) {
                Vector2 flameVel = muzzleDir.RotatedByRandom(MathHelper.ToRadians(20)) * Main.rand.NextFloat(4f, 8f);
                Dust flame = Dust.NewDustPerfect(muzzlePos, DustID.Shadowflame, flameVel, 100, default, Main.rand.NextFloat(2.5f, 4f));
                flame.noGravity = true;
            }
            // 后坐力
            player.velocity -= muzzleDir * 3f;
            // 升级演出: 炮口 RadialBloom 大闪 (纯视觉, 仅本机)
            SoulCannonMuzzleFlash.Spawn(source, muzzlePos, player.whoAmI);
            WeaponVFX.AddScreenShake(player, 3f);
            return false;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            position += velocity.SafeNormalize(Vector2.Zero) * 25f;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<NetherfireBlunderbuss>(1)
                .AddIngredient(ModContent.ItemType<Corpsefragments>(), 10)
                .AddIngredient<SoulFragment>(20)
                .AddIngredient<UmbralStoneItem>(50)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    public class SoulEatingBullet : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/RevenantEXs/SoulEatingCannon";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 90;
            Projectile.ignoreWater = false;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity.Y += 0.03f;
            Lighting.AddLight(Projectile.Center, 0.8f, 0.3f, 1f);

            for (int i = 0; i < 2; i++) {
                Dust flame = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity, 4, 4, DustID.PurpleTorch,
                    -Projectile.velocity.X * 0.4f, -Projectile.velocity.Y * 0.4f,
                    100, default, Main.rand.NextFloat(1.2f, 2f)
                );
                flame.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                Dust smoke = Dust.NewDustDirect(
                    Projectile.Center, 4, 4, DustID.Smoke,
                    -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f,
                    160, new Color(80, 30, 120), 1.2f
                );
                smoke.noGravity = true;
            }
            if (Main.rand.NextBool(3)) {
                Dust soul = Dust.NewDustDirect(
                    Projectile.Center + Main.rand.NextVector2Circular(6, 6), 4, 4, DustID.Wraith,
                    0f, -0.5f, 120, default, 1.0f
                );
                soul.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 300);
            target.AddBuff(BuffID.OnFire3, 300);
            target.AddBuff(BuffID.Ichor, 240);

            // 噬魂爆裂
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.PurpleTorch, vel, 80, default, Main.rand.NextFloat(1.8f, 2.8f));
                burst.noGravity = true;
            }
            for (int i = 0; i < 8; i++) {
                Vector2 smokeVel = Main.rand.NextVector2Circular(3f, 3f);
                Dust smoke = Dust.NewDustPerfect(target.Center, DustID.Smoke, smokeVel, 180, new Color(100, 40, 160), Main.rand.NextFloat(2f, 3.5f));
                smoke.noGravity = true;
            }
            // 吸收灵魂回复生命
            Player owner = Main.player[Projectile.owner];
            if (target.life <= 0) {
                owner.Heal(Main.rand.Next(15, 35));
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.6f, Pitch = 0.5f }, target.Center);
                for (int i = 0; i < 8; i++) {
                    Vector2 soulVel = (owner.Center - target.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(5f, 10f);
                    soulVel = soulVel.RotatedByRandom(0.3f);
                    Dust soul = Dust.NewDustPerfect(target.Center, DustID.Wraith, soulVel, 80, default, 2f);
                    soul.noGravity = true;
                }
                // 升级演出: 噬魂紫染屏 (PaletteLUT, 仅击杀, 仅本机)
                SoulEatPaletteFinisher.Spawn(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner);
            }

            // 命中冲击演出 (噬魂紫径向辉光 + 冲击环)
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.AbyssPurple, scale: 1f, owner: Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 噬魂紫双层带状弹迹
            WeaponVFX.DrawProjectileTrail(Projectile, 9f,
                new Color(110, 30, 180), new Color(225, 130, 255));

            Texture2D lightShot = ACMAsset.LightShot;
            if (lightShot != null) {
                Vector2 origin = lightShot.Size() / 2f;
                Color mainColor = new Color(240, 120, 255) * 0.9f;
                mainColor.A = 0;
                Main.EntitySpriteDraw(lightShot, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, 0.7f, SpriteEffects.None, 0);
                Color glowColor = new Color(180, 60, 255) * 0.5f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(lightShot, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
            for (int i = 0; i < 15; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.PurpleTorch,
                    Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-6f, 6f),
                    80, default, Main.rand.NextFloat(1.5f, 2.5f)
                );
                death.noGravity = true;
            }
            for (int i = 0; i < 8; i++) {
                Dust smoke = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height, DustID.Smoke,
                    0f, -1.5f, 180, new Color(100, 40, 160), Main.rand.NextFloat(1.5f, 2.5f)
                );
                smoke.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 噬魂炮口闪光弹幕 (纯视觉, damage=0): 每次开炮在炮口展开 RadialBloom 大闪 + 冲击环。绘制只在 PreDraw。
    /// </summary>
    public class SoulCannonMuzzleFlash : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 16;

        public static void Spawn(IEntitySource source, Vector2 worldPos, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<SoulCannonMuzzleFlash>(), 0, 0f, owner);
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;
        public override void AI() => Projectile.velocity = Vector2.Zero;

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float life = 1f - Projectile.timeLeft / (float)Life;
            float fade = MathHelper.Clamp(1f - life, 0f, 1f);

            WeaponVFX.DrawShockwaveRing(Projectile.Center, 10f + life * 70f, 9f, fade * 0.8f,
                new Color(235, 150, 255), new Color(120, 40, 190));
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.12f, fade * 0.9f, new Color(210, 110, 255), 0f);
            WeaponVFX.DrawGlowBurst(Projectile.Center, (1.5f + life * 1.5f) * 1.6f, new Color(220, 130, 255) * (fade * 0.7f));
            return false;
        }
    }

    /// <summary>
    /// 噬魂染屏演出弹幕 (纯视觉, damage=0): 击杀瞬间对全屏做短促 PaletteLUT 噬魂紫定调 (强度 ≤0.15, 占单一名额)。
    /// </summary>
    public class SoulEatPaletteFinisher : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_1";
        private const int Life = 24;

        public static void Spawn(IEntitySource source, Vector2 worldPos, int owner) {
            if (Main.dedServ || Main.myPlayer != owner)
                return;
            Projectile.NewProjectile(source, worldPos, Vector2.Zero,
                ModContent.ProjectileType<SoulEatPaletteFinisher>(), 0, 0f, owner);
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        public override bool ShouldUpdatePosition() => false;
        public override void AI() => Projectile.velocity = Vector2.Zero;

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            float life = 1f - Projectile.timeLeft / (float)Life;
            float fade = MathHelper.Clamp(life < 0.25f ? life / 0.25f : 1f - (life - 0.25f) / 0.75f, 0f, 1f);

            // 噬魂紫: 阴影偏深紫, 高光偏亮紫 (ApplyPaletteTint 内部 clamp ≤0.15 + 占名额)
            WeaponVFX.ApplyPaletteTint(Main.spriteBatch,
                new Color(60, 20, 110), new Color(210, 140, 255), fade * 0.15f, saturation: 1.1f);
            return false;
        }
    }
}
