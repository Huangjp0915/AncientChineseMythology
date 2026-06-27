using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Profanes;

/// <summary>
/// 爆裂肉瘤 - 投手类炸弹武器
/// 抛出肉瘤炸弹，重力弧线飞行，碰撞或超时后爆炸
/// 爆炸释放8道追踪血肉碎片 + 大范围VFX
/// 碰弹时弹跳一次再爆炸
/// </summary>
public class BurstingTumorBomb : ModItem
{
    public override void SetDefaults() {
        Item.damage = 1500;
        Item.crit = 8;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 28;
        Item.height = 28;
        Item.useTime = 32;
        Item.useAnimation = 32;
        Item.knockBack = 7f;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.value = Item.buyPrice(gold: 90);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<TumorBombProj>();
        Item.shootSpeed = 10f;
        Item.consumable = false;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
        return false;
    }
}

/// <summary>
/// 肉瘤弹幕 - 弧线飞行的肉块
/// 碰到物块弹跳一次，碰到NPC或弹跳后超时爆炸
/// 使用原版NPC肉块Gore效果飞行拖尾
/// </summary>
public class TumorBombProj : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/Profanes/BurstingTumorBomb";

    private ref float BounceCount => ref Projectile.ai[0];
    private ref float AiTimer => ref Projectile.ai[1];

    public override void SetDefaults() {
        Projectile.width = 124;
        Projectile.height = 124;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 240;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
    }

    public override void AI() {
        AiTimer++;
        Projectile.velocity.Y += 0.35f;
        Projectile.rotation += Projectile.velocity.Length() * 0.04f * Math.Sign(Projectile.velocity.X);
        Lighting.AddLight(Projectile.Center, 0.4f, 0.06f, 0.05f);

        // 血液拖尾粒子
        if (AiTimer % 2 == 0) {
            Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6, 6),
                DustID.Blood, -Projectile.velocity * 0.05f, 0, default, 1.4f);
            d.noGravity = true;
        }

        // Gore肉块拖尾(每6帧)
        if (AiTimer % 6 == 0 && Main.netMode != NetmodeID.Server) {
            int goreType = Main.rand.Next(new int[] { GoreID.Smoke1, GoreID.Smoke2, GoreID.Smoke3 });
            Gore g = Gore.NewGorePerfect(Projectile.GetSource_Death(), Projectile.Center,
                -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(1f, 1f), goreType);
            g.timeLeft = 15;
        }
    }

    public override bool OnTileCollide(Vector2 oldVelocity) {
        if (BounceCount < 1) {
            BounceCount++;
            SoundEngine.PlaySound(SoundID.NPCHit9 with { Volume = 0.6f, Pitch = 0.4f }, Projectile.Center);

            // 弹跳反射
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon)
                Projectile.velocity.X = -oldVelocity.X * 0.5f;
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon)
                Projectile.velocity.Y = -oldVelocity.Y * 0.65f;

            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    Main.rand.NextVector2CircularEdge(4f, 4f), 0, default, 1.5f);
                d.noGravity = true;
            }
            return false;
        }
        // 二次碰撞爆炸
        Explode();
        return true;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        Explode();
    }

    public override void OnKill(int timeLeft) {
        Explode();
    }

    private bool _exploded;
    private void Explode() {
        if (_exploded) return;
        _exploded = true;

        SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 1.2f, Pitch = -0.5f }, Projectile.Center);

        // 大型血肉爆裂演出 (冲击环 + 径向辉光)
        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
            ACMWeaponBurst.Profane, scale: 1.8f, owner: Projectile.owner);

        if (Main.myPlayer == Projectile.owner) {
            // 爆炸VFX弹幕
            Projectile.NewProjectile(
                Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<TumorBlastVFX>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner);

            // 8道追踪血肉碎片
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8 + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 fragVel = angle.ToRotationVector2() * Main.rand.NextFloat(7f, 12f);
                Projectile.NewProjectile(
                    Projectile.GetSource_Death(), Projectile.Center, fragVel,
                    ModContent.ProjectileType<TumorFragment>(),
                    Projectile.damage / 4, 2f, Projectile.owner);
            }
        }

        // 大量血液粒子
        for (int i = 0; i < 40; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(12f, 12f) * Main.rand.NextFloat(0.5f, 1f);
            Dust boom = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                vel, 0, default, Main.rand.NextFloat(2f, 4f));
            boom.noGravity = i < 25;
        }

        // Gore碎块
        if (Main.netMode != NetmodeID.Server) {
            for (int i = 0; i < 6; i++) {
                int goreType = Main.rand.Next(new int[] {
                    GoreID.Smoke1, GoreID.Smoke2, GoreID.Smoke3
                });
                Gore g = Gore.NewGorePerfect(Projectile.GetSource_Death(), Projectile.Center,
                    Main.rand.NextVector2CircularEdge(8f, 8f), goreType);
                g.timeLeft = 30;
            }
        }

        WeaponVFX.AddScreenShake(Projectile.Center, 6f);
        Projectile.Kill();
    }
}

