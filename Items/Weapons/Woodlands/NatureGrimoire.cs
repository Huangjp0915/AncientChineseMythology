using System;
using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands;

/// <summary>
/// 自然秘典 - 法师书类武器
/// 翻书释放扇形散射的3片树叶弹幕，角度较宽
/// 叶片飘飞，命中后有概率施加中毒
/// </summary>
public class NatureGrimoire : ModItem
{
    public override void SetDefaults() {
        Item.damage = 13;
        Item.crit = 6;
        Item.DamageType = DamageClass.Magic;
        Item.width = 28;
        Item.height = 32;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 2.5f;
        Item.value = Item.buyPrice(silver: 55);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<NatureGrimoireLeaf>();
        Item.shootSpeed = 8f;
        Item.mana = 8;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        // 扇形发射3片叶片
        float spreadAngle = MathHelper.ToRadians(18);
        for (int i = -1; i <= 1; i++) {
            Vector2 leafVel = velocity.RotatedBy(spreadAngle * i) * Main.rand.NextFloat(0.9f, 1.1f);
            Projectile.NewProjectile(source, position, leafVel, type, damage, knockback, player.whoAmI,
                ai0: Main.rand.NextFloat(MathHelper.TwoPi));
        }
        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Book, 1)
            .AddIngredient(ItemID.JungleSpores, 5)
            .AddIngredient(ItemID.Vine, 2)
            .AddIngredient(ItemID.FallenStar, 3)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

/// <summary>
/// 自然秘典叶片 - 飘飞旋转的树叶弹幕
/// 使用原版叶片水晶法杖的叶子贴图，自然飘动飞行
/// </summary>
public class NatureGrimoireLeaf : ModProjectile
{
    public override string Texture
        => $"Terraria/Images/Projectile_{ProjectileID.Leaf}";

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 5;
    }

    public override void SetDefaults() {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 90;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        // 五帧叶片动画
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 5) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % 5;
        }

        Projectile.ai[1]++;

        // 叶子自然飘动 - 正弦波横向偏移
        float wave = MathF.Sin(Projectile.ai[0] + Projectile.ai[1] * 0.12f) * 0.6f;
        Vector2 perpendicular = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X).SafeNormalize(Vector2.Zero);
        Projectile.position += perpendicular * wave;

        // 叶子旋转
        Projectile.rotation += Projectile.velocity.X * 0.04f;

        // 轻微减速 + 受重力影响，模拟叶片飘落感
        if (Projectile.ai[1] > 30f) {
            Projectile.velocity.Y += 0.02f;
        }

        Lighting.AddLight(Projectile.Center, 0.05f, 0.12f, 0.03f);

        // 偶尔飘落小叶片粒子
        if (Main.rand.NextBool(6)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Grass,
                Main.rand.NextVector2Circular(0.5f, 0.5f), 80, default, 0.8f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 叶片飘飞时残留翠绿柔光 (毒/自然能量感), 保留原版叶片帧绘制
        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.32f, new Color(80, 200, 90) * 0.7f);
        return true;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        bool poisoned = Main.rand.NextBool(3);
        if (poisoned) {
            target.AddBuff(BuffID.Poisoned, 90);
        }
        // 命中时散落叶片
        for (int i = 0; i < 5; i++) {
            int dustType = Main.rand.NextBool() ? DustID.Grass : DustID.GrassBlades;
            Dust d = Dust.NewDustPerfect(target.Center, dustType,
                Main.rand.NextVector2Circular(3f, 3f), 60, default, 1.2f);
            d.noGravity = true;
        }
        // 中毒触发时额外飘几缕翠尘强调
        if (poisoned) {
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.GreenTorch,
                    Main.rand.NextVector2Circular(2f, 2f), 80, default, 0.9f);
                d.noGravity = true;
            }
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.Nature, scale: 0.7f, owner: Projectile.owner);
    }

    public override void OnKill(int timeLeft) {
        // 消散时散落叶片碎屑
        for (int i = 0; i < 6; i++) {
            int dustType = Main.rand.NextBool() ? DustID.Grass : DustID.GrassBlades;
            Dust d = Dust.NewDustPerfect(Projectile.Center, dustType,
                Main.rand.NextVector2CircularEdge(2f, 2f), 40, default, 1f);
            d.noGravity = true;
        }
    }
}
