using AncientChineseMythology.Global;
using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.XuanTie;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

/// <summary>
/// 玄铁树根回力镖 — 投掷"玄铁血镖" <see cref="XuanTieBoomerangProj"/>。
/// 可见质变: 暗钢-暗红双层 ribbon 拖尾, 命中施加 <see cref="Buffs.XuanTieBleed"/> 流血 + 血色命中演出。
/// </summary>
public class XuanTieRootBoomerang : ModItem
{
    public override void SetDefaults() {
        Item.damage = 48;
        Item.crit = 8;
        Item.DamageType = DamageClass.Melee;
        Item.width = 30;
        Item.height = 30;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 6f;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<XuanTieBoomerangProj>();
        Item.shootSpeed = 12f;
    }

    public override bool CanUseItem(Player player) =>
        player.ownedProjectileCounts[ModContent.ProjectileType<XuanTieBoomerangProj>()] < 1;

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<RootBoomerang>()
            .AddIngredient<XuanTieBar>(15)
            .AddIngredient<YaoQiFragment>(3)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

/// <summary>
/// 玄铁血镖 — 继承 <see cref="RootBoomerangProj"/> 飞出返回机制, 增强为玄铁流血表现。
/// </summary>
public class XuanTieBoomerangProj : RootBoomerangProj
{
    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
        ProjectileID.Sets.TrailingMode[Type] = 0;
    }

    public override void AI() {
        base.AI();
        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                Main.rand.NextVector2Circular(1f, 1f), 110, default, 1f);
        }
        Lighting.AddLight(Projectile.Center, 0.22f, 0.04f, 0.05f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        if (!target.friendly && !target.dontTakeDamage) {
            target.AddBuff(ModContent.BuffType<Buffs.XuanTieBleed>(), 60 * 3);
            var bleed = target.GetGlobalNPC<XuanTieBleedGlobalNPC>();
            if (bleed.bleedStacks < 10)
                bleed.bleedStacks++;
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.XuanTieBleed, scale: 1f, owner: Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 9f,
            outerColor: new Color(90, 95, 110, 150), innerColor: new Color(190, 40, 40, 190),
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.2f);
        return true; // 原版镖体
    }
}
