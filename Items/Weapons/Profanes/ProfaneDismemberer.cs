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
/// 亵渎系列公共层：统一血肉配色、72BPM 心跳波形、湿裂分层音、
/// 剖检印（系列机制）API 与专属着色器绘制助手。
/// 系列所有武器只消费本类，配色与节律的唯一事实来源。
/// </summary>
public static class ProfaneCommon
{
    // ===== 系列统一配色 =====
    /// <summary>外层暗血。</summary>
    public static readonly Color FleshDark = new(92, 8, 24);
    /// <summary>中间层肉红。</summary>
    public static readonly Color FleshMid = new(170, 24, 34);
    /// <summary>内亮动脉红。</summary>
    public static readonly Color BloodBright = new(248, 64, 96);
    /// <summary>肌腱苍白（膜边/骨白）。</summary>
    public static readonly Color SinewPale = new(235, 190, 170);
    /// <summary>凝视金瞳（只给"眼"语言，与血红分工）。</summary>
    public static readonly Color EyeGold = new(250, 208, 130);

    /// <summary>剖检印上限，叠满自动触发摘取。</summary>
    public const int MaxMarks = 5;

    // ===== 心跳节律 (系列签名: 72BPM 双搏 "咚-咚—") =====

    /// <summary>双搏波形：输入一个心动周期内的相位 0~1，输出 0~1（主搏 t=0，次搏 t=0.3）。</summary>
    public static float BeatWave(float phase01) {
        float t = phase01 - MathF.Floor(phase01);
        float lub = MathF.Exp(-t * 14f);
        float t2 = t - 0.30f;
        float dub = t2 > 0f ? 0.55f * MathF.Exp(-t2 * 16f) : 0f;
        return MathHelper.Clamp(lub + dub, 0f, 1f);
    }

    /// <summary>全局心跳（72BPM，锁 GlobalTime；全系列弹幕同相呼吸）。</summary>
    public static float Heartbeat(float phaseOffset = 0f) {
        const float cycle = 60f / 72f;
        return BeatWave(((float)Main.GlobalTimeWrappedHourly + phaseOffset) / cycle);
    }

    // ===== 分层音效 =====

    /// <summary>湿裂音（低频钝击 + 高频湿滑），pitch 供连击/层数上行。</summary>
    public static void PlaySquelch(Vector2 pos, float volume = 1f, float pitch = 0f) {
        SoundEngine.PlaySound(SoundID.NPCHit18 with {
            Volume = 0.7f * volume,
            Pitch = pitch - 0.35f + Main.rand.NextFloat(-0.08f, 0.08f)
        }, pos);
        SoundEngine.PlaySound(SoundID.NPCDeath13 with {
            Volume = 0.4f * volume,
            Pitch = pitch + 0.25f + Main.rand.NextFloat(-0.12f, 0.12f)
        }, pos);
    }

    /// <summary>低闷心跳"咚"，蓄力分层用（pitch 随心率上行）。</summary>
    public static void PlayThump(Vector2 pos, float pitch = -0.85f, float volume = 0.55f) {
        SoundEngine.PlaySound(SoundID.Dig with { Volume = volume, Pitch = pitch, MaxInstances = 3 }, pos);
    }

    // ===== 剖检印 API =====

