using AncientChineseMythology.Celestias.Boss.Dazhengs.Items;
using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Weapons.DivineWoods;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.ArrogantDivineSylvans;

/// <summary>
/// 傲世神木·落叶风暴 (系列次旗舰) - 螺旋风暴回旋镖
/// 掷出→落点展开螺旋风暴 (杀伤区=**环带**, 用 Vortex 着色器画出的藤叶风暴环即判定区)
/// 按住维持风暴 (每 45 帧环带脉冲甩出叶片), 松开→内爆坍缩→**范围引爆年轮烙印**→高速回收
/// 回程伤害×2, 接住时冲击反馈; 风暴/回程命中刻下烙印
/// </summary>
public class ArrogantDivineSylvanChakram : ModItem
{
    public override void SetDefaults() {
        Item.damage = 1500;
        Item.crit = 30;
        Item.DamageType = DamageClass.Melee;
        Item.width = 44;
        Item.height = 44;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 12f;
        Item.value = Item.buyPrice(gold: 500);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item1;
        Item.channel = true;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<ArrogantSylvanChakramProj>();
        Item.shootSpeed = 32f;
    }

    public override bool CanUseItem(Player player) {
        return player.ownedProjectileCounts[ModContent.ProjectileType<ArrogantSylvanChakramProj>()] < 1;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<ArrogantDivineSylvan>(15)
            .AddIngredient<DivineWoodGyratingLeaf>()
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
}

/// <summary>
/// 傲世旋叶弹幕 - 投射(反拍前摇+爆发)→螺旋风暴(环带判定)→内爆坍缩(引爆烙印)→高速回收
/// 判定与视觉严格对齐: 风暴期杀伤区=环带(±75px), 由 ArrogantSylvanVortex 着色器可视化
/// </summary>
public class ArrogantSylvanChakramProj : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/ArrogantDivineSylvans/ArrogantDivineSylvanChakram";

    // Phase: 0 = Launching, 1 = Spiraling, 2 = Imploding, 3 = Recalling
    private ref float Phase => ref Projectile.ai[0];
    private ref float Timer => ref Projectile.ai[1];
    private ref float SpiralAngle => ref Projectile.localAI[1];

    // ---- 投射 (反拍→爆发: 出手先泄力 6 帧再猛然弹射, 速度对比卖"快") ----
    private const int ReelFrames = 6;
    private const int LaunchDuration = 22;
    private const float LaunchSpeed = 38f;

    // ---- 螺旋风暴 ----
    private const float SpiralStartRadius = 40f;
    private const float SpiralMaxRadius = 260f;
    private const float SpiralExpandRate = 3.5f;
    private const float SpiralAngularSpeed = 0.28f;
    private const int MaxSpiralDuration = 240;
    private const float BandHalfWidth = 75f;     // 环带半宽 = 伤害判定半宽 (与 Vortex 视觉一致)
    private const int PulseInterval = 45;        // 环带脉冲节奏 (万木裁决改造为节奏阀)

    // ---- 内爆坍缩 ----
    private const float ImplodeContractRate = 12f;
    private const float ImplodeAngularSpeed = 0.50f;

    // ---- 回收 ----
    private const float RecallAccel = 3.0f;
    private const float MaxRecallSpeed = 50f;
    private const float CatchRadius = 50f;

    private Vector2 _stormCenter;
    private float _spiralRadius;
    private int _spiralTimer;
    private bool _caughtBurst;
    private int _collapseFlash;   // 内爆坍缩径向泛光计时 (纯视觉)
    private float _pulseFlash;    // 环带脉冲增亮包络 (纯视觉)

    private bool IsSpiraling => Phase >= 1f && Phase < 2f;
    private bool IsImploding => Phase >= 2f && Phase < 3f;
    private bool IsRecalling => Phase >= 3f;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[Type] = 24;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults() {
        Projectile.width = 44;
        Projectile.height = 44;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 900;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 6;
    }

    public override void AI() {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead) { Projectile.Kill(); return; }

        Timer++;
        if (_collapseFlash > 0) _collapseFlash--;
        if (_pulseFlash > 0.01f) _pulseFlash *= 0.90f;

