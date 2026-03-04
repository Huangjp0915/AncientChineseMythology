using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Profanes;

/// <summary>
/// 亵渎肢解者 - 战士持握型挥砍大剑
/// 挥刀释放血肉弧光拖尾，挥至40%进度释放一道血肉刀波
/// 刀波沿途喷洒血液碎片，命中后敌人释放血雾爆发
/// 三连击循环：横斩→上挑→下劈，下劈触发血肉震荡
/// </summary>
public class ProfaneDismemberer : ModItem
{
    private int _attackType;

    public override void SetDefaults() {
        Item.damage = 1400;
        Item.crit = 18;
        Item.DamageType = DamageClass.Melee;
        Item.width = 70;
        Item.height = 70;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 9f;
        Item.value = Item.buyPrice(gold: 80);
        Item.rare = ItemRarityID.Purple;
        Item.autoReuse = true;
        Item.noUseGraphic = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<ProfaneDismembererSwing>();
        Item.shootSpeed = 3f;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI, _attackType);
        _attackType = (_attackType + 1) % 3;
        return false;
    }
}

/// <summary>
/// 亵渎肢解者挥砍弹幕 - 持握旋转，释放血肉弧光
/// 三种攻击：0=横斩，1=上挑，2=下劈（带血肉震荡）
/// </summary>
public class ProfaneDismembererSwing : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/Profanes/ProfaneDismemberer";

    private const float SWING_RANGE = MathF.PI * 1.55f;
    private const float PREP_FRAC = 0.18f;
    private const float EXEC_FRAC = 0.56f;

    private enum Stage { Prepare, Execute, Unwind }

    private ref float Timer => ref Projectile.ai[1];
    private ref float InitAngle => ref Projectile.ai[2];
    private ref float RawProgress => ref Projectile.localAI[0];
    private int AttackType => (int)Projectile.ai[0];

    private Stage CurrentStage {
        get => (Stage)Projectile.localAI[1];
        set { Projectile.localAI[1] = (float)value; Timer = 0f; }
    }

    private bool _waveFired;
    private Player Owner => Main.player[Projectile.owner];

    public override void SetStaticDefaults() {
        ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 16;
    }

    public override void SetDefaults() {
        Projectile.width = 70;
        Projectile.height = 70;
        Projectile.friendly = true;
        Projectile.timeLeft = 10000;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
        Projectile.ownerHitCheck = true;
        Projectile.DamageType = DamageClass.Melee;
    }

    public override void OnSpawn(IEntitySource source) {
        int dirMod = AttackType switch { 1 => -1, _ => 1 };
        Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
        float toMouse = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
        int dir = Projectile.spriteDirection * dirMod;

        if (dir > 0) {
            toMouse = MathHelper.Clamp(toMouse, -MathF.PI / 2.8f, MathF.PI / 5f);
            InitAngle = toMouse - SWING_RANGE * 0.55f;
        }
        else {
            if (toMouse < 0) toMouse += MathHelper.TwoPi;
            toMouse = MathHelper.Clamp(toMouse, MathF.PI * 0.78f, MathF.PI * 1.4f);
            InitAngle = toMouse + SWING_RANGE * 0.55f;
        }
    }

    private int SwingDir {
        get {
            int dirMod = AttackType switch { 1 => -1, _ => 1 };
            return Projectile.spriteDirection * dirMod;
        }
    }

    public override void AI() {
        if (!Owner.active || Owner.dead) { Projectile.Kill(); return; }
        Owner.itemAnimation = 2;
        Owner.itemTime = 2;

        float totalTime = Owner.itemAnimationMax;
        float prepEnd = totalTime * PREP_FRAC;
        float execDur = totalTime * EXEC_FRAC;
        float unwindDur = totalTime * (1f - PREP_FRAC - EXEC_FRAC);
        int dir = SwingDir;

        switch (CurrentStage) {
            case Stage.Prepare:
                RawProgress = 0f;
                if (Timer >= prepEnd) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f }, Owner.position);
                    CurrentStage = Stage.Execute;
                }
                break;

            case Stage.Execute:
                RawProgress = MathHelper.SmoothStep(0f, SWING_RANGE, Math.Min(Timer / execDur, 1f));

                if (!_waveFired && Timer >= execDur * 0.40f) {
                    _waveFired = true;
                    Vector2 wd = Owner.DirectionTo(Main.MouseWorld);
                    int waveType = ModContent.ProjectileType<ProfaneFleshWave>();
                    float waveDmgMult = AttackType == 2 ? 1.5f : 1.0f;
                    Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem),
                        Owner.Center, wd * 20f, waveType,
                        (int)(Owner.HeldItem.damage * waveDmgMult),
                        Owner.HeldItem.knockBack * 0.6f, Owner.whoAmI, AttackType);

                    if (AttackType == 2)
                        Owner.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 8);

                    SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.5f, Volume = 0.7f }, Owner.position);
                }

                // 挥砍中喷洒血液粒子
                if (Timer % 2 == 0) {
                    Vector2 tip = Owner.Center + Projectile.rotation.ToRotationVector2()
                                  * Projectile.Size.Length() * Projectile.scale * 0.7f;
                    for (int i = 0; i < 2; i++) {
                        Dust d = Dust.NewDustPerfect(tip + Main.rand.NextVector2Circular(10, 10),
                            DustID.Blood, Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.5f);
                        d.noGravity = true;
                    }
                }

                if (Timer >= execDur) CurrentStage = Stage.Unwind;
                break;

            case Stage.Unwind:
                RawProgress = MathHelper.Lerp(SWING_RANGE, SWING_RANGE * 1.04f,
                    Math.Min(Timer / unwindDur, 1f));
                if (Timer >= unwindDur) Projectile.Kill();
                break;
        }

        Projectile.rotation = InitAngle + dir * RawProgress;
        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
            Projectile.rotation - MathHelper.PiOver2);
        Vector2 arm = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full,
            Projectile.rotation - MathHelper.PiOver2);
        arm.Y += Owner.gfxOffY;
        Projectile.Center = arm;
        Projectile.scale = 1.35f * Owner.GetAdjustedItemScale(Owner.HeldItem);
        Owner.heldProj = Projectile.whoAmI;
        Timer++;
    }

    public override bool? CanDamage() => CurrentStage == Stage.Execute ? null : false;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        Vector2 s = Owner.MountedCenter;
        Vector2 e = s + Projectile.rotation.ToRotationVector2()
                    * Projectile.Size.Length() * Projectile.scale * 1.1f;
        float col = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            s, e, 26f * Projectile.scale, ref col);
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
        modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        if (AttackType == 2)
            modifiers.SourceDamage += 0.3f;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 300);

        // 血液粒子
        for (int i = 0; i < 12; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                Main.rand.NextVector2Circular(7f, 7f), 0, default, 2.2f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        int dir = SwingDir;
        float rotOff = dir > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        if (CurrentStage == Stage.Execute) {
            Texture2D wave = ACMAsset.GlaciateWave;
            for (int i = 1; i < 14 && i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                float a = (1f - i / 14f) * 0.72f;
                float rot = Projectile.oldRot[i] + rotOff;
                sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                    new Color(200, 30, 30) * a, rot,
                    wave.Size() * 0.5f,
                    Projectile.scale * 0.55f, SpriteEffects.None, 0);
                sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 120, 100) * (a * 0.35f), rot + 0.10f,
                    wave.Size() * 0.5f,
                    Projectile.scale * 0.38f, SpriteEffects.None, 0);
            }

            float pulse = 0.75f + 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 0.24f);
            Texture2D sg = ACMAsset.SoftGlow;
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(220, 40, 30) * 0.50f * pulse, Projectile.rotation + rotOff,
                sg.Size() * 0.5f,
                Projectile.scale * 2.0f, SpriteEffects.None, 0);

            Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2()
                          * Projectile.Size.Length() * Projectile.scale * 0.6f;
            Texture2D sparkle = ACMAsset.Sparkle;
            sb.Draw(sparkle, tip - Main.screenPosition, null,
                new Color(255, 100, 80) * 0.45f,
                (float)Main.timeForVisualEffects * 0.07f,
                sparkle.Size() * 0.5f,
                Projectile.scale * 0.55f, SpriteEffects.None, 0);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
        SpriteEffects fx = dir < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Vector2 origin = dir > 0
            ? new Vector2(0, tex.Height) : new Vector2(tex.Width, tex.Height);
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            lightColor, Projectile.rotation + rotOff, origin,
            Projectile.scale, fx, 0);
        return false;
    }
}

