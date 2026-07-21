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
/// 爆裂肉瘤 - 投手类炸弹武器（孕育机制）。
/// 按住攻击"孕育"肉瘤（55帧满）：手中膨胀、心跳加速上行、静脉发亮；
/// 松手掷出（满蓄自动掷出）。蓄力加成：伤害×(1+0.55c)、碎片6→10、半径×(1+0.35c)、初速×(1+0.3c)。
/// 爆炸直击 +2 剖检印。
/// </summary>
public class BurstingTumorBomb : ModItem
{
    public override void SetDefaults() {
        Item.damage = 1500;
        Item.crit = 8;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 28;
        Item.height = 28;
        Item.useTime = 32;
        Item.useAnimation = 32;
        Item.knockBack = 7f;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.value = Item.buyPrice(gold: 90);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = null;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.channel = true;
        Item.shoot = ModContent.ProjectileType<TumorBombHolder>();
        Item.shootSpeed = 10f;
        Item.consumable = false;
    }

    public override bool CanUseItem(Player player) {
        return player.ownedProjectileCounts[ModContent.ProjectileType<TumorBombHolder>()] < 1;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI);
        return false;
    }
}

/// <summary>
/// 孕育中的肉瘤（手持弹幕）- 按住攻击膨胀，松手/满蓄掷出。
/// 膨胀期心跳间隔 40f→14f、音高上行；掷出瞬间玩家后坐。
/// </summary>
public class TumorBombHolder : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/Profanes/BurstingTumorBomb";

    private const int MaxCook = 55;

    private ref float Cook => ref Projectile.ai[0];
    private int _thumpTimer;
    private Player Owner => Main.player[Projectile.owner];

    public override void SetStaticDefaults() {
        ProjectileID.Sets.HeldProjDoesNotUsePlayerGfxOffY[Type] = true;
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.TumorBombHolder.DisplayName",
            () => "Gestating Tumor");
    }

    public override void SetDefaults() {
        Projectile.width = 24;
        Projectile.height = 24;
        Projectile.friendly = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 600;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        if (!Owner.active || Owner.dead || Owner.noItems || Owner.CCed
            || Owner.HeldItem?.type != ModContent.ItemType<BurstingTumorBomb>()) {
            Projectile.Kill();
            return;
        }

        Owner.heldProj = Projectile.whoAmI;
        Owner.itemTime = 2;
        Owner.itemAnimation = 2;

        // 手上位置 + 朝向
        Vector2 aim = Projectile.owner == Main.myPlayer
            ? Owner.DirectionTo(Main.MouseWorld)
            : new Vector2(Owner.direction, -0.3f).SafeNormalize(Vector2.UnitX);
        if (Projectile.owner == Main.myPlayer)
            Owner.ChangeDir(Main.MouseWorld.X > Owner.Center.X ? 1 : -1);
        Projectile.Center = Owner.MountedCenter + new Vector2(Owner.direction * 14f, -12f);
        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters,
            (Projectile.Center - Owner.MountedCenter).ToRotation() - MathHelper.PiOver2);

        float cook01 = Cook / MaxCook;
        bool channeling = Owner.channel;

        if (channeling && Cook < MaxCook) {
            Cook++;

            // 心跳加速上行 (40f→14f, 音高 -0.9→-0.3) —— 孕育张力
            if (--_thumpTimer <= 0) {
                _thumpTimer = (int)MathHelper.Lerp(40f, 14f, cook01);
                ProfaneCommon.PlayThump(Projectile.Center,
                    pitch: MathHelper.Lerp(-0.9f, -0.3f, cook01),
                    volume: 0.45f + 0.3f * cook01);
            }

            // 膨胀期渗血 + 满蓄前 5 帧静默切断 (爆发前的吸气)
            if (Cook < MaxCook - 5 && Main.rand.NextFloat() < 0.3f + 0.5f * cook01) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12f * (1f + cook01), 12f * (1f + cook01)),
                    DustID.Blood, new Vector2(0, Main.rand.NextFloat(0.5f, 1.5f)), 0, default, 1.2f + cook01);
                d.noGravity = false;
            }

            if (Cook == MaxCook - 1)
                ProfaneCommon.PlaySquelch(Projectile.Center, 1f, 0.3f); // 满蓄湿裂提示
        }
        else {
            // 松手或满蓄 → 掷出
            Throw(cook01);
            return;
        }

        Projectile.scale = 1f + 0.55f * cook01 * (1f + ProfaneCommon.Heartbeat() * 0.06f);
        Lighting.AddLight(Projectile.Center, 0.4f + 0.3f * cook01, 0.07f, 0.05f);
    }

    private void Throw(float cook01) {
        if (Projectile.owner == Main.myPlayer) {
            Vector2 vel = Owner.DirectionTo(Main.MouseWorld)
                * Owner.HeldItem.shootSpeed * (1f + 0.3f * cook01);
            int damage = (int)(Owner.GetWeaponDamage(Owner.HeldItem) * (1f + 0.55f * cook01));
            Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem),
                Projectile.Center, vel,
                ModContent.ProjectileType<TumorBombProj>(),
                damage, Owner.HeldItem.knockBack, Projectile.owner,
                0f, 0f, cook01);

            // 掷出后坐 (蓄得越满甩得越重)
            Owner.velocity -= vel.SafeNormalize(Vector2.Zero) * (1f + 1.5f * cook01);
        }
        SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.2f * cook01 }, Projectile.Center);
        // 掷出恢复拍 (recovery)
        Owner.itemTime = Owner.itemAnimation = 18;
        Projectile.Kill();
    }

    public override bool PreDraw(ref Color lightColor) {
        float cook01 = Cook / MaxCook;
        Texture2D tex = TextureAssets.Projectile[Type].Value;

        // 静脉膜 (孕育后半段渐显)
        if (cook01 > 0.3f) {
            ProfaneCommon.DrawFleshMembrane(Projectile.Center, 30f * Projectile.scale,
                (cook01 - 0.3f) * 0.8f, ProfaneCommon.Heartbeat(), veinBoost: cook01, seed: 0.7f);
        }

        // 肉瘤本体 (孕育抖动: 满蓄前越抖越凶)
        Vector2 jitter = Main.rand.NextVector2Circular(1.5f, 1.5f) * MathF.Pow(cook01, 2f);
        Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition + jitter, null,
            lightColor, Projectile.rotation, tex.Size() * 0.5f,
            Projectile.scale, SpriteEffects.None, 0f);
        return false;
    }
}

