using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

/// <summary>
/// 赤铜枯木火铳 — 发射"灼烧橡子" <see cref="CupriteAcornProj"/>。
/// 可见质变: 橡子本体走 <c>DissolveBurn.fx</c> 灼烧边 (烧焦余烬质感) + 赤铜双层 ember 拖尾, 命中点燃。
/// </summary>
public class CupriteDeadwoodMusket : ModItem
{
    public override void SetDefaults() {
        Item.damage = 40;
        Item.crit = 6;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 44;
        Item.height = 20;
        Item.useTime = 28;
        Item.useAnimation = 28;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 4f;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.UseSound = SoundID.Item11;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<CupriteAcornProj>();
        Item.shootSpeed = 10f;
    }

    public override Vector2? HoldoutOffset() => new Vector2(-6, 2);

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
        Vector2 muzzlePos = position + muzzleDir * 30f;
        Vector2 perturbedVel = velocity.RotatedByRandom(MathHelper.ToRadians(3));
        Projectile.NewProjectile(source, muzzlePos, perturbedVel, type, damage, knockback, player.whoAmI);

        // 枪口火星烟雾 (赤铜灼烧色)
        for (int i = 0; i < 5; i++) {
            Vector2 dustVel = muzzleDir.RotatedByRandom(0.5f) * Main.rand.NextFloat(1f, 4f);
            int dustType = Main.rand.NextBool(3) ? DustID.Smoke : DustID.Torch;
            Dust d = Dust.NewDustPerfect(muzzlePos, dustType, dustVel, 80, default, 1.1f);
            d.noGravity = true;
        }
        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<DeadwoodMusket>()
            .AddIngredient(ModContent.ItemType<Items.Cuprite.Cuprite>(), 12)
            .AddIngredient<YaoQiFragment>(5)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

/// <summary>
/// 灼烧橡子 — 继承 <see cref="DeadwoodAcornProj"/> 全部机制 (重力/碎裂), 仅增强表现层。
/// </summary>
public class CupriteAcornProj : DeadwoodAcornProj
{
    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
        ProjectileID.Sets.TrailingMode[Type] = 0;
    }

    public override void AI() {
        base.AI();
        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                -Projectile.velocity * 0.15f, 80, default, Main.rand.NextFloat(0.8f, 1.3f));
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.5f, 0.25f, 0.05f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        base.OnHitNPC(target, hit, damageDone);
        target.AddBuff(BuffID.OnFire, 120);
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.CupriteBurn, scale: 0.9f, owner: Projectile.owner);
    }

    public override void OnKill(int timeLeft) {
        base.OnKill(timeLeft);
        for (int i = 0; i < 6; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                Main.rand.NextVector2Circular(2.5f, 2.5f), 60, default, 1.1f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 赤铜双层 ember 拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 10f,
            outerColor: new Color(190, 55, 15, 160), innerColor: new Color(255, 205, 110, 200),
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);

        // 橡子本体走 DissolveBurn 灼烧边 (余烬质感)
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        WeaponVFX.ApplyDissolveBurn(tex, Projectile.Center, null, lightColor, Projectile.rotation,
            tex.Size() * 0.5f, Projectile.scale, threshold: 0.12f, intensity: 1f,
            edgeColor: new Color(255, 130, 40), edgeWidth: 0.12f, noiseScale: 3f);
        return false;
    }
}
