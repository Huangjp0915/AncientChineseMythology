using AncientChineseMythology.Celestias.Boss.Dryades.Items;
using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.DivineWoods;

/// <summary>
/// 神木长弓 - 真蓄力弓 (手持弹幕):
/// 按住蓄力 — 档1 (&lt;40%) 快速叶矢 ×0.75; 档2 (40~95%) 三箭平行齐射 ×0.9 + 双螺旋叶护卫;
/// 满蓄"贯林矢" ×2.4 巨型穿透箭, 命中每个敌人都引爆生根。
/// 蓄力汇聚粒子 72% 后寂静一拍, 满蓄弓臂微颤 + 就绪音。
/// </summary>
public class DivineWoodLongbow : ModItem
{
    public override void SetDefaults() {
        Item.damage = 155;
        Item.crit = 14;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 24;
        Item.height = 56;
        Item.useTime = 18;
        Item.useAnimation = 18;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 4f;
        Item.value = Item.buyPrice(gold: 50);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = null;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.channel = true;
        Item.shoot = ModContent.ProjectileType<DivineWoodBowHeld>();
        Item.shootSpeed = 16f;
        Item.useAmmo = AmmoID.Arrow;
    }

    public override Vector2? HoldoutOffset() => new Vector2(-2, 0);

    public override bool CanUseItem(Player player) {
        return player.ownedProjectileCounts[ModContent.ProjectileType<DivineWoodBowHeld>()] < 1;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        // 一次点击消耗一支箭 → 装填进手持弓 (伤害含箭矢加成), 释放时按蓄力档位射出
        Projectile.NewProjectile(source, player.MountedCenter, velocity,
            ModContent.ProjectileType<DivineWoodBowHeld>(), damage, knockback, player.whoAmI);
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
/// 神木长弓·手持弓体 - 蓄力/瞄准/释放全流程:
/// 汇聚粒子密度∝√charge 且 72% 硬切 (释放前的吸气), 满蓄弓臂微颤;
/// 释放档位: &lt;40% 快速单矢 / 40~95% 三连齐射 / ≥95% 贯林矢。
/// </summary>
public class DivineWoodBowHeld : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/DivineWoods/DivineWoodLongbow";

    private const int MaxCharge = 55;

    private ref float Timer => ref Projectile.ai[0];
    private ref float Released => ref Projectile.localAI[0];
    private float Charge => MathHelper.Clamp(Timer / MaxCharge, 0f, 1f);

    private bool _fullPinged;
    private Player Owner => Main.player[Projectile.owner];

    public override void SetDefaults() {
        Projectile.width = 20;
        Projectile.height = 20;
        Projectile.friendly = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 3600;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void OnSpawn(IEntitySource source) {
        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.6f, Pitch = -0.2f }, Owner.Center); // 搭弦
    }

    public override void AI() {
        if (!Owner.active || Owner.dead || Owner.HeldItem.type != ModContent.ItemType<DivineWoodLongbow>()) {
            Projectile.Kill();
            return;
        }

        // 瞄准: owner 端取鼠标, velocity 槽承载准向 (自动同步)
        if (Main.myPlayer == Projectile.owner && Released == 0f) {
            Vector2 aim = (Main.MouseWorld - Owner.MountedCenter).SafeNormalize(Vector2.UnitX);
            if ((aim - Projectile.velocity).LengthSquared() > 0.002f) {
                Projectile.velocity = aim;
                Projectile.netUpdate = true;
            }
        }
        Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);

