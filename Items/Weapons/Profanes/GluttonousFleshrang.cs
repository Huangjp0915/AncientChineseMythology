using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Profanes;

/// <summary>
/// 贪婪回肉刃 - 战士回旋镖，掷出飞行时吞噬沿途血肉
/// 掷出→减速→高速返回，返回伤害×1.8
/// 命中敌人时吸取生命值，飞行中喷洒血液拖尾
/// 每4次命中释放一圈8道血肉触手追踪弹幕
/// </summary>
public class GluttonousFleshrang : ModItem
{
    public override void SetDefaults() {
        Item.damage = 1350;
        Item.crit = 16;
        Item.DamageType = DamageClass.Melee;
        Item.width = 40;
        Item.height = 40;
        Item.useTime = 24;
        Item.useAnimation = 24;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 7f;
        Item.value = Item.buyPrice(gold: 80);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<GluttonousFleshrangProj>();
        Item.shootSpeed = 22f;
    }

    public override bool CanUseItem(Player player) {
        return player.ownedProjectileCounts[ModContent.ProjectileType<GluttonousFleshrangProj>()] < 1;
    }
}

/// <summary>
/// 贪婪回肉刃弹幕 - 掷出→减速→自动返回
/// 返回时吸收生命，带血肉拖尾和残影
/// </summary>
public class GluttonousFleshrangProj : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/Profanes/GluttonousFleshrang";

    private ref float Timer => ref Projectile.ai[0];
    private ref float HitCounter => ref Projectile.ai[1];
    private bool _isReturning;

    private const int OutgoingDuration = 28;
    private const float ReturnAccel = 2.0f;
    private const float MaxReturnSpeed = 34f;
    private const float CatchRadius = 42f;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 20;
    }

    public override void SetDefaults() {
        Projectile.width = 36;
        Projectile.height = 36;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 600;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override void AI() {
        Player owner = Main.player[Projectile.owner];
        Timer++;

        Projectile.rotation += (_isReturning ? 0.45f : 0.30f) * Projectile.direction;

        if (!_isReturning) {
            // 前进阶段：逐渐减速
            float decel = MathHelper.Lerp(1f, 0.92f, Math.Min(Timer / OutgoingDuration, 1f));
            Projectile.velocity *= decel;

            if (Timer >= OutgoingDuration || Projectile.velocity.Length() < 3f) {
                _isReturning = true;
                Projectile.tileCollide = false;
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.4f, Volume = 0.6f }, Projectile.Center);
            }
        }
        else {
            // 返回阶段：加速飞向玩家
            Vector2 toOwner = owner.Center - Projectile.Center;
            Vector2 dir = toOwner.SafeNormalize(Vector2.Zero);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * MaxReturnSpeed, 0.08f);
            float speed = Projectile.velocity.Length();
            if (speed < MaxReturnSpeed)
                Projectile.velocity += dir * ReturnAccel;

            if (toOwner.Length() < CatchRadius) {
                Projectile.Kill();
                SoundEngine.PlaySound(SoundID.NPCHit9 with { Pitch = 0.2f, Volume = 0.5f }, owner.Center);
            }
        }

        // 血液粒子拖尾
        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12, 12),
                DustID.Blood, -Projectile.velocity * 0.08f, 0, default, 1.6f);
            d.noGravity = true;
        }

        // 暗红光照
        Lighting.AddLight(Projectile.Center, 0.5f, 0.08f, 0.06f);
    }

    public override bool OnTileCollide(Vector2 oldVelocity) {
        _isReturning = true;
        Projectile.tileCollide = false;
        SoundEngine.PlaySound(SoundID.Dig, Projectile.Center);
        return false;
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
        if (_isReturning)
            modifiers.SourceDamage *= 1.8f;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        // 吸血效果
        Player owner = Main.player[Projectile.owner];
        int healAmount = Math.Min(damageDone / 40, 25);
        if (healAmount > 0) {
            owner.Heal(healAmount);
            owner.HealEffect(healAmount);
        }

        target.AddBuff(BuffID.Ichor, 300);
        HitCounter++;

        // 血液粒子爆发
        for (int i = 0; i < 10; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                Main.rand.NextVector2Circular(6f, 6f), 0, default, 2.0f);
            d.noGravity = true;
        }

        // 每4次命中释放8道血肉触手
        if (HitCounter % 4 == 0 && Projectile.owner == Main.myPlayer) {
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.6f, Volume = 0.7f }, target.Center);
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(7f, 10f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, vel,
                    ModContent.ProjectileType<ProfaneTendrilChaser>(),
                    Projectile.damage / 3, 2f, Projectile.owner);
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

        // 血肉拖尾
        for (int i = 0; i < Projectile.oldPos.Length; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float progress = 1f - (float)i / Projectile.oldPos.Length;
            Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
            Color trailColor = Color.Lerp(new Color(200, 30, 20), new Color(255, 140, 120), progress)
                * progress * (_isReturning ? 0.60f : 0.42f);
            trailColor.A = 0;
            float trailScale = Projectile.scale * progress * (_isReturning ? 0.95f : 0.82f);
            sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
        }

        // 回程冲击波
        if (_isReturning && Projectile.velocity.Length() > 10f) {
            Texture2D wave = ACMAsset.GlaciateWave;
            sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                new Color(220, 40, 30) * 0.50f,
                Projectile.velocity.ToRotation(), wave.Size() * 0.5f,
                new Vector2(0.5f, 0.20f), SpriteEffects.None, 0);
        }

        Texture2D sg = ACMAsset.SoftGlow;
        float pulse = 0.35f + 0.12f * MathF.Sin(Timer * 0.14f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(220, 50, 40) * pulse, 0f,
            sg.Size() * 0.5f,
            _isReturning ? 0.80f : 0.60f, SpriteEffects.None, 0);

        Color glowColor = new Color(200, 40, 30) * 0.30f;
        glowColor.A = 0;
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        // 残影
        for (int i = 1; i < Projectile.oldPos.Length; i += 2) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float progress = 1f - (float)i / Projectile.oldPos.Length;
            Vector2 afterPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
            float afterAlpha = progress * progress * (_isReturning ? 0.50f : 0.32f);
            Color afterColor = Color.Lerp(lightColor, new Color(255, 160, 140), 0.25f) * afterAlpha;
            float afterScale = Projectile.scale * MathHelper.Lerp(0.55f, 0.95f, progress);
            sb.Draw(tex, afterPos, null, afterColor, Projectile.oldRot[i], origin, afterScale, SpriteEffects.None, 0);
        }

        Color mainColor = Color.Lerp(lightColor, new Color(255, 200, 190), 0.25f);
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

        return false;
    }

    public override void OnKill(int timeLeft) {
        for (int i = 0; i < 15; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                Main.rand.NextVector2Circular(5f, 5f), 0, default, 2f);
            d.noGravity = true;
        }
    }
}

/// <summary>
/// 亵渎血肉触手 - 追踪弹幕，使用原版BloodNautilus的Eye纹理
/// 从命中点释放，螺旋扩散后追踪最近敌人
/// </summary>
public class ProfaneTendrilChaser : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BloodNautilusShot;

    private float _timer;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 8;
    }

    public override void SetDefaults() {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 100;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

        if (_timer > 25) {
            float closestDist = 550f;
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

        // 血滴粒子
        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                -Projectile.velocity * 0.06f, 0, default, 1.0f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.3f, 0.05f, 0.04f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 180);
        for (int i = 0; i < 5; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.5f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D sg = ACMAsset.SoftGlow;
        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.45f;
            sb.Draw(sg,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(200, 30, 20) * a, 0f,
                sg.Size() * 0.5f,
                0.25f, SpriteEffects.None, 0);
        }

        float pulse = 0.50f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.20f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(220, 50, 30) * (0.55f * pulse), 0f,
            sg.Size() * 0.5f,
            0.30f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D tex = TextureAssets.Projectile[Type].Value;
        Vector2 origin = tex.Size() / 2f;
        Color tint = Color.Lerp(lightColor, new Color(255, 130, 120), 0.35f);
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            tint, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
        return false;
    }
}
