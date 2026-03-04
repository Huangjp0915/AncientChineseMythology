using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.DivineWoods;

/// <summary>
/// 神木典籍 - 释放5枚花瓣环绕鼠标位置旋转收束，然后追踪最近敌人
/// 参照CelestialCircletScepter的螺旋环绕→追踪冲刺模式
/// </summary>
public class DivineWoodTome : ModItem
{
    public override void SetDefaults() {
        Item.damage = 165;
        Item.crit = 10;
        Item.DamageType = DamageClass.Magic;
        Item.width = 28;
        Item.height = 32;
        Item.useTime = 28;
        Item.useAnimation = 28;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 6f;
        Item.value = Item.buyPrice(gold: 50);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<DivineWoodPetalOrb>();
        Item.shootSpeed = 14f;
        Item.mana = 14;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        int count = 5;
        for (int i = 0; i < count; i++) {
            float angle = MathHelper.TwoPi * i / count;
            Vector2 vel = velocity.RotatedBy(angle - MathHelper.TwoPi * (count / 2) / count * 0.35f);
            Projectile.NewProjectile(source, position, vel, type, damage, knockback,
                player.whoAmI, ai0: angle, ai1: i);
        }
        return false;
    }
}

/// <summary>
/// 神木花瓣光环 - 初期绕鼠标前方旋转收束，然后追踪最近敌人
/// 使用BlankStar + SoftGlow + ElectricArcSheet(藤蔓电弧)渲染
/// </summary>
public class DivineWoodPetalOrb : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/BlankStar";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
    }

    private bool _homing;
    private float _orbitTimer;
    private const float ORBIT_DURATION = 70f;

    public override void SetDefaults() {
        Projectile.width = 28;
        Projectile.height = 28;
        Projectile.friendly = true;
        Projectile.tileCollide = false;
        Projectile.penetrate = 5;
        Projectile.timeLeft = 260;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.light = 0.8f;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override void AI() {
        Player p = Main.player[Projectile.owner];
        _orbitTimer++;

        if (!_homing && _orbitTimer < ORBIT_DURATION) {
            Vector2 center = p.Center + p.DirectionTo(Main.MouseWorld) * 140f;
            float baseAngle = Projectile.ai[0];
            float orbitRadius = MathHelper.Lerp(160f, 40f, _orbitTimer / ORBIT_DURATION);
            float orbitSpeed = MathHelper.Lerp(0.07f, 0.20f, _orbitTimer / ORBIT_DURATION);
            float angle = baseAngle + _orbitTimer * orbitSpeed;
            Vector2 target = center + new Vector2(orbitRadius, 0).RotatedBy(angle);
            Projectile.velocity = (target - Projectile.Center) * 0.18f;
        }
        else {
            _homing = true;
            float closestDist = 800f;
            int targetNPC = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                float dist = Vector2.Distance(Projectile.Center, npc.Center);
                if (dist < closestDist) { closestDist = dist; targetNPC = i; }
            }
            if (targetNPC >= 0) {
                Vector2 dir = Projectile.DirectionTo(Main.npc[targetNPC].Center);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 22f, 0.12f);
            }
        }

        Projectile.rotation += 0.18f;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 300);
        target.AddBuff(BuffID.Venom, 120);
        for (int i = 0; i < 8; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2CircularEdge(5, 5), 0, default, 2.0f);
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

        float pulse = 0.75f + 0.25f * MathF.Sin((float)Main.timeForVisualEffects * 0.20f);

        // SoftGlow拖尾
        for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.55f;
            sb.Draw(sg,
                Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                null, new Color(70, 210, 80) * a, 0f,
                sg.Size() * 0.5f,
                0.65f, SpriteEffects.None, 0);
        }

        // 双层星星主体
        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            new Color(80, 255, 100) * (0.90f * pulse),
            Projectile.rotation, star.Size() * 0.5f,
            1.20f, SpriteEffects.None, 0);
        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            new Color(200, 255, 210) * (0.45f * pulse),
            Projectile.rotation + MathHelper.PiOver4,
            star.Size() * 0.5f,
            0.80f, SpriteEffects.None, 0);

        // SoftGlow核心
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(140, 255, 160) * (0.80f * pulse), 0f,
            sg.Size() * 0.5f,
            0.80f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
