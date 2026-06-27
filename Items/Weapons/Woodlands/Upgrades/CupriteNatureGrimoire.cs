using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

/// <summary>
/// 赤铜自然秘典 — 扇形发射"燃叶" <see cref="CupriteNatureLeaf"/>。
/// 可见质变: 飘叶燃烧 (橙焰拖尾 + 余烬), 命中"毒+灼"双 DoT + 赤铜灼烧演出。
/// </summary>
public class CupriteNatureGrimoire : ModItem
{
    public override void SetDefaults() {
        Item.damage = 35;
        Item.crit = 8;
        Item.DamageType = DamageClass.Magic;
        Item.width = 28;
        Item.height = 32;
        Item.useTime = 26;
        Item.useAnimation = 26;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 3f;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<CupriteNatureLeaf>();
        Item.shootSpeed = 9f;
        Item.mana = 9;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        float spreadAngle = MathHelper.ToRadians(20);
        for (int i = -1; i <= 1; i++) {
            Vector2 leafVel = velocity.RotatedBy(spreadAngle * i) * Main.rand.NextFloat(0.9f, 1.1f);
            Projectile.NewProjectile(source, position, leafVel, type, damage, knockback, player.whoAmI,
                ai0: Main.rand.NextFloat(MathHelper.TwoPi));
        }
        return false;
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
/// 燃叶 — 继承 <see cref="NatureGrimoireLeaf"/> 飘动/动画机制, 增强为赤铜灼烧表现。
/// </summary>
public class CupriteNatureLeaf : NatureGrimoireLeaf
{
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
        target.AddBuff(BuffID.OnFire, 120);
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.CupriteBurn, scale: 0.75f, owner: Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 7f,
            outerColor: new Color(180, 60, 20, 130), innerColor: new Color(255, 180, 80, 180),
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);
        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.35f, new Color(255, 150, 60));
        return true; // 保留原版动画叶片本体
    }
}
