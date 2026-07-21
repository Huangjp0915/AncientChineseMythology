using AncientChineseMythology.Celestias.Boss.Dazhengs.Items;
using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Weapons.DivineWoods;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.ArrogantDivineSylvans;

/// <summary>
/// 傲世神木·连天铳 - 神木火铳的终极形态
/// 真·五连发金翠荆棘针 (每发后坐 + 枪口反馈), 命中刻下「年轮烙印」并连锁弹射
/// 每第 25 发射出三枚弧线种子迫击炮, 炮爆**引爆**范围烙印
/// 「万棘狂涌」(修复闭环): 单次炮击引爆 ≥3 层烙印时, 自玩家绽出 8 枚全追踪荆棘
/// </summary>
public class ArrogantDivineSylvanMusket : ModItem
{
    private int burstCounter;

    public override void SetDefaults() {
        Item.damage = 300;
        Item.crit = 24;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 56;
        Item.height = 28;
        Item.useTime = 2;
        Item.useAnimation = 10; // 真·五连发 (10/2=5 发, 30 发/s 与原 7 发/14f 相同)
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

        // 每发后坐: 打在玩家身上的反作用力 (mass is reaction — 空中尤其明显)
        player.velocity -= muzzleDir * 0.55f;

        // 每第 25 发 (第 5 轮五连最后一发) 射出三枚弧线种子迫击炮
        if (burstCounter % 25 == 0) {
            for (int k = -1; k <= 1; k++) {
                Vector2 mortarVel = velocity.RotatedBy(MathHelper.ToRadians(12 * k)) * 0.65f +
                    new Vector2(0, -5f);
                Projectile.NewProjectile(source, muzzlePos, mortarVel,
                    ModContent.ProjectileType<ArrogantSylvanSeedMortar>(),
                    damage * 4, knockback * 3f, player.whoAmI);
            }
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.3f + Main.rand.NextFloat(-0.1f, 0.1f), Volume = 1.1f }, position);
            SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.2f, Volume = 0.8f }, position);
            player.velocity -= muzzleDir * 2.4f; // 重炮后坐 (∝ 弹重)
            WeaponVFX.AddScreenShake(position, 4f);
            // 连天铳炮击触发技 → 短暂金翠染屏定调 (占全屏唯一名额, 同屏≤1 自动仲裁)
            ArrogantSylvanScreenTint.Spawn(source, position, player.whoAmI);
        }
        else {
            Vector2 perturbedVel = velocity.RotatedByRandom(MathHelper.ToRadians(3));
            Projectile.NewProjectile(source, muzzlePos, perturbedVel, type, damage, knockback,
                player.whoAmI);
        }

        // 枪口粒子 - 金翠双色 + 少量下落弹壳金屑
        for (int i = 0; i < 5; i++) {
            Vector2 dustVel = -muzzleDir.RotatedByRandom(0.35f) * Main.rand.NextFloat(3f, 7f);
            int dustType = i % 2 == 0 ? DustID.JungleTorch : DustID.GoldFlame;
            Dust d = Dust.NewDustPerfect(muzzlePos, dustType, dustVel, 60, default, 1.5f);
            d.noGravity = true;
        }
        Dust shell = Dust.NewDustPerfect(position, DustID.GoldCoin,
            new Vector2(-muzzleDir.X * 1.5f, -2.2f), 100, default, 0.9f);
        shell.noGravity = false;

        return false;
    }

    /// <summary>
    /// 万棘狂涌 (由迫击炮爆炸的引爆结果回调, owner 端): 引爆总层数 ≥3 时自玩家绽出 8 枚全追踪荆棘。
    /// </summary>
    public static void TriggerThornFury(Player player, IEntitySource source, int totalStacks) {
        if (totalStacks < 3 || player.whoAmI != Main.myPlayer)
            return;

        SoundEngine.PlaySound(SoundID.Item17 with { Volume = 1.2f, Pitch = 0.4f + Main.rand.NextFloat(-0.1f, 0.1f) }, player.Center);
        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = -0.25f }, player.Center);
        ACMWeaponBurst.Spawn(source, player.Center, ACMWeaponBurst.ArrogantSylvan, 1.4f, player.whoAmI);
        WeaponVFX.AddScreenShake(player.Center, 4f);

        int dmg = player.HeldItem?.damage > 0
            ? (int)player.GetTotalDamage(DamageClass.Ranged).ApplyTo(player.HeldItem.damage)
            : 300;
        for (int i = 0; i < 8; i++) {
            float ang = MathHelper.TwoPi * i / 8f;
            Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(9f, 12f);
            Projectile.NewProjectile(source, player.Center, vel,
                ModContent.ProjectileType<ArrogantSylvanMusketSerpent>(),
                dmg, 2f, player.whoAmI);
        }
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<ArrogantDivineSylvan>(15)
            .AddIngredient<DivineWoodMusket>()
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
}

