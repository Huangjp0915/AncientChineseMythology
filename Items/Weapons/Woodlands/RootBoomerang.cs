using System;
using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands;

/// <summary>
/// 树根回力镖 - 战士回力镖类武器
/// 投掷出一根弯曲的树根，飞出后自动返回，穿透2个敌人
/// </summary>
public class RootBoomerang : ModItem
{
    public override void SetDefaults() {
        Item.damage = 13;
        Item.crit = 4;
        Item.DamageType = DamageClass.Melee;
        Item.width = 30;
        Item.height = 30;
        Item.useTime = 22;
        Item.useAnimation = 22;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 5f;
        Item.value = Item.buyPrice(silver: 40);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<RootBoomerangProj>();
        Item.shootSpeed = 10f;
    }

    public override bool CanUseItem(Player player) {
        return player.ownedProjectileCounts[ModContent.ProjectileType<RootBoomerangProj>()] < 1;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 15)
            .AddIngredient(ItemID.Vine, 2)
            .AddIngredient(ItemID.Stinger, 2)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

/// <summary>
/// 树根回力镖弹幕 - 飞出后减速返回玩家，旋转飞行
/// </summary>
public class RootBoomerangProj : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/Woodlands/RootBoomerang";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
        ProjectileID.Sets.TrailingMode[Type] = 0;
    }

    public override void SetDefaults() {
        Projectile.width = 22;
        Projectile.height = 22;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 600;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        // 旋转
        Projectile.rotation += 0.35f * Math.Sign(Projectile.velocity.X);

        // 飞出30帧后开始返回
        Projectile.ai[0]++;
        if (Projectile.ai[0] >= 30f) {
            Projectile.tileCollide = false;
            Player owner = Main.player[Projectile.owner];
            Vector2 returnDir = owner.Center - Projectile.Center;
            float dist = returnDir.Length();

            if (dist < 20f) {
                Projectile.Kill();
                return;
            }

            returnDir = returnDir.SafeNormalize(Vector2.Zero);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, returnDir * 14f, 0.08f);
        }
        else {
            // 逐渐减速
            Projectile.velocity *= 0.98f;
        }

        // 叶片粒子
        if (Main.rand.NextBool(4)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Grass,
                Main.rand.NextVector2Circular(1f, 1f), 80, default, 1f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.08f, 0.2f, 0.06f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        for (int i = 0; i < 4; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Grass,
                Main.rand.NextVector2Circular(3f, 3f), 60, default, 1.2f);
            d.noGravity = true;
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.Nature, scale: 0.8f, owner: Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        // 旋飞木纹弧光双层拖尾; 返回阶段提升内层亮度
        bool returning = Projectile.ai[0] >= 30f;
        int innerAlpha = returning ? 220 : 150;
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 9f,
            outerColor: new Color(90, 70, 40, 150), innerColor: new Color(170, 220, 120, innerAlpha),
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.2f);
        return true; // 保留镖体贴图
    }
}
