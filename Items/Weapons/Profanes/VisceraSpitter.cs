using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Profanes;

/// <summary>
/// 脏器喷吐枪 - 五连发血液弹丸（高速穿透）。
/// 蓄压可读化：25 发循环的最后 5 发心跳加速、枪口辉光增长、音高逐发上行，
/// 第 25 发喷出巨型脏器弹（大后坐+震屏+双层湿裂音），直击 +2 剖检印。
/// </summary>
public class VisceraSpitter : ModItem
{
    private int _burstCounter;

    public override void SetDefaults() {
        Item.damage = 1100;
        Item.crit = 12;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 56;
        Item.height = 28;
        Item.useTime = 3;
        Item.useAnimation = 15;
        Item.knockBack = 5f;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.value = Item.buyPrice(gold: 80);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item40;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<VisceraBloodBullet>();
        Item.shootSpeed = 20f;
        Item.useAmmo = AmmoID.Bullet;
    }

    public override Vector2? HoldoutOffset() => new Vector2(-12, 2);

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
        type = ModContent.ProjectileType<VisceraBloodBullet>();
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        _burstCounter++;
        Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
        Vector2 muzzlePos = position + muzzleDir * 44f;
        int cyclePos = _burstCounter % 25;

        if (cyclePos == 0) {
            // 第 25 发: 巨型脏器弹 —— 大后坐 + 震屏 + 双层湿裂 (蓄压峰值)
            Vector2 visceraVel = velocity * 0.65f;
            Projectile.NewProjectile(source, muzzlePos, visceraVel,
                ModContent.ProjectileType<VisceraGlobShot>(),
                damage * 4, knockback * 3f, player.whoAmI);
            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Pitch = 0.3f, Volume = 0.8f }, position);
            ProfaneCommon.PlaySquelch(muzzlePos, 1.2f, -0.15f);
            if (player.whoAmI == Main.myPlayer)
                player.velocity -= muzzleDir * 3f;
            WeaponVFX.AddScreenShake(player.Center, 5f);
        }
        else {
            Vector2 perturbedVel = velocity.RotatedByRandom(MathHelper.ToRadians(5));
            Projectile.NewProjectile(source, muzzlePos, perturbedVel, type, damage, knockback, player.whoAmI);

            // 蓄压尾段 (最后 5 发): 心跳加速 + 音高逐发上行 —— 脏器弹前兆
            if (cyclePos >= 20) {
                float pressure = (cyclePos - 20) / 5f;
                ProfaneCommon.PlayThump(muzzlePos,
                    pitch: MathHelper.Lerp(-0.8f, -0.2f, pressure),
                    volume: 0.4f + 0.3f * pressure);
                // 枪管膨压渗血
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustPerfect(muzzlePos - muzzleDir * Main.rand.NextFloat(10f, 34f),
                        DustID.Blood, Main.rand.NextVector2Circular(1.5f, 1.5f), 0, default, 1.2f + pressure);
                    d.noGravity = true;
                }
            }
            // 微后坐 (每发)
            if (player.whoAmI == Main.myPlayer)
                player.velocity -= muzzleDir * 0.4f;
        }

        // 枪口血液粒子 + 血雾烟 (蓄压尾段辉光增长)
        float glowMult = cyclePos >= 20 ? 1f + (cyclePos - 20) / 5f : 1f;
        for (int i = 0; i < 3; i++) {
            Vector2 dustVel = muzzleDir.RotatedByRandom(0.4f) * Main.rand.NextFloat(3f, 6f);
            Dust d = Dust.NewDustPerfect(muzzlePos, DustID.Blood, dustVel, 0, default, 1.5f * glowMult);
            d.noGravity = true;
        }
        for (int i = 0; i < 2; i++) {
            Vector2 smokeVel = muzzleDir.RotatedByRandom(0.5f) * Main.rand.NextFloat(1.5f, 3.5f);
            Dust s = Dust.NewDustPerfect(muzzlePos, DustID.Smoke, smokeVel, 120,
                new Color(120, 10, 18), 1.3f);
            s.noGravity = true;
        }

        return false;
    }
}

