using AncientChineseMythology.Celestias.Boss.Dryades.Items;
using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.DivineWoods;

/// <summary>
/// 神木旋叶 - 可驻场的回旋镖:
/// 掷出减速后, 按住左键化作"年轮锯"驻场 (跟随光标缓移, 连续锯击播种生根, 最长 1.5 秒);
/// 松开 → 高速回程 ×1.5, 命中引爆沿途生根。每 5 次命中触发自然裁决 (花瓣环 + 区域播种)。
/// </summary>
public class DivineWoodGyratingLeaf : ModItem
{
    public override void SetDefaults() {
        Item.damage = 175;
        Item.crit = 16;
        Item.DamageType = DamageClass.Melee;
        Item.width = 40;
        Item.height = 40;
        Item.useTime = 28;
        Item.useAnimation = 28;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.knockBack = 7f;
        Item.value = Item.buyPrice(gold: 50);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.channel = true;
        Item.shoot = ModContent.ProjectileType<DivineWoodGyratingLeafProj>();
        Item.shootSpeed = 22f;
    }

    public override bool CanUseItem(Player player) {
        return player.ownedProjectileCounts[ModContent.ProjectileType<DivineWoodGyratingLeafProj>()] < 1;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<Livinglog>(12)
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
}

/// <summary>
/// 神木旋叶弹幕 - 掷出 → (按住) 年轮锯驻场 → 回程引爆:
/// ai[0]=阶段 (0 掷出 / 1 驻锯 / 2 回程), ai[1]=计时, localAI[0]=命中计数, localAI[1]=驻锯计时。
/// </summary>
public class DivineWoodGyratingLeafProj : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Items/Weapons/DivineWoods/DivineWoodGyratingLeaf";

    private ref float Phase => ref Projectile.ai[0];
    private ref float Timer => ref Projectile.ai[1];
    private ref float HitCounter => ref Projectile.localAI[0];
    private ref float SawTimer => ref Projectile.localAI[1];

    private const int OutgoingDuration = 30;     // 飞出持续帧数
    private const int SawMaxTime = 90;           // 驻锯上限
    private const float ReturnAccel = 1.8f;      // 回程加速度
    private const float MaxReturnSpeed = 34f;    // 回程最大速度
    private const float CatchRadius = 40f;       // 接住判定半径
    private const float SawFollowSpeed = 7.5f;   // 驻锯跟随光标速度上限

