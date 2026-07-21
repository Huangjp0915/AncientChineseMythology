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
/// 枯木火铳 - 射手火铳 (重做: 三发弹仓)
/// 不消耗弹药。第 1、2 发单橡子; 第 3 发"霰爆": 前摇更长, 一次喷出 4 颗小橡子
/// 扇形 + 后坐力 + 大枪口焰, 弹仓重置。橡子碎裂产生实体木壳弹片。
/// </summary>
public class DeadwoodMusket : ModItem
{
    /// <summary>弹仓容量 (第 MagSize 发为霰爆)。</summary>
    protected const int MagSize = 3;

    // 弹仓状态 (仅 owner 端 Shoot 消费)
    internal int shotIndex;
    private uint _lastShotTime;

    /// <summary>是否即将霰爆 (供 UseTime 乘数与枪口视觉)。</summary>
    protected bool BurstNext => shotIndex >= MagSize - 1;

    /// <summary>单发橡子弹幕 (赤铜升级覆写)。</summary>
    protected virtual int AcornType => ModContent.ProjectileType<DeadwoodAcornProj>();
    /// <summary>霰爆小橡子弹幕 (赤铜升级覆写)。</summary>
    protected virtual int PelletType => ModContent.ProjectileType<DeadwoodPellet>();
    protected virtual int MuzzleDustType => DustID.Smoke;

    public override void SetDefaults() {
        Item.damage = 14;
        Item.crit = 4;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 44;
        Item.height = 20;
        Item.useTime = 28;
        Item.useAnimation = 28;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 4f;
        Item.value = Item.buyPrice(silver: 45);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = null; // 分层手动播放
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<DeadwoodAcornProj>();
        Item.shootSpeed = 9.5f;
    }

    public override Vector2? HoldoutOffset() {
        return new Vector2(-6, 2);
    }

    // 霰爆发: 前摇更长 (36f), 普通发 28f
    public override float UseTimeMultiplier(Player player) => BurstNext ? 36f / 28f : 1f;
    public override float UseAnimationMultiplier(Player player) => BurstNext ? 36f / 28f : 1f;

    public override bool CanUseItem(Player player) {
        // 长时间不开火弹仓自动复位 (5s)
        if (_lastShotTime != 0 && Main.GameUpdateCount - _lastShotTime > 300)
            shotIndex = 0;
        return true;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Vector2 muzzleDir = velocity.SafeNormalize(Vector2.UnitX);
        Vector2 muzzlePos = position + muzzleDir * 30f;
        bool burst = BurstNext;
        _lastShotTime = Main.GameUpdateCount;
        shotIndex = burst ? 0 : shotIndex + 1;

        if (!burst) {
            // 单发橡子
            Vector2 perturbedVel = velocity.RotatedByRandom(MathHelper.ToRadians(3));
            Projectile.NewProjectile(source, muzzlePos, perturbedVel, AcornType, damage, knockback, player.whoAmI);
            SoundEngine.PlaySound(SoundID.Item11 with { Volume = 0.8f, Pitch = 0.1f + shotIndex * 0.08f + Main.rand.NextFloat(-0.05f, 0.05f) }, muzzlePos);
        }
        else {
            // 霰爆: 4 颗小橡子, 8° 扇形, 每颗 40%
            int pelletDamage = Math.Max((int)(damage * 0.4f), 1);
            for (int i = 0; i < 4; i++) {
                float spread = MathHelper.Lerp(-0.14f, 0.14f, i / 3f) + Main.rand.NextFloat(-0.02f, 0.02f);
                Vector2 vel = velocity.RotatedBy(spread) * Main.rand.NextFloat(0.92f, 1.15f);
                Projectile.NewProjectile(source, muzzlePos, vel, PelletType, pelletDamage, knockback * 0.6f, player.whoAmI);
            }

            // 后坐: 推动玩家 + 枪口焰 + 震屏 (霰爆的重量感)
            player.velocity -= muzzleDir * 2.5f;
            WeaponVFX.AddScreenShake(player.Center, 2f);
            SoundEngine.PlaySound(SoundID.Item36 with { Volume = 0.9f, Pitch = -0.15f }, muzzlePos);
            SoundEngine.PlaySound(SoundID.Item11 with { Volume = 0.6f, Pitch = -0.4f }, muzzlePos);
            for (int i = 0; i < 10; i++) {
                Vector2 dustVel = muzzleDir.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 6f);
                Dust d = Dust.NewDustPerfect(muzzlePos, Main.rand.NextBool(3) ? MuzzleDustType : DustID.WoodFurniture,
                    dustVel, 70, default, Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = true;
            }
        }

        // 枪口烟量随弹仓消耗增加 (弹仓可读性)
        int smoke = 2 + shotIndex * 2;
        for (int i = 0; i < smoke; i++) {
            Vector2 dustVel = -muzzleDir.RotatedByRandom(0.4f) * Main.rand.NextFloat(1f, 3f);
            Dust d = Dust.NewDustPerfect(muzzlePos, MuzzleDustType, dustVel, 110, default, 0.9f);
            d.noGravity = true;
        }
        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 18)
            .AddIngredient(ItemID.Acorn, 5)
            .AddIngredient(ItemID.FallenStar, 1)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

/// <summary>
/// 橡子弹丸 - 枯木火铳单发弹。碰撞/命中时碎裂为 2 片木壳弹片 (30% 伤害实弹)。
/// </summary>
public class DeadwoodAcornProj : ModProjectile
{
    public override string Texture
        => $"Terraria/Images/Item_{ItemID.Acorn}";

