using AncientChineseMythology.Celestias.Boss.Dryades.Items;
using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.DivineWoods;

/// <summary>
/// 神木火铳 - 三连发荆棘弹，每第三连发替换为一颗弧线种子迫击炮
/// 荆棘弹使用LightShot高速穿透，种子弹落地后爆炸生成短暂荆棘领域
/// </summary>
public class DivineWoodMusket : ModItem
{
    private int burstCounter;

    public override void SetDefaults() {
        Item.damage = 140;
        Item.crit = 12;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 52;
        Item.height = 26;
        Item.useTime = 3;
        Item.useAnimation = 18;
        Item.knockBack = 5f;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.value = Item.buyPrice(gold: 50);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item40;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<DivineWoodThornNeedle>();
        Item.shootSpeed = 18f;
        Item.useAmmo = AmmoID.Bullet;
        Item.crit = 10;
    }

    public override Vector2? HoldoutOffset() {
        return new Vector2(-10, 2);
    }

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
        type = ModContent.ProjectileType<DivineWoodThornNeedle>();
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        burstCounter++;
        Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
        Vector2 muzzlePos = position + muzzleDir * 40f;

        if (burstCounter % 9 == 0) {
            // 每第9发(第3次三连最后一发)射出弧线种子弹
            Vector2 mortarVel = velocity * 0.7f + new Vector2(0, -4f);
            Projectile.NewProjectile(source, muzzlePos, mortarVel,
                ModContent.ProjectileType<DivineWoodSeedMortar>(),
                damage * 3, knockback * 2f, player.whoAmI);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.5f, Volume = 0.8f }, position);
        }
        else {
            Vector2 perturbedVel = velocity.RotatedByRandom(MathHelper.ToRadians(4));
            Projectile.NewProjectile(source, muzzlePos, perturbedVel, type, damage, knockback, player.whoAmI);
        }

        // 枪口粒子
        for (int i = 0; i < 4; i++) {
            Vector2 dustVel = -muzzleDir.RotatedByRandom(0.3f) * Main.rand.NextFloat(2f, 4f);
            Dust d = Dust.NewDustPerfect(muzzlePos, DustID.JungleTorch, dustVel, 80, default, 1.2f);
            d.noGravity = true;
        }

        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<Livinglog>(12)
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
}

/// <summary>
/// 荆棘针弹 - 高速穿透弹丸，使用LightShot渲染
/// </summary>
public class DivineWoodThornNeedle : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 10;
    }

    public override void SetDefaults() {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 120;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.extraUpdates = 3;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.15f, 0.5f, 0.15f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 180);
        for (int i = 0; i < 6; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(4f, 4f), 60, default, 1.5f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 绿芯双层 ribbon 拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 6f,
            outerColor: new Color(20, 110, 55, 150), innerColor: new Color(170, 255, 150, 200),
            tex: ACMAsset.LightShot, uvScroll: -Main.GlobalTimeWrappedHourly * 2.5f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        Texture2D sg = ACMAsset.SoftGlow;

        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(80, 230, 90), Projectile.rotation,
            lsh.Size() * 0.5f,
            new Vector2(0.55f, 0.10f), SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(60, 200, 70) * 0.60f, 0f,
            sg.Size() * 0.5f,
            0.25f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 种子迫击弹 - 弧线飞行，落地爆炸生成荆棘领域
/// 使用SoftGlow + SlashBurst渲染爆炸
/// </summary>
public class DivineWoodSeedMortar : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    public override void SetDefaults() {
        Projectile.width = 18;
        Projectile.height = 18;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        Projectile.velocity.Y += 0.25f;
        Projectile.rotation += 0.2f;
        Lighting.AddLight(Projectile.Center, 0.2f, 0.6f, 0.2f);

        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
            -Projectile.velocity * 0.1f, 60, default, 1.5f);
        d.noGravity = true;
    }

    public override void OnKill(int timeLeft) {
        Explode();
    }

    private void Explode() {
        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = 0.3f }, Projectile.Center);

        if (Main.myPlayer == Projectile.owner) {
            Projectile.NewProjectile(
                Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<DivineWoodThornFieldExplosion>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner);
        }

        for (int i = 0; i < 25; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
            Dust boom = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                vel, 40, default, Main.rand.NextFloat(2f, 3f));
            boom.noGravity = true;
        }

        // 落地荆棘领域绽放演出 (DrawShockwaveRing + 径向辉光由演出弹幕承载) + 轻度震屏
        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
            ACMWeaponBurst.DivineWood, scale: 1.5f, owner: Projectile.owner);
        WeaponVFX.AddScreenShake(Projectile.Center, 4f);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D sg = ACMAsset.SoftGlow;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        float pulse = 0.5f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.3f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(60, 220, 70) * 0.70f, 0f,
            sg.Size() * 0.5f,
            pulse, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 210) * 0.40f, 0f,
            sg.Size() * 0.5f,
            pulse * 0.5f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 荆棘领域爆炸 - 种子落地后产生的扩散伤害区域
/// 使用SlashBurst + SoftGlow做自然爆炸光效
/// </summary>
public class DivineWoodThornFieldExplosion : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 45;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Projectile.ai[0]++;
        float radius = Projectile.ai[0] * 10f;

        for (int i = 0; i < 6; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.5f, radius);
            Dust d = Dust.NewDustPerfect(pos, DustID.JungleTorch,
                Main.rand.NextVector2Circular(1f, 1f), 60, default, 1.5f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.3f, 1f, 0.3f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 480);
        target.AddBuff(BuffID.Venom, 240);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float radius = Projectile.ai[0] * 10f;
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
    }

    public override bool PreDraw(ref Color lightColor) {
        float prog = 1f - Projectile.timeLeft / 45f;
        float alpha = ACMUtils.QuadOut(1f - prog) * 0.88f;
        float scale = MathHelper.SmoothStep(0f, 12f, ACMUtils.QuadOut(prog));

        // 荆棘领域扩张冲击环 (绿)
        float ringR = Projectile.ai[0] * 10f;
        WeaponVFX.DrawShockwaveRing(Projectile.Center, ringR, 12f, alpha * 0.8f,
            new Color(170, 255, 150), new Color(20, 110, 55));

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D sparkle = ACMAsset.Sparkle;

        // 放射状藤蔓爆发
        for (int k = 0; k < 6; k++) {
            float bAngle = k * MathF.PI / 3f + Projectile.ai[0] * 0.02f;
            float bLen = (k % 2 == 0) ? scale * 0.55f : scale * 0.35f;
            Color bColor = (k % 2 == 0) ? new Color(40, 200, 60) : new Color(160, 255, 180);
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                bColor * (alpha * 0.75f), bAngle,
                new Vector2(burst.Width * 0.5f, burst.Height), // 从底部中心发散
                new Vector2(0.12f, bLen), SpriteEffects.None, 0);
        }

        // 外层柔光
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(60, 200, 70) * (alpha * 0.45f), 0f,
            sg.Size() * 0.5f,
            scale * 0.50f, SpriteEffects.None, 0);

        // 中心白核
        float flashAlpha = MathHelper.SmoothStep(1f, 0f, prog * 1.5f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 230) * (alpha * flashAlpha), 0f,
            sg.Size() * 0.5f,
            scale * 0.18f, SpriteEffects.None, 0);

        // 花瓣Sparkle装饰
        sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
            new Color(100, 255, 120) * (alpha * 0.50f),
            Projectile.ai[0] * 0.04f,
            sparkle.Size() * 0.5f,
            scale * 0.20f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
