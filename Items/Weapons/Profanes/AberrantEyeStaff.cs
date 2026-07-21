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
/// 畸变眼球杖 - 法师引导法杖。
/// 持续引导时"活体苏醒"ramp-up（90帧）：触手 2→3 道、散布收窄、弹速+30%，
/// 心跳音随 ramp 加速上行（45f→28f 间隔）——蓄力即听觉张力。
/// 每第6次释放畸变眼球（ProfaneGazeEye 渲染），直击 +2 剖检印。
/// </summary>
public class AberrantEyeStaff : ModItem
{
    private int _fireCount;
    private float _ramp;       // 引导苏醒度 0~1
    private int _thumpTimer;   // 心跳音计时

    public override void SetDefaults() {
        Item.damage = 1250;
        Item.crit = 8;
        Item.DamageType = DamageClass.Magic;
        Item.mana = 6;
        Item.width = 48;
        Item.height = 48;
        Item.useTime = 12;
        Item.useAnimation = 12;
        Item.knockBack = 3f;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.value = Item.buyPrice(gold: 85);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = null;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.staff[Type] = true;
        Item.shoot = ModContent.ProjectileType<AberrantEyeballProj>();
        Item.shootSpeed = 14f;
        Item.channel = true;
    }

    public override void HoldItem(Player player) {
        bool channeling = player.channel && !player.noItems && !player.CCed;
        if (channeling) {
            _ramp = MathF.Min(_ramp + 1f / 90f, 1f);

            // 心跳分层: 间隔 45f→28f, 音高上行 (活体苏醒的听觉张力)
            if (--_thumpTimer <= 0) {
                _thumpTimer = (int)MathHelper.Lerp(45f, 28f, _ramp);
                ProfaneCommon.PlayThump(player.Center,
                    pitch: MathHelper.Lerp(-0.9f, -0.45f, _ramp),
                    volume: 0.4f + 0.25f * _ramp);
            }

            // 苏醒期杖身泛血光粒子 (密度随 ramp)
            if (Main.rand.NextFloat() < 0.25f + 0.5f * _ramp) {
                Vector2 tip = player.MountedCenter + player.direction * new Vector2(30f, -8f);
                Dust d = Dust.NewDustPerfect(tip + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.Blood, Main.rand.NextVector2Circular(1.5f, 1.5f), 0, default, 1f + _ramp);
                d.noGravity = true;
            }
        }
        else {
            _ramp = MathF.Max(_ramp - 1f / 20f, 0f);
            _thumpTimer = 0;
        }
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        _fireCount++;
        Vector2 staffTip = position + velocity.SafeNormalize(Vector2.UnitX) * 46f;
        float speedMult = 1f + 0.3f * _ramp;

        if (_fireCount % 6 == 0) {
            // 每6次释放畸变眼球
            Projectile.NewProjectile(source, staffTip, velocity * 0.8f * speedMult,
                ModContent.ProjectileType<AberrantEyeballProj>(),
                damage * 3, knockback * 2f, player.whoAmI);
            SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.5f, Pitch = 0.3f }, position);
        }
        else {
            // 追踪触手: 苏醒后 2→3 道, 散布收窄 (0.35→0.22 rad)
            int count = _ramp > 0.6f ? 3 : 2;
            float spread = MathHelper.Lerp(0.35f, 0.22f, _ramp);
            for (int i = 0; i < count; i++) {
                Vector2 tVel = velocity.RotatedByRandom(spread) * Main.rand.NextFloat(0.85f, 1.15f) * speedMult;
                Projectile.NewProjectile(source, staffTip, tVel,
                    ModContent.ProjectileType<ProfaneTendrilChaser>(),
                    damage / 2, 2f, player.whoAmI, 0f, ProfaneTendrilChaser.ClassMagic);
            }
            SoundEngine.PlaySound(SoundID.NPCHit18 with {
                Volume = 0.3f, Pitch = 0.5f + 0.2f * _ramp + Main.rand.NextFloat(-0.08f, 0.08f)
            }, position);
        }

        // 杖尖血液粒子
        for (int i = 0; i < 4; i++) {
            Vector2 dustVel = velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.5f)
                * Main.rand.NextFloat(3f, 7f);
            Dust d = Dust.NewDustPerfect(staffTip, DustID.Blood, dustVel, 0, default, 1.5f);
            d.noGravity = true;
        }

        return false;
    }
}

