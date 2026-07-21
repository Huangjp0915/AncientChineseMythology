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

namespace AncientChineseMythology.Items.Weapons.Woodlands;

/// <summary>
/// 林地巨剑 - 战士旗舰 (重做: 手持弹幕三连段)
/// 横斩 → 回斩 → 根须重斩 (剑尖落点爆出荆根); 命中积累"生机",
/// 满 10 层后下一次重斩升级为大招"万木生" (根须环爆 + 年轮脉冲)。
/// </summary>
public class WoodlandGreatsword : ModItem
{
    /// <summary>生机上限 (命中+1 / 暴击+2)。</summary>
    public const int VigorMax = 10;
    /// <summary>连段保持窗口 (挥砍结束后帧数)。</summary>
    private const int ComboKeepFrames = 80;

    // —— 连段/生机状态 (仅 owner 端 Shoot/OnHit 消费, 不参与同步) ——
    internal int comboStep;
    internal int vigor;
    private uint _lastSwingTime;

    /// <summary>三段各自的挥舞时长 (帧), 供 UseTime/UseAnimation 乘数与弹幕读取。</summary>
    internal static int ComboDuration(int step) => step switch { 1 => 24, 2 => 34, _ => 26 };

    /// <summary>挥砍弹幕类型 (赤铜升级覆写)。</summary>
    protected virtual int SwingProjType => ModContent.ProjectileType<WoodlandSwing>();
    /// <summary>生机满层提示光色 (赤铜升级覆写)。</summary>
    protected virtual Color VigorGlowColor => new(150, 255, 120);

    public override void SetDefaults() {
        Item.damage = 16;
        Item.crit = 4;
        Item.DamageType = DamageClass.Melee;
        Item.width = 48;
        Item.height = 48;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 6f;
        Item.value = Item.buyPrice(silver: 50);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = null; // 分段手动播放
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<WoodlandSwing>();
        Item.shootSpeed = 1f;
        Item.scale = 1.1f;
    }

    public override float UseTimeMultiplier(Player player) => ComboDuration(comboStep) / 30f;
    public override float UseAnimationMultiplier(Player player) => ComboDuration(comboStep) / 30f;

    public override bool CanUseItem(Player player) {
        // 连段窗口超时回段 1
        if (_lastSwingTime != 0 && Main.GameUpdateCount - _lastSwingTime > ComboKeepFrames)
            comboStep = 0;
        return true;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        int step = comboStep;
        bool bloom = step == 2 && vigor >= VigorMax;
        if (bloom)
            vigor = 0; // 万木生在重斩落点释放, 挥出即消耗

        // 清掉残留旧挥砍 (hitstop 可能令其比动画多活几帧)
        if (player.ownedProjectileCounts[SwingProjType] > 0) {
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.owner == player.whoAmI && p.type == SwingProjType)
                    p.Kill();
            }
        }

        Vector2 aim = velocity.SafeNormalize(Vector2.UnitX * player.direction);
        Projectile.NewProjectile(source, player.MountedCenter, aim, SwingProjType,
            damage, knockback, player.whoAmI, ai0: step + (bloom ? 10 : 0));

        // 分段挥砍音: 段数越深 pitch 越沉, 重斩前摇配拉弓声
        float pitch = step switch { 1 => 0.1f, 2 => -0.25f, _ => -0.05f };
        SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = pitch + Main.rand.NextFloat(-0.05f, 0.05f) }, player.Center);
        if (step == 2)
            SoundEngine.PlaySound(SoundID.Item7 with { Volume = 0.5f, Pitch = -0.4f }, player.Center);

        _lastSwingTime = Main.GameUpdateCount;
        comboStep = (step + 1) % 3;
        return false;
    }

    /// <summary>挥砍弹幕命中回写生机 (仅 owner 端调用)。</summary>
    internal void AddVigor(int amount) {
        vigor = Math.Min(vigor + amount, VigorMax);
    }

    public override void HoldItem(Player player) {
        // 生机满层: 剑上翠光呼吸提示 "下一次重斩 = 万木生"
        if (vigor >= VigorMax && !Main.dedServ && Main.rand.NextBool(4)) {
            Vector2 pos = player.Center + Main.rand.NextVector2Circular(30f, 30f);
            Dust d = Dust.NewDustPerfect(pos, DustID.GreenTorch,
                new Vector2(0, -Main.rand.NextFloat(0.5f, 1.4f)), 100, VigorGlowColor, 1.1f);
            d.noGravity = true;
        }
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 20)
            .AddIngredient(ItemID.Vine, 3)
            .AddIngredient(ItemID.JungleSpores, 5)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

