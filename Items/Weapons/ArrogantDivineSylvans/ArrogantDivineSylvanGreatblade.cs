using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Celestias.Boss.Dazhengs.Items;
using AncientChineseMythology.Items.Weapons.DivineWoods;

namespace AncientChineseMythology.Items.Weapons.ArrogantDivineSylvans;

/// <summary>
/// 傲世神木·斩天巨刃 - 神木巨刃的终极形态
/// 三连斩循环：横斩→上挑→下劈
/// 横斩释放巨型藤蔓弧光，上挑射出三道扇形刀波
/// 下劈触发大地震裂 + 藤蔓旋风场
/// 所有攻击附带「古藤缠绕」持续伤害
/// </summary>
public class ArrogantDivineSylvanGreatblade : ModItem
{
    private int attackType;

    public override void SetDefaults() {
        Item.damage = 1700;
        Item.crit = 32;
        Item.DamageType = DamageClass.Melee;
        Item.width = 80;
        Item.height = 80;
        Item.useTime = 18;
        Item.useAnimation = 18;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 14f;
        Item.value = Item.buyPrice(gold: 500);
        Item.rare = ItemRarityID.Purple;
        Item.autoReuse = true;
        Item.noUseGraphic = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<ArrogantSylvanGreatbladeSwing>();
        Item.shootSpeed = 4f;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI, attackType);
        attackType = (attackType + 1) % 3;
        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<ArrogantDivineSylvan>(15)
            .AddIngredient<DivineWoodGreatblade>()
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
}

