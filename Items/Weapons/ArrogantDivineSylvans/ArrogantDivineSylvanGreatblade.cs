using AncientChineseMythology.Celestias.Boss.Dazhengs.Items;
using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Weapons.DivineWoods;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.ArrogantDivineSylvans;

/// <summary>
/// 傲世神木·斩天巨刃 (系列旗舰) - 三连斩循环: 横斩→上挑→天崩下劈
/// 手感重锻: 回拉前摇 (30%) → poly(12) 爆发斩 (30%, 角行程集中在前几帧) → 过冲收招 (40%)
/// 横斩/上挑命中刻下「年轮烙印」; 天崩下劈命中/落点范围**引爆**烙印 (金翠年轮绽放)
/// </summary>
public class ArrogantDivineSylvanGreatblade : ModItem
{
    private int attackType;

    public override void SetDefaults() {
        Item.damage = 2300;
        Item.crit = 32;
        Item.DamageType = DamageClass.Melee;
        Item.width = 80;
        Item.height = 80;
        Item.useTime = 26;
        Item.useAnimation = 26;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 14f;
        Item.value = Item.buyPrice(gold: 500);
        Item.rare = ItemRarityID.Purple;
        Item.autoReuse = true;
        Item.noUseGraphic = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<ArrogantSylvanGreatbladeSwing>();
        Item.shootSpeed = 4f;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI, attackType);
        attackType = (attackType + 1) % 3;
        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<ArrogantDivineSylvan>(15)
            .AddIngredient<DivineWoodGreatblade>()
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
}

