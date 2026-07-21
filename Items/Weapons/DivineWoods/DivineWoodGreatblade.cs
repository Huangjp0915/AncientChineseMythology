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

// ============================================================
//  神木系列共享地基 (本文件为系列"旗舰文件", 承载全系列复用的
//  生根→绽放 机制与年轮法阵/翠脉流光着色器接入; 其余六件只消费)
// ============================================================

/// <summary>神木系列统一配色 (与 ACMWeaponBurst.DivineWood 同源)。</summary>
internal static class DivineWoodPalette
{
    /// <summary>外层暗翠。</summary>
    public static readonly Color DeepGreen = new(20, 110, 55);
    /// <summary>主翠。</summary>
    public static readonly Color Emerald = new(60, 220, 90);
    /// <summary>亮芯。</summary>
    public static readonly Color BrightCore = new(180, 255, 165);
    /// <summary>年轮金绿 (法阵亮纹)。</summary>
    public static readonly Color RingGold = new(205, 240, 125);
}

/// <summary>
/// 神木系列专属着色器接入 (年轮法阵 / 翠脉流光) + 同帧绘制预算。
/// 着色器经 WeaponVFX.GetEffect 静态缓存; 缺失时自动退化为共享原语, 保证总有反馈。
/// </summary>
internal static class DivineWoodFX
{
    private static ulong _ringFrame;
    private static int _ringCount;

    /// <summary>年轮法阵 decal 同帧 ≤2 (超出退化为冲击环)。</summary>
    public static bool RequestRingDecal() {
        if (_ringFrame != Main.GameUpdateCount) {
            _ringFrame = Main.GameUpdateCount;
            _ringCount = 0;
        }
        if (_ringCount >= 2)
            return false;
        _ringCount++;
        return true;
    }

    /// <summary>
    /// 在世界点绘制年轮法阵 (DivineWoodGrowthRing.fx, 屏幕空间 decal)。
    /// 须在有活动批的阶段调用 (PreDraw); 预算满/着色器缺失时退化为双色冲击环。
    /// </summary>
    /// <param name="grow">生长进度 0~1 (法阵自内向外长出)。</param>
    public static void DrawGrowthRing(Vector2 worldCenter, float worldRadius, float grow, float intensity, float spin = 0f) {
        if (Main.dedServ || intensity <= 0.02f || grow <= 0.01f)
            return;
        Effect fx = WeaponVFX.GetEffect("DivineWoodGrowthRing");
        if (fx == null || !RequestRingDecal()) {
            WeaponVFX.DrawShockwaveRing(worldCenter, worldRadius * grow, 10f, intensity * 0.7f,
                DivineWoodPalette.BrightCore, DivineWoodPalette.DeepGreen);
            return;
        }
        ACMShaders.WorldDecalParams(worldCenter, worldRadius, out Vector2 uvCenter, out float radiusFrac, out float aspect);
        fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
        fx.Parameters["uCenter"]?.SetValue(uvCenter);
        fx.Parameters["uRadius"]?.SetValue(radiusFrac);
        fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
        fx.Parameters["uAspect"]?.SetValue(aspect);
        fx.Parameters["uColorPrimary"]?.SetValue(DivineWoodPalette.DeepGreen.ToVector4());
        fx.Parameters["uColorSecondary"]?.SetValue(DivineWoodPalette.RingGold.ToVector4());
        fx.Parameters["uGrow"]?.SetValue(MathHelper.Clamp(grow, 0f, 1f));
        fx.Parameters["uSpin"]?.SetValue(spin);
        ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.AlphaBlend);
    }

    /// <summary>
    /// 用 DivineWoodSapFlow.fx (翠脉流光) 绘制一张武器贴图: 树液光脉沿贴图流动 + 轮廓翠光。
    /// 须在有活动批的阶段调用; 着色器缺失时退化为普通绘制 (当前批内)。
    /// </summary>
    public static void DrawSapFlowSprite(Texture2D tex, Vector2 worldPos, Rectangle? src, Color color,
        float rotation, Vector2 origin, float scale, SpriteEffects effects, float intensity) {
        if (Main.dedServ || tex == null)
            return;
        SpriteBatch sb = Main.spriteBatch;
        Effect fx = WeaponVFX.GetEffect("DivineWoodSapFlow");
        Texture2D noise = ACMShaders.NoiseTexture;
        if (fx == null || noise == null || intensity <= 0.02f) {
            sb.Draw(tex, worldPos - Main.screenPosition, src, color, rotation, origin, scale, effects, 0f);
            return;
        }

        fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
        fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
        fx.Parameters["uVeinColor"]?.SetValue(new Color(120, 255, 150, 200).ToVector4());
        fx.Parameters["uRimColor"]?.SetValue(new Color(185, 255, 170, 220).ToVector4());
        fx.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
        fx.Parameters["uFlowSpeed"]?.SetValue(0.9f);
        fx.Parameters["uNoiseScale"]?.SetValue(2.4f);

        GraphicsDevice gd = Main.graphics.GraphicsDevice;
        sb.End();
        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
        gd.Textures[1] = noise;
        gd.SamplerStates[1] = SamplerState.LinearWrap;
        sb.Draw(tex, worldPos - Main.screenPosition, src, color, rotation, origin, scale, effects, 0f);
        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);
    }
}

