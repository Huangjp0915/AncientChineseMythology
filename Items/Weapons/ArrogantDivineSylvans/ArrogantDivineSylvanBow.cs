using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Celestias.Boss.Dazhengs.Items;
using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Weapons.DivineWoods;

namespace AncientChineseMythology.Items.Weapons.ArrogantDivineSylvans;

/// <summary>
/// 傲世神木·穿林弓 - 神木长弓的终极形态
/// 每射释放一道主箭 + 4道DNA双螺旋叶刃
/// 每第4射释放「世界树之矢」巨型穿透弹幕
/// 主箭无限穿透，命中敌人锁定减速
/// </summary>
public class ArrogantDivineSylvanBow : ModItem
{
    private int shotCounter;

    public override void SetDefaults() {
        Item.damage = 1400;
        Item.crit = 28;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 28;
        Item.height = 60;
        Item.useTime = 14;
        Item.useAnimation = 14;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 7f;
        Item.value = Item.buyPrice(gold: 500);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item5;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<ArrogantSylvanLeafLance>();
        Item.shootSpeed = 20f;
        Item.useAmmo = AmmoID.Arrow;
    }

    public override Vector2? HoldoutOffset() => new Vector2(-2, 0);

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
        type = ModContent.ProjectileType<ArrogantSylvanLeafLance>();
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        shotCounter++;

        // 主箭
        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);

        // 4道螺旋叶刃
        for (int i = 0; i < 4; i++) {
            float offsetAngle = (i < 2 ? -1 : 1) * MathHelper.ToRadians(15 + (i % 2) * 10);
            Vector2 perturbedVel = velocity.RotatedBy(offsetAngle) * 0.85f;
            Projectile.NewProjectile(source, position, perturbedVel,
                ModContent.ProjectileType<ArrogantSylvanSpiralLeaf>(),
                (int)(damage * 0.45f), knockback * 0.3f, player.whoAmI,
                ai0: MathHelper.TwoPi * i / 4, ai1: 0);
        }

        // 每4射释放世界树之矢
        if (shotCounter % 4 == 0) {
            Projectile.NewProjectile(source, position, velocity * 0.8f,
                ModContent.ProjectileType<ArrogantSylvanWorldTreeArrow>(),
                (int)(damage * 2.5f), knockback * 2f, player.whoAmI);
            SoundEngine.PlaySound(SoundID.Item92 with { Volume = 1.2f, Pitch = -0.3f }, player.Center);
            // 世界树之矢触发技 → 短暂金翠染屏定调 (占全屏唯一名额, 同屏≤1 自动仲裁)
            ArrogantSylvanScreenTint.Spawn(source, player.Center, player.whoAmI);
        }

        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<ArrogantDivineSylvan>(15)
            .AddIngredient<DivineWoodLongbow>()
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
}

/// <summary>
/// 傲世叶枪主箭 - 高速穿透，命中减速锁定
/// </summary>
public class ArrogantSylvanLeafLance : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 18;
    }

    public override void SetDefaults() {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 200;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.extraUpdates = 2;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 6;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.4f, 1.0f, 0.3f);

        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                -Projectile.velocity * 0.05f, 40, default, 1.8f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 600);
        target.AddBuff(BuffID.Venom, 300);
        target.velocity *= 0.4f;
        for (int i = 0; i < 15; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(7f, 7f), 40, default, 2.5f);
            d.noGravity = true;
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.ArrogantSylvan, scale: 1.2f, owner: Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;

        // 主箭金翠流光束核 (ACMShaders.DrawBeam: 金芯 + 翠边)
        Vector2 bdir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        ACMShaders.DrawBeam(Projectile.Center - bdir * 90f, Projectile.Center + bdir * 12f,
            halfWidth: 9f, core: new Color(255, 230, 130, 220), edge: new Color(120, 220, 120, 0),
            intensity: 0.95f, flowSpeed: 1.8f, flowScale: 2.2f, coreSharp: 2.2f);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        Texture2D sg = ACMAsset.SoftGlow;

        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.70f;
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(220, 255, 100) * a, Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.60f + i * 0.016f, 0.18f), SpriteEffects.None, 0);
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(255, 255, 220) * (a * 0.30f), Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.30f, 0.09f), SpriteEffects.None, 0);
        }

        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 120), Projectile.rotation,
            lsh.Size() * 0.5f,
            new Vector2(1.1f, 0.28f), SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 100) * 0.80f, 0f,
            sg.Size() * 0.5f,
            0.55f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f }, Projectile.Center);
        for (int i = 0; i < 12; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(6f, 6f), 40, default, 2f);
            d.noGravity = true;
        }
    }
}