/// <summary>
/// 林地巨剑挥砍 (手持弹幕) - 三段挥舞波形: 前摇反拉 → poly 高次爆发 → 定格收招。
/// ai[0] = 段数 (0/1/2, +10 = 万木生重斩); 方向存于 velocity (单位向量)。
/// </summary>
public class WoodlandSwing : ModProjectile
{
    public override string Texture => "AncientChineseMythology/Items/Weapons/Woodlands/WoodlandGreatsword";

    private const int TrailLen = 14;

    private int Step => (int)Projectile.ai[0] % 10;
    private bool Bloom => Projectile.ai[0] >= 10f;

    private ref float Duration => ref Projectile.localAI[0];

    private int _hitstop;
    private bool _spiked;
    private bool _struckSoundDone;
    private Vector2[] _tipHistory;
    private int _tipCount;

    // —— 主题挂点 (赤铜挥砍覆写) ——
    protected virtual Color TrailOuter => new(90, 70, 40, 160);
    protected virtual Color TrailInner => new(170, 255, 130, 210);
    protected virtual int BurstTheme => ACMWeaponBurst.Nature;
    /// <summary>荆根/年轮主题参数 (0=Nature, 1=Cuprite)。</summary>
    protected virtual int GroundTheme => 0;
    protected virtual int SwingDustType => DustID.Grass;

    public override void SetStaticDefaults() {
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.WoodlandSwing.DisplayName", () => "林地巨剑");
    }

    public override void SetDefaults() {
        Projectile.width = 40;
        Projectile.height = 40;
        Projectile.friendly = false; // 仅爆发窗口开启
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 60;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.ownerHitCheck = true; // 不隔墙命中
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1; // 每段每目标一次
    }

    public override bool ShouldUpdatePosition() => false;

    // ============ 挥舞波形 ============

    private void GetSwingParams(out float antiFrac, out float strikeFrac, out float windup, out float power,
        out float startOff, out float endOff) {
        switch (Step) {
            case 1: // 回斩: 反向, 更快
                antiFrac = 0.26f; strikeFrac = 0.18f; windup = 0.09f; power = 8f;
                startOff = 1.9f; endOff = -1.8f;
                break;
            case 2: // 根须重斩: 高举过头, 最长前摇, 最狠的 snap
                antiFrac = 0.42f; strikeFrac = 0.16f; windup = 0.14f; power = 14f;
                startOff = -2.7f; endOff = 1.05f;
                break;
            default: // 横斩
                antiFrac = 0.32f; strikeFrac = 0.20f; windup = 0.10f; power = 10f;
                startOff = -2.1f; endOff = 1.75f;
                break;
        }
    }

    /// <summary>三段合成曲线: 0→(-windup)→1(+微回弹), MOTION.md §1。</summary>
    private static float SwingCurve(float p, float antiFrac, float strikeFrac, float windup, float power) {
        if (p < antiFrac) {
            float t = p / antiFrac;
            float ease = t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f; // ease-in-out quad
            return -windup * ease;
        }
        if (p < antiFrac + strikeFrac) {
            float t = (p - antiFrac) / strikeFrac;
            float ease = 1f - MathF.Pow(1f - t, power); // 高次 ease-out: 力量感的关键
            return -windup + (1f + windup) * ease;
        }
        float r = (p - antiFrac - strikeFrac) / MathF.Max(1f - antiFrac - strikeFrac, 0.001f);
        return 1f + 0.05f * MathF.Sin(r * MathHelper.Pi) * (1f - r); // 收招微回弹
    }