/// <summary>
/// 系列机制核心 API — 生根 (Rooted) → 绽放 (Bloom)。
/// 播种型攻击命中挂层 (≤5), 收割型攻击引爆全部层数生成年轮绽放 AoE。
/// 层数在命中方客户端本地记账 (命中与引爆都发生在 owner 端, 绽放伤害经同步弹幕输出, MP 安全);
/// 持续伤害走 vanilla 同步的 Buff (各端一致)。
/// </summary>
public static class DivineWoodRoot
{
    public const int MaxStacks = 5;
    public const int RootDuration = 240;

    private static ulong _bloomFrame;
    private static int _bloomCount;

    /// <summary>给敌人挂生根层 (播种)。请只在命中事件 (owner 端) 调用。</summary>
    public static void AddStack(NPC npc, int stacks) {
        if (npc == null || !npc.active || npc.friendly || npc.dontTakeDamage)
            return;
        npc.AddBuff(ModContent.BuffType<DivineWoodRootedBuff>(), RootDuration);
        var rooted = npc.GetGlobalNPC<DivineWoodRootedNPC>();
        rooted.Stacks = Math.Min(rooted.Stacks + stacks, MaxStacks);

        if (!Main.dedServ) {
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustDirect(new Vector2(npc.position.X, npc.Bottom.Y - 8f), npc.width, 8,
                    DustID.JungleGrass, 0f, -1.6f);
                d.noGravity = true;
                d.velocity *= 0.5f;
                d.scale = 1.1f;
            }
        }
    }

    /// <summary>
    /// 引爆目标身上的生根层 (收割): 消耗全部层数, 生成年轮绽放 AoE 弹幕。
    /// 伤害 = baseDamage × (0.35 + 0.18×层), 半径 = 110 + 26×层。
    /// 仅 owner 端生效; 同帧绽放 ≤3 (超出保留层数给下一次收割)。
    /// </summary>
    /// <returns>实际消耗的层数 (0 = 无层/预算满/非 owner)。</returns>
    public static int TriggerBloom(IEntitySource source, NPC npc, int baseDamage, float knockback, int owner) {
        if (npc == null || !npc.active || Main.myPlayer != owner)
            return 0;
        var rooted = npc.GetGlobalNPC<DivineWoodRootedNPC>();
        int stacks = npc.HasBuff(ModContent.BuffType<DivineWoodRootedBuff>()) ? rooted.Stacks : 0;
        if (stacks <= 0)
            return 0;

        if (_bloomFrame != Main.GameUpdateCount) {
            _bloomFrame = Main.GameUpdateCount;
            _bloomCount = 0;
        }
        if (_bloomCount >= 3)
            return 0;
        _bloomCount++;

        rooted.Stacks = 0;
        npc.RequestBuffRemoval(ModContent.BuffType<DivineWoodRootedBuff>());

        int dmg = (int)(baseDamage * (0.35f + 0.18f * stacks));
        Projectile.NewProjectile(source, npc.Center, Vector2.Zero,
            ModContent.ProjectileType<DivineWoodBloomBurst>(), dmg, knockback, owner, stacks);
        return stacks;
    }

    /// <summary>自世界点向下找最近地表 (瓦坐标网格, 上限 maxTiles 格)。找不到则返回下探极限点。</summary>
    public static Vector2 FindGroundBelow(Vector2 world, int maxTiles = 30) {
        int tx = (int)(world.X / 16f);
        int ty = (int)(world.Y / 16f);
        for (int i = 0; i < maxTiles; i++) {
            if (WorldGen.InWorld(tx, ty + i, 10) && WorldGen.SolidTile(tx, ty + i))
                return new Vector2(world.X, (ty + i) * 16f);
        }
        return new Vector2(world.X, world.Y + maxTiles * 16f);
    }
}

/// <summary>生根 debuff — 根须缠体持续伤害 (固定 24/s, 层数只影响绽放规模)。</summary>
public class DivineWoodRootedBuff : ModBuff
{
    public override string Texture => "Terraria/Images/Buff_20";

    public override void SetStaticDefaults() {
        Main.debuff[Type] = true;
        Main.buffNoSave[Type] = true;
    }
}

/// <summary>生根层数记账 + DoT + 根须缠体视觉。</summary>
public class DivineWoodRootedNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;

    /// <summary>生根层数 (本地记账, 0~5)。</summary>
    public int Stacks;

    public override void UpdateLifeRegen(NPC npc, ref int damage) {
        if (!npc.HasBuff(ModContent.BuffType<DivineWoodRootedBuff>()))
            return;
        if (npc.lifeRegen > 0)
            npc.lifeRegen = 0;
        npc.lifeRegen -= 48; // 24/s
        if (damage < 6)
            damage = 6;
    }

    public override void PostAI(NPC npc) {
        if (!npc.HasBuff(ModContent.BuffType<DivineWoodRootedBuff>())) {
            Stacks = 0;
            return;
        }
        // 根须提示粒子: 层数越高越密 (预算: 平均每 4~8 帧 1 dust)
        if (!Main.dedServ && Main.rand.NextBool(Math.Max(3, 9 - Stacks * 2))) {
            Dust d = Dust.NewDustDirect(new Vector2(npc.position.X, npc.Bottom.Y - 10f), npc.width, 10,
                DustID.JunglePlants, 0f, -1.2f);
            d.noGravity = true;
            d.scale = 0.9f + Stacks * 0.08f;
        }
    }

    public override void DrawEffects(NPC npc, ref Color drawColor) {
        if (npc.HasBuff(ModContent.BuffType<DivineWoodRootedBuff>()))
            drawColor = Color.Lerp(drawColor, new Color(120, 220, 130), 0.18f);
    }
}