/// <summary>
/// 傲世斩天挥砍 - 三段式循环连击 (0=横斩 1=上挑 2=天崩下劈)
/// 波形解剖: Prepare 回拉 -18% 行程 (读得懂的蓄势) → Execute poly(12) ease-out
/// (几乎全部角行程在前 2-3 帧, 斩击是"一记", 不是"一波") → Unwind 过冲 5% 后回正
/// </summary>
public class ArrogantSylvanGreatbladeSwing : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/ArrogantDivineSylvans/ArrogantDivineSylvanGreatblade";

    private const float SWING_RANGE = MathF.PI * 1.65f;
    private const float BACKSWING = 0.18f;   // 回拉行程占比
    private const float PREP_FRAC = 0.30f;
    private const float EXEC_FRAC = 0.30f;

    private enum Stage { Prepare, Execute, Unwind }

    private ref float Timer => ref Projectile.ai[1];
    private ref float InitAngle => ref Projectile.ai[2];
    private ref float RawProgress => ref Projectile.localAI[0];
    private int AttackType => (int)Projectile.ai[0]; // 0/1/2
    private int SwingDir => AttackType switch { 0 => 1, 1 => -1, _ => 1 };

    private Stage CurrentStage {
        get => (Stage)Projectile.localAI[1];
        set { Projectile.localAI[1] = (float)value; Timer = 0f; }
    }

    private bool _waveFired;
    private Player Owner => Main.player[Projectile.owner];

    public override void SetStaticDefaults() {
        ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 18;
    }

    public override void SetDefaults() {
        Projectile.width = 80;
        Projectile.height = 80;
        Projectile.friendly = true;
        Projectile.timeLeft = 10000;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
        Projectile.ownerHitCheck = true;
        Projectile.DamageType = DamageClass.Melee;
    }

    public override void OnSpawn(IEntitySource source) {
        Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
        float toMouse = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
        int dir = Projectile.spriteDirection * SwingDir;

        if (dir > 0) {
            toMouse = MathHelper.Clamp(toMouse, -MathF.PI / 2.8f, MathF.PI / 5f);
            InitAngle = toMouse - SWING_RANGE * 0.55f;
        }
        else {
            if (toMouse < 0) toMouse += MathHelper.TwoPi;
            toMouse = MathHelper.Clamp(toMouse, MathF.PI * 0.78f, MathF.PI * 1.4f);
            InitAngle = toMouse + SWING_RANGE * 0.55f;
        }
    }

    public override void AI() {
        if (!Owner.active || Owner.dead) { Projectile.Kill(); return; }
        Owner.itemAnimation = 2;
        Owner.itemTime = 2;

        float totalTime = Owner.itemAnimationMax;
        float prepEnd = totalTime * PREP_FRAC;
        float execDur = totalTime * EXEC_FRAC;
        float unwindDur = totalTime * (1f - PREP_FRAC - EXEC_FRAC);
        int dir = Projectile.spriteDirection * SwingDir;

        switch (CurrentStage) {
            case Stage.Prepare: {
                // 回拉蓄势: quad in-out 到 -18% 行程 (力量住在前摇里)
                float t = Math.Min(Timer / prepEnd, 1f);
                RawProgress = -BACKSWING * SWING_RANGE * ACMUtils.QuadInOut(t);

                // 汇聚金尘: 从刀尖外侧被吸向刀尖 (蓄力语法: 向心流)
                if (Main.rand.NextBool(2)) {
                    Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2()
                                  * Projectile.Size.Length() * Projectile.scale * 0.7f;
                    Vector2 from = tip + Main.rand.NextVector2CircularEdge(70f, 70f);
                    Dust d = Dust.NewDustPerfect(from, DustID.GoldFlame, (tip - from) * 0.14f, 60, default, 1.4f);
                    d.noGravity = true;
                }

                if (Timer >= prepEnd) {
                    // 爆发帧: 分层音 (低频重击 + 高频破空), pitch 随机
                    SoundEngine.PlaySound(SoundID.Item71 with {
                        Pitch = -0.35f + Main.rand.NextFloat(-0.1f, 0.1f), Volume = 1.15f
                    }, Owner.position);
                    SoundEngine.PlaySound(SoundID.Item1 with {
                        Pitch = 0.25f + Main.rand.NextFloat(-0.12f, 0.12f), Volume = 0.9f
                    }, Owner.position);
                    CurrentStage = Stage.Execute;
                }
                break;
            }

            case Stage.Execute: {
                // poly(12) ease-out: 角行程几乎全部在前 2-3 帧 — "一记斩击"
                float t = Math.Min(Timer / execDur, 1f);
                float snap = 1f - MathF.Pow(1f - t, 12f);
                RawProgress = MathHelper.Lerp(-BACKSWING * SWING_RANGE, SWING_RANGE, snap);

                if (!_waveFired && Timer >= execDur * 0.10f) {
                    _waveFired = true;
                    FireAttackProjectiles();
                }

                if (Timer >= execDur) CurrentStage = Stage.Unwind;
                break;
            }

            case Stage.Unwind: {
                // 过冲 5% 后回正 (0→1→0 bump), 让身体"收得住"
                float t = Math.Min(Timer / unwindDur, 1f);
                RawProgress = SWING_RANGE * (1f + 0.05f * MathF.Sin(t * MathF.PI));
                if (Timer >= unwindDur) Projectile.Kill();
                break;
            }
        }

        Projectile.rotation = InitAngle + dir * RawProgress;
        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
            Projectile.rotation - MathHelper.PiOver2);
        Vector2 arm = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full,
            Projectile.rotation - MathHelper.PiOver2);
        arm.Y += Owner.gfxOffY;
        Projectile.Center = arm;
        Projectile.scale = 1.5f * Owner.GetAdjustedItemScale(Owner.HeldItem);
        Owner.heldProj = Projectile.whoAmI;
        Timer++;

        // 爆发斩持续粒子 (只在 Execute — 速度门控的"外衣")
        if (CurrentStage == Stage.Execute) {
            Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2()
                          * Projectile.Size.Length() * Projectile.scale * 0.7f;
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(tip + Main.rand.NextVector2Circular(10, 10),
                    i == 0 ? DustID.GoldFlame : DustID.JungleTorch,
                    Main.rand.NextVector2Circular(4f, 4f), 40, default, 2.2f);
                d.noGravity = true;
            }
        }
    }

    private void FireAttackProjectiles() {
        if (Projectile.owner != Main.myPlayer) return;
        Vector2 wd = Owner.SafeDirectionTo(Main.MouseWorld);

        switch (AttackType) {
            case 0: // 横斩 → 巨型藤蔓弧光
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem),
                    Owner.Center, wd * 22f,
                    ModContent.ProjectileType<ArrogantSylvanVineWave>(),
                    (int)(Projectile.damage * 1.5f),
                    Projectile.knockBack * 0.8f, Owner.whoAmI);
                SoundEngine.PlaySound(SoundID.Item85 with { Pitch = 0.2f + Main.rand.NextFloat(-0.1f, 0.1f), Volume = 1.2f }, Owner.position);
                break;

            case 1: // 上挑 → 三道扇形刀波
                for (int i = -1; i <= 1; i++) {
                    Vector2 fanVel = wd.RotatedBy(i * MathHelper.ToRadians(18)) * 20f;
                    Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem),
                        Owner.Center, fanVel,
                        ModContent.ProjectileType<ArrogantSylvanVineWave>(),
                        (int)(Projectile.damage * 1.0f),
                        Projectile.knockBack * 0.5f, Owner.whoAmI);
                }
                SoundEngine.PlaySound(SoundID.Item85 with { Pitch = 0.6f + Main.rand.NextFloat(-0.1f, 0.1f), Volume = 1.3f }, Owner.position);
                break;

            case 2: // 天崩下劈 → 地裂震爆 (系列大招: 落点范围引爆全部年轮烙印)
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem),
                    Owner.Center + wd * 60, Vector2.Zero,
                    ModContent.ProjectileType<ArrogantSylvanEarthquake>(),
                    (int)(Projectile.damage * 2.0f),
                    Projectile.knockBack * 1.5f, Owner.whoAmI);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.5f + Main.rand.NextFloat(-0.08f, 0.08f), Volume = 1.4f }, Owner.position);
                SoundEngine.PlaySound(SoundID.Item70 with { Pitch = -0.2f, Volume = 0.8f }, Owner.position);
                // 下劈地裂 set-piece 命中演出 + 单入口震屏 (爆炸预算 6)
                ACMWeaponBurst.Spawn(Owner.GetSource_ItemUse(Owner.HeldItem), Owner.Center + wd * 60,
                    ACMWeaponBurst.ArrogantSylvan, scale: 2f, owner: Owner.whoAmI);
                WeaponVFX.AddScreenShake(Owner.Center + wd * 60, 6f);
                break;
        }
    }

    public override bool? CanDamage() => CurrentStage == Stage.Execute ? null : false;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        Vector2 s = Owner.MountedCenter;
        Vector2 e = s + Projectile.rotation.ToRotationVector2()
                    * Projectile.Size.Length() * Projectile.scale * 1.2f;
        float col = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            s, e, 30f * Projectile.scale, ref col);
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
        modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        if (AttackType == 2) modifiers.FinalDamage *= 1.3f;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 900);
        target.AddBuff(BuffID.Venom, 600);
        for (int i = 0; i < 14; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, i % 2 == 0 ? DustID.JungleTorch : DustID.GoldFlame,
                Main.rand.NextVector2Circular(9f, 9f), 40, default, 2.6f);
            d.noGravity = true;
        }
        WeaponVFX.AddScreenShake(target.Center, 2f);

        if (AttackType == 2) {
            // 天崩下劈本体命中 = 直接引爆该目标烙印
            ArrogantSylvanBloom.Detonate(Projectile.GetSource_OnHit(target), target,
                Projectile.damage, 4f, Projectile.owner);
        }
        else {
            // 横斩/上挑 = 浇灌 (刻下年轮)
            ArrogantSylvanBrandNPC.AddStack(target);
        }
        ArrogantSylvanFX.HitBurstThrottled(Projectile.GetSource_OnHit(target), target.Center,
            AttackType == 2 ? 1.4f : 1f, Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        int dir = Projectile.spriteDirection * SwingDir;
        float rotOff = dir > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi;

        // 金翠双色刀尖弧光 ribbon — 仅爆发斩期间 (速度门控, 残影是"快"的外衣不是常亮噪声)
        if (CurrentStage == Stage.Execute) {
            int cache = ProjectileID.Sets.TrailCacheLength[Type];
            float tipLen = Projectile.Size.Length() * Projectile.scale * 0.85f;
            var arcPts = new List<Vector2>(cache);
            for (int i = 0; i < cache; i++) {
                float r = Projectile.oldRot[i];
                if (r == 0f && i > 0) continue;
                arcPts.Add(Projectile.Center + r.ToRotationVector2() * tipLen);
            }
            if (arcPts.Count >= 2)
                WeaponVFX.DrawRibbonTrail(arcPts.ToArray(), baseWidth: 26f,
                    outerColor: ArrogantSylvanPalette.TrailOuter, innerColor: ArrogantSylvanPalette.TrailInner,
                    uvScroll: -(float)Main.timeForVisualEffects * 0.02f);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        // 回拉蓄势的刀尖蓄能光点 (小而聚: 与爆发的大开大合形成对比)
        if (CurrentStage == Stage.Prepare) {
            float t = Math.Min(Timer / MathF.Max(Owner.itemAnimationMax * PREP_FRAC, 1f), 1f);
            Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2()
                          * Projectile.Size.Length() * Projectile.scale * 0.7f;
            Texture2D sgPrep = ACMAsset.SoftGlow;
            sb.Draw(sgPrep, tip - Main.screenPosition, null,
                ArrogantSylvanPalette.GoldBright * (0.35f + 0.45f * t * t), 0f,
                sgPrep.Size() * 0.5f, 0.28f + 0.20f * t, SpriteEffects.None, 0);
        }

        if (CurrentStage == Stage.Execute) {
            Texture2D wave = ACMAsset.GlaciateWave;
            for (int i = 1; i < 16 && i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                float a = (1f - i / 16f) * 0.75f;
                float rot = Projectile.oldRot[i] + rotOff;
                sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                    ArrogantSylvanPalette.GoldDark * a, rot,
                    wave.Size() * 0.5f,
                    Projectile.scale * 0.65f, SpriteEffects.None, 0);
                sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                    ArrogantSylvanPalette.JadeBright * (a * 0.55f), rot + 0.08f,
                    wave.Size() * 0.5f,
                    Projectile.scale * 0.42f, SpriteEffects.None, 0);
                sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                    ArrogantSylvanPalette.WhiteHot * (a * 0.25f), rot + 0.04f,
                    wave.Size() * 0.5f,
                    Projectile.scale * 0.28f, SpriteEffects.None, 0);
            }

            float pulse = 0.85f + 0.25f * MathF.Sin((float)Main.timeForVisualEffects * 0.25f);
            Texture2D sg = ACMAsset.SoftGlow;
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                ArrogantSylvanPalette.JadeBright * 0.5f * pulse, Projectile.rotation + rotOff,
                sg.Size() * 0.5f,
                Projectile.scale * 2.8f, SpriteEffects.None, 0);

            Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2()
                          * Projectile.Size.Length() * Projectile.scale * 0.65f;
            Texture2D sparkle = ACMAsset.Sparkle;
            sb.Draw(sparkle, tip - Main.screenPosition, null,
                ArrogantSylvanPalette.GoldBright * 0.70f,
                (float)Main.timeForVisualEffects * 0.08f,
                sparkle.Size() * 0.5f,
                Projectile.scale * 0.80f, SpriteEffects.None, 0);

            if (AttackType == 2) {
                Texture2D star = ACMAsset.BlankStar;
                sb.Draw(star, Projectile.Center - Main.screenPosition, null,
                    ArrogantSylvanPalette.GoldBright * 0.40f * pulse,
                    (float)Main.timeForVisualEffects * 0.03f,
                    star.Size() * 0.5f,
                    Projectile.scale * 1.2f, SpriteEffects.None, 0);
            }
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
        SpriteEffects fx = dir < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Vector2 origin = dir > 0
            ? new Vector2(0, tex.Height) : new Vector2(tex.Width, tex.Height);
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            lightColor, Projectile.rotation + rotOff, origin,
            Projectile.scale, fx, 0);
        return false;
    }
}

