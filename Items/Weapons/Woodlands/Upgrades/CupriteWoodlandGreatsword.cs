using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

/// <summary>
/// 赤铜林地巨剑 — 完整继承三连段 / 生机 / 万木生机制身份, 叠赤铜灼烧风味:
/// 荆根变焦铜火根 (点燃), 万木生变"燎原" (橙焰年轮), 命中已点燃目标迸出火星 (燃烧链)。
/// </summary>
public class CupriteWoodlandGreatsword : WoodlandGreatsword
{
    protected override int SwingProjType => ModContent.ProjectileType<CupriteWoodlandSwing>();
    protected override Color VigorGlowColor => new(255, 190, 90);

    public override void SetDefaults() {
        base.SetDefaults();
        Item.damage = 42;
        Item.crit = 6;
        Item.useTime = 28;
        Item.useAnimation = 28;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.scale = 1.15f;
        Item.shoot = ModContent.ProjectileType<CupriteWoodlandSwing>();
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<WoodlandGreatsword>()
            .AddIngredient(ModContent.ItemType<Items.Cuprite.Cuprite>(), 12)
            .AddIngredient<YaoQiFragment>(5)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

/// <summary>
/// 赤铜巨剑挥砍 — 继承三段波形, 换赤铜主题: 橙焰拖尾 / 火根 / 燎原年轮 /
/// 命中点燃 + 燃烧链火星。
/// </summary>
public class CupriteWoodlandSwing : WoodlandSwing
{
    public override string Texture => "AncientChineseMythology/Items/Weapons/Woodlands/Upgrades/CupriteWoodlandGreatsword";

    protected override Color TrailOuter => new(190, 55, 15, 160);
    protected override Color TrailInner => new(255, 205, 110, 210);
    protected override int BurstTheme => ACMWeaponBurst.CupriteBurn;
    protected override int GroundTheme => 1; // 火根 + 燎原年轮
    protected override int SwingDustType => DustID.Torch;

    public override void SetStaticDefaults() {
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.CupriteWoodlandSwing.DisplayName", () => "赤铜林地巨剑");
    }

    protected override void OnThemeHit(NPC target) {
        // 燃烧链判定须在点燃之前 (只对"已在燃烧"的目标迸火星)
        CupriteEmberSpark.TryChain(Projectile, target, DamageClass.Melee);
        if (Main.rand.NextBool(3))
            target.AddBuff(BuffID.Poisoned, 150);
        target.AddBuff(BuffID.OnFire, 120);
    }
}

/// <summary>
/// 赤铜火星 — 燃烧链载体: 命中"已点燃"目标时迸出的小火星 (35% 伤害, 弧线, 命中点燃)。
/// 火星本身不再触发连锁。全部赤铜升级件共用。
/// </summary>
public class CupriteEmberSpark : ModProjectile
{
    public override string Texture => "InnoVault/Assets/placeholder";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[Type] = 8;
        ProjectileID.Sets.TrailingMode[Type] = 0;
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.CupriteEmberSpark.DisplayName", () => "赤铜火星");
    }

    /// <summary>
    /// 燃烧链入口: 若目标已点燃则迸出 2 颗火星 (owner 端生成)。
    /// 在给目标 AddBuff(OnFire) **之前**调用。
    /// </summary>
    public static void TryChain(Projectile source, NPC target, DamageClass damageClass) {
        if (source.owner != Main.myPlayer)
            return;
        if (!target.HasBuff(BuffID.OnFire) && !target.HasBuff(BuffID.OnFire3))
            return;

        int sparkDamage = Math.Max((int)(source.damage * 0.35f), 1);
        for (int i = 0; i < 2; i++) {
            Vector2 vel = new(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(4.5f, 7f));
            int idx = Projectile.NewProjectile(source.GetSource_OnHit(target), target.Top, vel,
                ModContent.ProjectileType<CupriteEmberSpark>(), sparkDamage, 1f, source.owner);
            if (idx >= 0 && idx < Main.maxProjectiles)
                Main.projectile[idx].DamageType = damageClass; // 跟随来源武器职业
        }
        SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.4f, Pitch = 0.4f + Main.rand.NextFloat(0.1f) }, target.Center);
    }

    public override void SetDefaults() {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Generic;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 55;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        Projectile.velocity.Y += 0.18f;
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                -Projectile.velocity * 0.15f, 80, default, Main.rand.NextFloat(0.8f, 1.2f));
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.35f, 0.18f, 0.04f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.OnFire, 90);
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.CupriteBurn, 0.6f, Projectile.owner);
    }

    public override void OnKill(int timeLeft) {
        for (int i = 0; i < 5; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                Main.rand.NextVector2Circular(2f, 2f), 60, default, 1f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 5f,
            outerColor: new Color(190, 55, 15, 140), innerColor: new Color(255, 205, 110, 190),
            uvScroll: -Main.GlobalTimeWrappedHourly * 2f);
        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.3f, new Color(255, 170, 70));
        return false;
    }
}