/// <summary>
/// 肉瘤弹幕 - 弧线飞行的肉块（hitbox 与视觉对齐 30px）。
/// ai[2]=孕育度；碰到物块弹跳一次（挤压变形），碰NPC或弹跳后超时爆炸。
/// </summary>
public class TumorBombProj : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/Profanes/BurstingTumorBomb";

    private ref float BounceCount => ref Projectile.ai[0];
    private ref float AiTimer => ref Projectile.ai[1];
    private float Cook01 => Projectile.ai[2];
    private int _squashTimer;

    public override void SetDefaults() {
        Projectile.width = 30;
        Projectile.height = 30;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 240;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        AiTimer++;
        Projectile.velocity.Y += 0.35f;
        Projectile.rotation += Projectile.velocity.Length() * 0.04f * Math.Sign(Projectile.velocity.X);
        Projectile.scale = (1f + 0.35f * Cook01) * (1f + ProfaneCommon.Heartbeat() * 0.05f);
        Lighting.AddLight(Projectile.Center, 0.4f, 0.06f, 0.05f);
        if (_squashTimer > 0) _squashTimer--;

        // 血液拖尾粒子
        if (AiTimer % 2 == 0) {
            Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6, 6),
                DustID.Blood, -Projectile.velocity * 0.05f, 0, default, 1.4f);
            d.noGravity = true;
        }
    }

    public override bool OnTileCollide(Vector2 oldVelocity) {
        if (BounceCount < 1) {
            BounceCount++;
            _squashTimer = 6; // 落地挤压变形
            ProfaneCommon.PlaySquelch(Projectile.Center, 0.8f, 0.2f);

            // 弹跳反射
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon)
                Projectile.velocity.X = -oldVelocity.X * 0.5f;
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon)
                Projectile.velocity.Y = -oldVelocity.Y * 0.65f;

            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    Main.rand.NextVector2CircularEdge(4f, 4f), 0, default, 1.5f);
                d.noGravity = true;
            }
            return false;
        }
        // 二次碰撞爆炸
        Explode();
        return true;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        // 直击 +2 印; 摘取伤害 = 面板×2
        ProfaneCommon.AddMark(target, Projectile, 2, Projectile.damage * 2);
        Explode();
    }

    public override void OnKill(int timeLeft) {
        Explode();
    }

    private bool _exploded;
    private void Explode() {
        if (_exploded) return;
        _exploded = true;

        SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 1.2f, Pitch = -0.5f }, Projectile.Center);
        ProfaneCommon.PlaySquelch(Projectile.Center, 1.3f, -0.2f);

        // 大型血肉爆裂演出 (冲击环 + 径向辉光)
        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
            ACMWeaponBurst.Profane, scale: 1.8f + 0.5f * Cook01, owner: Projectile.owner);

        if (Main.myPlayer == Projectile.owner) {
            // 爆炸AOE弹幕 (半径随孕育度)
            Projectile.NewProjectile(
                Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<TumorBlastVFX>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner,
                0f, 1f + 0.35f * Cook01);

            // 追踪血肉碎片 6→10 (随孕育度)
            int frags = 6 + (int)(4f * Cook01);
            for (int i = 0; i < frags; i++) {
                float angle = MathHelper.TwoPi * i / frags + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 fragVel = angle.ToRotationVector2() * Main.rand.NextFloat(7f, 12f);
                Projectile.NewProjectile(
                    Projectile.GetSource_Death(), Projectile.Center, fragVel,
                    ModContent.ProjectileType<TumorFragment>(),
                    Projectile.damage / 4, 2f, Projectile.owner);
            }
        }

        // 大量血液粒子
        for (int i = 0; i < 40; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(12f, 12f) * Main.rand.NextFloat(0.5f, 1f);
            Dust boom = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                vel, 0, default, Main.rand.NextFloat(2f, 4f));
            boom.noGravity = i < 25;
        }

        // 烟雾碎块
        if (Main.netMode != NetmodeID.Server) {
            for (int i = 0; i < 6; i++) {
                int goreType = Main.rand.Next(new int[] {
                    GoreID.Smoke1, GoreID.Smoke2, GoreID.Smoke3
                });
                Gore g = Gore.NewGorePerfect(Projectile.GetSource_Death(), Projectile.Center,
                    Main.rand.NextVector2CircularEdge(8f, 8f), goreType);
                g.timeLeft = 30;
            }
        }

        WeaponVFX.AddScreenShake(Projectile.Center, 6f + 2f * Cook01);
        Projectile.Kill();
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = TextureAssets.Projectile[Type].Value;

        // 心跳呼吸辉光 + 落地挤压变形
        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.5f * Projectile.scale * (1f + ProfaneCommon.Heartbeat() * 0.3f),
            ProfaneCommon.FleshMid * 0.45f);

        Vector2 squash = _squashTimer > 0
            ? new Vector2(1.2f, 0.8f) : Vector2.One;
        Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null,
            lightColor, Projectile.rotation, tex.Size() * 0.5f,
            squash * Projectile.scale, SpriteEffects.None, 0f);
        return false;
    }
}

