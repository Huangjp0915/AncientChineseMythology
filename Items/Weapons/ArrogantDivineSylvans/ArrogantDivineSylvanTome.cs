using AncientChineseMythology.Celestias.Boss.Dazhengs.Items;
using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Weapons.DivineWoods;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.ArrogantDivineSylvans;

/// <summary>
/// 傲世神木·山海典 - 神木典籍的终极形态
/// 每次使用释放20~28片金翠叶片扇形散射
/// 叶片具有更长的螺旋飞行阶段和更强的追踪能力
/// 命中有50%概率释放次生追踪花瓣
/// 每第3次使用触发「叶暴漩涡」：持续存在的范围伤害旋风
/// </summary>
public class ArrogantDivineSylvanTome : ModItem
{
    private int useCounter;

    public override void SetDefaults() {
        Item.damage = 1500;
        Item.crit = 22;
        Item.DamageType = DamageClass.Magic;
        Item.width = 32;
        Item.height = 36;
        Item.useTime = 20;
        Item.useAnimation = 20;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 7f;
        Item.value = Item.buyPrice(gold: 500);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<ArrogantSylvanTomeLeaf>();
        Item.shootSpeed = 16f;
        Item.mana = 16;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
        Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        useCounter++;
        int count = Main.rand.Next(20, 29);
        float spreadHalf = MathHelper.ToRadians(45);
        float baseAngle = velocity.ToRotation();

        SoundEngine.PlaySound(SoundID.Grass with { Volume = 1f, Pitch = 0.2f }, player.Center);

        for (int i = 0; i < count; i++) {
            float angle = baseAngle + MathHelper.Lerp(-spreadHalf, spreadHalf, (float)i / (count - 1));
            angle += Main.rand.NextFloat(-0.04f, 0.04f);
            float speed = velocity.Length() * Main.rand.NextFloat(0.80f, 1.20f);
            Vector2 leafVel = angle.ToRotationVector2() * speed;
            float spiralDir = i % 2 == 0 ? 1f : -1f;
            Projectile.NewProjectile(source, position, leafVel, type, damage, knockback,
                player.whoAmI, ai0: spiralDir);
        }

        // 每第3次使用触发叶暴漩涡
        if (useCounter % 3 == 0 && player.whoAmI == Main.myPlayer) {
            Vector2 vortexPos = player.Center + velocity.SafeNormalize(Vector2.UnitX) * 250f;
            Projectile.NewProjectile(source, vortexPos, Vector2.Zero,
                ModContent.ProjectileType<ArrogantSylvanLeafVortex>(),
                damage * 2, knockback * 2f, player.whoAmI);
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 1f, Pitch = 0.3f }, vortexPos);
            // 叶暴漩涡触发技 → 短暂金翠染屏定调 (占全屏唯一名额, 同屏≤1 自动仲裁)
            ArrogantSylvanScreenTint.Spawn(source, vortexPos, player.whoAmI);
        }

        // 释放叶片尘雾 - 金翠交替
        for (int i = 0; i < 20; i++) {
            Vector2 dustVel = velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.7f) * Main.rand.NextFloat(3f, 8f);
            int dustType = i % 3 == 0 ? DustID.GoldFlame : DustID.GrassBlades;
            Dust d = Dust.NewDustPerfect(position, dustType, dustVel, 60, default, 1.8f);
            d.noGravity = true;
        }

        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<ArrogantDivineSylvan>(15)
            .AddIngredient<DivineWoodTome>()
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
}