/// <summary>
/// 傲世荆棘针弹 - 高速穿透弹丸 (金芯翠边流光束核)
/// 命中刻下年轮烙印并连锁弹射至附近 1 个敌人 (最多 2 跳)
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

        // 浇灌: 刻下年轮烙印 (等迫击炮引爆收割)
        ArrogantSylvanBrandNPC.AddStack(target);

        for (int i = 0; i < 6; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, i % 2 == 0 ? DustID.JungleTorch : DustID.GoldFlame,
                Main.rand.NextVector2Circular(5f, 5f), 40, default, 1.8f);
            d.noGravity = true;
        }
        // 高射速武器: 命中演出节流 (3 帧内不重复 spawn)
        ArrogantSylvanFX.HitBurstThrottled(Projectile.GetSource_OnHit(target), target.Center, 0.8f, Projectile.owner);

        // 连锁弹射: 找最近的其他敌人, 发射一枚新弹
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

    /// <summary>主弹金翠流光束核 (ACMShaders.DrawBeam, 金芯 + 翠边)。</summary>
    private void DrawNeedleBeam(Vector2 dir) {
        Vector2 start = Projectile.Center - dir * 64f;
        Vector2 end = Projectile.Center + dir * 8f;
        ACMShaders.DrawBeam(start, end, halfWidth: 6f,
            core: new Color(255, 230, 130, 220), edge: new Color(120, 220, 120, 0),
            intensity: 0.95f, flowSpeed: 2.2f, flowScale: 2.4f, coreSharp: 2.4f);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;

        // 主弹金翠流光束核 (BeamGrad: 金芯 + 翠边, 沿飞行方向)
        Vector2 beamDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        DrawNeedleBeam(beamDir);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        Texture2D sg = ACMAsset.SoftGlow;

        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.60f;
            Color trailCol = Color.Lerp(ArrogantSylvanPalette.GoldBright, ArrogantSylvanPalette.JadeDeep,
                (float)i / ProjectileID.Sets.TrailCacheLength[Type]);
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
            ArrogantSylvanPalette.JadeBright * 0.65f, 0f,
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
/// 傲世种子迫击弹 - 弧线飞行, 落地爆炸并**引爆**范围年轮烙印 (系列引爆动作)
/// 引爆 ≥3 层时回调枪身触发「万棘狂涌」; 母弹分裂 3 枚子迫击弹
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
        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = 0.2f + Main.rand.NextFloat(-0.1f, 0.1f) }, Projectile.Center);
        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
            ACMWeaponBurst.ArrogantSylvan, scale: IsChild == 0 ? 1.5f : 1f, owner: Projectile.owner);
        WeaponVFX.AddScreenShake(Projectile.Center, IsChild == 0 ? 5f : 3f);

        if (Main.myPlayer == Projectile.owner) {
            // 爆炸场
            Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<ArrogantSylvanThornFieldExplosion>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner);

            // === 系列引爆动作: 炮爆波及范围内全部年轮烙印 ===
            int consumed = ArrogantSylvanBloom.DetonateArea(Projectile.GetSource_Death(), Projectile.Center,
                300f, Projectile.damage, 3f, Projectile.owner);
            // 万棘狂涌闭环 (修复原死机制): 引爆够多年轮 → 玩家绽出追踪荆棘风暴
            if (consumed >= 3)
                ArrogantDivineSylvanMusket.TriggerThornFury(Main.player[Projectile.owner],
                    Projectile.GetSource_Death(), consumed);

            // 母弹分裂成 3 枚子迫击弹
            if (IsChild == 0) {
                for (int k = -1; k <= 1; k++) {
                    float angle = MathHelper.ToRadians(45 * k) - MathHelper.PiOver2;
                    Vector2 splitVel = angle.ToRotationVector2() * 9f;
                    int p = Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, splitVel,
                        Type, Projectile.damage / 2, Projectile.knockBack, Projectile.owner);
                    if (p >= 0 && p < Main.maxProjectiles) Main.projectile[p].ai[0] = 1f;
                }
            }

            // 释放追踪藤蛇弹 (降量: 6/4, 可读性优先)
            int serpentCount = IsChild == 0 ? 6 : 4;
            for (int i = 0; i < serpentCount; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(7f, 7f);
                Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, vel,
                    ModContent.ProjectileType<ArrogantSylvanMusketSerpent>(),
                    Projectile.damage / 3, 2f, Projectile.owner);
            }
        }

        for (int i = 0; i < 22; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(10f, 10f);
            int dustType = i % 3 == 0 ? DustID.GoldFlame : DustID.JungleTorch;
            Dust d = Dust.NewDustPerfect(Projectile.Center, dustType,
                vel, 30, default, Main.rand.NextFloat(2f, 3.2f));
            d.noGravity = true;
        }
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
            ArrogantSylvanPalette.GoldBright * 0.7f, 0f,
            sg.Size() * 0.5f, pulse, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.WhiteHot * 0.45f, 0f,
            sg.Size() * 0.5f, pulse * 0.5f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 傲世荆棘领域爆炸 - 种子迫击弹爆炸范围场 (半径与炮爆引爆半径 300px 对齐)
