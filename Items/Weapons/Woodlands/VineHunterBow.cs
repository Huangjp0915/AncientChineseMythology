using AncientChineseMythology.Helpers;
using System;
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
/// 藤蔓猎弓的全局弹幕修改 - 对箭矢命中的敌人施加中毒, 并为本弓发射的箭附加翠绿藤蔓拖尾。
/// 通过GlobalProjectile实现弓的特殊效果 (表现层: 记录飞行轨迹点, 绘制双层 ribbon 拖尾)。
/// </summary>
public class VineHunterBowGlobalProj : GlobalProjectile
{
    public override bool InstancePerEntity => true;

    /// <summary>本弹幕是否由藤蔓猎弓发射 (用于附加拖尾/命中演出, 不改变机制)。</summary>
    private bool _fromVineBow;
    private Vector2[] _history;
    private int _histCount;

    public override void OnSpawn(Projectile projectile, IEntitySource source) {
        if (source is EntitySource_ItemUse itemSource && itemSource.Item?.ModItem is VineHunterBow)
            _fromVineBow = true;
    }

    public override void AI(Projectile projectile) {
        if (!_fromVineBow)
            return;

        // 记录最近轨迹点 (头→尾), 供 ribbon 拖尾使用
        _history ??= new Vector2[12];
        for (int i = _history.Length - 1; i > 0; i--)
            _history[i] = _history[i - 1];
        _history[0] = projectile.Center;
        if (_histCount < _history.Length)
            _histCount++;

        // 少量藤蔓翠尘
        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(projectile.Center, DustID.Grass,
                -projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                90, default, 0.85f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(Projectile projectile, ref Color lightColor) {
        if (_fromVineBow && _histCount >= 2) {
            Vector2[] pts = new Vector2[_histCount];
            Array.Copy(_history, pts, _histCount);
            // 藤蔓短拖尾 (外暗深绿 + 内亮嫩绿)
            WeaponVFX.DrawRibbonTrail(pts, baseWidth: 6f,
                outerColor: new Color(40, 140, 50, 150), innerColor: new Color(150, 240, 120, 200),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);
        }
        return true; // 保留箭矢贴图
    }

    public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
        Player owner = Main.player[projectile.owner];
        if (owner.active && owner.HeldItem?.ModItem is VineHunterBow) {
            if (Main.rand.NextBool(4)) {
                target.AddBuff(BuffID.Poisoned, 90);
            }
        }
        if (_fromVineBow)
            ACMWeaponBurst.Spawn(projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Nature, scale: 0.6f, owner: projectile.owner);
    }
}
