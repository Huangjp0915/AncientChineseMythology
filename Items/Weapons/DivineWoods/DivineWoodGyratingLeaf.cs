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
/// 轨道中自动冲刺攻击附近敌人，命中5次触发自然裁决
/// 参照AbyssalFrostJudgmentChakram的状态机系统
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
/// 使用GlaciateWave做冲刺拖尾，SoftGlow做光晕，BlankStar做核心闪光
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
            case LeafState.Flying:
                HandleFlying(owner);
                break;
            case LeafState.Orbiting:
                HandleOrbiting(owner);
                break;
            case LeafState.Dashing:
                HandleDashing(owner);
                break;
            case LeafState.Returning:
                HandleReturning(owner);
                break;
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

        // 每5次命中触发自然裁决
        if (HitCounter % 5 == 0) {
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 1f, Pitch = 0.5f }, target.Center);
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC nearby = Main.npc[i];
                if (!nearby.CanBeChasedBy()) continue;
                if (Vector2.Distance(target.Center, nearby.Center) < 450f) {
                    nearby.SimpleStrikeNPC(damageDone / 2, hit.HitDirection, false, 0f, null, false, 0, true);
                    nearby.AddBuff(BuffID.Poisoned, 300);
                }
            }
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.TwoPi / 40f * i;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 14f);
                Dust storm = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                    vel, 40, default, Main.rand.NextFloat(2f, 3.5f));
                storm.noGravity = true;
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

        // 拖尾幻影
        for (int i = 0; i < Projectile.oldPos.Length; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float progress = 1f - (float)i / Projectile.oldPos.Length;
            Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
            Color trailColor = Color.Lerp(new Color(50, 200, 60), new Color(180, 255, 190), progress) * progress * 0.45f;
            trailColor.A = 0;
            sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress * 0.85f, SpriteEffects.None, 0);
        }

        // 冲刺时额外GlaciateWave拖尾
        if (State == LeafState.Dashing) {
            Texture2D wave = ACMAsset.GlaciateWave;
            sb.Draw(wave, Projectile.Center - Main.screenPosition, null,
                new Color(60, 220, 80) * 0.50f,
                Projectile.velocity.ToRotation(), wave.Size() * 0.5f,
                new Vector2(0.5f, 0.20f), SpriteEffects.None, 0);
        }

        // 光晕
        Texture2D sg = ACMAsset.SoftGlow;
        float pulse = 0.35f + 0.1f * MathF.Sin(Timer * 0.12f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(80, 220, 90) * pulse, 0f,
            sg.Size() * 0.5f,
            0.60f, SpriteEffects.None, 0);

        // 自身纹理外发光层
        Color glowColor = new Color(60, 200, 80) * 0.28f;
        glowColor.A = 0;
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        // 自身纹理主体
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