    private float Progress => Duration <= 0f ? 0f : 1f - Projectile.timeLeft / Duration;

    private int DirSign {
        get {
            if (MathF.Abs(Projectile.velocity.X) > 0.01f)
                return Projectile.velocity.X >= 0f ? 1 : -1;
            return Main.player[Projectile.owner].direction;
        }
    }

    private float CurrentAngle {
        get {
            GetSwingParams(out float anti, out float strike, out float windup, out float power,
                out float startOff, out float endOff);
            float baseAngle = Projectile.velocity.ToRotation();
            int sign = DirSign;
            float t = SwingCurve(Progress, anti, strike, windup, power);
            return baseAngle + MathHelper.Lerp(startOff * sign, endOff * sign, t);
        }
    }

    /// <summary>刃身伸展 (突刺感: strike 段微伸出)。</summary>
    private float ReachScale {
        get {
            GetSwingParams(out float anti, out float strike, out _, out _, out _, out _);
            float p = Progress;
            if (p < anti) return 0.88f;
            if (p < anti + strike) return MathHelper.Lerp(0.88f, 1f, (p - anti) / strike);
            return 1f;
        }
    }

    private bool InStrikeWindow {
        get {
            GetSwingParams(out float anti, out float strike, out _, out _, out _, out _);
            float p = Progress;
            return p >= anti - 0.02f && p <= anti + strike + 0.10f;
        }
    }

    // ============ AI ============

    public override void AI() {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead || owner.CCed || owner.noItems) {
            Projectile.Kill();
            return;
        }

        // 首帧: 与物品动画同步时长
        if (Duration <= 0f) {
            Duration = Math.Max(owner.itemAnimationMax, 10);
            Projectile.timeLeft = (int)Duration;
        }

        // 命中定帧 (hitstop): 冻结进度, 重量感
        if (_hitstop > 0) {
            _hitstop--;
            Projectile.timeLeft++;
        }

        owner.heldProj = Projectile.whoAmI;
        int sign = DirSign;
        owner.ChangeDir(sign);

