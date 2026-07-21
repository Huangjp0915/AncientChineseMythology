using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands;

/// <summary>
/// 藤蔓猎弓 - 射手弓 (重做: 藤矢标记—藤鞭收割)
/// 每第 4 支箭化为"藤矢" (粗藤拖尾), 命中种下藤蔓标记 (6s);
/// 后续任意本弓箭命中带标记目标 → 从其脚下抽出藤鞭收割 (50% 伤害) 并消耗标记。
/// </summary>
public class VineHunterBow : ModItem
{
    /// <summary>第几支箭是标记矢。</summary>
    protected const int MarkPeriod = 4;

    // 箭计数 (仅 owner 端 Shoot 消费)
    internal int shotCounter;

    protected virtual int BowDustType => DustID.Grass;

    public override void SetDefaults() {
        Item.damage = 11;
        Item.crit = 6;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 18;
        Item.height = 52;
        Item.useTime = 26;
        Item.useAnimation = 26;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 2f;
        Item.value = Item.buyPrice(silver: 30);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = SoundID.Item5;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.shoot = ProjectileID.WoodenArrowFriendly;
        Item.shootSpeed = 8f;
        Item.useAmmo = AmmoID.Arrow;
    }

    public override Vector2? HoldoutOffset() {
        return new Vector2(-2, 0);
    }

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
        velocity = velocity.RotatedByRandom(MathHelper.ToRadians(2));
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        shotCounter++;
        bool vineShot = shotCounter >= MarkPeriod;
        if (vineShot)
            shotCounter = 0;

        int proj = Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
        if (proj >= 0 && proj < Main.maxProjectiles)
            Main.projectile[proj].GetGlobalProjectile<VineHunterBowGlobalProj>().vineShot = vineShot;

        // 发射时藤蔓粒子; 藤矢出弦音更亮 + 粒子更多
        Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
        int dustN = vineShot ? 8 : 3;
        for (int i = 0; i < dustN; i++) {
            Vector2 dustVel = muzzleDir.RotatedByRandom(0.4f) * Main.rand.NextFloat(1f, 3f);
            Dust d = Dust.NewDustPerfect(position + muzzleDir * 20f, BowDustType, dustVel, 80, default, 1f);
            d.noGravity = true;
        }
        if (vineShot)
            SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.7f, Pitch = 0.25f }, position);
        return false;
    }

    public override void HoldItem(Player player) {
        // 计数可读: 弓身翠光随计数渐盛 (第 3 发后明显)
        if (Main.dedServ || shotCounter <= 0)
            return;
        if (Main.rand.NextBool(9 - shotCounter * 2)) {
            Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(16f, 24f),
                BowDustType, new Vector2(0, -Main.rand.NextFloat(0.4f, 1f)), 100, default, 0.9f);
            d.noGravity = true;
        }
    }

    // ============ 箭命中回调 (由 GlobalProjectile 转发, 玄铁升级覆写) ============

    /// <summary>
    /// 本弓箭矢命中: 标记种植/收割逻辑 (仅 owner 端调用)。
    /// </summary>
    internal virtual void OnArrowHit(Projectile arrow, NPC target, bool vineShot) {
        if (!target.active || target.friendly || target.lifeMax <= 5)
            return;

        var mark = target.GetGlobalNPC<VineMarkGlobalNPC>();
        if (vineShot) {
            // 藤矢: 种藤 + 必中毒
            mark.markTimer = VineMarkGlobalNPC.MarkDuration;
            mark.markTheme = 0;
            target.AddBuff(BuffID.Poisoned, 150);
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = 0.3f }, target.Center);
        }
        else {
            if (Main.rand.NextBool(4))
                target.AddBuff(BuffID.Poisoned, 90);
            if (mark.markTimer > 0) {
                // 收割: 消耗标记, 脚下抽藤鞭
                mark.markTimer = 0;
                int whipDamage = Math.Max((int)(arrow.damage * 0.5f), 1);
                Projectile.NewProjectile(arrow.GetSource_OnHit(target),
                    WoodlandSwing.FindGround(target.Bottom), Vector2.Zero,
                    ModContent.ProjectileType<VineWhipLash>(), whipDamage, 3f, arrow.owner);
                target.AddBuff(BuffID.Poisoned, 150);
            }
        }
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 12)
            .AddIngredient(ItemID.Vine, 3)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

