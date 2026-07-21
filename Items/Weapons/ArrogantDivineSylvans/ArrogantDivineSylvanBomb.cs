using AncientChineseMythology.Celestias.Boss.Dazhengs.Items;
using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Weapons.DivineWoods;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.ArrogantDivineSylvans;

/// <summary>
/// 傲世神木·世界种 - 播种与收割的决策武器
/// 快甩: 立即抛出世界种 (铺场叠层); 按住蓄力 60 帧: 掌心种子生长为「母树种」
/// (×1.5 伤害, 爆炸半径 +40%, 主爆**引爆**范围年轮烙印, 子种全带小绽放)
/// 种子飞行中分裂 5 枚子种, 各自爆炸并释放追踪藤蛇 (藤蛇命中刻烙印)
/// </summary>
public class ArrogantDivineSylvanBomb : ModItem
{
    public override void SetDefaults() {
        Item.damage = 1800;
        Item.crit = 24;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 34;
        Item.height = 34;
        Item.useTime = 22;
        Item.useAnimation = 22;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 14f;
        Item.value = Item.buyPrice(gold: 500);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = null; // 音效由手持弹幕在抛出瞬间分层播放
        Item.autoReuse = true;
        Item.channel = true;  // 蓄力决策: 快甩铺场 vs 蓄满单点收割
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<ArrogantSylvanSeedHold>();
        Item.shootSpeed = 16f;
    }

    public override bool CanUseItem(Player player) {
        return player.ownedProjectileCounts[ModContent.ProjectileType<ArrogantSylvanSeedHold>()] < 1;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<ArrogantDivineSylvan>(15)
            .AddIngredient<DivineWoodBomb>()
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
}

/// <summary>
/// 掌心蓄力种子 (手持弹幕): 蓄力时种子在掌心生长 (scale 1→1.5, 翠→金渐变, 噪声抖动 ∝ charge²),
/// 蓄满白闪 + 音效提示; 松手抛出 (蓄满掷出「母树种」并震屏)
/// </summary>
public class ArrogantSylvanSeedHold : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/ArrogantDivineSylvans/ArrogantDivineSylvanBomb";

    private const int MaxCharge = 60;

    private ref float Charge => ref Projectile.ai[0];
    private bool _fullChargeCued;
    private Player Owner => Main.player[Projectile.owner];

