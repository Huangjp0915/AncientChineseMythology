using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.ArrogantDivineSylvans;

/// <summary>
/// 傲世神木·连天弩 - 神木火铳的终极形态
/// 五连发金翠荆棘弹，每第5连发(第25弹)射出三枚弧线分裂种子迫击炮
/// 荆棘弹击中敌人后连锁弹射至附近1个敌人
/// 累计命中50次后触发「万棘狂涌」：15枚全追踪荆棘弹风暴
/// </summary>
public class ArrogantDivineSylvanMusket : ModItem
{
    private int burstCounter;
    private int furyCounter;

    public override void SetDefaults() {
        Item.damage = 300;
        Item.crit = 24;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 56;
        Item.height = 28;
        Item.useTime = 2;
        Item.useAnimation = 14;
        Item.knockBack = 8f;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.value = Item.buyPrice(gold: 500);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item40;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<ArrogantSylvanThornNeedle>();
        Item.shootSpeed = 24f;
        Item.useAmmo = AmmoID.Bullet;
    }

    public override Vector2? HoldoutOffset() => new Vector2(-12, 2);

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity,
        ref int type, ref int damage, ref float knockback) {
        type = ModContent.ProjectileType<ArrogantSylvanThornNeedle>();
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
        Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        burstCounter++;
        Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
        Vector2 muzzlePos = position + muzzleDir * 45f;

        // 每第25发(第5次五连最后一发)射出三枚弧线种子弹
        if (burstCounter % 25 == 0) {
            for (int k = -1; k <= 1; k++) {
                Vector2 mortarVel = velocity.RotatedBy(MathHelper.ToRadians(12 * k)) * 0.65f +
                    new Vector2(0, -5f);
                Projectile.NewProjectile(source, muzzlePos, mortarVel,
                    ModContent.ProjectileType<ArrogantSylvanSeedMortar>(),
                    damage * 4, knockback * 3f, player.whoAmI);
            }
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.3f, Volume = 1.2f }, position);
            Main.player[player.whoAmI].GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 8);
        }
        else {
            Vector2 perturbedVel = velocity.RotatedByRandom(MathHelper.ToRadians(3));
            Projectile.NewProjectile(source, muzzlePos, perturbedVel, type, damage, knockback,
                player.whoAmI, ai0: furyCounter);
        }

        // 枪口粒子 - 金翠双色
        for (int i = 0; i < 6; i++) {
            Vector2 dustVel = -muzzleDir.RotatedByRandom(0.35f) * Main.rand.NextFloat(3f, 7f);
            int dustType = i % 2 == 0 ? DustID.JungleTorch : DustID.GoldFlame;
            Dust d = Dust.NewDustPerfect(muzzlePos, dustType, dustVel, 60, default, 1.5f);
            d.noGravity = true;
        }

        return false;
    }

    /// <summary>由荆棘针弹在OnHitNPC中调用,增加狂涌计数</summary>
    public void IncrementFury() {
        furyCounter++;
        if (furyCounter >= 50) {
            furyCounter = 0;
            // 触发万棘狂涌标志 - 下次射击由荆棘弹AI检测
        }
    }
}

