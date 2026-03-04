using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands;

/// <summary>
/// 藤蔓猎弓 - 射手弓类武器
/// 前期弓，使用箭矢弹药，命中敌人有概率施加中毒
/// 发射时附带少量藤蔓粒子
/// </summary>
public class VineHunterBow : ModItem
{
    public override void SetDefaults() {
        Item.damage = 11;
        Item.crit = 6;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 18;
        Item.height = 52;
        Item.useTime = 26;
        Item.useAnimation = 26;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 2f;
        Item.value = Item.buyPrice(silver: 30);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = SoundID.Item5;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.shoot = ProjectileID.WoodenArrowFriendly;
        Item.shootSpeed = 8f;
        Item.useAmmo = AmmoID.Arrow;
    }

    public override Vector2? HoldoutOffset() {
        return new Vector2(-2, 0);
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        // 发射时产生少量藤蔓粒子
        Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
        for (int i = 0; i < 3; i++) {
            Vector2 dustVel = muzzleDir.RotatedByRandom(0.4f) * Main.rand.NextFloat(1f, 3f);
            Dust d = Dust.NewDustPerfect(position + muzzleDir * 20f, DustID.Grass, dustVel, 80, default, 1f);
            d.noGravity = true;
        }
        return true;
    }

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
        // 稍微增加一些随机偏转
        velocity = velocity.RotatedByRandom(MathHelper.ToRadians(2));
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 12)
            .AddIngredient(ItemID.Vine, 3)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

/// <summary>
/// 藤蔓猎弓的全局弹幕修改 - 对箭矢命中的敌人施加中毒
/// 通过GlobalProjectile实现弓的特殊效果
/// </summary>
public class VineHunterBowGlobalProj : GlobalProjectile
{
    public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
        Player owner = Main.player[projectile.owner];
        if (owner.active && owner.HeldItem?.ModItem is VineHunterBow) {
            if (Main.rand.NextBool(4)) {
                target.AddBuff(BuffID.Poisoned, 90);
            }
        }
    }
}