/// <summary>
/// 傲世叶刃 - 使用Leaf纹理的升级叶片弹幕
/// 更长的螺旋阶段(30帧) + 更强的追踪(0.10 lerp, 600范围)
/// 命中50%概率释放次生花瓣
/// </summary>
public class ArrogantSylvanTomeLeaf : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Leaf;

    private float _timer;
    private const float SpiralDuration = 30f;

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 5;
    }

    public override void SetDefaults() {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 220;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 4) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        if (_timer < SpiralDuration) {
            float spiralDir = Projectile.ai[0];
            float spiralForce = MathF.Sin(_timer * 0.28f) * spiralDir * 1.0f;
            Vector2 perpendicular = new(-Projectile.velocity.Y, Projectile.velocity.X);
            perpendicular = perpendicular.SafeNormalize(Vector2.Zero);
            Projectile.velocity += perpendicular * spiralForce;
            Projectile.velocity *= 0.97f;
        }
        else {
            float closestDist = 700f;
            int targetIdx = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < closestDist) { closestDist = d; targetIdx = i; }
            }
            if (targetIdx >= 0) {
                Vector2 dir = Projectile.DirectionTo(Main.npc[targetIdx].Center);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 20f, 0.10f);
            }
        }

        if (Main.rand.NextBool(2)) {
            Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
                -Projectile.velocity * 0.04f, 80, default, 1.1f);
            trail.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.15f, 0.4f, 0.12f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 480);
        target.AddBuff(BuffID.Venom, 240);

        for (int i = 0; i < 8; i++) {
            int dustType = i % 2 == 0 ? DustID.JungleTorch : DustID.GoldFlame;
            Dust d = Dust.NewDustPerfect(target.Center, dustType,
                Main.rand.NextVector2Circular(5f, 5f), 40, default, 1.8f);
            d.noGravity = true;
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.ArrogantSylvan, scale: 1f, owner: Projectile.owner);

        // 50%概率释放次生追踪花瓣
        if (Main.rand.NextBool() && Projectile.owner == Main.myPlayer) {
            for (int i = 0; i < 2; i++) {
                Vector2 petalVel = Main.rand.NextVector2CircularEdge(6f, 6f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center,
                    petalVel, ModContent.ProjectileType<ArrogantSylvanTomePetal>(),
                    Projectile.damage / 2, 1.5f, Projectile.owner);
            }
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

        Color glow = new Color(200, 255, 100) * 0.30f;
        glow.A = 0;
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            glow, Projectile.rotation, origin, Projectile.scale * 1.6f, SpriteEffects.None, 0);

        return false;
    }
}

/// <summary>
/// 傲世次生花瓣 - 叶片命中时释放的追踪花瓣
/// 更强追踪更高穿透
/// </summary>
public class ArrogantSylvanTomePetal : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPetal;

    private float _timer;

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 3;
    }

    public override void SetDefaults() {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 120;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 4) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        if (_timer < 12) {
            Projectile.velocity *= 0.93f;
        }
        else {
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
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 18f, 0.12f);
            }
        }

        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                Vector2.Zero, 80, default, 0.9f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 360);
        target.AddBuff(BuffID.Venom, 180);
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        int fh = tex.Height / Main.projFrames[Type];
        Rectangle src = new(0, Projectile.frame * fh, tex.Width, fh);
        Vector2 origin = new(tex.Width / 2f, fh / 2f);
        Color tint = Color.Lerp(lightColor, new Color(220, 255, 200), 0.35f);
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            tint, Projectile.rotation, origin, Projectile.scale * 1.4f, SpriteEffects.None, 0);
        return false;
    }
}

