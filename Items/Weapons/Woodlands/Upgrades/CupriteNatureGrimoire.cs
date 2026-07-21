using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

/// <summary>
/// 赤铜自然秘典 — 完整继承页读韵律 / 荣枯页机制身份, 叠赤铜灼烧风味:
/// 燃叶 (毒+灼双 DoT), 第 5 页变"燎原页" (焰叶环 + 橙焰年轮 + 余烬领域持续点燃),
/// 命中已点燃目标迸火星 (燃烧链)。
/// </summary>
public class CupriteNatureGrimoire : NatureGrimoire
{
    protected override int LeafType => ModContent.ProjectileType<CupriteNatureLeaf>();
    protected override int BloomTheme => 1; // 焰叶环 + 余烬领域
    protected override int PulseTheme => 1; // 橙焰年轮
    protected override int PageDustType => DustID.Torch;

    public override void SetDefaults() {
        base.SetDefaults();
        Item.damage = 35;
        Item.crit = 8;
        Item.useTime = 26;
        Item.useAnimation = 26;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.shoot = ModContent.ProjectileType<CupriteNatureLeaf>();
        Item.shootSpeed = 9f;
        Item.mana = 9;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<NatureGrimoire>()
            .AddIngredient(ModContent.ItemType<Items.Cuprite.Cuprite>(), 10)
            .AddIngredient<YaoQiFragment>(4)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

/// <summary>
/// 燃叶 — 继承飘动/微追踪机制, 增强为赤铜灼烧表现: 毒+灼双 DoT + 燃烧链。
/// </summary>
public class CupriteNatureLeaf : NatureGrimoireLeaf
{
    protected override int HitBurstTheme => ACMWeaponBurst.CupriteBurn;

    public override void SetStaticDefaults() {
        // 继承类不共享静态设置, 必须重设帧数 (修复原帧动画越界隐患)
        Main.projFrames[Type] = 5;
    }

    public override void AI() {
        base.AI();
        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                Main.rand.NextVector2Circular(0.6f, 0.6f), 80, default, 0.8f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.3f, 0.18f, 0.05f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        base.OnHitNPC(target, hit, damageDone); // 原版毒
        CupriteEmberSpark.TryChain(Projectile, target, DamageClass.Magic); // 先链后燃
        target.AddBuff(BuffID.OnFire, 120);
    }

    public override bool PreDraw(ref Color lightColor) {
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 7f,
            outerColor: new Color(180, 60, 20, 130), innerColor: new Color(255, 180, 80, 180),
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);
        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.35f, new Color(255, 150, 60));
        return true; // 保留原版动画叶片本体
    }
}