        Projectile.Center = Owner.MountedCenter + dir * 13f;
        Projectile.rotation = dir.ToRotation();
        Owner.heldProj = Projectile.whoAmI;
        Owner.ChangeDir(dir.X >= 0 ? 1 : -1);
        Owner.itemTime = 2;
        Owner.itemAnimation = 2;
        Owner.itemRotation = MathF.Atan2(dir.Y * Owner.direction, dir.X * Owner.direction);
        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);

        if (Released > 0f) {
            // 释放后短收招
            Released++;
            if (Released > 6f)
                Projectile.Kill();
            return;
        }

        if (Owner.channel) {
            Timer = Math.Min(Timer + 1, MaxCharge + 20);

            // 汇聚粒子: 密度∝√charge, 72% 后硬切 (寂静一拍)
            if (Charge < 0.72f && Main.rand.NextFloat() < 0.2f + 0.5f * MathF.Sqrt(Charge)) {
                Vector2 spawn = Projectile.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(60f, 160f);
                Vector2 pull = (Projectile.Center - spawn) * 0.10f;
                Dust d = Dust.NewDustPerfect(spawn, DustID.JungleTorch, pull, 90, default, 1.3f);
                d.noGravity = true;
            }

            // 拉弦吱呀声 (随蓄力音高上移)
            if ((int)Timer % 14 == 0 && Charge < 0.95f)
                SoundEngine.PlaySound(SoundID.Item7 with { Volume = 0.3f, Pitch = -0.4f + Charge * 0.6f }, Owner.Center);

            // 满蓄就绪 ping (一次)
            if (!_fullPinged && Charge >= 1f) {
                _fullPinged = true;
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = 0.35f }, Owner.Center);
                for (int i = 0; i < 10; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                        Main.rand.NextVector2Circular(2.5f, 2.5f), 60, default, 1.6f);
                    d.noGravity = true;
                }
            }
            Lighting.AddLight(Projectile.Center, 0.2f + Charge * 0.3f, 0.6f + Charge * 0.5f, 0.25f);
        }
        else {
            Fire(dir);
            Released = 1f;
        }
    }

    private void Fire(Vector2 dir) {
        if (Main.myPlayer != Projectile.owner) {
            // 非 owner 端只演出
            return;
        }

        float c = Charge;
        Vector2 muzzle = Owner.MountedCenter + dir * 18f;
        IEntitySource src = Projectile.GetSource_FromThis();

        if (c >= 0.95f) {
            // ===== 满蓄·贯林矢 =====
            Projectile.NewProjectile(src, muzzle, dir * 24f,
                ModContent.ProjectileType<DivineWoodPierceArrow>(),
                (int)(Projectile.damage * 2.4f), Projectile.knockBack * 2f, Projectile.owner);
            SoundEngine.PlaySound(SoundID.Item5 with { Volume = 1f, Pitch = -0.2f }, muzzle);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = 0.3f }, muzzle);
            WeaponVFX.AddScreenShake(Owner.Center, 3.5f);
            // 后坐
            Owner.velocity -= dir * 2.5f;
        }
        else if (c >= 0.4f) {
            // ===== 三箭齐射 + 双螺旋叶护卫 =====
            Vector2 perp = new(-dir.Y, dir.X);
            for (int i = -1; i <= 1; i++) {
                Projectile.NewProjectile(src, muzzle + perp * i * 14f, dir * 19f,
                    ModContent.ProjectileType<DivineWoodLeafBolt>(),
                    (int)(Projectile.damage * 0.9f), Projectile.knockBack, Projectile.owner);
            }
            for (int i = 0; i < 2; i++) {
                Vector2 sVel = dir.RotatedBy((i == 0 ? -1 : 1) * 0.32f) * 14f;
                Projectile.NewProjectile(src, muzzle, sVel,
                    ModContent.ProjectileType<DivineWoodSpiralLeaf>(),
                    (int)(Projectile.damage * 0.5f), Projectile.knockBack * 0.3f, Projectile.owner,
                    ai0: MathHelper.Pi * i);
            }
            SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.95f, Pitch = 0.1f }, muzzle);
            WeaponVFX.AddScreenShake(Owner.Center, 2f);
            Owner.velocity -= dir * 1.2f;
        }
        else {
            // ===== 快速叶矢 =====
            Projectile.NewProjectile(src, muzzle, dir * 17f,
                ModContent.ProjectileType<DivineWoodLeafBolt>(),
                (int)(Projectile.damage * 0.75f), Projectile.knockBack * 0.7f, Projectile.owner);
            SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.8f, Pitch = 0.3f }, muzzle);
        }

        for (int i = 0; i < 8; i++) {
            Dust d = Dust.NewDustPerfect(muzzle, DustID.JungleTorch,
                dir.RotatedByRandom(0.4f) * Main.rand.NextFloat(2f, 6f), 60, default, 1.4f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
        Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        float c = Charge;

        // 满蓄弓臂微颤
        Vector2 drawPos = Projectile.Center;
        if (c >= 1f && Released == 0f)
            drawPos += Main.rand.NextVector2Circular(1.4f, 1.4f);

        SpriteBatch sb = Main.spriteBatch;

        // 弓体 (竖版贴图旋到准向; 弓背随蓄力微压 = 拉满的张力)
        SpriteEffects flip = dir.X < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
        sb.Draw(tex, drawPos - Main.screenPosition, null, lightColor,
            Projectile.rotation + MathHelper.PiOver2, tex.Size() * 0.5f,
            new Vector2(1f - 0.10f * c, 1f + 0.05f * c), flip, 0f);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        Texture2D sg = ACMAsset.SoftGlow;

        // 搭弦叶矢 (随蓄力后拉)
        if (Released == 0f && Timer > 2f) {
            Vector2 arrowPos = drawPos + dir * (10f - 14f * c);
            float arrowGlow = 0.5f + 0.5f * c;
            sb.Draw(lsh, arrowPos - Main.screenPosition, null,
                new Color(120, 255, 140) * arrowGlow, Projectile.rotation,
                lsh.Size() * 0.5f, new Vector2(0.8f, 0.16f), SpriteEffects.None, 0f);
            sb.Draw(sg, arrowPos + dir * 26f - Main.screenPosition, null,
                DivineWoodPalette.BrightCore * (0.5f * arrowGlow), 0f,
                sg.Size() * 0.5f, 0.22f + 0.18f * c, SpriteEffects.None, 0f);
        }

        // 弓心蓄力辉光 (72% 后转为凝实小核 = 寂静)
        float glowScale = c < 0.72f ? 0.35f + c * 0.5f : 0.62f - (c - 0.72f) * 0.6f;
        float glowInt = 0.35f + c * 0.5f;
        sb.Draw(sg, drawPos - Main.screenPosition, null,
            DivineWoodPalette.Emerald * glowInt, 0f, sg.Size() * 0.5f,
            MathF.Max(glowScale, 0.2f), SpriteEffects.None, 0f);

        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);

        // 释放帧闪光
        if (Released > 0f && Released < 4f) {
            float f = 1f - Released / 4f;
            WeaponVFX.DrawGlowBurst(Owner.MountedCenter + dir * 24f, 1.1f * f,
                DivineWoodPalette.BrightCore * (0.8f * f));
        }
        return false;
    }
}