/// <summary>
/// 傲世藤蔓弧波 - 巨型金绿色能量斩波 (横斩/上挑派生)
/// 命中刻下年轮烙印 (系列"浇灌"动作)
/// </summary>
public class ArrogantSylvanVineWave : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/GlaciateWave";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 20;
    }

    public override void SetDefaults() {
        Projectile.width = 120;
        Projectile.height = 60;
        Projectile.friendly = true;
        Projectile.tileCollide = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 65;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.velocity *= 0.975f;

        float life = 1f - Projectile.timeLeft / 65f;
        Lighting.AddLight(Projectile.Center, 0.6f * (1f - life), 1.5f * (1f - life), 0.4f * (1f - life));

        for (int i = 0; i < 2; i++) {
            Dust d = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(30, 30),
                DustID.JungleTorch, -Projectile.velocity * 0.2f, 40, default, 2.4f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 900);
        target.AddBuff(BuffID.Venom, 600);
        target.velocity *= 0.3f;

        ArrogantSylvanBrandNPC.AddStack(target);
        for (int i = 0; i < 16; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, i % 2 == 0 ? DustID.JungleTorch : DustID.GoldFlame,
                Main.rand.NextVector2Circular(9f, 9f), 30, default, 2.6f);
            d.noGravity = true;
        }
        ArrogantSylvanFX.HitBurstThrottled(Projectile.GetSource_OnHit(target), target.Center, 1f, Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D tex = ACMAsset.GlaciateWave;
        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D lsh = ACMAsset.LightShot;

        float life = 1f - Projectile.timeLeft / 65f;
        float scaleX = MathHelper.Lerp(2.4f, 0.8f, ACMUtils.QuadIn(life));
        float scaleY = MathHelper.Lerp(0.75f, 0.25f, ACMUtils.QuadIn(life));
        float alpha = ACMUtils.QuadOut(1f - life) * 0.95f;

        // 金翠双层弧波拖尾 (§B.1)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 22f,
            outerColor: ArrogantSylvanPalette.TrailOuter, innerColor: ArrogantSylvanPalette.TrailInner,
            uvScroll: -(float)Main.timeForVisualEffects * 0.03f);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.6f * alpha;
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, ArrogantSylvanPalette.GoldDark * a, Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.55f + i * 0.015f, 0.22f), SpriteEffects.None, 0);
        }

        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.GoldDark * alpha, Projectile.rotation,
            tex.Size() * 0.5f,
            new Vector2(scaleX, scaleY), SpriteEffects.None, 0);
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.JadeBright * (alpha * 0.6f), Projectile.rotation + 0.04f,
            tex.Size() * 0.5f,
            new Vector2(scaleX * 0.8f, scaleY * 0.75f), SpriteEffects.None, 0);
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.WhiteHot * (alpha * 0.30f), Projectile.rotation,
            tex.Size() * 0.5f,
            new Vector2(scaleX * 0.5f, scaleY * 0.45f), SpriteEffects.None, 0);

        Vector2 front = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 60f;
        sb.Draw(sg, front - Main.screenPosition, null,
            ArrogantSylvanPalette.WhiteHot * alpha * 0.80f, 0f,
            sg.Size() * 0.5f,
            scaleY * 2.5f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 傲世地震裂爆 - 天崩下劈的大地震爆 (系列大招载体)
/// 半径封顶 380px (伤害判定与视觉对齐); 落点范围引爆全部年轮烙印
/// </summary>
public class ArrogantSylvanEarthquake : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private const float MaxRadius = 380f;

    private ref float Timer => ref Projectile.ai[0];

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 70;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override bool ShouldUpdatePosition() => false;

    private float CurrentRadius() => Math.Min(Timer * 18f, MaxRadius);

    public override void AI() {
        Timer++;

        // 第 2 帧引爆落点范围内全部烙印 (等地裂视觉先出现一瞬, 因果可读)
        if (Timer == 2f && Projectile.owner == Main.myPlayer) {
            ArrogantSylvanBloom.DetonateArea(Projectile.GetSource_FromThis(), Projectile.Center,
                MaxRadius, Projectile.damage, 4f, Projectile.owner);
        }

        float radius = CurrentRadius();
        for (int i = 0; i < 8; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.3f, radius);
            Dust d = Dust.NewDustPerfect(pos, i % 3 == 0 ? DustID.GoldFlame : DustID.JungleTorch,
                Main.rand.NextVector2Circular(2f, 2f), 40, default, 2.4f);
            d.noGravity = true;
        }

        if (Timer < 20) {
            for (int i = 0; i < 5; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 18f);
                vel.Y -= 5f;
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Mud, vel, 80, default, 2f);
                d.noGravity = false;
            }
        }

        Lighting.AddLight(Projectile.Center, 0.8f, 2f, 0.6f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 900);
        target.AddBuff(BuffID.Venom, 600);
        target.velocity *= 0.2f;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        => VaultUtils.CircleIntersectsRectangle(Projectile.Center, CurrentRadius(), targetHitbox);

    /// <summary>下劈地裂签名 set-piece: ArenaRunic 屏幕空间根纹法阵 (金翠双色, 自起爆点扩张)。</summary>
    private void DrawRootRuneDecal(float prog) {
        if (Main.dedServ)
            return;
        Effect fx = ACMShaders.ArenaRunic;
        if (fx == null)
            return;

        // 钟形包络: 起爆快现 → 收尾淡出
        float env = MathHelper.Clamp(MathF.Sin(prog * MathF.PI), 0f, 1f);
        float intensity = env * 0.9f;
        if (intensity <= 0.01f)
            return;

        float worldRadius = MathHelper.Lerp(70f, MaxRadius * 0.9f, ACMUtils.QuadOut(prog));
        ACMShaders.WorldDecalParams(Projectile.Center, worldRadius, out Vector2 uv, out float radiusFrac, out float aspect);

        fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
        fx.Parameters["uCenter"]?.SetValue(uv);
        fx.Parameters["uRadius"]?.SetValue(radiusFrac);
        fx.Parameters["uIntensity"]?.SetValue(intensity);
        fx.Parameters["uAspect"]?.SetValue(aspect);
        fx.Parameters["uColorPrimary"]?.SetValue(new Color(230, 185, 70).ToVector4());   // 金
        fx.Parameters["uColorSecondary"]?.SetValue(new Color(60, 185, 95).ToVector4());  // 翠
        fx.Parameters["uRuneFreq"]?.SetValue(12f);
        fx.Parameters["uMode"]?.SetValue(0f);
        fx.Parameters["uShape"]?.SetValue(0f);

        ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
    }

    public override bool PreDraw(ref Color lightColor) {
        float prog = 1f - Projectile.timeLeft / 70f;
        float alpha = ACMUtils.QuadOut(1f - prog) * 0.92f;
        float scale = MathHelper.SmoothStep(0f, 20f, ACMUtils.QuadOut(prog));

        // === 地裂根纹法阵 set-piece (ArenaRunic 屏幕空间地纹, 金翠双色) ===
        DrawRootRuneDecal(prog);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D sparkle = ACMAsset.Sparkle;
        Texture2D star = ACMAsset.BlankStar;

        for (int k = 0; k < 12; k++) {
            float bAngle = k * MathF.PI / 6f + Timer * 0.025f;
            bool major = (k % 3 == 0);
            Color bColor = major ? ArrogantSylvanPalette.GoldBright : ArrogantSylvanPalette.JadeDeep;
            float bLen = major ? scale * 0.75f : scale * 0.45f;
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                bColor * (alpha * 0.80f), bAngle,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.18f, bLen), SpriteEffects.None, 0);
        }

        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.JadeBright * (alpha * 0.50f), 0f,
            sg.Size() * 0.5f,
            scale * 0.60f, SpriteEffects.None, 0);

        float flashAlpha = MathHelper.SmoothStep(1.2f, 0f, prog * 1.4f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.WhiteHot * (alpha * flashAlpha), 0f,
            sg.Size() * 0.5f,
            scale * 0.25f, SpriteEffects.None, 0);

        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.GoldBright * (alpha * 0.55f),
            Timer * 0.10f,
            star.Size() * 0.5f,
            scale * 0.18f, SpriteEffects.None, 0);
        sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.JadeBright * (alpha * 0.45f),
            -Timer * 0.06f,
            sparkle.Size() * 0.5f,
            scale * 0.22f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
