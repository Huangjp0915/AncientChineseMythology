using AncientChineseMythology.Celestias.Boss.Dryades.Items;
using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.DivineWoods;

/// <summary>
/// 神木灵杖 - 藤鞭法杖: 每次使用向光标甩出一条贝塞尔藤鞭 (前摇收拢 → poly(10) 鞭梢炸出 → 微颤 → 收回)。
/// 鞭梢命中挂 2 层生根, 鞭身 1 层; 每第 4 鞭为"重鞭": ×1.45 伤害、鞭梢引爆生根、落点留荆棘地。
/// </summary>
public class DivineWoodScepter : ModItem
{
    private const uint ComboResetGap = 110;

    private int _whipCombo;
    private uint _lastWhipFrame;

    public override void SetDefaults() {
        Item.damage = 155;
        Item.crit = 12;
        Item.DamageType = DamageClass.Magic;
        Item.width = 36;
        Item.height = 36;
        Item.useTime = 26;
        Item.useAnimation = 26;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 5f;
        Item.value = Item.buyPrice(gold: 50);
        Item.rare = ItemRarityID.Purple;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<DivineWoodVineWhipHead>();
        Item.shootSpeed = 18f;
        Item.mana = 14;
        Item.staff[Type] = true;
    }

    public override bool CanUseItem(Player player) {
        if (player.ownedProjectileCounts[ModContent.ProjectileType<DivineWoodVineWhipHead>()] > 0)
            return false;
        if (Main.GameUpdateCount - _lastWhipFrame > ComboResetGap)
            _whipCombo = 0;
        // 第 4 鞭 (重鞭) 用更长的动画承载更狠的前摇与收招
        bool heavy = _whipCombo == 3;
        Item.useTime = Item.useAnimation = heavy ? 34 : 26;
        Item.mana = heavy ? 20 : 14;
        return true;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        bool heavy = _whipCombo == 3;
        _whipCombo = (_whipCombo + 1) % 4;
        _lastWhipFrame = Main.GameUpdateCount;

        // 鞭梢目标点: 光标, 限程 (velocity 槽承载目标向量, 天然网络同步)
        float maxRange = heavy ? 400f : 360f;
        Vector2 toTarget = Main.MouseWorld - player.MountedCenter;
        if (toTarget.Length() > maxRange)
            toTarget = toTarget.SafeNormalize(Vector2.UnitX) * maxRange;

        Projectile.NewProjectile(source, player.MountedCenter, toTarget, type,
            heavy ? (int)(damage * 1.45f) : damage, knockback, player.whoAmI, heavy ? 1f : 0f);
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
/// 藤蔓链鞭 - 贝塞尔藤鞭本体 (held 生命周期 = 挥鞭动画):
/// 前摇 25% 鞭身收拢 → 25% poly(10) 甩出 (鞭梢近乎瞬到) → 15% 驻留微颤 → 35% 收回。
/// ai[0]=重鞭标记; velocity=鞭梢目标向量 (相对玩家)。
/// </summary>
public class DivineWoodVineWhipHead : ModProjectile
{
    public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private const int SampleCount = 13;

    private bool Heavy => Projectile.ai[0] >= 1f;
    private ref float Timer => ref Projectile.ai[1];
    private int LifeTotal => Heavy ? 34 : 26;

    private const float WindupFrac = 0.25f;
    private const float LashFrac = 0.25f;
    private const float HoldFrac = 0.15f;

    private bool _crackPlayed;
    private bool _patchSpawned;
    private Vector2 _tipPos;
    private readonly Vector2[] _points = new Vector2[SampleCount];

    private Player Owner => Main.player[Projectile.owner];

    public override void SetDefaults() {
        Projectile.width = 24;
        Projectile.height = 24;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 40;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
        Projectile.ownerHitCheck = false;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void OnSpawn(IEntitySource source) {
        Projectile.timeLeft = LifeTotal;
        Projectile.spriteDirection = Projectile.velocity.X >= 0 ? 1 : -1;
        SoundEngine.PlaySound(SoundID.Item7 with { Volume = 0.6f, Pitch = Heavy ? -0.35f : -0.1f }, Owner.Center);
    }

    private float Progress => MathHelper.Clamp(Timer / LifeTotal, 0f, 1f);

    /// <summary>0 收拢中 / 1 甩出 / 2 驻留 / 3 收回。</summary>
    private int PhaseOf(float p, out float t) {
        if (p < WindupFrac) { t = p / WindupFrac; return 0; }
        p -= WindupFrac;
        if (p < LashFrac) { t = p / LashFrac; return 1; }
        p -= LashFrac;
        if (p < HoldFrac) { t = p / HoldFrac; return 2; }
        t = (p - HoldFrac) / Math.Max(1f - WindupFrac - LashFrac - HoldFrac, 0.01f);
        return 3;
    }

    private void BuildWhip() {
        Vector2 hand = Owner.MountedCenter;
        Vector2 targetVec = Projectile.velocity;
        Vector2 tipTarget = hand + targetVec;
        Vector2 dirN = targetVec.SafeNormalize(Vector2.UnitX);
        Vector2 perp = new(-dirN.Y, dirN.X);
        float side = Projectile.spriteDirection >= 0 ? 1f : -1f;
        float dist = targetVec.Length();

        int phase = PhaseOf(Progress, out float t);
        float bow;   // 鞭身弯垂量 (贝塞尔控制点偏移系数)
        switch (phase) {
            case 0: {
                // 收拢: 鞭梢拖在身后卷起 (pow2 渐深 = 吸气)
                Vector2 curl = hand - dirN * (30f + 50f * t * t) + perp * side * (60f * t);
                _tipPos = curl;
                bow = 0.85f;
                break;
            }
            case 1: {
                // 甩出: poly(10) — 几乎全部行程压进前几帧
                float ease = 1f - MathF.Pow(1f - t, 10f);
                Vector2 curl = hand - dirN * 80f + perp * side * 60f;
                _tipPos = Vector2.Lerp(curl, tipTarget, ease);
                bow = MathHelper.Lerp(0.85f, -0.22f, ease); // 弯垂翻面 = 鞭甩过头的反弓
                if (!_crackPlayed && ease > 0.6f) {
                    _crackPlayed = true;
                    SoundEngine.PlaySound(SoundID.Item153 with { Volume = Heavy ? 1f : 0.8f, Pitch = Heavy ? -0.15f : 0.15f }, _tipPos);
                    if (Heavy) {
                        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = 0.5f }, _tipPos);
                        WeaponVFX.AddScreenShake(_tipPos, 3f);
                    }
                    else {
                        WeaponVFX.AddScreenShake(_tipPos, 1.2f);
                    }
                    // 重鞭落点: 荆棘地 (owner 端)
                    if (Heavy && !_patchSpawned && Main.myPlayer == Projectile.owner) {
                        _patchSpawned = true;
                        Vector2 ground = DivineWoodRoot.FindGroundBelow(tipTarget, 22);
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), ground, Vector2.Zero,
                            ModContent.ProjectileType<DivineWoodThornPatch>(),
                            (int)(Projectile.damage * 0.35f), 1f, Projectile.owner);
                    }
                }
                break;
            }
            case 2: {
                // 驻留微颤
                _tipPos = tipTarget + Main.rand.NextVector2Circular(2.5f, 2.5f);
                bow = -0.10f;
                break;
            }
            default: {
                // 收回
                _tipPos = Vector2.Lerp(tipTarget, hand + dirN * 20f, ACMUtils.QuadIn(t));
                bow = MathHelper.Lerp(-0.10f, 0.5f, t);
                break;
            }
        }