/// <summary>
/// 肉瘤爆炸 - FleshPulse 血肉膜 + SlashBurst 放射。
/// ai[1]=半径倍率；伤害半径与膜/冲击环视觉严格对齐（上限≈205px）。
/// </summary>
public class TumorBlastVFX : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private ref float Timer => ref Projectile.ai[0];
    private float RadiusMult => Projectile.ai[1] <= 0f ? 1f : Projectile.ai[1];
    private const int DURATION = 45;
    private float Seed => (Projectile.whoAmI * 0.149f) % 1f;

    private float Radius => MathF.Min(24f + Timer * 13f, 152f) * RadiusMult;

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = DURATION;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 12;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Timer++;

        for (int i = 0; i < 6; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(Radius * 0.3f, Radius);
            Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                Main.rand.NextVector2Circular(2.5f, 2.5f), 0, default, 2f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.7f, 0.12f, 0.1f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 600);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, Radius, targetHitbox);
    }

    public override bool PreDraw(ref Color lightColor) {
        float prog = 1f - Projectile.timeLeft / (float)DURATION;
        float alpha = ACMUtils.QuadOut(1f - prog) * 0.90f;
        float scale = MathHelper.SmoothStep(0f, 16f, ACMUtils.QuadOut(prog));

        // 血肉膜扩张 (与伤害半径同式) + 大血肉冲击环
        ProfaneCommon.DrawFleshMembrane(Projectile.Center, Radius * 1.08f, alpha * 0.9f,
            1f - prog, veinBoost: 0.6f, seed: Seed);
        WeaponVFX.DrawShockwaveRing(Projectile.Center, Radius, 16f, alpha * 0.9f,
            ProfaneCommon.BloodBright, ProfaneCommon.FleshDark);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D sg = ACMAsset.SoftGlow;

        // SlashBurst放射
        for (int k = 0; k < 10; k++) {
            float bAngle = k * MathF.PI / 5f + Timer * 0.015f;
            bool strong = (k % 2 == 0);
            Color bColor = strong ? ProfaneCommon.FleshMid : ProfaneCommon.BloodBright;
            float bLen = strong ? scale * 0.55f : scale * 0.36f;
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                bColor * (alpha * 0.65f), bAngle,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.15f, bLen * RadiusMult), SpriteEffects.None, 0);
        }

        // 中心焦点闪
        float flashAlpha = MathHelper.SmoothStep(1f, 0f, prog * 2f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 200, 180) * (alpha * flashAlpha), 0f,
            sg.Size() * 0.5f,
            scale * 0.15f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 追踪血肉碎片 - 扩散后追踪最近敌人。
