using AncientChineseMythology.Celestias.Boss.Dazhengs.Items;
using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Weapons.DivineWoods;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.ArrogantDivineSylvans;

/// <summary>
/// 傲世神木·山海典 - 数量换质量的重铸
/// 每次使用释放 7 片金翠大叶刃 (扇形 ±40°, 每叶带金边翠芯 ribbon), 命中刻「年轮烙印」
/// 每第 6 次使用凝聚「叶暴漩涡」(大招): 引力吸拢 + 每 30 帧脉冲**引爆**范围烙印,
/// 绽放伴生环舞花瓣
/// </summary>
public class ArrogantDivineSylvanTome : ModItem
{
    private int useCounter;

    public override void SetDefaults() {
        Item.damage = 1700;
        Item.crit = 22;
        Item.DamageType = DamageClass.Magic;
        Item.width = 32;
        Item.height = 36;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 7f;
        Item.value = Item.buyPrice(gold: 500);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<ArrogantSylvanTomeLeaf>();
        Item.shootSpeed = 16f;
        Item.mana = 16;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
        Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        useCounter++;
        const int count = 7;
        float spreadHalf = MathHelper.ToRadians(40);
        float baseAngle = velocity.ToRotation();

        SoundEngine.PlaySound(SoundID.Grass with { Volume = 1f, Pitch = 0.2f + Main.rand.NextFloat(-0.1f, 0.1f) }, player.Center);
        SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = -0.15f }, player.Center);

        for (int i = 0; i < count; i++) {
            float angle = baseAngle + MathHelper.Lerp(-spreadHalf, spreadHalf, i / (count - 1f));
            angle += Main.rand.NextFloat(-0.03f, 0.03f);
            float speed = velocity.Length() * Main.rand.NextFloat(0.85f, 1.15f);
            Vector2 leafVel = angle.ToRotationVector2() * speed;
            float spiralDir = i % 2 == 0 ? 1f : -1f;
            Projectile.NewProjectile(source, position, leafVel, type, damage, knockback,
                player.whoAmI, ai0: spiralDir);
        }

        // 每第 6 次使用凝聚叶暴漩涡 (≈2.4s 一次的大招节奏)
        if (useCounter % 6 == 0 && player.whoAmI == Main.myPlayer) {
            Vector2 vortexPos = player.Center + velocity.SafeNormalize(Vector2.UnitX) * 250f;
            Projectile.NewProjectile(source, vortexPos, Vector2.Zero,
                ModContent.ProjectileType<ArrogantSylvanLeafVortex>(),
                damage * 2, knockback * 2f, player.whoAmI);
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 1f, Pitch = 0.3f + Main.rand.NextFloat(-0.1f, 0.1f) }, vortexPos);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = -0.3f }, vortexPos);
            // 叶暴漩涡触发技 → 短暂金翠染屏定调 (占全屏唯一名额, 同屏≤1 自动仲裁)
            ArrogantSylvanScreenTint.Spawn(source, vortexPos, player.whoAmI);
        }

        // 释放叶片尘雾 - 金翠交替 (减量)
        for (int i = 0; i < 12; i++) {
            Vector2 dustVel = velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.7f) * Main.rand.NextFloat(3f, 8f);
            int dustType = i % 3 == 0 ? DustID.GoldFlame : DustID.GrassBlades;
            Dust d = Dust.NewDustPerfect(position, dustType, dustVel, 60, default, 1.8f);
            d.noGravity = true;
        }

        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<ArrogantDivineSylvan>(15)
            .AddIngredient<DivineWoodTome>()
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
}

