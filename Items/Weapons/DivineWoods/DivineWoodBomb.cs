using AncientChineseMythology.Celestias.Boss.Dryades.Items;
using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.DivineWoods;

/// <summary>
/// 神木种子弹 - 会"生长"的三形态种子:
/// 砸中地面 → 扎根, 三波根须尖刺依次窜出 (前两波播种, 第三波引爆生根);
/// 砸中敌人 → 寄生, 45 帧膨胀后开花 (挂 3 层并立即引爆, 单体处决);
/// 空中超时 → 原地自然绽放爆炸 + 追踪藤蔓碎片。
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
/// 神木种子手雷 - 三形态: ai[1]=形态 (0 飞行 / 1 扎根 / 2 寄生), ai[0]=形态内计时,
/// localAI[0]=寄生目标, velocity 在寄生态复用为贴附偏移。
/// </summary>
public class DivineWoodSeedGrenade : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/DivineWoods/DivineWoodBomb";

    private const int AirTimeout = 90;
    private const int PlantShow = 68;      // 扎根后演出驻留
    private const int SwellTime = 45;      // 寄生膨胀

    private ref float Timer => ref Projectile.ai[0];
    private ref float Phase => ref Projectile.ai[1];
    private ref float TargetIdx => ref Projectile.ai[2]; // 走 ai 槽以获得网络同步 (寄生宿主对全客户端一致)

    private bool Flying => Phase == 0f;
    private bool Planted => Phase == 1f;
    private bool Attached => Phase == 2f;

    public override void SetDefaults() {
        Projectile.width = 20;
        Projectile.height = 20;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 400;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => Flying;

    public override void AI() {
        Timer++;

        if (Flying) {
            Projectile.velocity.Y += 0.22f;
            Projectile.rotation += Projectile.velocity.X * 0.04f;
            Lighting.AddLight(Projectile.Center, 0.2f, 0.6f, 0.2f);

            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                -Projectile.velocity * 0.08f, 60, default, 1.2f);
            d.noGravity = true;

            // 空中超时 → 原地绽放
            if (Timer > AirTimeout)
                AirBloom();
            return;
        }

        if (Planted) {
            // 扎根: 根网蔓延提示 + 驻留后安静谢幕 (尖刺弹幕自行演出)
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-90f, 90f), 4f),
                    DustID.JunglePlants, new Vector2(0, -Main.rand.NextFloat(0.5f, 1.5f)), 80, default, 1.1f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.25f, 0.7f, 0.3f);
            if (Timer > PlantShow)
                Projectile.Kill();
            return;
        }

        // ===== 寄生态 =====
        int idx = (int)TargetIdx;
        NPC host = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
        if (host == null || !host.active) {
            // 宿主提前死亡 → 就地开花 (无宿主, 只演出)
            ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
                ACMWeaponBurst.DivineWood, scale: 1.2f, owner: Projectile.owner);
            Projectile.Kill();
            return;
        }

        // 贴附 (velocity 槽 = 相对偏移)
        Projectile.Center = host.Center + Projectile.velocity;
        float swell = Timer / SwellTime;
        Projectile.rotation += 0.05f + swell * 0.1f;

        // 膨胀滴答 (音高爬升)
        if ((int)Timer % 9 == 0)
            SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.35f, Pitch = -0.3f + swell * 0.8f }, Projectile.Center);
        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                DustID.JungleTorch, new Vector2(0, -1f), 60, default, 1f + swell);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.3f + swell * 0.4f, 0.8f + swell * 0.4f, 0.3f);

        // 开花: 挂满 3 层并立即引爆 (单体处决)
        if (Timer >= SwellTime) {
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.9f, Pitch = 0.3f }, Projectile.Center);
            if (Main.myPlayer == Projectile.owner) {
                DivineWoodRoot.AddStack(host, 3);
                DivineWoodRoot.TriggerBloom(Projectile.GetSource_Death(), host,
                    Projectile.damage, Projectile.knockBack, Projectile.owner);
            }
            ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
                ACMWeaponBurst.DivineWood, scale: 1.4f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(Projectile.Center, 3f);
            Projectile.Kill();
        }
    }

    public override bool OnTileCollide(Vector2 oldVelocity) {
        if (!Flying)
            return false;
        // ===== 扎根 =====
        Phase = 1f;
        Timer = 0f;
        Projectile.velocity = Vector2.Zero;
        Projectile.netUpdate = true;
        SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.8f, Pitch = -0.25f }, Projectile.Center);
        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
        WeaponVFX.AddScreenShake(Projectile.Center, 2.5f);

        for (int i = 0; i < 16; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center,
                Main.rand.NextBool(3) ? DustID.Dirt : DustID.JungleTorch,
                new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1f, 5f)), 50, default, 1.6f);
            d.noGravity = Main.rand.NextBool();
        }

        // 三波根须尖刺 (内→外, 前两波播种, 第三波引爆); 依次窜出的节奏由尖刺 ai[0] 延迟承载
        if (Main.myPlayer == Projectile.owner) {
            Vector2 ground = DivineWoodRoot.FindGroundBelow(Projectile.Center, 12);
            for (int wave = 0; wave < 3; wave++) {
                bool bloom = wave == 2;
                for (int i = -1; i <= 1; i++) {
                    float xOff = i * (52f + wave * 34f) + Main.rand.NextFloat(-10f, 10f);
                    if (wave > 0 && i == 0)
                        continue; // 中心刺只出第一波, 后两波向两侧推进
                    Vector2 spikeBase = DivineWoodRoot.FindGroundBelow(ground + new Vector2(xOff, -40f), 14);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), spikeBase, Vector2.Zero,
                        ModContent.ProjectileType<DivineWoodRootSpike>(),
                        (int)(Projectile.damage * 0.6f), 4f, Projectile.owner,
                        wave * 20f + Math.Abs(i) * 4f, bloom ? 1f : 0f);
                }
            }
        }
        return false;
    }

    public override bool? CanDamage() => Flying ? null : false;

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        if (!Flying)
            return;
        // ===== 寄生 =====
        Phase = 2f;
        Timer = 0f;
        TargetIdx = target.whoAmI;
        Vector2 offset = Projectile.Center - target.Center;
        if (offset.Length() > target.width * 0.4f)
            offset = offset.SafeNormalize(Vector2.Zero) * target.width * 0.4f;
        Projectile.velocity = offset;   // velocity 槽复用为贴附偏移
        Projectile.tileCollide = false;
        Projectile.timeLeft = SwellTime + 20;
        Projectile.netUpdate = true;

        SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.6f, Pitch = 0.4f }, target.Center);
        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = -0.2f }, target.Center);
        for (int i = 0; i < 8; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(3f, 3f), 60, default, 1.4f);
            d.noGravity = true;
        }
    }

    /// <summary>空中超时: 原地自然绽放 + 追踪藤蔓碎片。</summary>
    private void AirBloom() {
        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = 0.3f }, Projectile.Center);

        if (Main.myPlayer == Projectile.owner) {
            Projectile.NewProjectile(
                Projectile.GetSource_Death(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<DivineWoodBloomExplosion>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner);

            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6;
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

        ACMWeaponBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center,
            ACMWeaponBurst.DivineWood, scale: 1.5f, owner: Projectile.owner);
        WeaponVFX.AddScreenShake(Projectile.Center, 4f);
        Projectile.Kill();
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
        Texture2D sg = ACMAsset.SoftGlow;

        // 扎根态: 落点年轮 telegraph (由内向外三圈对应三波尖刺)
        if (Planted) {
            float grow = MathHelper.Clamp(Timer / 60f, 0f, 1f);
            DivineWoodFX.DrawGrowthRing(Projectile.Center, 165f, grow, 0.65f * (1f - Timer / PlantShow * 0.4f));
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        float swell = Attached ? MathHelper.Clamp(Timer / SwellTime, 0f, 1f) : 0f;
        float pulseRate = Attached ? 0.25f + swell * 0.5f : 0.25f;
        float pulse = 0.45f + 0.12f * MathF.Sin((float)Main.timeForVisualEffects * pulseRate);
        Color glowC = Attached
            ? Color.Lerp(new Color(60, 220, 70), new Color(220, 255, 200), swell)
            : new Color(60, 220, 70);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            glowC * (0.55f + swell * 0.35f), 0f,
            sg.Size() * 0.5f,
            pulse * (1f + swell * 1.2f), SpriteEffects.None, 0);

        // 扎根态: 发芽小苗 (SlashBurst 细芽从种子里长出)
        if (Planted) {
            Texture2D burst = ACMAsset.SlashBurst;
            float sprout = ACMUtils.BackOut(MathHelper.Clamp(Timer / 30f, 0f, 1f));
            sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                DivineWoodPalette.Emerald * 0.8f, 0f,
                new Vector2(burst.Width * 0.5f, burst.Height),
                new Vector2(0.08f, 0.5f * sprout), SpriteEffects.None, 0);
        }

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        // 种子本体 (寄生态膨胀)
        float bodyScale = Projectile.scale * (1f + swell * 0.55f);
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            lightColor, Projectile.rotation,
            tex.Size() * 0.5f,
            bodyScale, SpriteEffects.None, 0);
        return false;
    }
}

