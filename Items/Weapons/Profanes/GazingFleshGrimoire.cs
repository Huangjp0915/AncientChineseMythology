using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Profanes;

/// <summary>
/// 凝视肉典 - 法师魔法书（魔法副旗舰）。
/// 扇形喷射7道血液弹幕（总面板与旧10道一致）；
/// 每第4次施法释放凝视巨眼（ProfaneGazeEye 程序化之眼：睁眼→追踪→锁定→爆裂），
/// 巨眼直击叠2层剖检印，爆裂放出8道追踪触手。
/// </summary>
public class GazingFleshGrimoire : ModItem
{
    private int _castCount;

    public override void SetDefaults() {
        Item.damage = 1300;
        Item.crit = 10;
        Item.DamageType = DamageClass.Magic;
        Item.mana = 14;
        Item.width = 32;
        Item.height = 34;
        Item.useTime = 22;
        Item.useAnimation = 22;
        Item.knockBack = 4f;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.value = Item.buyPrice(gold: 85);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.NPCDeath13 with { Pitch = 0.2f, Volume = 0.7f };
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<GrimoireBloodBolt>();
        Item.shootSpeed = 18f;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        _castCount++;
        Vector2 baseDir = velocity.SafeNormalize(Vector2.UnitX);
        Vector2 muzzle = position + baseDir * 20f;

        if (_castCount % 4 == 0) {
            // 每4次施法: 凝视巨眼 (睁眼三段律的载体)
            Projectile.NewProjectile(source, muzzle, velocity * 0.7f,
                ModContent.ProjectileType<GrimoireGazingEye>(),
                damage * 3, knockback * 2.5f, player.whoAmI);
            SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = 0.5f, Volume = 0.6f }, position);
            ProfaneCommon.PlaySquelch(muzzle, 0.9f, 0.1f);
        }

        // 扇形散射7道血液弹 (原10道; 单发×10/7, 总面板不变, 批次成本 -30%)
        float spreadAngle = MathHelper.ToRadians(26);
        const int bolts = 7;
        int boltDamage = (int)(damage * 10f / 7f);
        for (int i = 0; i < bolts; i++) {
            float angle = MathHelper.Lerp(-spreadAngle, spreadAngle, i / (float)(bolts - 1));
            angle += Main.rand.NextFloat(-0.03f, 0.03f);
            Vector2 boltVel = velocity.RotatedBy(angle) * Main.rand.NextFloat(0.90f, 1.10f);
            Projectile.NewProjectile(source, muzzle, boltVel, type, boltDamage, knockback, player.whoAmI);
        }

        // 施法后坐 (吐出血弹的"呕"感) + 血液粒子
        if (player.whoAmI == Main.myPlayer)
            player.velocity -= baseDir * 2f;
        for (int i = 0; i < 8; i++) {
            Vector2 dustVel = baseDir.RotatedByRandom(0.6f) * Main.rand.NextFloat(4f, 8f);
            Dust d = Dust.NewDustPerfect(muzzle, DustID.Blood, dustVel, 0, default, 1.8f);
            d.noGravity = true;
        }

        return false;
    }
}

