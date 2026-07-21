using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.ArrogantDivineSylvans;

// ============================================================
//  傲世神木系列共享地基 (仅本武器线消费):
//  - ArrogantSylvanPalette   金边翠芯配色常量 (全系列统一轨迹语言)
//  - ArrogantSylvanFX        GrowthRing/Vortex 专属着色器门面 + 演出节流
//  - ArrogantSylvanTargeting 节流索敌 (替换逐弹幕每帧全 NPC 扫描)
//  - ArrogantSylvanBrandNPC  「年轮烙印」层数 (系列共享机制)
//  - ArrogantSylvanBloom     烙印引爆入口 (owner 端, 伤害经同步弹幕承载)
//  - ArrogantSylvanBloomBurst 绽放伤害弹幕 (金翠年轮环)
//  - ArrogantSylvanScreenTint 短暂金翠染屏 (走全屏名额契约)
// ============================================================

/// <summary>系列统一配色: 金(神威)永远在外沿, 翠(生命)永远在内芯。</summary>
public static class ArrogantSylvanPalette
{
    public static readonly Color GoldDark = new(200, 150, 40);
    public static readonly Color GoldBright = new(255, 235, 150);
    public static readonly Color JadeDeep = new(30, 130, 60);
    public static readonly Color JadeBright = new(185, 255, 150);
    public static readonly Color WhiteHot = new(255, 255, 230);
    /// <summary>ribbon 拖尾外层 (宽暗金, a=权重)。</summary>
    public static readonly Color TrailOuter = new(200, 150, 40, 150);
    /// <summary>ribbon 拖尾内层 (窄亮翠, a=权重)。</summary>
    public static readonly Color TrailInner = new(185, 255, 150, 200);
}

/// <summary>
/// 系列专属着色器门面 + 共用演出小件。着色器经 <see cref="WeaponVFX.GetEffect"/> 按名缓存,
/// 均为屏幕空间 decal (只点亮环带内像素, 不读 screenTarget, 不占全屏后处理名额)。
/// </summary>
public static class ArrogantSylvanFX
{
    /// <summary>
    /// 年轮绽放环 (ArrogantSylvanGrowthRing.fx): 同心年轮 + 藤脉辐条 + 生长前沿白热。
    /// 须在有活动批阶段调用 (PreDraw 等)。
    /// </summary>
    /// <param name="worldCenter">环心世界坐标。</param>
    /// <param name="worldRadius">最大半径 (像素)。</param>
    /// <param name="progress">生长进度 0~1 (前沿位置)。</param>
    /// <param name="intensity">强度 0~1。</param>
    /// <param name="ringFreq">年轮密度。</param>
    public static void DrawGrowthRing(Vector2 worldCenter, float worldRadius, float progress, float intensity,
        float ringFreq = 9f) {
        if (Main.dedServ || intensity <= 0.01f || worldRadius < 4f)
            return;
        Effect fx = WeaponVFX.GetEffect("ArrogantSylvanGrowthRing");
        if (fx == null)
            return;

        ACMShaders.WorldDecalParams(worldCenter, worldRadius, out Vector2 uv, out float radiusFrac, out float aspect);
        fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
        fx.Parameters["uCenter"]?.SetValue(uv);
        fx.Parameters["uRadius"]?.SetValue(radiusFrac);
        fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
        fx.Parameters["uAspect"]?.SetValue(aspect);
        fx.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(progress, 0f, 1f));
        fx.Parameters["uColorGold"]?.SetValue(ArrogantSylvanPalette.GoldBright.ToVector4());
        fx.Parameters["uColorJade"]?.SetValue(ArrogantSylvanPalette.JadeBright.ToVector4());
        fx.Parameters["uRingFreq"]?.SetValue(ringFreq);

        ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
    }

    /// <summary>
    /// 神木风暴环带 (ArrogantSylvanVortex.fx): 极坐标旋转藤叶流, 环带即伤害判定的可视化。
    /// 须在有活动批阶段调用。
    /// </summary>
    /// <param name="worldCenter">环带中心。</param>
    /// <param name="bandRadius">环带中心半径 (像素)。</param>
    /// <param name="bandHalfWidth">环带半宽 (像素)。</param>
    /// <param name="spin">旋转速度 (视觉)。</param>
    /// <param name="pulse">脉冲增亮 0~1。</param>
    /// <param name="intensity">强度 0~1。</param>
    public static void DrawVortexBand(Vector2 worldCenter, float bandRadius, float bandHalfWidth,
        float spin, float pulse, float intensity) {
        if (Main.dedServ || intensity <= 0.01f || bandRadius < 8f)
            return;
        Effect fx = WeaponVFX.GetEffect("ArrogantSylvanVortex");
        if (fx == null)
            return;

        ACMShaders.WorldDecalParams(worldCenter, bandRadius, out Vector2 uv, out float radiusFrac, out float aspect);
        float halfFrac = radiusFrac * bandHalfWidth / MathF.Max(bandRadius, 1f);
        fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
        fx.Parameters["uCenter"]?.SetValue(uv);
        fx.Parameters["uRadius"]?.SetValue(radiusFrac);
        fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
        fx.Parameters["uAspect"]?.SetValue(aspect);
        fx.Parameters["uBandHalf"]?.SetValue(halfFrac);
        fx.Parameters["uSpin"]?.SetValue(spin);
        fx.Parameters["uPulse"]?.SetValue(MathHelper.Clamp(pulse, 0f, 1f));
        fx.Parameters["uColorGold"]?.SetValue(ArrogantSylvanPalette.GoldBright.ToVector4());
        fx.Parameters["uColorJade"]?.SetValue(ArrogantSylvanPalette.JadeBright.ToVector4());

        ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
    }

    // —— 命中演出节流: 高射速武器 (火铳/山海典) 同帧海量命中时避免每中一发都 spawn Burst ——
    private static uint _lastBurstFrame;

    /// <summary>节流版 <see cref="ACMWeaponBurst.Spawn"/>: 距上次生成不足 minGap 帧时静默跳过 (纯视觉)。</summary>
    public static void HitBurstThrottled(IEntitySource source, Vector2 worldPos, float scale, int owner,
        uint minGap = 3) {
        if (Main.dedServ || Main.myPlayer != owner)
            return;
        if (Main.GameUpdateCount - _lastBurstFrame < minGap)
            return;
        _lastBurstFrame = Main.GameUpdateCount;
        ACMWeaponBurst.Spawn(source, worldPos, ACMWeaponBurst.ArrogantSylvan, scale, owner);
    }

    /// <summary>绽放分层音: 低频闷响 + 高频叶簌, 音高随层数抬升并加随机 (§3.3 音效分层)。</summary>
    public static void PlayBloomSound(Vector2 pos, int stacks) {
        float t = stacks / (float)ArrogantSylvanBrandNPC.MaxStacks;
        SoundEngine.PlaySound(SoundID.Item14 with {
            Volume = 0.45f + 0.35f * t,
            Pitch = -0.35f + 0.15f * t + Main.rand.NextFloat(-0.08f, 0.08f)
        }, pos);
        SoundEngine.PlaySound(SoundID.Grass with {
            Volume = 0.85f,
            Pitch = 0.3f + 0.15f * t + Main.rand.NextFloat(-0.1f, 0.1f)
        }, pos);
    }
}

