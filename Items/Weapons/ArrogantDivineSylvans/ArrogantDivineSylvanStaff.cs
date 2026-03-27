using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Celestias.Boss.Dazhengs.Items;
using AncientChineseMythology.Items.Weapons.DivineWoods;

namespace AncientChineseMythology.Items.Weapons.ArrogantDivineSylvans;

/// <summary>
/// 傲世神木·万藤杖 - 神木灵杖的终极形态
/// 释放一道巨型藤蔓链鞭，比原版更长更粗壮
/// 藤蔓具有更强的追踪能力，命中时释放8片追踪叶爆
/// 藤蔓末端到达最远距离后触发「藤蔓新星」：环形16道爆炸
/// 沿链身每3节产生分支藤蔓触手，独立造成伤害
/// </summary>
public class ArrogantDivineSylvanStaff : ModItem
{
    public override void SetDefaults() {
        Item.damage = 1400;
        Item.crit = 24;
        Item.DamageType = DamageClass.Magic;
        Item.width = 40;
        Item.height = 40;
        Item.useTime = 22;
        Item.useAnimation = 22;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 8f;
        Item.value = Item.buyPrice(gold: 500);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<ArrogantSylvanVineWhipHead>();
        Item.shootSpeed = 24f;
        Item.mana = 18;
        Item.channel = true;
        Item.staff[Type] = true;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
        Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.9f, Pitch = 0.1f }, player.Center);
        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<ArrogantDivineSylvan>(15)
            .AddIngredient<DivineWoodScepter>()
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
}

