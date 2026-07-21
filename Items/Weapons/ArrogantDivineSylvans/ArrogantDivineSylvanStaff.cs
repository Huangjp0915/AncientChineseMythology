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
/// 傲世神木·万藤杖 - 真·藤蔓链鞭
/// 按住引导「驱鞭」: 鞭头持续追随鼠标方向; 鞭身 (金边翠芯 ribbon) 本体具线段伤害
/// 鞭至最大链长或松手 → 「咬合」: 鞭口弹性张开 → 猛然咬合 → 藤蔓新星 + **引爆**范围年轮烙印
/// 鞭身/鞭头命中刻烙印并弹出追踪叶爆; 引导期链身分生藤蔓触手
/// </summary>
public class ArrogantDivineSylvanStaff : ModItem
{
    public override void SetDefaults() {
        Item.damage = 1400;
        Item.crit = 24;
        Item.DamageType = DamageClass.Magic;
        Item.width = 40;
        Item.height = 40;
        Item.useTime = 22;
        Item.useAnimation = 22;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 8f;
        Item.value = Item.buyPrice(gold: 500);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<ArrogantSylvanVineWhipHead>();
        Item.shootSpeed = 24f;
        Item.mana = 18;
        Item.channel = true; // channel 真正生效: 按住驱鞭
        Item.staff[Type] = true;
    }

    public override bool CanUseItem(Player player) {
        return player.ownedProjectileCounts[ModContent.ProjectileType<ArrogantSylvanVineWhipHead>()] < 1;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
        Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.9f, Pitch = 0.1f + Main.rand.NextFloat(-0.1f, 0.1f) }, player.Center);
        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<ArrogantDivineSylvan>(15)
            .AddIngredient<DivineWoodScepter>()
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
}