/// <summary>
/// 傲世荆棘针弹 - 高速穿透弹丸，使用LightShot渲染
/// 命中时连锁弹射至附近1个敌人
/// 累计50次命中触发万棘狂涌：释放15枚全追踪弹幕
/// </summary>
public class ArrogantSylvanThornNeedle : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    private ref float ChainCount => ref Projectile.ai[1];

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
    }

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 140;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.extraUpdates = 4;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.25f, 0.7f, 0.2f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 300);
        target.AddBuff(BuffID.Venom, 180);

        for (int i = 0; i < 10; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(5f, 5f), 40, default, 2f);
            d.noGravity = true;
        }

        // 连锁弹射：找最近的其他敌人，发射一枚新弹
        if (ChainCount < 2 && Projectile.owner == Main.myPlayer) {
            float closestDist = 500f;
            int chainTarget = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy() || npc.whoAmI == target.whoAmI) continue;
                float d = Vector2.Distance(target.Center, npc.Center);
                if (d < closestDist) { closestDist = d; chainTarget = i; }
            }
            if (chainTarget >= 0) {
                Vector2 chainVel = (Main.npc[chainTarget].Center - target.Center).SafeNormalize(Vector2.UnitX) * 22f;
                int chain = Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, chainVel,
                    Type, Projectile.damage * 3 / 4, Projectile.knockBack * 0.5f, Projectile.owner);
                if (chain >= 0 && chain < Main.maxProjectiles) {
                    Main.projectile[chain].ai[1] = ChainCount + 1;
                }
            }
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
            Color trailCol = Color.Lerp(new Color(220, 255, 100), new Color(50, 180, 60), (float)i / ProjectileID.Sets.TrailCacheLength[Type]);
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, trailCol * a, Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.40f + i * 0.01f, 0.10f), SpriteEffects.None, 0);
        }

        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 120), Projectile.rotation,
            lsh.Size() * 0.5f,
            new Vector2(0.60f, 0.12f), SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 100) * 0.65f, 0f,
            sg.Size() * 0.5f,
            0.30f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 傲世种子迫击弹 - 弧线飞行，落地分裂爆炸
/// 分裂成3枚子迫击弹，每枚产生独立爆炸场 + 8枚追踪藤蛇弹
/// </summary>
public class ArrogantSylvanSeedMortar : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private ref float IsChild => ref Projectile.ai[0];

    public override void SetDefaults() {
        Projectile.width = 22;
        Projectile.height = 22;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 200;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        Projectile.velocity.Y += 0.22f;
        Projectile.rotation += 0.25f;
        Lighting.AddLight(Projectile.Center, 0.3f, 0.8f, 0.25f);

        for (int i = 0; i < 2; i++) {
            int dustType = i == 0 ? DustID.JungleTorch : DustID.GoldFlame;
            Dust d = Dust.NewDustPerfect(Projectile.Center, dustType,
                -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(1f, 1f), 50, default, 1.8f);
            d.noGravity = true;
        }
    }

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = 0.2f }, Projectile.Center);

        if (Main.myPlayer == Projectile.owner) {
            // 爆炸场
            Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<ArrogantSylvanThornFieldExplosion>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner);

            // 如果不是子弹药，分裂成3枚子迫击弹
            if (IsChild == 0) {
                for (int k = -1; k <= 1; k++) {
                    float angle = MathHelper.ToRadians(45 * k) - MathHelper.PiOver2;
                    Vector2 splitVel = angle.ToRotationVector2() * 9f;
                    int p = Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, splitVel,
                        Type, Projectile.damage / 2, Projectile.knockBack, Projectile.owner);
                    if (p >= 0 && p < Main.maxProjectiles) Main.projectile[p].ai[0] = 1f;
                }
            }

            // 释放追踪藤蛇弹
            int serpentCount = IsChild == 0 ? 10 : 6;
            for (int i = 0; i < serpentCount; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(7f, 7f);
                Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, vel,
                    ModContent.ProjectileType<ArrogantSylvanMusketSerpent>(),
                    Projectile.damage / 3, 2f, Projectile.owner);
            }
        }

        for (int i = 0; i < 35; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(10f, 10f);
            int dustType = i % 3 == 0 ? DustID.GoldFlame : DustID.JungleTorch;
            Dust d = Dust.NewDustPerfect(Projectile.Center, dustType,
                vel, 30, default, Main.rand.NextFloat(2f, 3.5f));
            d.noGravity = true;
        }

        if (Main.player[Projectile.owner].whoAmI == Main.myPlayer)
            Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(8, 10);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D sg = ACMAsset.SoftGlow;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        float pulse = 0.6f + 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 0.3f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 100) * 0.75f, 0f,
            sg.Size() * 0.5f, pulse, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 255, 220) * 0.45f, 0f,
            sg.Size() * 0.5f, pulse * 0.5f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 傲世荆棘领域爆炸 - 种子迫击弹爆炸范围场
