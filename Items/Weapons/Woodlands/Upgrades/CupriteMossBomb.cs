using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

/// <summary>
/// 赤铜苔藓爆弹 — 投掷"灼苔弹" <see cref="CupriteMossBombProj"/>。
/// 可见质变: 飞行 ember 拖尾, 爆炸保留绿色蘑菇云 AoE (机制不变) 并叠加赤铜灼烧演出 (径向辉光 + 冲击环 + 落地屏震)。
/// </summary>
public class CupriteMossBomb : ModItem
{
    public override void SetDefaults() {
        Item.damage = 45;
        Item.crit = 6;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 24;
        Item.height = 24;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 5f;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<CupriteMossBombProj>();
        Item.shootSpeed = 11f;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Vector2 launchVel = velocity + new Vector2(0, -2f);
        Projectile.NewProjectile(source, position, launchVel, type, damage, knockback, player.whoAmI);
        return false;
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
/// 灼苔弹 — 继承 <see cref="MossBombProj"/> 弧线/爆炸机制 (含绿色蘑菇云 AoE), 增强为赤铜灼烧表现。
/// </summary>
public class CupriteMossBombProj : MossBombProj
{
    public override void AI() {
        base.AI();
        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                -Projectile.velocity * 0.08f, 80, default, 0.9f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.45f, 0.22f, 0.06f);
    }

    public override void OnKill(int timeLeft) {
        base.OnKill(timeLeft); // 绿色蘑菇云 AoE + 毒 (机制不变)

        // 赤铜灼烧叠层
        for (int i = 0; i < 16; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, vel, 40, default, Main.rand.NextFloat(1.3f, 2.2f));
            d.noGravity = true;
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
            ACMWeaponBurst.CupriteBurn, scale: 2f, owner: Projectile.owner);
        WeaponVFX.AddScreenShake(Projectile.Center, 5f);
    }

    public override bool PreDraw(ref Color lightColor) {
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 9f,
            outerColor: new Color(180, 60, 20, 130), innerColor: new Color(255, 180, 80, 180),
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);
        return base.PreDraw(ref lightColor); // 原版弹体贴图
    }
}
