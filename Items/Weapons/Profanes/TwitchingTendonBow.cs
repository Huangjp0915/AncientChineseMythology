using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Profanes;

/// <summary>
/// 抽搐筋腱弓 - 射手弓类武器
/// 将箭矢转化为血肉脊椎箭，高速穿透+沿途留下血滴
/// 每第3箭替换为一颗巨型眼球弹，爆炸释放6道追踪血刺
/// 两侧同时释放旋转筋腱飞刃
/// </summary>
public class TwitchingTendonBow : ModItem
{
    private int _shotCount;

    public override void SetDefaults() {
        Item.damage = 1200;
        Item.crit = 14;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 24;
        Item.height = 56;
        Item.useTime = 16;
        Item.useAnimation = 16;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 4f;
        Item.value = Item.buyPrice(gold: 80);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item5;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<TendonSpineBolt>();
        Item.shootSpeed = 18f;
        Item.useAmmo = AmmoID.Arrow;
    }

    public override Vector2? HoldoutOffset() => new Vector2(-2, 0);

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
        type = ModContent.ProjectileType<TendonSpineBolt>();
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        _shotCount++;

        if (_shotCount % 3 == 0) {
            // 每第3箭：巨型眼球弹
            Projectile.NewProjectile(source, position, velocity * 0.85f,
                ModContent.ProjectileType<TendonEyeballShot>(),
                (int)(damage * 2.5f), knockback * 2f, player.whoAmI);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.5f, Volume = 0.7f }, position);
        }
        else {
            // 主箭：脊椎箭
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
        }

        // 两侧筋腱飞刃
        for (int i = 0; i < 2; i++) {
            float offsetAngle = (i == 0 ? -1 : 1) * MathHelper.ToRadians(20);
            Vector2 perturbedVel = velocity.RotatedBy(offsetAngle) * 0.85f;
            Projectile.NewProjectile(source, position, perturbedVel,
                ModContent.ProjectileType<TendonSpiralBlade>(),
                (int)(damage * 0.5f), knockback * 0.3f, player.whoAmI,
                ai0: MathHelper.TwoPi * i / 2, ai1: 0);
        }

        return false;
    }
}

/// <summary>
/// 脊椎箭 - 高速穿透主箭，使用LightShot暗红渲染
/// 沿途留下血液粒子
/// </summary>
public class TendonSpineBolt : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
    }

    public override void SetDefaults() {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.extraUpdates = 1;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.5f, 0.08f, 0.06f);

        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(1f, 1f),
                0, default, 1.2f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 300);
        for (int i = 0; i < 8; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                Main.rand.NextVector2Circular(5f, 5f), 0, default, 1.8f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        Texture2D sg = ACMAsset.SoftGlow;

        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.60f;
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(200, 25, 15) * a, Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.50f + i * 0.014f, 0.14f), SpriteEffects.None, 0);
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(255, 140, 120) * (a * 0.30f), Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.25f, 0.07f), SpriteEffects.None, 0);
        }

        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(220, 40, 30),
            Projectile.rotation, lsh.Size() * 0.5f,
            new Vector2(0.85f, 0.20f), SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(200, 50, 40) * 0.70f, 0f,
            sg.Size() * 0.5f,
            0.40f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.4f }, Projectile.Center);
        for (int i = 0; i < 8; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.5f);
            d.noGravity = true;
        }
    }
}