/// 8道放射状SlashBurst + SoftGlow，范围随时间增长
/// </summary>
public class ArrogantSylvanThornFieldExplosion : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 55;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Projectile.ai[0]++;
        float radius = Projectile.ai[0] * 14f;

        for (int i = 0; i < 8; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.4f, radius);
            int dustType = i % 2 == 0 ? DustID.JungleTorch : DustID.GoldFlame;
            Dust d = Dust.NewDustPerfect(pos, dustType,
                Main.rand.NextVector2Circular(1.5f, 1.5f), 50, default, 2f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.5f, 1.2f, 0.4f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 600);
        target.AddBuff(BuffID.Venom, 300);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float radius = Projectile.ai[0] * 14f;
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
    }

    public override bool PreDraw(ref Color lightColor) {
        float prog = 1f - Projectile.timeLeft / 55f;
        float alpha = ACMUtils.QuadOut(1f - prog) * 0.90f;
        float scale = MathHelper.SmoothStep(0f, 16f, ACMUtils.QuadOut(prog));

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D sparkle = ACMAsset.Sparkle;

        for (int k = 0; k < 8; k++) {
            float bAngle = k * MathF.PI / 4f + Projectile.ai[0] * 0.025f;
            float bLen = k % 2 == 0 ? scale * 0.60f : scale * 0.38f;
            Color bColor = k % 2 == 0 ? new Color(220, 255, 100) : new Color(40, 200, 60);
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                bColor * (alpha * 0.80f), bAngle,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.14f, bLen), SpriteEffects.None, 0);
        }

        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 100) * (alpha * 0.50f), 0f,
            sg.Size() * 0.5f, scale * 0.55f, SpriteEffects.None, 0);

        float flashAlpha = MathHelper.SmoothStep(1f, 0f, prog * 1.4f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 255, 220) * (alpha * flashAlpha), 0f,
            sg.Size() * 0.5f, scale * 0.20f, SpriteEffects.None, 0);

        sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 120) * (alpha * 0.55f),
            Projectile.ai[0] * 0.05f,
            sparkle.Size() * 0.5f, scale * 0.22f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 傲世藤蛇弹 - 迫击弹爆炸后释放的追踪弹幕
/// 高速追踪最近敌人，命中3次消失
/// </summary>
public class ArrogantSylvanMusketSerpent : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    private float _timer;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
    }

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 120;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation = Projectile.velocity.ToRotation();

        if (_timer < 12) {
            Projectile.velocity *= 0.94f;
        }
        else {
            float closestDist = 700f;
            int targetIdx = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < closestDist) { closestDist = d; targetIdx = i; }
            }
            if (targetIdx >= 0) {
                Vector2 dir = Projectile.DirectionTo(Main.npc[targetIdx].Center);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 20f, 0.14f);
            }
            else {
                Projectile.velocity *= 1.01f;
            }
        }

        Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
            -Projectile.velocity * 0.05f, 60, default, 1.4f);
        trail.noGravity = true;
        Lighting.AddLight(Projectile.Center, 0.15f, 0.5f, 0.15f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 300);
        target.AddBuff(BuffID.Venom, 180);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.50f;
            Color c = Color.Lerp(new Color(220, 255, 100), new Color(40, 200, 60),
                (float)i / ProjectileID.Sets.TrailCacheLength[Type]);
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, c * a, Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.30f, 0.07f), SpriteEffects.None, 0);
        }

        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 100), Projectile.rotation,
            lsh.Size() * 0.5f,
            new Vector2(0.45f, 0.09f), SpriteEffects.None, 0);

        Texture2D sg = ACMAsset.SoftGlow;
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(180, 255, 80) * 0.50f, 0f,
            sg.Size() * 0.5f, 0.22f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