    private bool _outgoing => Phase == 0f;
    private bool _sawing => Phase == 1f;
    private bool _returning => Phase == 2f;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[Type] = 18;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults() {
        Projectile.width = 40;
        Projectile.height = 40;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 600;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = false;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    private void EnterReturn() {
        Phase = 2f;
        Timer = 0f;
        SoundEngine.PlaySound(SoundID.Item7 with { Volume = 0.7f, Pitch = 0.8f }, Projectile.Center);
        WeaponVFX.AddScreenShake(Projectile.Center, 2f);
    }

    public override void AI() {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead) { Projectile.Kill(); return; }

        Timer++;

        // 高速旋转 - 驻锯/回程更快
        float rotSpeed = _outgoing ? 0.35f : _sawing ? 0.75f : 0.55f;
        Projectile.rotation += rotSpeed;

        if (_outgoing) {
            // === 掷出阶段: 逐渐减速 ===
            float decel = MathHelper.Lerp(0.96f, 0.92f, Math.Min(Timer / OutgoingDuration, 1f));
            Projectile.velocity *= decel;

            // 翻转点 (owner 端裁决, netUpdate 广播): 按住 → 化作年轮锯驻场; 松开 → 直接回程
            if ((Timer >= OutgoingDuration || Projectile.velocity.Length() < 2f)
                && Main.myPlayer == Projectile.owner) {
                Phase = owner.channel ? 1f : 2f;
                Timer = 0f;
                Projectile.netUpdate = true;
                SoundEngine.PlaySound(SoundID.Item7 with { Volume = 0.6f, Pitch = 0.4f }, Projectile.Center);

                // 翻转时释放4片旋风叶片
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4 + Main.rand.NextFloat(0.3f);
                    Vector2 leafVel = angle.ToRotationVector2() * 5f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                        leafVel, ModContent.ProjectileType<DivineWoodWhirlLeaf>(),
                        Projectile.damage / 3, 1f, Projectile.owner);
                }
            }
        }
        else if (_sawing) {
            // === 年轮锯驻场: 跟随光标缓移, 松开或超时则回程 ===
            SawTimer++;
            if (Main.myPlayer == Projectile.owner) {
                Vector2 desired = (Main.MouseWorld - Projectile.Center) * 0.09f;
                if (desired.Length() > SawFollowSpeed)
                    desired = desired.SafeNormalize(Vector2.Zero) * SawFollowSpeed;
                Projectile.velocity = desired;
                if ((int)SawTimer % 10 == 0)
                    Projectile.netUpdate = true;

                if (!owner.channel || SawTimer >= SawMaxTime) {
                    EnterReturn();
                    Projectile.netUpdate = true;
                }
            }

            // 锯击音循环 (随驻锯时间音高微升)
            if ((int)SawTimer % 12 == 0)
                SoundEngine.PlaySound(SoundID.Item7 with { Volume = 0.45f, Pitch = 0.2f + SawTimer / SawMaxTime * 0.3f }, Projectile.Center);
        }
        else {
            // === 回程阶段 ===
            Vector2 toPlayer = owner.Center - Projectile.Center;
            float dist = toPlayer.Length();
            Vector2 dir = toPlayer.SafeNormalize(Vector2.Zero);

            Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * MaxReturnSpeed, 0.12f);
            if (Projectile.velocity.Length() < MaxReturnSpeed)
                Projectile.velocity += dir * ReturnAccel;

            if (dist < CatchRadius) {
                Projectile.Kill();
                return;
            }
        }

        // 粒子特效
        if (Main.rand.NextBool(_outgoing ? 3 : 2)) {
            Dust d = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(15, 15),
                DustID.JungleTorch, -Projectile.velocity * 0.15f, 60, default,
                Main.rand.NextFloat(1f, 1.8f));
            d.noGravity = true;
        }

        Lighting.AddLight(Projectile.Center, 0.25f, 0.7f, 0.25f);
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
        // 驻锯 ×0.85 (高频), 回程 ×1.5 (收割)
        if (_sawing)
            modifiers.SourceDamage *= 0.85f;
        else if (_returning)
            modifiers.SourceDamage *= 1.5f;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        HitCounter++;

        int dustCount = _returning ? 14 : 8;
        for (int i = 0; i < dustCount; i++) {
            Dust burst = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(6f, 6f), 60, default, 1.8f);
            burst.noGravity = true;
        }

        if (_returning) {
            // 回程收割: 引爆生根
            int consumed = DivineWoodRoot.TriggerBloom(Projectile.GetSource_OnHit(target), target,
                Projectile.damage, 5f, Projectile.owner);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.DivineWood, scale: 1.15f + consumed * 0.05f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, consumed > 0 ? 3.5f : 2f);
        }
        else {
            // 掷出/驻锯: 播种
            DivineWoodRoot.AddStack(target, 1);
            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.DivineWood, scale: 0.9f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 1.2f);

            // 驻锯每 3 次锯击甩出 1 片旋风叶
            if (_sawing && (int)HitCounter % 3 == 0 && Projectile.owner == Main.myPlayer) {
                Vector2 leafVel = Main.rand.NextVector2CircularEdge(6f, 6f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center,
                    leafVel, ModContent.ProjectileType<DivineWoodWhirlLeaf>(),
                    Projectile.damage / 3, 1f, Projectile.owner);
            }
        }

        // 每5次命中触发自然裁决：花瓣弹幕环 + 区域播种
        if (HitCounter % 5 == 0) {
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 1f, Pitch = 0.35f + HitCounter / 50f }, target.Center);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = 0.6f }, target.Center);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.DivineWood, scale: 1.7f, owner: Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 3f);

            if (Projectile.owner == Main.myPlayer) {
                for (int i = 0; i < 12; i++) {
                    float angle = MathHelper.TwoPi * i / 12;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 9f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center,
                        vel, ModContent.ProjectileType<DivineWoodVerdictPetal>(),
                        damageDone / 2, 2f, Projectile.owner);
                }
            }

            // 区域播种 (350px 内敌人各挂 1 层)
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC nearby = Main.npc[i];
                if (!nearby.CanBeChasedBy()) continue;
                if (Vector2.Distance(target.Center, nearby.Center) < 350f)
                    DivineWoodRoot.AddStack(nearby, 1);
            }
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        SpriteBatch sb = Main.spriteBatch;
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        Vector2 origin = tex.Size() / 2f;

        // 驻锯: 锯下年轮法阵 (预算内; 半径小, 强度克制不遮场)
        if (_sawing) {
            float sawIn = ACMUtils.QuadOut(Math.Min(SawTimer / 12f, 1f));
            DivineWoodFX.DrawGrowthRing(Projectile.Center, 78f, sawIn, 0.4f, Projectile.rotation * 0.5f);
        }

        // 双层 ribbon 拖尾 — 回程加速段加亮加宽
        float ret = _returning ? 1f : 0f;
        WeaponVFX.DrawProjectileTrail(Projectile,
            baseWidth: MathHelper.Lerp(13f, 19f, ret),
            outerColor: new Color(20, 110, 55, (byte)MathHelper.Lerp(120, 175, ret)),
            innerColor: new Color(170, 255, 150, (byte)MathHelper.Lerp(170, 235, ret)),
            tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * (1.2f + ret));

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D sg = ACMAsset.SoftGlow;
        float pulse = 0.35f + 0.1f * MathF.Sin(Timer * 0.12f);
        float glowScale = _sawing ? 0.9f : _returning ? 0.75f : 0.60f;
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(80, 220, 90) * (_sawing ? pulse + 0.15f : pulse), 0f,
            sg.Size() * 0.5f,
            glowScale, SpriteEffects.None, 0);

        Color glowColor = new Color(60, 200, 80) * 0.28f;
        glowColor.A = 0;
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        // 残影绘制 - 在AlphaBlend模式下绘制半透明完整精灵副本
        for (int i = 1; i < Projectile.oldPos.Length; i += 2) {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float progress = 1f - (float)i / Projectile.oldPos.Length;
            Vector2 afterimagePos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
            float afterAlpha = progress * progress * (_returning ? 0.50f : 0.35f);
            Color afterColor = Color.Lerp(lightColor, new Color(140, 255, 170), 0.25f) * afterAlpha;
            float afterScale = Projectile.scale * MathHelper.Lerp(0.60f, 0.95f, progress);
            sb.Draw(tex, afterimagePos, null, afterColor, Projectile.oldRot[i], origin, afterScale, SpriteEffects.None, 0);
        }

        Color mainColor = Color.Lerp(lightColor, new Color(180, 255, 190), 0.3f);
        sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
            mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

        return false;
    }

    public override void OnKill(int timeLeft) {
        for (int i = 0; i < 15; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(5f, 5f), 60, default, 2f);
            d.noGravity = true;
        }
    }
}

