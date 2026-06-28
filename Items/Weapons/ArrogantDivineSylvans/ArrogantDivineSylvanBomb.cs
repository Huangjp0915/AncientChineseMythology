using AncientChineseMythology.Celestias.Boss.Dazhengs.Items;
using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Weapons.DivineWoods;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.ArrogantDivineSylvans;

/// <summary>
/// 傲世神木·世界种 - 神木种子弹的终极形态
/// 抛出巨型种子，飞行中途分裂为5颗子种子
/// 每颗种子碰撞后产生巨型绽放爆炸 + 16道追踪藤蔓蛇
/// 爆炸留下持续伤害荆棘领域
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
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<ArrogantSylvanWorldSeed>();
        Item.shootSpeed = 16f;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Vector2 launchVel = velocity + new Vector2(0, -4f);
        Projectile.NewProjectile(source, position, launchVel, type, damage, knockback, player.whoAmI);
        return false;
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
/// 世界种主弹 - 飞行中途分裂为5颗子种
/// </summary>
public class ArrogantSylvanWorldSeed : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/ArrogantDivineSylvans/ArrogantDivineSylvanBomb";

    private ref float AiTimer => ref Projectile.ai[0];
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
        Lighting.AddLight(Projectile.Center, 0.4f, 1.0f, 0.3f);

        for (int i = 0; i < 2; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                -Projectile.velocity * 0.12f, 40, default, 1.8f);
            d.noGravity = true;
        }

        // 飞行40帧后分裂
        if (!_hasSplit && AiTimer > 40 && Main.myPlayer == Projectile.owner) {
            _hasSplit = true;
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 1f, Pitch = 0.3f }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                float angle = MathHelper.TwoPi * i / 5;
                Vector2 splitVel = Projectile.velocity.RotatedBy(MathHelper.ToRadians(-30 + 15 * i)) * 0.8f;
                splitVel.Y -= 2f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, splitVel,
                    ModContent.ProjectileType<ArrogantSylvanChildSeed>(),
                    Projectile.damage, Projectile.knockBack, Projectile.owner);
            }
            for (int i = 0; i < 20; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                    Main.rand.NextVector2Circular(8f, 8f), 40, default, 2.5f);
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
        if (Projectile.ai[1] != 0) return;
        Projectile.ai[1] = 1;

        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.3f, Pitch = 0.1f }, Projectile.Center);
        // 世界种主爆命中演出 (金翠 scale 2) + 重击震屏
        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
            ACMWeaponBurst.ArrogantSylvan, scale: 2f, owner: Projectile.owner);
        WeaponVFX.AddScreenShake(Projectile.Center, 10f);

        if (Main.myPlayer == Projectile.owner) {
            Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<ArrogantSylvanBloomExplosion>(),
                (int)(Projectile.damage * 1.5f), Projectile.knockBack, Projectile.owner);

            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16;
                Vector2 fragVel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 14f);
                Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, fragVel,
                    ModContent.ProjectileType<ArrogantSylvanVineSerpent>(),
                    Projectile.damage / 3, 3f, Projectile.owner);
            }
        }

        for (int i = 0; i < 50; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(14f, 14f);
            Dust boom = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                vel, 30, default, Main.rand.NextFloat(2.5f, 4f));
            boom.noGravity = true;
        }

        if (Main.player[Projectile.owner].whoAmI == Main.myPlayer)
            Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(10, 15);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
        Texture2D sg = ACMAsset.SoftGlow;

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        float pulse = 0.55f + 0.18f * MathF.Sin(AiTimer * 0.22f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 100) * 0.65f, 0f,
            sg.Size() * 0.5f,
            pulse, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 255, 200) * 0.30f, 0f,
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
/// 子种子 - 分裂后的小种子，碰撞后各自爆炸
/// </summary>
public class ArrogantSylvanChildSeed : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/ArrogantDivineSylvans/ArrogantDivineSylvanBomb";

    private ref float AiTimer => ref Projectile.ai[0];

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

        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
            -Projectile.velocity * 0.08f, 50, default, 1.2f);
        d.noGravity = true;

        if (AiTimer > 100) Explode();
    }

    public override void OnKill(int timeLeft) {
        Explode();
    }

    private void Explode() {
        if (Projectile.ai[1] != 0) return;
        Projectile.ai[1] = 1;

        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = 0.4f }, Projectile.Center);
        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
            ACMWeaponBurst.ArrogantSylvan, scale: 1.2f, owner: Projectile.owner);

        if (Main.myPlayer == Projectile.owner) {
            Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<ArrogantSylvanBloomExplosion>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner);

            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8;
                Vector2 fragVel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 12f);
                Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, fragVel,
                    ModContent.ProjectileType<ArrogantSylvanVineSerpent>(),
                    Projectile.damage / 4, 2f, Projectile.owner);
            }
        }

        for (int i = 0; i < 30; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(12f, 12f);
            Dust boom = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                vel, 30, default, Main.rand.NextFloat(2f, 3.5f));
            boom.noGravity = true;
        }

        if (Main.player[Projectile.owner].whoAmI == Main.myPlayer)
            Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 10);

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

        float pulse = 0.40f + 0.10f * MathF.Sin(AiTimer * 0.3f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 100) * 0.55f, 0f,
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
/// 傲世绽放爆炸 - 巨型圆形爆炸场
/// 12道放射状SlashBurst + 多层光环
/// </summary>
public class ArrogantSylvanBloomExplosion : ModProjectile
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
        Projectile.timeLeft = 65;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Timer++;
        float radius = Timer * 16f;

        for (int i = 0; i < 8; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.3f, radius);
            Dust d = Dust.NewDustPerfect(pos, DustID.JungleTorch,
                Main.rand.NextVector2Circular(2f, 2f), 40, default, 2.5f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.6f, 1.8f, 0.5f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 900);
        target.AddBuff(BuffID.Venom, 600);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float radius = Timer * 16f;
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
    }

    public override bool PreDraw(ref Color lightColor) {
        float prog = 1f - Projectile.timeLeft / 65f;
        float alpha = ACMUtils.QuadOut(1f - prog) * 0.92f;
        float scale = MathHelper.SmoothStep(0f, 18f, ACMUtils.QuadOut(prog));

        // === 世界种绽放 set-piece ===
        // 1) 大型一次性金翠径向泛光 (起爆瞬间最强, 占全屏名额, 名额满自动退化柔光)
        if (prog < 0.55f) {
            float bell = (float)System.Math.Sin(System.Math.Min(prog / 0.55f, 1f) * System.Math.PI);
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.16f + 0.12f * prog, bell * 0.9f,
                new Color(230, 235, 120), 12f);
        }
        // 2) 荆棘领域生长溶解 (DissolveBurn 喂 Sparkle 放射纹, 噪声 clip + 金灼边)
        {
            Texture2D thorn = ACMAsset.Sparkle;
            if (thorn != null) {
                float grow = System.Math.Min(prog / 0.4f, 1f);     // 生长进度
                float domainScale = scale * 0.30f;
                WeaponVFX.ApplyDissolveBurn(thorn, Projectile.Center, null,
                    new Color(120, 220, 110) * (alpha * 0.9f), Projectile.ai[0] * 0.04f,
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
            Color bColor = major ? new Color(220, 255, 100) : new Color(40, 200, 60);
            float bLen = major ? scale * 0.70f : scale * 0.42f;
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                bColor * (alpha * 0.82f), bAngle,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.16f, bLen), SpriteEffects.None, 0);
        }

        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 80) * (alpha * 0.50f), 0f,
            sg.Size() * 0.5f,
            scale * 0.60f, SpriteEffects.None, 0);

        float flashAlpha = MathHelper.SmoothStep(1.2f, 0f, prog * 1.4f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 255, 230) * (alpha * flashAlpha), 0f,
            sg.Size() * 0.5f,
            scale * 0.22f, SpriteEffects.None, 0);

        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            new Color(255, 255, 180) * (alpha * 0.55f),
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
/// 傲世藤蔓蛇 - 追踪型碎片弹幕，更强的追踪力和伤害
/// </summary>
public class ArrogantSylvanVineSerpent : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/BlankStar";

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
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 18f, 0.14f);
            }
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
        for (int i = 0; i < 6; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(5f, 5f), 40, default, 2f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;

        // 藤蔓蛇金翠双层 ribbon (§B.1)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 11f,
            outerColor: new Color(200, 150, 40, 150), innerColor: new Color(190, 255, 150, 200),
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
            new Color(200, 255, 100) * (0.80f * pulse),
            Projectile.rotation, star.Size() * 0.5f,
            0.55f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 255, 180) * (0.45f * pulse), 0f,
            sg.Size() * 0.5f,
            0.35f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
