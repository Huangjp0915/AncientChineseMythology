using AncientChineseMythology.Helpers;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands;

/// <summary>
/// 自然秘典 - 法师旗舰 (重做: 页读韵律)
/// 施法翻页 (书周绿萤 0~4 粒可读); 第 5 页为大招"荣枯页":
/// 在指向处绽放 8 叶生命之环 (螺旋展开→悬停→收拢二次命中) + 年轮脉冲 +
/// 繁茂领域 (2.5s, 站内玩家每秒回 1 HP)。
/// </summary>
public class NatureGrimoire : ModItem
{
    private const int PagesPerBloom = 5;
    private const float BloomMaxRange = 440f;

    // 页数状态 (仅 owner 端 Shoot 消费)
    internal int pageCount;

    /// <summary>普通施法叶片类型 (赤铜升级覆写)。</summary>
    protected virtual int LeafType => ModContent.ProjectileType<NatureGrimoireLeaf>();
    /// <summary>荣枯页主题: 叶环/领域 0=自然, 1=赤铜燎原。</summary>
    protected virtual int BloomTheme => 0;
    /// <summary>年轮脉冲主题 (2=秘典嫩绿金, 1=赤铜橙焰)。</summary>
    protected virtual int PulseTheme => 2;
    protected virtual int PageDustType => DustID.GreenTorch;

    public override void SetDefaults() {
        Item.damage = 13;
        Item.crit = 6;
        Item.DamageType = DamageClass.Magic;
        Item.width = 28;
        Item.height = 32;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 2.5f;
        Item.value = Item.buyPrice(silver: 55);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<NatureGrimoireLeaf>();
        Item.shootSpeed = 8f;
        Item.mana = 8;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        if (pageCount < PagesPerBloom - 1) {
            pageCount++;
            // 常规页: 扇形 3 叶
            float spreadAngle = MathHelper.ToRadians(18);
            for (int i = -1; i <= 1; i++) {
                Vector2 leafVel = velocity.RotatedBy(spreadAngle * i) * Main.rand.NextFloat(0.9f, 1.1f);
                Projectile.NewProjectile(source, position, leafVel, LeafType, damage, knockback, player.whoAmI,
                    ai0: Main.rand.NextFloat(MathHelper.TwoPi));
            }
            // 翻页音随页数升调 (韵律可读)
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.4f, Pitch = 0.1f + pageCount * 0.12f }, position);
            return false;
        }

        // ===== 第 5 页: 荣枯页 =====
        pageCount = 0;

        // 环心 = 指向点 (限距 + 粗略视线检查, 不隔墙施放)
        Vector2 target = Main.MouseWorld;
        Vector2 toTarget = target - player.Center;
        if (toTarget.Length() > BloomMaxRange)
            target = player.Center + toTarget.SafeNormalize(Vector2.UnitX) * BloomMaxRange;
        Vector2 center = player.Center;
        for (int i = 1; i <= 16; i++) {
            Vector2 probe = Vector2.Lerp(player.Center, target, i / 16f);
            if (!Collision.CanHitLine(player.Center, 1, 1, probe, 1, 1))
                break;
            center = probe;
        }

        // 8 叶生命之环 (螺旋展开→收拢)
        for (int i = 0; i < 8; i++) {
            Projectile.NewProjectile(source, center, Vector2.Zero,
                ModContent.ProjectileType<GrimoireBloomLeaf>(), damage, knockback, player.whoAmI,
                ai0: i, ai1: BloomTheme);
        }
        // 年轮脉冲 + 繁茂领域
        Projectile.NewProjectile(source, center, Vector2.Zero,
            ModContent.ProjectileType<WoodlandVerdantPulseVFX>(), 0, 0f, player.whoAmI, ai0: PulseTheme);
        Projectile.NewProjectile(source, center, Vector2.Zero,
            ModContent.ProjectileType<VerdantFieldProj>(), 0, 0f, player.whoAmI, ai0: BloomTheme);

        ACMWeaponBurst.Spawn(source, center, BloomTheme == 1 ? ACMWeaponBurst.CupriteBurn : ACMWeaponBurst.Nature,
            1.8f, player.whoAmI);
        WeaponVFX.AddScreenShake(center, 5f);
        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = 0.15f }, center);
        SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = -0.3f }, player.Center);
        return false;
    }

    public override void HoldItem(Player player) {
        // 书周绿萤 = 已翻页数 (大招充能可读)
        if (Main.dedServ || pageCount <= 0 || !Main.rand.NextBool(5))
            return;
        for (int i = 0; i < pageCount; i++) {
            float ang = Main.GlobalTimeWrappedHourly * 2f + i * MathHelper.TwoPi / 4f;
            Vector2 pos = player.Center + ang.ToRotationVector2() * 24f;
            Dust d = Dust.NewDustPerfect(pos, PageDustType, new Vector2(0, -0.4f), 140, default, 0.7f);
            d.noGravity = true;
        }
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Book, 1)
            .AddIngredient(ItemID.JungleSpores, 5)
            .AddIngredient(ItemID.Vine, 2)
            .AddIngredient(ItemID.FallenStar, 3)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

