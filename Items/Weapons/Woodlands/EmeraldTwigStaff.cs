using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands;

/// <summary>
/// 翡翠树枝杖 - 法师法杖类武器
/// 释放一段飞旋的小树枝攻击，穿透1个敌人
/// 命中敌人后碎裂为叶片
/// </summary>
public class EmeraldTwigStaff : ModItem
{
    public override void SetDefaults() {
        Item.damage = 16;
        Item.crit = 4;
        Item.DamageType = DamageClass.Magic;
        Item.width = 36;
        Item.height = 36;
        Item.useTime = 28;
        Item.useAnimation = 28;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 3f;
        Item.value = Item.buyPrice(silver: 50);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<EmeraldTwigBolt>();
        Item.shootSpeed = 10f;
        Item.mana = 6;
        Item.staff[Type] = true;
    }

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
        velocity = velocity.RotatedByRandom(MathHelper.ToRadians(5));
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 10)
            .AddIngredient(ItemID.Emerald, 3)
            .AddIngredient(ItemID.JungleSpores, 3)
            .AddIngredient(ItemID.FallenStar, 2)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

/// <summary>
/// 翡翠树枝弹 - 翠绿能量弹幕
/// 使用 LightShot + SoftGlow 叠加渲染，拖尾发光效果
/// </summary>
public class EmeraldTwigBolt : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
        ProjectileID.Sets.TrailingMode[Type] = 0;
    }

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 90;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
        Projectile.extraUpdates = 1;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.1f, 0.25f, 0.1f);

        // 翠绿粒子尾迹
        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                60, default, 0.8f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 翡翠双层拖尾 (外暗深翠 + 内亮嫩绿)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 7f,
            outerColor: new Color(40, 150, 60, 150), innerColor: new Color(190, 255, 150, 200),
            uvScroll: -Main.GlobalTimeWrappedHourly * 2f);
        // 翠绿能量核心
        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.4f, new Color(120, 230, 90));
        return false;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        for (int i = 0; i < 6; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.GreenTorch,
                Main.rand.NextVector2Circular(3f, 3f), 60, default, 1.1f);
            d.noGravity = true;
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.Nature, scale: 0.8f, owner: Projectile.owner);
    }

    public override void OnKill(int timeLeft) {
        for (int i = 0; i < 8; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                Main.rand.NextVector2CircularEdge(3f, 3f), 40, default, 1f);
            d.noGravity = true;
        }
    }
}
