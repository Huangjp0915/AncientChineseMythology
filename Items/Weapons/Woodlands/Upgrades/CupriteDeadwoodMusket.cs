using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

/// <summary>
/// 赤铜枯木火铳 — 完整继承三发弹仓 / 霰爆机制身份, 叠赤铜灼烧风味:
/// 灼烧橡子 (DissolveBurn 余烬质感) + 霰爆变燃爆 + 命中已点燃目标迸火星 (燃烧链)。
/// </summary>
public class CupriteDeadwoodMusket : DeadwoodMusket
{
    protected override int AcornType => ModContent.ProjectileType<CupriteAcornProj>();
    protected override int PelletType => ModContent.ProjectileType<CupritePellet>();
    protected override int MuzzleDustType => DustID.Torch;

    public override void SetDefaults() {
        base.SetDefaults();
        Item.damage = 40;
        Item.crit = 6;
        Item.useTime = 26;
        Item.useAnimation = 26;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.shoot = ModContent.ProjectileType<CupriteAcornProj>();
        Item.shootSpeed = 10f;
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
/// 灼烧橡子 — 继承橡子机制 (重力/碎裂弹片), 增强为赤铜灼烧表现 + 燃烧链。
/// </summary>
public class CupriteAcornProj : DeadwoodAcornProj
{
    protected override int HitBurstTheme => ACMWeaponBurst.CupriteBurn;

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
        CupriteEmberSpark.TryChain(Projectile, target, DamageClass.Ranged); // 先链后燃
        target.AddBuff(BuffID.OnFire, 120);
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

/// <summary>
/// 燃爆霰弹 — 燃爆装填喷出的灼烧小橡子 (不二次碎裂), 命中点燃。
/// </summary>
public class CupritePellet : DeadwoodPellet
{
    protected override int HitBurstTheme => ACMWeaponBurst.CupriteBurn;

    public override void SetStaticDefaults() {
        base.SetStaticDefaults();
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.CupritePellet.DisplayName", () => "燃爆霰弹");
    }

    public override void AI() {
        base.AI();
        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                -Projectile.velocity * 0.1f, 90, default, 0.9f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.3f, 0.15f, 0.03f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        base.OnHitNPC(target, hit, damageDone);
        target.AddBuff(BuffID.OnFire, 90);
    }
}
