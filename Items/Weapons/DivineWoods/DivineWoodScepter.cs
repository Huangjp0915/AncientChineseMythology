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
/// 神木灵杖 - 法师法杖，向鼠标方向释放一道藤蔓链鞭
/// 藤蔓逐节延伸（使用原版NettleBurst纹理风格的链式渲染）
/// 每一节藤蔓都是独立伤害源，末端命中时释放叶片爆发
/// 藤蔓整体连接玩家与末端，形成一条抽打式的绿色链鞭
/// </summary>
public class DivineWoodScepter : ModItem
{
    public override void SetDefaults() {
        Item.damage = 155;
        Item.crit = 12;
        Item.DamageType = DamageClass.Magic;
        Item.width = 36;
        Item.height = 36;
        Item.useTime = 26;
        Item.useAnimation = 26;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 5f;
        Item.value = Item.buyPrice(gold: 50);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<DivineWoodVineWhipHead>();
        Item.shootSpeed = 18f;
        Item.mana = 14;
        Item.channel = true;
        Item.staff[Type] = true;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.7f, Pitch = 0.2f }, player.Center);
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
/// 藤蔓链鞭头部 - 法杖释放的藤蔓前端
/// 飞行时在身后留下链式藤蔓段，命中后释放叶片爆发
/// 使用链式绘制连接玩家与头部之间的藤蔓节段
/// </summary>
public class DivineWoodVineWhipHead : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/SoftGlow";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 20;
    }

    private ref float Timer => ref Projectile.ai[0];

    public override void SetDefaults() {
        Projectile.width = 20;
        Projectile.height = 20;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 145;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 6;
    }

    public override void AI() {
        Timer++;
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead) { Projectile.Kill(); return; }

        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.velocity *= 0.96f;

        // 轻微追踪
        float closestDist = 350f;
        int targetIdx = -1;
        for (int i = 0; i < Main.maxNPCs; i++) {
            NPC npc = Main.npc[i];
            if (!npc.CanBeChasedBy()) continue;
            float d = Vector2.Distance(Projectile.Center, npc.Center);
            if (d < closestDist) { closestDist = d; targetIdx = i; }
        }
        if (targetIdx >= 0) {
            Vector2 dir = Projectile.DirectionTo(Main.npc[targetIdx].Center);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * Projectile.velocity.Length(), 0.04f);
        }

        // 藤蔓尖端粒子
        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                DustID.JungleTorch, -Projectile.velocity * 0.1f, 60, default, 1.3f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.2f, 0.7f, 0.2f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 360);
        target.AddBuff(BuffID.Venom, 180);

        // 命中释放叶片爆发
        for (int i = 0; i < 8; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(6f, 6f), 40, default, 2f);
            d.noGravity = true;
        }

        // 释放4片追踪叶子
        if (Projectile.owner == Main.myPlayer) {
            for (int i = 0; i < 4; i++) {
                Vector2 leafVel = Main.rand.NextVector2CircularEdge(6f, 6f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center,
                    leafVel, ModContent.ProjectileType<DivineWoodVineBurstLeaf>(),
                    Projectile.damage / 3, 1f, Projectile.owner);
            }
        }

        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.DivineWood, scale: 0.9f, owner: Projectile.owner);

        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = 0.5f }, target.Center);
    }

    public override void OnKill(int timeLeft) {
        for (int i = 0; i < 12; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
                Main.rand.NextVector2Circular(4f, 4f), 80, default, 1.5f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        Player owner = Main.player[Projectile.owner];

        // === 连续藤蔓带：玩家中心 → 弹幕头部 (双层 ribbon 取代逐节链贴图) ===
        Vector2 start = owner.MountedCenter;
        Vector2 end = Projectile.Center;
        Vector2 diff = end - start;
        float totalDist = diff.Length();
        Vector2 direction = diff.SafeNormalize(Vector2.Zero);
        Vector2 perp = new(-direction.Y, direction.X);
        int segmentCount = Math.Max(2, (int)(totalDist / 16f));

        var vine = new Vector2[segmentCount + 1];
        for (int i = 0; i <= segmentCount; i++) {
            float progress = (float)i / segmentCount;
            float wave = MathF.Sin(progress * MathF.PI * 2f + Timer * 0.15f) * 12f * (1f - progress);
            vine[i] = start + direction * (totalDist * progress) + perp * wave;
        }
        WeaponVFX.DrawRibbonTrail(vine, baseWidth: 16f,
            outerColor: new Color(20, 110, 55, 170), innerColor: new Color(170, 255, 150, 210),
            tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);

        // === 头部特效绘制 ===
        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D sg = ACMAsset.SoftGlow;
        Texture2D sparkle = ACMAsset.Sparkle;
        Texture2D slash = ACMAsset.SlashBurst;

        float pulse = 0.6f + 0.2f * MathF.Sin(Timer * 0.25f);

        // 头部发光核心
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(60, 220, 80) * (0.65f * pulse), 0f,
            sg.Size() * 0.5f,
            0.55f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(180, 255, 190) * (0.35f * pulse), 0f,
            sg.Size() * 0.5f,
            0.30f, SpriteEffects.None, 0);

        // 头部尖端叶爆 (SlashBurst 放射) + Sparkle
        for (int k = 0; k < 4; k++) {
            float bAngle = Projectile.rotation + k * MathHelper.PiOver2 + Timer * 0.04f;
            sb.Draw(slash, Projectile.Center - Main.screenPosition, null,
                new Color(120, 255, 150) * (0.40f * pulse), bAngle,
                new Vector2(slash.Width * 0.5f, slash.Height),
                new Vector2(0.10f, 0.32f), SpriteEffects.None, 0);
        }
        sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
            new Color(100, 255, 120) * (0.45f * pulse),
            Timer * 0.08f,
            sparkle.Size() * 0.5f,
            0.25f, SpriteEffects.None, 0);

        // 沿藤身发光节点
        for (int i = 0; i <= segmentCount; i += 3) {
            float progress = (float)i / segmentCount;
            float wave = MathF.Sin(progress * MathF.PI * 2f + Timer * 0.15f) * 12f * (1f - progress);
            Vector2 glowPos = start + direction * (totalDist * progress) + perp * wave - Main.screenPosition;
            sb.Draw(sg, glowPos, null,
                new Color(60, 200, 70) * (0.20f * pulse), 0f,
                sg.Size() * 0.5f,
                0.18f, SpriteEffects.None, 0);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}