        // 贝塞尔采样: 控制点 = 中点 + 垂向弯垂
        Vector2 mid = (hand + _tipPos) * 0.5f + perp * side * bow * dist * 0.4f;
        for (int i = 0; i < SampleCount; i++)
            _points[i] = ACMUtils.BezierQuad(hand, mid, _tipPos, i / (float)(SampleCount - 1));

        Projectile.Center = _tipPos;
    }

    public override void AI() {
        if (!Owner.active || Owner.dead) { Projectile.Kill(); return; }
        Owner.itemAnimation = Math.Max(Owner.itemAnimation, 2);
        Owner.itemTime = Math.Max(Owner.itemTime, 2);
        Owner.heldProj = Projectile.whoAmI;
        Owner.ChangeDir(Projectile.velocity.X >= 0 ? 1 : -1);

        BuildWhip();

        // 鞭梢灵叶粒子 (甩出/驻留阶段)
        int phase = PhaseOf(Progress, out _);
        if ((phase == 1 || phase == 2) && Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(_tipPos + Main.rand.NextVector2Circular(8, 8),
                DustID.JungleTorch, Main.rand.NextVector2Circular(2f, 2f), 60, default, 1.4f);
            d.noGravity = true;
        }
        Lighting.AddLight(_tipPos, 0.25f, 0.7f, 0.3f);
        Timer++;
    }

    public override bool? CanDamage() {
        int phase = PhaseOf(Progress, out _);
        return phase == 1 || phase == 2 ? null : false;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float width = Heavy ? 26f : 18f;
        for (int i = 0; i < SampleCount - 1; i++) {
            float col = 0f;
            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    _points[i], _points[i + 1], width, ref col))
                return true;
        }
        return false;
    }

    private bool IsTipHit(NPC target) => Vector2.Distance(target.Hitbox.Center.ToVector2(), _tipPos) < 52f;

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
        if (IsTipHit(target))
            modifiers.SourceDamage *= 1.3f; // 鞭梢音爆点
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        bool tip = IsTipHit(target);

        for (int i = 0; i < (tip ? 12 : 6); i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(5f, 5f), 50, default, tip ? 2f : 1.5f);
            d.noGravity = true;
        }

        if (tip) {
            if (Heavy) {
                int consumed = DivineWoodRoot.TriggerBloom(Projectile.GetSource_OnHit(target), target,
                    Projectile.damage, 5f, Projectile.owner);
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.DivineWood, scale: 1.25f + consumed * 0.05f, owner: Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, consumed > 0 ? 4f : 2.5f);
                // 重鞭梢命中: 3 片藤蔓叶爆
                if (Main.myPlayer == Projectile.owner) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 leafVel = Main.rand.NextVector2CircularEdge(6f, 6f);
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center,
                            leafVel, ModContent.ProjectileType<DivineWoodVineBurstLeaf>(),
                            Projectile.damage / 3, 1f, Projectile.owner);
                    }
                }
            }
            else {
                DivineWoodRoot.AddStack(target, 2);
                ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                    ACMWeaponBurst.DivineWood, scale: 1f, owner: Projectile.owner);
                WeaponVFX.AddScreenShake(target.Center, 1.5f);
            }
        }
        else {
            DivineWoodRoot.AddStack(target, 1);
            WeaponVFX.AddScreenShake(target.Center, 1f);
        }
        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = tip ? 0.5f : 0.2f }, target.Center);
    }

    public override bool PreDraw(ref Color lightColor) {
        int phase = PhaseOf(Progress, out float t);
        float alpha = phase == 3 ? 1f - ACMUtils.QuadIn(t) * 0.7f : 1f;

        // 鞭身: 双层 ribbon (重鞭更粗更亮)
        WeaponVFX.DrawRibbonTrail(_points, baseWidth: Heavy ? 19f : 15f,
            outerColor: new Color(20, 110, 55, (byte)((Heavy ? 190 : 165) * alpha)),
            innerColor: new Color(170, 255, 150, (byte)((Heavy ? 235 : 205) * alpha)),
            tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D sparkle = ACMAsset.Sparkle;
        float pulse = 0.7f + 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 0.25f);

        // 鞭梢辉光 + 灵叶
        sb.Draw(sg, _tipPos - Main.screenPosition, null,
            (Heavy ? DivineWoodPalette.BrightCore : DivineWoodPalette.Emerald) * (0.7f * pulse * alpha), 0f,
            sg.Size() * 0.5f, Heavy ? 0.65f : 0.5f, SpriteEffects.None, 0);
        sb.Draw(sparkle, _tipPos - Main.screenPosition, null,
            new Color(140, 255, 160) * (0.55f * pulse * alpha),
            (float)Main.timeForVisualEffects * 0.1f, sparkle.Size() * 0.5f,
            Heavy ? 0.4f : 0.3f, SpriteEffects.None, 0);

        // 鞭身发光叶节
        for (int i = 2; i < SampleCount - 1; i += 3) {
            sb.Draw(sg, _points[i] - Main.screenPosition, null,
                DivineWoodPalette.Emerald * (0.25f * pulse * alpha), 0f,
                sg.Size() * 0.5f, 0.16f, SpriteEffects.None, 0);
        }

        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);
        return false;
    }
}