/// <summary>
/// 傲世螺旋叶刃 - DNA双螺旋飞行后追踪敌人
/// </summary>
public class ArrogantSylvanSpiralLeaf : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/BlankStar";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
    }

    private bool _homing;
    private float _spiralTimer;
    private const float SPIRAL_DURATION = 35f;

    public override void SetDefaults() {
        Projectile.width = 22;
        Projectile.height = 22;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 200;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override void AI() {
        _spiralTimer++;
        Projectile.rotation += 0.30f;

        if (!_homing && _spiralTimer < SPIRAL_DURATION) {
            float baseAngle = Projectile.ai[0];
            float spiralAngle = baseAngle + _spiralTimer * 0.18f;
            float spiralRadius = MathHelper.Lerp(50f, 6f, _spiralTimer / SPIRAL_DURATION);
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = new(-forward.Y, forward.X);
            Vector2 spiralOffset = new Vector2(spiralRadius, 0).RotatedBy(spiralAngle);
            Projectile.Center += perp * spiralOffset.X * 0.18f;
        }
        else {
            _homing = true;
            float closestDist = 800f;
            int targetIdx = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < closestDist) { closestDist = d; targetIdx = i; }
            }
            if (targetIdx >= 0) {
                Vector2 dir = Projectile.DirectionTo(Main.npc[targetIdx].Center);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 22f, 0.14f);
            }
        }

        Lighting.AddLight(Projectile.Center, 0.2f, 0.6f, 0.2f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 480);
        target.AddBuff(BuffID.Venom, 240);
        for (int i = 0; i < 8; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(5f, 5f), 40, default, 2f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;

        // 螺旋叶刃金翠双层 ribbon (§B.1)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 12f,
            outerColor: new Color(200, 150, 40, 150), innerColor: new Color(190, 255, 150, 200),
            uvScroll: -(float)Main.timeForVisualEffects * 0.05f);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D star = ACMAsset.BlankStar;
        Texture2D sg = ACMAsset.SoftGlow;
        float pulse = 0.65f + 0.22f * MathF.Sin((float)Main.timeForVisualEffects * 0.2f);

        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.55f;
            sb.Draw(sg,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(220, 255, 100) * a, 0f,
                sg.Size() * 0.5f,
                0.40f, SpriteEffects.None, 0);
        }

        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 120) * (0.90f * pulse),
            Projectile.rotation, star.Size() * 0.5f,
            0.70f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 255, 200) * (0.55f * pulse), 0f,
            sg.Size() * 0.5f,
            0.55f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 世界树之矢 - 每4射释放的巨型穿透弹幕
/// 超大体积、无限穿透、扩散范围伤害
/// </summary>
public class ArrogantSylvanWorldTreeArrow : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 24;
    }

    public override void SetDefaults() {
        Projectile.width = 40;
        Projectile.height = 40;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 120;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 6;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.velocity *= 0.985f;
        Lighting.AddLight(Projectile.Center, 0.8f, 2f, 0.6f);

        for (int i = 0; i < 5; i++) {
            Dust d = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(25, 25),
                DustID.JungleTorch, -Projectile.velocity * 0.15f, 30, default, 2.5f);
            d.noGravity = true;
        }

        // 途经地面时产生藤蔓粒子
        for (int i = 0; i < 2; i++) {
            Vector2 side = Projectile.Center + new Vector2(Main.rand.NextFloat(-30, 30), Main.rand.NextFloat(-30, 30));
            Dust leafDust = Dust.NewDustPerfect(side, DustID.GrassBlades,
                Main.rand.NextVector2Circular(2f, 2f), 80, default, 1.5f);
            leafDust.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 900);
        target.AddBuff(BuffID.Venom, 600);
        target.velocity *= 0.2f;

        for (int i = 0; i < 25; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(10f, 10f), 30, default, 3f);
            d.noGravity = true;
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.ArrogantSylvan, scale: 1.4f, owner: Projectile.owner);

        if (Projectile.owner == Main.myPlayer && Main.rand.NextBool(2)) {
            for (int i = 0; i < 6; i++) {
                Vector2 fragVel = Main.rand.NextVector2CircularEdge(8f, 8f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center,
                    fragVel, ModContent.ProjectileType<ArrogantSylvanSpiralLeaf>(),
                    Projectile.damage / 4, 2f, Projectile.owner,
                    ai0: MathHelper.TwoPi * i / 6);
            }
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;

        // 世界树之矢: 加粗金翠流光束核 (ACMShaders.DrawBeam)
        Vector2 wdir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        ACMShaders.DrawBeam(Projectile.Center - wdir * 150f, Projectile.Center + wdir * 24f,
            halfWidth: 20f, core: new Color(255, 235, 150, 230), edge: new Color(120, 230, 120, 0),
            intensity: 1f, flowSpeed: 1.6f, flowScale: 2f, coreSharp: 2f);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D sparkle = ACMAsset.Sparkle;

        // 华丽拖尾
        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.72f;
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(220, 255, 100) * a, Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.80f + i * 0.02f, 0.30f), SpriteEffects.None, 0);
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(40, 200, 60) * (a * 0.50f), Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.50f, 0.15f), SpriteEffects.None, 0);
        }

        // 主体巨箭
        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(255, 255, 180), Projectile.rotation,
            lsh.Size() * 0.5f,
            new Vector2(1.6f, 0.45f), SpriteEffects.None, 0);
        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 100) * 0.70f, Projectile.rotation,
            lsh.Size() * 0.5f,
            new Vector2(2.0f, 0.60f), SpriteEffects.None, 0);

        // 前端光芒
        Vector2 front = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 30f;
        sb.Draw(sg, front - Main.screenPosition, null,
            new Color(255, 255, 200) * 0.85f, 0f,
            sg.Size() * 0.5f,
            0.80f, SpriteEffects.None, 0);
        sb.Draw(sparkle, front - Main.screenPosition, null,
            new Color(255, 255, 180) * 0.65f,
            (float)Main.timeForVisualEffects * 0.1f,
            sparkle.Size() * 0.5f,
            0.60f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