/// <summary>
/// 节流索敌: 目标索引缓存在调用方弹幕自己的 ai/localAI 槽位, 每 6 帧才全表重扫一次;
/// 目标失效立即重扫。替换系列内 7 处"每帧全 NPC 最近敌扫描"。
/// </summary>
public static class ArrogantSylvanTargeting
{
    private const int RescanInterval = 6;

    /// <summary>
    /// 取当前追踪目标 (无则返回 null)。cachedIdx 存"npc 索引+1" (0=无), rescanTimer 为重扫倒计时,
    /// 两者由调用方持久化 (localAI 槽等, 皆为本地模拟数据, 无需同步)。
    /// </summary>
    public static NPC UpdateTarget(Projectile proj, ref float cachedIdx, ref float rescanTimer, float range) {
        rescanTimer--;
        int idx = (int)cachedIdx - 1;
        if (idx >= 0 && idx < Main.maxNPCs) {
            NPC cur = Main.npc[idx];
            // 已锁目标给 1.5x 保持范围, 避免边界抖动丢锁
            if (cur.active && cur.CanBeChasedBy()
                && Vector2.DistanceSquared(proj.Center, cur.Center) < range * range * 2.25f && rescanTimer > 0f) {
                return cur;
            }
        }

        rescanTimer = RescanInterval;
        float best = range * range;
        int bestIdx = -1;
        for (int i = 0; i < Main.maxNPCs; i++) {
            NPC npc = Main.npc[i];
            if (!npc.active || !npc.CanBeChasedBy())
                continue;
            float d = Vector2.DistanceSquared(proj.Center, npc.Center);
            if (d < best) { best = d; bestIdx = i; }
        }
        cachedIdx = bestIdx + 1;
        return bestIdx >= 0 ? Main.npc[bestIdx] : null;
    }

