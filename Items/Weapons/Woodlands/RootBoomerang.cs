using AncientChineseMythology.Helpers;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands;

/// <summary>
/// 树根回力镖 - 战士回力镖 (重做: 接镖研磨)
/// 返回时接住镖 → +1"研磨"层 (≤3, 每层 +10% 伤害 +8% 出手速度, 20s 保持);
/// 落地未接/超时清层。单轮命中 ≥2 时返程增亮且返程伤害 ×1.2。
/// </summary>
public class RootBoomerang : ModItem
{
    public const int MaxGrind = 3;
    private const int GrindKeepFrames = 1200; // 20s

    // 研磨状态 (仅 owner 端消费)
    internal int grindStacks;
    private uint _grindTime;

    /// <summary>投掷弹幕类型 (玄铁升级覆写)。</summary>
    protected virtual int BoomerangType => ModContent.ProjectileType<RootBoomerangProj>();
    protected virtual int GrindDustType => DustID.Grass;

    public override void SetDefaults() {
        Item.damage = 13;
        Item.crit = 4;
        Item.DamageType = DamageClass.Melee;
        Item.width = 30;
        Item.height = 30;
        Item.useTime = 22;
        Item.useAnimation = 22;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 5f;
        Item.value = Item.buyPrice(silver: 40);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<RootBoomerangProj>();
        Item.shootSpeed = 10f;
    }

    public override bool CanUseItem(Player player) {
        if (grindStacks > 0 && Main.GameUpdateCount - _grindTime > GrindKeepFrames)
            grindStacks = 0;
        return player.ownedProjectileCounts[BoomerangType] < 1;
    }

    public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
        damage += 0.10f * grindStacks;
    }

    public override float UseTimeMultiplier(Player player) => 1f - 0.08f * grindStacks;
    public override float UseAnimationMultiplier(Player player) => 1f - 0.08f * grindStacks;

    /// <summary>接镖回调 (owner 端): 叠研磨 + 反馈。</summary>
    internal void OnCatch(Player player, int flightHits) {
        grindStacks = Math.Min(grindStacks + 1, MaxGrind);
        _grindTime = Main.GameUpdateCount;

        // 接镖反馈: 柔光 + 层数升调 + 微震
        SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.8f, Pitch = 0.1f + grindStacks * 0.15f }, player.Center);
        WeaponVFX.AddScreenShake(player.Center, 1f);
        for (int i = 0; i < 4 + grindStacks * 2; i++) {
            Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(14f, 14f),
                GrindDustType, Main.rand.NextVector2Circular(1.5f, 1.5f), 70, default, 1f);
            d.noGravity = true;
        }
    }

    /// <summary>落地/超时未接住: 研磨清零 (owner 端)。</summary>
    internal void ResetGrind() {
        grindStacks = 0;
    }

    public override void HoldItem(Player player) {
        // 研磨层可读: 手周环绕叶尘 = 层数
        if (Main.dedServ || grindStacks <= 0 || !Main.rand.NextBool(6))
            return;
        for (int i = 0; i < grindStacks; i++) {
            float ang = Main.GlobalTimeWrappedHourly * 3f + i * MathHelper.TwoPi / MaxGrind;
            Dust d = Dust.NewDustPerfect(player.Center + ang.ToRotationVector2() * 20f,
                GrindDustType, new Vector2(0, -0.3f), 120, default, 0.8f);
            d.noGravity = true;
        }
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 15)
            .AddIngredient(ItemID.Vine, 2)
            .AddIngredient(ItemID.Stinger, 2)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

