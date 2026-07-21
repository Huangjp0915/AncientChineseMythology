using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

/// <summary>
/// 赤铜苔藓爆弹 — 完整继承孢子二段 (弹跳引信 / 直击立爆 / 孢子芽) 机制身份,
/// 叠赤铜灼烧风味: 火孢 (点燃小云) + 爆炸点燃 + 直击已点燃目标迸火星 (燃烧链)。
/// </summary>
public class CupriteMossBomb : MossBomb
{
    protected override int BombType => ModContent.ProjectileType<CupriteMossBombProj>();

    public override void SetDefaults() {
        base.SetDefaults();
        Item.damage = 45;
        Item.crit = 6;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.shoot = ModContent.ProjectileType<CupriteMossBombProj>();
        Item.shootSpeed = 11f;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<MossBomb>()
            .AddIngredient(ModContent.ItemType<Items.Cuprite.Cuprite>(), 12)
            .AddIngredient<YaoQiFragment>(5)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

/// <summary>
/// 灼苔弹 — 继承弹跳引信/孢子芽机制 (SporeTheme=1 → 火孢 + 赤铜蘑菇云), 增强灼烧表现。
/// </summary>
public class CupriteMossBombProj : MossBombProj
{
    protected override int SporeTheme => 1;

    public override void AI() {
        base.AI();
        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                -Projectile.velocity * 0.08f, 80, default, 0.9f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.45f, 0.22f, 0.06f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        base.OnHitNPC(target, hit, damageDone);
        CupriteEmberSpark.TryChain(Projectile, target, DamageClass.Ranged); // 直击已燃目标 → 火星
        target.AddBuff(BuffID.OnFire, 120);
    }

    public override void OnKill(int timeLeft) {
        base.OnKill(timeLeft); // 蘑菇云 AoE + 孢子芽 (赤铜主题由 SporeTheme 传入)

        // 赤铜灼烧叠层: 火星环 + 更重的落地感
        for (int i = 0; i < 16; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 40, default, Main.rand.NextFloat(1.3f, 2.2f));
            d.noGravity = true;
        }
        WeaponVFX.AddScreenShake(Projectile.Center, 5f);
    }

    public override bool PreDraw(ref Color lightColor) {
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 9f,
            outerColor: new Color(180, 60, 20, 130), innerColor: new Color(255, 180, 80, 180),
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);
        return base.PreDraw(ref lightColor); // 弹体贴图 + 引信闪烁
    }
}