    /// <summary>是否碎裂出木壳弹片 (霰爆小橡子关闭, 防连锁弹幕膨胀)。</summary>
    protected virtual bool SplinterOnDeath => true;
    /// <summary>命中演出主题 (赤铜升级覆写)。</summary>
    protected virtual int HitBurstTheme => ACMWeaponBurst.Nature;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
        ProjectileID.Sets.TrailingMode[Type] = 0;
    }

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 120;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        Projectile.rotation += Projectile.velocity.X * 0.05f;
        Projectile.velocity.Y += 0.08f; // 轻微重力

        if (Main.rand.NextBool(5)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WoodFurniture,
                -Projectile.velocity * 0.1f, 60, default, 0.8f);
            d.noGravity = true;
        }
    }

    public override void OnKill(int timeLeft) {
        // 碎裂: 音效 + 木屑 + 实体木壳弹片
        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = 0.5f }, Projectile.Center);
        for (int i = 0; i < 8; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WoodFurniture,
                vel, 40, default, 1.2f);
            d.noGravity = false;
        }

        if (SplinterOnDeath && Projectile.owner == Main.myPlayer) {
            int shardDamage = Math.Max((int)(Projectile.damage * 0.3f), 1);
            for (int i = 0; i < 2; i++) {
                // 沿入射方向的反射锥飞散 (撞墙向外弹, 命中穿过)
                Vector2 baseDir = (-Projectile.velocity).SafeNormalize(-Vector2.UnitY);
                Vector2 vel = baseDir.RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(3f, 5.5f)
                    + new Vector2(0, -1.5f);
                Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, vel,
                    ModContent.ProjectileType<DeadwoodShard>(), shardDamage, 0.5f, Projectile.owner);
            }
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        for (int i = 0; i < 5; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.WoodFurniture,
                Main.rand.NextVector2Circular(3f, 3f), 60, default, 1f);
            d.noGravity = false;
        }
        // 橡子碎裂命中演出 (偏小)
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            HitBurstTheme, scale: 0.7f, owner: Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        // 橡子暖芯双层拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 6f,
            outerColor: new Color(90, 70, 30, 150), innerColor: new Color(210, 180, 110, 200),
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);
        // 枪口柔光闪 (仅出膛瞬间)
        if (Projectile.timeLeft > 116)
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.6f, new Color(230, 200, 130));
        return true;
    }
}

/// <summary>
/// 霰爆小橡子 - 第 3 发喷出的散射弹。继承橡子机制但更小更轻, 不再二次碎裂。
/// </summary>
public class DeadwoodPellet : DeadwoodAcornProj
{
    protected override bool SplinterOnDeath => false;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[Type] = 10;
        ProjectileID.Sets.TrailingMode[Type] = 0;
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.DeadwoodPellet.DisplayName", () => "橡子霰弹");
    }

    public override void SetDefaults() {
        base.SetDefaults();
        Projectile.width = 6;
        Projectile.height = 6;
        Projectile.scale = 0.6f;
        Projectile.timeLeft = 60;
    }
}

/// <summary>
/// 木壳弹片 - 橡子碎裂的实体弹片 (30% 伤害, 短寿命弧线)。
/// </summary>
public class DeadwoodShard : ModProjectile
{
    public override string Texture => "InnoVault/Assets/placeholder";

    public override void SetStaticDefaults() {
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.DeadwoodShard.DisplayName", () => "木壳弹片");
    }

    public override void SetDefaults() {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 28;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        Projectile.velocity.Y += 0.22f;
        Projectile.rotation += 0.4f * MathF.Sign(Projectile.velocity.X);
        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WoodFurniture,
                -Projectile.velocity * 0.15f, 60, default, 0.8f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        for (int i = 0; i < 3; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.WoodFurniture,
                Main.rand.NextVector2Circular(2f, 2f), 60, default, 0.9f);
            d.noGravity = false;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 无贴图: 程序化小木片 (Sparkle 遮罩染木色)
        var tex = ACMAsset.Sparkle;
        if (tex == null)
            return false;
        Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null,
            new Color(160, 125, 70, 0), Projectile.rotation, tex.Size() * 0.5f, 0.12f,
            Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
        return false;
    }
}