/// <summary>
/// 血液弹丸 - 高速穿透小型弹丸，暗红色LightShot渲染。
/// </summary>
public class VisceraBloodBullet : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 0;
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
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
        Lighting.AddLight(Projectile.Center, 0.35f, 0.05f, 0.04f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 180);
        for (int i = 0; i < 5; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.5f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 统一双层暗红血肉拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 6f,
            outerColor: ProfaneCommon.FleshDark, innerColor: ProfaneCommon.BloodBright,
            uvScroll: -Main.GlobalTimeWrappedHourly * 2f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        Texture2D sg = ACMAsset.SoftGlow;

        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(200, 35, 25), Projectile.rotation,
            lsh.Size() * 0.5f,
            new Vector2(0.50f, 0.10f), SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(180, 30, 25) * 0.55f, 0f,
            sg.Size() * 0.5f,
            0.22f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 脏器弹 - 巨型脏器飞弹，弧线飞行后爆炸。
/// 心跳呼吸缩放；直击 +2 剖检印；爆炸释放血肉膜+追踪触手。
/// </summary>
public class VisceraGlobShot : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private ref float AiTimer => ref Projectile.ai[0];

    public override void SetDefaults() {
        Projectile.width = 24;
        Projectile.height = 24;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 150;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        AiTimer++;
        Projectile.velocity.Y += 0.18f;
        Projectile.rotation += Projectile.velocity.X * 0.03f;
        Projectile.scale = 1f + ProfaneCommon.Heartbeat() * 0.12f; // 心跳呼吸 (系列签名)
        Lighting.AddLight(Projectile.Center, 0.5f, 0.08f, 0.06f);

        // 血液拖尾
        for (int i = 0; i < 2; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                DustID.Blood, -Projectile.velocity * 0.06f, 0, default, 1.6f);
            d.noGravity = true;
        }

        // 超时爆炸
        if (AiTimer > 100) Explode();
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        // 直击 +2 印; 摘取伤害 = 面板×2 (脏器弹为面板×4 → ×0.5)
        ProfaneCommon.AddMark(target, Projectile, 2, Projectile.damage / 2);
        Explode();
    }

    public override void OnKill(int timeLeft) {
        Explode();
    }

    private bool _exploded;
    private void Explode() {
        if (_exploded) return;
        _exploded = true;

        SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 1f, Pitch = -0.4f }, Projectile.Center);
        ProfaneCommon.PlaySquelch(Projectile.Center, 1.2f, -0.25f);

        // 脏器弹爆裂血肉演出 (冲击环 + 径向辉光)
        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
            ACMWeaponBurst.Profane, scale: 1.5f, owner: Projectile.owner);

        if (Main.myPlayer == Projectile.owner) {
            // 爆炸AOE弹幕
            Projectile.NewProjectile(
                Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<VisceraBlastExplosion>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner);

            // 6道追踪内脏碎片 (远程)
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6;
                Vector2 fragVel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 10f);
                Projectile.NewProjectile(
                    Projectile.GetSource_Death(), Projectile.Center, fragVel,
                    ModContent.ProjectileType<ProfaneTendrilChaser>(),
                    Projectile.damage / 3, 2f, Projectile.owner, 0f, ProfaneTendrilChaser.ClassRanged);
            }
        }

        for (int i = 0; i < 30; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(10f, 10f);
            Dust boom = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                vel, 0, default, Main.rand.NextFloat(2f, 3.5f));
            boom.noGravity = true;
        }

        WeaponVFX.AddScreenShake(Projectile.Center, 5f);
        Projectile.Kill();
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D sg = ACMAsset.SoftGlow;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        float pulse = 0.55f + 0.25f * ProfaneCommon.Heartbeat();
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(220, 40, 30) * 0.65f, 0f,
            sg.Size() * 0.5f,
            pulse, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 180, 160) * 0.35f, 0f,
            sg.Size() * 0.5f,
            pulse * 0.4f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 脏器爆炸 - FleshPulse 血肉膜 + SlashBurst 放射。
/// 伤害半径与膜/冲击环视觉严格对齐（上限 150px）。
/// </summary>
public class VisceraBlastExplosion : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private ref float Timer => ref Projectile.ai[0];
    private float Seed => (Projectile.whoAmI * 0.163f) % 1f;

    private float Radius => MathF.Min(16f + Timer * 12f, 150f);

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
        Timer++;

        for (int i = 0; i < 5; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(Radius * 0.4f, Radius);
            Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                Main.rand.NextVector2Circular(2f, 2f), 0, default, 1.8f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.6f, 0.1f, 0.08f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 480);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, Radius, targetHitbox);
    }

    public override bool PreDraw(ref Color lightColor) {
        float prog = 1f - Projectile.timeLeft / 45f;
        float alpha = ACMUtils.QuadOut(1f - prog) * 0.88f;
        float scale = MathHelper.SmoothStep(0f, 13f, ACMUtils.QuadOut(prog));

        // 血肉膜 (与伤害半径同式) + 冲击环
        ProfaneCommon.DrawFleshMembrane(Projectile.Center, Radius * 1.06f, alpha * 0.85f,
            1f - prog, veinBoost: 0.5f, seed: Seed);
        WeaponVFX.DrawShockwaveRing(Projectile.Center, Radius, 10f, alpha * 0.85f,
            ProfaneCommon.BloodBright, ProfaneCommon.FleshDark);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D sg = ACMAsset.SoftGlow;

        // 放射状血肉爆发
        for (int k = 0; k < 8; k++) {
            float bAngle = k * MathF.PI / 4f + Timer * 0.02f;
            bool cardinal = (k % 2 == 0);
            Color bColor = cardinal ? ProfaneCommon.FleshMid : ProfaneCommon.BloodBright;
            float bLen = cardinal ? scale * 0.50f : scale * 0.32f;
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                bColor * (alpha * 0.70f), bAngle,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.13f, bLen), SpriteEffects.None, 0);
        }

        // 中心白核
        float flashAlpha = MathHelper.SmoothStep(1f, 0f, prog * 1.5f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 220, 210) * (alpha * flashAlpha), 0f,
            sg.Size() * 0.5f,
            scale * 0.18f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
