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
/// 苔藓爆弹 - 投掷物 (重做: 孢子二段)
/// 撞地弹跳一次并点燃 22 帧短引信 (脉冲闪烁加速), 引信到时/二次碰撞才爆;
/// 直接砸中敌人立即爆 (奖励直击)。爆后弹出 3 颗孢子芽, 落地生成小孢子云。
/// </summary>
public class MossBomb : ModItem
{
    /// <summary>投掷弹幕类型 (赤铜升级覆写)。</summary>
    protected virtual int BombType => ModContent.ProjectileType<MossBombProj>();

    public override void SetDefaults() {
        Item.damage = 20;
        Item.crit = 4;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 24;
        Item.height = 24;
        Item.useTime = 35;
        Item.useAnimation = 35;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 5f;
        Item.value = Item.buyPrice(silver: 60);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<MossBombProj>();
        Item.shootSpeed = 10f;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        // 稍微上抛
        Vector2 launchVel = velocity + new Vector2(0, -2f);
        Projectile.NewProjectile(source, position, launchVel, BombType, damage, knockback, player.whoAmI);
        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 10)
            .AddIngredient(ItemID.GlowingMushroom, 10)
            .AddIngredient(ItemID.Gel, 15)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

/// <summary>
/// 苔藓爆弹弹幕 - 弧线飞行; 撞地弹跳 + 短引信, 二次碰撞/引信到时/命中敌人爆炸。
/// ai[0] = 飞行计时; ai[1] = 防重复爆标志; ai[2] = 已弹跳标志。
/// </summary>
public class MossBombProj : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/Woodlands/MossBomb";

    private const int FuseFrames = 22;

    private ref float AiTimer => ref Projectile.ai[0];
    private ref float Bounced => ref Projectile.ai[2];
    /// <summary>弹跳后引信倒计时 (各端由碰撞独立驱动, 结果一致)。</summary>
    private ref float Fuse => ref Projectile.localAI[0];

    /// <summary>孢子芽主题 (0=自然孢子, 1=赤铜火孢; 赤铜升级覆写)。</summary>
    protected virtual int SporeTheme => 0;

    public override void SetDefaults() {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        AiTimer++;
        Projectile.velocity.Y += 0.20f; // 重力
        Projectile.rotation += Projectile.velocity.X * 0.04f;

        // 弹跳后: 引信倒计时
        if (Bounced == 1f) {
            Fuse++;
            if (Fuse >= FuseFrames) {
                Projectile.Kill();
                return;
            }
            // 引信滋滋声 (低频提示)
            if ((int)Fuse % 7 == 0)
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.25f, Pitch = 0.6f + Fuse / FuseFrames * 0.3f }, Projectile.Center);
        }

        // 飞行粒子
        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                -Projectile.velocity * 0.05f, 80, default, 0.8f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.1f, 0.3f, 0.1f);
    }

    public override bool OnTileCollide(Vector2 oldVelocity) {
        if (Bounced == 0f) {
            // 第一次撞地: 弹起 + 引信点燃
            Bounced = 1f;
            if (Projectile.velocity.X != oldVelocity.X)
                Projectile.velocity.X = -oldVelocity.X * 0.45f;
            if (Projectile.velocity.Y != oldVelocity.Y)
                Projectile.velocity.Y = -oldVelocity.Y * 0.55f;
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Bottom, DustID.Grass,
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 2.5f)), 60, default, 1f);
                d.noGravity = false;
            }
            return false; // 不销毁
        }
        return true; // 二次碰撞 → 爆
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        // 直击敌人立即爆 (penetrate=1, 命中后 Kill → OnKill → Explode)
    }

    public override void OnKill(int timeLeft) {
        Explode();
    }

    /// <summary>爆炸: 蘑菇云 AoE + 孢子芽弹出。</summary>
    private void Explode() {
        if (Projectile.ai[1] != 0) return;
        Projectile.ai[1] = 1;

        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = 0.4f }, Projectile.Center);

        if (Main.myPlayer == Projectile.owner) {
            // 主爆 AoE
            Projectile.NewProjectile(
                Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<MossExplosion>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner,
                ai1: 1f, ai2: SporeTheme);

            // 3 颗孢子芽弹出 (落地小孢子云)
            int budDamage = Math.Max((int)(Projectile.damage * 0.25f), 1);
            for (int i = 0; i < 3; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-3.5f, 3.5f), -Main.rand.NextFloat(4f, 7f));
                Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, vel,
                    ModContent.ProjectileType<MossSporeBud>(), budDamage, 1f, Projectile.owner,
                    ai0: SporeTheme);
            }
        }

        // 爆炸粒子 - 绿色蘑菇云
        for (int i = 0; i < 20; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                vel, 40, default, Main.rand.NextFloat(1.5f, 2.5f));
            d.noGravity = true;
        }
        // 向上升起的烟雾粒子
        for (int i = 0; i < 10; i++) {
            Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-5f, -2f));
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Grass,
                vel, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;

        // 引信闪烁: 频率随剩余时间加速 (可读的"要炸了")
        Color drawColor = lightColor;
        if (Bounced == 1f) {
            float fuseT = Fuse / FuseFrames;
            float freq = MathHelper.Lerp(6f, 22f, fuseT * fuseT);
            float flash = MathF.Sin(Fuse * freq * 0.1f * MathHelper.TwoPi) > 0f ? 1f : 0f;
            drawColor = Color.Lerp(lightColor, new Color(190, 255, 130), flash * 0.8f);
            if (flash > 0f)
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.5f + fuseT * 0.4f, new Color(150, 240, 110) * 0.8f);
        }

        Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null,
            drawColor, Projectile.rotation,
            tex.Size() * 0.5f,
            Projectile.scale, SpriteEffects.None, 0);
        return false;
    }
}