/// <summary>
/// 旋风叶片 - 翻转/锯击甩出的旋转叶片, 短暂飞行后追踪敌人。
/// </summary>
public class DivineWoodWhirlLeaf : ModProjectile
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
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 90;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation += 0.25f * Projectile.direction;
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 5) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        // 前30帧螺旋扩散，然后追踪
        if (_timer > 30) {
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
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 16f, 0.10f);
            }
        }
        else {
            Projectile.velocity *= 0.96f;
        }

        Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
            -Projectile.velocity * 0.05f, 100, default, 0.8f);
        trail.noGravity = true;
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        int fh = tex.Height / Main.projFrames[Type];
        Rectangle src = new(0, Projectile.frame * fh, tex.Width, fh);
        Vector2 origin = new(tex.Width / 2f, fh / 2f);
        Color tint = Color.Lerp(lightColor, new Color(100, 255, 120), 0.4f);
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            tint, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
        return false;
    }
}

/// <summary>
/// 裁决花瓣 - 自然裁决释放的花瓣弹幕 (扩散后追踪, 命中播种 1 层)。
/// </summary>
public class DivineWoodVerdictPetal : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPetal;

    private float _timer;

    public override void SetStaticDefaults() {
        Main.projFrames[Type] = 3;
    }

    public override void SetDefaults() {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 120;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override void AI() {
        _timer++;
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        Projectile.frameCounter++;
        if (Projectile.frameCounter >= 5) {
            Projectile.frameCounter = 0;
            Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
        }

        // 前20帧减速扩散，然后加速追踪
        if (_timer < 20) {
            Projectile.velocity *= 0.95f;
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
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 18f, 0.08f);
            }
            else {
                Projectile.velocity *= 1.02f;
            }
        }

        if (Main.rand.NextBool(3)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                -Projectile.velocity * 0.05f, 100, default, 1.0f);
            d.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        DivineWoodRoot.AddStack(target, 1);
        for (int i = 0; i < 5; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(4f, 4f), 60, default, 1.5f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        Texture2D tex = TextureAssets.Projectile[Type].Value;
        int fh = tex.Height / Main.projFrames[Type];
        Rectangle src = new(0, Projectile.frame * fh, tex.Width, fh);
        Vector2 origin = new(tex.Width / 2f, fh / 2f);
        Color tint = Color.Lerp(lightColor, new Color(160, 255, 180), 0.35f);
        Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src,
            tint, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);
        return false;
    }
}
