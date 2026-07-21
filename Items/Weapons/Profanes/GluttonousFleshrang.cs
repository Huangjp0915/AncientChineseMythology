using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Profanes;

/// <summary>
/// 贪噬肉旋 - 战士回旋镖，掷出飞行时吞噬沿途血肉。
/// 掷出→减速→高速返回，返回伤害×1.8；命中吸血并叠剖检印。
/// 饱食机制：单程命中≥3次后接住 → 获得"饱食"6秒（治疗15，掷出的镖膨胀×1.28、伤害×1.2、吸血上限提升）。
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

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        // 饱食态: 掷出膨胀镖 (伤害×1.2, ai[2]=1 标记同步各端)
        bool satiated = player.HasBuff(ModContent.BuffType<GluttonousSatietyBuff>());
        if (satiated)
            damage = (int)(damage * 1.2f);
        Projectile.NewProjectile(source, position, velocity, type, damage, knockback,
            player.whoAmI, 0f, 0f, satiated ? 1f : 0f);

        // launch is a set: 出手瞬间玩家微后坐 + 出手闪
        if (player.whoAmI == Main.myPlayer)
            player.velocity -= velocity.SafeNormalize(Vector2.Zero) * 1f;
        return false;
    }
}

/// <summary>饱食 - 贪噬肉旋吃饱后的增益（掷出膨胀镖）。</summary>
public class GluttonousSatietyBuff : ModBuff
{
    public override string Texture => "Terraria/Images/Buff_" + BuffID.WellFed;

    public override void SetStaticDefaults() {
        Language.GetOrRegister("Mods.AncientChineseMythology.Buffs.GluttonousSatietyBuff.DisplayName",
            () => "Satiated");
        Language.GetOrRegister("Mods.AncientChineseMythology.Buffs.GluttonousSatietyBuff.Description",
            () => "The fleshrang is engorged: bigger, hungrier, deadlier");
        Main.buffNoSave[Type] = true;
    }
}

/// <summary>
/// 贪噬肉旋弹幕 - 掷出→减速→自动返回。
/// ai[2]=1 表示饱食膨胀态（×1.28 体积）；返程吸血、心跳呼吸缩放。
/// </summary>
public class GluttonousFleshrangProj : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/Profanes/GluttonousFleshrang";

    private ref float Timer => ref Projectile.ai[0];
    private ref float HitCounter => ref Projectile.ai[1];
    private bool Engorged => Projectile.ai[2] > 0.5f;
    private bool _isReturning;
    private int _healFlash;

    private const int OutgoingDuration = 28;
    private const float ReturnAccel = 2.0f;
    private const float MaxReturnSpeed = 34f;
    private const float CatchRadius = 42f;
    private const int SatietyHits = 3; // 单程命中此数以上 → 接住触发饱食

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
        // 心跳呼吸 + 饱食膨胀
        Projectile.scale = (Engorged ? 1.28f : 1f) * (1f + ProfaneCommon.Heartbeat() * 0.05f);

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
                OnCaught(owner);
                Projectile.Kill();
            }
        }

        // 血液粒子拖尾 (饱食态滴得更凶)
        if (Main.rand.NextBool(Engorged ? 1 : 2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12, 12),
                DustID.Blood, -Projectile.velocity * 0.08f, 0, default, Engorged ? 2.0f : 1.6f);
            d.noGravity = !Engorged; // 饱食态血滴受重力下坠 (进食过量的淌血感)
        }

        if (_healFlash > 0)
            _healFlash--;

        Lighting.AddLight(Projectile.Center, 0.5f, 0.08f, 0.06f);
    }

    private void OnCaught(Player owner) {
        SoundEngine.PlaySound(SoundID.NPCHit9 with { Pitch = 0.2f, Volume = 0.5f }, owner.Center);
        if (HitCounter < SatietyHits || Projectile.owner != Main.myPlayer)
            return;

        // 饱食: 打嗝湿裂音 + 治疗 + buff (影响 6 秒内的后续掷出)
        ProfaneCommon.PlaySquelch(owner.Center, 1.1f, -0.45f);
        owner.AddBuff(ModContent.BuffType<GluttonousSatietyBuff>(), 360);
        if (owner.statLife < owner.statLifeMax2)
            owner.Heal(15);
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
        // 吸血效果 (饱食态上限提升)
        Player owner = Main.player[Projectile.owner];
        int healCap = Engorged ? 35 : 25;
        int healAmount = Math.Min(damageDone / 40, healCap);
        if (healAmount > 0) {
            owner.Heal(healAmount);
            owner.HealEffect(healAmount);
            _healFlash = 12; // 吸血血珠闪
        }

        target.AddBuff(BuffID.Ichor, 300);
        HitCounter++;

        // 命中反馈栈: burst + 微震 + 湿裂 (返程更重)
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.Profane, scale: _isReturning ? 1.2f : 1f, owner: Projectile.owner);
        WeaponVFX.AddScreenShake(target.Center, _isReturning ? 2f : 1.5f);
        ProfaneCommon.PlaySquelch(target.Center, 0.8f, _isReturning ? 0.1f : -0.1f);

        // 剖检印 +1; 摘取伤害 = 面板 ×2
        ProfaneCommon.AddMark(target, Projectile, 1, Projectile.damage * 2);

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
                    Projectile.damage / 3, 2f, Projectile.owner, 0f, ProfaneTendrilChaser.ClassMelee);
            }
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        Vector2 origin = tex.Size() / 2f;

        // 统一双层暗红血肉拖尾; 返回加速时内层提亮
        Color inner = _isReturning ? ProfaneCommon.BloodBright : ProfaneCommon.FleshMid;
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 13f * (Engorged ? 1.28f : 1f),
            outerColor: ProfaneCommon.FleshDark,
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
        float pulse = 0.35f + 0.25f * ProfaneCommon.Heartbeat();
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(180, 30, 30) * pulse, 0f,
            sg.Size() * 0.5f,
            (_isReturning ? 0.80f : 0.60f) * Projectile.scale, SpriteEffects.None, 0);

        // 吸血血珠闪 (Sparkle)
        if (_healFlash > 0) {
            float hf = _healFlash / 12f;
            Texture2D sparkle = ACMAsset.Sparkle;
            sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
                ProfaneCommon.BloodBright * (0.85f * hf),
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
/// 亵渎血肉触手 - 系列共享追踪弹幕（肉旋/搐筋弓/脏器枪/肉典/眼球杖引用）。
/// ai[1]=伤害类型码（0近战/1远程/2魔法, 由生成武器传入, 各端从同步 ai 一致解析）。
/// 从命中点释放，螺旋扩散后追踪最近敌人。
/// </summary>
public class ProfaneTendrilChaser : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BloodNautilusShot;

    /// <summary>ai[1] 伤害类型码。</summary>
    public const float ClassMelee = 0f;
    public const float ClassRanged = 1f;
    public const float ClassMagic = 2f;

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
        // 伤害类型随生成武器 (同步 ai → 各端一致)
        Projectile.DamageType = Projectile.ai[1] switch {
            ClassRanged => DamageClass.Ranged,
            ClassMagic => DamageClass.Magic,
            _ => DamageClass.Melee,
        };

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
                core: ProfaneCommon.BloodBright, edge: ProfaneCommon.FleshDark, intensity: 0.6f);
        }

        // 统一双层暗红血肉拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 8f,
            outerColor: ProfaneCommon.FleshDark, innerColor: ProfaneCommon.BloodBright,
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D sg = ACMAsset.SoftGlow;
        float pulse = 0.50f + 0.28f * ProfaneCommon.Heartbeat();
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