        // 旋转速度: 风暴阶段极速, 回收阶段猛烈
        float rotSpeed = IsSpiraling ? 0.55f : (IsImploding ? 0.75f : (IsRecalling ? 0.70f : 0.40f));
        Projectile.rotation += rotSpeed;

        switch ((int)Phase) {
            case 0: HandleLaunching(owner); break;
            case 1: HandleSpiraling(owner); break;
            case 2: HandleImploding(owner); break;
            default: HandleRecalling(owner); break;
        }

        // 粒子: 风暴阶段密集, 其余正常
        int dustCount = IsSpiraling ? 3 : (IsImploding ? 4 : 2);
        for (int i = 0; i < dustCount; i++) {
            Dust d = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(16, 16),
                DustID.JungleTorch,
                IsSpiraling ? (-Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(2f, 2f))
                            : Projectile.velocity * 0.12f,
                40, default, Main.rand.NextFloat(1.5f, 2.4f));
            d.noGravity = true;
        }

        // 环带内旋转粒子 (只在环带里撒 — 粒子提示与判定同域)
        if (IsSpiraling && Timer % 3 == 0) {
            float pAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            float pr = _spiralRadius + Main.rand.NextFloat(-BandHalfWidth, BandHalfWidth) * 0.8f;
            Vector2 pPos = _stormCenter + pAngle.ToRotationVector2() * MathF.Max(pr, 10f);
            Dust rd = Dust.NewDustPerfect(pPos,
                Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.GrassBlades,
                pAngle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 4f,
                60, default, 2f);
            rd.noGravity = true;
        }

        // 内爆时向心收缩粒子 (向心流 = 坍缩前摇语法)
        if (IsImploding && Timer % 2 == 0) {
            float randAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 spawnPos = _stormCenter + randAngle.ToRotationVector2() * (_spiralRadius + 40f);
            Vector2 inVel = (_stormCenter - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(6f, 12f);
            Dust id = Dust.NewDustPerfect(spawnPos, DustID.JungleTorch, inVel, 30, default, 2.5f);
            id.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.40f, 0.95f, 0.35f);
        if (IsSpiraling || IsImploding)
            Lighting.AddLight(_stormCenter, 0.3f, 0.8f, 0.25f);
    }

    private void HandleLaunching(Player owner) {
        if (owner.channel)
            owner.itemAnimation = 2;

        // 反拍前摇: 出手先急泄力 (旋镖"绷住"), 第 6 帧猛然弹射 — 速度是对比出来的
        if (Timer <= ReelFrames) {
            Projectile.velocity *= 0.80f;
            if (Timer == ReelFrames) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * LaunchSpeed;
                SoundEngine.PlaySound(SoundID.Item71 with {
                    Volume = 1.1f, Pitch = 0.15f + Main.rand.NextFloat(-0.1f, 0.1f)
                }, Projectile.Center);
                Projectile.netUpdate = true;
            }
        }
        else {
            Projectile.velocity *= 0.955f;
        }

        if (Timer >= ReelFrames + LaunchDuration || (Timer > ReelFrames && Projectile.velocity.Length() < 5f)) {
            // 无论是否按住, 都进入螺旋。按住 = 维持风暴, 松手 = 风暴后自动回收
            Phase = 1f;
            Timer = 0;
            _spiralTimer = 0;
            _stormCenter = Projectile.Center;
            _spiralRadius = SpiralStartRadius;
            SpiralAngle = Projectile.velocity.ToRotation();
            Projectile.netUpdate = true;
            SoundEngine.PlaySound(SoundID.Item66 with { Volume = 0.9f, Pitch = 0.2f }, Projectile.Center);
        }
    }

    private void HandleSpiraling(Player owner) {
        if (owner.channel)
            owner.itemAnimation = 2;

        _spiralTimer++;

        // 高速旋转 + 扩展半径
        SpiralAngle += SpiralAngularSpeed;
        if (_spiralRadius < SpiralMaxRadius)
            _spiralRadius += SpiralExpandRate;

        Vector2 newPos = _stormCenter + new Vector2(MathF.Cos(SpiralAngle), MathF.Sin(SpiralAngle)) * _spiralRadius;
        Projectile.velocity = newPos - Projectile.Center;
        Projectile.Center = newPos;

        // 环带脉冲 (每 45 帧): 8 片叶沿切向甩出 + 环带增亮 + 分层音 — 稳定的节奏阀
        if (_spiralTimer % PulseInterval == 0) {
            _pulseFlash = 1f;
            SoundEngine.PlaySound(SoundID.Item17 with {
                Volume = 0.9f, Pitch = 0.25f + Main.rand.NextFloat(-0.1f, 0.1f)
            }, _stormCenter);
            if (Projectile.owner == Main.myPlayer) {
                for (int i = 0; i < 8; i++) {
                    float ang = MathHelper.TwoPi * i / 8f + SpiralAngle;
                    Vector2 basePos = _stormCenter + ang.ToRotationVector2() * _spiralRadius;
                    Vector2 leafVel = ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(7f, 10f)
                                      + ang.ToRotationVector2() * 2.5f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), basePos,
                        leafVel, ModContent.ProjectileType<ArrogantSylvanWhirlLeaf>(),
                        Projectile.damage / 4, 1.5f, Projectile.owner);
                }
            }
        }

        // 松手或超时 → 内爆坍缩
        if (!owner.channel || _spiralTimer >= MaxSpiralDuration) {
            Phase = 2f;
            Timer = 0;
            Projectile.netUpdate = true;
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = 0.6f }, _stormCenter);
        }
    }

    private void HandleImploding(Player owner) {
        // 急速收缩半径 + 加速旋转
        SpiralAngle += ImplodeAngularSpeed;
        _spiralRadius -= ImplodeContractRate;

        if (_spiralRadius <= 0f) {
            // 坍缩完成 → 大招时刻: 中心爆发 + 范围引爆全部年轮烙印 + 进入回收
            _spiralRadius = 0f;
            Phase = 3f;
            Timer = 0;
            _collapseFlash = 20;
            Projectile.netUpdate = true;

            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1.2f, Pitch = -0.3f + Main.rand.NextFloat(-0.08f, 0.08f) }, _stormCenter);
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 1f, Pitch = 0.4f }, _stormCenter);

            if (Projectile.owner == Main.myPlayer) {
                // 范围引爆烙印 (整个风暴曾覆盖的区域)
                ArrogantSylvanBloom.DetonateArea(Projectile.GetSource_FromThis(), _stormCenter,
                    SpiralMaxRadius + BandHalfWidth + 60f, Projectile.damage, 3f, Projectile.owner);

                // 内爆冲击波: 8 叶 + 6 裁决花瓣自中心炸开
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi * i / 8;
                    Vector2 leafVel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 13f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), _stormCenter,
                        leafVel, ModContent.ProjectileType<ArrogantSylvanWhirlLeaf>(),
                        Projectile.damage / 3, 2.5f, Projectile.owner);
                }
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6 + 0.3f;
                    Vector2 petalVel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 10f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), _stormCenter,
                        petalVel, ModContent.ProjectileType<ArrogantSylvanVerdictPetal>(),
                        Projectile.damage / 3, 3f, Projectile.owner);
                }
            }
            WeaponVFX.AddScreenShake(_stormCenter, 6f);

            for (int i = 0; i < 24; i++) {
                Dust d = Dust.NewDustPerfect(_stormCenter, i % 3 == 0 ? DustID.GoldFlame : DustID.JungleTorch,
                    Main.rand.NextVector2Circular(12f, 12f), 20, default, 2.8f);
                d.noGravity = true;
            }

            Projectile.Center = _stormCenter;
            Projectile.velocity = Vector2.Zero;
            return;
        }

        // 继续螺旋
        Vector2 newPos = _stormCenter + new Vector2(MathF.Cos(SpiralAngle), MathF.Sin(SpiralAngle)) * _spiralRadius;
        Projectile.velocity = newPos - Projectile.Center;
        Projectile.Center = newPos;
    }

    private void HandleRecalling(Player owner) {
        Vector2 toPlayer = owner.Center - Projectile.Center;
        float dist = toPlayer.Length();
        Vector2 dir = toPlayer.SafeNormalize(Vector2.Zero);

        // 极速加速回收
        Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * MaxRecallSpeed, 0.18f);
        if (Projectile.velocity.Length() < MaxRecallSpeed)
            Projectile.velocity += dir * RecallAccel;

        // 接住
        if (dist < CatchRadius) {
            if (!_caughtBurst) {
                _caughtBurst = true;

                // 接住命中演出 (金翠) + 震屏 (预算内单入口)
                ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), owner.Center,
                    ACMWeaponBurst.ArrogantSylvan, scale: 1.6f, owner: Projectile.owner);
                WeaponVFX.AddScreenShake(owner.Center, 4f);

                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 1.2f, Pitch = 0.5f + Main.rand.NextFloat(-0.1f, 0.1f) }, owner.Center);
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.7f, Pitch = -0.2f }, owner.Center);

                for (int i = 0; i < 20; i++) {
                    Dust d = Dust.NewDustPerfect(owner.Center, i % 2 == 0 ? DustID.GoldFlame : DustID.JungleTorch,
                        Main.rand.NextVector2Circular(11f, 11f), 20, default, 2.6f);
                    d.noGravity = true;
                }
            }
            Projectile.Kill();
            return;
        }
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        // 螺旋风暴/内爆时: 杀伤区 = 环带 (与 Vortex 着色器画出的藤叶风暴环严格对齐)
        if (IsSpiraling || IsImploding) {
            float inner = MathF.Max(_spiralRadius - BandHalfWidth, 0f);
            float outer = _spiralRadius + BandHalfWidth;
            Vector2 c = _stormCenter;

            float closestX = MathHelper.Clamp(c.X, targetHitbox.Left, targetHitbox.Right);
            float closestY = MathHelper.Clamp(c.Y, targetHitbox.Top, targetHitbox.Bottom);
            float dMin2 = (c.X - closestX) * (c.X - closestX) + (c.Y - closestY) * (c.Y - closestY);
            if (dMin2 > outer * outer)
                return false; // 整个 hitbox 在环外

            float farX = MathF.Max(MathF.Abs(c.X - targetHitbox.Left), MathF.Abs(c.X - targetHitbox.Right));
            float farY = MathF.Max(MathF.Abs(c.Y - targetHitbox.Top), MathF.Abs(c.Y - targetHitbox.Bottom));
            float dMax2 = farX * farX + farY * farY;
            if (dMax2 < inner * inner)
                return false; // 整个 hitbox 在环孔内 (风暴眼是安全区 — 判定诚实)

            return true;
        }
        return null;
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
        if (IsRecalling)
            modifiers.SourceDamage *= 2f;
        else if (IsImploding)
            modifiers.SourceDamage *= 1.5f;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 600);
        target.AddBuff(BuffID.Venom, 300);

        // 风暴/回程命中 = 浇灌 (刻下年轮烙印), 等待内爆收割
        ArrogantSylvanBrandNPC.AddStack(target);

        int dustAmt = IsRecalling ? 14 : 10;
        for (int i = 0; i < dustAmt; i++) {
            Dust burst = Dust.NewDustPerfect(target.Center, i % 3 == 0 ? DustID.GoldFlame : DustID.JungleTorch,
                Main.rand.NextVector2Circular(8f, 8f), 40, default, 2.4f);
            burst.noGravity = true;
        }
        WeaponVFX.AddScreenShake(target.Center, IsRecalling ? 2f : 1.2f);
        ArrogantSylvanFX.HitBurstThrottled(Projectile.GetSource_OnHit(target), target.Center,
            IsRecalling ? 1.2f : 0.9f, Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        Vector2 origin = tex.Size() / 2f;

        // 金翠双层 ribbon 主拖尾 (§B.1)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: IsSpiraling ? 18f : 14f,
            outerColor: ArrogantSylvanPalette.TrailOuter, innerColor: ArrogantSylvanPalette.TrailInner,
            uvScroll: -(float)Main.timeForVisualEffects * 0.04f);

        // === 风暴环带 (系列专属 Vortex 着色器 — 环带即判定区可视化) ===
        if (IsSpiraling || IsImploding) {
            float bandIntensity = IsImploding
                ? MathHelper.Clamp(_spiralRadius / SpiralMaxRadius + 0.35f, 0f, 1f)
                : MathHelper.Clamp(_spiralTimer / 30f, 0f, 0.85f);
            float spin = IsImploding ? 1.6f : 0.9f;
            ArrogantSylvanFX.DrawVortexBand(_stormCenter, MathF.Max(_spiralRadius, 20f), BandHalfWidth,
                spin, _pulseFlash, bandIntensity);
        }

        // 内爆坍缩 set-piece: 向心径向泛光 (占全屏名额, 名额满自动退化柔光)
        if (_collapseFlash > 0) {
            float f = _collapseFlash / 20f;                 // 1→0
            float bell = MathF.Sin(f * MathF.PI);
            WeaponVFX.DrawRadialBloom(_stormCenter, 0.05f + 0.13f * f, bell * 0.85f,
                new Color(220, 235, 120), 10f);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        // 内爆向心叶片碎光 (Sparkle 向中心收束)
        if (_collapseFlash > 0) {
            float f = _collapseFlash / 20f;
            Texture2D spk = ACMAsset.Sparkle;
            for (int s = 0; s < 8; s++) {
                float ang = MathHelper.TwoPi * s / 8f + Timer * 0.1f;
                Vector2 p = _stormCenter + ang.ToRotationVector2() * (140f * f);
                sb.Draw(spk, p - Main.screenPosition, null,
                    ArrogantSylvanPalette.JadeBright * (f * 0.6f), ang,
                    spk.Size() * 0.5f, 0.45f * f, SpriteEffects.None, 0);
            }
        }

        // 镖体拖尾 - 风暴阶段更粗更亮
        for (int i = 0; i < Projectile.oldPos.Length; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float progress = 1f - (float)i / Projectile.oldPos.Length;
            Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
            float alpha = IsSpiraling ? 0.55f : (IsImploding ? 0.65f : (IsRecalling ? 0.60f : 0.50f));
            Color trailColor = Color.Lerp(ArrogantSylvanPalette.GoldBright, ArrogantSylvanPalette.JadeDeep, progress)
                * progress * alpha;
            trailColor.A = 0;
            float trailScale = Projectile.scale * progress * (IsSpiraling ? 0.95f : 0.90f);
            sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin,
                trailScale, SpriteEffects.None, 0);
        }

        // 回收冲击波
        if (IsRecalling && Projectile.velocity.Length() > 12f) {
            Texture2D wave = ACMAsset.GlaciateWave;
            sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                ArrogantSylvanPalette.GoldBright * 0.70f,
                Projectile.velocity.ToRotation(), wave.Size() * 0.5f,
                new Vector2(0.75f, 0.30f), SpriteEffects.None, 0);
        }

        // 风暴眼中心光核 (安全区提示: 环带才是杀伤区)
        if (IsSpiraling || IsImploding) {
            Texture2D sg2 = ACMAsset.SoftGlow;
            sb.Draw(sg2, _stormCenter - Main.screenPosition, null,
                ArrogantSylvanPalette.WhiteHot * 0.30f, 0f,
                sg2.Size() * 0.5f,
                0.35f, SpriteEffects.None, 0);
        }

        Texture2D sg = ACMAsset.SoftGlow;
        float pulse = 0.40f + 0.15f * MathF.Sin(Timer * 0.14f);
        float glowScale = IsSpiraling ? 0.80f : (IsRecalling ? 0.95f : 0.70f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.JadeBright * pulse, 0f,
            sg.Size() * 0.5f,
            glowScale, SpriteEffects.None, 0);

        Color glowColor = ArrogantSylvanPalette.GoldBright * 0.32f;
        glowColor.A = 0;
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            glowColor, Projectile.rotation, origin, Projectile.scale * 1.25f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        // 残影 - 高速阶段更浓烈 (速度门控)
        float phaseAfterAlphaBase = IsSpiraling ? 0.55f : (IsImploding ? 0.60f : (IsRecalling ? 0.50f : 0.35f));
        for (int i = 1; i < Projectile.oldPos.Length; i += 2) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float progress = 1f - (float)i / Projectile.oldPos.Length;
            Vector2 afterimagePos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
            float afterAlpha = progress * progress * phaseAfterAlphaBase;
            Color afterColor = Color.Lerp(lightColor, new Color(220, 255, 160), 0.30f) * afterAlpha;
            float afterScale = Projectile.scale * MathHelper.Lerp(0.55f, 0.95f, progress);
            sb.Draw(tex, afterimagePos, null, afterColor, Projectile.oldRot[i], origin, afterScale, SpriteEffects.None, 0);
        }

        Color mainColor = Color.Lerp(lightColor, new Color(220, 255, 180), 0.35f);
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

        return false;
    }

    public override void OnKill(int timeLeft) {
        for (int i = 0; i < 16; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(7f, 7f), 40, default, 2.4f);
            d.noGravity = true;
        }
    }
}