/// <summary>
/// 年轮绽放 — 系列共享收割 AoE: 翠玉年轮法阵自敌脚下生长绽开, 环形伤害。
/// ai[0] = 消耗层数 (决定半径与演出规模)。伤害在生成时按引爆武器算好 (MP 安全)。
/// </summary>
public class DivineWoodBloomBurst : ModProjectile
{
    public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private const int LifeTime = 40;
    private const int GrowTime = 22;

    private int StacksConsumed => Math.Max(1, (int)Projectile.ai[0]);
    private float MaxRadius => 110f + 26f * StacksConsumed;
    private ref float Timer => ref Projectile.ai[1];

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Generic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = LifeTime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1; // 每目标只结算一次
    }

    public override bool ShouldUpdatePosition() => false;

    public override void OnSpawn(IEntitySource source) {
        float pitch = -0.15f + StacksConsumed * 0.08f;
        SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.85f, Pitch = pitch }, Projectile.Center);
        if (StacksConsumed >= 3)
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = 0.55f }, Projectile.Center);
        WeaponVFX.AddScreenShake(Projectile.Center, 1.5f + StacksConsumed * 0.5f);
    }

    public override void AI() {
        Timer++;
        float grow = ACMUtils.QuadOut(Math.Min(Timer / GrowTime, 1f));
        float radius = MaxRadius * grow;

        // 绽放花瓣粒子 (沿环沿, 数量∝层数)
        int count = 2 + StacksConsumed;
        for (int i = 0; i < count; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.7f, radius);
            Dust d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.JungleTorch : DustID.JunglePlants,
                angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(1f, 3f) + new Vector2(0, -1f),
                60, default, Main.rand.NextFloat(1.2f, 2f));
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.35f, 0.9f, 0.4f);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        if (Timer > GrowTime + 6)
            return false;
        float radius = MaxRadius * ACMUtils.QuadOut(Math.Min(Timer / GrowTime, 1f));
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
    }

    public override bool PreDraw(ref Color lightColor) {
        float life = 1f - Projectile.timeLeft / (float)LifeTime;
        float grow = ACMUtils.QuadOut(Math.Min(Timer / GrowTime, 1f));
        float fade = ACMUtils.QuadOut(1f - life);

        // 年轮法阵 (专属着色器, 预算内; 满则内部退化为冲击环)
        DivineWoodFX.DrawGrowthRing(Projectile.Center, MaxRadius * 1.08f, grow,
            fade * (0.55f + StacksConsumed * 0.08f), Projectile.whoAmI * 1.3f);

        // 外扩冲击环 + 中心柔光
        WeaponVFX.DrawShockwaveRing(Projectile.Center, MaxRadius * grow, 12f + StacksConsumed * 2f,
            fade * 0.8f, DivineWoodPalette.BrightCore, DivineWoodPalette.DeepGreen);
        WeaponVFX.DrawGlowBurst(Projectile.Center, (0.9f + StacksConsumed * 0.25f) * fade,
            DivineWoodPalette.Emerald * (fade * 0.8f));

        // 大绽放 (≥4层) 追加短促径向辉光 (名额满自动退化)
        if (StacksConsumed >= 4 && life < 0.4f)
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.09f, fade * 0.55f, DivineWoodPalette.Emerald, 6f);
        return false;
    }
}

/// <summary>
/// 根须尖刺 — 系列共享地面弹幕 (火铳迫击炮 / 种子弹扎根消费)。
/// 生成位置应为地表点 (DivineWoodRoot.FindGroundBelow)。
/// ai[0] = 出土前延迟帧; ai[1] = 模式 (0=播种挂1层, 1=收割引爆)。
/// </summary>
public class DivineWoodRootSpike : ModProjectile
{
    public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private const int TelegraphTime = 8;
    private const int RiseTime = 10;
    private const int StayTime = 12;
    private const int FadeTime = 10;
    private const float Height = 92f;
    private const float HalfWidth = 15f;

    private int Delay => (int)Projectile.ai[0];
    private bool BloomMode => Projectile.ai[1] >= 1f;
    private ref float Timer => ref Projectile.localAI[0];