/// <summary>
/// 自然秘典叶片 - 飘飞旋转的树叶弹幕 (常规页)。微追踪提高前期命中率。
/// </summary>
public class NatureGrimoireLeaf : ModProjectile
{
    public override string Texture
        => $"Terraria/Images/Projectile_{ProjectileID.Leaf}";

    /// <summary>命中演出主题 (赤铜升级覆写)。</summary>
    protected virtual int HitBurstTheme => ACMWeaponBurst.Nature;

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 5;
    }

    public override void SetDefaults() {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 90;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        // 五帧叶片动画
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 5) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % 5;
        }

        Projectile.ai[1]++;

        // 叶子自然飘动 - 正弦波横向偏移
        float wave = MathF.Sin(Projectile.ai[0] + Projectile.ai[1] * 0.12f) * 0.6f;
        Vector2 perpendicular = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X).SafeNormalize(Vector2.Zero);
        Projectile.position += perpendicular * wave;

        // 微追踪 (只轻推转向, 保留飘叶感)
        if (Projectile.ai[1] < 60f) {
            NPC nearest = null;
            float bestDist = 320f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile))
                    continue;
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    nearest = npc;
                }
            }
            if (nearest != null) {
                Vector2 desired = (nearest.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Projectile.velocity.Length();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.03f);
            }
        }

        // 叶子旋转
        Projectile.rotation += Projectile.velocity.X * 0.04f;

        // 轻微减速 + 受重力影响，模拟叶片飘落感
        if (Projectile.ai[1] > 30f) {
            Projectile.velocity.Y += 0.02f;
        }

        Lighting.AddLight(Projectile.Center, 0.05f, 0.12f, 0.03f);

        // 偶尔飘落小叶片粒子
        if (Main.rand.NextBool(6)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Grass,
                Main.rand.NextVector2Circular(0.5f, 0.5f), 80, default, 0.8f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 叶片飘飞时残留翠绿柔光 (毒/自然能量感), 保留原版叶片帧绘制
        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.32f, new Color(80, 200, 90) * 0.7f);
        return true;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        bool poisoned = Main.rand.NextBool(3);
        if (poisoned) {
            target.AddBuff(BuffID.Poisoned, 90);
        }
        // 命中时散落叶片
        for (int i = 0; i < 5; i++) {
            int dustType = Main.rand.NextBool() ? DustID.Grass : DustID.GrassBlades;
            Dust d = Dust.NewDustPerfect(target.Center, dustType,
                Main.rand.NextVector2Circular(3f, 3f), 60, default, 1.2f);
            d.noGravity = true;
        }
        // 中毒触发时额外飘几缕翠尘强调
        if (poisoned) {
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.GreenTorch,
                    Main.rand.NextVector2Circular(2f, 2f), 80, default, 0.9f);
                d.noGravity = true;
            }
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            HitBurstTheme, scale: 0.7f, owner: Projectile.owner);
    }

    public override void OnKill(int timeLeft) {
        // 消散时散落叶片碎屑
        for (int i = 0; i < 6; i++) {
            int dustType = Main.rand.NextBool() ? DustID.Grass : DustID.GrassBlades;
            Dust d = Dust.NewDustPerfect(Projectile.Center, dustType,
                Main.rand.NextVector2CircularEdge(2f, 2f), 40, default, 1f);
            d.noGravity = true;
        }
    }
}

