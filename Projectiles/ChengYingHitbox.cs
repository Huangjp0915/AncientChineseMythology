using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Projectiles;

/// <summary>
/// 承影剑接触判定盒 + 影剑演出载体 (由 ChengYingMount 骑乘时生成, 常驻刷新)。
/// 「昧爽之交, 淡淡焉若有物存」—— 影剑语言全部由速度门控:
///   静止近乎无形; |v|&gt;6 拉出淡青白剑影拖尾; |v|&gt;7 落下折影残像 (溶解微颤);
///   |v|&gt;9 剑尖流光 + 风鸣。接触伤害随骑乘速度缩放 (0.75~1.5×), 全速冲撞触发「破影一闪」。
/// </summary>
public class ChengYingHitbox : ModProjectile
{
    public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

    // 影剑主题色 (淡青白)
    private static readonly Color ShadowPale = new(150, 185, 215);
    private static readonly Color ShadowCore = new(230, 245, 255);

    private static Texture2D texSword;

    private static void EnsureTexture() {
        texSword ??= ModContent.Request<Texture2D>(
            "AncientChineseMythology/Textures/Mounts/ChengYing/ChengYing", AssetRequestMode.ImmediateLoad).Value;
    }

    // 折影残像快照 (纯视觉, 逐客户端)
    private struct GhostSnap
    {
        public Vector2 Pos;
        public int Dir;
        public float Life; // 1→0
    }

    private readonly GhostSnap[] _ghosts = new GhostSnap[5];
    private int _ghostHead;
    private int _ghostCooldown;
    private int _whooshCooldown;
    private float _speed; // 本帧骑手速度 (绘制/判定共用)

    private ref float BaseDamage => ref Projectile.ai[2]; // 首帧捕获生成伤害作为基准

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
        ProjectileID.Sets.TrailingMode[Type] = 0;
    }

    public override void SetDefaults() {
        Projectile.width = 96;
        Projectile.height = 34;
        Projectile.friendly = true;
        Projectile.tileCollide = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 2;          // 由 AI 每帧续命, 下马后 SwordMountPlayer 查杀
        Projectile.DamageType = DamageClass.Melee;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 20; // 单次撞击更重、频率更低 (替代旧全局 10f 贴脸磨血)
    }

    public override void AI() {
        Player player = Main.player[Projectile.owner];

        if (BaseDamage <= 0f)
            BaseDamage = Projectile.damage; // 捕获坐骑生成时的基准伤害 (60)

        // 定位到骑手, 前移让剑尖判定 (修复旧版向后偏移导致冲撞打不到的问题)
        Projectile.Center = player.Center + new Vector2(16f * player.direction, 4f);
        Projectile.direction = player.direction;
        Projectile.timeLeft = 2;

        // 速度即锋芒: 伤害随骑乘速度缩放 0.75×~1.5×
        _speed = player.velocity.Length();
        float speedFrac = MathHelper.Clamp(_speed / 12f, 0f, 1f);
        Projectile.damage = (int)(BaseDamage * (0.75f + 0.75f * speedFrac));

        if (Main.dedServ)
            return;

        // 折影残像采样 (速度门控)
        if (_ghostCooldown > 0)
            _ghostCooldown--;
        if (_speed > 7f && _ghostCooldown <= 0) {
            _ghostCooldown = 5;
            _ghosts[_ghostHead] = new GhostSnap { Pos = Projectile.Center, Dir = player.direction, Life = 1f };
            _ghostHead = (_ghostHead + 1) % _ghosts.Length;
        }
        for (int i = 0; i < _ghosts.Length; i++)
            if (_ghosts[i].Life > 0f)
                _ghosts[i].Life -= 0.055f;

        // 高速风鸣 (音高随速度)
        if (_whooshCooldown > 0)
            _whooshCooldown--;
        if (_speed > 9f && _whooshCooldown <= 0) {
            _whooshCooldown = 12;
            SoundEngine.PlaySound(SoundID.Item1 with {
                Volume = 0.18f,
                Pitch = 0.2f + speedFrac * 0.25f + Main.rand.NextFloat(-0.05f, 0.05f)
            }, Projectile.Center);
        }

        // 剑刃微光 (速度越快越亮)
        Lighting.AddLight(Projectile.Center, ShadowPale.ToVector3() * (0.15f + 0.35f * speedFrac));
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        bool ram = _speed > 10f; // 全速冲撞 =「破影一闪」
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.Shadow, scale: ram ? 1.3f : 0.85f, owner: Projectile.owner);
        WeaponVFX.AddScreenShake(target.Center, ram ? 3f : 2f);
        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = ram ? 0.35f : 0f }, target.Center);

        for (int i = 0; i < (ram ? 14 : 8); i++) {
            Dust d = Dust.NewDustPerfect(target.Center,
                Main.rand.NextBool() ? DustID.IceTorch : DustID.SilverFlame,
                Main.rand.NextVector2CircularEdge(5f, 5f) * Main.rand.NextFloat(0.4f, 1f), 130, default, 1.2f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        if (Main.dedServ)
            return false;
        EnsureTexture();
        if (texSword == null)
            return false;

        Player player = Main.player[Projectile.owner];
        float speedFrac = MathHelper.Clamp(_speed / 12f, 0f, 1f);

        // 剑影拖尾 (|v|>6 渐显, 透明度 ∝ 速度)
        if (_speed > 6f) {
            float a = MathHelper.Clamp((_speed - 6f) / 6f, 0f, 1f);
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 14f,
                outerColor: ShadowPale * (0.5f * a), innerColor: ShadowCore * (0.75f * a),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);
        }

        // 折影残像: 半溶解的剑形虚影, threshold 随时间微颤 →"淡淡焉若有物存"
        for (int i = 0; i < _ghosts.Length; i++) {
            GhostSnap g = _ghosts[i];
            if (g.Life <= 0f)
                continue;
            float tremble = 0.42f + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + i * 1.7f);
            SpriteEffects flip = g.Dir == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            WeaponVFX.ApplyDissolveBurn(texSword, g.Pos, null, ShadowPale * (0.5f * g.Life),
                0f, texSword.Size() * 0.5f, 1f, threshold: tremble + (1f - g.Life) * 0.4f,
                intensity: g.Life * 0.8f, edgeColor: new Color(200, 235, 255), edgeWidth: 0.10f,
                noiseScale: 2.5f, effects: flip);
        }

        // 剑尖流光 (|v|>9)
        if (_speed > 9f && ACMAsset.LightShot != null) {
            Vector2 tip = Projectile.Center + new Vector2(player.direction * 46f, 0f);
            Color glint = ShadowCore * (0.35f + 0.5f * speedFrac);
            glint.A = 0;
            Main.EntitySpriteDraw(ACMAsset.LightShot, tip - Main.screenPosition, null, glint,
                player.velocity.ToRotation(), ACMAsset.LightShot.Size() * 0.5f,
                new Vector2(0.7f + speedFrac * 0.5f, 0.25f), SpriteEffects.None, 0);
        }

        return false;
    }
}