    public override void SetDefaults() {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 300;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override void OnSpawn(IEntitySource source) {
        Projectile.timeLeft = Delay + TelegraphTime + RiseTime + StayTime + FadeTime;
    }

    private float CurrentHeight() {
        float t = Timer - Delay - TelegraphTime;
        if (t <= 0f)
            return 0f;
        float p = Math.Min(t / RiseTime, 1f);
        return Height * (1f - MathF.Pow(1f - p, 8f));
    }

    public override void AI() {
        Timer++;

        if (Timer < Delay)
            return;

        // 地裂 telegraph
        if (Timer < Delay + TelegraphTime) {
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), 0f),
                    DustID.Dirt, new Vector2(0, -Main.rand.NextFloat(1f, 2.5f)), 60, default, 1.2f);
                d.noGravity = false;
            }
            return;
        }

        // 出土瞬间
        if ((int)Timer == Delay + TelegraphTime) {
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.7f, Pitch = Main.rand.NextFloat(0.1f, 0.35f) }, Projectile.Center);
            WeaponVFX.AddScreenShake(Projectile.Center, BloomMode ? 2f : 1.2f);
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center,
                    Main.rand.NextBool() ? DustID.JungleTorch : DustID.Dirt,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(2f, 6f)), 40, default, 1.5f);
                d.noGravity = Main.rand.NextBool();
            }
        }

        Lighting.AddLight(Projectile.Center - new Vector2(0, CurrentHeight() * 0.5f), 0.15f, 0.5f, 0.2f);
    }

    public override bool? CanDamage() {
        float t = Timer - Delay - TelegraphTime;
        return t >= 0f && t <= RiseTime + StayTime ? null : false;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float h = CurrentHeight();
        if (h < 4f)
            return false;
        Rectangle rect = new((int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - h),
            (int)(HalfWidth * 2), (int)h);
        return rect.Intersects(targetHitbox);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        if (BloomMode) {
            DivineWoodRoot.TriggerBloom(Projectile.GetSource_OnHit(target), target,
                Projectile.damage, 4f, Projectile.owner);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.DivineWood, scale: 1.1f, owner: Projectile.owner);
        }
        else {
            DivineWoodRoot.AddStack(target, 1);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.DivineWood, scale: 0.8f, owner: Projectile.owner);
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        float h = CurrentHeight();
        if (h < 2f)
            return false;

        float fadeT = Timer - Delay - TelegraphTime - RiseTime - StayTime;
        float alpha = fadeT > 0f ? 1f - Math.Min(fadeT / FadeTime, 1f) : 1f;

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D lsh = ACMAsset.LightShot;
        Texture2D sg = ACMAsset.SoftGlow;
        Vector2 basePos = Projectile.Center - Main.screenPosition;
        Color body = (BloomMode ? DivineWoodPalette.Emerald : DivineWoodPalette.DeepGreen) * (alpha * 0.9f);
        Color core = DivineWoodPalette.BrightCore * (alpha * 0.85f);

        // 主刺 (SlashBurst 竖立, 底部中心为原点) + 两根伴刺
        float len = h / burst.Height;
        sb.Draw(burst, basePos, null, body, 0f,
            new Vector2(burst.Width * 0.5f, burst.Height), new Vector2(0.20f, len), SpriteEffects.None, 0f);
        sb.Draw(burst, basePos + new Vector2(-9f, 0f), null, body * 0.6f, -0.16f,
            new Vector2(burst.Width * 0.5f, burst.Height), new Vector2(0.12f, len * 0.62f), SpriteEffects.None, 0f);
        sb.Draw(burst, basePos + new Vector2(9f, 0f), null, body * 0.6f, 0.16f,
            new Vector2(burst.Width * 0.5f, burst.Height), new Vector2(0.12f, len * 0.62f), SpriteEffects.None, 0f);

        // 亮芯 + 尖端辉点
        sb.Draw(lsh, basePos - new Vector2(0f, h * 0.5f), null, core, MathHelper.PiOver2,
            lsh.Size() * 0.5f, new Vector2(h / lsh.Width, 0.06f), SpriteEffects.None, 0f);
        sb.Draw(sg, basePos - new Vector2(0f, h), null, core * 0.8f, 0f, sg.Size() * 0.5f,
            0.30f, SpriteEffects.None, 0f);

        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);
        return false;
    }
}

// ============================================================
//  神木巨刃 — 系列旗舰 (近战)
// ============================================================

/// <summary>
/// 神木巨刃 - 三连段挥砍大刀 (段1横斩/段2逆斩挂生根, 段3年轮重斩引爆生根并放刀波),
/// 右键大招"建木擎天": 蓄力年轮法阵 → 面前依次拔起三根建木巨柱。
/// 刀身常态运行翠脉流光专属着色器。
/// </summary>
public class DivineWoodGreatblade : ModItem
{
    private const uint UltCooldown = 540;  // 9s
    private const uint ComboResetGap = 130;

    private int _combo;
    private uint _lastSwingFrame;
    private uint _ultReadyFrame;

    public override void SetDefaults() {
        Item.damage = 190;
        Item.crit = 18;
        Item.DamageType = DamageClass.Melee;
        Item.width = 70;
        Item.height = 70;
        Item.useTime = 20;
        Item.useAnimation = 20;
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

    public override bool AltFunctionUse(Player player) => true;

    public override bool CanUseItem(Player player) {
        if (player.ownedProjectileCounts[ModContent.ProjectileType<DivineWoodPillarRite>()] > 0)
            return false;

        if (player.altFunctionUse == 2) {
            if (Main.GameUpdateCount < _ultReadyFrame)
                return false;
            Item.useTime = Item.useAnimation = 56;
            Item.shoot = ModContent.ProjectileType<DivineWoodPillarRite>();
        }
        else {
            if (Main.GameUpdateCount - _lastSwingFrame > ComboResetGap)
                _combo = 0;
            // 段3 年轮重斩用更长的挥舞周期承载 45% 前摇
            Item.useTime = Item.useAnimation = _combo == 2 ? 34 : 20;
            Item.shoot = ModContent.ProjectileType<DivineWoodGreatbladeSwing>();
        }
        return true;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        if (player.altFunctionUse == 2) {
            _ultReadyFrame = Main.GameUpdateCount + UltCooldown;
            Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI);
        }
        else {
            Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI, _combo);
            _combo = (_combo + 1) % 3;
            _lastSwingFrame = Main.GameUpdateCount;
        }
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
/// 神木巨刃挥砍弹幕 — 三连段:
/// ai[0]=段序 (0 横斩 / 1 逆斩 / 2 年轮重斩)。
/// 前摇反向拉刀 (pow3 后吸) → poly(9)/poly(20) 爆发 → 过冲回摆收招。
/// 段1/2 命中播种; 段3 ×1.6 引爆生根 + 释放加宽刀波。
/// </summary>
public class DivineWoodGreatbladeSwing : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/DivineWoods/DivineWoodGreatblade";

