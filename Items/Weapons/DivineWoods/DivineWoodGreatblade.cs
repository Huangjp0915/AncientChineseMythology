using AncientChineseMythology.Celestias.Boss.Dryades.Items;
using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.DivineWoods;

/// <summary>
/// 神木巨刃 - 持握型挥砍大刀，参照AzureRuinBlade的Held Projectile系统
/// 挥刀时释放藤蔓弧光，弧光挥至40%进度时向鼠标方向发射一道自然刀波
/// 刀波沿途生长荆棘，命中后藤蔓缠绕减速敌人
/// </summary>
public class DivineWoodGreatblade : ModItem
{
    private int attackType;

    public override void SetDefaults() {
        Item.damage = 190;
        Item.crit = 18;
        Item.DamageType = DamageClass.Melee;
        Item.width = 70;
        Item.height = 70;
        Item.useTime = 22;
        Item.useAnimation = 22;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 8f;
        Item.value = Item.buyPrice(gold: 50);
        Item.rare = ItemRarityID.Purple;
        Item.autoReuse = true;
        Item.noUseGraphic = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<DivineWoodGreatbladeSwing>();
        Item.shootSpeed = 3f;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI, attackType);
        attackType = (attackType + 1) % 2;
        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<Livinglog>(12)
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
}