    /// <summary>标准追踪转向: 朝目标以 lerp 力度收敛到 speed。目标为 null 时不动作。</summary>
    public static void SteerTowards(Projectile proj, NPC target, float speed, float lerp) {
        if (target == null)
            return;
        Vector2 dir = proj.SafeDirectionTo(target.Center);
        proj.velocity = Vector2.Lerp(proj.velocity, dir * speed, lerp);
    }
}

/// <summary>
/// 「年轮烙印」— 系列共享机制: 系列武器命中在敌人体表刻下年轮 (0~5 层, 8 秒衰减),
/// 各件武器的"引爆动作"读层数触发 <see cref="ArrogantSylvanBloom"/> 绽放。
/// 层数为**各端本地缓存** (命中判定跑在 owner 端, 绽放伤害由 owner 端生成的同步弹幕承载, 多人安全)。
/// </summary>
public class ArrogantSylvanBrandNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;

    public const int MaxStacks = 5;
    private const int DecayFrames = 480;

    public int Stacks { get; private set; }
    private int _decay;
    private float _flash;   // 叠层/绽放白闪包络 (纯视觉)

    /// <summary>命中叠层 (任意端可调; 只影响本端缓存与视觉)。</summary>
    public static void AddStack(NPC npc, int amount = 1) {
        if (npc == null || !npc.active || npc.friendly || npc.dontTakeDamage)
            return;
        var brand = npc.GetGlobalNPC<ArrogantSylvanBrandNPC>();
        brand.Stacks = Math.Min(brand.Stacks + amount, MaxStacks);
        brand._decay = DecayFrames;
        brand._flash = MathF.Max(brand._flash, 0.55f);
    }

    /// <summary>读出并清零层数 (引爆时)。</summary>
    public static int ConsumeStacks(NPC npc) {
        if (npc == null || !npc.active)
            return 0;
        var brand = npc.GetGlobalNPC<ArrogantSylvanBrandNPC>();
        int s = brand.Stacks;
        brand.Stacks = 0;
        if (s > 0)
            brand._flash = 1f;
        return s;
    }

    public override void PostAI(NPC npc) {
        if (_flash > 0.01f)
            _flash *= 0.90f;
        if (Stacks <= 0)
            return;
        if (--_decay <= 0)
            Stacks = 0;
    }

    public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
        if (Main.dedServ || npc.IsABestiaryIconDummy)
            return;
        if (Stacks <= 0 && _flash < 0.05f)
            return;

        Texture2D spark = ACMAsset.Sparkle;
        Texture2D wave = ACMAsset.GlaciateWave;
        if (spark == null || wave == null)
            return;

        float ringR = MathF.Max(npc.width, npc.height) * 0.5f + 12f;
        float t = (float)Main.timeForVisualEffects * 0.04f;
        float pulse = 0.75f + 0.25f * MathF.Sin((float)Main.timeForVisualEffects * 0.18f);

        // 年轮刻度点: 逐层围绕敌人生长 (A=0 → 项目默认预乘批下呈加法辉光)
        for (int i = 0; i < Stacks; i++) {
            float ang = -MathHelper.PiOver2 + MathHelper.TwoPi * i / MaxStacks + t;
            Vector2 pos = npc.Center + ang.ToRotationVector2() * ringR - screenPos;
            Color c = Stacks >= MaxStacks ? ArrogantSylvanPalette.WhiteHot : ArrogantSylvanPalette.GoldBright;
            c *= 0.65f * pulse;
            c.A = 0;
            spriteBatch.Draw(spark, pos, null, c, ang, spark.Size() * 0.5f, 0.14f, SpriteEffects.None, 0f);
        }

        // 满层白热提示环 + 叠层白闪
        float ringA = (Stacks >= MaxStacks ? 0.35f * pulse : 0f) + _flash * 0.5f;
        if (ringA > 0.03f) {
            Color rc = ArrogantSylvanPalette.JadeBright * ringA;
            rc.A = 0;
            spriteBatch.Draw(wave, npc.Center - screenPos, null, rc, t * 2f,
                wave.Size() * 0.5f, ringR / (wave.Width * 0.38f), SpriteEffects.None, 0f);
        }
    }
}

/// <summary>
/// 烙印引爆入口 (owner 端调用)。绽放伤害 = 引爆基准 × 0.32 × 层数, 由
/// <see cref="ArrogantSylvanBloomBurst"/> (同步弹幕) 承载; ≥5 层追加金翠染屏定调 + 震屏。
/// </summary>
public static class ArrogantSylvanBloom
{
    /// <summary>每层绽放伤害系数 (相对引爆动作的基准伤害)。</summary>
    public const float DamagePerStack = 0.32f;