    public override void SetDefaults() {
        Projectile.width = 24;
        Projectile.height = 24;
        Projectile.friendly = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 3600;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Player owner = Owner;
        if (!owner.active || owner.dead) { Projectile.Kill(); return; }

        // 松手 → 抛出
        if (!owner.channel) {
            ThrowSeed(owner);
            Projectile.Kill();
            return;
        }

        // 蓄力中: 钉住使用动画
        owner.itemAnimation = 2;
        owner.itemTime = 2;
        owner.heldProj = Projectile.whoAmI;

        if (Charge < MaxCharge)
            Charge++;

        float t = Charge / MaxCharge;

        // 蓄满提示: 白闪 + 清脆音 (一次)
        if (Charge >= MaxCharge && !_fullChargeCued) {
            _fullChargeCued = true;
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 1f, Pitch = 0.2f }, owner.Center);
            for (int i = 0; i < 14; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame,
                    Main.rand.NextVector2Circular(5f, 5f), 30, default, 2.2f);
                d.noGravity = true;
            }
        }

        // 掌心定位 (朝向鼠标; 非 owner 端用 owner.direction 兜底)
        if (Projectile.owner == Main.myPlayer) {
            int dir = Main.MouseWorld.X > owner.MountedCenter.X ? 1 : -1;
            if (owner.direction != dir) {
                owner.ChangeDir(dir);
                Projectile.netUpdate = true;
            }
        }
        Vector2 hand = owner.MountedCenter + new Vector2(owner.direction * 18f, -6f);
        // 噪声抖动 ∝ charge² (蓄力语法: 能量憋不住)
        hand += Main.rand.NextVector2Circular(2.5f, 2.5f) * t * t;
        Projectile.Center = hand;
        Projectile.rotation += 0.02f + 0.06f * t;

        // 生长期间的翠色生命粒子被吸入种子
        if (Main.rand.NextFloat() < 0.3f + 0.4f * t) {
            Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(40f, 40f);
            Dust d = Dust.NewDustPerfect(from, t > 0.7f ? DustID.GoldFlame : DustID.JungleTorch,
                (Projectile.Center - from) * 0.12f, 60, default, 1.4f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.3f + 0.4f * t, 0.7f + 0.6f * t, 0.25f + 0.3f * t);
    }

    private void ThrowSeed(Player owner) {
        if (Projectile.owner != Main.myPlayer)
            return;

        bool charged = Charge >= MaxCharge;
        Vector2 dir = owner.SafeDirectionTo(Main.MouseWorld);
        Vector2 vel = dir * (charged ? 19f : 16f) + new Vector2(0, -4f);
        int dmg = charged ? (int)(Projectile.damage * 1.5f) : Projectile.damage;

        Projectile.NewProjectile(Projectile.GetSource_FromThis(), owner.MountedCenter, vel,
            ModContent.ProjectileType<ArrogantSylvanWorldSeed>(),
            dmg, Projectile.knockBack, Projectile.owner, 0f, charged ? 1f : 0f);

        // 抛出反馈: 分层音 + 蓄满震屏 + 后坐
        SoundEngine.PlaySound(SoundID.Item1 with {
            Volume = 1f, Pitch = (charged ? -0.2f : 0.1f) + Main.rand.NextFloat(-0.1f, 0.1f)
        }, owner.Center);
        if (charged) {
            SoundEngine.PlaySound(SoundID.Item69 with { Volume = 0.8f, Pitch = 0.15f }, owner.Center);
            WeaponVFX.AddScreenShake(owner.Center, 3f);
            owner.velocity -= dir * 1.5f;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        Texture2D sg = ACMAsset.SoftGlow;
        float t = Charge / MaxCharge;
        float scale = MathHelper.Lerp(0.9f, 1.5f, ACMUtils.QuadOut(t));

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        // 翠→金渐变辉光 (charge 即读条)
        Color glow = Color.Lerp(ArrogantSylvanPalette.JadeBright, ArrogantSylvanPalette.GoldBright, t);
        float pulse = 0.5f + 0.2f * MathF.Sin((float)Main.timeForVisualEffects * (0.2f + 0.25f * t));
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            glow * (0.45f + 0.35f * t) * pulse, 0f, sg.Size() * 0.5f,
            scale * (0.55f + 0.25f * t), SpriteEffects.None, 0);
        if (t >= 1f) {
            Texture2D star = ACMAsset.BlankStar;
            sb.Draw(star, Projectile.Center - Main.screenPosition, null,
                ArrogantSylvanPalette.WhiteHot * (0.5f * pulse),
                (float)Main.timeForVisualEffects * 0.06f, star.Size() * 0.5f,
                0.5f, SpriteEffects.None, 0);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            lightColor, Projectile.rotation, tex.Size() * 0.5f,
            scale, SpriteEffects.None, 0);
        return false;
    }
}