/// <summary>
/// 神木巨刃挥砍弹幕 - 持握旋转，挥动时释放藤蔓弧光拖尾
/// 挥至40%进度时释放自然刀波
/// </summary>
public class DivineWoodGreatbladeSwing : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/DivineWoods/DivineWoodGreatblade";

    private const float SWING_RANGE = MathF.PI * 1.55f;
    private const float PREP_FRAC = 0.20f;
    private const float EXEC_FRAC = 0.55f;

    private enum Stage { Prepare, Execute, Unwind }

    private ref float Timer => ref Projectile.ai[1];
    private ref float InitAngle => ref Projectile.ai[2];
    private ref float RawProgress => ref Projectile.localAI[0];
    private int AttackDir => (int)Projectile.ai[0] == 0 ? 1 : -1;

    private Stage CurrentStage {
        get => (Stage)Projectile.localAI[1];
        set { Projectile.localAI[1] = (float)value; Timer = 0f; }
    }

    private bool _waveFired;
    private Player Owner => Main.player[Projectile.owner];

    public override void SetStaticDefaults() {
        ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
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
        Projectile.spriteDirection = Main.MouseWorld.X > Owner.MountedCenter.X ? 1 : -1;
        float toMouse = (Main.MouseWorld - Owner.MountedCenter).ToRotation();
        int dir = Projectile.spriteDirection * AttackDir;

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

    public override void AI() {
        if (!Owner.active || Owner.dead) { Projectile.Kill(); return; }
        Owner.itemAnimation = 2;
        Owner.itemTime = 2;

        float totalTime = Owner.itemAnimationMax;
        float prepEnd = totalTime * PREP_FRAC;
        float execDur = totalTime * EXEC_FRAC;
        float unwindDur = totalTime * (1f - PREP_FRAC - EXEC_FRAC);
        int dir = Projectile.spriteDirection * AttackDir;

        switch (CurrentStage) {
            case Stage.Prepare:
                RawProgress = 0f;
                if (Timer >= prepEnd) {
                    SoundEngine.PlaySound(SoundID.Item71, Owner.position);
                    CurrentStage = Stage.Execute;
                }
                break;

            case Stage.Execute:
                RawProgress = MathHelper.SmoothStep(0f, SWING_RANGE, Math.Min(Timer / execDur, 1f));

                if (!_waveFired && Timer >= execDur * 0.40f) {
                    _waveFired = true;
                    Vector2 wd = Owner.DirectionTo(Main.MouseWorld);
                    Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem),
                        Owner.Center, wd * 18f,
                        ModContent.ProjectileType<DivineWoodVineWave>(),
                        (int)(Owner.HeldItem.damage * 1.2f),
                        Owner.HeldItem.knockBack * 0.6f, Owner.whoAmI);
                    SoundEngine.PlaySound(SoundID.Item85 with { Pitch = 0.4f }, Owner.position);
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
        Projectile.scale = 1.3f * Owner.GetAdjustedItemScale(Owner.HeldItem);
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
            s, e, 24f * Projectile.scale, ref col);
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
        modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 600);
        for (int i = 0; i < 12; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(6f, 6f), 60, default, 2f);
            d.noGravity = true;
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.DivineWood, scale: 1.2f, owner: Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        int dir = Projectile.spriteDirection * AttackDir;
        float rotOff = dir > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi;

        // 挥砍弧光 — 统一为"外宽暗 + 内窄亮"双层 ribbon (沿刀尖扫过的弧线)
        if (CurrentStage == Stage.Execute) {
            float tipLen = Projectile.Size.Length() * Projectile.scale * 0.55f;
            var arc = new List<Vector2>();
            for (int i = 0; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                arc.Add(Projectile.Center + Projectile.oldRot[i].ToRotationVector2() * tipLen);
            }
            if (arc.Count >= 2)
                WeaponVFX.DrawRibbonTrail(arc.ToArray(), baseWidth: 26f,
                    outerColor: new Color(20, 110, 55, 150), innerColor: new Color(170, 255, 150, 200),
                    tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        if (CurrentStage == Stage.Execute) {
            float pulse = 0.8f + 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 0.22f);
            Texture2D sg = ACMAsset.SoftGlow;
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(60, 220, 80) * 0.55f * pulse, Projectile.rotation + rotOff,
                sg.Size() * 0.5f,
                Projectile.scale * 2.0f, SpriteEffects.None, 0);

            Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2()
                          * Projectile.Size.Length() * Projectile.scale * 0.6f;
            Texture2D sparkle = ACMAsset.Sparkle;
            sb.Draw(sparkle, tip - Main.screenPosition, null,
                new Color(120, 255, 150) * 0.50f,
                (float)Main.timeForVisualEffects * 0.06f,
                sparkle.Size() * 0.5f,
                Projectile.scale * 0.60f, SpriteEffects.None, 0);
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
/// 自然刀波 - 挥刀释放的翠绿弧形能量波
/// 使用GlaciateWave做主体渲染，LightShot做拖尾
/// </summary>
public class DivineWoodVineWave : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/GlaciateWave";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 16;
    }

    public override void SetDefaults() {
        Projectile.width = 80;
        Projectile.height = 40;
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
        Projectile.velocity *= 0.97f;

        float life = 1f - Projectile.timeLeft / 50f;
        Lighting.AddLight(Projectile.Center, 0.3f * (1f - life), 1.0f * (1f - life), 0.3f * (1f - life));

        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(20, 20),
                DustID.JungleTorch, -Projectile.velocity * 0.15f, 60, default, 1.8f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 480);
        target.AddBuff(BuffID.Venom, 240);
        target.velocity *= 0.6f;

        for (int i = 0; i < 15; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(7f, 7f), 40, default, 2.5f);
            d.noGravity = true;
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.DivineWood, scale: 1f, owner: Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        float life = 1f - Projectile.timeLeft / 50f;
        float alpha = ACMUtils.QuadOut(1f - life) * 0.95f;

        // 双层 ribbon 拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 14f,
            outerColor: new Color(20, 110, 55, (byte)(150 * alpha)),
            innerColor: new Color(170, 255, 150, (byte)(200 * alpha)),
            tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 2f);

        // 渐变绿刀波 — 横跨飞行方向的弦月光束
        Vector2 fwd = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 perp = new(-fwd.Y, fwd.X);
        float halfLen = MathHelper.Lerp(72f, 26f, ACMUtils.QuadIn(life)) * Projectile.scale;
        float halfW = MathHelper.Lerp(20f, 7f, ACMUtils.QuadIn(life));
        ACMShaders.DrawBeam(Projectile.Center - perp * halfLen, Projectile.Center + perp * halfLen,
            halfW, new Color(180, 255, 170), new Color(25, 130, 60), alpha);

        // 前缘柔光
        Vector2 front = Projectile.Center + fwd * 40f;
        WeaponVFX.DrawGlowBurst(front, 0.9f * Projectile.scale, new Color(150, 255, 170) * alpha);
        return false;
    }
}