    /// <summary>引爆单个目标。返回消耗的层数 (0=无烙印)。仅 owner 端生效。</summary>
    public static int Detonate(IEntitySource source, NPC target, int baseDamage, float knockback, int owner,
        bool petals = false) {
        if (Main.dedServ || Main.myPlayer != owner || target == null || !target.active)
            return 0;
        int stacks = ArrogantSylvanBrandNPC.ConsumeStacks(target);
        if (stacks <= 0)
            return 0;

        int dmg = Math.Max(1, (int)(baseDamage * DamagePerStack * stacks));
        Projectile.NewProjectile(source, target.Center, Vector2.Zero,
            ModContent.ProjectileType<ArrogantSylvanBloomBurst>(), dmg, knockback, owner,
            stacks, petals ? 1f : 0f);

        ArrogantSylvanFX.PlayBloomSound(target.Center, stacks);
        if (stacks >= ArrogantSylvanBrandNPC.MaxStacks) {
            ArrogantSylvanScreenTint.Spawn(source, target.Center, owner, tier: 1);
            WeaponVFX.AddScreenShake(target.Center, 6f);
        }
        else {
            WeaponVFX.AddScreenShake(target.Center, 1.5f + stacks * 0.5f);
        }
        return stacks;
    }

    /// <summary>引爆世界点半径内全部烙印。返回消耗的总层数。仅 owner 端生效。</summary>
    public static int DetonateArea(IEntitySource source, Vector2 center, float radius, int baseDamage,
        float knockback, int owner, bool petals = false) {
        if (Main.dedServ || Main.myPlayer != owner)
            return 0;
        int total = 0;
        float r2 = radius * radius;
        for (int i = 0; i < Main.maxNPCs; i++) {
            NPC npc = Main.npc[i];
            if (!npc.active || npc.friendly || npc.dontTakeDamage)
                continue;
            if (Vector2.DistanceSquared(npc.Center, center) > r2)
                continue;
            total += Detonate(source, npc, baseDamage, knockback, owner, petals);
        }
        return total;
    }
}

/// <summary>
/// 年轮绽放伤害弹幕: 自敌人体表扩张的金翠年轮环, 每目标单次判定。
/// ai[0]=层数 (决定半径/演出规模), ai[1]=1 时伴生 3 片环舞花瓣 (山海典风味)。
/// ≥3 层用 GrowthRing 专属着色器, 低层退化为廉价冲击环 (性能梯度)。
/// </summary>
public class ArrogantSylvanBloomBurst : ModProjectile
{
    public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private const int LifeTime = 36;
    private const int GrowFrames = 22;

    private int Stacks => Math.Max(1, (int)Projectile.ai[0]);
    private float MaxRadius => 90f + 34f * Stacks;
    private ref float Timer => ref Projectile.localAI[0];

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Generic; // 伤害在引爆时已按来源武器结算完毕, 不再吃职业加成
        Projectile.penetrate = -1;
        Projectile.timeLeft = LifeTime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = LifeTime + 4; // 每目标只结算一次
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Timer++;
        Lighting.AddLight(Projectile.Center, 0.5f, 1.1f, 0.4f);