/// <summary>
/// 傲世藤蔓链鞭头部 - 巨型链鞭前端
/// 更强追踪 + 更高穿透 + 命中时8叶爆发
/// 末端触发藤蔓新星：16道SlashBurst扩散爆炸
/// 链式绘制带金翠双色发光节点
/// </summary>
public class ArrogantSylvanVineWhipHead : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 28;
    }

    private ref float Timer => ref Projectile.ai[0];
    private ref float NovaTriggered => ref Projectile.ai[1];

    public override void SetDefaults() {
        Projectile.width = 24;
        Projectile.height = 24;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 160;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 5;
    }

    public override void AI() {
        Timer++;
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead) { Projectile.Kill(); return; }

        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.velocity *= 0.955f;

        // 更强追踪
        float closestDist = 500f;
        int targetIdx = -1;
        for (int i = 0; i < Main.maxNPCs; i++) {
            NPC npc = Main.npc[i];
            if (!npc.CanBeChasedBy()) continue;
            float d = Vector2.Distance(Projectile.Center, npc.Center);
            if (d < closestDist) { closestDist = d; targetIdx = i; }
        }
        if (targetIdx >= 0) {
            Vector2 dir = Projectile.DirectionTo(Main.npc[targetIdx].Center);
            float speed = Projectile.velocity.Length();
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * speed, 0.07f);
        }

        // 速度过低时触发藤蔓新星
        if (Projectile.velocity.Length() < 2f && NovaTriggered == 0) {
            TriggerVineNova();
            NovaTriggered = 1;
        }

        // 尖端粒子 - 双色
        for (int i = 0; i < 2; i++) {
            int dustType = i == 0 ? DustID.JungleTorch : DustID.GoldFlame;
            Dust d = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(10, 10),
                dustType, -Projectile.velocity * 0.08f, 50, default, 1.6f);
            d.noGravity = true;
        }

        // 沿链身每间隔段生成分支触手弹幕
        if (Timer % 16 == 0 && Timer > 10 && Timer < 100 && Projectile.owner == Main.myPlayer) {
            Vector2 diff = Projectile.Center - owner.MountedCenter;
            float totalDist = diff.Length();
            if (totalDist > 80f) {
                Vector2 direction = diff.SafeNormalize(Vector2.Zero);
                int tendrils = Math.Min(4, (int)(totalDist / 100f));
                for (int t = 0; t < tendrils; t++) {
                    float p = (t + 1f) / (tendrils + 1f);
                    Vector2 spawnPos = owner.MountedCenter + direction * (totalDist * p);
                    Vector2 perpDir = new(-direction.Y, direction.X);
                    float side = t % 2 == 0 ? 1f : -1f;
                    Vector2 tendrilVel = perpDir * side * 10f + direction * 3f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawnPos, tendrilVel,
                        ModContent.ProjectileType<ArrogantSylvanTendril>(),
                        Projectile.damage / 4, 2f, Projectile.owner);
                }
            }
        }

        Lighting.AddLight(Projectile.Center, 0.35f, 0.9f, 0.3f);
    }

    private void TriggerVineNova() {
        SoundEngine.PlaySound(SoundID.Item17 with { Volume = 1.2f, Pitch = 0.2f }, Projectile.Center);

        if (Projectile.owner == Main.myPlayer) {
            // 16道藤蔓新星爆发
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<ArrogantSylvanVineNova>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner);

            // 释放12片追踪叶刃
            for (int i = 0; i < 12; i++) {
                Vector2 leafVel = Main.rand.NextVector2CircularEdge(9f, 9f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                    leafVel, ModContent.ProjectileType<ArrogantSylvanVineBurstLeaf>(),
                    Projectile.damage / 3, 2f, Projectile.owner);
            }
        }

        if (Main.player[Projectile.owner].whoAmI == Main.myPlayer)
            Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(10, 14);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 600);
        target.AddBuff(BuffID.Venom, 300);

        for (int i = 0; i < 12; i++) {
            int dustType = i % 2 == 0 ? DustID.JungleTorch : DustID.GoldFlame;
            Dust d = Dust.NewDustPerfect(target.Center, dustType,
                Main.rand.NextVector2Circular(7f, 7f), 30, default, 2.2f);
            d.noGravity = true;
        }

        // 命中释放8片追踪叶子
        if (Projectile.owner == Main.myPlayer) {
            for (int i = 0; i < 8; i++) {
                Vector2 leafVel = Main.rand.NextVector2CircularEdge(7f, 7f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center,
                    leafVel, ModContent.ProjectileType<ArrogantSylvanVineBurstLeaf>(),
                    Projectile.damage / 3, 1.5f, Projectile.owner);
            }
        }

        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f, Pitch = 0.4f }, target.Center);
    }

    public override void OnKill(int timeLeft) {
        for (int i = 0; i < 16; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
                Main.rand.NextVector2Circular(5f, 5f), 60, default, 2f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Player owner = Main.player[Projectile.owner];

        // === 链式藤蔓绘制 ===
        Texture2D vineTex = TextureAssets.Chains[13].Value;
        Vector2 start = owner.MountedCenter;
        Vector2 end = Projectile.Center;
        Vector2 diff = end - start;
        float totalDist = diff.Length();
        Vector2 direction = diff.SafeNormalize(Vector2.Zero);
        float segmentLen = vineTex.Height / 2;
        int segmentCount = (int)(totalDist / segmentLen);
        float chainRot = direction.ToRotation() + MathHelper.PiOver2;

        for (int i = 0; i < segmentCount; i++) {
            float progress = (float)i / Math.Max(segmentCount, 1);
            Vector2 segPos = start + direction * (i * segmentLen);
            float wave = MathF.Sin(progress * MathF.PI * 2.5f + Timer * 0.15f) * 16f * (1f - progress);
            Vector2 perp = new(-direction.Y, direction.X);
            Vector2 drawPos = segPos + perp * wave - Main.screenPosition;
            Color segColor = Color.Lerp(new Color(80, 200, 80), new Color(220, 255, 100), progress * 0.5f);
            sb.Draw(vineTex, drawPos, null, segColor * 0.92f, chainRot, vineTex.Size() * 0.5f,
                1.2f, SpriteEffects.None, 0);
        }

        // === 头部特效 ===
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D sparkle = ACMAsset.Sparkle;

        float pulse = 0.7f + 0.25f * MathF.Sin(Timer * 0.22f);

        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 100) * (0.70f * pulse), 0f,
            sg.Size() * 0.5f, 0.70f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 255, 220) * (0.40f * pulse), 0f,
            sg.Size() * 0.5f, 0.35f, SpriteEffects.None, 0);

        sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 120) * (0.50f * pulse),
            Timer * 0.08f,
            sparkle.Size() * 0.5f, 0.30f, SpriteEffects.None, 0);

        // 沿链身金翠发光节点
        for (int i = 0; i < segmentCount; i += 2) {
            float progress = (float)i / Math.Max(segmentCount, 1);
            Vector2 segPos = start + direction * (i * segmentLen);
            float wave = MathF.Sin(progress * MathF.PI * 2.5f + Timer * 0.15f) * 16f * (1f - progress);
            Vector2 perp = new(-direction.Y, direction.X);
            Vector2 glowPos = segPos + perp * wave - Main.screenPosition;
            Color glowCol = i % 4 == 0 ? new Color(220, 255, 100) : new Color(40, 200, 60);
            sb.Draw(sg, glowPos, null,
                glowCol * (0.25f * pulse), 0f,
                sg.Size() * 0.5f, 0.22f, SpriteEffects.None, 0);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 傲世分支触手 - 链身分支的小型藤蔓弹幕
/// 追踪附近敌人，命中后消失
/// </summary>
public class ArrogantSylvanTendril : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    private float _timer;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 8;
    }

    public override void SetDefaults() {
        Projectile.width = 12;
        Projectile.height = 12;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 80;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation = Projectile.velocity.ToRotation();

        if (_timer > 10) {
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
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 16f, 0.10f);
            }
        }
        else {
            Projectile.velocity *= 0.96f;
        }

        Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
            -Projectile.velocity * 0.04f, 70, default, 1.1f);
        trail.noGravity = true;
        Lighting.AddLight(Projectile.Center, 0.1f, 0.4f, 0.1f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 300);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.45f;
            sb.Draw(lsh,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(40, 200, 60) * a, Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.30f, 0.06f), SpriteEffects.None, 0);
        }

        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(80, 230, 90), Projectile.rotation,
            lsh.Size() * 0.5f,
            new Vector2(0.40f, 0.08f), SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 傲世藤蔓新星 - 链鞭到达最远端时释放的环形爆炸
