using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Celestias.Boss.Dazhengs.Items;
using AncientChineseMythology.Items.Weapons.DivineWoods;

namespace AncientChineseMythology.Items.Weapons.ArrogantDivineSylvans;

/// <summary>
/// 傲世神木·落叶风暴 - 暴力螺旋风暴型终极回旋镖
/// 掷出后在落点展开极速扩展螺旋风暴，卷碎一切
/// 按住攻击键维持风暴，松开后内爆坍缩→高速回收
/// 回程伤害×2，接住时屏幕震动 + 叶片冲击波
/// 每3次命中触发万木裁决：16花瓣爆发 + 范围藤蔓缠绕
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
/// 傲世旋叶弹幕 - 暴力螺旋风暴回旋镖
/// 投射→螺旋风暴(扩展)→内爆坍缩→高速回收
/// 全程高速运动，力量感拉满
/// </summary>
public class ArrogantSylvanChakramProj : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/ArrogantDivineSylvans/ArrogantDivineSylvanChakram";

    // Phase: 0 = Launching, 1 = Spiraling, 2 = Imploding, 3 = Recalling
    private ref float Phase => ref Projectile.ai[0];
    private ref float Timer => ref Projectile.ai[1];
    private ref float HitCounter => ref Projectile.localAI[0];
    private ref float SpiralAngle => ref Projectile.localAI[1];

    // ---- 投射 ----
    private const int LaunchDuration = 20;

    // ---- 螺旋风暴 ----
    private const float SpiralStartRadius = 40f;     // 起始半径
    private const float SpiralMaxRadius = 260f;      // 最大扩展半径
    private const float SpiralExpandRate = 3.5f;     // 每帧半径增长
    private const float SpiralAngularSpeed = 0.28f;  // 每帧角速度（极快旋转）
    private const int MaxSpiralDuration = 240;       // 最大风暴持续帧（4秒）

    // ---- 内爆坍缩 ----
    private const float ImplodeContractRate = 12f;   // 内爆收缩速率
    private const float ImplodeAngularSpeed = 0.50f; // 内爆旋转加速

    // ---- 回收 ----
    private const float RecallAccel = 3.0f;
    private const float MaxRecallSpeed = 50f;
    private const float CatchRadius = 50f;

    private Vector2 _stormCenter;    // 风暴中心点
    private float _spiralRadius;     // 当前螺旋半径
    private int _spiralTimer;
    private bool _caughtBurst;

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

        // 旋转速度：风暴阶段极速，回收阶段猛烈
        float rotSpeed = IsSpiraling ? 0.55f : (IsImploding ? 0.75f : (IsRecalling ? 0.70f : 0.40f));
        Projectile.rotation += rotSpeed;

        switch ((int)Phase) {
            case 0: HandleLaunching(owner); break;
            case 1: HandleSpiraling(owner); break;
            case 2: HandleImploding(owner); break;
            default: HandleRecalling(owner); break;
        }

        // 粒子：风暴阶段密集，其余正常
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

        // 风暴时在风暴中心产生旋转粒子环
        if (IsSpiraling && Timer % 3 == 0) {
            float pAngle = SpiralAngle + MathHelper.Pi; // 对侧
            Vector2 pOff = new Vector2(MathF.Cos(pAngle), MathF.Sin(pAngle)) * _spiralRadius * 0.6f;
            Dust rd = Dust.NewDustPerfect(_stormCenter + pOff,
                DustID.GrassBlades, pOff.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * 3f,
                60, default, 2.2f);
            rd.noGravity = true;
        }

        // 内爆时在中心产生向内收缩的粒子
        if (IsImploding && Timer % 2 == 0) {
            float randAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 spawnPos = _stormCenter + randAngle.ToRotationVector2() * (_spiralRadius + 40f);
            Vector2 inVel = (_stormCenter - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(6f, 12f);
            Dust id = Dust.NewDustPerfect(spawnPos, DustID.JungleTorch, inVel, 30, default, 2.5f);
            id.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.40f, 0.95f, 0.35f);

        // 风暴中心也发光
        if (IsSpiraling || IsImploding)
            Lighting.AddLight(_stormCenter, 0.3f, 0.8f, 0.25f);
    }

    private void HandleLaunching(Player owner) {
        if (owner.channel)
            owner.itemAnimation = 2;

        // 高速飞行，轻微减速
        Projectile.velocity *= 0.97f;

        if (Timer >= LaunchDuration || Projectile.velocity.Length() < 4f) {
            // 无论是否按住，都进入螺旋。按住 = 维持风暴，松手 = 风暴后自动回收
            Phase = 1f;
            Timer = 0;
            _spiralTimer = 0;
            _stormCenter = Projectile.Center;
            _spiralRadius = SpiralStartRadius;
            SpiralAngle = Projectile.velocity.ToRotation();
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

        // 计算螺旋位置
        Vector2 newPos = _stormCenter + new Vector2(MathF.Cos(SpiralAngle), MathF.Sin(SpiralAngle)) * _spiralRadius;
        Projectile.velocity = newPos - Projectile.Center;
        Projectile.Center = newPos;

        // 每15帧释放旋风叶片（从旋叶位置向外飞散）
        if (_spiralTimer % 15 == 0 && Projectile.owner == Main.myPlayer) {
            Vector2 outDir = (Projectile.Center - _stormCenter).SafeNormalize(Vector2.UnitX);
            Vector2 leafVel = outDir * Main.rand.NextFloat(6f, 10f) +
                              outDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-3f, 3f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                leafVel, ModContent.ProjectileType<ArrogantSylvanWhirlLeaf>(),
                Projectile.damage / 4, 1.5f, Projectile.owner);
        }

        // 松手或超时 → 内爆坍缩
        if (!owner.channel || _spiralTimer >= MaxSpiralDuration) {
            Phase = 2f;
            Timer = 0;
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = 0.6f }, _stormCenter);
        }
    }

    private void HandleImploding(Player owner) {
        // 急速收缩半径 + 加速旋转
        SpiralAngle += ImplodeAngularSpeed;
        _spiralRadius -= ImplodeContractRate;

        if (_spiralRadius <= 0f) {
            // 坍缩完成 → 中心爆发 + 进入回收
            _spiralRadius = 0f;
            Phase = 3f;
            Timer = 0;

            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 1.2f, Pitch = -0.3f }, _stormCenter);

            // 内爆冲击波：8道旋风叶片从中心炸开
            if (Projectile.owner == Main.myPlayer) {
                for (int i = 0; i < 10; i++) {
                    float angle = MathHelper.TwoPi * i / 10;
                    Vector2 leafVel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 13f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), _stormCenter,
                        leafVel, ModContent.ProjectileType<ArrogantSylvanWhirlLeaf>(),
                        Projectile.damage / 3, 2.5f, Projectile.owner);
                }

                ScreenShakePlayer shaker = owner.GetModPlayer<ScreenShakePlayer>();
                shaker.ShakeScreen(6f, 8);
            }

            // 爆发粒子
            for (int i = 0; i < 30; i++) {
                Dust d = Dust.NewDustPerfect(_stormCenter, DustID.JungleTorch,
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

                if (Projectile.owner == Main.myPlayer) {
                    ScreenShakePlayer shaker = owner.GetModPlayer<ScreenShakePlayer>();
                    shaker.ShakeScreen(10f, 14);
                }

                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 1.3f, Pitch = 0.5f }, owner.Center);

                for (int i = 0; i < 30; i++) {
                    Dust d = Dust.NewDustPerfect(owner.Center, DustID.JungleTorch,
                        Main.rand.NextVector2Circular(11f, 11f), 20, default, 2.8f);
                    d.noGravity = true;
                }
            }
            Projectile.Kill();
            return;
        }
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        // 螺旋风暴时：整个风暴范围都是杀伤区
        if (IsSpiraling || IsImploding) {
            float radius = _spiralRadius + 30f; // 略大于当前螺旋半径
            Vector2 center = _stormCenter;
            float closestX = MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right);
            float closestY = MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom);
            float dx = center.X - closestX;
            float dy = center.Y - closestY;
            return (dx * dx + dy * dy) <= (radius * radius);
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
        HitCounter++;
        target.AddBuff(BuffID.Poisoned, 600);
        target.AddBuff(BuffID.Venom, 300);

        int dustAmt = IsRecalling ? 22 : (IsImploding ? 18 : 14);
        for (int i = 0; i < dustAmt; i++) {
            Dust burst = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(8f, 8f), 40, default, 2.5f);
            burst.noGravity = true;
        }

        // 每3次命中触发万木裁决
        if (HitCounter % 3 == 0) {
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 1.2f, Pitch = 0.3f }, target.Center);

            if (Projectile.owner == Main.myPlayer) {
                for (int i = 0; i < 16; i++) {
                    float angle = MathHelper.TwoPi * i / 16;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(7f, 12f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center,
                        vel, ModContent.ProjectileType<ArrogantSylvanVerdictPetal>(),
                        damageDone / 2, 3f, Projectile.owner);
                }
            }

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC nearby = Main.npc[i];
                if (!nearby.CanBeChasedBy()) continue;
                if (Vector2.Distance(target.Center, nearby.Center) < 600f) {
                    nearby.AddBuff(BuffID.Poisoned, 600);
                    nearby.AddBuff(BuffID.Venom, 300);
                }
            }
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        Vector2 origin = tex.Size() / 2f;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        // 拖尾 - 风暴阶段更粗更亮
        for (int i = 0; i < Projectile.oldPos.Length; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float progress = 1f - (float)i / Projectile.oldPos.Length;
            Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
            float alpha = IsSpiraling ? 0.55f : (IsImploding ? 0.65f : (IsRecalling ? 0.60f : 0.50f));
            Color trailColor = Color.Lerp(new Color(220, 255, 100), new Color(40, 200, 60), progress)
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
                new Color(220, 255, 100) * 0.70f,
                Projectile.velocity.ToRotation(), wave.Size() * 0.5f,
                new Vector2(0.75f, 0.30f), SpriteEffects.None, 0);
        }

        // 风暴中心涡旋光圈
        if (IsSpiraling || IsImploding) {
            Texture2D sg2 = ACMAsset.SoftGlow;
            // 外圈：表示风暴范围
            float stormPulse = 0.15f + 0.06f * MathF.Sin(_spiralTimer * 0.15f);
            float stormGlowScale = (_spiralRadius + 30f) / (sg2.Width * 0.5f);
            sb.Draw(sg2, _stormCenter - Main.screenPosition, null,
                new Color(100, 255, 80) * stormPulse, 0f,
                sg2.Size() * 0.5f,
                stormGlowScale, SpriteEffects.None, 0);
            // 内核：中心亮点
            sb.Draw(sg2, _stormCenter - Main.screenPosition, null,
                new Color(220, 255, 150) * 0.30f, 0f,
                sg2.Size() * 0.5f,
                0.35f, SpriteEffects.None, 0);
        }

        // 内爆时中心闪烁冲击波
        if (IsImploding) {
            Texture2D wave2 = ACMAsset.GlaciateWave;
            float implodeFlash = MathHelper.Clamp(1f - _spiralRadius / SpiralMaxRadius, 0f, 1f);
            for (int a = 0; a < 4; a++) {
                float angle = MathHelper.PiOver2 * a + SpiralAngle * 0.5f;
                sb.Draw(wave2, _stormCenter - Main.screenPosition, null,
                    new Color(200, 255, 80) * implodeFlash * 0.4f,
                    angle, wave2.Size() * 0.5f,
                    new Vector2(0.4f, 0.15f), SpriteEffects.None, 0);
            }
        }

        Texture2D sg = ACMAsset.SoftGlow;
        float pulse = 0.40f + 0.15f * MathF.Sin(Timer * 0.14f);
        float glowScale = IsSpiraling ? 0.80f : (IsRecalling ? 0.95f : 0.70f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 100) * pulse, 0f,
            sg.Size() * 0.5f,
            glowScale, SpriteEffects.None, 0);

        Color glowColor = new Color(220, 255, 100) * 0.32f;
        glowColor.A = 0;
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            glowColor, Projectile.rotation, origin, Projectile.scale * 1.25f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        // 残影绘制 - 在AlphaBlend模式下绘制半透明完整精灵副本，高速阶段更浓烈
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
        for (int i = 0; i < 20; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(7f, 7f), 40, default, 2.5f);
            d.noGravity = true;
        }
    }
}

/// <summary>
/// 傲世旋风叶片 - 冲刺释放的叶片弹幕
/// </summary>
public class ArrogantSylvanWhirlLeaf : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Leaf;

    private float _timer;

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
            float closestDist = 600f;
            int targetIdx = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < closestDist) { closestDist = d; targetIdx = i; }
            }
            if (targetIdx >= 0) {
                Vector2 dir = Projectile.DirectionTo(Main.npc[targetIdx].Center);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 20f, 0.12f);
            }
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
/// 傲世裁决花瓣 - 万木裁决时释放的强力花瓣弹幕
/// </summary>
public class ArrogantSylvanVerdictPetal : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPetal;

    private float _timer;

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
            float closestDist = 800f;
            int targetIdx = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < closestDist) { closestDist = d; targetIdx = i; }
            }
            if (targetIdx >= 0) {
                Vector2 dir = Projectile.DirectionTo(Main.npc[targetIdx].Center);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 22f, 0.10f);
            }
            else {
                Projectile.velocity *= 1.02f;
            }
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
        for (int i = 0; i < 7; i++) {
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