/// <summary>
/// 肉瘤爆炸VFX - SlashBurst放射 + SoftGlow光环
/// 范围AOE伤害
/// </summary>
public class TumorBlastVFX : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private ref float Timer => ref Projectile.ai[0];
    private const int DURATION = 50;

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = DURATION;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 12;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Timer++;
        float radius = Timer * 12f;

        for (int i = 0; i < 8; i++) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.3f, radius);
            Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                Main.rand.NextVector2Circular(2.5f, 2.5f), 0, default, 2f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.7f, 0.12f, 0.1f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 600);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float radius = Timer * 12f;
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
    }

    public override bool PreDraw(ref Color lightColor) {
        float prog = 1f - Projectile.timeLeft / (float)DURATION;
        float alpha = ACMUtils.QuadOut(1f - prog) * 0.90f;
        float scale = MathHelper.SmoothStep(0f, 16f, ACMUtils.QuadOut(prog));

        // 大血肉冲击环 (在自管批之前调用)
        WeaponVFX.DrawShockwaveRing(Projectile.Center, 24f + prog * 220f, 16f, alpha * 0.9f,
            new Color(240, 100, 52), new Color(86, 16, 8));

        // 血肉溶解灼烧边 (用炸弹贴图喂 DissolveBurn)
        Texture2D bombTex = ModContent.Request<Texture2D>(
            "AncientChineseMythology/Items/Weapons/Profanes/BurstingTumorBomb",
            ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
        if (bombTex != null) {
            WeaponVFX.ApplyDissolveBurn(bombTex, Projectile.Center, null,
                new Color(140, 12, 18), 0f, bombTex.Size() * 0.5f, 0.9f + prog * 0.5f,
                threshold: prog, intensity: (1f - prog) * 0.85f,
                edgeColor: new Color(240, 100, 52, 220), edgeWidth: 0.10f, noiseScale: 2.5f);
        }

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D sg = ACMAsset.SoftGlow;

        // SlashBurst放射
        for (int k = 0; k < 10; k++) {
            float bAngle = k * MathF.PI / 5f + Timer * 0.015f;
            bool strong = (k % 2 == 0);
            Color bColor = strong ? new Color(165, 45, 14) : new Color(240, 100, 52);
            float bLen = strong ? scale * 0.60f : scale * 0.40f;
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                bColor * (alpha * 0.70f), bAngle,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.15f, bLen), SpriteEffects.None, 0);
        }

        // 大范围血雾
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(86, 16, 8) * (alpha * 0.40f), 0f,
            sg.Size() * 0.5f,
            scale * 0.55f, SpriteEffects.None, 0);

        // 中心焦点
        float flashAlpha = MathHelper.SmoothStep(1f, 0f, prog * 2f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 200, 180) * (alpha * flashAlpha), 0f,
            sg.Size() * 0.5f,
            scale * 0.15f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 追踪血肉碎片 - 扩散后追踪最近敌人
/// 使用BlankStar暗红色渲染
/// </summary>
public class TumorFragment : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/BlankStar";

    private ref float Timer => ref Projectile.ai[0];
    private const int SPREAD_TIME = 20;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 0;
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
    }

    public override void SetDefaults() {
        Projectile.width = 12;
        Projectile.height = 12;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        Timer++;
        Projectile.rotation += 0.25f;
        Lighting.AddLight(Projectile.Center, 0.25f, 0.04f, 0.03f);

        if (Timer < SPREAD_TIME) {
            Projectile.velocity *= 0.96f;
        }
        else {
            // 追踪最近敌人
            NPC target = null;
            float closest = 900f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.friendly || n.dontTakeDamage) continue;
                float dist = Vector2.Distance(Projectile.Center, n.Center);
                if (dist < closest) { closest = dist; target = n; }
            }

            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 16f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.12f);
            }
            else {
                Projectile.velocity *= 0.97f;
            }
        }

        // 血液拖尾
        if (Timer % 2 == 0) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                -Projectile.velocity * 0.04f, 0, default, 1.2f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 240);
        for (int i = 0; i < 4; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.4f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 统一双层暗红血肉拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 7f,
            outerColor: new Color(86, 16, 8), innerColor: new Color(240, 100, 52),
            uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D star = ACMAsset.BlankStar;
        Texture2D sg = ACMAsset.SoftGlow;

        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            new Color(165, 45, 14), Projectile.rotation,
            star.Size() * 0.5f,
            0.30f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(180, 35, 30) * 0.50f, 0f,
            sg.Size() * 0.5f,
            0.20f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