/// <summary>
/// 筋腱旋转飞刃 - 两侧释放的螺旋飞行血刃
/// 初期螺旋偏移后追踪最近敌人
/// </summary>
public class TendonSpiralBlade : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/BlankStar";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 10;
    }

    private bool _homing;
    private float _spiralTimer;
    private const float SPIRAL_DURATION = 35f;

    public override void SetDefaults() {
        Projectile.width = 18;
        Projectile.height = 18;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 160;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override void AI() {
        _spiralTimer++;
        Projectile.rotation += 0.28f;

        if (!_homing && _spiralTimer < SPIRAL_DURATION) {
            float baseAngle = Projectile.ai[0];
            float spiralAngle = baseAngle + _spiralTimer * 0.15f;
            float spiralRadius = MathHelper.Lerp(35f, 6f, _spiralTimer / SPIRAL_DURATION);
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = new(-forward.Y, forward.X);
            Vector2 spiralOffset = new Vector2(spiralRadius, 0).RotatedBy(spiralAngle);
            Projectile.Center += perp * spiralOffset.X * 0.15f;
        }
        else {
            _homing = true;
            float closestDist = 550f;
            int targetIdx = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < closestDist) { closestDist = d; targetIdx = i; }
            }
            if (targetIdx >= 0) {
                Vector2 dir = Projectile.DirectionTo(Main.npc[targetIdx].Center);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 17f, 0.12f);
            }
        }

        Lighting.AddLight(Projectile.Center, 0.3f, 0.05f, 0.04f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 240);
        for (int i = 0; i < 5; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.4f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D star = ACMAsset.BlankStar;
        Texture2D sg = ACMAsset.SoftGlow;
        float pulse = 0.55f + 0.18f * MathF.Sin((float)Main.timeForVisualEffects * 0.20f);

        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.45f;
            sb.Draw(sg,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(200, 30, 20) * a, 0f,
                sg.Size() * 0.5f,
                0.30f, SpriteEffects.None, 0);
        }

        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            new Color(220, 50, 30) * (0.80f * pulse),
            Projectile.rotation, star.Size() * 0.5f,
            0.55f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 100, 80) * (0.55f * pulse), 0f,
            sg.Size() * 0.5f,
            0.40f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 眼球弹 - 巨型眼球飞弹，命中或超时后爆炸
/// 爆炸释放6道追踪血刺 + 血液粒子爆发
/// 使用原版EyeOfCthulhu弹幕纹理
/// </summary>
public class TendonEyeballShot : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.EyeLaser;

    private ref float Timer => ref Projectile.ai[0];

    public override void SetDefaults() {
        Projectile.width = 22;
        Projectile.height = 22;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 120;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        Timer++;
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.6f, 0.1f, 0.08f);

        // 脉动效果
        Projectile.scale = 1.0f + 0.1f * MathF.Sin(Timer * 0.3f);

        // 血液拖尾
        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
            -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(1f, 1f),
            0, default, 1.5f);
        d.noGravity = true;

        // 轻微追踪
        if (Timer > 15) {
            float closestDist = 400f;
            int targetIdx = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < closestDist) { closestDist = dist; targetIdx = i; }
            }
            if (targetIdx >= 0) {
                Vector2 dir = Projectile.DirectionTo(Main.npc[targetIdx].Center);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * Projectile.velocity.Length(), 0.03f);
            }
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        Explode();
    }

    public override void OnKill(int timeLeft) {
        Explode();
    }

    private bool _exploded;
    private void Explode() {
        if (_exploded) return;
        _exploded = true;

        SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.3f, Volume = 0.8f }, Projectile.Center);

        // 血液大爆发
        for (int i = 0; i < 25; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                Main.rand.NextVector2CircularEdge(8f, 8f), 0, default,
                Main.rand.NextFloat(1.8f, 3f));
            d.noGravity = true;
        }

        // 释放6道追踪血刺
        if (Main.myPlayer == Projectile.owner) {
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(7f, 11f);
                Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, vel,
                    ModContent.ProjectileType<ProfaneTendrilChaser>(),
                    Projectile.damage / 3, 2f, Projectile.owner);
            }
        }

        Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(4, 6);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D sg = ACMAsset.SoftGlow;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        float pulse = 0.55f + 0.15f * MathF.Sin(Timer * 0.30f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(220, 40, 30) * 0.65f, 0f,
            sg.Size() * 0.5f,
            pulse * 0.7f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 200, 180) * 0.35f, 0f,
            sg.Size() * 0.5f,
            pulse * 0.3f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
        Color tint = Color.Lerp(lightColor, new Color(255, 120, 100), 0.4f);
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            tint, Projectile.rotation, tex.Size() * 0.5f,
            Projectile.scale, SpriteEffects.None, 0);
        return false;
    }
}