/// <summary>
/// 荆棘地 - 重鞭落点留下的 2 秒荆棘丛: 周期性刺击并播种生根。
/// </summary>
public class DivineWoodThornPatch : ModProjectile
{
    public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private const int LifeTime = 120;
    private const float HalfWidth = 80f;
    private const float Height = 46f;

    private ref float Timer => ref Projectile.ai[0];

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = LifeTime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 20;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void OnSpawn(IEntitySource source) {
        SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = -0.1f }, Projectile.Center);
    }

    public override void AI() {
        Timer++;
        // 荆棘丛粒子
        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(
                Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), -Main.rand.NextFloat(0f, 20f)),
                Main.rand.NextBool() ? DustID.JunglePlants : DustID.JungleTorch,
                new Vector2(0, -Main.rand.NextFloat(0.5f, 2f)), 70, default, 1.2f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.2f, 0.55f, 0.25f);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        Rectangle rect = new((int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - Height),
            (int)(HalfWidth * 2), (int)Height);
        return rect.Intersects(targetHitbox);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        DivineWoodRoot.AddStack(target, 1);
        for (int i = 0; i < 4; i++) {
            Dust d = Dust.NewDustPerfect(target.Bottom, DustID.JungleTorch,
                new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 3f)), 60, default, 1.3f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        float life = Timer / LifeTime;
        float appear = ACMUtils.QuadOut(Math.Min(Timer / 10f, 1f));
        float fade = life > 0.75f ? 1f - (life - 0.75f) / 0.25f : 1f;
        float alpha = appear * fade;

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D sg = ACMAsset.SoftGlow;
        Vector2 basePos = Projectile.Center - Main.screenPosition;

        // 一排荆棘刺 (交错倾斜, 呼吸摆动)
        for (int i = 0; i < 7; i++) {
            float fx = MathHelper.Lerp(-HalfWidth * 0.9f, HalfWidth * 0.9f, i / 6f);
            float sway = MathF.Sin((float)Main.timeForVisualEffects * 0.06f + i * 1.3f) * 0.08f;
            float tilt = (i % 2 == 0 ? -0.18f : 0.18f) + sway;
            float hMul = (i % 2 == 0 ? 1f : 0.65f) * appear;
            Color c = (i % 2 == 0 ? DivineWoodPalette.Emerald : DivineWoodPalette.DeepGreen) * (alpha * 0.8f);
            sb.Draw(burst, basePos + new Vector2(fx, 0f), null, c, tilt,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.10f, Height / burst.Height * hMul), SpriteEffects.None, 0f);
        }

        // 地面辉光
        sb.Draw(sg, basePos, null, DivineWoodPalette.DeepGreen * (alpha * 0.5f), 0f,
            sg.Size() * 0.5f, new Vector2(1.6f, 0.5f), SpriteEffects.None, 0f);

        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);
        return false;
    }
}