/// <summary>
/// 傲世藤蔓链鞭头部 - 三相: 驱鞭(channel 追鼠标) → 咬合(elastic 张口 10 帧 → snap 引爆) → 回收
/// 鞭身 = BuildRibbonStrip 金边翠芯 ribbon + 命中/咬合注入的行波 (弹簧衰减 ×0.88/f)
/// 鞭身线段本体具伤害; 触发时机 = 鞭到最远点, 完全可读
/// </summary>
public class ArrogantSylvanVineWhipHead : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private const float MaxChainLength = 560f;
    private const int BiteOpenFrames = 10;
    private const int ChainSamples = 14;      // 链身采样点数 (绘制/判定共用)

    // Phase: 0=驱鞭(Extend/Steer) 1=咬合(Bite) 2=回收(Return)
    private ref float Timer => ref Projectile.ai[0];
    private ref float Phase => ref Projectile.ai[1];
    private ref float PhaseTimer => ref Projectile.ai[2];

    private float _wavePulse;      // 行波能量 (命中/咬合注入, ×0.88/f 衰减; 纯视觉)
    private float _wavePhase;      // 行波相位
    private bool IsExtending => Phase == 0f;
    private bool IsBiting => Phase == 1f;

    private Player Owner => Main.player[Projectile.owner];

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 28;
    }

    public override void SetDefaults() {
        Projectile.width = 24;
        Projectile.height = 24;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 480;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 12;
    }

    public override void AI() {
        Timer++;
        PhaseTimer++;
        Player owner = Owner;
        if (!owner.active || owner.dead) { Projectile.Kill(); return; }

        if (_wavePulse > 0.01f) _wavePulse *= 0.88f;
        _wavePhase += 0.5f;

        Projectile.rotation = Projectile.velocity.ToRotation();
        float distToOwner = Vector2.Distance(owner.MountedCenter, Projectile.Center);

        switch ((int)Phase) {
            case 0: { // === 驱鞭: channel 追鼠标方向 ===
                if (owner.channel) {
                    owner.itemAnimation = 2;
                    owner.itemTime = 2;
                    if (Projectile.owner == Main.myPlayer) {
                        Vector2 steer = Projectile.SafeDirectionTo(Main.MouseWorld);
                        float speed = MathF.Max(Projectile.velocity.Length(), 14f);
                        Projectile.velocity = Vector2.Lerp(Projectile.velocity, steer * speed, 0.10f);
                        if (Timer % 8 == 0)
                            Projectile.netUpdate = true;
                    }
                    Projectile.velocity *= 0.99f;
                }
                else {
                    Projectile.velocity *= 0.96f;
                }

                // 引导期链身分生触手 (节流: 每 24 帧最多 2 根, 交替两侧)
                if (owner.channel && Timer % 24 == 0 && Timer > 10 && distToOwner > 120f
                    && Projectile.owner == Main.myPlayer) {
                    Vector2 dirBody = (Projectile.Center - owner.MountedCenter).SafeNormalize(Vector2.UnitX);
                    Vector2 perpDir = new(-dirBody.Y, dirBody.X);
                    for (int t = 0; t < 2; t++) {
                        float p = 0.35f + 0.3f * t;
                        Vector2 spawnPos = owner.MountedCenter + dirBody * (distToOwner * p);
                        float side = (Timer / 24 + t) % 2 == 0 ? 1f : -1f;
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawnPos,
                            perpDir * side * 10f + dirBody * 3f,
                            ModContent.ProjectileType<ArrogantSylvanTendril>(),
                            Projectile.damage / 4, 2f, Projectile.owner);
                    }
                }

                // 到达最大链长或松手且已伸出 → 咬合
                bool reachedMax = distToOwner >= MaxChainLength;
                bool released = !owner.channel && Timer > 12;
                if (reachedMax || released) {
                    Phase = 1f;
                    PhaseTimer = 0f;
                    Projectile.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.9f, Pitch = -0.1f }, Projectile.Center);
                }
                break;
            }

            case 1: { // === 咬合: elastic 张口 → snap ===
                owner.itemAnimation = 2;
                owner.itemTime = 2;
                Projectile.velocity *= 0.80f; // 咬合前"憋住"

                if (PhaseTimer >= BiteOpenFrames) {
                    // SNAP — 大招时刻: 新星 + 范围引爆烙印
                    SnapBite(owner);
                    Phase = 2f;
                    PhaseTimer = 0f;
                    Projectile.netUpdate = true;
                }
                break;
            }

            default: { // === 回收 ===
                Vector2 back = owner.MountedCenter - Projectile.Center;
                Vector2 dir = back.SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 46f, 0.20f);
                if (back.Length() < 44f)
                    Projectile.Kill();
                break;
            }
        }

        // 尖端粒子 - 双色
        int dustN = IsBiting ? 3 : 2;
        for (int i = 0; i < dustN; i++) {
            int dustType = i == 0 ? DustID.JungleTorch : DustID.GoldFlame;
            Dust d = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(10, 10),
                dustType, -Projectile.velocity * 0.08f, 50, default, 1.6f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.35f, 0.9f, 0.3f);
    }

    /// <summary>咬合 snap: 藤蔓新星 + 范围引爆烙印 + 行波回传 + 震屏。</summary>
    private void SnapBite(Player owner) {
        _wavePulse = 1f; // 行波自鞭头注入, 沿链传回

        SoundEngine.PlaySound(SoundID.Item17 with { Volume = 1.2f, Pitch = 0.2f + Main.rand.NextFloat(-0.1f, 0.1f) }, Projectile.Center);
        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.9f, Pitch = -0.3f }, Projectile.Center);
        ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center,
            ACMWeaponBurst.ArrogantSylvan, scale: 1.4f, owner: Projectile.owner);
        WeaponVFX.AddScreenShake(Projectile.Center, 5f);

        if (Projectile.owner == Main.myPlayer) {
            // 藤蔓新星 (环形爆发)
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<ArrogantSylvanVineNova>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner);

            // === 系列引爆动作: 咬合波及范围内全部年轮烙印 ===
            ArrogantSylvanBloom.DetonateArea(Projectile.GetSource_FromThis(), Projectile.Center,
                280f, Projectile.damage, 3f, Projectile.owner);

            // 6 片追踪叶爆
            for (int i = 0; i < 6; i++) {
                Vector2 leafVel = Main.rand.NextVector2CircularEdge(9f, 9f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                    leafVel, ModContent.ProjectileType<ArrogantSylvanVineBurstLeaf>(),
                    Projectile.damage / 3, 2f, Projectile.owner);
            }
        }
    }

    /// <summary>链身采样点 (绘制与线段判定共用): 基础波 + 行波脉冲。</summary>
    private void BuildChainPoints(Vector2[] points) {
        Vector2 start = Owner.MountedCenter;
        Vector2 end = Projectile.Center;
        Vector2 diff = end - start;
        float totalDist = diff.Length();
        Vector2 direction = diff.SafeNormalize(Vector2.UnitX);
        Vector2 perp = new(-direction.Y, direction.X);

        for (int i = 0; i < points.Length; i++) {
            float p = i / (float)(points.Length - 1);
            // 基础起伏 (根部大尾部小) + 行波 (命中/咬合注入, 从鞭头向手传播)
            float wave = MathF.Sin(p * MathF.PI * 2.5f + Timer * 0.15f) * 14f * (1f - p) * MathF.Min(totalDist / 300f, 1f);
            float pulseWave = _wavePulse * 24f * MathF.Sin(p * MathF.PI * 3f + _wavePhase);
            points[i] = start + direction * (totalDist * p) + perp * (wave + pulseWave);
        }
    }

    private readonly Vector2[] _chainPoints = new Vector2[ChainSamples];

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        // 鞭头自身矩形
        if (projHitbox.Intersects(targetHitbox))
            return true;
        // 鞭身线段判定 (与 ribbon 视觉同一条曲线)
        BuildChainPoints(_chainPoints);
        float col = 0f;
        for (int i = 0; i < _chainPoints.Length - 1; i++) {
            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    _chainPoints[i], _chainPoints[i + 1], 16f, ref col))
                return true;
        }
        return false;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 600);
        target.AddBuff(BuffID.Venom, 300);

        // 浇灌: 刻下年轮烙印; 行波回传卖"鞭子抽中了"
        ArrogantSylvanBrandNPC.AddStack(target);
        _wavePulse = MathF.Max(_wavePulse, 0.7f);

        for (int i = 0; i < 8; i++) {
            int dustType = i % 2 == 0 ? DustID.JungleTorch : DustID.GoldFlame;
            Dust d = Dust.NewDustPerfect(target.Center, dustType,
                Main.rand.NextVector2Circular(7f, 7f), 30, default, 2f);
            d.noGravity = true;
        }

        // 命中弹出 4 片追踪叶
        if (Projectile.owner == Main.myPlayer) {
            for (int i = 0; i < 4; i++) {
                Vector2 leafVel = Main.rand.NextVector2CircularEdge(7f, 7f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center,
                    leafVel, ModContent.ProjectileType<ArrogantSylvanVineBurstLeaf>(),
                    Projectile.damage / 3, 1.5f, Projectile.owner);
            }
        }

        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f, Pitch = 0.4f + Main.rand.NextFloat(-0.15f, 0.15f) }, target.Center);
        WeaponVFX.AddScreenShake(target.Center, 1.5f);
        ArrogantSylvanFX.HitBurstThrottled(Projectile.GetSource_OnHit(target), target.Center, 1f, Projectile.owner);
    }

    public override void OnKill(int timeLeft) {
        for (int i = 0; i < 12; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
                Main.rand.NextVector2Circular(5f, 5f), 60, default, 2f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;

        // === 鞭身: 金边翠芯双层 ribbon (替换原版铁链贴图) ===
        BuildChainPoints(_chainPoints);
        WeaponVFX.DrawRibbonTrail(_chainPoints, baseWidth: 13f,
            outerColor: ArrogantSylvanPalette.TrailOuter, innerColor: ArrogantSylvanPalette.TrailInner,
            uvScroll: -(float)Main.timeForVisualEffects * 0.06f, subdivisions: 3);

        // 鞭头短拖尾 ribbon
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 14f,
            outerColor: ArrogantSylvanPalette.TrailOuter, innerColor: ArrogantSylvanPalette.TrailInner,
            uvScroll: -(float)Main.timeForVisualEffects * 0.04f);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D sparkle = ACMAsset.Sparkle;
        Texture2D wave = ACMAsset.GlaciateWave;
        float pulse = 0.7f + 0.25f * MathF.Sin(Timer * 0.22f);

        // 鞭口"咬合"双颚: elastic 张开 (overshoot 1.3×) → snap 闭合
        if (IsBiting) {
            float openT = MathF.Min(PhaseTimer / BiteOpenFrames, 1f);
            float jaw = ACMUtils.ElasticOut(openT) * 0.85f; // 弧度: 双颚张角
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float baseRot = dir.ToRotation();
            for (int s = -1; s <= 1; s += 2) {
                float jawRot = baseRot + s * jaw;
                sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                    ArrogantSylvanPalette.GoldBright * 0.8f, jawRot,
                    new Vector2(wave.Width * 0.15f, wave.Height * 0.5f),
                    new Vector2(0.55f, 0.18f), SpriteEffects.None, 0);
                sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                    ArrogantSylvanPalette.JadeBright * 0.55f, jawRot,
                    new Vector2(wave.Width * 0.15f, wave.Height * 0.5f),
                    new Vector2(0.38f, 0.11f), SpriteEffects.None, 0);
            }
        }

        // 鞭头辉光
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 100) * (0.70f * pulse), 0f,
            sg.Size() * 0.5f, IsBiting ? 0.85f : 0.70f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.WhiteHot * (0.40f * pulse), 0f,
            sg.Size() * 0.5f, 0.35f, SpriteEffects.None, 0);
        sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 120) * (0.50f * pulse),
            Timer * 0.08f,
            sparkle.Size() * 0.5f, 0.30f, SpriteEffects.None, 0);

        // 沿链身金翠发光节点 (行波经过处更亮)
        for (int i = 1; i < _chainPoints.Length - 1; i += 2) {
            float p = i / (float)(_chainPoints.Length - 1);
            float waveBoost = _wavePulse * MathF.Max(0f, MathF.Sin(p * MathF.PI * 3f + _wavePhase));
            Color glowCol = i % 4 == 1 ? new Color(220, 255, 100) : new Color(40, 200, 60);
            sb.Draw(sg, _chainPoints[i] - Main.screenPosition, null,
                glowCol * ((0.22f + 0.35f * waveBoost) * pulse), 0f,
                sg.Size() * 0.5f, 0.20f + 0.12f * waveBoost, SpriteEffects.None, 0);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 傲世分支触手 - 链身分生的小型藤蔓弹幕 (共享节流索敌, 命中刻烙印)
/// </summary>
public class ArrogantSylvanTendril : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    private float _timer;
    private ref float TargetCache => ref Projectile.localAI[0];
    private ref float RescanTimer => ref Projectile.localAI[1];

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 8;
    }

    public override void SetDefaults() {
        Projectile.width = 12;
        Projectile.height = 12;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 80;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation = Projectile.velocity.ToRotation();

        if (_timer > 10) {
            NPC target = ArrogantSylvanTargeting.UpdateTarget(Projectile, ref TargetCache, ref RescanTimer, 400f);
            ArrogantSylvanTargeting.SteerTowards(Projectile, target, 16f, 0.10f);
        }
        else {
            Projectile.velocity *= 0.96f;
        }

        Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
            -Projectile.velocity * 0.04f, 70, default, 1.1f);
        trail.noGravity = true;
        Lighting.AddLight(Projectile.Center, 0.1f, 0.4f, 0.1f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 300);
        ArrogantSylvanBrandNPC.AddStack(target);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;

        // 分支触手金翠双层 ribbon (§B.1)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 9f,
            outerColor: ArrogantSylvanPalette.TrailOuter, innerColor: ArrogantSylvanPalette.TrailInner,
            uvScroll: -(float)Main.timeForVisualEffects * 0.05f);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.45f;
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, ArrogantSylvanPalette.JadeDeep * a, Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.30f, 0.06f), SpriteEffects.None, 0);
        }

        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(80, 230, 90), Projectile.rotation,
            lsh.Size() * 0.5f,
            new Vector2(0.40f, 0.08f), SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 傲世藤蔓新星 - 鞭咬 snap 释放的环形爆炸 (半径判定与冲击环视觉对齐)