/// <summary>
/// 苔藓爆炸 - 蘑菇云范围伤害。
/// ai[0] = 计时; ai[1] = 半径/演出缩放 (0 视为 1); ai[2] = 主题 (0=自然, 1=赤铜)。
/// </summary>
public class MossExplosion : ModProjectile
{
    public override string Texture
        => $"Terraria/Images/Projectile_{ProjectileID.Grenade}";

    private float SizeScale => Projectile.ai[1] <= 0f ? 1f : Projectile.ai[1];
    private bool Cuprite => Projectile.ai[2] == 1f;

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 30;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
        Projectile.alpha = 255; // 完全透明，不绘制自身贴图
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Projectile.ai[0]++;
        float radius = Projectile.ai[0] * 6f * SizeScale;

        // 爆炸首帧: 命中演出 (径向辉光 + 冲击环 + 泛光) + 落地屏震
        if (Projectile.ai[0] == 1f) {
            WeaponVFX.AddScreenShake(Projectile.Center, 4f * SizeScale);
            ACMWeaponBurst.Spawn(Projectile.GetSource_FromAI(), Projectile.Center,
                Cuprite ? ACMWeaponBurst.CupriteBurn : ACMWeaponBurst.Nature,
                scale: 1.6f * SizeScale, owner: Projectile.owner);
        }