        float angle = CurrentAngle;
        float reach = (26f + 62f * ReachScale) * owner.HeldItem.scale;
        Projectile.Center = owner.MountedCenter + angle.ToRotationVector2() * (reach * 0.55f);
        Projectile.rotation = angle;
        owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, angle - MathHelper.PiOver2);
        owner.itemRotation = MathHelper.WrapAngle(sign < 0 ? angle + MathHelper.Pi : angle);

        Projectile.friendly = InStrikeWindow;

        // 出刃瞬间的破空音 (strike 起点一次)
        GetSwingParams(out float anti, out float strike, out _, out _, out _, out _);
        if (!_struckSoundDone && Progress >= anti) {
            _struckSoundDone = true;
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.35f, Pitch = 0.3f + Step * 0.1f }, Projectile.Center);
        }

        // 刃尖轨迹记录 (strike 段满亮, 其余淡)
        _tipHistory ??= new Vector2[TrailLen];
        Vector2 tip = owner.MountedCenter + angle.ToRotationVector2() * (26f + 62f * ReachScale) * owner.HeldItem.scale;
        for (int i = TrailLen - 1; i > 0; i--)
            _tipHistory[i] = _tipHistory[i - 1];
        _tipHistory[0] = tip;
        if (_tipCount < TrailLen)
            _tipCount++;

        // strike 段叶尘飞散 (速度门控装饰)
        if (InStrikeWindow && Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Vector2.Lerp(owner.MountedCenter, tip, Main.rand.NextFloat(0.5f, 1f)),
                SwingDustType, angle.ToRotationVector2().RotatedBy(sign * MathHelper.PiOver2) * Main.rand.NextFloat(2f, 5f),
                80, default, Main.rand.NextFloat(0.9f, 1.3f));
            d.noGravity = true;
        }

        // 重斩落点: strike 结束帧触发荆根 (owner 端生成)
        if (Step == 2 && !_spiked && Progress >= anti + strike) {
            _spiked = true;
            SlamGround(owner, tip);
        }

        Lighting.AddLight(Projectile.Center, 0.08f, 0.2f, 0.06f);
    }

    /// <summary>重斩落地: 荆根爆发 / 万木生根须环爆。</summary>
    private void SlamGround(Player owner, Vector2 tip) {
        WeaponVFX.AddScreenShake(tip, Bloom ? 7f : 4f);
        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.9f, Pitch = -0.5f }, tip);
        if (Bloom)
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = -0.2f }, tip);

        if (Projectile.owner != Main.myPlayer)
            return;

        Vector2 ground = FindGround(tip);
        int sign = DirSign;
        int spikeDamage = (int)(Projectile.damage * 0.9f);
        IEntitySource src = Projectile.GetSource_FromAI();

        if (!Bloom) {
            // 常规重斩: 前向 3 根荆根
            for (int i = 0; i < 3; i++) {
                Vector2 pos = FindGround(ground + new Vector2(sign * (26f + i * 46f), -16f));
                Projectile.NewProjectile(src, pos, Vector2.Zero, ModContent.ProjectileType<WoodlandRootSpike>(),
                    spikeDamage, Projectile.knockBack * 0.7f, Projectile.owner, ai0: i * 3f, ai1: GroundTheme);
            }
        }
        else {
            // 万木生: 双向 8 根环爆 + 年轮脉冲
            for (int i = 0; i < 8; i++) {
                int side = i % 2 == 0 ? 1 : -1;
                float dist = 44f + (i / 2) * 58f;
                Vector2 pos = FindGround(ground + new Vector2(side * dist, -16f));
                Projectile.NewProjectile(src, pos, Vector2.Zero, ModContent.ProjectileType<WoodlandRootSpike>(),
                    spikeDamage, Projectile.knockBack * 0.7f, Projectile.owner, ai0: (i / 2) * 3f + 2f, ai1: GroundTheme);
            }
            Projectile.NewProjectile(src, ground - new Vector2(0, 30f), Vector2.Zero,
                ModContent.ProjectileType<WoodlandVerdantPulseVFX>(), 0, 0f, Projectile.owner, ai0: GroundTheme);
            ACMWeaponBurst.Spawn(src, ground, BurstTheme, 2f, Projectile.owner);
        }
    }

    /// <summary>从起点向下找地表 (最多 12 格), 找不到则原地。</summary>
    internal static Vector2 FindGround(Vector2 from) {
        int tx = (int)(from.X / 16f);
        int ty = (int)(from.Y / 16f);
        for (int j = 0; j < 12; j++) {
            int y = ty + j;
            if (!WorldGen.InWorld(tx, y, 8))
                break;
            Tile t = Main.tile[tx, y];
            if (t.HasUnactuatedTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType])
                return new Vector2(from.X, y * 16f);
        }
        return from;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        Player owner = Main.player[Projectile.owner];
        float angle = CurrentAngle;
        float reach = (26f + 62f * ReachScale) * owner.HeldItem.scale;
        Vector2 start = owner.MountedCenter + angle.ToRotationVector2() * 14f;
        Vector2 end = owner.MountedCenter + angle.ToRotationVector2() * reach;
        float _ = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 34f, ref _);
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
        if (Step == 2)
            modifiers.FinalDamage *= 1.25f; // 重斩本体加成
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        _hitstop = 3; // 命中定帧

        // 生机回写 (owner 端)
        if (Projectile.owner == Main.myPlayer &&
            Main.player[Projectile.owner].HeldItem?.ModItem is WoodlandGreatsword sword)
            sword.AddVigor(hit.Crit ? 2 : 1);

        if (Main.rand.NextBool(4))
            target.AddBuff(BuffID.Poisoned, 120);

        OnThemeHit(target);

        for (int i = 0; i < 6; i++) {
            int type = Main.rand.NextBool() ? DustID.Grass : DustID.GrassBlades;
            Dust d = Dust.NewDustPerfect(target.Center, type,
                Main.rand.NextVector2Circular(3.5f, 3.5f), 60, default, Main.rand.NextFloat(1f, 1.5f));
            d.noGravity = true;
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            BurstTheme, Step == 2 ? 1.3f : 1f, Projectile.owner);
        WeaponVFX.AddScreenShake(target.Center, Step == 2 ? 3f : 1.5f);
        SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.5f, Pitch = -0.2f + Step * 0.12f + Main.rand.NextFloat(0.08f) }, target.Center);
    }

    /// <summary>材料主题附加命中效果 (基础版无; 赤铜覆写点燃+火星链)。</summary>
    protected virtual void OnThemeHit(NPC target) { }

    // ============ 绘制 ============

    public override bool PreDraw(ref Color lightColor) {
        Player owner = Main.player[Projectile.owner];
        float angle = CurrentAngle;
        int sign = DirSign;

        // 刃尖拖尾: strike 段满亮, 其余弱 (速度门控)
        if (_tipCount >= 2) {
            float gate = InStrikeWindow ? 1f : 0.28f;
            var pts = new Vector2[_tipCount];
            Array.Copy(_tipHistory, pts, _tipCount);
            Color outer = TrailOuter * gate;
            outer.A = (byte)(TrailOuter.A * gate);
            Color inner = TrailInner * gate;
            inner.A = (byte)(TrailInner.A * gate);
            WeaponVFX.DrawRibbonTrail(pts, 20f, outer, inner,
                tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 1.2f);
        }

        // 剑体
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        Vector2 handle = owner.MountedCenter + angle.ToRotationVector2() * 10f - Main.screenPosition;
        float drawRot;
        Vector2 origin;
        SpriteEffects fx;
        if (sign >= 0) {
            fx = SpriteEffects.None;
            drawRot = angle + MathHelper.PiOver4;
            origin = new Vector2(0, tex.Height);
        }
        else {
            fx = SpriteEffects.FlipHorizontally;
            drawRot = angle + MathHelper.PiOver4 + MathHelper.PiOver2;
            origin = new Vector2(tex.Width, tex.Height);
        }
        Main.spriteBatch.Draw(tex, handle, null, lightColor, drawRot, origin,
            owner.HeldItem.scale, fx, 0f);

        // 万木生重斩: 剑身翠光呼吸
        if (Bloom && Step == 2) {
            Vector2 mid = owner.MountedCenter + angle.ToRotationVector2() * 52f;
            float pulse = 0.5f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f);
            WeaponVFX.DrawGlowBurst(mid, 0.9f * pulse, TrailInner * 0.8f);
        }
        return false;
    }
}