/// <summary>
/// 血液弹 - 高速穿透血柱。
/// 暗红色LightShot渲染，短距离消失。
/// </summary>
public class GrimoireBloodBolt : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 0;
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
    }

    public override void SetDefaults() {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 45;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.extraUpdates = 2;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.30f, 0.05f, 0.04f);

        if (Projectile.timeLeft % 3 == 0) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                -Projectile.velocity * 0.04f, 0, default, 1.0f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 180);
    }

    public override void OnKill(int timeLeft) {
        for (int i = 0; i < 4; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.3f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 统一双层暗红血肉拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 6f,
            outerColor: ProfaneCommon.FleshDark, innerColor: ProfaneCommon.BloodBright,
            uvScroll: -Main.GlobalTimeWrappedHourly * 2f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(220, 40, 30), Projectile.rotation,
            lsh.Size() * 0.5f,
            new Vector2(0.40f, 0.08f), SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 凝视巨眼 - ProfaneGazeEye 程序化之眼。
/// 凝视三段律：飞行中睁眼 → 瞳孔追踪最近敌人 → 接近后竖瞳锁定（震颤+红环）→ 爆裂。
/// 直击 +2 剖检印；爆裂释放8道追踪触手（魔法）。
/// </summary>
public class GrimoireGazingEye : ModProjectile
{
    public override string Texture => "InnoVault/Assets/placeholder";

    private ref float Timer => ref Projectile.ai[0];

    private float _lockAmt;     // 锁定态 0~1 (纯视觉, 各端独立计算)
    private Vector2 _gazeDir;   // 瞳孔视线
    private float Seed => (Projectile.whoAmI * 0.173f) % 1f;

    public override void SetDefaults() {
        Projectile.width = 20;
        Projectile.height = 20;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        Timer++;
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.55f, 0.28f, 0.10f);

        // 追踪最近敌人 (锁定后咬得更紧)
        NPC target = null;
        float closest = 700f;
        for (int i = 0; i < Main.maxNPCs; i++) {
            NPC n = Main.npc[i];
            if (!n.active || n.friendly || n.dontTakeDamage) continue;
            float dist = Vector2.Distance(Projectile.Center, n.Center);
            if (dist < closest) { closest = dist; target = n; }
        }

        if (target != null) {
            _gazeDir = Projectile.DirectionTo(target.Center);
            if (Timer > 10) {
                float steer = _lockAmt > 0.5f ? 0.075f : 0.04f;
                Vector2 desired = _gazeDir * Projectile.velocity.Length();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, steer);
            }
            // 锁定三段律: 接近 260px 内瞳孔收缩咬死
            float lockTarget = closest < 260f ? 1f : 0f;
            float prevLock = _lockAmt;
            _lockAmt = MathHelper.Lerp(_lockAmt, lockTarget, 0.15f);
            if (prevLock < 0.5f && _lockAmt >= 0.5f)
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.5f, Pitch = 0.55f }, Projectile.Center);
        }
        else {
            _gazeDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            _lockAmt = MathHelper.Lerp(_lockAmt, 0f, 0.1f);
        }

        // 血泪拖尾
        if (Timer % 2 == 0) {
            Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6, 6),
                DustID.Blood, -Projectile.velocity * 0.05f, 0, default, 1.4f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        // 直击 +2 印; 摘取伤害 = 面板×2 (巨眼伤害为面板×3 → ×2/3)
        ProfaneCommon.AddMark(target, Projectile, 2, Projectile.damage * 2 / 3);
    }

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
        ProfaneCommon.PlaySquelch(Projectile.Center, 1.1f, 0f);

        // 凝视巨眼爆裂血肉演出
        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
            ACMWeaponBurst.Profane, scale: 1.2f, owner: Projectile.owner);

        if (Main.myPlayer == Projectile.owner) {
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8 + Main.rand.NextFloat(-0.15f, 0.15f);
                Vector2 fVel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 10f);
                Projectile.NewProjectile(
                    Projectile.GetSource_Death(), Projectile.Center, fVel,
                    ModContent.ProjectileType<ProfaneTendrilChaser>(),
                    Projectile.damage / 3, 2f, Projectile.owner, 0f, ProfaneTendrilChaser.ClassMagic);
            }
        }

        // 玻璃体血液爆发
        for (int i = 0; i < 25; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                vel, 0, default, Main.rand.NextFloat(2f, 3f));
            d.noGravity = true;
        }

        WeaponVFX.AddScreenShake(Projectile.Center, 4f);
    }

    public override bool PreDraw(ref Color lightColor) {
        // 拖尾血痕
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 11f,
            outerColor: ProfaneCommon.FleshDark, innerColor: ProfaneCommon.FleshMid,
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);

        // 程序化凝视之眼: 睁眼 20f → 追踪 → 锁定收瞳
        float open = ACMUtils.QuadOut(MathHelper.Clamp(Timer / 20f, 0f, 1f));
        float breathe = 1f + ProfaneCommon.Heartbeat(Seed) * 0.06f;
        ProfaneCommon.DrawGazeEye(Projectile.Center, 92f * breathe, _gazeDir * 0.9f,
            open, _lockAmt, 1f, Seed, Projectile.rotation * 0.15f);

        // 眼底肉光
        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.5f + 0.15f * ProfaneCommon.Heartbeat(Seed),
            ProfaneCommon.FleshMid * 0.5f);
        return false;
    }
}
