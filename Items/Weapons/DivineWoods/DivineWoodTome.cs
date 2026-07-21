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
/// 神木典籍 - 三连诵法书:
/// 左键第 1/2 诵各 5 片叶刃 (收窄扇形, 命中播种生根), 第 3 诵华盖倾泻 —— 7 叶收束 + 中心穿透莲华 (命中引爆生根);
/// 右键"催花" (耗魔 40, 冷却 5 秒): 以光标为心张开年轮法阵, 引爆域内所有生根敌人。
/// </summary>
public class DivineWoodTome : ModItem
{
    private const uint CallCooldown = 300;   // 5s
    private const uint ComboResetGap = 100;

    private int _castCombo;
    private uint _lastCastFrame;
    private uint _callReadyFrame;

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
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<DivineWoodTomeLeaf>();
        Item.shootSpeed = 12f;
        Item.mana = 12;
    }

    public override bool AltFunctionUse(Player player) => true;

    public override bool CanUseItem(Player player) {
        if (player.altFunctionUse == 2) {
            if (Main.GameUpdateCount < _callReadyFrame)
                return false;
            Item.mana = 40;
            Item.useTime = Item.useAnimation = 30;
            Item.shoot = ModContent.ProjectileType<DivineWoodBloomCall>();
        }
        else {
            if (Main.GameUpdateCount - _lastCastFrame > ComboResetGap)
                _castCombo = 0;
            Item.mana = 12;
            Item.useTime = Item.useAnimation = 24;
            Item.shoot = ModContent.ProjectileType<DivineWoodTomeLeaf>();
        }
        return true;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        // —— 右键·催花: 在光标处 (限程 600) 张开收割法阵 ——
        if (player.altFunctionUse == 2) {
            _callReadyFrame = Main.GameUpdateCount + CallCooldown;
            Vector2 target = Main.MouseWorld;
            Vector2 toTarget = target - player.Center;
            if (toTarget.Length() > 600f)
                target = player.Center + toTarget.SafeNormalize(Vector2.UnitX) * 600f;
            Projectile.NewProjectile(source, target, Vector2.Zero,
                ModContent.ProjectileType<DivineWoodBloomCall>(), damage, knockback, player.whoAmI);
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.9f, Pitch = 0.35f }, player.Center);
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = -0.1f }, target);
            return false;
        }

        // —— 左键·三连诵 ——
        int combo = _castCombo;
        _castCombo = (_castCombo + 1) % 3;
        _lastCastFrame = Main.GameUpdateCount;

        bool finale = combo == 2;
        int count = finale ? 7 : 5;
        float spreadHalf = MathHelper.ToRadians(finale ? 16 : 24); // 华盖收束更紧
        float baseAngle = velocity.ToRotation();

        // 音高逐诵上升 (第三诵额外低频层 = 华盖落下)
        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.85f, Pitch = 0.05f + combo * 0.15f }, player.Center);
        if (finale)
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.2f }, player.Center);

        for (int i = 0; i < count; i++) {
            float angle = baseAngle + MathHelper.Lerp(-spreadHalf, spreadHalf, count == 1 ? 0.5f : (float)i / (count - 1));
            angle += Main.rand.NextFloat(-0.04f, 0.04f);
            float speed = velocity.Length() * Main.rand.NextFloat(0.9f, 1.1f);
            Vector2 leafVel = angle.ToRotationVector2() * speed;
            float spiralDir = i % 2 == 0 ? 1f : -1f;
            Projectile.NewProjectile(source, position, leafVel,
                ModContent.ProjectileType<DivineWoodTomeLeaf>(), damage, knockback,
                player.whoAmI, ai0: spiralDir);
        }

        // 第三诵: 中心莲华 (穿透收割弹)
        if (finale) {
            Projectile.NewProjectile(source, position,
                velocity.SafeNormalize(Vector2.UnitX) * 9.5f,
                ModContent.ProjectileType<DivineWoodLotus>(),
                (int)(damage * 1.35f), knockback * 1.5f, player.whoAmI);
        }

        // 释放叶尘
        for (int i = 0; i < (finale ? 18 : 10); i++) {
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
/// 神木叶刃 - 螺旋偏移后追踪的叶片 (命中播种 1 层生根, 概率释放次生花瓣)。
/// </summary>
public class DivineWoodTomeLeaf : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Leaf;

    private float _timer;
    private const float SpiralDuration = 25f;

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
        DivineWoodRoot.AddStack(target, 1);

        for (int i = 0; i < 6; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(4f, 4f), 60, default, 1.5f);
            d.noGravity = true;
        }

        // 概率释放次生花瓣
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
/// 次生花瓣 - 叶片命中时概率释放的小花瓣 (追踪, 命中播种 1 层)。
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
        DivineWoodRoot.AddStack(target, 1);
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

