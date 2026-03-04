using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.DivineWoods;

/// <summary>
/// 神木旋叶 - 投掷后进入环绕轨道的回旋叶刃
/// 冲刺攻击时释放一圈叶片弹幕旋风
/// 命中5次触发自然裁决：释放花瓣环形弹幕
/// </summary>
public class DivineWoodGyratingLeaf : ModItem
{
    public override void SetDefaults() {
        Item.damage = 175;
        Item.crit = 16;
        Item.DamageType = DamageClass.Melee;
        Item.width = 40;
        Item.height = 40;
        Item.useTime = 16;
        Item.useAnimation = 16;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 7f;
        Item.value = Item.buyPrice(gold: 50);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<DivineWoodGyratingLeafProj>();
        Item.shootSpeed = 20f;
    }

    public override bool CanUseItem(Player player) {
        return player.ownedProjectileCounts[ModContent.ProjectileType<DivineWoodGyratingLeafProj>()] < 2;
    }
}

/// <summary>
/// 神木旋叶弹幕 - 使用自身纹理，状态机驱动
/// 冲刺时释放叶片旋风，裁决时释放花瓣弹幕
/// </summary>
public class DivineWoodGyratingLeafProj : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/DivineWoods/DivineWoodGyratingLeaf";

    private enum LeafState { Flying, Orbiting, Dashing, Returning }

    private LeafState State {
        get => (LeafState)Projectile.ai[0];
        set => Projectile.ai[0] = (float)value;
    }

    private ref float Timer => ref Projectile.ai[1];
    private ref float HitCounter => ref Projectile.localAI[0];
    private ref float OrbitAngle => ref Projectile.localAI[1];

    private const float OrbitRadius = 110f;
    private const float OrbitSpeed = 0.07f;
    private const float DashSpeed = 30f;
    private const int DashCooldown = 35;
    private const float MaxFlyDistance = 500f;

    private int dashTarget = -1;
    private int dashCooldownTimer;

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
        Projectile.timeLeft = 1800;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override void AI() {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead) { Projectile.Kill(); return; }

        Timer++;
        Projectile.rotation += 0.35f * (State == LeafState.Dashing ? 2.5f : 1f);

        switch (State) {
            case LeafState.Flying: HandleFlying(owner); break;
            case LeafState.Orbiting: HandleOrbiting(owner); break;
            case LeafState.Dashing: HandleDashing(owner); break;
            case LeafState.Returning: HandleReturning(owner); break;
        }

        // 环绕时周期性释放装饰叶片
        if (State == LeafState.Orbiting && Timer % 20 == 0) {
            Dust d = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(20, 20),
                DustID.GrassBlades, Main.rand.NextVector2Circular(1f, 1f), 80, default, 1.5f);
            d.noGravity = true;
        }

        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(15, 15),
                DustID.JungleTorch, Projectile.velocity * 0.1f, 60, default,
                Main.rand.NextFloat(1f, 1.6f));
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.25f, 0.7f, 0.25f);
    }

    private void HandleFlying(Player owner) {
        Projectile.velocity *= 0.97f;
        float dist = Vector2.Distance(Projectile.Center, owner.Center);
        if (dist > MaxFlyDistance || Projectile.velocity.Length() < 3f || Timer > 35) {
            State = LeafState.Orbiting;
            Timer = 0;
            dashCooldownTimer = 0;
            OrbitAngle = (Projectile.Center - owner.Center).ToRotation();
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f, Pitch = 0.5f }, Projectile.Center);
        }
    }

    private void HandleOrbiting(Player owner) {
        OrbitAngle += OrbitSpeed;
        Vector2 targetPos = owner.Center + new Vector2(MathF.Cos(OrbitAngle), MathF.Sin(OrbitAngle)) * OrbitRadius;
        Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.2f);
        Projectile.velocity = (targetPos - Projectile.Center) * 0.5f;
        dashCooldownTimer++;

        if (dashCooldownTimer >= DashCooldown) {
            NPC target = FindClosestNPC(650f);
            if (target != null) {
                dashTarget = target.whoAmI;
                State = LeafState.Dashing;
                Timer = 0;
                Projectile.velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * DashSpeed;
                SoundEngine.PlaySound(SoundID.Item7 with { Volume = 0.5f, Pitch = 0.6f }, Projectile.Center);

                // 冲刺时释放6片旋风叶片
                if (Projectile.owner == Main.myPlayer) {
                    for (int i = 0; i < 6; i++) {
                        float angle = MathHelper.TwoPi * i / 6;
                        Vector2 leafVel = angle.ToRotationVector2() * 6f + Projectile.velocity * 0.3f;
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                            leafVel, ModContent.ProjectileType<DivineWoodWhirlLeaf>(),
                            Projectile.damage / 3, 1f, Projectile.owner);
                    }
                }
            }
            dashCooldownTimer = 0;
        }

        if (Timer > 500) { State = LeafState.Returning; Timer = 0; }
    }

    private void HandleDashing(Player owner) {
        if (Timer > 18 || (dashTarget >= 0 && dashTarget < Main.maxNPCs && !Main.npc[dashTarget].active)) {
            State = LeafState.Orbiting;
            Timer = 0;
            dashCooldownTimer = 0;
            OrbitAngle = (Projectile.Center - owner.Center).ToRotation();
            return;
        }
        if (dashTarget >= 0 && dashTarget < Main.maxNPCs && Main.npc[dashTarget].active) {
            Vector2 toTarget = (Main.npc[dashTarget].Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * DashSpeed, 0.15f);
        }
    }

    private void HandleReturning(Player owner) {
        Vector2 toPlayer = owner.Center - Projectile.Center;
        Vector2 direction = toPlayer.SafeNormalize(Vector2.Zero);
        Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * 28f, 0.2f);
        if (toPlayer.Length() < 40f) Projectile.Kill();
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        HitCounter++;
        target.AddBuff(BuffID.Poisoned, 300);

        for (int i = 0; i < 10; i++) {
            Dust burst = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(6f, 6f), 60, default, 1.8f);
            burst.noGravity = true;
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

        if (State == LeafState.Dashing) {
            State = LeafState.Orbiting;
            Timer = 0;
            dashCooldownTimer = 0;
            Player owner = Main.player[Projectile.owner];
            OrbitAngle = (Projectile.Center - owner.Center).ToRotation();
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

        for (int i = 0; i < Projectile.oldPos.Length; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float progress = 1f - (float)i / Projectile.oldPos.Length;
            Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
            Color trailColor = Color.Lerp(new Color(50, 200, 60), new Color(180, 255, 190), progress) * progress * 0.45f;
            trailColor.A = 0;
            sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress * 0.85f, SpriteEffects.None, 0);
        }

        if (State == LeafState.Dashing) {
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
            0.60f, SpriteEffects.None, 0);

        Color glowColor = new Color(60, 200, 80) * 0.28f;
        glowColor.A = 0;
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

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

    private NPC FindClosestNPC(float maxRange) {
        NPC closest = null;
        float closestDist = maxRange;
        for (int i = 0; i < Main.maxNPCs; i++) {
            NPC npc = Main.npc[i];
            if (!npc.CanBeChasedBy()) continue;
            float dist = Vector2.Distance(Projectile.Center, npc.Center);
            if (dist < closestDist) { closestDist = dist; closest = npc; }
        }
        return closest;
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
