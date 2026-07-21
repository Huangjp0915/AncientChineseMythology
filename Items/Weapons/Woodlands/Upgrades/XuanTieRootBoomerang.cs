using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.XuanTie;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

/// <summary>
/// 玄铁树根回力镖 — 完整继承接镖研磨机制身份, 叠玄铁放血风味:
/// 命中 +1 流血层; 单轮命中 ≥3 触发"血怒返航" (返速 +50%, 返程伤害 ×1.25 + 溅血)。
/// </summary>
public class XuanTieRootBoomerang : RootBoomerang
{
    protected override int BoomerangType => ModContent.ProjectileType<XuanTieBoomerangProj>();
    protected override int GrindDustType => DustID.Blood;

    public override void SetDefaults() {
        base.SetDefaults();
        Item.damage = 48;
        Item.crit = 8;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.knockBack = 6f;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.shoot = ModContent.ProjectileType<XuanTieBoomerangProj>();
        Item.shootSpeed = 12f;
    }

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
/// 玄铁血镖 — 继承飞出/返回/接镖机制; 命中叠流血, 命中 ≥3 血怒返航。
/// </summary>
public class XuanTieBoomerangProj : RootBoomerangProj
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/Woodlands/Upgrades/XuanTieRootBoomerang";

    protected override int RageHitThreshold => 3;
    protected override float RageDamageMul => 1.25f;
    protected override float RageSpeedMul => 1.5f;
    protected override Color TrailOuter => new(90, 95, 110, 150);
    protected override Color TrailInner => new(190, 40, 40, 150);
    protected override int BurstTheme => ACMWeaponBurst.XuanTieBleed;

    protected override int ThemeDustType() => DustID.Blood;

    protected override void OnThemeHit(NPC target) {
        XuanTieHunterBow.AddBleed(target, 1);

        // 血怒触发瞬间: 低吼音 + 溅血宣告
        if (FlightHits == RageHitThreshold) {
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.7f, Pitch = -0.35f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    Main.rand.NextVector2CircularEdge(4f, 4f), 60, default, 1.3f);
                d.noGravity = Main.rand.NextBool();
            }
        }
    }
}
