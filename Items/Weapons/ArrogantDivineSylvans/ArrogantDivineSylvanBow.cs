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
/// 傲世神木·穿林弓 - 神木长弓的终极形态
/// 每射一道无限穿透叶枪主箭 + 4 片真·DNA双螺旋叶刃 (绕飞行轴对旋)
/// 主箭/叶刃命中刻下「年轮烙印」
/// 每第 4 射释放「世界树之矢」: 18 帧聚能凝形 → 1 帧静默 → 爆发穿透, 命中**引爆**烙印
/// </summary>
public class ArrogantDivineSylvanBow : ModItem
{
    private int shotCounter;

    public override void SetDefaults() {
        Item.damage = 1400;
        Item.crit = 28;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 28;
        Item.height = 60;
        Item.useTime = 14;
        Item.useAnimation = 14;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 7f;
        Item.value = Item.buyPrice(gold: 500);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item5;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<ArrogantSylvanLeafLance>();
        Item.shootSpeed = 20f;
        Item.useAmmo = AmmoID.Arrow;
    }

    public override Vector2? HoldoutOffset() => new Vector2(-2, 0);

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
        type = ModContent.ProjectileType<ArrogantSylvanLeafLance>();
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        shotCounter++;

        // 主箭
        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);

        // 4 片真·DNA双螺旋叶刃: 两对螺旋链, 相位差 π, 对旋
        for (int i = 0; i < 4; i++) {
            float phase = (i % 2) * MathF.PI;          // 同链相位差 π (双链)
            float spinDir = i < 2 ? 1f : -1f;          // 两对对旋
            Projectile.NewProjectile(source, position, velocity * 0.85f,
                ModContent.ProjectileType<ArrogantSylvanSpiralLeaf>(),
                (int)(damage * 0.45f), knockback * 0.3f, player.whoAmI,
                ai0: phase, ai1: spinDir);
        }

        // 每 4 射释放世界树之矢 (三段感: 聚能→静默→爆发, 由弹幕自身承载)
        if (shotCounter % 4 == 0) {
            Projectile.NewProjectile(source, position, velocity * 0.8f,
                ModContent.ProjectileType<ArrogantSylvanWorldTreeArrow>(),
                (int)(damage * 2.5f), knockback * 2f, player.whoAmI);
            SoundEngine.PlaySound(SoundID.Item92 with { Volume = 1.1f, Pitch = -0.3f + Main.rand.NextFloat(-0.08f, 0.08f) }, player.Center);
            // 世界树之矢触发技 → 短暂金翠染屏定调 (占全屏唯一名额, 同屏≤1 自动仲裁)
            ArrogantSylvanScreenTint.Spawn(source, player.Center, player.whoAmI);
        }

        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<ArrogantDivineSylvan>(15)
            .AddIngredient<DivineWoodLongbow>()
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
}

/// <summary>
/// 傲世叶枪主箭 - 高速穿透, 命中减速锁定 + 刻下年轮烙印
/// </summary>
public class ArrogantSylvanLeafLance : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 18;
    }

    public override void SetDefaults() {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 200;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.extraUpdates = 2;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 6;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.4f, 1.0f, 0.3f);

        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                -Projectile.velocity * 0.05f, 40, default, 1.8f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 600);
        target.AddBuff(BuffID.Venom, 300);
        target.velocity *= 0.4f;

        // 浇灌: 刻下年轮烙印
        ArrogantSylvanBrandNPC.AddStack(target);

        // 减速锁定的可读表现: 脚下翠色根须缠绕
        for (int i = 0; i < 6; i++) {
            Vector2 rootPos = target.Bottom + new Vector2(Main.rand.NextFloat(-target.width * 0.5f, target.width * 0.5f), 0);
            Dust root = Dust.NewDustPerfect(rootPos, DustID.GrassBlades,
                new Vector2(0, Main.rand.NextFloat(-3f, -1f)), 60, default, 1.6f);
            root.noGravity = true;
        }
        for (int i = 0; i < 9; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, i % 2 == 0 ? DustID.JungleTorch : DustID.GoldFlame,
                Main.rand.NextVector2Circular(7f, 7f), 40, default, 2.2f);
            d.noGravity = true;
        }
        ArrogantSylvanFX.HitBurstThrottled(Projectile.GetSource_OnHit(target), target.Center, 1.1f, Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;

        // 主箭金翠流光束核 (ACMShaders.DrawBeam: 金芯 + 翠边)
        Vector2 bdir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        ACMShaders.DrawBeam(Projectile.Center - bdir * 90f, Projectile.Center + bdir * 12f,
            halfWidth: 9f, core: new Color(255, 230, 130, 220), edge: new Color(120, 220, 120, 0),
            intensity: 0.95f, flowSpeed: 1.8f, flowScale: 2.2f, coreSharp: 2.2f);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        Texture2D sg = ACMAsset.SoftGlow;

        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.70f;
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, ArrogantSylvanPalette.GoldDark * a, Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.60f + i * 0.016f, 0.18f), SpriteEffects.None, 0);
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, ArrogantSylvanPalette.JadeBright * (a * 0.35f), Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.30f, 0.09f), SpriteEffects.None, 0);
        }

        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 120), Projectile.rotation,
            lsh.Size() * 0.5f,
            new Vector2(1.1f, 0.28f), SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.JadeBright * 0.80f, 0f,
            sg.Size() * 0.5f,
            0.55f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f, Pitch = Main.rand.NextFloat(-0.15f, 0.15f) }, Projectile.Center);
        for (int i = 0; i < 10; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(6f, 6f), 40, default, 2f);
            d.noGravity = true;
        }
    }
}

