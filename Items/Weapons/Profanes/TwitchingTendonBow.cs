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
/// 搐筋弓 - 蓄力长弓（筋腱张力机制）。
/// 按住拉弦：肌腱可见拉伸、60%/85% 两次心跳、满弦"痉挛"（弓体抽搐+绷断声+金瞳一闪）。
/// 松手释放：&lt;30% 弱射（0.45×）；30~99% 标准脊椎箭；100% 痉挛箭
/// （3.6×、穿透5、附带双螺旋飞刃、命中肋骨爆裂、+1 剖检印）；每第3次满弦射出畸变眼球箭（+2 印）。
/// </summary>
public class TwitchingTendonBow : ModItem
{
    /// <summary>满弦射击计数（每第3次换眼球箭），owner 客户端消费。</summary>
    internal int FullShotCount;

    public override void SetDefaults() {
        Item.damage = 1200;
        Item.crit = 14;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 24;
        Item.height = 56;
        Item.useTime = 16;
        Item.useAnimation = 16;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 4f;
        Item.value = Item.buyPrice(gold: 80);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = null;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.channel = true;
        Item.shoot = ModContent.ProjectileType<TendonBowHeld>();
        Item.shootSpeed = 18f;
        Item.useAmmo = AmmoID.Arrow; // 点击时消耗一支箭, 松手释放不再二次消耗
    }

    public override bool CanUseItem(Player player) {
        return player.ownedProjectileCounts[ModContent.ProjectileType<TendonBowHeld>()] < 1;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Projectile.NewProjectile(source, player.Center, Vector2.Zero,
            ModContent.ProjectileType<TendonBowHeld>(), damage, knockback, player.whoAmI);
        return false;
    }
}