/// <summary>
/// 世界种主弹 - 飞行 40 帧后分裂 5 枚子种 (ai[1]=1 为蓄满母树种: 更大、主爆引爆烙印、子种带小绽放)
/// </summary>
public class ArrogantSylvanWorldSeed : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/ArrogantDivineSylvans/ArrogantDivineSylvanBomb";

    private ref float AiTimer => ref Projectile.ai[0];
    private bool Charged => Projectile.ai[1] == 1f;
    private ref float ExplodedGuard => ref Projectile.localAI[0];
    private bool _hasSplit;

    public override void SetDefaults() {
        Projectile.width = 24;
        Projectile.height = 24;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 200;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        AiTimer++;
        Projectile.velocity.Y += 0.18f;
        Projectile.rotation += Projectile.velocity.X * 0.05f;
        Projectile.scale = Charged ? 1.35f : 1f;
        Lighting.AddLight(Projectile.Center, 0.4f, 1.0f, 0.3f);

        for (int i = 0; i < 2; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center,
                Charged && i == 0 ? DustID.GoldFlame : DustID.JungleTorch,
                -Projectile.velocity * 0.12f, 40, default, 1.8f);
            d.noGravity = true;
        }

        // 飞行 40 帧后分裂
        if (!_hasSplit && AiTimer > 40 && Main.myPlayer == Projectile.owner) {
            _hasSplit = true;
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 1f, Pitch = 0.3f + Main.rand.NextFloat(-0.1f, 0.1f) }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                Vector2 splitVel = Projectile.velocity.RotatedBy(MathHelper.ToRadians(-30 + 15 * i)) * 0.8f;
                splitVel.Y -= 2f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, splitVel,
                    ModContent.ProjectileType<ArrogantSylvanChildSeed>(),
                    Projectile.damage, Projectile.knockBack, Projectile.owner,
                    0f, Charged ? 1f : 0f);
            }
            for (int i = 0; i < 14; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                    Main.rand.NextVector2Circular(8f, 8f), 40, default, 2.4f);
                d.noGravity = true;
            }
            Projectile.Kill();
        }

        if (AiTimer > 150) Explode();
    }

    public override void OnKill(int timeLeft) {
        if (!_hasSplit) Explode();
    }

    private void Explode() {
        if (ExplodedGuard != 0) return;
        ExplodedGuard = 1;

        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = 0.1f + Main.rand.NextFloat(-0.08f, 0.08f) }, Projectile.Center);
        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.9f, Pitch = 0.35f }, Projectile.Center);
        // 世界种主爆命中演出 (金翠) + 单入口震屏 (大招预算)
        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
            ACMWeaponBurst.ArrogantSylvan, scale: Charged ? 2.2f : 1.8f, owner: Projectile.owner);
        WeaponVFX.AddScreenShake(Projectile.Center, Charged ? 8f : 6f);

        if (Main.myPlayer == Projectile.owner) {
            Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<ArrogantSylvanBloomExplosion>(),
                (int)(Projectile.damage * 1.5f), Projectile.knockBack, Projectile.owner,
                0f, Charged ? 1f : 0f);

            // === 母树种主爆 = 系列引爆动作: 波及范围内全部年轮烙印 ===
            if (Charged) {
                ArrogantSylvanBloom.DetonateArea(Projectile.GetSource_Death(), Projectile.Center,
                    400f, Projectile.damage, 4f, Projectile.owner);
            }

            int serpents = Charged ? 10 : 8;
            for (int i = 0; i < serpents; i++) {
                float angle = MathHelper.TwoPi * i / serpents;
                Vector2 fragVel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 14f);
                Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, fragVel,
                    ModContent.ProjectileType<ArrogantSylvanVineSerpent>(),
                    Projectile.damage / 3, 3f, Projectile.owner);
            }
        }

        for (int i = 0; i < 24; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(14f, 14f);
            Dust boom = Dust.NewDustPerfect(Projectile.Center, i % 3 == 0 ? DustID.GoldFlame : DustID.JungleTorch,
                vel, 30, default, Main.rand.NextFloat(2.4f, 3.6f));
            boom.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        Texture2D sg = ACMAsset.SoftGlow;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        float pulse = 0.55f + 0.18f * MathF.Sin(AiTimer * 0.22f);
        Color glow = Charged ? ArrogantSylvanPalette.GoldBright : new Color(220, 255, 100);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            glow * 0.65f, 0f,
            sg.Size() * 0.5f,
            pulse * (Charged ? 1.25f : 1f), SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.WhiteHot * 0.30f, 0f,
            sg.Size() * 0.5f,
            pulse * 0.5f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            lightColor, Projectile.rotation, tex.Size() * 0.5f,
            Projectile.scale * 1.2f, SpriteEffects.None, 0);
        return false;
    }
}

