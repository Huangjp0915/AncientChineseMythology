using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

/// <summary>
/// 赤铜翡翠树枝杖 — 发射"灼翠枝弹" <see cref="CupriteEmeraldBolt"/>。
/// 可见质变: 弹幕由翠绿改为"翠芯橙焰"双层拖尾 (毒+灼混合), 命中点燃 + 赤铜灼烧演出。
/// </summary>
public class CupriteEmeraldTwigStaff : ModItem
{
    public override void SetDefaults() {
        Item.damage = 36;
        Item.crit = 6;
        Item.DamageType = DamageClass.Magic;
        Item.width = 36;
        Item.height = 36;
        Item.useTime = 24;
        Item.useAnimation = 24;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 3f;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<CupriteEmeraldBolt>();
        Item.shootSpeed = 11f;
        Item.mana = 7;
        Item.staff[Type] = true;
    }

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
        velocity = velocity.RotatedByRandom(MathHelper.ToRadians(5));
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
/// 灼翠枝弹 — 继承 <see cref="EmeraldTwigBolt"/> 机制, 重写表现为赤铜灼烧主题。
/// </summary>
public class CupriteEmeraldBolt : EmeraldTwigBolt
{
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
        base.OnHitNPC(target, hit, damageDone);
        target.AddBuff(BuffID.OnFire, 120);
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.CupriteBurn, scale: 0.8f, owner: Projectile.owner);
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