        // 蘑菇云主体 - 边缘扩散的绿色火焰粒子
        int dustCount = Projectile.ai[0] < 10 ? 6 : 3;
        if (SizeScale < 0.7f)
            dustCount = Math.Max(dustCount / 2, 1);
        for (int i = 0; i < dustCount; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.2f, radius);
            int type = Cuprite && Main.rand.NextBool(3) ? DustID.Torch : DustID.GreenTorch;
            Dust d = Dust.NewDustPerfect(pos, type,
                new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-1.5f, -0.3f)),
                60, default, Main.rand.NextFloat(1.5f, 2.5f) * MathF.Sqrt(SizeScale));
            d.noGravity = true;
        }

        // 向上升起的烟雾柱
        if (Projectile.ai[0] < 15) {
            for (int i = 0; i < 2; i++) {
                Vector2 smokePos = Projectile.Center + new Vector2(Main.rand.NextFloat(-15f, 15f) * SizeScale, 0);
                Dust s = Dust.NewDustPerfect(smokePos, DustID.Grass,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-3f, -1.5f)),
                    120, default, Main.rand.NextFloat(2f, 3f) * SizeScale);
                s.noGravity = true;
            }
        }

        // 草叶碎片飞散
        if (Main.rand.NextBool(2)) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.5f);
            Dust g = Dust.NewDustPerfect(pos, DustID.GrassBlades,
                angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f), 40, default, 1.2f);
            g.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.15f, 0.5f, 0.15f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 120);
        if (Cuprite)
            target.AddBuff(BuffID.OnFire, 120);
    }

    public override bool PreDraw(ref Color lightColor) {
        if (Main.dedServ)
            return false;

        float t = MathHelper.Clamp(Projectile.ai[0] / 30f, 0f, 1f); // 0→1
        float fade = 1f - t;
        if (fade <= 0.01f)
            return false;

        Vector2 center = Projectile.Center - Main.screenPosition;
        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        Color glowCol = Cuprite ? new Color(230, 140, 60, 0) : new Color(70, 210, 90, 0);
        Color sparkCol = Cuprite ? new Color(255, 200, 120, 0) : new Color(160, 255, 130, 0);
        Color slashCol = Cuprite ? new Color(240, 160, 80, 0) : new Color(110, 230, 100, 0);
        float s = SizeScale;

        // 柔光核心
        Texture2D glow = ACMAsset.SoftGlow;
        if (glow != null)
            sb.Draw(glow, center, null, glowCol * fade, 0f,
                glow.Size() * 0.5f, (2f + t * 5f) * s, SpriteEffects.None, 0f);

        // 爆裂火花 (扩张旋转)
        Texture2D sparkle = ACMAsset.Sparkle;
        if (sparkle != null)
            sb.Draw(sparkle, center, null, sparkCol * fade, Projectile.rotation + t * 0.6f,
                sparkle.Size() * 0.5f, (0.6f + t * 2.2f) * s, SpriteEffects.None, 0f);

        // 向上喷发的剑气光柱 (蘑菇云升腾)
        Texture2D slash = ACMAsset.SlashBurst;
        if (slash != null) {
            Vector2 slashOrigin = new Vector2(slash.Width * 0.5f, slash.Height);
            sb.Draw(slash, center, null, slashCol * fade * 0.85f, 0f,
                slashOrigin, new Vector2((0.25f + t * 0.2f) * s, (0.25f + t * 0.45f) * s), SpriteEffects.None, 0f);
        }

        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);
        return false;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float radius = Projectile.ai[0] * 6f * SizeScale;
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
    }
}

/// <summary>
/// 孢子芽 - 爆炸弹出的次级种子, 落地绽放小孢子云 (25% 伤害)。
/// ai[0] = 主题 (0=自然, 1=赤铜火孢)。
/// </summary>
public class MossSporeBud : ModProjectile
{
    public override string Texture => "InnoVault/Assets/placeholder";

    private bool Cuprite => Projectile.ai[0] == 1f;

    public override void SetStaticDefaults() {
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.MossSporeBud.DisplayName", () => "孢子芽");
    }

    public override void SetDefaults() {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.friendly = false; // 本体不打人, 落地云打
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 90;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        Projectile.velocity.Y += 0.24f;
        Projectile.rotation += 0.2f * MathF.Sign(Projectile.velocity.X);
        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center,
                Cuprite ? DustID.Torch : DustID.GreenTorch,
                -Projectile.velocity * 0.1f, 90, default, 0.8f);
            d.noGravity = true;
        }
    }

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.4f, Pitch = 0.5f }, Projectile.Center);
        if (Main.myPlayer == Projectile.owner) {
            Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<MossExplosion>(), Projectile.damage, 0.5f, Projectile.owner,
                ai1: 0.45f, ai2: Cuprite ? 1f : 0f);
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 小孢子核 (柔光点)
        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.35f,
            Cuprite ? new Color(255, 170, 80) : new Color(140, 240, 110));
        return false;
    }
}