/// <summary>
/// 傲世螺旋叶刃 - 真·DNA双螺旋: 绕主箭飞行轴做余弦投影对旋 (ai0=相位, ai1=旋向),
/// 30 帧解旋后转入共享节流追踪
/// </summary>
public class ArrogantSylvanSpiralLeaf : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/BlankStar";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
    }

    private bool _homing;
    private float _spiralTimer;
    private float _prevOffset;            // 上一帧螺旋横向偏移 (差分应用, 保持速度向量为轴向)
    private ref float TargetCache => ref Projectile.localAI[0];
    private ref float RescanTimer => ref Projectile.localAI[1];
    private const float SPIRAL_DURATION = 30f;
    private const float SpiralRadius = 26f;

    public override void SetDefaults() {
        Projectile.width = 22;
        Projectile.height = 22;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 200;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override void AI() {
        _spiralTimer++;
        Projectile.rotation += 0.30f;

        if (!_homing && _spiralTimer < SPIRAL_DURATION) {
            // 真双螺旋: 横向偏移 = cos(相位) × 半径 (3D 螺旋的 2D 投影), 差分作用于位置
            float t = _spiralTimer / SPIRAL_DURATION;
            float radius = MathHelper.Lerp(SpiralRadius, 4f, t);      // 解旋收束
            float phase = Projectile.ai[0] + _spiralTimer * 0.30f * Projectile.ai[1];
            float newOffset = MathF.Cos(phase) * radius;

            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = new(-forward.Y, forward.X);
            Projectile.Center += perp * (newOffset - _prevOffset);
            _prevOffset = newOffset;
        }
        else {
            _homing = true;
            NPC target = ArrogantSylvanTargeting.UpdateTarget(Projectile, ref TargetCache, ref RescanTimer, 800f);
            ArrogantSylvanTargeting.SteerTowards(Projectile, target, 22f, 0.14f);
        }

        Lighting.AddLight(Projectile.Center, 0.2f, 0.6f, 0.2f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 480);
        target.AddBuff(BuffID.Venom, 240);
        ArrogantSylvanBrandNPC.AddStack(target);
        for (int i = 0; i < 6; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(5f, 5f), 40, default, 2f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;

        // 螺旋叶刃金翠双层 ribbon (§B.1)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 12f,
            outerColor: ArrogantSylvanPalette.TrailOuter, innerColor: ArrogantSylvanPalette.TrailInner,
            uvScroll: -(float)Main.timeForVisualEffects * 0.05f);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D star = ACMAsset.BlankStar;
        Texture2D sg = ACMAsset.SoftGlow;
        float pulse = 0.65f + 0.22f * MathF.Sin((float)Main.timeForVisualEffects * 0.2f);

        // 螺旋近端 (cos>0) 亮金, 远端 (cos<0) 沉翠 — 双螺旋的"前后景"深度提示
        float depth = MathF.Cos(Projectile.ai[0] + _spiralTimer * 0.30f * Projectile.ai[1]);
        Color coreCol = Color.Lerp(ArrogantSylvanPalette.JadeDeep, ArrogantSylvanPalette.GoldBright,
            _homing ? 0.7f : (depth * 0.5f + 0.5f));

        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.55f;
            sb.Draw(sg,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, ArrogantSylvanPalette.GoldBright * (a * 0.7f), 0f,
                sg.Size() * 0.5f,
                0.40f, SpriteEffects.None, 0);
        }

        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            coreCol * (0.90f * pulse),
            Projectile.rotation, star.Size() * 0.5f,
            0.70f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.WhiteHot * (0.50f * pulse), 0f,
            sg.Size() * 0.5f,
            0.50f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 世界树之矢 - 每 4 射的大招时刻, 完整三段感:
/// 聚能凝形 18 帧 (缓漂 + 向心金尘 + 藤纹自箭尾生长) → 1 帧静默 (收束 + 无粒子)
/// → 爆发 (1.5×速度 + 震屏 + 分层音) → 命中**引爆**年轮烙印
/// 爆发前无伤害判定 (伤害窗口与视觉严格对齐)
/// </summary>
public class ArrogantSylvanWorldTreeArrow : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    private const int ChargeFrames = 18;
    private const int SilenceFrame = ChargeFrames + 1;   // 静默帧 (inhale)

    private ref float Timer => ref Projectile.ai[0];
    private bool Launched => Timer > SilenceFrame;

    private Vector2 _storedVel;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 24;
    }

    public override void SetDefaults() {
        Projectile.width = 40;
        Projectile.height = 40;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 150;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 6;
    }

    public override void OnSpawn(IEntitySource source) {
        _storedVel = Projectile.velocity;
        Projectile.netUpdate = true;
    }

    public override bool? CanDamage() => Launched ? null : false; // 伤害窗口=爆发后 (公平阀)

    public override void AI() {
        Timer++;
        Projectile.rotation = (_storedVel == Vector2.Zero ? Projectile.velocity : _storedVel).ToRotation();

        if (Timer <= ChargeFrames) {
            // === 聚能凝形: 缓漂 + 向心金尘 (密度 ∝ sqrt(charge), 尾段渐静) ===
            if (_storedVel == Vector2.Zero) _storedVel = Projectile.velocity; // 非 owner 端 OnSpawn 后同步兜底
            Projectile.velocity = _storedVel * 0.12f;

            float charge = Timer / (float)ChargeFrames;
            if (charge < 0.75f && Main.rand.NextFloat() < MathF.Sqrt(charge) * 0.9f) {
                Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(90f, 90f);
                Dust d = Dust.NewDustPerfect(from, Main.rand.NextBool() ? DustID.GoldFlame : DustID.JungleTorch,
                    (Projectile.Center - from) * 0.11f, 50, default, 1.7f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.5f * charge, 1.2f * charge, 0.4f * charge);
        }
        else if (Timer == SilenceFrame) {
            // === 静默帧: 呼气前的吸气 — 完全无粒子, 速度归零 ===
            Projectile.velocity = Vector2.Zero;
        }
        else if (Timer == SilenceFrame + 1) {
            // === 爆发: 1.5× 原速弹射 + 震屏 + 分层音 ===
            Projectile.velocity = _storedVel * 1.5f;
            Projectile.netUpdate = true;
            WeaponVFX.AddScreenShake(Projectile.Center, 3f);
            SoundEngine.PlaySound(SoundID.Item5 with { Volume = 1.1f, Pitch = -0.4f + Main.rand.NextFloat(-0.08f, 0.08f) }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.9f, Pitch = 0.3f }, Projectile.Center);
        }
        else {
            // 飞行: 轻微衰减 + 密集粒子 (粒子 ∝ 动能)
            Projectile.velocity *= 0.985f;
            Lighting.AddLight(Projectile.Center, 0.8f, 2f, 0.6f);

            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(25, 25),
                    DustID.JungleTorch, -Projectile.velocity * 0.15f, 30, default, 2.4f);
                d.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                Vector2 side = Projectile.Center + Main.rand.NextVector2Circular(30f, 30f);
                Dust leafDust = Dust.NewDustPerfect(side, DustID.GrassBlades,
                    Main.rand.NextVector2Circular(2f, 2f), 80, default, 1.5f);
                leafDust.noGravity = true;
            }
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 900);
        target.AddBuff(BuffID.Venom, 600);
        target.velocity *= 0.2f;

        for (int i = 0; i < 16; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, i % 2 == 0 ? DustID.JungleTorch : DustID.GoldFlame,
                Main.rand.NextVector2Circular(10f, 10f), 30, default, 2.8f);
            d.noGravity = true;
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.ArrogantSylvan, scale: 1.4f, owner: Projectile.owner);
        WeaponVFX.AddScreenShake(target.Center, 2f);

        // === 系列引爆动作: 世界树之矢命中即绽放该目标烙印 ===
        ArrogantSylvanBloom.Detonate(Projectile.GetSource_OnHit(target), target,
            Projectile.damage, 3f, Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        float charge = MathHelper.Clamp(Timer / ChargeFrames, 0f, 1f);
        Vector2 wdir = (_storedVel == Vector2.Zero ? Projectile.velocity : _storedVel).SafeNormalize(Vector2.UnitX);

        if (!Launched) {
            // 聚能期: 藤纹光束自箭尾向前生长 (charge² — 隐形开局, 惊人收尾)
            float growth = charge * charge;
            ACMShaders.DrawBeam(Projectile.Center - wdir * (30f + 130f * growth), Projectile.Center + wdir * (10f + 16f * growth),
                halfWidth: 6f + 13f * growth,
                core: new Color(255, 235, 150, 230), edge: new Color(120, 230, 120, 0),
                intensity: 0.3f + 0.7f * growth, flowSpeed: 2.4f, flowScale: 2.6f, coreSharp: 2f);
        }
        else {
            // 爆发飞行: 加粗金翠流光束核
            ACMShaders.DrawBeam(Projectile.Center - wdir * 150f, Projectile.Center + wdir * 24f,
                halfWidth: 20f, core: new Color(255, 235, 150, 230), edge: new Color(120, 230, 120, 0),
                intensity: 1f, flowSpeed: 1.6f, flowScale: 2f, coreSharp: 2f);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D sparkle = ACMAsset.Sparkle;

        if (!Launched) {
            // 聚能核心: 收束闪烁 (静默帧前一瞬变小 — 爆前收缩)
            float shrink = Timer >= ChargeFrames - 2 ? 0.55f : 1f;
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                ArrogantSylvanPalette.GoldBright * (0.3f + 0.55f * charge), 0f,
                sg.Size() * 0.5f, (0.25f + 0.55f * charge * charge) * shrink, SpriteEffects.None, 0);
            sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
                ArrogantSylvanPalette.JadeBright * (0.5f * charge),
                Timer * 0.1f, sparkle.Size() * 0.5f, 0.45f * charge * shrink, SpriteEffects.None, 0);
        }
        else {
            // 华丽拖尾
            for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.72f;
                sb.Draw(lsh,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, ArrogantSylvanPalette.GoldDark * a, Projectile.oldRot[i],
                    lsh.Size() * 0.5f,
                    new Vector2(0.80f + i * 0.02f, 0.30f), SpriteEffects.None, 0);
                sb.Draw(lsh,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, ArrogantSylvanPalette.JadeDeep * (a * 0.50f), Projectile.oldRot[i],
                    lsh.Size() * 0.5f,
                    new Vector2(0.50f, 0.15f), SpriteEffects.None, 0);
            }

            // 主体巨箭
            sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
                new Color(255, 255, 180), Projectile.rotation,
                lsh.Size() * 0.5f,
                new Vector2(1.6f, 0.45f), SpriteEffects.None, 0);
            sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
                ArrogantSylvanPalette.GoldBright * 0.70f, Projectile.rotation,
                lsh.Size() * 0.5f,
                new Vector2(2.0f, 0.60f), SpriteEffects.None, 0);

            // 前端光芒
            Vector2 front = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 30f;
            sb.Draw(sg, front - Main.screenPosition, null,
                ArrogantSylvanPalette.WhiteHot * 0.85f, 0f,
                sg.Size() * 0.5f,
                0.80f, SpriteEffects.None, 0);
            sb.Draw(sparkle, front - Main.screenPosition, null,
                ArrogantSylvanPalette.GoldBright * 0.65f,
                (float)Main.timeForVisualEffects * 0.1f,
                sparkle.Size() * 0.5f,
                0.60f, SpriteEffects.None, 0);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }

    public override void OnKill(int timeLeft) {
        // 终点绽放 (纯视觉)
        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
            ACMWeaponBurst.ArrogantSylvan, scale: 1.6f, owner: Projectile.owner);
        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = -0.1f }, Projectile.Center);
        for (int i = 0; i < 14; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, i % 2 == 0 ? DustID.GoldFlame : DustID.JungleTorch,
                Main.rand.NextVector2Circular(8f, 8f), 30, default, 2.4f);
            d.noGravity = true;
        }
    }
}