/// </summary>
public class ArrogantSylvanVineNova : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private const float MaxRadius = 320f;

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 60;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override bool ShouldUpdatePosition() => false;

    private float CurrentRadius() {
        float prog = Projectile.ai[0] / 60f;
        return MathHelper.SmoothStep(10f, MaxRadius, ACMUtils.QuadOut(MathHelper.Clamp(prog * 1.5f, 0f, 1f)));
    }

    public override void AI() {
        Projectile.ai[0]++;
        float radius = CurrentRadius();

        for (int i = 0; i < 8; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.4f, radius);
            int dustType = i % 3 == 0 ? DustID.GoldFlame : DustID.JungleTorch;
            Dust d = Dust.NewDustPerfect(pos, dustType,
                (pos - Projectile.Center).SafeNormalize(Vector2.Zero) * 2f, 40, default, 2.2f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.5f, 1.3f, 0.4f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 600);
        target.AddBuff(BuffID.Venom, 300);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        => VaultUtils.CircleIntersectsRectangle(Projectile.Center, CurrentRadius(), targetHitbox);

    public override bool PreDraw(ref Color lightColor) {
        float prog = 1f - Projectile.timeLeft / 60f;
        float alpha = ACMUtils.QuadOut(1f - prog) * 0.92f;
        float scale = MathHelper.SmoothStep(0f, 20f, ACMUtils.QuadOut(prog));

        // 年轮新星: GrowthRing 专属着色器 + 金翠双环冲击波 (§B.8)
        ArrogantSylvanFX.DrawGrowthRing(Projectile.Center, MaxRadius,
            ACMUtils.QuadOut(MathHelper.Clamp(prog * 1.5f, 0f, 1f)), alpha * 0.7f, ringFreq: 9f);
        float ringR = CurrentRadius();
        WeaponVFX.DrawShockwaveRing(Projectile.Center, ringR, 16f, alpha,
            ArrogantSylvanPalette.JadeBright, ArrogantSylvanPalette.GoldDark);
        WeaponVFX.DrawShockwaveRing(Projectile.Center, ringR * 0.62f, 10f, alpha * 0.8f,
            new Color(230, 240, 150), new Color(80, 200, 90));

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D sparkle = ACMAsset.Sparkle;

        // 16 道放射藤蔓新星
        for (int k = 0; k < 16; k++) {
            float bAngle = k * MathF.PI / 8f + Projectile.ai[0] * 0.03f;
            float bLen = k % 2 == 0 ? scale * 0.65f : scale * 0.42f;
            Color bColor = k % 3 == 0
                ? ArrogantSylvanPalette.GoldBright
                : k % 3 == 1
                    ? ArrogantSylvanPalette.JadeDeep
                    : ArrogantSylvanPalette.WhiteHot;
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                bColor * (alpha * 0.75f), bAngle,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.12f, bLen), SpriteEffects.None, 0);
        }

        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.JadeBright * (alpha * 0.55f), 0f,
            sg.Size() * 0.5f, scale * 0.55f, SpriteEffects.None, 0);

        float flashAlpha = MathHelper.SmoothStep(1f, 0f, prog * 1.3f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.WhiteHot * (alpha * flashAlpha), 0f,
            sg.Size() * 0.5f, scale * 0.22f, SpriteEffects.None, 0);

        sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 140) * (alpha * 0.55f),
            Projectile.ai[0] * 0.06f,
            sparkle.Size() * 0.5f, scale * 0.28f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 傲世藤蔓叶爆 - 鞭击命中/咬合释放的追踪叶片 (共享节流索敌, 命中刻烙印)
/// </summary>
public class ArrogantSylvanVineBurstLeaf : ModProjectile
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
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 120;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation += 0.24f * Projectile.direction;
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 4) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        if (_timer > 12) {
            NPC target = ArrogantSylvanTargeting.UpdateTarget(Projectile, ref TargetCache, ref RescanTimer, 600f);
            ArrogantSylvanTargeting.SteerTowards(Projectile, target, 18f, 0.10f);
        }
        else {
            Projectile.velocity *= 0.93f;
        }

        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
                -Projectile.velocity * 0.04f, 80, default, 1f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.12f, 0.35f, 0.12f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 300);
        target.AddBuff(BuffID.Venom, 120);
        ArrogantSylvanBrandNPC.AddStack(target);
        for (int i = 0; i < 4; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(4f, 4f), 50, default, 1.5f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
        int fh = tex.Height / Main.projFrames[Type];
        Rectangle src = new(0, Projectile.frame * fh, tex.Width, fh);
        Vector2 origin = new(tex.Width / 2f, fh / 2f);
        Color tint = Color.Lerp(lightColor, new Color(220, 255, 160), 0.40f);
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            tint, Projectile.rotation, origin, Projectile.scale * 1.3f, SpriteEffects.None, 0);
        return false;
    }
}