/// <summary>
/// 傲世斩天挥砍 - 三段式循环连击
/// 0=横斩(释放巨弧) 1=上挑(三道扇形刀波) 2=下劈(地裂+藤蔓旋风)
/// </summary>
public class ArrogantSylvanGreatbladeSwing : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/ArrogantDivineSylvans/ArrogantDivineSylvanGreatblade";

    private const float SWING_RANGE = MathF.PI * 1.65f;
    private const float PREP_FRAC = 0.18f;
    private const float EXEC_FRAC = 0.55f;

    private enum Stage { Prepare, Execute, Unwind }

    private ref float Timer => ref Projectile.ai[1];
    private ref float InitAngle => ref Projectile.ai[2];
    private ref float RawProgress => ref Projectile.localAI[0];
    private int AttackType => (int)Projectile.ai[0]; // 0/1/2
    private int SwingDir => AttackType switch { 0 => 1, 1 => -1, _ => 1 };

    private Stage CurrentStage {
        get => (Stage)Projectile.localAI[1];
        set { Projectile.localAI[1] = (float)value; Timer = 0f; }
    }

    private bool _waveFired;
    private Player Owner => Main.player[Projectile.owner];

    public override void SetStaticDefaults() {
        ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 18;
    }

    public override void SetDefaults() {
        Projectile.width = 80;
        Projectile.height = 80;
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
        int dir = Projectile.spriteDirection * SwingDir;

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
        int dir = Projectile.spriteDirection * SwingDir;

        switch (CurrentStage) {
            case Stage.Prepare:
                RawProgress = 0f;
                if (Timer >= prepEnd) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 1.2f }, Owner.position);
                    CurrentStage = Stage.Execute;
                }
                break;

            case Stage.Execute:
                RawProgress = MathHelper.SmoothStep(0f, SWING_RANGE, Math.Min(Timer / execDur, 1f));

                if (!_waveFired && Timer >= execDur * 0.35f) {
                    _waveFired = true;
                    FireAttackProjectiles();
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
        Projectile.scale = 1.5f * Owner.GetAdjustedItemScale(Owner.HeldItem);
        Owner.heldProj = Projectile.whoAmI;
        Timer++;

        // 挥砍时持续粒子
        if (CurrentStage == Stage.Execute) {
            Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2()
                          * Projectile.Size.Length() * Projectile.scale * 0.7f;
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(tip + Main.rand.NextVector2Circular(10, 10),
                    DustID.JungleTorch, Main.rand.NextVector2Circular(4f, 4f), 40, default, 2.5f);
                d.noGravity = true;
            }
        }
    }

    private void FireAttackProjectiles() {
        if (Projectile.owner != Main.myPlayer) return;
        Vector2 wd = Owner.DirectionTo(Main.MouseWorld);

        switch (AttackType) {
            case 0: // 横斩 → 巨型藤蔓弧光
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem),
                    Owner.Center, wd * 22f,
                    ModContent.ProjectileType<ArrogantSylvanVineWave>(),
                    (int)(Projectile.damage * 1.5f),
                    Projectile.knockBack * 0.8f, Owner.whoAmI);
                SoundEngine.PlaySound(SoundID.Item85 with { Pitch = 0.2f, Volume = 1.3f }, Owner.position);
                break;

            case 1: // 上挑 → 三道扇形刀波
                for (int i = -1; i <= 1; i++) {
                    Vector2 fanVel = wd.RotatedBy(i * MathHelper.ToRadians(18)) * 20f;
                    Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem),
                        Owner.Center, fanVel,
                        ModContent.ProjectileType<ArrogantSylvanVineWave>(),
                        (int)(Projectile.damage * 1.0f),
                        Projectile.knockBack * 0.5f, Owner.whoAmI);
                }
                SoundEngine.PlaySound(SoundID.Item85 with { Pitch = 0.6f, Volume = 1.4f }, Owner.position);
                break;

            case 2: // 下劈 → 地裂震爆
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem),
                    Owner.Center + wd * 60, Vector2.Zero,
                    ModContent.ProjectileType<ArrogantSylvanEarthquake>(),
                    (int)(Projectile.damage * 2.0f),
                    Projectile.knockBack * 1.5f, Owner.whoAmI);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.5f, Volume = 1.5f }, Owner.position);
                if (Owner.whoAmI == Main.myPlayer)
                    Owner.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 18);
                break;
        }
    }

    public override bool? CanDamage() => CurrentStage == Stage.Execute ? null : false;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        Vector2 s = Owner.MountedCenter;
        Vector2 e = s + Projectile.rotation.ToRotationVector2()
                    * Projectile.Size.Length() * Projectile.scale * 1.2f;
        float col = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            s, e, 30f * Projectile.scale, ref col);
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
        modifiers.HitDirectionOverride = target.position.X > Owner.MountedCenter.X ? 1 : -1;
        if (AttackType == 2) modifiers.FinalDamage *= 1.3f;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 900);
        target.AddBuff(BuffID.Venom, 600);
        for (int i = 0; i < 20; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(10f, 10f), 40, default, 3f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        int dir = Projectile.spriteDirection * SwingDir;
        float rotOff = dir > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        if (CurrentStage == Stage.Execute) {
            Texture2D wave = ACMAsset.GlaciateWave;
            for (int i = 1; i < 16 && i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                float a = (1f - i / 16f) * 0.75f;
                float rot = Projectile.oldRot[i] + rotOff;
                sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                    new Color(220, 255, 100) * a, rot,
                    wave.Size() * 0.5f,
                    Projectile.scale * 0.65f, SpriteEffects.None, 0);
                sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                    new Color(40, 200, 60) * (a * 0.55f), rot + 0.08f,
                    wave.Size() * 0.5f,
                    Projectile.scale * 0.42f, SpriteEffects.None, 0);
                sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 255, 220) * (a * 0.25f), rot + 0.04f,
                    wave.Size() * 0.5f,
                    Projectile.scale * 0.28f, SpriteEffects.None, 0);
            }

            float pulse = 0.85f + 0.25f * MathF.Sin((float)Main.timeForVisualEffects * 0.25f);
            Texture2D sg = ACMAsset.SoftGlow;
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(200, 255, 80) * 0.65f * pulse, Projectile.rotation + rotOff,
                sg.Size() * 0.5f,
                Projectile.scale * 2.8f, SpriteEffects.None, 0);

            Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2()
                          * Projectile.Size.Length() * Projectile.scale * 0.65f;
            Texture2D sparkle = ACMAsset.Sparkle;
            sb.Draw(sparkle, tip - Main.screenPosition, null,
                new Color(255, 255, 180) * 0.70f,
                (float)Main.timeForVisualEffects * 0.08f,
                sparkle.Size() * 0.5f,
                Projectile.scale * 0.80f, SpriteEffects.None, 0);

            if (AttackType == 2) {
                Texture2D star = ACMAsset.BlankStar;
                sb.Draw(star, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 240, 120) * 0.40f * pulse,
                    (float)Main.timeForVisualEffects * 0.03f,
                    star.Size() * 0.5f,
                    Projectile.scale * 1.2f, SpriteEffects.None, 0);
            }
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
/// 傲世藤蔓弧波 - 巨型金绿色能量斩波
/// 体积更大、持续更久、穿透无限、减速更强
/// </summary>
public class ArrogantSylvanVineWave : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/GlaciateWave";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 20;
    }

    public override void SetDefaults() {
        Projectile.width = 120;
        Projectile.height = 60;
        Projectile.friendly = true;
        Projectile.tileCollide = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 65;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.velocity *= 0.975f;

        float life = 1f - Projectile.timeLeft / 65f;
        Lighting.AddLight(Projectile.Center, 0.6f * (1f - life), 1.5f * (1f - life), 0.4f * (1f - life));

        for (int i = 0; i < 3; i++) {
            Dust d = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(30, 30),
                DustID.JungleTorch, -Projectile.velocity * 0.2f, 40, default, 2.5f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 900);
        target.AddBuff(BuffID.Venom, 600);
        target.velocity *= 0.3f;

        for (int i = 0; i < 25; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(10f, 10f), 30, default, 3f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D tex = ACMAsset.GlaciateWave;
        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D lsh = ACMAsset.LightShot;

        float life = 1f - Projectile.timeLeft / 65f;
        float scaleX = MathHelper.Lerp(2.4f, 0.8f, ACMUtils.QuadIn(life));
        float scaleY = MathHelper.Lerp(0.75f, 0.25f, ACMUtils.QuadIn(life));
        float alpha = ACMUtils.QuadOut(1f - life) * 0.95f;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.6f * alpha;
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(220, 255, 100) * a, Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.55f + i * 0.015f, 0.22f), SpriteEffects.None, 0);
        }

        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 100) * alpha, Projectile.rotation,
            tex.Size() * 0.5f,
            new Vector2(scaleX, scaleY), SpriteEffects.None, 0);
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            new Color(40, 210, 60) * (alpha * 0.55f), Projectile.rotation + 0.04f,
            tex.Size() * 0.5f,
            new Vector2(scaleX * 0.8f, scaleY * 0.75f), SpriteEffects.None, 0);
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            new Color(255, 255, 220) * (alpha * 0.30f), Projectile.rotation,
            tex.Size() * 0.5f,
            new Vector2(scaleX * 0.5f, scaleY * 0.45f), SpriteEffects.None, 0);

        Vector2 front = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 60f;
        sb.Draw(sg, front - Main.screenPosition, null,
            new Color(255, 255, 200) * alpha * 0.80f, 0f,
            sg.Size() * 0.5f,
            scaleY * 2.5f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 傲世地震裂爆 - 下劈第三击的大地震爆
/// 圆形扩散伤害 + 12道藤蔓柱爆发
/// </summary>
public class ArrogantSylvanEarthquake : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private ref float Timer => ref Projectile.ai[0];

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 70;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Timer++;
        float radius = Timer * 18f;

        for (int i = 0; i < 10; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.3f, radius);
            Dust d = Dust.NewDustPerfect(pos, DustID.JungleTorch,
                Main.rand.NextVector2Circular(2f, 2f), 40, default, 2.5f);
            d.noGravity = true;
        }

        if (Timer < 20) {
            for (int i = 0; i < 6; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 18f);
                vel.Y -= 5f;
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Mud, vel, 80, default, 2f);
                d.noGravity = false;
            }
        }

        Lighting.AddLight(Projectile.Center, 0.8f, 2f, 0.6f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 900);
        target.AddBuff(BuffID.Venom, 600);
        target.velocity *= 0.2f;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float radius = Timer * 18f;
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
    }

    public override bool PreDraw(ref Color lightColor) {
        float prog = 1f - Projectile.timeLeft / 70f;
        float alpha = ACMUtils.QuadOut(1f - prog) * 0.92f;
        float scale = MathHelper.SmoothStep(0f, 20f, ACMUtils.QuadOut(prog));

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D sparkle = ACMAsset.Sparkle;
        Texture2D star = ACMAsset.BlankStar;

        for (int k = 0; k < 12; k++) {
            float bAngle = k * MathF.PI / 6f + Timer * 0.025f;
            bool major = (k % 3 == 0);
            Color bColor = major ? new Color(220, 255, 100) : new Color(40, 200, 60);
            float bLen = major ? scale * 0.75f : scale * 0.45f;
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                bColor * (alpha * 0.80f), bAngle,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.18f, bLen), SpriteEffects.None, 0);
        }

        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 80) * (alpha * 0.50f), 0f,
            sg.Size() * 0.5f,
            scale * 0.60f, SpriteEffects.None, 0);

        float flashAlpha = MathHelper.SmoothStep(1.2f, 0f, prog * 1.4f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 255, 230) * (alpha * flashAlpha), 0f,
            sg.Size() * 0.5f,
            scale * 0.25f, SpriteEffects.None, 0);

        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            new Color(255, 255, 180) * (alpha * 0.55f),
            Timer * 0.10f,
            star.Size() * 0.5f,
            scale * 0.18f, SpriteEffects.None, 0);
        sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 120) * (alpha * 0.45f),
            -Timer * 0.06f,
            sparkle.Size() * 0.5f,
            scale * 0.22f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