/// <summary>
/// 手持搐筋弓（拉弦弹幕）- 握弓→拉弦→满弦痉挛→松手释放。
/// 弦与肌腱用 DrawBeam 绘制，拉弦深度/心跳/抽搐全部可读。
/// </summary>
public class TendonBowHeld : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/Profanes/TwitchingTendonBow";

    private const int GripTime = 8;      // 握弓前摇
    private const int DrawTime = 37;     // 拉弦时长 (8→45f 满)
    private const float WeakGate = 0.30f;

    private ref float Timer => ref Projectile.ai[0];
    private int _twitchTimer;            // 满弦痉挛帧
    private bool _thump60, _thump85, _snapped;
    private Player Owner => Main.player[Projectile.owner];

    private float Charge01 => MathHelper.Clamp((Timer - GripTime) / DrawTime, 0f, 1f);

    public override void SetStaticDefaults() {
        ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.TendonBowHeld.DisplayName",
            () => "Twitching Tendon Bow");
    }

    public override void SetDefaults() {
        Projectile.width = 24;
        Projectile.height = 56;
        Projectile.friendly = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 3600;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed
            || Owner.HeldItem?.type != ModContent.ItemType<TwitchingTendonBow>()) {
            Projectile.Kill();
            return;
        }

        Owner.heldProj = Projectile.whoAmI;
        Owner.itemTime = 2;
        Owner.itemAnimation = 2;
        Timer++;

        // 朝向 (owner 端跟鼠标, 其他端由同步的 velocity 兜底; 角度阈值节流 netUpdate)
        if (Projectile.owner == Main.myPlayer) {
            Vector2 aim = Owner.DirectionTo(Main.MouseWorld);
            if (Vector2.Distance(aim, Projectile.velocity) > 0.035f) {
                Projectile.velocity = aim;
                Projectile.netUpdate = true;
            }
            Owner.ChangeDir(Main.MouseWorld.X > Owner.Center.X ? 1 : -1);
        }
        Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
        Projectile.rotation = dir.ToRotation();
        Projectile.Center = Owner.MountedCenter + dir * 16f;
        Owner.itemRotation = (dir * Owner.direction).ToRotation();
        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
            Projectile.rotation - MathHelper.PiOver2);

        float charge = Charge01;

        // 拉弦分层反馈: 60%/85% 心跳, 100% 痉挛
        if (Timer > GripTime) {
            if (!_thump60 && charge >= 0.60f) {
                _thump60 = true;
                ProfaneCommon.PlayThump(Projectile.Center, -0.7f, 0.5f);
            }
            if (!_thump85 && charge >= 0.85f) {
                _thump85 = true;
                ProfaneCommon.PlayThump(Projectile.Center, -0.45f, 0.6f);
            }
            if (!_snapped && charge >= 1f) {
                _snapped = true;
                _twitchTimer = 6;
                SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.8f, Pitch = 0.4f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.6f, Pitch = 0.7f }, Projectile.Center);
            }

            // 拉弦渗血 (张力可视化, 密度随蓄力)
            if (Main.rand.NextFloat() < 0.2f + 0.4f * charge) {
                Vector2 nock = NockPoint(charge);
                Dust d = Dust.NewDustPerfect(nock + Main.rand.NextVector2Circular(4f, 4f),
                    DustID.Blood, -dir * Main.rand.NextFloat(0.5f, 1.5f), 0, default, 1.1f);
                d.noGravity = true;
            }
        }
        if (_twitchTimer > 0) _twitchTimer--;

        // 松手 → 释放
        if (!Owner.channel && Timer > GripTime) {
            Fire(charge);
            return;
        }

        Lighting.AddLight(Projectile.Center, 0.35f + 0.3f * charge, 0.06f, 0.05f);
    }

    private Vector2 NockPoint(float charge) {
        Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        return Projectile.Center - dir * (4f + 14f * ACMUtils.QuadOut(charge));
    }

    private void Fire(float charge) {
        if (Projectile.owner == Main.myPlayer) {
            Vector2 dir = Owner.DirectionTo(Main.MouseWorld);
            Vector2 muzzle = Projectile.Center + dir * 8f;
            var src = Owner.GetSource_ItemUse(Owner.HeldItem);
            int baseDamage = Projectile.damage;

            if (charge < WeakGate) {
                // 弱射 (惩罚乱点): 0.45×, 低穿透
                Projectile.NewProjectile(src, muzzle, dir * 14f,
                    ModContent.ProjectileType<TendonSpineBolt>(),
                    (int)(baseDamage * 0.45f), Projectile.knockBack * 0.5f, Projectile.owner);
                SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.6f, Pitch = -0.2f }, muzzle);
            }
            else if (charge < 1f) {
                // 标准脊椎箭
                Projectile.NewProjectile(src, muzzle, dir * 18f,
                    ModContent.ProjectileType<TendonSpineBolt>(),
                    baseDamage, Projectile.knockBack, Projectile.owner);
                SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.8f }, muzzle);
            }
            else {
                // 痉挛满弦: 每第3次换畸变眼球箭
                bool eyeShot = false;
                if (Owner.HeldItem?.ModItem is TwitchingTendonBow bow) {
                    bow.FullShotCount++;
                    eyeShot = bow.FullShotCount % 3 == 0;
                }

                if (eyeShot) {
                    Projectile.NewProjectile(src, muzzle, dir * 17f,
                        ModContent.ProjectileType<TendonEyeballShot>(),
                        (int)(baseDamage * 3.5f), Projectile.knockBack * 2f, Projectile.owner);
                    SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.7f, Pitch = 0.2f }, muzzle);
                }
                else {
                    // ai[0]=1: 满弦痉挛箭 (穿透5, 命中肋骨爆裂 + 印记)
                    Projectile.NewProjectile(src, muzzle, dir * 24f,
                        ModContent.ProjectileType<TendonSpineBolt>(),
                        (int)(baseDamage * 3.6f), Projectile.knockBack * 1.5f, Projectile.owner, 1f);
                }

                // 满弦附带双螺旋飞刃
                for (int i = 0; i < 2; i++) {
                    float offsetAngle = (i == 0 ? -1 : 1) * MathHelper.ToRadians(20);
                    Projectile.NewProjectile(src, muzzle, dir.RotatedBy(offsetAngle) * 15f,
                        ModContent.ProjectileType<TendonSpiralBlade>(),
                        (int)(baseDamage * 0.6f), Projectile.knockBack * 0.3f, Projectile.owner,
                        MathHelper.TwoPi * i / 2, 0);
                }

                // 满弦释放冲击链: 绷断声 + 后坐 + 微震
                ProfaneCommon.PlaySquelch(muzzle, 1f, 0.35f);
                SoundEngine.PlaySound(SoundID.Item5 with { Volume = 1f, Pitch = 0.15f }, muzzle);
                Owner.velocity -= dir * 2.5f;
                WeaponVFX.AddScreenShake(Owner.Center, 3f);
            }
        }

        // 释放恢复拍
        Owner.itemTime = Owner.itemAnimation = 12;
        Projectile.Kill();
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        float charge = Charge01;
        Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 perp = new(-dir.Y, dir.X);

        // 满弦痉挛抖动 + 心跳呼吸
        Vector2 jitter = _twitchTimer > 0
            ? Main.rand.NextVector2Circular(2f, 2f)
            : Vector2.Zero;
        Vector2 bowPos = Projectile.Center + jitter;
        float breathe = 1f + ProfaneCommon.Heartbeat() * 0.03f * charge;

        // 弓弦 (肌腱): 两端 → 搭箭点, 拉得越满绷得越亮
        Vector2 tipUp = bowPos + perp * 24f + dir * 2f;
        Vector2 tipDown = bowPos - perp * 24f + dir * 2f;
        Vector2 nock = NockPoint(charge) + jitter;
        float stringGlow = 0.35f + 0.5f * charge;
        ACMShaders.DrawBeam(tipUp, nock, 2.2f, ProfaneCommon.SinewPale, ProfaneCommon.FleshDark, stringGlow);
        ACMShaders.DrawBeam(tipDown, nock, 2.2f, ProfaneCommon.SinewPale, ProfaneCommon.FleshDark, stringGlow);

        // 搭着的脊椎箭 (拉弦后拉)
        if (Timer > GripTime) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D lsh = ACMAsset.LightShot;
            sb.Draw(lsh, nock + dir * 16f - Main.screenPosition, null,
                new Color(220, 40, 30) * (0.5f + 0.5f * charge), Projectile.rotation,
                lsh.Size() * 0.5f,
                new Vector2(0.55f, 0.12f), SpriteEffects.None, 0);

            // 满弦金瞳一闪 (弓臂上的眼睁开)
            if (_snapped) {
                Texture2D sparkle = ACMAsset.Sparkle;
                float glint = _twitchTimer > 0 ? 1f : 0.55f + 0.25f * ProfaneCommon.Heartbeat();
                sb.Draw(sparkle, bowPos - Main.screenPosition, null,
                    ProfaneCommon.EyeGold * (0.8f * glint), 0f,
                    sparkle.Size() * 0.5f,
                    0.4f, SpriteEffects.None, 0);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        // 弓本体 (贴图纵向, 旋转到瞄准方向)
        SpriteEffects fx = dir.X < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
        Main.spriteBatch.Draw(tex, bowPos - Main.screenPosition, null, lightColor,
            Projectile.rotation + MathHelper.PiOver2, tex.Size() * 0.5f,
            breathe, fx, 0f);
        return false;
    }
}