/// <summary>
/// 神木莲华 - 第三诵中心穿透弹: 缓速直行的巨型莲花, 命中引爆生根。
/// 程序化绘制 (BlankStar 花瓣轮 + Sparkle + 柔光核)。
/// </summary>
public class DivineWoodLotus : ModProjectile
{
    public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private ref float Timer => ref Projectile.ai[0];

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
    }

    public override void SetDefaults() {
        Projectile.width = 40;
        Projectile.height = 40;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 110;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 18;
    }

    public override void AI() {
        Timer++;
        Projectile.rotation += 0.06f;
        // 缓速: 出手后逐渐减速为压场慢弹
        if (Projectile.velocity.Length() > 4.5f)
            Projectile.velocity *= 0.985f;

        // 花瓣灵光环绕
        if (Main.rand.NextBool(2)) {
            float ang = Main.rand.NextFloat(MathHelper.TwoPi);
            Dust d = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * 22f,
                DustID.JunglePlants, ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1.8f,
                70, default, 1.3f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.4f, 1f, 0.5f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        int consumed = DivineWoodRoot.TriggerBloom(Projectile.GetSource_OnHit(target), target,
            Projectile.damage, 4f, Projectile.owner);
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.DivineWood, scale: 1.1f + consumed * 0.05f, owner: Projectile.owner);
        WeaponVFX.AddScreenShake(target.Center, consumed > 0 ? 3f : 1.5f);
        SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.7f, Pitch = 0.25f + consumed * 0.05f }, target.Center);
    }

    public override bool PreDraw(ref Color lightColor) {
        // 细拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 16f,
            outerColor: new Color(20, 110, 55, 150), innerColor: new Color(170, 255, 150, 200),
            tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D star = ACMAsset.BlankStar;
        Texture2D sparkle = ACMAsset.Sparkle;
        Texture2D sg = ACMAsset.SoftGlow;
        Vector2 pos = Projectile.Center - Main.screenPosition;
        float pulse = 0.8f + 0.2f * MathF.Sin(Timer * 0.15f);

        // 外花瓣轮 (双层星形交叠旋转)
        sb.Draw(star, pos, null, DivineWoodPalette.Emerald * (0.75f * pulse),
            Projectile.rotation, star.Size() * 0.5f, 1.05f, SpriteEffects.None, 0);
        sb.Draw(star, pos, null, DivineWoodPalette.BrightCore * (0.6f * pulse),
            -Projectile.rotation * 0.7f + 0.4f, star.Size() * 0.5f, 0.72f, SpriteEffects.None, 0);
        // 花蕊
        sb.Draw(sparkle, pos, null, new Color(220, 255, 210) * (0.8f * pulse),
            Projectile.rotation * 1.4f, sparkle.Size() * 0.5f, 0.5f, SpriteEffects.None, 0);
        sb.Draw(sg, pos, null, DivineWoodPalette.Emerald * (0.55f * pulse), 0f,
            sg.Size() * 0.5f, 0.9f, SpriteEffects.None, 0);

        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);
        return false;
    }
}

/// <summary>
/// 催花法阵 - 右键收割: 光标处张开年轮法阵, 数帧内引爆域内所有生根敌人 (每帧 ≤2, 配合全局绽放预算)。
/// 自身无直接伤害, 伤害由绽放输出。
/// </summary>
public class DivineWoodBloomCall : ModProjectile
{
    public override string Texture => "AncientChineseMythology/Textures/Masking/SoftGlow";

    private const float CallRadius = 420f;
    private const int LifeTime = 44;
    private const int TriggerStart = 14;
    private const int TriggerEnd = 32;

    private ref float Timer => ref Projectile.ai[0];

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = LifeTime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        Timer++;

        // 收割窗口: 每帧最多引爆 2 个 (剩余的下帧继续, 与全局绽放预算协同)
        if (Timer >= TriggerStart && Timer <= TriggerEnd && Main.myPlayer == Projectile.owner) {
            int triggered = 0;
            for (int i = 0; i < Main.maxNPCs && triggered < 2; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || !npc.HasBuff(ModContent.BuffType<DivineWoodRootedBuff>()))
                    continue;
                if (Vector2.Distance(npc.Center, Projectile.Center) > CallRadius)
                    continue;
                if (DivineWoodRoot.TriggerBloom(Projectile.GetSource_FromThis(), npc,
                        Projectile.damage, 4f, Projectile.owner) > 0)
                    triggered++;
            }
            if (triggered > 0)
                WeaponVFX.AddScreenShake(Projectile.Center, 2.5f);
        }

        // 法阵沿花瓣粒子
        float grow = ACMUtils.QuadOut(Math.Min(Timer / TriggerStart, 1f));
        for (int i = 0; i < 3; i++) {
            float ang = Main.rand.NextFloat(MathHelper.TwoPi);
            Dust d = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * CallRadius * grow * Main.rand.NextFloat(0.9f, 1f),
                DustID.JunglePlants, ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2f, 80, default, 1.2f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.3f, 0.8f, 0.4f);
    }

    public override bool PreDraw(ref Color lightColor) {
        float life = Timer / LifeTime;
        float grow = ACMUtils.QuadOut(Math.Min(Timer / TriggerStart, 1f));
        float fade = life > 0.65f ? 1f - (life - 0.65f) / 0.35f : 1f;

        // 大年轮法阵 (专属着色器) + 扩张环
        DivineWoodFX.DrawGrowthRing(Projectile.Center, CallRadius, grow, fade * 0.7f,
            (float)Main.timeForVisualEffects * 0.012f);
        WeaponVFX.DrawShockwaveRing(Projectile.Center, CallRadius * grow, 16f, fade * 0.5f,
            DivineWoodPalette.BrightCore, DivineWoodPalette.DeepGreen);
        WeaponVFX.DrawGlowBurst(Projectile.Center, 1.2f * fade, DivineWoodPalette.Emerald * (fade * 0.6f));
        return false;
    }
}
