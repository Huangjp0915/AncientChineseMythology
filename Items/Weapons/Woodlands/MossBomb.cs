using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands;

/// <summary>
/// 苔藓爆弹 - 投掷物类武器
/// 投掷后弧线飞行，碰撞后爆炸释放绿色蘑菇云，对范围内敌人造成伤害
/// 不可消耗，有冷却时间
/// </summary>
public class MossBomb : ModItem
{
    public override void SetDefaults() {
        Item.damage = 20;
        Item.crit = 4;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 24;
        Item.height = 24;
        Item.useTime = 35;
        Item.useAnimation = 35;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 5f;
        Item.value = Item.buyPrice(silver: 60);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<MossBombProj>();
        Item.shootSpeed = 10f;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        // 稍微上抛
        Vector2 launchVel = velocity + new Vector2(0, -2f);
        Projectile.NewProjectile(source, position, launchVel, type, damage, knockback, player.whoAmI);
        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 10)
            .AddIngredient(ItemID.GlowingMushroom, 10)
            .AddIngredient(ItemID.Gel, 15)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

/// <summary>
/// 苔藓爆弹弹幕 - 弧线飞行，碰撞后爆炸
/// 使用物品贴图作为飞行外观
/// </summary>
public class MossBombProj : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/Woodlands/MossBomb";

    private ref float AiTimer => ref Projectile.ai[0];

    public override void SetDefaults() {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 150;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        AiTimer++;
        Projectile.velocity.Y += 0.20f; // 重力
        Projectile.rotation += Projectile.velocity.X * 0.04f;

        // 飞行粒子
        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                -Projectile.velocity * 0.05f, 80, default, 0.8f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.1f, 0.3f, 0.1f);
    }

    public override void OnKill(int timeLeft) {
        Explode();
    }

    private void Explode() {
        if (Projectile.ai[1] != 0) return;
        Projectile.ai[1] = 1;

        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = 0.4f }, Projectile.Center);

        // 生成爆炸范围弹幕
        if (Main.myPlayer == Projectile.owner) {
            Projectile.NewProjectile(
                Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<MossExplosion>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner);
        }

        // 爆炸粒子 - 绿色蘑菇云
        for (int i = 0; i < 20; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                vel, 40, default, Main.rand.NextFloat(1.5f, 2.5f));
            d.noGravity = true;
        }
        // 向上升起的烟雾粒子
        for (int i = 0; i < 10; i++) {
            Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-5f, -2f));
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Grass,
                vel, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
        Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null,
            lightColor, Projectile.rotation,
            tex.Size() * 0.5f,
            Projectile.scale, SpriteEffects.None, 0);
        return false;
    }
}

/// <summary>
/// 苔藓爆炸 - 蘑菇云范围伤害，纯粹使用大量Dust粒子模拟绿色蘑菇云
/// </summary>
public class MossExplosion : ModProjectile
{
    public override string Texture
        => $"Terraria/Images/Projectile_{ProjectileID.Grenade}";

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 30;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
        Projectile.alpha = 255; // 完全透明，不绘制自身贴图
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Projectile.ai[0]++;
        float radius = Projectile.ai[0] * 6f;

        // 蘑菇云主体 - 边缘扩散的绿色火焰粒子
        int dustCount = Projectile.ai[0] < 10 ? 6 : 3;
        for (int i = 0; i < dustCount; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.2f, radius);
            Dust d = Dust.NewDustPerfect(pos, DustID.GreenTorch,
                new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-1.5f, -0.3f)),
                60, default, Main.rand.NextFloat(1.5f, 2.5f));
            d.noGravity = true;
        }

        // 向上升起的烟雾柱
        if (Projectile.ai[0] < 15) {
            for (int i = 0; i < 2; i++) {
                Vector2 smokePos = Projectile.Center + new Vector2(Main.rand.NextFloat(-15f, 15f), 0);
                Dust s = Dust.NewDustPerfect(smokePos, DustID.Grass,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-3f, -1.5f)),
                    120, default, Main.rand.NextFloat(2f, 3f));
                s.noGravity = true;
            }
        }

        // 草叶碎片飞散
        if (Main.rand.NextBool(2)) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.5f);
            Dust g = Dust.NewDustPerfect(pos, DustID.GrassBlades,
                angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f), 40, default, 1.2f);
            g.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.15f, 0.5f, 0.15f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 120);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float radius = Projectile.ai[0] * 6f;
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
    }
}