/// <summary>
/// 藤蔓猎弓全局弹幕 - 记录箭矢来源弓与藤矢标志, 绘制主题拖尾, 命中转发给弓的标记逻辑。
/// </summary>
public class VineHunterBowGlobalProj : GlobalProjectile
{
    public override bool InstancePerEntity => true;

    /// <summary>发射本箭的猎弓 (含玄铁升级); null = 非本系弓所发。</summary>
    private VineHunterBow _bowItem;
    /// <summary>本箭是否为标记矢 (第 4 发), 由弓在生成后立刻设置 (owner 端)。</summary>
    internal bool vineShot;

    private Vector2[] _history;
    private int _histCount;

    public override void OnSpawn(Projectile projectile, IEntitySource source) {
        if (source is EntitySource_ItemUse itemSource && itemSource.Item?.ModItem is VineHunterBow bow)
            _bowItem = bow;
    }

    public override void AI(Projectile projectile) {
        if (_bowItem == null)
            return;

        // 记录最近轨迹点 (头→尾), 供 ribbon 拖尾使用
        _history ??= new Vector2[12];
        for (int i = _history.Length - 1; i > 0; i--)
            _history[i] = _history[i - 1];
        _history[0] = projectile.Center;
        if (_histCount < _history.Length)
            _histCount++;

        // 藤矢粒子更浓 (可读)
        if (Main.rand.NextBool(vineShot ? 2 : 3)) {
            Dust d = Dust.NewDustPerfect(projectile.Center, _bowItem is Upgrades.XuanTieHunterBow ? DustID.Blood : DustID.Grass,
                -projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                90, default, vineShot ? 1.1f : 0.85f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(Projectile projectile, ref Color lightColor) {
        if (_bowItem != null && _histCount >= 2) {
            Vector2[] pts = new Vector2[_histCount];
            Array.Copy(_history, pts, _histCount);
            bool xuantie = _bowItem is Upgrades.XuanTieHunterBow;
            Color outer = xuantie ? new Color(90, 95, 110, 150) : new Color(40, 140, 50, 150);
            Color inner = xuantie ? new Color(190, 40, 40, 200) : new Color(150, 240, 120, 200);
            // 藤矢: 粗藤拖尾 (身份可读)
            WeaponVFX.DrawRibbonTrail(pts, baseWidth: vineShot ? 10f : 6f,
                outerColor: outer, innerColor: inner,
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);
        }
        return true; // 保留箭矢贴图
    }

    public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
        if (_bowItem == null)
            return;
        if (projectile.owner == Main.myPlayer)
            _bowItem.OnArrowHit(projectile, target, vineShot);
        ACMWeaponBurst.Spawn(projectile.GetSource_OnHit(target), target.Center,
            _bowItem is Upgrades.XuanTieHunterBow ? ACMWeaponBurst.XuanTieBleed : ACMWeaponBurst.Nature,
            scale: vineShot ? 0.9f : 0.6f, owner: projectile.owner);
    }
}

/// <summary>
/// 藤蔓标记 GlobalNPC - 目标被藤矢种藤的窗口计时 (owner 端状态) + 脚边藤叶视觉。
/// </summary>
public class VineMarkGlobalNPC : GlobalNPC
{
    public const int MarkDuration = 360; // 6s

    public override bool InstancePerEntity => true;

    public int markTimer;
    /// <summary>标记主题: 0=藤蔓翠绿, 1=玄铁血红。</summary>
    public int markTheme;

    public override void PostAI(NPC npc) {
        if (markTimer > 0)
            markTimer--;
    }

    public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
        if (markTimer <= 0 || Main.dedServ)
            return;
        Texture2D star = ACMAsset.BlankStar;
        if (star == null)
            return;

        // 脚边两簇缠藤叶 (摆动), 快过期时闪烁
        float fade = markTimer < 60 ? (markTimer / 8 % 2 == 0 ? 0.4f : 1f) : 1f;
        Color markColor = markTheme == 1 ? new Color(230, 60, 60, 0) : new Color(110, 220, 90, 0);
        for (int i = 0; i < 2; i++) {
            float side = i == 0 ? -1f : 1f;
            float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * 4f + i * 2f) * 4f;
            Vector2 pos = npc.Bottom + new Vector2(side * (npc.width * 0.4f + 4f) + sway, -6f);
            spriteBatch.Draw(star, pos - screenPos, null, markColor * (0.8f * fade),
                sway * 0.1f, star.Size() * 0.5f, 0.12f, SpriteEffects.None, 0f);
        }
    }
}

