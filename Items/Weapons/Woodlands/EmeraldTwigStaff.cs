using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands;

/// <summary>
/// 翡翠树枝杖 - 法师法杖类武器
/// 释放一段飞旋的小树枝攻击，穿透1个敌人
/// 命中敌人后碎裂为叶片
/// </summary>
public class EmeraldTwigStaff : ModItem
{
    public override void SetDefaults() {
        Item.damage = 16;
        Item.crit = 4;
        Item.DamageType = DamageClass.Magic;
        Item.width = 36;
        Item.height = 36;
        Item.useTime = 28;
        Item.useAnimation = 28;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 3f;
        Item.value = Item.buyPrice(silver: 50);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<EmeraldTwigBolt>();
        Item.shootSpeed = 10f;
        Item.mana = 6;
        Item.staff[Type] = true;
    }

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
        velocity = velocity.RotatedByRandom(MathHelper.ToRadians(5));
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 10)
            .AddIngredient(ItemID.Emerald, 3)
            .AddIngredient(ItemID.JungleSpores, 3)
            .AddIngredient(ItemID.FallenStar, 2)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

/// <summary>
/// 翡翠树枝弹 - 翠绿能量弹幕
/// 使用 LightShot + SoftGlow 叠加渲染，拖尾发光效果
/// </summary>
public class EmeraldTwigBolt : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 6;
    }

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 90;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
        Projectile.extraUpdates = 1;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.1f, 0.25f, 0.1f);

        // 翠绿粒子尾迹
        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                60, default, 0.8f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        var sb = Main.spriteBatch;
        var lightShot = ACMAsset.LightShot;
        var softGlow = ACMAsset.SoftGlow;
        var origin = lightShot.Size() / 2f;
        var glowOrigin = softGlow.Size() / 2f;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        // 拖尾
        for (int i = 1; i < Projectile.oldPos.Length; i++) {
            Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
            float progress = 1f - (float)i / Projectile.oldPos.Length;
            float scale = 0.25f * progress;
            Color trailColor = new Color(30, 160, 50) * progress * 0.6f;
            sb.Draw(lightShot, drawPos, null, trailColor, Projectile.oldRot[i], origin, scale, SpriteEffects.None, 0f);
        }

        // 主体
        Vector2 mainPos = Projectile.Center - Main.screenPosition;
        sb.Draw(lightShot, mainPos, null, new Color(60, 200, 70), Projectile.rotation, origin, new Vector2(0.3f, 0.1f), SpriteEffects.None, 0f);

        // 柔光
        sb.Draw(softGlow, mainPos, null, new Color(40, 180, 55) * 0.45f, 0f, glowOrigin, 0.5f, SpriteEffects.None, 0f);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        return false;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        for (int i = 0; i < 6; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.GreenTorch,
                Main.rand.NextVector2Circular(3f, 3f), 60, default, 1.1f);
            d.noGravity = true;
        }
    }

    public override void OnKill(int timeLeft) {
        for (int i = 0; i < 8; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                Main.rand.NextVector2CircularEdge(3f, 3f), 40, default, 1f);
            d.noGravity = true;
        }
    }
}
