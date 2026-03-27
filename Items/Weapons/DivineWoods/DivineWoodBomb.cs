using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Celestias.Boss.Dryades.Items;

namespace AncientChineseMythology.Items.Weapons.DivineWoods;

/// <summary>
/// 神木种子弹 - 抛掷弧线种子，碰撞后爆炸绽放
/// 爆炸使用SlashBurst+SoftGlow+Sparkle多层叠加，释放8道追踪藤蔓碎片
/// </summary>
public class DivineWoodBomb : ModItem
{
    public override void SetDefaults() {
        Item.damage = 200;
        Item.crit = 12;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 30;
        Item.height = 30;
        Item.useTime = 26;
        Item.useAnimation = 26;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 8f;
        Item.value = Item.buyPrice(gold: 50);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<DivineWoodSeedGrenade>();
        Item.shootSpeed = 14f;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        // 上抛弧线
        Vector2 launchVel = velocity + new Vector2(0, -3f);
        Projectile.NewProjectile(source, position, launchVel, type, damage, knockback, player.whoAmI);
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
/// 神木种子手雷 - 弧线飞行，碰撞或超时后爆炸
/// 使用SoftGlow做飞行时的呼吸光晕
/// </summary>
public class DivineWoodSeedGrenade : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/DivineWoods/DivineWoodBomb";

    private ref float AiTimer => ref Projectile.ai[0];

    public override void SetDefaults() {
        Projectile.width = 20;
        Projectile.height = 20;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        AiTimer++;
        Projectile.velocity.Y += 0.22f;
        Projectile.rotation += Projectile.velocity.X * 0.04f;
        Lighting.AddLight(Projectile.Center, 0.2f, 0.6f, 0.2f);

        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
            -Projectile.velocity * 0.08f, 60, default, 1.2f);
        d.noGravity = true;

        // 超时爆炸
        if (AiTimer > 120) Explode();
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        Explode();
    }

    public override void OnKill(int timeLeft) {
        Explode();
    }

    private void Explode() {
        if (Projectile.ai[1] != 0) return; // 防止重复爆炸
        Projectile.ai[1] = 1;

        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = 0.3f }, Projectile.Center);

        if (Main.myPlayer == Projectile.owner) {
            // 爆炸VFX弹幕
            Projectile.NewProjectile(
                Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<DivineWoodBloomExplosion>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner);

            // 8道追踪藤蔓碎片
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8;
                Vector2 fragVel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 10f);
                Projectile.NewProjectile(
                    Projectile.GetSource_Death(), Projectile.Center, fragVel,
                    ModContent.ProjectileType<DivineWoodVineShard>(),
                    Projectile.damage / 3, 2f, Projectile.owner);
            }
        }

        for (int i = 0; i < 30; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(10f, 10f);
            Dust boom = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                vel, 40, default, Main.rand.NextFloat(2f, 3.5f));
            boom.noGravity = true;
        }

        if (Main.player[Projectile.owner].whoAmI == Main.myPlayer) {
            Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 10);
        }

        Projectile.Kill();
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
        Texture2D sg = ACMAsset.SoftGlow;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        float pulse = 0.45f + 0.12f * MathF.Sin(AiTimer * 0.25f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(60, 220, 70) * 0.55f, 0f,
            sg.Size() * 0.5f,
            pulse, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            lightColor, Projectile.rotation,
            tex.Size() * 0.5f,
            Projectile.scale, SpriteEffects.None, 0);
        return false;
    }
}

/// <summary>
/// 自然绽放爆炸 - 种子爆炸的视觉效果弹幕
/// 使用SlashBurst做放射状藤蔓爆发，SoftGlow做扩散光环，Sparkle做花瓣装饰
/// </summary>
public class DivineWoodBloomExplosion : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private ref float Timer => ref Projectile.ai[0];

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 50;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Timer++;
        float radius = Timer * 12f;

        for (int i = 0; i < 5; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.4f, radius);
            Dust d = Dust.NewDustPerfect(pos, DustID.JungleTorch,
                Main.rand.NextVector2Circular(1f, 1f), 60, default, 1.5f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.4f, 1.2f, 0.4f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 600);
        target.AddBuff(BuffID.Venom, 300);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float radius = Timer * 12f;
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
    }

    public override bool PreDraw(ref Color lightColor) {
        float prog = 1f - Projectile.timeLeft / 50f;
        float alpha = ACMUtils.QuadOut(1f - prog) * 0.90f;
        float scale = MathHelper.SmoothStep(0f, 14f, ACMUtils.QuadOut(prog));

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D sparkle = ACMAsset.Sparkle;
        Texture2D star = ACMAsset.BlankStar;

        // 放射状藤蔓爆发 - 8向
        for (int k = 0; k < 8; k++) {
            float bAngle = k * MathF.PI / 4f + Timer * 0.02f;
            bool cardinal = (k % 2 == 0);
            Color bColor = cardinal ? new Color(40, 200, 60) : new Color(160, 255, 180);
            float bLen = cardinal ? scale * 0.60f : scale * 0.38f;
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                bColor * (alpha * 0.80f),
                bAngle,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.14f, bLen), SpriteEffects.None, 0);
        }

        // 外层扩散光环
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(60, 210, 70) * (alpha * 0.45f), 0f,
            sg.Size() * 0.5f,
            scale * 0.55f, SpriteEffects.None, 0);

        // 中心白核闪光
        float flashAlpha = MathHelper.SmoothStep(1.1f, 0f, prog * 1.5f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 230) * (alpha * flashAlpha), 0f,
            sg.Size() * 0.5f,
            scale * 0.20f, SpriteEffects.None, 0);

        // BlankStar花朵旋转
        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            new Color(100, 255, 120) * (alpha * 0.55f),
            Timer * 0.08f,
            star.Size() * 0.5f,
            scale * 0.12f, SpriteEffects.None, 0);

        // Sparkle花瓣装饰
        sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
            new Color(180, 255, 100) * (alpha * 0.45f),
            -Timer * 0.05f,
            sparkle.Size() * 0.5f,
            scale * 0.18f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 藤蔓碎片 - 爆炸后释放的追踪碎片
/// 使用BlankStar渲染，追踪最近敌人
/// </summary>
public class DivineWoodVineShard : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/BlankStar";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 8;
    }

    public override void SetDefaults() {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 90;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        Projectile.rotation += 0.2f;

        if (Projectile.timeLeft < 70) {
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
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 14f, 0.10f);
            }
        }

        Lighting.AddLight(Projectile.Center, 0.12f, 0.35f, 0.12f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 180);
        for (int i = 0; i < 4; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(3f, 3f), 60, default, 1.2f);
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

        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.45f;
            sb.Draw(sg,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(50, 180, 60) * a, 0f,
                sg.Size() * 0.5f,
                0.25f, SpriteEffects.None, 0);
        }

        float pulse = 0.55f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.22f);
        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            new Color(80, 230, 90) * (0.75f * pulse),
            Projectile.rotation, star.Size() * 0.5f,
            0.45f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(120, 255, 130) * (0.50f * pulse), 0f,
            sg.Size() * 0.5f,
            0.30f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
