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
/// 神木灵杖 - 法师法杖，发射自然能量弹，落点生成藤蔓漩涡持续伤害
/// 能量弹飞行时吸附周围叶片粒子，命中后在目标位置绽放藤蔓漩涡
/// 漩涡持续牵引并伤害附近敌人，施加中毒+毒液
/// </summary>
public class DivineWoodScepter : ModItem
{
    public override void SetDefaults() {
        Item.damage = 155;
        Item.crit = 12;
        Item.DamageType = DamageClass.Magic;
        Item.width = 36;
        Item.height = 36;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 5f;
        Item.value = Item.buyPrice(gold: 50);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<DivineWoodNatureBolt>();
        Item.shootSpeed = 16f;
        Item.mana = 12;
        Item.staff[Type] = true;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
        SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f }, player.Center);
        return false;
    }
}

/// <summary>
/// 自然能量弹 - 飞行时吸附叶片粒子，命中后在落点生成藤蔓漩涡
/// 使用BlankStar + SoftGlow渲染，拖尾使用LightShot
/// </summary>
public class DivineWoodNatureBolt : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/BlankStar";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
    }

    public override void SetDefaults() {
        Projectile.width = 24;
        Projectile.height = 24;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.light = 0.6f;
    }

    public override void AI() {
        Projectile.rotation += 0.15f;

        // 轻微追踪最近目标
        float closestDist = 400f;
        int targetIdx = -1;
        for (int i = 0; i < Main.maxNPCs; i++) {
            NPC npc = Main.npc[i];
            if (!npc.CanBeChasedBy()) continue;
            float d = Vector2.Distance(Projectile.Center, npc.Center);
            if (d < closestDist) { closestDist = d; targetIdx = i; }
        }
        if (targetIdx >= 0) {
            Vector2 dir = Projectile.DirectionTo(Main.npc[targetIdx].Center);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * Projectile.velocity.Length(), 0.03f);
        }

        // 吸附叶片粒子
        if (Main.rand.NextBool(2)) {
            Vector2 offset = Main.rand.NextVector2CircularEdge(40f, 40f);
            Vector2 dustVel = (Projectile.Center - (Projectile.Center + offset)).SafeNormalize(Vector2.Zero) * 3f;
            Dust d = Dust.NewDustPerfect(Projectile.Center + offset, DustID.JungleTorch,
                dustVel, 60, default, 1.2f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.25f, 0.8f, 0.25f);
    }

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.9f, Pitch = 0.4f }, Projectile.Center);

        if (Main.myPlayer == Projectile.owner) {
            Projectile.NewProjectile(
                Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<DivineWoodVineVortex>(),
                Projectile.damage, Projectile.knockBack * 0.5f, Projectile.owner);
        }

        for (int i = 0; i < 20; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                vel, 40, default, Main.rand.NextFloat(1.5f, 2.5f));
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D star = ACMAsset.BlankStar;
        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D lsh = ACMAsset.LightShot;

        // LightShot拖尾
        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.50f;
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(50, 200, 70) * a, Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.40f + i * 0.012f, 0.12f), SpriteEffects.None, 0);
        }

        // SoftGlow拖尾灯带
        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.40f;
            sb.Draw(sg,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(70, 220, 80) * a, 0f,
                sg.Size() * 0.5f,
                0.50f, SpriteEffects.None, 0);
        }

        float pulse = 0.7f + 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 0.20f);

        // BlankStar主体双层
        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            new Color(80, 255, 100) * (0.85f * pulse),
            Projectile.rotation, star.Size() * 0.5f,
            1.10f, SpriteEffects.None, 0);
        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 210) * (0.45f * pulse),
            Projectile.rotation + MathHelper.PiOver4,
            star.Size() * 0.5f,
            0.70f, SpriteEffects.None, 0);

        // SoftGlow核心
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(140, 255, 160) * (0.75f * pulse), 0f,
            sg.Size() * 0.5f,
            0.70f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 藤蔓漩涡 - 自然能量弹落点生成的持续伤害区域