/// <summary>
/// 神木叶刃箭 - 高速叶矢 (命中播种 1 层生根)。
/// </summary>
public class DivineWoodLeafBolt : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
    }

    public override void SetDefaults() {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.extraUpdates = 1;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.2f, 0.7f, 0.2f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        DivineWoodRoot.AddStack(target, 1);
        for (int i = 0; i < 10; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(5f, 5f), 60, default, 1.8f);
            d.noGravity = true;
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.DivineWood, scale: 0.9f, owner: Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        // 出膛瞬间的小型径向辉光 (满拉释放)
        float release = MathHelper.Clamp((Projectile.timeLeft - 168f) / 12f, 0f, 1f);
        if (release > 0.01f)
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.06f, release * 0.6f,
                new Color(120, 255, 140), 6f);

        // 绿芯双层 ribbon 拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 8f,
            outerColor: new Color(20, 110, 55, 160), innerColor: new Color(170, 255, 150, 210),
            tex: ACMAsset.LightShot, uvScroll: -Main.GlobalTimeWrappedHourly * 2.4f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        Texture2D sg = ACMAsset.SoftGlow;

        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(100, 255, 120),
            Projectile.rotation, lsh.Size() * 0.5f,
            new Vector2(0.85f, 0.20f), SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(80, 220, 90) * 0.75f, 0f,
            sg.Size() * 0.5f,
            0.45f, SpriteEffects.None, 0);

        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);
        return false;
    }

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f }, Projectile.Center);
        for (int i = 0; i < 8; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(4f, 4f), 60, default, 1.5f);
            d.noGravity = true;
        }
    }
}