/// <summary>
/// 荆根尖刺 - 重斩落点从地面钻出的根须 (程序化绘制, 无贴图)。
/// ai[0] = 出土延迟帧; ai[1] = 主题 (0=林地翠绿, 1=赤铜火根)。
/// </summary>
public class WoodlandRootSpike : ModProjectile
{
    public override string Texture => "InnoVault/Assets/placeholder";

    private const int GrowFrames = 8;
    private const int HoldFrames = 12;
    private const int WitherFrames = 18;

    private int Delay => (int)Projectile.ai[0];
    private bool Cuprite => Projectile.ai[1] == 1f;

    private ref float Timer => ref Projectile.localAI[0];

    public override void SetStaticDefaults() {
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.WoodlandRootSpike.DisplayName", () => "荆根");
    }

    public override void SetDefaults() {
        Projectile.width = 30;
        Projectile.height = 88;
        Projectile.friendly = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 90; // 实际由 Delay+生长+枯萎决定, AI 内自杀
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 30;
    }

    public override bool ShouldUpdatePosition() => false;

    /// <summary>当前生长比例 0→1 (出土后 poly(6) 快出)。</summary>
    private float Growth {
        get {
            float t = Timer - Delay;
            if (t <= 0f) return 0f;
            if (t >= GrowFrames) {
                float witherT = t - GrowFrames - HoldFrames;
                if (witherT <= 0f) return 1f;
                return MathHelper.Clamp(1f - witherT / WitherFrames, 0f, 1f);
            }
            return 1f - MathF.Pow(1f - t / GrowFrames, 6f);
        }
    }