/// <summary>
/// 子种子 - 分裂后的小种子, 碰撞后各自爆炸 (ai[1]=1 继承母树种血统: 爆炸带小年轮环)
/// </summary>
public class ArrogantSylvanChildSeed : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/ArrogantDivineSylvans/ArrogantDivineSylvanBomb";

    private ref float AiTimer => ref Projectile.ai[0];
    private bool Charged => Projectile.ai[1] == 1f;
    private ref float ExplodedGuard => ref Projectile.localAI[0];

    public override void SetDefaults() {
        Projectile.width = 18;
        Projectile.height = 18;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 120;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
        Projectile.scale = 0.75f;
    }

    public override void AI() {
        AiTimer++;
        Projectile.velocity.Y += 0.25f;
        Projectile.rotation += Projectile.velocity.X * 0.06f;
        Lighting.AddLight(Projectile.Center, 0.2f, 0.6f, 0.2f);

        Dust d = Dust.NewDustPerfect(Projectile.Center, Charged ? DustID.GoldFlame : DustID.JungleTorch,
            -Projectile.velocity * 0.08f, 50, default, 1.2f);
        d.noGravity = true;

        if (AiTimer > 100) Explode();
    }

    public override void OnKill(int timeLeft) {
        Explode();
    }

    private void Explode() {
        if (ExplodedGuard != 0) return;
        ExplodedGuard = 1;

        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.85f, Pitch = 0.4f + Main.rand.NextFloat(-0.1f, 0.1f) }, Projectile.Center);
        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
            ACMWeaponBurst.ArrogantSylvan, scale: 1.2f, owner: Projectile.owner);
        WeaponVFX.AddScreenShake(Projectile.Center, 3f);

        if (Main.myPlayer == Projectile.owner) {
            Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<ArrogantSylvanBloomExplosion>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner,
                0f, Charged ? 2f : 0f); // 2=子种带小年轮环档

            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4 + Main.rand.NextFloat(0.5f);
                Vector2 fragVel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 12f);
                Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, fragVel,
                    ModContent.ProjectileType<ArrogantSylvanVineSerpent>(),
                    Projectile.damage / 4, 2f, Projectile.owner);
            }
        }

        for (int i = 0; i < 16; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(12f, 12f);
            Dust boom = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                vel, 30, default, Main.rand.NextFloat(2f, 3.2f));
            boom.noGravity = true;
        }

        Projectile.Kill();
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        Texture2D sg = ACMAsset.SoftGlow;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        float pulse = 0.40f + 0.10f * MathF.Sin(AiTimer * 0.3f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            (Charged ? ArrogantSylvanPalette.GoldBright : new Color(200, 255, 100)) * 0.55f, 0f,
            sg.Size() * 0.5f,
            pulse, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            lightColor, Projectile.rotation, tex.Size() * 0.5f,
            Projectile.scale, SpriteEffects.None, 0);
        return false;
    }
}

/// <summary>
/// 傲世绽放爆炸 - 圆形爆炸场 (伤害判定半径与视觉严格对齐, 封顶)
/// ai[1]: 0=普通 (廉价演出) / 1=母树种主爆 (GrowthRing+径向泛光 set-piece) / 2=子种小年轮环
/// </summary>
public class ArrogantSylvanBloomExplosion : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private ref float Timer => ref Projectile.ai[0];
    private int Tier => (int)Projectile.ai[1];

    private float MaxRadius => Tier == 1 ? 400f : (Tier == 2 ? 260f : 280f);

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 65;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override bool ShouldUpdatePosition() => false;

    private float CurrentRadius() => Math.Min(Timer * 16f, MaxRadius);

    public override void AI() {
        Timer++;
        float radius = CurrentRadius();

        for (int i = 0; i < 6; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.3f, radius);
            Dust d = Dust.NewDustPerfect(pos, i % 3 == 0 ? DustID.GoldFlame : DustID.JungleTorch,
                Main.rand.NextVector2Circular(2f, 2f), 40, default, 2.3f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.6f, 1.8f, 0.5f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 900);
        target.AddBuff(BuffID.Venom, 600);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        => VaultUtils.CircleIntersectsRectangle(Projectile.Center, CurrentRadius(), targetHitbox);

    public override bool PreDraw(ref Color lightColor) {
        float prog = 1f - Projectile.timeLeft / 65f;
        float alpha = ACMUtils.QuadOut(1f - prog) * 0.92f;
        float scale = MathHelper.SmoothStep(0f, 18f, ACMUtils.QuadOut(prog));

        // === 绽放 set-piece (按档位分级, 性能梯度) ===
        if (Tier == 1) {
            // 母树种主爆: 一次性金翠径向泛光 (占全屏名额, 名额满自动退化柔光)
            if (prog < 0.55f) {
                float bell = MathF.Sin(Math.Min(prog / 0.55f, 1f) * MathF.PI);
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.16f + 0.12f * prog, bell * 0.9f,
                    new Color(230, 235, 120), 12f);
            }
            // 大年轮绽放环 (系列专属着色器)
            ArrogantSylvanFX.DrawGrowthRing(Projectile.Center, MaxRadius,
                ACMUtils.QuadOut(Math.Min(prog * 1.5f, 1f)), alpha * 0.85f, ringFreq: 11f);
        }
        else if (Tier == 2) {
            // 子种小年轮环 (低强度)
            ArrogantSylvanFX.DrawGrowthRing(Projectile.Center, MaxRadius,
                ACMUtils.QuadOut(Math.Min(prog * 1.6f, 1f)), alpha * 0.4f, ringFreq: 6f);
        }

        // 荆棘领域生长溶解 (DissolveBurn 喂 Sparkle 放射纹, 噪声 clip + 金灼边)
        {
            Texture2D thorn = ACMAsset.Sparkle;
            if (thorn != null) {
                float grow = Math.Min(prog / 0.4f, 1f);
                float domainScale = scale * 0.30f;
                WeaponVFX.ApplyDissolveBurn(thorn, Projectile.Center, null,
                    new Color(120, 220, 110) * (alpha * 0.9f), Timer * 0.04f,
                    thorn.Size() * 0.5f, domainScale,
                    threshold: 1f - grow, intensity: alpha,
                    edgeColor: new Color(255, 210, 90, 220), edgeWidth: 0.1f, noiseScale: 2.4f);
            }
        }

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D sparkle = ACMAsset.Sparkle;
        Texture2D star = ACMAsset.BlankStar;

        for (int k = 0; k < 12; k++) {
            float bAngle = k * MathF.PI / 6f + Timer * 0.02f;
            bool major = (k % 2 == 0);
            Color bColor = major ? ArrogantSylvanPalette.GoldBright : ArrogantSylvanPalette.JadeDeep;
            float bLen = major ? scale * 0.70f : scale * 0.42f;
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                bColor * (alpha * 0.82f), bAngle,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.16f, bLen), SpriteEffects.None, 0);
        }

        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.JadeBright * (alpha * 0.50f), 0f,
            sg.Size() * 0.5f,
            scale * 0.60f, SpriteEffects.None, 0);

        float flashAlpha = MathHelper.SmoothStep(1.2f, 0f, prog * 1.4f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.WhiteHot * (alpha * flashAlpha), 0f,
            sg.Size() * 0.5f,
            scale * 0.22f, SpriteEffects.None, 0);

        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.GoldBright * (alpha * 0.55f),
            Timer * 0.08f, star.Size() * 0.5f,
            scale * 0.15f, SpriteEffects.None, 0);
        sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 120) * (alpha * 0.50f),
            -Timer * 0.05f, sparkle.Size() * 0.5f,
            scale * 0.20f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 傲世藤蔓蛇 - 追踪型碎片弹幕 (共享节流索敌, 命中刻烙印)