/// <summary>
/// 傲世大叶刃 - 7 片制的"重"叶: 更大体积 + 金边翠芯 ribbon 拖尾
/// 螺旋 30 帧 → 共享节流追踪; 命中刻年轮烙印
/// </summary>
public class ArrogantSylvanTomeLeaf : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Leaf;

    private float _timer;
    private ref float TargetCache => ref Projectile.localAI[0];
    private ref float RescanTimer => ref Projectile.localAI[1];
    private const float SpiralDuration = 30f;

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 5;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 10;
    }

    public override void SetDefaults() {
        Projectile.width = 20;
        Projectile.height = 20;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 220;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 4) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        if (_timer < SpiralDuration) {
            float spiralDir = Projectile.ai[0];
            float spiralForce = MathF.Sin(_timer * 0.28f) * spiralDir * 1.0f;
            Vector2 perpendicular = new(-Projectile.velocity.Y, Projectile.velocity.X);
            perpendicular = perpendicular.SafeNormalize(Vector2.Zero);
            Projectile.velocity += perpendicular * spiralForce;
            Projectile.velocity *= 0.97f;
        }
        else {
            NPC target = ArrogantSylvanTargeting.UpdateTarget(Projectile, ref TargetCache, ref RescanTimer, 700f);
            ArrogantSylvanTargeting.SteerTowards(Projectile, target, 20f, 0.10f);
        }

        if (Main.rand.NextBool(2)) {
            Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
                -Projectile.velocity * 0.04f, 80, default, 1.1f);
            trail.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.15f, 0.4f, 0.12f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 480);
        target.AddBuff(BuffID.Venom, 240);

        // 浇灌: 刻下年轮烙印 (等漩涡脉冲收割)
        ArrogantSylvanBrandNPC.AddStack(target);

        for (int i = 0; i < 6; i++) {
            int dustType = i % 2 == 0 ? DustID.JungleTorch : DustID.GoldFlame;
            Dust d = Dust.NewDustPerfect(target.Center, dustType,
                Main.rand.NextVector2Circular(5f, 5f), 40, default, 1.8f);
            d.noGravity = true;
        }
        // 高密度弹幕: 命中演出节流 (不再每叶一个 Burst)
        ArrogantSylvanFX.HitBurstThrottled(Projectile.GetSource_OnHit(target), target.Center, 0.9f, Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        // 大叶刃金边翠芯 ribbon 拖尾 (7 片制的"重"感)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 10f,
            outerColor: ArrogantSylvanPalette.TrailOuter, innerColor: ArrogantSylvanPalette.TrailInner,
            uvScroll: -(float)Main.timeForVisualEffects * 0.05f);

        Texture2D tex = TextureAssets.Projectile[Type].Value;
        int fh = tex.Height / Main.projFrames[Type];
        Rectangle src = new(0, Projectile.frame * fh, tex.Width, fh);
        Vector2 origin = new(tex.Width / 2f, fh / 2f);

        Color tint = Color.Lerp(lightColor, new Color(220, 255, 160), 0.40f);
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            tint, Projectile.rotation, origin, Projectile.scale * 1.7f, SpriteEffects.None, 0);

        Color glow = ArrogantSylvanPalette.JadeBright * 0.35f;
        glow.A = 0;
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            glow, Projectile.rotation, origin, Projectile.scale * 2.1f, SpriteEffects.None, 0);

        return false;
    }
}

/// <summary>
/// 傲世次生花瓣 - 烙印绽放的环舞伴生花瓣 (共享节流索敌)
/// </summary>
public class ArrogantSylvanTomePetal : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPetal;

    private float _timer;
    private ref float TargetCache => ref Projectile.localAI[0];
    private ref float RescanTimer => ref Projectile.localAI[1];

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 3;
    }

    public override void SetDefaults() {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 120;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 4) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        if (_timer < 12) {
            Projectile.velocity *= 0.93f;
        }
        else {
            NPC target = ArrogantSylvanTargeting.UpdateTarget(Projectile, ref TargetCache, ref RescanTimer, 600f);
            ArrogantSylvanTargeting.SteerTowards(Projectile, target, 18f, 0.12f);
        }

        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                Vector2.Zero, 80, default, 0.9f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 360);
        target.AddBuff(BuffID.Venom, 180);
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        int fh = tex.Height / Main.projFrames[Type];
        Rectangle src = new(0, Projectile.frame * fh, tex.Width, fh);
        Vector2 origin = new(tex.Width / 2f, fh / 2f);
        Color tint = Color.Lerp(lightColor, new Color(220, 255, 200), 0.35f);
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            tint, Projectile.rotation, origin, Projectile.scale * 1.4f, SpriteEffects.None, 0);
        return false;
    }
}