    private enum Stage { Prepare, Execute, Recover }

    private int Step => (int)Projectile.ai[0];
    private ref float Timer => ref Projectile.ai[1];
    private ref float InitAngle => ref Projectile.ai[2];
    private ref float RawProgress => ref Projectile.localAI[0];

    private Stage CurrentStage {
        get => (Stage)Projectile.localAI[1];
        set { Projectile.localAI[1] = (float)value; Timer = 0f; }
    }

    private bool IsHeavy => Step == 2;
    private float SwingRange => IsHeavy ? MathF.PI * 1.75f : MathF.PI * 1.35f;
    private float BackAngle => IsHeavy ? 0.9f : 0.35f;
    private float PrepFrac => IsHeavy ? 0.42f : 0.24f;
    private float ExecFrac => IsHeavy ? 0.16f : 0.30f;
    private int SwingDir => Step == 1 ? -1 : 1;

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
        int dir = Projectile.spriteDirection * SwingDir;

        if (dir > 0) {
            toMouse = MathHelper.Clamp(toMouse, -MathF.PI / 2.8f, MathF.PI / 5f);
            InitAngle = toMouse - SwingRange * 0.55f;
        }
        else {
            if (toMouse < 0)
                toMouse += MathHelper.TwoPi;
            toMouse = MathHelper.Clamp(toMouse, MathF.PI * 0.78f, MathF.PI * 1.4f);
            InitAngle = toMouse + SwingRange * 0.55f;
        }