/// <summary>
/// 傲世旋风叶片 - 环带脉冲/坍缩甩出的叶片弹幕 (共享节流索敌, 命中刻烙印)
/// </summary>
public class ArrogantSylvanWhirlLeaf : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Leaf;

    private float _timer;
    private ref float TargetCache => ref Projectile.localAI[0];
    private ref float RescanTimer => ref Projectile.localAI[1];

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 5;
    }

    public override void SetDefaults() {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 100;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation += 0.30f * Projectile.direction;
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 4) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        if (_timer > 25) {
            NPC target = ArrogantSylvanTargeting.UpdateTarget(Projectile, ref TargetCache, ref RescanTimer, 600f);
            ArrogantSylvanTargeting.SteerTowards(Projectile, target, 20f, 0.12f);
        }
        else {
            Projectile.velocity *= 0.95f;
        }

        Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
            -Projectile.velocity * 0.05f, 80, default, 1f);
        trail.noGravity = true;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 300);
        target.AddBuff(BuffID.Venom, 120);
        ArrogantSylvanBrandNPC.AddStack(target);
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        int fh = tex.Height / Main.projFrames[Type];
        Rectangle src = new(0, Projectile.frame * fh, tex.Width, fh);
        Vector2 origin = new(tex.Width / 2f, fh / 2f);
        Color tint = Color.Lerp(lightColor, new Color(220, 255, 140), 0.45f);
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            tint, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);
        return false;
    }
}