/// 16道SlashBurst + 扩散伤害场
/// </summary>
public class ArrogantSylvanVineNova : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 60;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Projectile.ai[0]++;
        float radius = Projectile.ai[0] * 18f;

        for (int i = 0; i < 10; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.4f, radius);
            int dustType = i % 3 == 0 ? DustID.GoldFlame : DustID.JungleTorch;
            Dust d = Dust.NewDustPerfect(pos, dustType,
                (pos - Projectile.Center).SafeNormalize(Vector2.Zero) * 2f, 40, default, 2.2f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.5f, 1.3f, 0.4f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 600);
        target.AddBuff(BuffID.Venom, 300);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float radius = Projectile.ai[0] * 18f;
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
    }

    public override bool PreDraw(ref Color lightColor) {
        float prog = 1f - Projectile.timeLeft / 60f;
        float alpha = ACMUtils.QuadOut(1f - prog) * 0.92f;
        float scale = MathHelper.SmoothStep(0f, 20f, ACMUtils.QuadOut(prog));

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D sparkle = ACMAsset.Sparkle;

        // 16道放射藤蔓新星
        for (int k = 0; k < 16; k++) {
            float bAngle = k * MathF.PI / 8f + Projectile.ai[0] * 0.03f;
            float bLen = k % 2 == 0 ? scale * 0.65f : scale * 0.42f;
            Color bColor = k % 3 == 0
                ? new Color(220, 255, 100)
                : k % 3 == 1
                    ? new Color(40, 200, 60)
                    : new Color(255, 255, 220);
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                bColor * (alpha * 0.75f), bAngle,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.12f, bLen), SpriteEffects.None, 0);
        }

        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 100) * (alpha * 0.55f), 0f,
            sg.Size() * 0.5f, scale * 0.55f, SpriteEffects.None, 0);

        float flashAlpha = MathHelper.SmoothStep(1f, 0f, prog * 1.3f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 255, 230) * (alpha * flashAlpha), 0f,
            sg.Size() * 0.5f, scale * 0.22f, SpriteEffects.None, 0);

        sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 140) * (alpha * 0.55f),
            Projectile.ai[0] * 0.06f,
            sparkle.Size() * 0.5f, scale * 0.28f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 傲世藤蔓叶爆 - 链鞭命中后释放的追踪叶片
/// 使用Leaf纹理，更强追踪 + 更长存活
/// </summary>
public class ArrogantSylvanVineBurstLeaf : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Leaf;

    private float _timer;

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 5;
    }

    public override void SetDefaults() {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 120;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation += 0.24f * Projectile.direction;
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 4) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        if (_timer > 12) {
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
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 18f, 0.10f);
            }
        }
        else {
            Projectile.velocity *= 0.93f;
        }

        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
                -Projectile.velocity * 0.04f, 80, default, 1f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.12f, 0.35f, 0.12f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 300);
        target.AddBuff(BuffID.Venom, 120);
        for (int i = 0; i < 5; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(4f, 4f), 50, default, 1.5f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        int fh = tex.Height / Main.projFrames[Type];
        Rectangle src = new(0, Projectile.frame * fh, tex.Width, fh);
        Vector2 origin = new(tex.Width / 2f, fh / 2f);
        Color tint = Color.Lerp(lightColor, new Color(220, 255, 160), 0.40f);
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            tint, Projectile.rotation, origin, Projectile.scale * 1.3f, SpriteEffects.None, 0);
        return false;
    }
}