/// </summary>
public class ArrogantSylvanThornFieldExplosion : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private const float MaxRadius = 300f;

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

    private float CurrentRadius() => Math.Min(Projectile.ai[0] * 14f, MaxRadius);

    public override void AI() {
        Projectile.ai[0]++;
        float radius = CurrentRadius();

        for (int i = 0; i < 6; i++) {
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

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        => VaultUtils.CircleIntersectsRectangle(Projectile.Center, CurrentRadius(), targetHitbox);

    public override bool PreDraw(ref Color lightColor) {
        float prog = 1f - Projectile.timeLeft / 55f;
        float alpha = ACMUtils.QuadOut(1f - prog) * 0.90f;
        float scale = MathHelper.SmoothStep(0f, 16f, ACMUtils.QuadOut(prog));

        // 炮爆年轮小环 (GrowthRing 低强度复用 — 落点即引爆区可读化)
        ArrogantSylvanFX.DrawGrowthRing(Projectile.Center, MaxRadius, ACMUtils.QuadOut(Math.Min(prog * 1.6f, 1f)),
            alpha * 0.45f, ringFreq: 6f);

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
            Color bColor = k % 2 == 0 ? ArrogantSylvanPalette.GoldBright : ArrogantSylvanPalette.JadeDeep;
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                bColor * (alpha * 0.80f), bAngle,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.14f, bLen), SpriteEffects.None, 0);
        }

        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.JadeBright * (alpha * 0.50f), 0f,
            sg.Size() * 0.5f, scale * 0.55f, SpriteEffects.None, 0);

        float flashAlpha = MathHelper.SmoothStep(1f, 0f, prog * 1.4f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.WhiteHot * (alpha * flashAlpha), 0f,
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
/// 傲世藤蛇弹 - 迫击炮爆炸/万棘狂涌释放的追踪弹幕 (共享节流索敌, 命中刻烙印)
/// </summary>
public class ArrogantSylvanMusketSerpent : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    private float _timer;
    private ref float TargetCache => ref Projectile.localAI[0];
    private ref float RescanTimer => ref Projectile.localAI[1];

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
            NPC target = ArrogantSylvanTargeting.UpdateTarget(Projectile, ref TargetCache, ref RescanTimer, 700f);
            if (target != null)
                ArrogantSylvanTargeting.SteerTowards(Projectile, target, 20f, 0.14f);
            else
                Projectile.velocity *= 1.01f;
        }

        Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
            -Projectile.velocity * 0.05f, 60, default, 1.4f);
        trail.noGravity = true;
        Lighting.AddLight(Projectile.Center, 0.15f, 0.5f, 0.15f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 300);
        target.AddBuff(BuffID.Venom, 180);
        ArrogantSylvanBrandNPC.AddStack(target);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;

        // 藤蛇弹金翠双层 ribbon (§B.1)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 8f,
            outerColor: ArrogantSylvanPalette.TrailOuter, innerColor: ArrogantSylvanPalette.TrailInner,
            uvScroll: -(float)Main.timeForVisualEffects * 0.05f);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.50f;
            Color c = Color.Lerp(ArrogantSylvanPalette.GoldBright, ArrogantSylvanPalette.JadeDeep,
                (float)i / ProjectileID.Sets.TrailCacheLength[Type]);
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, c * a, Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.30f, 0.07f), SpriteEffects.None, 0);
        }

        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.JadeBright, Projectile.rotation,
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