    /// <summary>
    /// 给敌人叠剖检印（owner 客户端累计，与仓库 _castCount 模式一致）。
    /// 叠满 <see cref="MaxMarks"/> 层立即清零并触发摘取处决（<see cref="ProfaneHarvestBurst"/>，
    /// 伤害= <paramref name="harvestDamage"/>，伤害类型继承 <paramref name="source"/>）。
    /// </summary>
    public static void AddMark(NPC target, Projectile source, int stacks, int harvestDamage) {
        if (target == null || !target.active || target.friendly || target.life <= 0 || stacks <= 0)
            return;
        if (Main.myPlayer != source.owner)
            return;
        if (!target.TryGetGlobalNPC(out ProfaneMarkNPC mark))
            return;

        mark.Stacks += stacks;
        mark.Timer = ProfaneMarkNPC.Decay;
        SoundEngine.PlaySound(SoundID.NPCHit18 with {
            Volume = 0.3f, Pitch = -0.2f + 0.12f * mark.Stacks
        }, target.Center);

        if (mark.Stacks >= MaxMarks) {
            mark.Stacks = 0;
            int classCode = source.DamageType == DamageClass.Melee ? 0
                : source.DamageType == DamageClass.Ranged ? 1 : 2;
            Projectile.NewProjectile(source.GetSource_FromThis(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<ProfaneHarvestBurst>(), harvestDamage, 8f,
                source.owner, classCode, target.whoAmI);
        }
    }

    // ===== 专属着色器绘制助手 (须在有活动批阶段调用; 服务端/丢着色器自动退化) =====

    private static void DrawShaderQuad(Effect fx, Vector2 worldCenter, Vector2 scalePx, float rotation) {
        Texture2D noise = ACMShaders.NoiseTexture;
        if (noise == null)
            return;
        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
            DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
        sb.Draw(noise, worldCenter - Main.screenPosition, null, Color.White, rotation,
            noise.Size() * 0.5f, scalePx / noise.Width, SpriteEffects.None, 0f);
        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);
    }

    /// <summary>径向血肉膜（ProfaneFleshPulse uMode=1）：爆炸/摘取/烹煮膨胀。</summary>
    public static void DrawFleshMembrane(Vector2 worldCenter, float radiusPx, float intensity,
        float pulse, float veinBoost = 0f, float seed = 0f) {
        if (Main.dedServ || intensity <= 0.01f || radiusPx < 2f)
            return;
        Effect fx = WeaponVFX.GetEffect("ProfaneFleshPulse");
        if (fx == null) {
            WeaponVFX.DrawGlowBurst(worldCenter, radiusPx / 32f, FleshMid * intensity);
            return;
        }
        SetFleshParams(fx, intensity, pulse, veinBoost, seed, mode: 1f);
        DrawShaderQuad(fx, worldCenter, new Vector2(radiusPx * 2f), 0f);
    }

    /// <summary>方向血肉波（ProfaneFleshPulse uMode=0）：刀波/冲击带。</summary>
    public static void DrawFleshWave(Vector2 worldCenter, float rotation, Vector2 sizePx,
        float intensity, float pulse, float seed = 0f, float veinBoost = 0f) {
        if (Main.dedServ || intensity <= 0.01f)
            return;
        Effect fx = WeaponVFX.GetEffect("ProfaneFleshPulse");
        if (fx == null) {
            WeaponVFX.DrawGlowBurst(worldCenter, sizePx.Y / 40f, FleshMid * intensity);
            return;
        }
        SetFleshParams(fx, intensity, pulse, veinBoost, seed, mode: 0f);
        DrawShaderQuad(fx, worldCenter, sizePx, rotation);
    }

    private static void SetFleshParams(Effect fx, float intensity, float pulse, float veinBoost, float seed, float mode) {
        fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
        fx.Parameters["uPulse"]?.SetValue(MathHelper.Clamp(pulse, 0f, 1f));
        fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
        fx.Parameters["uMode"]?.SetValue(mode);
        fx.Parameters["uSeed"]?.SetValue(seed);
        fx.Parameters["uVeinBoost"]?.SetValue(veinBoost);
        fx.Parameters["uColorDark"]?.SetValue(FleshDark.ToVector4());
        fx.Parameters["uColorBright"]?.SetValue(BloodBright.ToVector4());
        fx.Parameters["uColorPale"]?.SetValue(SinewPale.ToVector4());
    }