        if (IsHeavy)
            SoundEngine.PlaySound(SoundID.Item7 with { Volume = 0.6f, Pitch = -0.35f }, Owner.Center); // 木质拉弓般的蓄势吱呀
    }

    public override void AI() {
        if (!Owner.active || Owner.dead) { Projectile.Kill(); return; }
        Owner.itemAnimation = 2;
        Owner.itemTime = 2;

        // 段时长按段序确定 (不读 itemAnimationMax — 远端客户端的物品副本不携带连段状态)
        float totalTime = IsHeavy ? 34f : 20f;
        float prepEnd = totalTime * PrepFrac;
        float execDur = totalTime * ExecFrac;
        float unwindDur = totalTime * (1f - PrepFrac - ExecFrac);
        int dir = Projectile.spriteDirection * SwingDir;

        switch (CurrentStage) {
            case Stage.Prepare:
                // 反向拉刀: pow3 后吸 (慢…慢…猛然吸入)
                float pt = MathHelper.Clamp(Timer / Math.Max(prepEnd, 1f), 0f, 1f);
                RawProgress = -BackAngle * pt * pt * pt;
                // 重斩末段微抖 (蓄满颤动)
                if (IsHeavy && pt > 0.8f)
                    RawProgress += Main.rand.NextFloat(-0.02f, 0.02f);
                if (Timer >= prepEnd) {
                    if (IsHeavy) {
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.95f, Pitch = -0.15f }, Owner.position);
                        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.8f, Pitch = -0.3f }, Owner.position);
                        WeaponVFX.AddScreenShake(Owner.Center, 3f);
                    }
                    else {
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = Step == 0 ? -0.05f : 0.12f }, Owner.position);
                        WeaponVFX.AddScreenShake(Owner.Center, 1.2f);
                    }
                    CurrentStage = Stage.Execute;
                }
                break;

            case Stage.Execute:
                float p = Math.Min(Timer / Math.Max(execDur, 1f), 1f);
                // 爆发: 高次多项式 ease-out — 几乎全部角程压进前几帧
                float ease = 1f - MathF.Pow(1f - p, IsHeavy ? 20f : 9f);
                RawProgress = MathHelper.Lerp(-BackAngle, SwingRange, ease);

                // 段3 在爆发瞬间放加宽刀波
                if (IsHeavy && !_waveFired && p >= 0.05f) {
                    _waveFired = true;
                    if (Main.myPlayer == Projectile.owner) {
                        Vector2 wd = Owner.SafeDirectionTo(Main.MouseWorld);
                        Projectile.NewProjectile(Owner.GetSource_ItemUse(Owner.HeldItem),
                            Owner.Center, wd * 20f,
                            ModContent.ProjectileType<DivineWoodVineWave>(),
                            (int)(Projectile.damage * 1.5f),
                            Projectile.knockBack * 0.6f, Owner.whoAmI);
                    }
                    SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.9f, Pitch = 0.3f }, Owner.position);
                }

                if (Timer >= execDur)
                    CurrentStage = Stage.Recover;
                break;

            case Stage.Recover:
                float r = Math.Min(Timer / Math.Max(unwindDur, 1f), 1f);
                // 过冲回摆: 先冲过头 0.10 rad 再settle
                RawProgress = SwingRange + 0.10f * MathF.Sin(r * MathF.PI);
                if (Timer >= unwindDur)
                    Projectile.Kill();
                break;
        }

        Projectile.rotation = InitAngle + dir * RawProgress;
        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
            Projectile.rotation - MathHelper.PiOver2);
        Vector2 arm = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full,
            Projectile.rotation - MathHelper.PiOver2);
        arm.Y += Owner.gfxOffY;
        Projectile.Center = arm;
        Projectile.scale = (IsHeavy ? 1.5f : 1.3f) * Owner.GetAdjustedItemScale(Owner.HeldItem);
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
        if (IsHeavy)
            modifiers.SourceDamage *= 1.6f;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        for (int i = 0; i < (IsHeavy ? 16 : 10); i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(6f, 6f), 60, default, IsHeavy ? 2.2f : 1.8f);
            d.noGravity = true;
        }

        if (IsHeavy) {
            int consumed = DivineWoodRoot.TriggerBloom(Projectile.GetSource_OnHit(target), target,
                Projectile.damage, 6f, Projectile.owner);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.DivineWood, scale: 1.3f + consumed * 0.06f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, consumed > 0 ? 4f : 2.5f);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.5f }, target.Center);
        }
        else {
            DivineWoodRoot.AddStack(target, 1);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.DivineWood, scale: 1f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 1.5f);
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        int dir = Projectile.spriteDirection * SwingDir;
        float rotOff = dir > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi;

        // 挥砍弧光 — 双层 ribbon (沿刀尖扫过的弧线); 仅爆发段, 重斩加宽
        if (CurrentStage == Stage.Execute) {
            float tipLen = Projectile.Size.Length() * Projectile.scale * 0.55f;
            var arc = new List<Vector2>();
            for (int i = 0; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                arc.Add(Projectile.Center + Projectile.oldRot[i].ToRotationVector2() * tipLen);
            }
            if (arc.Count >= 2)
                WeaponVFX.DrawRibbonTrail(arc.ToArray(), baseWidth: IsHeavy ? 34f : 26f,
                    outerColor: new Color(20, 110, 55, IsHeavy ? 180 : 150),
                    innerColor: new Color(170, 255, 150, IsHeavy ? 230 : 200),
                    tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);
        }

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        if (CurrentStage == Stage.Execute) {
            float pulse = 0.8f + 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 0.22f);
            Texture2D sg = ACMAsset.SoftGlow;
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                DivineWoodPalette.Emerald * (IsHeavy ? 0.7f : 0.55f) * pulse, Projectile.rotation + rotOff,
                sg.Size() * 0.5f,
                Projectile.scale * (IsHeavy ? 2.4f : 2.0f), SpriteEffects.None, 0);

            Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2()
                          * Projectile.Size.Length() * Projectile.scale * 0.6f;
            Texture2D sparkle = ACMAsset.Sparkle;
            sb.Draw(sparkle, tip - Main.screenPosition, null,
                new Color(120, 255, 150) * 0.55f,
                (float)Main.timeForVisualEffects * 0.06f,
                sparkle.Size() * 0.5f,
                Projectile.scale * (IsHeavy ? 0.75f : 0.60f), SpriteEffects.None, 0);
        }
        // 重斩前摇: 刀根处能量收拢微光 (读起来 = 正在蓄)
        else if (IsHeavy && CurrentStage == Stage.Prepare) {
            Texture2D sg = ACMAsset.SoftGlow;
            float prepT = MathHelper.Clamp(Timer / (34f * PrepFrac), 0f, 1f);
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                DivineWoodPalette.BrightCore * (0.35f * prepT * prepT), 0f,
                sg.Size() * 0.5f, 0.5f + prepT * 0.5f, SpriteEffects.None, 0);
        }

        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);

        // 刀身 — 翠脉流光专属着色器 (常态低强度, 爆发段增亮)
        Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
        SpriteEffects fxFlip = dir < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Vector2 origin = dir > 0
            ? new Vector2(0, tex.Height) : new Vector2(tex.Width, tex.Height);
        float sap = CurrentStage switch {
            Stage.Execute => 0.9f,
            Stage.Prepare => IsHeavy ? 0.65f : 0.45f,
            _ => 0.35f,
        };
        DivineWoodFX.DrawSapFlowSprite(tex, Projectile.Center, null, lightColor,
            Projectile.rotation + rotOff, origin, Projectile.scale, fxFlip, sap);
        return false;
    }
}

/// <summary>
/// 自然刀波 - 段3 年轮重斩释放的翠绿弧形能量波 (命中播种 1 层并短暂迟滞)。
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
        DivineWoodRoot.AddStack(target, 1);
        target.velocity *= 0.7f;

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

