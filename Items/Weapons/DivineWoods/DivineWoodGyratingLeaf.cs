using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.DivineWoods;

/// <summary>
/// 神木旋叶 - 主动投掷的强力回旋镖
/// 掷出后减速翻转，自动加速返回玩家手中
/// 回程伤害×1.5，命中释放旋风叶片
/// 每5次命中触发自然裁决：释放花瓣环形弹幕
/// </summary>
public class DivineWoodGyratingLeaf : ModItem
{
    public override void SetDefaults() {
        Item.damage = 175;
        Item.crit = 16;
        Item.DamageType = DamageClass.Melee;
        Item.width = 40;
        Item.height = 40;
        Item.useTime = 28;
        Item.useAnimation = 28;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 7f;
        Item.value = Item.buyPrice(gold: 50);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<DivineWoodGyratingLeafProj>();
        Item.shootSpeed = 22f;
    }

    public override bool CanUseItem(Player player) {
        return player.ownedProjectileCounts[ModContent.ProjectileType<DivineWoodGyratingLeafProj>()] < 1;
    }
}

/// <summary>
/// 神木旋叶弹幕 - 主动投掷回旋镖
/// 掷出→减速→翻转→加速返回，回程伤害×1.5
/// 命中释放3片旋风叶，每5hit触发花瓣裁决
/// </summary>
public class DivineWoodGyratingLeafProj : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/DivineWoods/DivineWoodGyratingLeaf";

    // Phase: 0 = Outgoing（掷出）, 1 = Returning（回程）
    private ref float Phase => ref Projectile.ai[0];
    private ref float Timer => ref Projectile.ai[1];
    private ref float HitCounter => ref Projectile.localAI[0];

    private const int OutgoingDuration = 30;     // 飞出持续帧数
    private const float ReturnAccel = 1.8f;      // 回程加速度
    private const float MaxReturnSpeed = 32f;    // 回程最大速度
    private const float CatchRadius = 40f;       // 接住判定半径

    private bool _isReturning => Phase >= 1f;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[Type] = 18;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults() {
        Projectile.width = 40;
        Projectile.height = 40;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 600;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override void AI() {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead) { Projectile.Kill(); return; }

        Timer++;

        // 高速旋转 - 回程时更快
        float rotSpeed = _isReturning ? 0.55f : 0.35f;
        Projectile.rotation += rotSpeed;

        if (!_isReturning) {
            // === 掷出阶段 ===
            // 逐渐减速
            float decel = MathHelper.Lerp(0.96f, 0.92f, Math.Min(Timer / OutgoingDuration, 1f));
            Projectile.velocity *= decel;

            // 到时间或速度过低则翻转进入回程
            if (Timer >= OutgoingDuration || Projectile.velocity.Length() < 2f) {
                Phase = 1f;
                Timer = 0;
                SoundEngine.PlaySound(SoundID.Item7 with { Volume = 0.6f, Pitch = 0.8f }, Projectile.Center);

                // 翻转时释放4片旋风叶片
                if (Projectile.owner == Main.myPlayer) {
                    for (int i = 0; i < 4; i++) {
                        float angle = MathHelper.TwoPi * i / 4 + Main.rand.NextFloat(0.3f);
                        Vector2 leafVel = angle.ToRotationVector2() * 5f;
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                            leafVel, ModContent.ProjectileType<DivineWoodWhirlLeaf>(),
                            Projectile.damage / 3, 1f, Projectile.owner);
                    }
                }
            }
        }
        else {
            // === 回程阶段 ===
            Vector2 toPlayer = owner.Center - Projectile.Center;
            float dist = toPlayer.Length();
            Vector2 dir = toPlayer.SafeNormalize(Vector2.Zero);

            // 加速飞向玩家
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * MaxReturnSpeed, 0.12f);
            if (Projectile.velocity.Length() < MaxReturnSpeed)
                Projectile.velocity += dir * ReturnAccel;

            // 接住
            if (dist < CatchRadius) {
                Projectile.Kill();
                return;
            }
        }

        // 粒子特效
        if (Main.rand.NextBool(_isReturning ? 2 : 3)) {
            Dust d = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(15, 15),
                DustID.JungleTorch, -Projectile.velocity * 0.15f, 60, default,
                Main.rand.NextFloat(1f, 1.8f));
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.25f, 0.7f, 0.25f);
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
        // 回程伤害×1.5
        if (_isReturning)
            modifiers.SourceDamage *= 1.5f;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        HitCounter++;
        target.AddBuff(BuffID.Poisoned, 300);

        // 命中爆发粒子
        int dustCount = _isReturning ? 14 : 10;
        for (int i = 0; i < dustCount; i++) {
            Dust burst = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(6f, 6f), 60, default, 1.8f);
            burst.noGravity = true;
        }

        // 每次命中释放3片旋风叶
        if (Projectile.owner == Main.myPlayer) {
            for (int i = 0; i < 3; i++) {
                float angle = MathHelper.TwoPi * i / 3 + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 leafVel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 7f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center,
                    leafVel, ModContent.ProjectileType<DivineWoodWhirlLeaf>(),
                    Projectile.damage / 3, 1f, Projectile.owner);
            }
        }

        // 每5次命中触发自然裁决：释放花瓣弹幕环
        if (HitCounter % 5 == 0) {
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 1f, Pitch = 0.5f }, target.Center);

            if (Projectile.owner == Main.myPlayer) {
                for (int i = 0; i < 12; i++) {
                    float angle = MathHelper.TwoPi * i / 12;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 9f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center,
                        vel, ModContent.ProjectileType<DivineWoodVerdictPetal>(),
                        damageDone / 2, 2f, Projectile.owner);
                }
            }

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC nearby = Main.npc[i];
                if (!nearby.CanBeChasedBy()) continue;
                if (Vector2.Distance(target.Center, nearby.Center) < 450f) {
                    nearby.AddBuff(BuffID.Poisoned, 300);
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

        // 拖尾
        for (int i = 0; i < Projectile.oldPos.Length; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float progress = 1f - (float)i / Projectile.oldPos.Length;
            Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
            Color trailColor = Color.Lerp(new Color(50, 200, 60), new Color(180, 255, 190), progress)
                * progress * (_isReturning ? 0.60f : 0.45f);
            trailColor.A = 0;
            float trailScale = Projectile.scale * progress * (_isReturning ? 0.95f : 0.85f);
            sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
        }

        // 回程时显示冲击波
        if (_isReturning && Projectile.velocity.Length() > 10f) {
            Texture2D wave = ACMAsset.GlaciateWave;
            sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                new Color(60, 220, 80) * 0.50f,
                Projectile.velocity.ToRotation(), wave.Size() * 0.5f,
                new Vector2(0.5f, 0.20f), SpriteEffects.None, 0);
        }

        Texture2D sg = ACMAsset.SoftGlow;
        float pulse = 0.35f + 0.1f * MathF.Sin(Timer * 0.12f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(80, 220, 90) * pulse, 0f,
            sg.Size() * 0.5f,
            _isReturning ? 0.75f : 0.60f, SpriteEffects.None, 0);

        Color glowColor = new Color(60, 200, 80) * 0.28f;
        glowColor.A = 0;
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        // 残影绘制 - 在AlphaBlend模式下绘制半透明完整精灵副本
        for (int i = 1; i < Projectile.oldPos.Length; i += 2) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float progress = 1f - (float)i / Projectile.oldPos.Length;
            Vector2 afterimagePos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
            float afterAlpha = progress * progress * (_isReturning ? 0.50f : 0.35f);
            Color afterColor = Color.Lerp(lightColor, new Color(140, 255, 170), 0.25f) * afterAlpha;
            float afterScale = Projectile.scale * MathHelper.Lerp(0.60f, 0.95f, progress);
            sb.Draw(tex, afterimagePos, null, afterColor, Projectile.oldRot[i], origin, afterScale, SpriteEffects.None, 0);
        }

        Color mainColor = Color.Lerp(lightColor, new Color(180, 255, 190), 0.3f);
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

        return false;
    }

    public override void OnKill(int timeLeft) {
        for (int i = 0; i < 15; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(5f, 5f), 60, default, 2f);
            d.noGravity = true;
        }
    }
}