/// 使用BlankStar暗红色渲染。
/// </summary>
public class TumorFragment : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/BlankStar";

    private ref float Timer => ref Projectile.ai[0];
    private const int SPREAD_TIME = 20;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 0;
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
    }

    public override void SetDefaults() {
        Projectile.width = 12;
        Projectile.height = 12;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        Timer++;
        Projectile.rotation += 0.25f;
        Lighting.AddLight(Projectile.Center, 0.25f, 0.04f, 0.03f);

        if (Timer < SPREAD_TIME) {
            Projectile.velocity *= 0.96f;
        }
        else {
            // 追踪最近敌人
            NPC target = null;
            float closest = 900f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.friendly || n.dontTakeDamage) continue;
                float dist = Vector2.Distance(Projectile.Center, n.Center);
                if (dist < closest) { closest = dist; target = n; }
            }

            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 16f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.12f);
            }
            else {
                Projectile.velocity *= 0.97f;
            }
        }

        // 血液拖尾
        if (Timer % 2 == 0) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                -Projectile.velocity * 0.04f, 0, default, 1.2f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 240);
        for (int i = 0; i < 4; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.4f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 统一双层暗红血肉拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 7f,
            outerColor: ProfaneCommon.FleshDark, innerColor: ProfaneCommon.FleshMid,
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D star = ACMAsset.BlankStar;
        Texture2D sg = ACMAsset.SoftGlow;

        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            ProfaneCommon.FleshMid, Projectile.rotation,
            star.Size() * 0.5f,
            0.30f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(180, 35, 30) * 0.50f, 0f,
            sg.Size() * 0.5f,
            0.20f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
