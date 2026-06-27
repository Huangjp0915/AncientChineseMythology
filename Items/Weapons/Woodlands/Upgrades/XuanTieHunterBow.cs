using AncientChineseMythology.Global;
using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.XuanTie;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

/// <summary>
/// 玄铁猎弓 — 把弹药转化为"玄铁血矢" <see cref="XuanTieArrow"/> 射出 (仍消耗箭矢)。
/// 可见质变: 暗钢-暗红双层 ribbon 拖尾, 命中施加 <see cref="Buffs.XuanTieBleed"/> 流血 (与玄铁套装叠层呼应) + 血色命中演出。
/// </summary>
public class XuanTieHunterBow : ModItem
{
    public override void SetDefaults() {
        Item.damage = 38;
        Item.crit = 8;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 18;
        Item.height = 52;
        Item.useTime = 24;
        Item.useAnimation = 24;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 3f;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.UseSound = SoundID.Item5;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.shoot = ProjectileID.WoodenArrowFriendly;
        Item.shootSpeed = 9f;
        Item.useAmmo = AmmoID.Arrow;
    }

    public override Vector2? HoldoutOffset() => new Vector2(-2, 0);

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Projectile.NewProjectile(source, position, velocity,
            ModContent.ProjectileType<XuanTieArrow>(), damage, knockback, player.whoAmI);

        // 暗钢枪口火星
        Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);
        for (int i = 0; i < 3; i++) {
            Vector2 dustVel = dir.RotatedByRandom(0.35f) * Main.rand.NextFloat(1f, 3f);
            Dust d = Dust.NewDustPerfect(position + dir * 20f, DustID.Blood, dustVel, 120, default, 0.9f);
            d.noGravity = true;
        }
        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<VineHunterBow>()
            .AddIngredient<XuanTieBar>(15)
            .AddIngredient<YaoQiFragment>(3)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

/// <summary>
/// 玄铁血矢 — 复用原版木箭 AI (重力/插地), 增强为玄铁流血表现。
/// </summary>
public class XuanTieArrow : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.WoodenArrowFriendly}";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
        ProjectileID.Sets.TrailingMode[Type] = 0;
    }

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 600;
        Projectile.tileCollide = true;
        Projectile.arrow = true;
        Projectile.aiStyle = ProjAIStyleID.Arrow;
        AIType = ProjectileID.WoodenArrowFriendly;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        if (!target.friendly && !target.dontTakeDamage) {
            target.AddBuff(ModContent.BuffType<Buffs.XuanTieBleed>(), 60 * 3);
            var bleed = target.GetGlobalNPC<XuanTieBleedGlobalNPC>();
            if (bleed.bleedStacks < 10)
                bleed.bleedStacks++;
        }
        for (int i = 0; i < 6; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                Main.rand.NextVector2Circular(3f, 3f), 100, default, 1.1f);
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.XuanTieBleed, scale: 0.85f, owner: Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        Lighting.AddLight(Projectile.Center, 0.25f, 0.04f, 0.05f);
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 8f,
            outerColor: new Color(90, 95, 110, 150), innerColor: new Color(190, 40, 40, 190),
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.2f);
        return true; // 暗钢箭体
    }
}