/// <summary>
/// 傲世叶暴漩涡 - 每第3次使用时在前方生成的持续伤害旋风
/// 持续吸引附近敌人并造成范围伤害
/// 使用SoftGlow + SlashBurst做旋转特效
/// </summary>
public class ArrogantSylvanLeafVortex : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private ref float Timer => ref Projectile.ai[0];

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Timer++;
        float radius = MathHelper.Lerp(40f, 200f, Math.Min(Timer / 40f, 1f));

        // 吸引范围内敌人
        for (int i = 0; i < Main.maxNPCs; i++) {
            NPC npc = Main.npc[i];
            if (!npc.CanBeChasedBy()) continue;
            float dist = Vector2.Distance(Projectile.Center, npc.Center);
            if (dist < radius * 2f && dist > 20f) {
                Vector2 pull = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero);
                float pullStrength = MathHelper.Lerp(3f, 0.5f, dist / (radius * 2f));
                npc.velocity += pull * pullStrength;
            }
        }

        // 旋转叶片粒子
        for (int i = 0; i < 4; i++) {
            float angle = Timer * 0.15f + i * MathHelper.PiOver2;
            float r = radius * Main.rand.NextFloat(0.3f, 1f);
            Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
            int dustType = i % 2 == 0 ? DustID.GrassBlades : DustID.JungleTorch;
            Dust d = Dust.NewDustPerfect(dustPos, dustType,
                new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 3f + Main.rand.NextVector2Circular(1f, 1f),
                40, default, 1.8f);
            d.noGravity = true;
        }

        // 金翠高亮粒子
        if (Timer % 8 == 0) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 edgePos = Projectile.Center + angle.ToRotationVector2() * radius;
            Dust glow = Dust.NewDustPerfect(edgePos, DustID.GoldFlame,
                Main.rand.NextVector2Circular(2f, 2f), 30, default, 2f);
            glow.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.4f, 1f, 0.35f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 600);
        target.AddBuff(BuffID.Venom, 300);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float radius = MathHelper.Lerp(40f, 200f, Math.Min(Timer / 40f, 1f));
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
    }

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = -0.2f }, Projectile.Center);

        // 消散时释放大量叶片
        if (Projectile.owner == Main.myPlayer) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(10f, 10f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                    ModContent.ProjectileType<ArrogantSylvanTomeLeaf>(),
                    Projectile.damage / 3, 2f, Projectile.owner, ai0: i % 2 == 0 ? 1f : -1f);
            }
        }

        for (int i = 0; i < 30; i++) {
            Vector2 vel = Main.rand.NextVector2CircularEdge(12f, 12f);
            int dustType = i % 3 == 0 ? DustID.GoldFlame : DustID.GrassBlades;
            Dust d = Dust.NewDustPerfect(Projectile.Center, dustType,
                vel, 30, default, 2.5f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        float radius = MathHelper.Lerp(40f, 200f, Math.Min(Timer / 40f, 1f));
        float fadeAlpha = Projectile.timeLeft < 30 ? Projectile.timeLeft / 30f : 1f;
        float scale = radius / 100f;

        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D burst = ACMAsset.SlashBurst;
        Texture2D sparkle = ACMAsset.Sparkle;

        // 旋转的8道SlashBurst形成漩涡
        for (int k = 0; k < 8; k++) {
            float bAngle = Timer * 0.08f + k * MathHelper.PiOver4;
            float bLen = scale * (0.35f + 0.08f * MathF.Sin(Timer * 0.12f + k));
            Color bColor = k % 2 == 0 ? new Color(220, 255, 100) : new Color(40, 200, 60);
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                bColor * (0.55f * fadeAlpha), bAngle,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.10f, bLen), SpriteEffects.None, 0);
        }

        // 外层柔光环 — 金 (统一金翠双色基调)
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(225, 185, 75) * (0.32f * fadeAlpha), 0f,
            sg.Size() * 0.5f, scale * 1.4f, SpriteEffects.None, 0);
        // 中层柔光环 — 翠
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(120, 230, 110) * (0.30f * fadeAlpha), 0f,
            sg.Size() * 0.5f, scale * 1.0f, SpriteEffects.None, 0);

        // 中心核心
        float pulse = 0.5f + 0.15f * MathF.Sin(Timer * 0.20f);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(255, 255, 220) * (0.45f * pulse * fadeAlpha), 0f,
            sg.Size() * 0.5f, scale * 0.4f, SpriteEffects.None, 0);

        // 旋转Sparkle装饰
        sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
            new Color(220, 255, 140) * (0.40f * fadeAlpha),
            Timer * 0.10f,
            sparkle.Size() * 0.5f, scale * 0.6f, SpriteEffects.None, 0);
        sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
            new Color(40, 200, 60) * (0.30f * fadeAlpha),
            -Timer * 0.07f,
            sparkle.Size() * 0.5f, scale * 0.8f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