    private bool Withering => Timer - Delay > GrowFrames + HoldFrames;

    public override void AI() {
        Timer++;
        if (Timer == 1f)
            Projectile.Center -= new Vector2(0, Projectile.height * 0.5f); // 生成点为地表 → 底部锚地

        float t = Timer - Delay;
        if (t < 0f) {
            // 出土预告: 地表土屑翻涌 (玩家能看到"因")
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-12f, 12f), 0f),
                    DustID.Dirt, new Vector2(0, -Main.rand.NextFloat(1f, 2.5f)), 60, default, 1.1f);
                d.noGravity = false;
            }
            return;
        }

        if (t == 1f) {
            // 破土瞬间
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = 0.35f + Main.rand.NextFloat(0.1f) }, Projectile.Bottom);
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Bottom,
                    Main.rand.NextBool() ? DustID.Dirt : (Cuprite ? DustID.Torch : DustID.GrassBlades),
                    new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(2f, 6f)), 40, default,
                    Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        // 判定窗口: 生长期 + 短保持
        Projectile.friendly = t >= 1f && !Withering;

        if (Cuprite && Projectile.friendly && Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Bottom - new Vector2(0, Main.rand.NextFloat(20f, 70f) * Growth),
                DustID.Torch, new Vector2(0, -1f), 80, default, 1f);
            d.noGravity = true;
        }

        if (Cuprite)
            Lighting.AddLight(Projectile.Center, 0.4f, 0.2f, 0.05f);
        else
            Lighting.AddLight(Projectile.Center, 0.06f, 0.18f, 0.05f);

        if (t > GrowFrames + HoldFrames + WitherFrames)
            Projectile.Kill();
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float h = Projectile.height * Growth;
        var rect = new Rectangle((int)(Projectile.Bottom.X - Projectile.width * 0.5f),
            (int)(Projectile.Bottom.Y - h), Projectile.width, (int)h);
        return rect.Intersects(targetHitbox);
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
        modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
        modifiers.Knockback *= 1.2f; // 根须上挑
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        if (Cuprite)
            target.AddBuff(BuffID.OnFire, 120);
        else if (Main.rand.NextBool(3))
            target.AddBuff(BuffID.Poisoned, 120);
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            Cuprite ? ACMWeaponBurst.CupriteBurn : ACMWeaponBurst.Nature, 0.8f, Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        float growth = Growth;
        if (growth <= 0.01f)
            return false;
        Texture2D tex = ACMAsset.SlashBurst;
        if (tex == null)
            return false;

        float t = Timer - Delay;
        float witherLerp = Withering ? MathHelper.Clamp((t - GrowFrames - HoldFrames) / WitherFrames, 0f, 1f) : 0f;

        // 主题双色: 外宽暗 + 内窄亮; 枯萎期褪为枯褐
        Color outer = Cuprite ? new Color(190, 55, 15) : new Color(60, 120, 45);
        Color inner = Cuprite ? new Color(255, 190, 90) : new Color(180, 255, 140);
        outer = Color.Lerp(outer, new Color(80, 60, 35), witherLerp);
        inner = Color.Lerp(inner, new Color(120, 95, 60), witherLerp);
        float alpha = 1f - witherLerp * 0.6f;

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        Vector2 bottom = Projectile.Bottom - Main.screenPosition;
        Vector2 origin = new(tex.Width * 0.5f, tex.Height);
        float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.whoAmI) * 0.05f * growth;
        float hScale = Projectile.height * growth / tex.Height;

        sb.Draw(tex, bottom, null, outer * (0.85f * alpha), sway, origin,
            new Vector2(Projectile.width * 1.5f / tex.Width, hScale * 1.05f), SpriteEffects.None, 0f);
        sb.Draw(tex, bottom, null, inner * alpha, sway, origin,
            new Vector2(Projectile.width * 0.7f / tex.Width, hScale), SpriteEffects.None, 0f);

        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);
        return false;
    }
}