/// <summary>
/// 螺旋叶刃 - 齐射护卫叶刃, 初期螺旋后追踪 (命中播种 1 层)。
/// </summary>
public class DivineWoodSpiralLeaf : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/BlankStar";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 10;
    }

    private bool _homing;
    private float _spiralTimer;
    private const float SPIRAL_DURATION = 40f;

    public override void SetDefaults() {
        Projectile.width = 20;
        Projectile.height = 20;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override void AI() {
        _spiralTimer++;
        Projectile.rotation += 0.25f;

        if (!_homing && _spiralTimer < SPIRAL_DURATION) {
            float baseAngle = Projectile.ai[0];
            float spiralAngle = baseAngle + _spiralTimer * 0.15f;
            float spiralRadius = MathHelper.Lerp(40f, 8f, _spiralTimer / SPIRAL_DURATION);
            Vector2 spiralOffset = new Vector2(spiralRadius, 0).RotatedBy(spiralAngle);
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = new Vector2(-forward.Y, forward.X);
            Projectile.Center += perp * spiralOffset.X * 0.15f;
        }
        else {
            _homing = true;
            float closestDist = 600f;
            int targetIdx = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < closestDist) { closestDist = d; targetIdx = i; }
            }
            if (targetIdx >= 0) {
                Vector2 dir = Projectile.DirectionTo(Main.npc[targetIdx].Center);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 18f, 0.12f);
            }
        }

        Lighting.AddLight(Projectile.Center, 0.15f, 0.5f, 0.15f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        DivineWoodRoot.AddStack(target, 1);
        for (int i = 0; i < 6; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(4f, 4f), 60, default, 1.5f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 细双层 ribbon 拖尾 (螺旋叶刃)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 4f,
            outerColor: new Color(20, 110, 55, 140), innerColor: new Color(170, 255, 150, 200),
            uvScroll: -Main.GlobalTimeWrappedHourly * 2f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D star = ACMAsset.BlankStar;
        Texture2D sg = ACMAsset.SoftGlow;
        float pulse = 0.6f + 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 0.18f);

        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            new Color(80, 255, 100) * (0.85f * pulse),
            Projectile.rotation, star.Size() * 0.5f,
            0.60f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(140, 255, 160) * (0.65f * pulse), 0f,
            sg.Size() * 0.5f,
            0.50f, SpriteEffects.None, 0);

        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);
        return false;
    }
}

/// <summary>
/// 贯林矢 - 满蓄巨箭: 高速穿透, 沿途荆棘尾迹, 命中每个敌人都引爆生根。
/// </summary>
public class DivineWoodPierceArrow : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 18;
    }

    public override void SetDefaults() {
        Projectile.width = 22;
        Projectile.height = 22;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 8;
        Projectile.timeLeft = 150;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.extraUpdates = 3;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 12;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.35f, 0.9f, 0.4f);

        // 荆棘尾迹粒子
        if (Main.rand.NextBool(2)) {
            Vector2 perp = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X).SafeNormalize(Vector2.Zero);
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                perp * Main.rand.NextFloat(-2.5f, 2.5f), 60, default, 1.5f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        int consumed = DivineWoodRoot.TriggerBloom(Projectile.GetSource_OnHit(target), target,
            Projectile.damage, 5f, Projectile.owner);
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.DivineWood, scale: 1.2f + consumed * 0.05f, owner: Projectile.owner);
        WeaponVFX.AddScreenShake(target.Center, consumed > 0 ? 3.5f : 2f);
        SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.7f, Pitch = 0.2f }, target.Center);
    }

    public override bool PreDraw(ref Color lightColor) {
        // 宽幅双层 ribbon 拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 16f,
            outerColor: new Color(20, 110, 55, 190), innerColor: new Color(185, 255, 165, 235),
            tex: ACMAsset.LightShot, uvScroll: -Main.GlobalTimeWrappedHourly * 3f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D star = ACMAsset.BlankStar;

        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            DivineWoodPalette.BrightCore, Projectile.rotation,
            lsh.Size() * 0.5f, new Vector2(1.5f, 0.34f), SpriteEffects.None, 0);
        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            DivineWoodPalette.Emerald * 0.8f, Projectile.rotation * 2f,
            star.Size() * 0.5f, 0.5f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            DivineWoodPalette.Emerald * 0.8f, 0f, sg.Size() * 0.5f, 0.65f, SpriteEffects.None, 0);

        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);
        return false;
    }

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.8f, Pitch = -0.1f }, Projectile.Center);
        for (int i = 0; i < 16; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(6f, 6f), 40, default, 2f);
            d.noGravity = true;
        }
    }
}