        // 花瓣伴生 (山海典): owner 端第 2 帧环舞 3 片
        if (Timer == 2f && Projectile.ai[1] == 1f && Projectile.owner == Main.myPlayer) {
            for (int i = 0; i < 3; i++) {
                Vector2 vel = (MathHelper.TwoPi * i / 3f).ToRotationVector2() * 6.5f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                    ModContent.ProjectileType<ArrogantSylvanTomePetal>(),
                    Projectile.damage / 3, 1.5f, Projectile.owner);
            }
        }

        // 粒子 ∝ 层数, 设上限
        int dustCount = Math.Min(2 + Stacks, 6);
        float radius = CurrentRadius();
        for (int i = 0; i < dustCount; i++) {
            float ang = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + ang.ToRotationVector2() * (radius * Main.rand.NextFloat(0.75f, 1f));
            Dust d = Dust.NewDustPerfect(pos, i % 2 == 0 ? DustID.JungleTorch : DustID.GoldFlame,
                ang.ToRotationVector2() * 2.5f, 40, default, 1.8f);
            d.noGravity = true;
        }
    }

    private float CurrentRadius()
        => MaxRadius * ACMUtils.QuadOut(Math.Min(Timer / GrowFrames, 1f));

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        => VaultUtils.CircleIntersectsRectangle(Projectile.Center, CurrentRadius(), targetHitbox);

    public override bool PreDraw(ref Color lightColor) {
        float life = Timer / LifeTime;
        float bell = MathF.Sin(MathHelper.Clamp(life, 0f, 1f) * MathF.PI);
        float grow = ACMUtils.QuadOut(Math.Min(Timer / GrowFrames, 1f));

        // ≥3 层: 专属年轮环着色器; 低层: 廉价冲击环 (性能梯度)
        if (Stacks >= 3) {
            ArrogantSylvanFX.DrawGrowthRing(Projectile.Center, MaxRadius * 1.1f, grow,
                bell * (0.5f + 0.1f * Stacks), ringFreq: 7f + Stacks);
        }
        WeaponVFX.DrawShockwaveRing(Projectile.Center, CurrentRadius(), 8f + Stacks * 2f, bell * 0.85f,
            ArrogantSylvanPalette.JadeBright, ArrogantSylvanPalette.GoldDark);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D sg = ACMAsset.SoftGlow;

        // 6 道金翠交替花瓣光芒
        for (int k = 0; k < 6; k++) {
            float ang = MathHelper.TwoPi * k / 6f + Timer * 0.03f;
            Color c = k % 2 == 0 ? ArrogantSylvanPalette.GoldBright : ArrogantSylvanPalette.JadeBright;
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                c * (bell * 0.7f), ang, new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.13f, grow * MaxRadius / burst.Height * 0.9f), SpriteEffects.None, 0f);
        }

        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.JadeBright * (bell * 0.55f), 0f, sg.Size() * 0.5f,
            grow * MaxRadius / (sg.Width * 0.30f), SpriteEffects.None, 0f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.WhiteHot * (bell * (1f - life) * 0.8f), 0f, sg.Size() * 0.5f,
            grow * 1.2f, SpriteEffects.None, 0f);

        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);
        return false;
    }
}

/// <summary>
/// 傲世神木·触发短暂染屏 (纯视觉, 仅本武器线复用)。
///
/// 大招/触发瞬间释放一道金翠"定调"全屏调色 (<see cref="WeaponVFX.ApplyPaletteTint"/>, 内部走
/// <see cref="ACMShaders.RequestFullscreenSlot"/> 名额契约 (同屏 ≤1 自动仲裁) 并尊重
/// <see cref="MythologyConfig"/> 全屏开关, 强度自动 clamp ≤0.15)。
/// 触发点: 满层绽放 (tier 1) / 世界树之矢 / 连天铳每 25 发炮击 / 叶暴漩涡生成 (tier 0)。
/// damage=0, 不更新位置, owner 客户端生成 (染屏纯本地表现, 无需联机同步)。
/// </summary>
public class ArrogantSylvanScreenTint : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_1";

    private const int LifeTime = 26;
    private static uint _lastSpawnFrame; // 同帧多触发去重 (纯视觉)

    /// <summary>tier: 0=技触发 (0.10), 1=满层绽放 (0.135)。</summary>
    public static void Spawn(IEntitySource source, Vector2 worldPos, int owner, int tier = 0) {
        if (Main.dedServ || Main.myPlayer != owner)
            return;
        if (_lastSpawnFrame == Main.GameUpdateCount)
            return;
        _lastSpawnFrame = Main.GameUpdateCount;
        Projectile.NewProjectile(source, worldPos, Vector2.Zero,
            ModContent.ProjectileType<ArrogantSylvanScreenTint>(), 0, 0f, owner, tier);
    }

    public override void SetDefaults() {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.friendly = false;
        Projectile.hostile = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = LifeTime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.alpha = 255;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Projectile.velocity = Vector2.Zero;
    }

    public override bool PreDraw(ref Color lightColor) {
        if (Main.dedServ)
            return false;

        // 0→1→0 钟形包络, 短暂定调
        float life = 1f - Projectile.timeLeft / (float)LifeTime;
        float env = MathHelper.Clamp(MathF.Sin(life * MathF.PI), 0f, 1f);
        float peak = Projectile.ai[0] >= 1f ? 0.135f : 0.10f;
        float intensity = peak * env;

        // 金翠双色: 阴影压暗翠, 高光提亮金
        WeaponVFX.ApplyPaletteTint(Main.spriteBatch,
            shadowTint: new Color(28, 60, 30, 110),
            highlightTint: new Color(230, 235, 150, 95),
            intensity: intensity, saturation: 1.06f, hueShift: 0f);
        return false;
    }
}