/// <summary>
/// 藤蔓叶爆 - 藤蔓命中后释放的追踪叶片
/// 使用原版Leaf纹理，追踪最近敌人
/// </summary>
public class DivineWoodVineBurstLeaf : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Leaf;

    private float _timer;

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 5;
    }

    public override void SetDefaults() {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 90;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation += 0.22f * Projectile.direction;
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 5) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        if (_timer > 15) {
            float closestDist = 450f;
            int targetIdx = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < closestDist) { closestDist = d; targetIdx = i; }
            }
            if (targetIdx >= 0) {
                Vector2 dir = Projectile.DirectionTo(Main.npc[targetIdx].Center);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 14f, 0.08f);
            }
        }
        else {
            Projectile.velocity *= 0.94f;
        }

        if (Main.rand.NextBool(4)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
                -Projectile.velocity * 0.05f, 100, default, 0.8f);
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.1f, 0.3f, 0.1f);
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
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        int fh = tex.Height / Main.projFrames[Type];
        Rectangle src = new(0, Projectile.frame * fh, tex.Width, fh);
        Vector2 origin = new(tex.Width / 2f, fh / 2f);
        Color tint = Color.Lerp(lightColor, new Color(100, 255, 120), 0.35f);
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            tint, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
        return false;
    }
}
