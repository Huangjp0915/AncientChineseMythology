using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Profanes;

/// <summary>
/// 凝视肉典 - 法师魔法书类型武器
/// 扇形喷射10道血液弹幕（使用LightShot暗红色渲染），呈散射枪模式
/// 每第4次攻击释放一颗跟踪巨眼弹（EyeLaser纹理），碰撞后爆裂出追踪触手
/// 血粒子+暗红光效
/// </summary>
public class GazingFleshGrimoire : ModItem
{
    private int _castCount;

    public override void SetDefaults() {
        Item.damage = 1300;
        Item.crit = 10;
        Item.DamageType = DamageClass.Magic;
        Item.mana = 14;
        Item.width = 32;
        Item.height = 34;
        Item.useTime = 22;
        Item.useAnimation = 22;
        Item.knockBack = 4f;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.value = Item.buyPrice(gold: 85);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.NPCDeath13 with { Pitch = 0.2f, Volume = 0.7f };
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<GrimoireBloodBolt>();
        Item.shootSpeed = 18f;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        _castCount++;
        Vector2 baseDir = velocity.SafeNormalize(Vector2.UnitX);
        Vector2 muzzle = position + baseDir * 20f;

        if (_castCount % 4 == 0) {
            // 每4次释放巨眼弹
            Projectile.NewProjectile(source, muzzle, velocity * 0.7f,
                ModContent.ProjectileType<GrimoireGazingEye>(),
                damage * 3, knockback * 2.5f, player.whoAmI);
            SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = 0.5f, Volume = 0.6f }, position);
        }

        // 扇形散射10道血液弹
        float spreadAngle = MathHelper.ToRadians(28);
        int bolts = 10;
        for (int i = 0; i < bolts; i++) {
            float angle = MathHelper.Lerp(-spreadAngle, spreadAngle, i / (float)(bolts - 1));
            angle += Main.rand.NextFloat(-0.03f, 0.03f);
            Vector2 boltVel = velocity.RotatedBy(angle) * Main.rand.NextFloat(0.90f, 1.10f);
            Projectile.NewProjectile(source, muzzle, boltVel, type, damage, knockback, player.whoAmI);
        }

        // 施法血液粒子
        for (int i = 0; i < 8; i++) {
            Vector2 dustVel = baseDir.RotatedByRandom(0.6f) * Main.rand.NextFloat(4f, 8f);
            Dust d = Dust.NewDustPerfect(muzzle, DustID.Blood, dustVel, 0, default, 1.8f);
            d.noGravity = true;
        }

        return false;
    }
}

/// <summary>
/// 血液弹 - 高速穿透血柱
/// 暗红色LightShot渲染，短距离消失
/// </summary>
public class GrimoireBloodBolt : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 8;
    }

    public override void SetDefaults() {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 45;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.extraUpdates = 2;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.30f, 0.05f, 0.04f);

        if (Projectile.timeLeft % 3 == 0) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                -Projectile.velocity * 0.04f, 0, default, 1.0f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Ichor, 180);
    }

    public override void OnKill(int timeLeft) {
        for (int i = 0; i < 4; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.3f);
            d.noGravity = true;
        }
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
                null, new Color(180, 20, 15) * a, Projectile.oldRot[i],
                lsh.Size() * 0.5f,
                new Vector2(0.30f, 0.06f), SpriteEffects.None, 0);
        }

        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(220, 40, 30), Projectile.rotation,
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
/// 巨眼弹 - 使用EyeLaser纹理，缓速飞行+轻微追踪
/// 碰撞后爆炸出8道追踪触手(ProfaneTendrilChaser)
/// </summary>
public class GrimoireGazingEye : ModProjectile
{
    public override string Texture
        => "Terraria/Images/Projectile_" + ProjectileID.EyeLaser;

    private ref float Timer => ref Projectile.ai[0];

    public override void SetDefaults() {
        Projectile.width = 20;
        Projectile.height = 20;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        Timer++;
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        Lighting.AddLight(Projectile.Center, 0.5f, 0.08f, 0.06f);

        // 微弱追踪
        if (Timer > 10) {
            NPC target = null;
            float closest = 700f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (!n.active || n.friendly || n.dontTakeDamage) continue;
                float dist = Vector2.Distance(Projectile.Center, n.Center);
                if (dist < closest) { closest = dist; target = n; }
            }

            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Projectile.velocity.Length();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.04f);
            }
        }

        // 血液拖尾
        if (Timer % 2 == 0) {
            Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6, 6),
                DustID.Blood, -Projectile.velocity * 0.05f, 0, default, 1.4f);
            d.noGravity = true;
        }
    }

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);

        if (Main.myPlayer == Projectile.owner) {
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8 + Main.rand.NextFloat(-0.15f, 0.15f);
                Vector2 fVel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 10f);
                Projectile.NewProjectile(
                    Projectile.GetSource_Death(), Projectile.Center, fVel,
                    ModContent.ProjectileType<ProfaneTendrilChaser>(),
                    Projectile.damage / 3, 2f, Projectile.owner);
            }
        }

        // 血液爆发
        for (int i = 0; i < 25; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                vel, 0, default, Main.rand.NextFloat(2f, 3f));
            d.noGravity = true;
        }

        Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(4, 6);
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[ProjectileID.EyeLaser].Value;
        Texture2D sg = ACMAsset.SoftGlow;

        // SoftGlow光晕 (Additive)
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        float pulse = 0.40f + 0.12f * MathF.Sin(Timer * 0.2f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(200, 30, 20) * 0.55f, 0f,
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