/// <summary>
/// 年轮脉冲 VFX - 万木生/荣枯页的大招演出弹幕 (WoodlandVerdantPulse.fx, 世界空间 quad, 不占全屏名额)。
/// ai[0] = 主题: 0=巨剑翠绿, 1=赤铜橙焰, 2=秘典嫩绿金。纯视觉 (damage=0)。
/// </summary>
public class WoodlandVerdantPulseVFX : ModProjectile
{
    public override string Texture => "InnoVault/Assets/placeholder";

    private const int LifeTime = 44;
    private const float Radius = 290f;

    private int Theme => (int)Projectile.ai[0];

    public override void SetStaticDefaults() {
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.WoodlandVerdantPulseVFX.DisplayName", () => "年轮脉冲");
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

    private void GetThemeColors(out Color inner, out Color outer) {
        switch (Theme) {
            case 1: // 赤铜燎原
                inner = new Color(255, 200, 110);
                outer = new Color(200, 70, 20);
                break;
            case 2: // 秘典荣枯 (嫩绿金)
                inner = new Color(230, 255, 170);
                outer = new Color(120, 200, 70);
                break;
            default: // 林地翠绿
                inner = new Color(180, 255, 140);
                outer = new Color(45, 160, 60);
                break;
        }
    }

    public override void AI() {
        float progress = 1f - Projectile.timeLeft / (float)LifeTime;
        GetThemeColors(out _, out Color outer);

        // 波前叶尘 (稀疏, 随波扩散)
        if (progress is > 0.05f and < 0.7f && Main.rand.NextBool(2)) {
            float front = progress * Radius;
            float ang = Main.rand.NextFloat(MathHelper.TwoPi);
            Dust d = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * front,
                Theme == 1 ? DustID.Torch : DustID.GrassBlades,
                ang.ToRotationVector2() * Main.rand.NextFloat(1f, 2.5f), 70, default, 1.2f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, outer.ToVector3() * 0.6f * (1f - progress));
    }

    public override bool PreDraw(ref Color lightColor) {
        if (Main.dedServ)
            return false;

        float progress = 1f - Projectile.timeLeft / (float)LifeTime;
        GetThemeColors(out Color inner, out Color outer);

        Effect fx = WeaponVFX.GetEffect("WoodlandVerdantPulse");
        Texture2D glow = ACMAsset.SoftGlow;
        Texture2D noise = ACMShaders.NoiseTexture;
        if (fx == null || glow == null || noise == null) {
            // 容错退化: 冲击环 + 柔光
            WeaponVFX.DrawShockwaveRing(Projectile.Center, progress * Radius, 14f, 1f - progress, inner, outer);
            WeaponVFX.DrawGlowBurst(Projectile.Center, 2f * (1f - progress), inner);
            return false;
        }

        fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
        fx.Parameters["uProgress"]?.SetValue(progress);
        fx.Parameters["uIntensity"]?.SetValue(1f);
        fx.Parameters["uRayCount"]?.SetValue(12f);
        fx.Parameters["uColorInner"]?.SetValue(inner.ToVector4());
        fx.Parameters["uColorOuter"]?.SetValue(outer.ToVector4());

        SpriteBatch sb = Main.spriteBatch;
        GraphicsDevice gd = Main.graphics.GraphicsDevice;
        sb.End();
        sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
        gd.Textures[1] = noise;
        gd.SamplerStates[1] = SamplerState.LinearWrap;
        float scale = Radius * 2f / glow.Width;
        sb.Draw(glow, Projectile.Center - Main.screenPosition, null, Color.White, 0f,
            glow.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);

        // 波前叠一道廉价冲击环增加"实体感"
        if (progress < 0.8f)
            WeaponVFX.DrawShockwaveRing(Projectile.Center, progress * Radius, 10f,
                (1f - progress / 0.8f) * 0.6f, inner, outer);
        return false;
    }
}