/// <summary>
/// 自然绽放爆炸 - 空爆形态的视觉+AoE (命中播种 2 层生根)。
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
        DivineWoodRoot.AddStack(target, 2);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        float radius = Timer * 12f;
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, radius, targetHitbox);
    }

    public override bool PreDraw(ref Color lightColor) {
        float prog = 1f - Projectile.timeLeft / 50f;
        float alpha = ACMUtils.QuadOut(1f - prog) * 0.90f;
        float scale = MathHelper.SmoothStep(0f, 14f, ACMUtils.QuadOut(prog));

        // 绽放扩张冲击环 (绿)
        float ringR = Timer * 12f;
        WeaponVFX.DrawShockwaveRing(Projectile.Center, ringR, 14f, alpha * 0.85f,
            new Color(170, 255, 150), new Color(20, 110, 55));

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
        ACMShaders.RestoreDefaultBatch(sb);
        return false;
    }
}

/// <summary>
/// 藤蔓碎片 - 空爆释放的追踪碎片 (命中播种 1 层生根)。
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
        DivineWoodRoot.AddStack(target, 1);
        for (int i = 0; i < 4; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(3f, 3f), 60, default, 1.2f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 细双层 ribbon 拖尾 (藤蔓碎片)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 5f,
            outerColor: new Color(20, 110, 55, 140), innerColor: new Color(170, 255, 150, 200),
            uvScroll: -Main.GlobalTimeWrappedHourly * 2f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D star = ACMAsset.BlankStar;
        Texture2D sg = ACMAsset.SoftGlow;

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
        ACMShaders.RestoreDefaultBatch(sb);
        return false;
    }
}