/// <summary>
/// 畸变眼球 - 飞行追踪眼球弹幕（ProfaneGazeEye 程序化渲染）。
/// 追踪最近敌人；直击 +2 剖检印；碰撞爆炸释放环形血液弹幕。
/// </summary>
public class AberrantEyeballProj : ModProjectile
{
    public override string Texture => "InnoVault/Assets/placeholder";

    private ref float Timer => ref Projectile.ai[0];
    private Vector2 _gazeDir;
    private float _lockAmt;
    private float Seed => (Projectile.whoAmI * 0.219f) % 1f;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 0;
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
    }

    public override void SetDefaults() {
        Projectile.width = 18;
        Projectile.height = 18;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 240;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        Timer++;
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.45f, 0.2f, 0.06f);

        // 追踪
        NPC target = null;
        float closest = 800f;
        if (Timer > 8) {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.friendly || n.dontTakeDamage) continue;
                float dist = Vector2.Distance(Projectile.Center, n.Center);
                if (dist < closest) { closest = dist; target = n; }
            }

            if (target != null) {
                _gazeDir = Projectile.DirectionTo(target.Center);
                Vector2 desired = _gazeDir * MathHelper.Max(Projectile.velocity.Length(), 14f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.06f);
                _lockAmt = MathHelper.Lerp(_lockAmt, closest < 220f ? 1f : 0f, 0.14f);
            }
            else {
                _gazeDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                _lockAmt = MathHelper.Lerp(_lockAmt, 0f, 0.1f);
            }
        }

        // 血液拖尾
        if (Timer % 2 == 0) {
            Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(5, 5),
                DustID.Blood, -Projectile.velocity * 0.04f, 0, default, 1.3f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        // 直击 +2 印; 摘取伤害 = 面板×2 (眼球伤害为面板×3 → ×2/3)
        ProfaneCommon.AddMark(target, Projectile, 2, Projectile.damage * 2 / 3);
    }

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
        ProfaneCommon.PlaySquelch(Projectile.Center, 0.9f, -0.1f);

        // 畸变眼球爆裂血肉演出
        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
            ACMWeaponBurst.Profane, scale: 0.9f, owner: Projectile.owner);

        if (Main.myPlayer == Projectile.owner) {
            // 环形血液弹幕(8发)
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8;
                Vector2 bVel = angle.ToRotationVector2() * 9f;
                Projectile.NewProjectile(
                    Projectile.GetSource_Death(), Projectile.Center, bVel,
                    ModContent.ProjectileType<AberrantBloodBurst>(),
                    Projectile.damage / 3, 2f, Projectile.owner);
            }
        }

        // 血液爆发
        for (int i = 0; i < 25; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(7f, 7f);
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                vel, 0, default, Main.rand.NextFloat(1.8f, 3f));
            d.noGravity = true;
        }

        WeaponVFX.AddScreenShake(Projectile.Center, 3f);
    }

    public override bool PreDraw(ref Color lightColor) {
        // 统一双层暗红血肉拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 9f,
            outerColor: ProfaneCommon.FleshDark, innerColor: ProfaneCommon.BloodBright,
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);

        // 程序化畸变眼球: 睁眼 12f, 心跳呼吸
        float open = ACMUtils.QuadOut(MathHelper.Clamp(Timer / 12f, 0f, 1f));
        float breathe = 1f + ProfaneCommon.Heartbeat(Seed) * 0.08f;
        ProfaneCommon.DrawGazeEye(Projectile.Center, 58f * breathe, _gazeDir * 0.9f,
            open, _lockAmt, 1f, Seed, Projectile.rotation * 0.2f);

        // 眼底脉冲辉光
        float pulse = 0.35f + 0.18f * ProfaneCommon.Heartbeat(Seed);
        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.45f + pulse * 0.3f, new Color(180, 30, 25));
        return false;
    }
}

/// <summary>
/// 环形血液弹 - 眼球爆炸后释放的血弹。
/// 短距离飞行+穿透，LightShot暗红渲染。
/// </summary>
public class AberrantBloodBurst : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 60;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.extraUpdates = 1;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.velocity *= 0.97f;
        Lighting.AddLight(Projectile.Center, 0.25f, 0.04f, 0.03f);

        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                -Projectile.velocity * 0.03f, 0, default, 1.1f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 240);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(200, 30, 20), Projectile.rotation,
            lsh.Size() * 0.5f,
            new Vector2(0.35f, 0.09f), SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