/// </summary>
public class ArrogantSylvanVineSerpent : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/BlankStar";

    private ref float TargetCache => ref Projectile.localAI[0];
    private ref float RescanTimer => ref Projectile.localAI[1];

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
    }

    public override void SetDefaults() {
        Projectile.width = 18;
        Projectile.height = 18;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 120;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        Projectile.rotation += 0.25f;

        if (Projectile.timeLeft < 100) {
            NPC target = ArrogantSylvanTargeting.UpdateTarget(Projectile, ref TargetCache, ref RescanTimer, 600f);
            ArrogantSylvanTargeting.SteerTowards(Projectile, target, 18f, 0.14f);
        }

        Lighting.AddLight(Projectile.Center, 0.2f, 0.5f, 0.15f);

        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                -Projectile.velocity * 0.05f, 50, default, 1.5f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 600);
        target.AddBuff(BuffID.Venom, 300);
        ArrogantSylvanBrandNPC.AddStack(target);
        for (int i = 0; i < 5; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(5f, 5f), 40, default, 2f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;

        // 藤蔓蛇金翠双层 ribbon (§B.1)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 11f,
            outerColor: ArrogantSylvanPalette.TrailOuter, innerColor: ArrogantSylvanPalette.TrailInner,
            uvScroll: -(float)Main.timeForVisualEffects * 0.05f);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D star = ACMAsset.BlankStar;
        Texture2D sg = ACMAsset.SoftGlow;

        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.50f;
            sb.Draw(sg,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(220, 255, 100) * a, 0f,
                sg.Size() * 0.5f,
                0.30f, SpriteEffects.None, 0);
        }

        float pulse = 0.60f + 0.18f * MathF.Sin((float)Main.timeForVisualEffects * 0.22f);
        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.JadeBright * (0.80f * pulse),
            Projectile.rotation, star.Size() * 0.5f,
            0.55f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            ArrogantSylvanPalette.GoldBright * (0.45f * pulse), 0f,
            sg.Size() * 0.5f,
            0.35f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