    /// <summary>凝视之眼（ProfaneGazeEye）：巨眼/眼球弹/处决眼闪。</summary>
    public static void DrawGazeEye(Vector2 worldCenter, float widthPx, Vector2 gazeDir,
        float open, float lockAmt, float intensity, float seed, float rotation = 0f) {
        if (Main.dedServ || intensity <= 0.01f || open <= 0.01f)
            return;
        Effect fx = WeaponVFX.GetEffect("ProfaneGazeEye");
        if (fx == null) {
            WeaponVFX.DrawGlowBurst(worldCenter, widthPx / 96f, EyeGold * (intensity * open));
            return;
        }
        fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
        fx.Parameters["uOpen"]?.SetValue(MathHelper.Clamp(open, 0f, 1f));
        fx.Parameters["uLock"]?.SetValue(MathHelper.Clamp(lockAmt, 0f, 1f));
        fx.Parameters["uGazeDir"]?.SetValue(gazeDir);
        fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
        fx.Parameters["uSeed"]?.SetValue(seed);
        fx.Parameters["uIrisColor"]?.SetValue(EyeGold.ToVector4());
        fx.Parameters["uScleraColor"]?.SetValue(SinewPale.ToVector4());
        fx.Parameters["uVeinColor"]?.SetValue(BloodBright.ToVector4());
        DrawShaderQuad(fx, worldCenter, new Vector2(widthPx, widthPx * 0.72f), rotation);
    }
}

/// <summary>
/// 剖检印数据（每 NPC 实例）：owner 客户端累计层数，头顶画缝合眼标记。
/// 层数只用于本地触发摘取弹幕生成（弹幕本身走网络同步），无需净同步。
/// </summary>
public class ProfaneMarkNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;

    /// <summary>印记衰减帧（6 秒无续叠清零）。</summary>
    public const int Decay = 360;

    public int Stacks;
    public int Timer;

    public override void PostAI(NPC npc) {
        if (Stacks <= 0)
            return;
        if (--Timer <= 0)
            Stacks = 0;
    }

    public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
        if (Stacks <= 0 || Main.dedServ)
            return;
        Texture2D pip = ACMAsset.Sparkle;
        if (pip == null)
            return;

        float beat = ProfaneCommon.Heartbeat();
        // 4 层起加速闪烁预警 (即将摘取)
        float warn = Stacks >= ProfaneCommon.MaxMarks - 1 ? 0.35f + 0.65f * ProfaneCommon.BeatWave((float)Main.GlobalTimeWrappedHourly * 2.4f) : 1f;
        float spacing = 11f;
        Vector2 basePos = npc.Top - screenPos + new Vector2(-(Stacks - 1) * spacing * 0.5f, -16f);
        for (int i = 0; i < Stacks && i < ProfaneCommon.MaxMarks; i++) {
            // NPC 批为 AlphaBlend, 保留 alpha 权重 (置 0 会整体不可见)
            Color c = Color.Lerp(ProfaneCommon.FleshMid, ProfaneCommon.BloodBright, i / 4f) * (0.85f * warn);
            float s = 0.085f * (1f + beat * 0.25f);
            spriteBatch.Draw(pip, basePos + new Vector2(i * spacing, 0f), null, c,
                0f, pip.Size() * 0.5f, s, SpriteEffects.None, 0f);
        }
    }
}

/// <summary>
/// 摘取处决（系列大招时刻）：凝视金瞳睁开 → 猛然闭合（收缩静默）→ 血肉爆纹。
/// ai[0]=伤害类型码(0近战/1远程/2魔法)，ai[1]=目标 NPC 索引（睁眼期跟随）。
/// 伤害由 AddMark 生成时传入（触发武器面板 ×2）。
/// </summary>
public class ProfaneHarvestBurst : ModProjectile
{
    public override string Texture => "InnoVault/Assets/placeholder";

    private const int LifeTime = 40;
    private const int EyeOpenEnd = 8;    // 0~8f 睁眼凝视
    private const int EyeShutEnd = 12;   // 9~12f 闭合静默
    private const int BoomFrame = 13;    // 起爆帧
    private const int DamageEnd = 24;    // 伤害窗口结束

    private int LifeFrame => LifeTime - Projectile.timeLeft;
    private float Seed => (Projectile.whoAmI * 0.137f) % 1f;