/// <summary>
/// 荣枯页叶刃 - 大招的 8 片环形叶: 螺旋展开成环 → 悬停 → 收拢回心 (二次命中窗口)。
/// ai[0] = 环位序号 (0..7); ai[1] = 主题 (0=自然, 1=赤铜焰叶)。
/// </summary>
public class GrimoireBloomLeaf : ModProjectile
{
    public override string Texture
        => $"Terraria/Images/Projectile_{ProjectileID.Leaf}";

    private const int LifeTime = 54;
    private const float RingRadius = 150f;

    private bool Cuprite => Projectile.ai[1] == 1f;

    private Vector2 _center;
    private bool _centerSet;

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 5;
        ProjectileID.Sets.TrailCacheLength[Type] = 8;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.GrimoireBloomLeaf.DisplayName", () => "荣枯叶刃");
    }

    public override void SetDefaults() {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = LifeTime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 24; // 展开与收拢各可命中一次
    }

    public override void AI() {
        if (!_centerSet) {
            _center = Projectile.Center;
            _centerSet = true;
        }

        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 4) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % 5;
        }

        float t = 1f - Projectile.timeLeft / (float)LifeTime;
        float baseAng = Projectile.ai[0] * MathHelper.TwoPi / 8f;

        // 展开 [0,0.45] → 悬停 [0.45,0.62] → 收拢 [0.62,1]
        float radius, spiral;
        if (t < 0.45f) {
            float e = t / 0.45f;
            e = 1f - MathF.Pow(1f - e, 3f); // ease-out cubic
            radius = RingRadius * e;
            spiral = 1.4f * e;
        }
        else if (t < 0.62f) {
            radius = RingRadius;
            spiral = 1.4f + (t - 0.45f) * 1.2f;
        }
        else {
            float e = (t - 0.62f) / 0.38f;
            radius = RingRadius * (1f - e * e); // ease-in 收拢
            spiral = 1.4f + 0.204f + e * 2.2f;
        }

        float ang = baseAng + spiral;
        Vector2 prev = Projectile.Center;
        Projectile.Center = _center + ang.ToRotationVector2() * radius;
        Projectile.velocity = Vector2.Zero;
        Projectile.rotation = (Projectile.Center - prev).ToRotation() + MathHelper.PiOver2;

        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center,
                Cuprite ? DustID.Torch : DustID.GrassBlades,
                Main.rand.NextVector2Circular(0.8f, 0.8f), 80, default, 1f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, Cuprite ? new Vector3(0.3f, 0.15f, 0.04f) : new Vector3(0.08f, 0.2f, 0.06f));
    }

    public override bool ShouldUpdatePosition() => false;

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        if (Cuprite)
            target.AddBuff(BuffID.OnFire, 120);
        else if (Main.rand.NextBool(3))
            target.AddBuff(BuffID.Poisoned, 120);
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            Cuprite ? ACMWeaponBurst.CupriteBurn : ACMWeaponBurst.Nature, 0.7f, Projectile.owner);
    }

    public override void OnKill(int timeLeft) {
        for (int i = 0; i < 4; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center,
                Cuprite ? DustID.Torch : DustID.Grass,
                Main.rand.NextVector2Circular(2f, 2f), 60, default, 1f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        Color outer = Cuprite ? new Color(190, 60, 20, 140) : new Color(45, 150, 60, 140);
        Color inner = Cuprite ? new Color(255, 200, 110, 200) : new Color(200, 255, 160, 200);
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 8f, outer, inner,
            uvScroll: -Main.GlobalTimeWrappedHourly * 2f);
        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.3f, inner * 0.7f);
        return true;
    }
}