/// <summary>
/// 树根回力镖弹幕 - 飞出减速返回; 返回撞到玩家 = 接镖 (研磨层)。
/// 单轮命中 ≥ 阈值时返程强化 (基础 ×1.2 / 玄铁血怒 ×1.25 + 提速)。
/// </summary>
public class RootBoomerangProj : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/Woodlands/RootBoomerang";

    protected int FlightHits;
    private bool _caught;

    /// <summary>返程强化所需命中数。</summary>
    protected virtual int RageHitThreshold => 2;
    /// <summary>返程强化伤害倍率。</summary>
    protected virtual float RageDamageMul => 1.2f;
    /// <summary>返程强化速度倍率。</summary>
    protected virtual float RageSpeedMul => 1f;
    protected virtual Color TrailOuter => new(90, 70, 40, 150);
    protected virtual Color TrailInner => new(170, 220, 120, 150);
    protected virtual int BurstTheme => ACMWeaponBurst.Nature;

    protected bool Returning => Projectile.ai[0] >= 30f;
    protected bool Raging => FlightHits >= RageHitThreshold;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
        ProjectileID.Sets.TrailingMode[Type] = 0;
    }

    public override void SetDefaults() {
        Projectile.width = 22;
        Projectile.height = 22;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 600;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        // 旋转 (返程强化转更快)
        Projectile.rotation += (Raging && Returning ? 0.5f : 0.35f) * Math.Sign(Projectile.velocity.X == 0 ? 1f : Projectile.velocity.X);

        // 飞出30帧后开始返回
        Projectile.ai[0]++;
        if (Returning) {
            Projectile.tileCollide = false;
            Player owner = Main.player[Projectile.owner];
            Vector2 returnDir = owner.Center - Projectile.Center;
            float dist = returnDir.Length();

            if (dist < 26f) {
                // 接镖: 层数回写 (仅 owner 端)
                _caught = true;
                if (Projectile.owner == Main.myPlayer && owner.HeldItem?.ModItem is RootBoomerang rb)
                    rb.OnCatch(owner, FlightHits);
                Projectile.Kill();
                return;
            }

            returnDir = returnDir.SafeNormalize(Vector2.Zero);
            float speed = 16f * (Raging ? RageSpeedMul : 1f);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, returnDir * speed, 0.09f);

            // 血怒/研磨返程强化视觉
            if (Raging && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, ThemeDustType(),
                    -Projectile.velocity * 0.15f, 70, default, 1.1f);
                d.noGravity = true;
            }
        }
        else {
            // 逐渐减速
            Projectile.velocity *= 0.98f;
        }

        // 叶片粒子
        if (Main.rand.NextBool(4)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, ThemeDustType(),
                Main.rand.NextVector2Circular(1f, 1f), 80, default, 1f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.08f, 0.2f, 0.06f);
    }

    protected virtual int ThemeDustType() => DustID.Grass;

    public override bool OnTileCollide(Vector2 oldVelocity) {
        // 撞墙立即折返 (不销毁, 保住接镖循环), 但研磨在落点抖掉
        if (!Returning) {
            Projectile.ai[0] = 30f;
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
        }
        Projectile.velocity = -oldVelocity * 0.5f;
        return false;
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
        if (Returning && Raging)
            modifiers.FinalDamage *= RageDamageMul;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        FlightHits++;
        OnThemeHit(target);
        for (int i = 0; i < 4; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, ThemeDustType(),
                Main.rand.NextVector2Circular(3f, 3f), 60, default, 1.2f);
            d.noGravity = true;
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            BurstTheme, scale: 0.8f, owner: Projectile.owner);
        SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.4f, Pitch = 0.1f + FlightHits * 0.08f }, target.Center);
    }

    /// <summary>材料主题附加命中效果 (玄铁覆写流血)。</summary>
    protected virtual void OnThemeHit(NPC target) { }

    public override void OnKill(int timeLeft) {
        // 未接住 (超时消散) → 研磨清零
        if (!_caught && Projectile.owner == Main.myPlayer &&
            Main.player[Projectile.owner].HeldItem?.ModItem is RootBoomerang rb)
            rb.ResetGrind();
    }

    public override bool PreDraw(ref Color lightColor) {
        // 旋飞木纹弧光双层拖尾; 返程强化时内层增亮
        bool bright = Returning && Raging;
        Color inner = TrailInner;
        inner.A = (byte)(bright ? 230 : 150);
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: bright ? 11f : 9f,
            outerColor: TrailOuter, innerColor: inner,
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.2f);
        if (bright)
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.5f, inner * 0.6f);
        return true; // 保留镖体贴图
    }
}
