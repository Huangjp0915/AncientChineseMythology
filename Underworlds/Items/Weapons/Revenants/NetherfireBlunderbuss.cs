using AncientChineseMythology.Underworlds.Items;
using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Items.Weapons.Revenants
{
    /// <summary>
    /// 冥府幽火铳 - 发射冥府幽火的火器，远程火铳类武器
    /// 肉后中期，发射散射幽火弹丸，命中产生冥烟爆裂
    /// </summary>
    public class NetherfireBlunderbuss : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 42;
            Item.crit = 6;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 54;
            Item.height = 22;
            Item.useTime = 32;
            Item.useAnimation = 32;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.buyPrice(gold: 8);
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item36;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<NetherfireBullet>();
            Item.shootSpeed = 10f;
            Item.useAmmo = AmmoID.Bullet;
            Item.staff[Type] = true;
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-8, 2);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int netherfireBullet = ModContent.ProjectileType<NetherfireBullet>();

            //散射3-5发幽火弹丸
            int count = Main.rand.Next(3, 6);
            for (int i = 0; i < count; i++) {
                Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(12));
                perturbedSpeed *= Main.rand.NextFloat(0.8f, 1.2f);
                Projectile.NewProjectile(source, position, perturbedSpeed, netherfireBullet, damage, knockback, player.whoAmI);
            }

            //枪口冥烟特效
            Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 muzzlePos = position + muzzleDir * 30f;
            for (int i = 0; i < 15; i++) {
                Vector2 smokeVel = muzzleDir.RotatedByRandom(MathHelper.ToRadians(35)) * Main.rand.NextFloat(2f, 6f);
                Dust smoke = Dust.NewDustPerfect(
                    muzzlePos, DustID.Smoke,
                    smokeVel, 180,
                    new Color(80, 40, 120), Main.rand.NextFloat(1.2f, 2.0f)
                );
                smoke.noGravity = true;
            }

            //枪口幽火闪光
            for (int i = 0; i < 8; i++) {
                Vector2 sparkVel = muzzleDir.RotatedByRandom(MathHelper.ToRadians(25)) * Main.rand.NextFloat(3f, 8f);
                Dust spark = Dust.NewDustPerfect(
                    muzzlePos, DustID.PurpleTorch,
                    sparkVel, 100, default, Main.rand.NextFloat(1.5f, 2.2f)
                );
                spark.noGravity = true;
            }

            return false;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            //枪口位置调整
            position += velocity.SafeNormalize(Vector2.Zero) * 20f;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<SoulFragment>(8)
                .AddIngredient<UmbralStoneItem>(28)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    /// <summary>
    /// 幽火弹丸弹幕 - 带有冥火拖尾的散射弹丸，命中时产生冥烟爆裂
    /// 使用ACMAsset.LightShot叠加光弹效果，ACMAsset.EmberShards命中碎片
    /// </summary>
    public class NetherfireBullet : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Underworlds/Items/Weapons/Revenants/NetherfireBlunderbuss";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60;
            Projectile.ignoreWater = false;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            //轻微重力
            Projectile.velocity.Y += 0.05f;

            //冥紫色光照
            Lighting.AddLight(Projectile.Center, 0.4f, 0.15f, 0.5f);

            //幽火拖尾
            if (Main.rand.NextBool(2)) {
                Dust flame = Dust.NewDustDirect(
                    Projectile.Center - Projectile.velocity,
                    4, 4, DustID.PurpleTorch,
                    -Projectile.velocity.X * 0.3f, -Projectile.velocity.Y * 0.3f,
                    120, default, Main.rand.NextFloat(0.8f, 1.3f)
                );
                flame.noGravity = true;
            }

            //暗烟拖尾
            if (Main.rand.NextBool(3)) {
                Dust smoke = Dust.NewDustDirect(
                    Projectile.Center, 4, 4, DustID.Smoke,
                    -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f,
                    180, new Color(60, 30, 90), 0.8f
                );
                smoke.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //冥火灼烧
            target.AddBuff(BuffID.ShadowFlame, 120);
            target.AddBuff(BuffID.OnFire3, 90);

            //命中冥烟爆裂
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust burst = Dust.NewDustPerfect(
                    target.Center, DustID.PurpleTorch, vel,
                    100, default, Main.rand.NextFloat(1.2f, 1.8f)
                );
                burst.noGravity = true;
            }

            //冥烟扩散
            for (int i = 0; i < 4; i++) {
                Vector2 smokeVel = Main.rand.NextVector2Circular(2f, 2f);
                Dust smoke = Dust.NewDustPerfect(
                    target.Center, DustID.Smoke, smokeVel,
                    200, new Color(80, 40, 120), Main.rand.NextFloat(1.5f, 2.5f)
                );
                smoke.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //使用LightShot灰度图绘制幽火光弹
            Texture2D lightShot = ACMAsset.LightShot;
            if (lightShot != null) {
                Vector2 origin = lightShot.Size() / 2f;

                //拖尾光弹
                for (int i = 0; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    float progress = 1f - (float)i / Projectile.oldPos.Length;
                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Color trailColor = Color.Lerp(new Color(60, 20, 100), new Color(180, 80, 220), progress) * progress * 0.5f;
                    trailColor.A = 0;
                    Main.EntitySpriteDraw(lightShot, drawPos, null, trailColor, Projectile.oldRot[i], origin, 0.4f * progress, SpriteEffects.None, 0);
                }

                //主体光弹
                Color mainColor = new Color(200, 100, 255) * 0.8f;
                mainColor.A = 0;
                Main.EntitySpriteDraw(lightShot, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, origin, 0.5f, SpriteEffects.None, 0);

                //外层光晕
                Color glowColor = new Color(140, 50, 200) * 0.4f;
                glowColor.A = 0;
                Main.EntitySpriteDraw(lightShot, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, origin, 0.7f, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);

            //消亡冥火碎片
            for (int i = 0; i < 8; i++) {
                Dust death = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.PurpleTorch,
                    Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f),
                    100, default, Main.rand.NextFloat(1.0f, 1.5f)
                );
                death.noGravity = true;
            }

            //冥烟
            for (int i = 0; i < 4; i++) {
                Dust smoke = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.Smoke, 0f, -1f,
                    200, new Color(80, 40, 120), Main.rand.NextFloat(1.0f, 1.8f)
                );
                smoke.noGravity = true;
            }
        }
    }
}
