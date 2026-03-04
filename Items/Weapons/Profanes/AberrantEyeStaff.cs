using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Profanes;

/// <summary>
/// 畸变眼球杖 - 法师法杖类型武器(持续施法)
/// 持续释放追踪血肉触手(ProfaneTendrilChaser)
/// 每第6次释放一颗飞行畸变眼球(追踪+碰撞爆炸释放血液弹幕)
/// Channel型持续消耗法力
/// </summary>
public class AberrantEyeStaff : ModItem
{
    private int _fireCount;

    public override void SetDefaults() {
        Item.damage = 1250;
        Item.crit = 8;
        Item.DamageType = DamageClass.Magic;
        Item.mana = 6;
        Item.width = 48;
        Item.height = 48;
        Item.useTime = 12;
        Item.useAnimation = 12;
        Item.knockBack = 3f;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.value = Item.buyPrice(gold: 85);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = null;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.staff[Type] = true;
        Item.shoot = ModContent.ProjectileType<AberrantEyeballProj>();
        Item.shootSpeed = 14f;
        Item.channel = true;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        _fireCount++;
        Vector2 staffTip = position + velocity.SafeNormalize(Vector2.UnitX) * 46f;

        if (_fireCount % 6 == 0) {
            // 每6次释放畸变眼球
            Projectile.NewProjectile(source, staffTip, velocity * 0.8f,
                ModContent.ProjectileType<AberrantEyeballProj>(),
                damage * 3, knockback * 2f, player.whoAmI);
            SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.5f, Pitch = 0.3f }, position);
        }
        else {
            // 释放2道追踪触手
            for (int i = 0; i < 2; i++) {
                Vector2 tVel = velocity.RotatedByRandom(0.35f) * Main.rand.NextFloat(0.85f, 1.15f);
                Projectile.NewProjectile(source, staffTip, tVel,
                    ModContent.ProjectileType<ProfaneTendrilChaser>(),
                    damage / 2, 2f, player.whoAmI);
            }
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.3f, Pitch = 0.6f }, position);
        }

        // 杖尖血液粒子
        for (int i = 0; i < 4; i++) {
            Vector2 dustVel = velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(0.5f)
                * Main.rand.NextFloat(3f, 7f);
            Dust d = Dust.NewDustPerfect(staffTip, DustID.Blood, dustVel, 0, default, 1.5f);
            d.noGravity = true;
        }

        return false;
    }
}

/// <summary>
/// 畸变眼球 - 飞行追踪眼球弹幕
/// 使用EyeLaser纹理，追踪最近敌人
/// 碰撞后爆炸释放环形血液弹幕+大量粒子
/// </summary>
public class AberrantEyeballProj : ModProjectile
{
    public override string Texture
        => "Terraria/Images/Projectile_" + ProjectileID.EyeLaser;

    private ref float Timer => ref Projectile.ai[0];

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
    }

    public override void SetDefaults() {
        Projectile.width = 18;
        Projectile.height = 18;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 240;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        Timer++;
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        Lighting.AddLight(Projectile.Center, 0.45f, 0.07f, 0.05f);

        // 追踪
        if (Timer > 8) {
            NPC target = null;
            float closest = 800f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.friendly || n.dontTakeDamage) continue;
                float dist = Vector2.Distance(Projectile.Center, n.Center);
                if (dist < closest) { closest = dist; target = n; }
            }

            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero)
                    * MathHelper.Max(Projectile.velocity.Length(), 14f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.06f);
            }
        }

        // 血液拖尾
        if (Timer % 2 == 0) {
            Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(5, 5),
                DustID.Blood, -Projectile.velocity * 0.04f, 0, default, 1.3f);
            d.noGravity = true;
        }
    }

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);

        if (Main.myPlayer == Projectile.owner) {
            // 环形血液弹幕(8发)
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8;
                Vector2 bVel = angle.ToRotationVector2() * 9f;
                Projectile.NewProjectile(
                    Projectile.GetSource_Death(), Projectile.Center, bVel,
                    ModContent.ProjectileType<AberrantBloodBurst>(),
                    Projectile.damage / 3, 2f, Projectile.owner);
            }
        }

        // 血液爆发
        for (int i = 0; i < 25; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(7f, 7f);
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                vel, 0, default, Main.rand.NextFloat(1.8f, 3f));
            d.noGravity = true;
        }

        Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(3, 5);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[ProjectileID.EyeLaser].Value;
        Texture2D sg = ACMAsset.SoftGlow;

        // SoftGlow拖尾 (Additive)
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.35f;
            sb.Draw(sg,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(200, 30, 20) * a, 0f,
                sg.Size() * 0.5f,
                0.22f - i * 0.01f, SpriteEffects.None, 0);
        }

        float pulse = 0.35f + 0.10f * MathF.Sin(Timer * 0.25f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(220, 40, 30) * 0.50f, 0f,
            sg.Size() * 0.5f,
            pulse, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        // 原版纹理本体
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            Color.White, Projectile.rotation,
            tex.Size() * 0.5f,
            Projectile.scale, SpriteEffects.None, 0);

        return false;
    }
}

/// <summary>
/// 环形血液弹 - 眼球爆炸后释放的血弹
/// 短距离飞行+穿透，LightShot暗红渲染
/// </summary>
public class AberrantBloodBurst : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 60;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.extraUpdates = 1;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.velocity *= 0.97f;
        Lighting.AddLight(Projectile.Center, 0.25f, 0.04f, 0.03f);

        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                -Projectile.velocity * 0.03f, 0, default, 1.1f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 240);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(200, 30, 20), Projectile.rotation,
            lsh.Size() * 0.5f,
            new Vector2(0.35f, 0.09f), SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