/// 牵引附近敌人并施加中毒+毒液
/// 使用SoftGlow + Sparkle + ElectricArcSheet做旋转漩涡光效
/// </summary>
public class DivineWoodVineVortex : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private ref float Timer => ref Projectile.ai[0];

    public override void SetDefaults() {
        Projectile.width = 140;
        Projectile.height = 140;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 90;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 12;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Timer++;

        float progress = Timer / 90f;

        // 牵引附近敌人
        for (int i = 0; i < Main.maxNPCs; i++) {
            NPC npc = Main.npc[i];
            if (!npc.CanBeChasedBy()) continue;
            float dist = Vector2.Distance(Projectile.Center, npc.Center);
            if (dist < 300f && dist > 20f) {
                Vector2 pull = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * 2.5f;
                npc.velocity += pull;
            }
        }

        Lighting.AddLight(Projectile.Center, 0.3f * (1f - progress), 1.0f * (1f - progress), 0.3f * (1f - progress));

        // 旋转叶片粒子
        for (int i = 0; i < 4; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = Main.rand.NextFloat(20f, 80f) * (1f - progress * 0.4f);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius;
            Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * 2.5f;
            vel = vel.RotatedBy(MathHelper.PiOver2 * 0.6f); // 螺旋向心
            Dust d = Dust.NewDustPerfect(pos, DustID.JungleTorch, vel, 40, new Color(60, 200, 60), Main.rand.NextFloat(1.5f, 2.5f));
            d.noGravity = true;
        }

        // 周期性藤蔓爆发
        if (Timer % 12 == 0) {
            for (int i = 0; i < 6; i++) {
                float vineAngle = MathHelper.TwoPi * i / 6 + Timer * 0.06f;
                Vector2 vineVel = vineAngle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                Dust vine = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades, vineVel, 60, default, 2f);
                vine.noGravity = true;
            }
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 480);
        target.AddBuff(BuffID.Venom, 240);
    }

    public override bool PreDraw(ref Color lightColor) {
        float progress = Timer / 90f;
        float opacity = ACMUtils.QuadOut(1f - progress) * 0.85f;

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D sparkle = ACMAsset.Sparkle;
        Texture2D arc = ACMAsset.ElectricArcSheet;

        // ElectricArcSheet 藤蔓漩涡旋转
        int row = (int)(Main.timeForVisualEffects / 7) % 4;
        Rectangle arcFrame = new(0, row * (arc.Height / 4), arc.Width, arc.Height / 4);
        float vortexPulse = 0.7f + 0.3f * MathF.Sin(Timer * 0.15f);
        sb.Draw(arc, Projectile.Center - Main.screenPosition, arcFrame,
            new Color(50, 200, 70) * (opacity * 0.55f * vortexPulse),
            Timer * 0.04f,
            new Vector2(arcFrame.Width * 0.5f, arcFrame.Height * 0.5f),
            1.2f, SpriteEffects.None, 0);
        sb.Draw(arc, Projectile.Center - Main.screenPosition, arcFrame,
            new Color(140, 255, 160) * (opacity * 0.30f * vortexPulse),
            -Timer * 0.03f + MathHelper.PiOver4,
            new Vector2(arcFrame.Width * 0.5f, arcFrame.Height * 0.5f),
            0.85f, SpriteEffects.None, 0);

        // SoftGlow 扩散光环
        float scale = 1.5f + progress * 0.5f;
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(60, 220, 70) * (opacity * 0.50f), 0f,
            sg.Size() * 0.5f,
            scale, SpriteEffects.None, 0);

        // 中心SoftGlow白核
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 210) * (opacity * 0.65f), 0f,
            sg.Size() * 0.5f,
            scale * 0.35f, SpriteEffects.None, 0);

        // Sparkle花瓣旋转装饰
        for (int i = 0; i < 4; i++) {
            float angle = MathHelper.TwoPi * i / 4 + Timer * 0.05f;
            Color petalColor = Color.Lerp(new Color(80, 255, 100), new Color(200, 255, 80),
                MathF.Sin(angle) * 0.5f + 0.5f) * opacity * 0.50f;
            petalColor.A = 0;
            sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
                petalColor, angle,
                sparkle.Size() * 0.5f,
                0.30f + progress * 0.15f, SpriteEffects.None, 0);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