/// <summary>
/// 旋风叶片 - 冲刺时释放的旋转叶片，使用原版Leaf纹理
/// 短暂飞行后追踪敌人
/// </summary>
public class DivineWoodWhirlLeaf : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Leaf;

    private float _timer;

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 5;
    }

    public override void SetDefaults() {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 90;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation += 0.25f * Projectile.direction;
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 5) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        // 前30帧螺旋扩散，然后追踪
        if (_timer > 30) {
            float closestDist = 500f;
            int targetIdx = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < closestDist) { closestDist = d; targetIdx = i; }
            }
            if (targetIdx >= 0) {
                Vector2 dir = Projectile.DirectionTo(Main.npc[targetIdx].Center);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 16f, 0.10f);
            }
        }
        else {
            Projectile.velocity *= 0.96f;
        }

        Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
            -Projectile.velocity * 0.05f, 100, default, 0.8f);
        trail.noGravity = true;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 180);
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        int fh = tex.Height / Main.projFrames[Type];
        Rectangle src = new(0, Projectile.frame * fh, tex.Width, fh);
        Vector2 origin = new(tex.Width / 2f, fh / 2f);
        Color tint = Color.Lerp(lightColor, new Color(100, 255, 120), 0.4f);
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            tint, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
        return false;
    }
}

/// <summary>
/// 裁决花瓣 - 5次命中裁决时释放的花瓣弹幕
/// 使用原版FlowerPetal纹理，扩散后追踪最近敌人
/// </summary>
public class DivineWoodVerdictPetal : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPetal;

    private float _timer;

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 3;
    }

    public override void SetDefaults() {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 120;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 5) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        // 前20帧减速扩散，然后加速追踪
        if (_timer < 20) {
            Projectile.velocity *= 0.95f;
        }
        else {
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
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 18f, 0.08f);
            }
            else {
                Projectile.velocity *= 1.02f;
            }
        }

        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                -Projectile.velocity * 0.05f, 100, default, 1.0f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 300);
        target.AddBuff(BuffID.Venom, 120);
        for (int i = 0; i < 5; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(4f, 4f), 60, default, 1.5f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        int fh = tex.Height / Main.projFrames[Type];
        Rectangle src = new(0, Projectile.frame * fh, tex.Width, fh);
        Vector2 origin = new(tex.Width / 2f, fh / 2f);
        Color tint = Color.Lerp(lightColor, new Color(160, 255, 180), 0.35f);
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            tint, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);
        return false;
    }
}