/// <summary>
/// 建木擎天·仪式 — 右键大招手持弹幕:
/// 46 帧蓄力 (地面年轮法阵生长 + 汇聚粒子, 72% 后寂静一拍) → 释放三根建木巨柱 + 12 帧翠色染屏。
/// </summary>
public class DivineWoodPillarRite : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/DivineWoods/DivineWoodGreatblade";

    private const int ChargeTime = 46;
    private const int AfterTime = 16;

    private ref float Timer => ref Projectile.ai[0];
    private float Charge => MathHelper.Clamp(Timer / ChargeTime, 0f, 1f);
    private Player Owner => Main.player[Projectile.owner];

    public override void SetDefaults() {
        Projectile.width = 20;
        Projectile.height = 20;
        Projectile.friendly = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = ChargeTime + AfterTime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void OnSpawn(IEntitySource source) {
        Projectile.spriteDirection = Projectile.velocity.X >= 0f ? 1 : -1;
        SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.8f, Pitch = -0.2f }, Owner.Center);
    }

    public override void AI() {
        if (!Owner.active || Owner.dead) { Projectile.Kill(); return; }

        // 蓄力+释放头几帧锁定玩家动作
        if (Timer < ChargeTime + 4) {
            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
        }
        Owner.heldProj = Projectile.whoAmI;
        Owner.ChangeDir(Projectile.spriteDirection);

        // 举刀过头: 从持平缓慢举起 + 满蓄微抖 (dir=-1 走左侧短弧扫向头顶)
        float raise = ACMUtils.QuadOut(Charge);
        Projectile.rotation = Projectile.spriteDirection == 1
            ? MathHelper.Lerp(-0.45f, -MathHelper.PiOver2, raise)
            : MathHelper.Lerp(MathF.PI + 0.45f, MathF.PI * 1.5f, raise);
        if (Charge > 0.9f && Timer < ChargeTime)
            Projectile.rotation += Main.rand.NextFloat(-0.03f, 0.03f);

        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
            Projectile.rotation - MathHelper.PiOver2);
        Vector2 arm = Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full,
            Projectile.rotation - MathHelper.PiOver2);
        arm.Y += Owner.gfxOffY;
        Projectile.Center = arm;

        if (Timer < ChargeTime) {
            // 汇聚粒子: 密度∝√charge, 72% 后硬切 (呐喊前的吸气)
            if (Charge < 0.72f && Main.rand.NextFloat() < 0.25f + 0.6f * MathF.Sqrt(Charge)) {
                Vector2 spawn = Owner.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(120f, 280f);
                Vector2 pull = (Owner.Center - spawn) * 0.085f;
                if (Main.rand.NextBool())
                    pull = pull.RotatedBy(MathHelper.PiOver2 * 0.6f); // 切向家族: 汇聚带旋
                Dust d = Dust.NewDustPerfect(spawn, DustID.JungleTorch, pull, 80, default, 1.6f);
                d.noGravity = true;
            }
            // 低频轰鸣: charge² 渐强
            if ((int)Timer % 6 == 0)
                WeaponVFX.AddScreenShake(Owner.Center, Charge * Charge * 1.8f);
            // 音高爬升 tick
            if ((int)Timer % 9 == 0)
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.3f, Pitch = -0.2f + Charge * 0.7f }, Owner.Center);
        }
        else if ((int)Timer == ChargeTime) {
            // ===== 释放帧 =====
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.9f, Pitch = 0.05f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.65f, Pitch = -0.25f }, Owner.Center);
            WeaponVFX.AddScreenShake(Owner.Center, 5f);

            if (Main.myPlayer == Projectile.owner) {
                int dir = Projectile.spriteDirection;
                for (int i = 0; i < 3; i++) {
                    Vector2 probe = Owner.Center + new Vector2(dir * (120f + 140f * i), 0f);
                    Vector2 ground = DivineWoodRoot.FindGroundBelow(probe);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), ground, Vector2.Zero,
                        ModContent.ProjectileType<DivineWoodPillar>(),
                        (int)(Projectile.damage * 2.2f), 8f, Projectile.owner, i * 8f, i);
                }
            }
        }

        Lighting.AddLight(Owner.Center, 0.3f + Charge * 0.4f, 0.8f + Charge * 0.5f, 0.35f);
        Timer++;
    }

    public override bool PreDraw(ref Color lightColor) {
        // 地面年轮法阵: 随蓄力生长, 释放后残留淡出
        float ringGrow = Timer < ChargeTime ? ACMUtils.QuadOut(Charge) : 1f;
        float ringInten = Timer < ChargeTime
            ? 0.5f + Charge * 0.4f
            : 0.9f * (1f - (Timer - ChargeTime) / AfterTime);
        Vector2 feet = DivineWoodRoot.FindGroundBelow(Owner.Bottom, 8);
        DivineWoodFX.DrawGrowthRing(feet, 150f, ringGrow, ringInten,
            (float)Main.timeForVisualEffects * 0.01f);

        // 释放后 12 帧: 翠色染屏定调 (占本帧全屏名额) + 径向辉光 (名额已被占 → 自动退化为柔光)
        if (Timer >= ChargeTime && Timer < ChargeTime + 12) {
            float tintFade = 1f - (Timer - ChargeTime) / 12f;
            WeaponVFX.ApplyPaletteTint(Main.spriteBatch,
                new Color(20, 90, 50, 170), new Color(190, 255, 170, 190), 0.12f * tintFade);
            WeaponVFX.DrawRadialBloom(Owner.Center, 0.13f, tintFade * 0.6f, DivineWoodPalette.Emerald, 8f);
        }

        // 举刀 (翠脉流光随蓄力增亮)
        Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
        SpriteEffects fxFlip = Projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Vector2 origin = Projectile.spriteDirection > 0
            ? new Vector2(0, tex.Height) : new Vector2(tex.Width, tex.Height);
        float rotOff = Projectile.spriteDirection > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi;
        DivineWoodFX.DrawSapFlowSprite(tex, Projectile.Center, null, lightColor,
            Projectile.rotation + rotOff, origin, 1.4f, fxFlip, 0.35f + 0.65f * Charge);
        return false;
    }
}

/// <summary>
/// 建木巨柱 — 大招释放的擎天木柱: 地裂 telegraph → 10 帧拔地而起 (poly8) → 驻留 → 淡出。
/// ai[0]=出土延迟, ai[1]=柱序 (0/1/2, 越远越高震越重)。命中引爆生根。
/// </summary>
public class DivineWoodPillar : ModProjectile
{
    public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private const int EruptTime = 10;
    private const int HoldTime = 16;
    private const int FadeTime = 22;