/// <summary>
/// 藤蔓叶爆 - 重鞭梢命中释放的追踪叶片 (命中播种 1 层)。
/// </summary>
public class DivineWoodVineBurstLeaf : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Leaf;

    private float _timer;

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 5;
    }

    public override void SetDefaults() {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 90;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation += 0.22f * Projectile.direction;
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 5) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        if (_timer > 15) {
            float closestDist = 450f;
            int targetIdx = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < closestDist) { closestDist = d; targetIdx = i; }
            }
            if (targetIdx >= 0) {
                Vector2 dir = Projectile.DirectionTo(Main.npc[targetIdx].Center);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 14f, 0.08f);
            }
        }
        else {
            Projectile.velocity *= 0.94f;
        }

        if (Main.rand.NextBool(4)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
                -Projectile.velocity * 0.05f, 100, default, 0.8f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.1f, 0.3f, 0.1f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        DivineWoodRoot.AddStack(target, 1);
        for (int i = 0; i < 4; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(3f, 3f), 60, default, 1.2f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        int fh = tex.Height / Main.projFrames[Type];
        Rectangle src = new(0, Projectile.frame * fh, tex.Width, fh);
        Vector2 origin = new(tex.Width / 2f, fh / 2f);
        Color tint = Color.Lerp(lightColor, new Color(100, 255, 120), 0.35f);
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            tint, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
        return false;
    }
}