/// <summary>
/// 繁茂领域 - 荣枯页环心留下的自然领域 (2.5s)。
/// ai[0] = 0: 站内玩家每秒回 1 HP (各客户端只处理自己); 1: 赤铜余烬领域, 持续点燃范围内敌人。
/// </summary>
public class VerdantFieldProj : ModProjectile
{
    public override string Texture => "InnoVault/Assets/placeholder";

    private const int LifeTime = 150;
    private const float Radius = 130f;

    private bool Cuprite => Projectile.ai[0] == 1f;

    public override void SetStaticDefaults() {
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.VerdantFieldProj.DisplayName", () => "繁茂领域");
    }

    public override void SetDefaults() {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.friendly = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = LifeTime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        int age = LifeTime - Projectile.timeLeft;

        if (!Cuprite) {
            // 微再生: 各客户端只处理自己的本地玩家 (MP 安全)
            if (!Main.dedServ && age > 0 && age % 60 == 0) {
                Player lp = Main.LocalPlayer;
                if (lp.active && !lp.dead && lp.statLife < lp.statLifeMax2 &&
                    Vector2.DistanceSquared(lp.Center, Projectile.Center) < Radius * Radius) {
                    lp.Heal(1);
                }
            }
            // 领域草花萤光
            if (Main.rand.NextBool(2)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(Radius, Radius * 0.6f);
                Dust d = Dust.NewDustPerfect(pos, Main.rand.NextBool(3) ? DustID.GreenTorch : DustID.GrassBlades,
                    new Vector2(0, -Main.rand.NextFloat(0.3f, 1f)), 110, default, Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = true;
            }
        }
        else {
            // 余烬领域: 每 0.5s 点燃范围内敌人 (owner 端驱动, AddBuff 内部走 MP 同步)
            if (Projectile.owner == Main.myPlayer && age % 30 == 0) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.friendly || npc.dontTakeDamage || !npc.CanBeChasedBy(Projectile))
                        continue;
                    if (Vector2.DistanceSquared(npc.Center, Projectile.Center) < Radius * Radius)
                        npc.AddBuff(BuffID.OnFire, 90);
                }
            }
            if (Main.rand.NextBool(2)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(Radius, Radius * 0.6f);
                Dust d = Dust.NewDustPerfect(pos, DustID.Torch,
                    new Vector2(0, -Main.rand.NextFloat(0.5f, 1.5f)), 90, default, Main.rand.NextFloat(0.8f, 1.3f));
                d.noGravity = true;
            }
        }

        Lighting.AddLight(Projectile.Center, Cuprite ? new Vector3(0.4f, 0.2f, 0.05f) : new Vector3(0.1f, 0.3f, 0.12f));
    }

    public override bool PreDraw(ref Color lightColor) {
        if (Main.dedServ)
            return false;
        float life = 1f - Projectile.timeLeft / (float)LifeTime;
        // 淡入淡出包络
        float env = MathHelper.Clamp(life / 0.15f, 0f, 1f) * MathHelper.Clamp((1f - life) / 0.25f, 0f, 1f);
        Color inner = Cuprite ? new Color(255, 190, 100) : new Color(180, 255, 150);
        Color outer = Cuprite ? new Color(190, 70, 20) : new Color(50, 160, 70);

        // 呼吸环 (边界可读) + 中心柔光
        float breath = 1f + 0.04f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4f);
        WeaponVFX.DrawShockwaveRing(Projectile.Center, Radius * breath, 7f, env * 0.4f, inner, outer);
        WeaponVFX.DrawGlowBurst(Projectile.Center, 2.2f, (Cuprite ? new Color(255, 150, 60) : new Color(110, 230, 110)) * (env * 0.35f));
        return false;
    }
}