/// <summary>
/// 傲世叶暴漩涡 - 每第 6 次使用的大招: 引力吸拢 + 每 30 帧脉冲引爆范围年轮烙印 (绽放伴生花瓣)
/// 视觉走系列专属 Vortex 着色器 (与落叶风暴同一轨迹语言)
/// </summary>
public class ArrogantSylvanLeafVortex : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private const int PulseInterval = 30;

    private ref float Timer => ref Projectile.ai[0];
    private float _pulseFlash; // 脉冲增亮包络 (纯视觉)

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override bool ShouldUpdatePosition() => false;

    private float CurrentRadius() => MathHelper.Lerp(40f, 200f, Math.Min(Timer / 40f, 1f));

    public override void AI() {
        Timer++;
        if (_pulseFlash > 0.01f) _pulseFlash *= 0.90f;
        float radius = CurrentRadius();

        // 吸引范围内敌人
        for (int i = 0; i < Main.maxNPCs; i++) {
            NPC npc = Main.npc[i];
            if (!npc.active || !npc.CanBeChasedBy()) continue;
            float dist = Vector2.Distance(Projectile.Center, npc.Center);
            if (dist < radius * 2f && dist > 20f) {
                Vector2 pull = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero);
                float pullStrength = MathHelper.Lerp(3f, 0.5f, dist / (radius * 2f));
                npc.velocity += pull * pullStrength;
            }
        }

        // === 漩涡脉冲 (每 30 帧): 引爆范围烙印 (绽放伴生花瓣) + 视觉增亮 ===
        if (Timer % PulseInterval == 0 && Timer >= PulseInterval) {
            _pulseFlash = 1f;
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.9f, Pitch = 0.35f + Main.rand.NextFloat(-0.1f, 0.1f) }, Projectile.Center);
            if (Projectile.owner == Main.myPlayer) {
                ArrogantSylvanBloom.DetonateArea(Projectile.GetSource_FromThis(), Projectile.Center,
                    radius * 1.6f, Projectile.damage, 2f, Projectile.owner, petals: true);
            }
        }

        // 旋转叶片粒子 (减量)
        for (int i = 0; i < 3; i++) {
            float angle = Timer * 0.15f + i * MathHelper.TwoPi / 3f;
            float r = radius * Main.rand.NextFloat(0.3f, 1f);
            Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
            int dustType = i % 2 == 0 ? DustID.GrassBlades : DustID.JungleTorch;
            Dust d = Dust.NewDustPerfect(dustPos, dustType,
                new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 3f + Main.rand.NextVector2Circular(1f, 1f),
                40, default, 1.8f);
            d.noGravity = true;
        }

        // 金翠高亮粒子
        if (Timer % 8 == 0) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 edgePos = Projectile.Center + angle.ToRotationVector2() * radius;
            Dust glow = Dust.NewDustPerfect(edgePos, DustID.GoldFlame,
                Main.rand.NextVector2Circular(2f, 2f), 30, default, 2f);
            glow.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.4f, 1f, 0.35f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 600);
        target.AddBuff(BuffID.Venom, 300);
        ArrogantSylvanBrandNPC.AddStack(target);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        => VaultUtils.CircleIntersectsRectangle(Projectile.Center, CurrentRadius(), targetHitbox);

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = -0.2f }, Projectile.Center);
        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = 0.1f }, Projectile.Center);

        // 消散终曲: 最后一次引爆 (不再喷叶片弹幕)
        if (Projectile.owner == Main.myPlayer) {
            ArrogantSylvanBloom.DetonateArea(Projectile.GetSource_FromThis(), Projectile.Center,
                CurrentRadius() * 1.6f, Projectile.damage, 2f, Projectile.owner, petals: true);
        }

        for (int i = 0; i < 20; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(12f, 12f);
            int dustType = i % 3 == 0 ? DustID.GoldFlame : DustID.GrassBlades;
            Dust d = Dust.NewDustPerfect(Projectile.Center, dustType,
                vel, 30, default, 2.4f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        float radius = CurrentRadius();
        float fadeAlpha = Projectile.timeLeft < 30 ? Projectile.timeLeft / 30f : 1f;
        float scale = radius / 100f;

        // === 漩涡本体: 系列专属 Vortex 着色器 (宽环带填满漩涡盘面) ===
        ArrogantSylvanFX.DrawVortexBand(Projectile.Center, radius * 0.62f, radius * 0.52f,
            spin: 1.1f, pulse: _pulseFlash, intensity: fadeAlpha * 0.8f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D sparkle = ACMAsset.Sparkle;

        // 旋转的 8 道 SlashBurst 加强漩涡骨架
        for (int k = 0; k < 8; k++) {
            float bAngle = Timer * 0.08f + k * MathHelper.PiOver4;
            float bLen = scale * (0.35f + 0.08f * MathF.Sin(Timer * 0.12f + k));
            Color bColor = k % 2 == 0 ? ArrogantSylvanPalette.GoldBright : ArrogantSylvanPalette.JadeDeep;
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                bColor * (0.5f * fadeAlpha), bAngle,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.10f, bLen), SpriteEffects.None, 0);
        }

        // 外层柔光环 — 金 (统一金翠双色基调)
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(225, 185, 75) * (0.30f * fadeAlpha), 0f,
            sg.Size() * 0.5f, scale * 1.4f, SpriteEffects.None, 0);
        // 中层柔光环 — 翠
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(120, 230, 110) * (0.28f * fadeAlpha), 0f,
            sg.Size() * 0.5f, scale * 1.0f, SpriteEffects.None, 0);

        // 中心核心 (脉冲时白热)
        float pulse = 0.5f + 0.15f * MathF.Sin(Timer * 0.20f) + 0.4f * _pulseFlash;
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.WhiteHot * (0.45f * pulse * fadeAlpha), 0f,
            sg.Size() * 0.5f, scale * 0.4f, SpriteEffects.None, 0);

        // 旋转 Sparkle 装饰
        sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 140) * (0.40f * fadeAlpha),
            Timer * 0.10f,
            sparkle.Size() * 0.5f, scale * 0.6f, SpriteEffects.None, 0);
        sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.JadeDeep * (0.30f * fadeAlpha),
            -Timer * 0.07f,
            sparkle.Size() * 0.5f, scale * 0.8f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