    public override void SetStaticDefaults() {
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.ProfaneHarvestBurst.DisplayName",
            () => "Vivisection Harvest");
    }

    public override void SetDefaults() {
        Projectile.width = 30;
        Projectile.height = 30;
        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.timeLeft = LifeTime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1; // 单次判定
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Projectile.DamageType = Projectile.ai[0] switch {
            0f => DamageClass.Melee,
            1f => DamageClass.Ranged,
            _ => DamageClass.Magic,
        };

        int frame = LifeFrame;
        int targetIdx = (int)Projectile.ai[1];

        if (frame <= EyeShutEnd) {
            // 睁眼期跟随目标 (目标死亡则原地)
            if (targetIdx >= 0 && targetIdx < Main.maxNPCs) {
                NPC t = Main.npc[targetIdx];
                if (t.active && t.life > 0)
                    Projectile.Center = t.Center;
            }
            // 汇聚血线 (蓄力语法: 收束粒子), 闭合期硬切 → 爆发前静默
            if (frame <= EyeOpenEnd) {
                for (int i = 0; i < 3; i++) {
                    Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(150f, 150f);
                    Dust d = Dust.NewDustPerfect(from, DustID.Blood,
                        (Projectile.Center - from) * 0.09f, 0, default, 1.6f);
                    d.noGravity = true;
                }
            }
            if (frame == EyeOpenEnd)
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.8f, Pitch = -0.4f }, Projectile.Center);
        }
        else if (frame == BoomFrame) {
            Detonate();
        }

        Lighting.AddLight(Projectile.Center, 0.7f, 0.1f, 0.12f);
    }

    private void Detonate() {
        // 起爆帧: 一帧内堆满冲击链 (音 → 震 → burst → 粒子 → 治疗)
        ProfaneCommon.PlaySquelch(Projectile.Center, 1.4f, -0.25f);
        SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 1.1f, Pitch = -0.6f }, Projectile.Center);
        WeaponVFX.AddScreenShake(Projectile.Center, 9f);

        ACMWeaponBurst.Spawn(Projectile.GetSource_FromThis(), Projectile.Center,
            ACMWeaponBurst.LethalRed, scale: 2.0f, owner: Projectile.owner);

        for (int i = 0; i < 36; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(11f, 11f) * Main.rand.NextFloat(0.4f, 1f);
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, vel, 0, default,
                Main.rand.NextFloat(1.8f, 3.2f));
            d.noGravity = i < 24;
        }

        if (Main.myPlayer == Projectile.owner) {
            Player owner = Main.player[Projectile.owner];
            if (owner.active && !owner.dead && owner.statLife < owner.statLifeMax2) {
                owner.Heal(20);
            }
        }
    }

    public override bool? CanDamage() => LifeFrame is >= BoomFrame and <= DamageEnd ? null : false;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        int frame = LifeFrame;
        if (frame < BoomFrame || frame > DamageEnd)
            return false;
        float radius = MathHelper.Lerp(46f, 190f, ACMUtils.QuadOut((frame - BoomFrame) / (float)(DamageEnd - BoomFrame)));
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 480);
    }

    public override bool PreDraw(ref Color lightColor) {
        int frame = LifeFrame;
        Vector2 eyePos = Projectile.Center + new Vector2(0f, -64f);

        if (frame <= EyeShutEnd) {
            // 凝视三段律: 睁眼 (QuadOut) → 咬死 (lock) → 猛然闭合
            float open = frame <= EyeOpenEnd
                ? ACMUtils.QuadOut(frame / (float)EyeOpenEnd)
                : 1f - ACMUtils.QuadIn((frame - EyeOpenEnd) / (float)(EyeShutEnd - EyeOpenEnd));
            float lockAmt = MathHelper.Clamp(frame / (float)EyeOpenEnd, 0f, 1f);
            ProfaneCommon.DrawGazeEye(eyePos, 150f, new Vector2(0f, 0.5f), open, lockAmt, 1f, Seed);
            // 目标身上的凝视投光
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.8f + lockAmt * 0.5f,
                ProfaneCommon.EyeGold * (0.35f * open));
        }
        else {
            float boomT = MathHelper.Clamp((frame - BoomFrame) / (float)(LifeTime - BoomFrame), 0f, 1f);
            float fade = 1f - ACMUtils.QuadIn(boomT);

            // 血肉膜扩张 + 冲击环 (伤害半径与视觉严格对齐)
            float radius = MathHelper.Lerp(46f, 190f, ACMUtils.QuadOut(MathHelper.Clamp(
                (frame - BoomFrame) / (float)(DamageEnd - BoomFrame), 0f, 1f)));
            ProfaneCommon.DrawFleshMembrane(Projectile.Center, radius * 1.05f, fade * 0.95f,
                1f - boomT, veinBoost: 0.8f, seed: Seed);
            WeaponVFX.DrawShockwaveRing(Projectile.Center, radius, 14f, fade * 0.85f,
                ProfaneCommon.BloodBright, ProfaneCommon.FleshDark);

            // 处决定调: 短暂染屏 (≤10 帧, 走全屏名额契约, 强度 ≤0.12)
            if (frame <= BoomFrame + 9) {
                WeaponVFX.ApplyPaletteTint(Main.spriteBatch,
                    shadowTint: new Color(60, 4, 14, 255),
                    highlightTint: new Color(255, 120, 120, 200),
                    intensity: 0.12f * fade, saturation: 0.9f);
            }
        }
        return false;
    }
}