    private int Delay => (int)Projectile.ai[0];
    private int Index => (int)Projectile.ai[1];
    private ref float Timer => ref Projectile.localAI[0];

    private float Height => 340f + 40f * Index;
    private const float HalfWidth = 38f;

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 300;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override void OnSpawn(IEntitySource source) {
        Projectile.timeLeft = Delay + EruptTime + HoldTime + FadeTime;
    }

    private float EruptP() {
        float t = Timer - Delay;
        return t <= 0f ? 0f : Math.Min(t / EruptTime, 1f);
    }

    private float CurrentHeight() => Height * (1f - MathF.Pow(1f - EruptP(), 8f));

    private float FadeAlpha() {
        float t = Timer - Delay - EruptTime - HoldTime;
        return t <= 0f ? 1f : 1f - Math.Min(t / FadeTime, 1f);
    }

    public override void AI() {
        Timer++;

        // 出土前: 地裂 telegraph
        if (Timer < Delay) {
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), 0f),
                    DustID.Dirt, new Vector2(0, -Main.rand.NextFloat(1.5f, 3.5f)), 50, default, 1.5f);
                d.noGravity = false;
            }
            return;
        }

        // 出土瞬间
        if ((int)Timer == Delay) {
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.8f, Pitch = -0.15f + 0.12f * Index }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.75f, Pitch = 0.4f }, Projectile.Center);
            WeaponVFX.AddScreenShake(Projectile.Center, 4f + Index);
            for (int i = 0; i < 26; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center,
                    Main.rand.NextBool(3) ? DustID.Dirt : DustID.JungleTorch,
                    new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(3f, 10f)), 40, default,
                    Main.rand.NextFloat(1.4f, 2.6f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        // 柱身粒子: 沿柱上升的叶灵 + 顶冠花瓣喷泉
        float h = CurrentHeight();
        if (h > 20f && FadeAlpha() > 0.4f) {
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth * 0.7f, HalfWidth * 0.7f), -Main.rand.NextFloat(0f, h)),
                    DustID.JungleTorch, new Vector2(0, -Main.rand.NextFloat(1f, 3f)), 80, default, 1.4f);
                d.noGravity = true;
            }
            if (EruptP() >= 1f && Main.rand.NextBool(3)) {
                Dust p = Dust.NewDustPerfect(Projectile.Center - new Vector2(0, h),
                    DustID.JunglePlants, new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(2f, 5f)),
                    60, default, 1.3f);
                p.noGravity = false;
            }
        }

        Lighting.AddLight(Projectile.Center - new Vector2(0, h * 0.5f), 0.4f, 1.1f, 0.5f);
    }

    public override bool? CanDamage() {
        float t = Timer - Delay;
        return t >= 0f && t <= EruptTime + HoldTime ? null : false;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float h = CurrentHeight();
        if (h < 8f)
            return false;
        Rectangle rect = new((int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - h),
            (int)(HalfWidth * 2), (int)h);
        return rect.Intersects(targetHitbox);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        DivineWoodRoot.TriggerBloom(Projectile.GetSource_OnHit(target), target,
            (int)(Projectile.damage * 0.55f), 4f, Projectile.owner);
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.DivineWood, scale: 1.2f, owner: Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        float h = CurrentHeight();
        float alpha = FadeAlpha();
        float eruptP = EruptP();

        // 出土前 telegraph: 小年轮生长
        if (Timer < Delay && Delay > 0) {
            DivineWoodFX.DrawGrowthRing(Projectile.Center, 70f, Timer / Delay, 0.45f);
            return false;
        }
        if (h < 8f)
            return false;

        Vector2 basePos = Projectile.Center;
        Vector2 topPos = basePos - new Vector2(0, h);

        // 柱身: 外宽暗 + 内窄亮双层竖光柱 (BeamGrad 有机流纹)
        ACMShaders.DrawBeam(basePos, topPos, HalfWidth * alpha,
            DivineWoodPalette.Emerald with { A = 170 }, DivineWoodPalette.DeepGreen,
            alpha * 0.95f, flowSpeed: 2.4f, flowScale: 1.5f, coreSharp: 2.6f);
        ACMShaders.DrawBeam(basePos, topPos, HalfWidth * 0.42f * alpha,
            DivineWoodPalette.BrightCore with { A = 210 }, DivineWoodPalette.Emerald,
            alpha * 0.9f, flowSpeed: 3.2f, flowScale: 2.2f, coreSharp: 3f);

        // 顶冠绽放
        WeaponVFX.DrawGlowBurst(topPos, 1.5f * alpha, DivineWoodPalette.BrightCore * (alpha * 0.85f));

        // 出土冲击环 + 基座小年轮 (只有首柱申请 decal, 省预算)
        if (eruptP < 1f)
            WeaponVFX.DrawShockwaveRing(basePos, ACMUtils.QuadOut(eruptP) * 130f, 14f,
                (1f - eruptP) * 0.9f, DivineWoodPalette.BrightCore, DivineWoodPalette.DeepGreen);
        if (Index == 0)
            DivineWoodFX.DrawGrowthRing(basePos, 90f, 1f, 0.5f * alpha);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        Texture2D sparkle = ACMAsset.Sparkle;
        sb.Draw(sparkle, topPos - Main.screenPosition, null,
            new Color(150, 255, 160) * (alpha * 0.7f), (float)Main.timeForVisualEffects * 0.05f,
            sparkle.Size() * 0.5f, 0.8f * alpha, SpriteEffects.None, 0);
        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);
        return false;
    }
}