/// <summary>
/// 脊椎箭 - 穿透主箭，LightShot暗红渲染。
/// ai[0]=1 为满弦痉挛箭：穿透5，命中肋骨爆裂（4枚侧向骨刺）+1 剖检印。
/// </summary>
public class TendonSpineBolt : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    private static readonly float[] RibAngles = { -1.92f, -1.22f, 1.22f, 1.92f }; // 肋骨侧向扇角

    private bool FullDraw => Projectile.ai[0] > 0.5f;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 0;
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
    }

    public override void SetDefaults() {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.extraUpdates = 1;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
        Projectile.arrow = true;
    }

    public override void AI() {
        // 满弦痉挛箭首帧提升穿透 (SetDefaults 后由同步 ai 一致解析)
        if (FullDraw && Projectile.penetrate == 3 && Projectile.numHits == 0)
            Projectile.penetrate = 5;

        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.5f, 0.08f, 0.06f);

        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(1f, 1f),
                0, default, 1.2f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 300);

        if (FullDraw) {
            // 肋骨爆裂: 4 枚侧向骨刺 + 印记 (摘取伤害 = 面板×2 ≈ 痉挛箭伤害×0.56)
            ProfaneCommon.PlaySquelch(target.Center, 0.9f, 0.1f);
            ProfaneCommon.AddMark(target, Projectile, 1, (int)(Projectile.damage * 0.56f));
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.Profane, scale: 1f, owner: Projectile.owner);

            if (Main.myPlayer == Projectile.owner) {
                Vector2 fwd = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                foreach (float off in RibAngles) {
                    Vector2 vel = fwd.RotatedBy(off) * 8f;
                    Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, vel,
                        ModContent.ProjectileType<TendonSpiralBlade>(),
                        (int)(Projectile.damage * 0.28f), 1f, Projectile.owner,
                        Main.rand.NextFloat(MathHelper.TwoPi), 0);
                }
            }
        }

        for (int i = 0; i < 8; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                Main.rand.NextVector2Circular(5f, 5f), 0, default, 1.8f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 统一双层暗红血肉脊椎拖尾 (满弦更粗)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: FullDraw ? 13f : 9f,
            outerColor: ProfaneCommon.FleshDark, innerColor: ProfaneCommon.BloodBright,
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.8f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        Texture2D sg = ACMAsset.SoftGlow;

        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(220, 40, 30),
            Projectile.rotation, lsh.Size() * 0.5f,
            new Vector2(FullDraw ? 1.05f : 0.85f, 0.20f), SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(180, 35, 30) * 0.70f, 0f,
            sg.Size() * 0.5f,
            0.40f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.4f }, Projectile.Center);
        for (int i = 0; i < 8; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.5f);
            d.noGravity = true;
        }
    }
}

