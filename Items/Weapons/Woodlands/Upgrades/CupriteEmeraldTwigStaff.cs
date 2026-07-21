using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

/// <summary>
/// 赤铜翡翠树枝杖 — 完整继承翡翠共鸣印记机制身份, 叠赤铜灼烧风味:
/// 共鸣爆裂变"熔爆" (点燃 AoE) + 命中已点燃目标迸火星 (燃烧链)。
/// </summary>
public class CupriteEmeraldTwigStaff : EmeraldTwigStaff
{
    public override void SetDefaults() {
        base.SetDefaults();
        Item.damage = 36;
        Item.crit = 6;
        Item.useTime = 24;
        Item.useAnimation = 24;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.shoot = ModContent.ProjectileType<CupriteEmeraldBolt>();
        Item.shootSpeed = 11f;
        Item.mana = 7;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<EmeraldTwigStaff>()
            .AddIngredient(ModContent.ItemType<Items.Cuprite.Cuprite>(), 10)
            .AddIngredient<YaoQiFragment>(4)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

/// <summary>
/// 灼翠枝弹 — 继承翡翠共鸣机制 (嵌片/熔爆), 表现为"翠芯橙焰"混合 + 燃烧链。
/// </summary>
public class CupriteEmeraldBolt : EmeraldTwigBolt
{
    protected override int BlastTheme => 1; // 熔爆
    protected override int HitBurstTheme => ACMWeaponBurst.CupriteBurn;

    public override void AI() {
        base.AI();
        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                70, default, 0.9f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.35f, 0.2f, 0.05f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        base.OnHitNPC(target, hit, damageDone); // 印记/熔爆逻辑
        CupriteEmberSpark.TryChain(Projectile, target, DamageClass.Magic); // 先链后燃
        target.AddBuff(BuffID.OnFire, 120);
    }

    public override bool PreDraw(ref Color lightColor) {
        // 翠芯橙焰双层拖尾 (毒+灼)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 8f,
            outerColor: new Color(60, 170, 50, 150), innerColor: new Color(255, 190, 90, 200),
            uvScroll: -Main.GlobalTimeWrappedHourly * 2f);
        // 橙焰核心
        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.45f, new Color(255, 170, 70));
        return false;
    }
}