/// <summary>
/// 亵渎肢解刃 - 近战旗舰持握大剑。
/// 三连击（横斩→上挑→下劈）；挥砍波形 = 迟滞后拉 → poly(14) 斩击 → 过冲回摆；
/// 刀波用 ProfaneFleshPulse 蠕动绘制；刀身命中叠剖检印（下劈 ×2），叠满触发摘取处决。
/// </summary>
public class ProfaneDismemberer : ModItem
{
    private int _attackType;
    private int _lastUseTick;

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
        // 40 帧未续击则连击重置回横斩 (节奏留白)
        int tick = (int)Main.GameUpdateCount;
        if (tick - _lastUseTick > Item.useAnimation + 40)
            _attackType = 0;
        _lastUseTick = tick;

        Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI, _attackType);
        _attackType = (_attackType + 1) % 3;
        return false;
    }
}

/// <summary>
/// 亵渎肢解刃挥砍弹幕 - 持握旋转。
/// 三种攻击：0=横斩，1=上挑，2=下劈（血肉震荡 + 双倍印记）。
/// 波形：Prepare pow(6) 迟滞后拉 / Execute poly(14) 斩击 / Unwind BackOut 过冲回摆。
/// </summary>
public class ProfaneDismembererSwing : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/Profanes/ProfaneDismemberer";

    private const float SWING_RANGE = MathF.PI * 1.55f;
    private const float BACKSWING = 0.42f;      // 后拉附加角
    private const float PREP_FRAC = 0.25f;
    private const float EXEC_FRAC = 0.40f;

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
    private readonly Vector2[] _tipHistory = new Vector2[10]; // 刀尖历史点 (ribbon 拖尾, 纯视觉)
    private int _tipCount;
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

    private Vector2 BladeTip => Projectile.Center + Projectile.rotation.ToRotationVector2()
        * Projectile.Size.Length() * Projectile.scale * 0.72f;

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
            case Stage.Prepare: {
                // 迟滞后拉: 几乎不动 → 最后几帧猛然吸回 (反向运动预备)
                float t = MathHelper.Clamp(Timer / prepEnd, 0f, 1f);
                RawProgress = -BACKSWING * MathF.Pow(t, 6f);
                // 后拉尾段: 血液被刀身吸入 (蓄力收束语法)
                if (t > 0.5f && Timer % 2 == 0) {
                    Vector2 from = BladeTip + Main.rand.NextVector2CircularEdge(46f, 46f);
                    Dust d = Dust.NewDustPerfect(from, DustID.Blood, (BladeTip - from) * 0.14f, 0, default, 1.3f);
                    d.noGravity = true;
                }
                if (Timer >= prepEnd) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f + AttackType * 0.12f }, Owner.position);
                    ProfaneCommon.PlaySquelch(Owner.Center, 0.6f, -0.1f + AttackType * 0.1f);
                    CurrentStage = Stage.Execute;
                }
                break;
            }

            case Stage.Execute: {
                // poly(14) ease-out: 几乎全部角位移在前几帧 —— 斩击是"一记", 不是"一段"
                float t = MathHelper.Clamp(Timer / execDur, 0f, 1f);
                float snap = 1f - MathF.Pow(1f - t, 14f);
                RawProgress = MathHelper.Lerp(-BACKSWING, SWING_RANGE, snap);

                if (!_waveFired && t >= 0.10f) {
                    _waveFired = true;
                    Vector2 wd = Owner.DirectionTo(Main.MouseWorld);
                    int waveType = ModContent.ProjectileType<ProfaneFleshWave>();
                    float waveDmgMult = AttackType == 2 ? 1.5f : 1.0f;
                    Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem),
                        Owner.Center, wd * 20f, waveType,
                        (int)(Owner.HeldItem.damage * waveDmgMult),
                        Owner.HeldItem.knockBack * 0.6f, Owner.whoAmI, AttackType);

                    if (AttackType == 2) {
                        // 下劈血肉震荡: 落点血环 + 震屏
                        Vector2 quakePos = Owner.Center + wd * 90f;
                        ACMWeaponBurst.Spawn(Owner.GetSource_ItemUse(Owner.HeldItem), quakePos,
                            ACMWeaponBurst.Profane, scale: 1.8f, owner: Owner.whoAmI);
                        WeaponVFX.AddScreenShake(quakePos, 8f);
                    }
                }

                // 斩击期喷洒血液 (粒子随刀速走, 只在斩击帧密集)
                if (t < 0.5f) {
                    for (int i = 0; i < 2; i++) {
                        Dust d = Dust.NewDustPerfect(BladeTip + Main.rand.NextVector2Circular(10, 10),
                            DustID.Blood, Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.6f);
                        d.noGravity = true;
                    }
                }

                if (Timer >= execDur) CurrentStage = Stage.Unwind;
                break;
            }

            case Stage.Unwind: {
                // BackOut 微过冲回摆: 斩完刀有"余劲"
                float t = MathHelper.Clamp(Timer / unwindDur, 0f, 1f);
                RawProgress = SWING_RANGE * (1f + 0.05f * (1f - ACMUtils.BackOut(t)));
                if (Timer >= unwindDur) Projectile.Kill();
                break;
            }
        }

        Projectile.rotation = InitAngle + dir * RawProgress;
        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
            Projectile.rotation - MathHelper.PiOver2);
        Vector2 arm = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full,
            Projectile.rotation - MathHelper.PiOver2);
        arm.Y += Owner.gfxOffY;
        Projectile.Center = arm;
        // 刀身心跳呼吸 (系列签名, ±2%)
        Projectile.scale = 1.35f * Owner.GetAdjustedItemScale(Owner.HeldItem)
            * (1f + ProfaneCommon.Heartbeat() * 0.02f);
        Owner.heldProj = Projectile.whoAmI;

        // 刀尖历史点 (仅斩击期记录, 供 ribbon 拖尾)
        if (CurrentStage == Stage.Execute) {
            for (int i = _tipHistory.Length - 1; i > 0; i--)
                _tipHistory[i] = _tipHistory[i - 1];
            _tipHistory[0] = BladeTip;
            if (_tipCount < _tipHistory.Length) _tipCount++;
        }
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

        // 命中反馈栈: burst + 微震 + 湿裂音 (音高随连击上行) + 玩家后坐
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.Profane, scale: 0.8f, owner: Projectile.owner);
        WeaponVFX.AddScreenShake(target.Center, 1.8f);
        ProfaneCommon.PlaySquelch(target.Center, 0.9f, AttackType * 0.15f);
        if (Projectile.owner == Main.myPlayer) {
            Owner.velocity -= Owner.DirectionTo(target.Center) * 1.5f;
        }

        // 剖检印: 刀身 +1, 下劈 +2; 摘取伤害 = 面板 ×2
        ProfaneCommon.AddMark(target, Projectile, AttackType == 2 ? 2 : 1, Projectile.damage * 2);

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

        // 斩击期: 刀尖 ribbon 弧光 (外暗内亮, 只在 strike act 出现 —— 速度门控)
        if (CurrentStage == Stage.Execute && _tipCount >= 2) {
            var pts = new Vector2[_tipCount];
            Array.Copy(_tipHistory, pts, _tipCount);
            WeaponVFX.DrawRibbonTrail(pts, baseWidth: 30f * Projectile.scale,
                outerColor: ProfaneCommon.FleshDark, innerColor: ProfaneCommon.BloodBright,
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        if (CurrentStage == Stage.Execute) {
            float pulse = 0.75f + 0.25f * ProfaneCommon.Heartbeat();
            Texture2D sg = ACMAsset.SoftGlow;
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(150, 12, 20) * 0.50f * pulse, Projectile.rotation + rotOff,
                sg.Size() * 0.5f,
                Projectile.scale * 2.0f, SpriteEffects.None, 0);

            Texture2D sparkle = ACMAsset.Sparkle;
            sb.Draw(sparkle, BladeTip - Main.screenPosition, null,
                ProfaneCommon.FleshMid * 0.5f,
                (float)Main.timeForVisualEffects * 0.07f,
                sparkle.Size() * 0.5f,
                Projectile.scale * 0.55f, SpriteEffects.None, 0);
        }
        else if (CurrentStage == Stage.Prepare) {
            // 后拉期刀刃泛起暗红预兆光 (张力可读)
            float t = MathHelper.Clamp(Timer / (Owner.itemAnimationMax * PREP_FRAC), 0f, 1f);
            Texture2D sg = ACMAsset.SoftGlow;
            sb.Draw(sg, BladeTip - Main.screenPosition, null,
                ProfaneCommon.FleshMid * (0.45f * MathF.Pow(t, 3f)), 0f,
                sg.Size() * 0.5f,
                Projectile.scale * 0.8f, SpriteEffects.None, 0);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D tex = TextureAssets.Projectile[Type].Value; // 静态资产缓存 (修复每帧 Request)
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
/// 血肉刀波 - ProfaneFleshPulse 方向模式绘制的蠕动血肉能量波。
/// ai[0]存储攻击类型：2=下劈（更大、更强）。
/// </summary>
public class ProfaneFleshWave : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/GlaciateWave";

    private int AttackType => (int)Projectile.ai[0];
    private float Seed => (Projectile.whoAmI * 0.211f) % 1f;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 0;
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
        ProfaneCommon.PlaySquelch(target.Center, 0.7f, -0.15f);

        for (int i = 0; i < 15; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                Main.rand.NextVector2Circular(8f, 8f), 0, default, 2.5f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        float life = 1f - Projectile.timeLeft / 50f;
        float sizeMult = AttackType == 2 ? 1.4f : 1.0f;
        float alpha = ACMUtils.QuadOut(1f - life) * 0.95f;

        // 统一双层暗红血肉拖尾 (外宽暗 + 内窄亮)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 22f * sizeMult,
            outerColor: ProfaneCommon.FleshDark * alpha,
            innerColor: ProfaneCommon.FleshMid * alpha,
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);

        // 蠕动血肉波本体 (方向模式: 肌纤维沿行进方向 + 心跳收缩)
        Vector2 size = new Vector2(
            MathHelper.Lerp(230f, 130f, ACMUtils.QuadIn(life)),
            MathHelper.Lerp(96f, 40f, ACMUtils.QuadIn(life))) * sizeMult;
        ProfaneCommon.DrawFleshWave(Projectile.Center, Projectile.rotation, size,
            alpha, ProfaneCommon.Heartbeat(Seed), Seed, veinBoost: AttackType == 2 ? 0.5f : 0f);

        // 波前缘亮核
        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D sg = ACMAsset.SoftGlow;
        Vector2 front = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 50f;
        sb.Draw(sg, front - Main.screenPosition, null,
            ProfaneCommon.BloodBright * (alpha * 0.55f), 0f,
            sg.Size() * 0.5f,
            0.55f * sizeMult, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