/// <summary>
/// 筋腱旋转飞刃 - 满弦附带/肋骨爆裂的螺旋血刃。
/// 初期螺旋偏移后追踪最近敌人。
/// </summary>
public class TendonSpiralBlade : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/BlankStar";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 0;
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
    }

    private bool _homing;
    private float _spiralTimer;
    private const float SPIRAL_DURATION = 35f;

    public override void SetDefaults() {
        Projectile.width = 18;
        Projectile.height = 18;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 160;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override void AI() {
        _spiralTimer++;
        Projectile.rotation += 0.28f;

        if (!_homing && _spiralTimer < SPIRAL_DURATION) {
            float baseAngle = Projectile.ai[0];
            float spiralAngle = baseAngle + _spiralTimer * 0.15f;
            float spiralRadius = MathHelper.Lerp(35f, 6f, _spiralTimer / SPIRAL_DURATION);
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = new(-forward.Y, forward.X);
            Vector2 spiralOffset = new Vector2(spiralRadius, 0).RotatedBy(spiralAngle);
            Projectile.Center += perp * spiralOffset.X * 0.15f;
        }
        else {
            _homing = true;
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
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 17f, 0.12f);
            }
        }

        Lighting.AddLight(Projectile.Center, 0.3f, 0.05f, 0.04f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 240);
        for (int i = 0; i < 5; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.4f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 统一双层暗红血肉拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 8f,
            outerColor: ProfaneCommon.FleshDark, innerColor: ProfaneCommon.BloodBright,
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D star = ACMAsset.BlankStar;
        Texture2D sg = ACMAsset.SoftGlow;
        float pulse = 0.55f + 0.25f * ProfaneCommon.Heartbeat();

        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            new Color(220, 50, 30) * (0.80f * pulse),
            Projectile.rotation, star.Size() * 0.5f,
            0.55f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ProfaneCommon.BloodBright * (0.5f * pulse), 0f,
            sg.Size() * 0.5f,
            0.40f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 畸变眼球箭 - 满弦每第3发的巨型眼球（ProfaneGazeEye 渲染）。
/// 直击 +2 剖检印；命中或超时爆炸释放6道追踪触手（远程）。
/// </summary>
public class TendonEyeballShot : ModProjectile
{
    public override string Texture => "InnoVault/Assets/placeholder";

    private ref float Timer => ref Projectile.ai[0];
    private Vector2 _gazeDir;
    private float _lockAmt;
    private float Seed => (Projectile.whoAmI * 0.191f) % 1f;

    public override void SetDefaults() {
        Projectile.width = 22;
        Projectile.height = 22;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 120;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        Timer++;
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.6f, 0.28f, 0.08f);

        // 血液拖尾
        Dust dt = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
            -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(1f, 1f),
            0, default, 1.5f);
        dt.noGravity = true;

        // 追踪 + 视线
        if (Timer > 15) {
            float closestDist = 400f;
            int targetIdx = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < closestDist) { closestDist = dist; targetIdx = i; }
            }
            if (targetIdx >= 0) {
                _gazeDir = Projectile.DirectionTo(Main.npc[targetIdx].Center);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                    _gazeDir * Projectile.velocity.Length(), 0.03f);
                _lockAmt = MathHelper.Lerp(_lockAmt, closestDist < 200f ? 1f : 0f, 0.14f);
            }
            else {
                _gazeDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                _lockAmt = MathHelper.Lerp(_lockAmt, 0f, 0.1f);
            }
        }
        else {
            _gazeDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        // 直击 +2 印; 摘取伤害 = 面板×2 (眼球箭为面板×3.5 → ×0.57)
        ProfaneCommon.AddMark(target, Projectile, 2, (int)(Projectile.damage * 0.57f));
        Explode();
    }

    public override void OnKill(int timeLeft) {
        Explode();
    }

    private bool _exploded;
    private void Explode() {
        if (_exploded) return;
        _exploded = true;

        SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.3f, Volume = 0.8f }, Projectile.Center);
        ProfaneCommon.PlaySquelch(Projectile.Center, 1f, 0f);

        // 巨眼弹爆裂血肉脉冲 (径向辉光 + 冲击环)
        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
            ACMWeaponBurst.Profane, scale: 1.2f, owner: Projectile.owner);

        // 血液大爆发
        for (int i = 0; i < 25; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                Main.rand.NextVector2CircularEdge(8f, 8f), 0, default,
                Main.rand.NextFloat(1.8f, 3f));
            d.noGravity = true;
        }

        // 释放6道追踪血刺 (远程)
        if (Main.myPlayer == Projectile.owner) {
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(7f, 11f);
                Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, vel,
                    ModContent.ProjectileType<ProfaneTendrilChaser>(),
                    Projectile.damage / 3, 2f, Projectile.owner, 0f, ProfaneTendrilChaser.ClassRanged);
            }
        }

        WeaponVFX.AddScreenShake(Projectile.Center, 4f);
    }

    public override bool PreDraw(ref Color lightColor) {
        // 拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 10f,
            outerColor: ProfaneCommon.FleshDark, innerColor: ProfaneCommon.FleshMid,
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);

        // 程序化眼球 (睁眼 10f + 心跳呼吸 + 锁定收瞳)
        float open = ACMUtils.QuadOut(MathHelper.Clamp(Timer / 10f, 0f, 1f));
        float breathe = 1f + ProfaneCommon.Heartbeat(Seed) * 0.08f;
        ProfaneCommon.DrawGazeEye(Projectile.Center, 66f * breathe, _gazeDir * 0.9f,
            open, _lockAmt, 1f, Seed, Projectile.rotation * 0.2f);

        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.4f + 0.15f * ProfaneCommon.Heartbeat(Seed),
            ProfaneCommon.FleshMid * 0.5f);
        return false;
    }
}
