using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands;

/// <summary>
/// 枯木火铳 - 射手火铳类武器
/// 发射橡子弹丸，不消耗弹药，橡子碰撞后碎裂产生小范围伤害
/// </summary>
public class DeadwoodMusket : ModItem
{
    public override void SetDefaults() {
        Item.damage = 14;
        Item.crit = 4;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 44;
        Item.height = 20;
        Item.useTime = 32;
        Item.useAnimation = 32;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 4f;
        Item.value = Item.buyPrice(silver: 45);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = SoundID.Item11;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<DeadwoodAcornProj>();
        Item.shootSpeed = 9f;
    }

    public override Vector2? HoldoutOffset() {
        return new Vector2(-6, 2);
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
        Vector2 muzzlePos = position + muzzleDir * 30f;

        // 稍微偏转
        Vector2 perturbedVel = velocity.RotatedByRandom(MathHelper.ToRadians(3));
        Projectile.NewProjectile(source, muzzlePos, perturbedVel, type, damage, knockback, player.whoAmI);

        // 枪口烟雾
        for (int i = 0; i < 3; i++) {
            Vector2 dustVel = -muzzleDir.RotatedByRandom(0.4f) * Main.rand.NextFloat(1f, 3f);
            Dust d = Dust.NewDustPerfect(muzzlePos, DustID.Smoke, dustVel, 100, default, 1f);
            d.noGravity = true;
        }

        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 18)
            .AddIngredient(ItemID.Acorn, 5)
            .AddIngredient(ItemID.FallenStar, 1)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

/// <summary>
/// 橡子弹丸 - 枯木火铳发射的橡子，碰撞后碎裂
/// </summary>
public class DeadwoodAcornProj : ModProjectile
{
    public override string Texture
        => $"Terraria/Images/Item_{ItemID.Acorn}";

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 120;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        Projectile.rotation += Projectile.velocity.X * 0.05f;
        Projectile.velocity.Y += 0.08f; // 轻微重力

        if (Main.rand.NextBool(5)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WoodFurniture,
                -Projectile.velocity * 0.1f, 60, default, 0.8f);
            d.noGravity = true;
        }
    }

    public override void OnKill(int timeLeft) {
        // 碎裂效果
        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = 0.5f }, Projectile.Center);
        for (int i = 0; i < 8; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WoodFurniture,
                vel, 40, default, 1.2f);
            d.noGravity = false;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        for (int i = 0; i < 5; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.WoodFurniture,
                Main.rand.NextVector2Circular(3f, 3f), 60, default, 1f);
            d.noGravity = false;
        }
    }
}
