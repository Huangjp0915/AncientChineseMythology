using AncientChineseMythology.Helpers;
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
    private int _healFlash;

    private const int OutgoingDuration = 28;
    private const float ReturnAccel = 2.0f;
    private const float MaxReturnSpeed = 34f;
    private const float CatchRadius = 42f;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 0;
        ProjectileID.Sets.TrailCacheLength[Type] = 16;
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

        if (_healFlash > 0)
            _healFlash--;

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
            _healFlash = 12; // 吸血血珠闪
        }

        target.AddBuff(BuffID.Ichor, 300);
        HitCounter++;

        // 命中血肉爆发
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.Profane, scale: 1f, owner: Projectile.owner);

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

        // 统一双层暗红血肉拖尾; 返回加速时内层转纯绯红
        Color inner = _isReturning ? new Color(248, 28, 120) : new Color(252, 58, 142);
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 13f,
            outerColor: new Color(92, 4, 30),
            innerColor: inner,
            uvScroll: -Main.GlobalTimeWrappedHourly * (_isReturning ? 2.2f : 1.4f));

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        // 回程冲击波
        if (_isReturning && Projectile.velocity.Length() > 10f) {
            Texture2D wave = ACMAsset.GlaciateWave;
            sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                new Color(150, 12, 20) * 0.50f,
                Projectile.velocity.ToRotation(), wave.Size() * 0.5f,
                new Vector2(0.5f, 0.20f), SpriteEffects.None, 0);
        }

        Texture2D sg = ACMAsset.SoftGlow;
        float pulse = 0.35f + 0.12f * MathF.Sin(Timer * 0.14f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(180, 30, 30) * pulse, 0f,
            sg.Size() * 0.5f,
            _isReturning ? 0.80f : 0.60f, SpriteEffects.None, 0);

        // 吸血血珠闪 (Sparkle)
        if (_healFlash > 0) {
            float hf = _healFlash / 12f;
            Texture2D sparkle = ACMAsset.Sparkle;
            sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
                new Color(252, 58, 142) * (0.85f * hf),
                Projectile.rotation, sparkle.Size() * 0.5f,
                MathHelper.Lerp(0.20f, 0.85f, hf), SpriteEffects.None, 0);
        }

        Color glowColor = new Color(150, 20, 20) * 0.30f;
        glowColor.A = 0;
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

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
        ProjectileID.Sets.TrailingMode[Type] = 0;
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
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

        // 畸变眼球杖持续引导时: 玩家↔触手的血肉连接光束
        Player owner = Main.player[Projectile.owner];
        if (owner.active && owner.channel && !owner.dead
            && owner.HeldItem?.type == ModContent.ItemType<AberrantEyeStaff>()) {
            Vector2 hand = owner.MountedCenter
                + owner.DirectionTo(Projectile.Center).SafeNormalize(Vector2.UnitX) * 28f;
            ACMShaders.DrawBeam(hand, Projectile.Center, halfWidth: 4.5f,
                core: new Color(252, 58, 142), edge: new Color(92, 4, 30), intensity: 0.6f);
        }

        // 统一双层暗红血肉拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 8f,
            outerColor: new Color(92, 4, 30), innerColor: new Color(252, 58, 142),
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D sg = ACMAsset.SoftGlow;
        float pulse = 0.50f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.20f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(180, 30, 25) * (0.55f * pulse), 0f,
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
