using AncientChineseMythology.Celestias.Boss.Dryades.Items;
using AncientChineseMythology.Helpers;
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
/// 神木典籍 - 法师魔法书，每次使用释放8~12片叶片扇形散射
/// 叶片使用原版Leaf纹理，初期扇形散射后螺旋飞行，然后追踪最近敌人
/// 叶片命中后有概率释放次生小花瓣
/// </summary>
public class DivineWoodTome : ModItem
{
    public override void SetDefaults() {
        Item.damage = 165;
        Item.crit = 10;
        Item.DamageType = DamageClass.Magic;
        Item.width = 28;
        Item.height = 32;
        Item.useTime = 24;
        Item.useAnimation = 24;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 4f;
        Item.value = Item.buyPrice(gold: 50);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<DivineWoodTomeLeaf>();
        Item.shootSpeed = 12f;
        Item.mana = 12;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        int count = Main.rand.Next(8, 13);
        float spreadHalf = MathHelper.ToRadians(35);
        float baseAngle = velocity.ToRotation();

        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = 0.3f }, player.Center);

        for (int i = 0; i < count; i++) {
            float angle = baseAngle + MathHelper.Lerp(-spreadHalf, spreadHalf, (float)i / (count - 1));
            angle += Main.rand.NextFloat(-0.05f, 0.05f);
            float speed = velocity.Length() * Main.rand.NextFloat(0.85f, 1.15f);
            Vector2 leafVel = angle.ToRotationVector2() * speed;
            // ai[0] = 螺旋方向 (交替左右)
            float spiralDir = i % 2 == 0 ? 1f : -1f;
            Projectile.NewProjectile(source, position, leafVel, type, damage, knockback,
                player.whoAmI, ai0: spiralDir);
        }

        // 释放叶片尘雾
        for (int i = 0; i < 15; i++) {
            Vector2 dustVel = velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.6f) * Main.rand.NextFloat(2f, 6f);
            Dust d = Dust.NewDustPerfect(position, DustID.GrassBlades, dustVel, 80, default, 1.5f);
            d.noGravity = true;
        }

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
/// 神木叶刃 - 使用原版Leaf纹理的叶片弹幕
/// 初期直线飞行+螺旋偏移，然后追踪最近敌人
/// 命中时有40%概率释放次生花瓣
/// </summary>
public class DivineWoodTomeLeaf : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Leaf;

    private float _timer;
    private const float SpiralDuration = 25f;
    private const float HomingDuration = 120f;

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 5;
    }

    public override void SetDefaults() {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 5) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        if (_timer < SpiralDuration) {
            // 螺旋偏移阶段：在垂直于飞行方向上施加正弦偏移
            float spiralDir = Projectile.ai[0];
            float spiralForce = MathF.Sin(_timer * 0.3f) * spiralDir * 0.8f;
            Vector2 perpendicular = new(-Projectile.velocity.Y, Projectile.velocity.X);
            perpendicular = perpendicular.SafeNormalize(Vector2.Zero);
            Projectile.velocity += perpendicular * spiralForce;
            Projectile.velocity *= 0.98f;
        }
        else {
            // 追踪阶段
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
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 16f, 0.06f);
            }
        }

        // 叶片粒子拖尾
        if (Main.rand.NextBool(3)) {
            Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
                -Projectile.velocity * 0.05f, 100, default, 0.9f);
            trail.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.1f, 0.3f, 0.1f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 240);

        for (int i = 0; i < 6; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(4f, 4f), 60, default, 1.5f);
            d.noGravity = true;
        }

        // 40%概率释放次生花瓣
        if (Main.rand.NextBool(5, 12) && Projectile.owner == Main.myPlayer) {
            Vector2 petalVel = Main.rand.NextVector2CircularEdge(5f, 5f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center,
                petalVel, ModContent.ProjectileType<DivineWoodTomePetal>(),
                Projectile.damage / 2, 1f, Projectile.owner);
        }

        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.DivineWood, scale: 0.7f, owner: Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        // 统一翡翠 SoftGlow 外发光 (每片叶子)
        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.5f, new Color(90, 230, 120) * 0.6f);

        Texture2D tex = TextureAssets.Projectile[Type].Value;
        int fh = tex.Height / Main.projFrames[Type];
        Rectangle src = new(0, Projectile.frame * fh, tex.Width, fh);
        Vector2 origin = new(tex.Width / 2f, fh / 2f);

        Color tint = Color.Lerp(lightColor, new Color(120, 255, 140), 0.3f);
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            tint, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

        Color glow = new Color(80, 220, 100) * 0.25f;
        glow.A = 0;
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            glow, Projectile.rotation, origin, Projectile.scale * 1.3f, SpriteEffects.None, 0);

        return false;
    }
}

/// <summary>
/// 次生花瓣 - 叶片命中时概率释放的小花瓣
/// 使用原版FlowerPetal纹理，追踪附近敌人
/// </summary>
public class DivineWoodTomePetal : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPetal;

    private float _timer;

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 3;
    }

    public override void SetDefaults() {
        Projectile.width = 12;
        Projectile.height = 12;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 80;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 5) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        // 前15帧减速，然后追踪
        if (_timer < 15) {
            Projectile.velocity *= 0.92f;
        }
        else {
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

        if (Main.rand.NextBool(4)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                Vector2.Zero, 100, default, 0.7f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 180);
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        int fh = tex.Height / Main.projFrames[Type];
        Rectangle src = new(0, Projectile.frame * fh, tex.Width, fh);
        Vector2 origin = new(tex.Width / 2f, fh / 2f);
        Color tint = Color.Lerp(lightColor, new Color(180, 255, 200), 0.3f);
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            tint, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
        return false;
    }
}