/// <summary>
/// 血肉刀波 - 暗红色血肉能量波
/// ai[0]存储攻击类型：2=下劈（更大、更强）
/// </summary>
public class ProfaneFleshWave : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/GlaciateWave";

    private int AttackType => (int)Projectile.ai[0];

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 16;
    }

    public override void SetDefaults() {
        Projectile.width = 90;
        Projectile.height = 45;
        Projectile.friendly = true;
        Projectile.tileCollide = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 50;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.velocity *= 0.96f;

        float life = 1f - Projectile.timeLeft / 50f;
        Lighting.AddLight(Projectile.Center, 0.8f * (1f - life), 0.15f * (1f - life), 0.1f * (1f - life));

        // 沿途喷洒血液
        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(25, 25),
                DustID.Blood, -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(2f, 2f),
                0, default, 2.0f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 480);

        for (int i = 0; i < 15; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                Main.rand.NextVector2Circular(8f, 8f), 0, default, 2.5f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D tex = ACMAsset.GlaciateWave;
        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D lsh = ACMAsset.LightShot;

        float life = 1f - Projectile.timeLeft / 50f;
        float sizeMult = AttackType == 2 ? 1.4f : 1.0f;
        float scaleX = MathHelper.Lerp(1.6f, 0.5f, ACMUtils.QuadIn(life)) * sizeMult;
        float scaleY = MathHelper.Lerp(0.55f, 0.18f, ACMUtils.QuadIn(life)) * sizeMult;
        float alpha = ACMUtils.QuadOut(1f - life) * 0.90f;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.55f * alpha;
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(200, 30, 20) * a, Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.45f + i * 0.012f, 0.15f), SpriteEffects.None, 0);
        }

        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            new Color(180, 20, 15) * alpha, Projectile.rotation,
            tex.Size() * 0.5f,
            new Vector2(scaleX, scaleY), SpriteEffects.None, 0);
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            new Color(255, 110, 90) * (alpha * 0.40f), Projectile.rotation + 0.05f,
            tex.Size() * 0.5f,
            new Vector2(scaleX * 0.75f, scaleY * 0.70f), SpriteEffects.None, 0);

        Vector2 front = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 50f;
        sb.Draw(sg, front - Main.screenPosition, null,
            new Color(255, 80, 60) * alpha * 0.70f, 0f,
            sg.Size() * 0.5f,
            scaleY * 1.8f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