/// <summary>
/// 傲世裁决花瓣 - 内爆坍缩时释放的强力花瓣弹幕 (共享节流索敌, 命中刻烙印)
/// </summary>
public class ArrogantSylvanVerdictPetal : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPetal;

    private float _timer;
    private ref float TargetCache => ref Projectile.localAI[0];
    private ref float RescanTimer => ref Projectile.localAI[1];

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 3;
    }

    public override void SetDefaults() {
        Projectile.width = 18;
        Projectile.height = 18;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 4;
        Projectile.timeLeft = 150;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 6;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 4) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        if (_timer < 18) {
            Projectile.velocity *= 0.94f;
        }
        else {
            NPC target = ArrogantSylvanTargeting.UpdateTarget(Projectile, ref TargetCache, ref RescanTimer, 800f);
            if (target != null)
                ArrogantSylvanTargeting.SteerTowards(Projectile, target, 22f, 0.10f);
            else
                Projectile.velocity *= 1.02f;
        }

        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                -Projectile.velocity * 0.05f, 80, default, 1.2f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 600);
        target.AddBuff(BuffID.Venom, 300);
        ArrogantSylvanBrandNPC.AddStack(target);
        for (int i = 0; i < 6; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(5f, 5f), 40, default, 2f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        int fh = tex.Height / Main.projFrames[Type];
        Rectangle src = new(0, Projectile.frame * fh, tex.Width, fh);
        Vector2 origin = new(tex.Width / 2f, fh / 2f);
        Color tint = Color.Lerp(lightColor, new Color(220, 255, 180), 0.40f);
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            tint, Projectile.rotation, origin, Projectile.scale * 1.4f, SpriteEffects.None, 0);
        return false;
    }
}