/// <summary>
/// 藤鞭收割 - 从标记目标脚下地面抽出的藤鞭 (程序化 S 形 ribbon, 无贴图)。
/// 6f 破土预告 → 8f 抽打判定窗口 → 消散。
/// </summary>
public class VineWhipLash : ModProjectile
{
    public override string Texture => "InnoVault/Assets/placeholder";

    private const int LifeTime = 26;
    private const float WhipHeight = 96f;

    private ref float Timer => ref Projectile.localAI[0];

    public override void SetStaticDefaults() {
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.VineWhipLash.DisplayName", () => "藤鞭");
    }

    public override void SetDefaults() {
        Projectile.width = 40;
        Projectile.height = 96;
        Projectile.friendly = false;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = LifeTime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = LifeTime;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Timer++;
        if (Timer == 1f)
            Projectile.Center -= new Vector2(0, Projectile.height * 0.5f); // 生成点为地表 → 底部锚地

        if (Timer < 6f) {
            // 破土预告: 叶屑翻涌
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-10f, 10f), 0),
                    DustID.GrassBlades, new Vector2(0, -Main.rand.NextFloat(1f, 3f)), 60, default, 1f);
                d.noGravity = false;
            }
            return;
        }

        if (Timer == 6f) {
            SoundEngine.PlaySound(SoundID.Item65 with { Volume = 0.6f, Pitch = 0.4f }, Projectile.Bottom);
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = -0.2f }, Projectile.Bottom);
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Bottom, DustID.Grass,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(3f, 7f)), 40, default, 1.3f);
                d.noGravity = Main.rand.NextBool();
            }
        }

        // 判定窗口 [6, 14]
        Projectile.friendly = Timer is >= 6f and <= 14f;
        Lighting.AddLight(Projectile.Center, 0.08f, 0.22f, 0.06f);
    }

    private float Growth {
        get {
            float t = Timer - 6f;
            if (t <= 0f) return 0f;
            return 1f - MathF.Pow(1f - MathHelper.Clamp(t / 6f, 0f, 1f), 4f);
        }
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float h = WhipHeight * Growth;
        var rect = new Rectangle((int)(Projectile.Bottom.X - 20f), (int)(Projectile.Bottom.Y - h), 40, (int)h);
        return rect.Intersects(targetHitbox);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.Nature, 1f, Projectile.owner);
        WeaponVFX.AddScreenShake(target.Center, 1.5f);
    }

    public override bool PreDraw(ref Color lightColor) {
        float growth = Growth;
        if (growth <= 0.01f)
            return false;

        // S 形藤鞭中心线 (9 点), 鞭梢随时间甩动
        float t = Timer - 6f;
        var pts = new Vector2[9];
        float whipSway = MathF.Sin(t * 0.5f) * 10f;
        for (int i = 0; i < 9; i++) {
            float f = i / 8f; // 0=底 1=梢
            float x = MathF.Sin(f * MathHelper.Pi * 1.5f + t * 0.35f) * (7f + 11f * f) + whipSway * f * f;
            pts[i] = Projectile.Bottom + new Vector2(x, -WhipHeight * growth * f);
        }
        // 底→梢: 外深绿 + 内嫩绿双层
        float alpha = Timer > 16f ? 1f - (Timer - 16f) / 10f : 1f;
        WeaponVFX.DrawRibbonTrail(pts, 9f,
            new Color(45, 130, 40, (int)(170 * alpha)), new Color(170, 250, 130, (int)(210 * alpha)),
            uvScroll: -Main.GlobalTimeWrappedHourly * 2f, subdivisions: 3);
        return false;
    }
}
