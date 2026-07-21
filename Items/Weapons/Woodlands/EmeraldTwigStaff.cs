using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands;

/// <summary>
/// 翡翠树枝杖 - 法师法杖 (重做: 翡翠共鸣)
/// 枝弹命中往目标嵌入翡翠碎片 (≤3, 头顶绿星可读); 嵌满后再次命中
/// 引发共鸣爆裂 (75% 伤害 90px AoE), 印记清空。
/// </summary>
public class EmeraldTwigStaff : ModItem
{
    public override void SetDefaults() {
        Item.damage = 16;
        Item.crit = 4;
        Item.DamageType = DamageClass.Magic;
        Item.width = 36;
        Item.height = 36;
        Item.useTime = 28;
        Item.useAnimation = 28;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 3f;
        Item.value = Item.buyPrice(silver: 50);
        Item.rare = ItemRarityID.Blue;
        Item.UseSound = SoundID.Item8;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<EmeraldTwigBolt>();
        Item.shootSpeed = 10f;
        Item.mana = 6;
        Item.staff[Type] = true;
    }

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
        velocity = velocity.RotatedByRandom(MathHelper.ToRadians(5));
    }

    public override bool Shoot(Player player, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        // 杖尖聚绿光 (发射反馈)
        Vector2 tip = position + velocity.SafeNormalize(Vector2.UnitX) * 34f;
        for (int i = 0; i < 4; i++) {
            Dust d = Dust.NewDustPerfect(tip + Main.rand.NextVector2Circular(6f, 6f), DustID.GemEmerald,
                velocity * 0.05f, 80, default, 0.9f);
            d.noGravity = true;
        }
        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
        return false;
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 10)
            .AddIngredient(ItemID.Emerald, 3)
            .AddIngredient(ItemID.JungleSpores, 3)
            .AddIngredient(ItemID.FallenStar, 2)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

/// <summary>
/// 翡翠树枝弹 - 翠绿能量弹幕。命中嵌入翡翠碎片, 嵌满 3 枚后触发共鸣爆裂。
/// </summary>
public class EmeraldTwigBolt : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    /// <summary>共鸣爆裂主题 (0=翡翠, 1=赤铜熔爆; 赤铜升级覆写)。</summary>
    protected virtual int BlastTheme => 0;
    /// <summary>共鸣爆裂伤害比例。</summary>
    protected virtual float BlastRatio => 0.75f;
    /// <summary>命中演出主题 (赤铜升级覆写)。</summary>
    protected virtual int HitBurstTheme => ACMWeaponBurst.Nature;

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
        ProjectileID.Sets.TrailingMode[Type] = 0;
    }

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 90;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;
        Projectile.extraUpdates = 1;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.1f, 0.25f, 0.1f);

        // 翠绿粒子尾迹
        if (Main.rand.NextBool(2)) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                60, default, 0.8f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 翡翠双层拖尾 (外暗深翠 + 内亮嫩绿)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 7f,
            outerColor: new Color(40, 150, 60, 150), innerColor: new Color(190, 255, 150, 200),
            uvScroll: -Main.GlobalTimeWrappedHourly * 2f);
        // 翠绿能量核心
        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.4f, new Color(120, 230, 90));
        return false;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        for (int i = 0; i < 6; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.GreenTorch,
                Main.rand.NextVector2Circular(3f, 3f), 60, default, 1.1f);
            d.noGravity = true;
        }

        // 翡翠共鸣: 嵌片 → 满 3 枚后下一次命中起爆 (印记状态仅 owner 端有效, 伤害亦由 owner 端生成)
        if (Projectile.owner == Main.myPlayer && target.active && !target.friendly && target.lifeMax > 5) {
            var mark = target.GetGlobalNPC<EmeraldMarkGlobalNPC>();
            if (mark.marks >= EmeraldMarkGlobalNPC.MaxMarks) {
                mark.marks = 0;
                int blastDamage = Math.Max((int)(Projectile.damage * BlastRatio), 1);
                Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<EmeraldResonanceBlast>(), blastDamage, 2f, Projectile.owner,
                    ai0: BlastTheme);
            }
            else {
                mark.marks++;
                mark.markTimer = EmeraldMarkGlobalNPC.MarkDuration;
                SoundEngine.PlaySound(SoundID.Item101 with { Volume = 0.35f, Pitch = 0.4f + mark.marks * 0.15f }, target.Center);
            }
        }

        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            HitBurstTheme, scale: 0.8f, owner: Projectile.owner);
    }

    public override void OnKill(int timeLeft) {
        for (int i = 0; i < 8; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                Main.rand.NextVector2CircularEdge(3f, 3f), 40, default, 1f);
            d.noGravity = true;
        }
    }
}

/// <summary>
/// 翡翠印记 GlobalNPC - 记录目标身上嵌入的翡翠碎片数 (owner 端状态) 并绘制头顶绿星。
/// </summary>
public class EmeraldMarkGlobalNPC : GlobalNPC
{
    public const int MaxMarks = 3;
    public const int MarkDuration = 360; // 6s

    public override bool InstancePerEntity => true;

    public int marks;
    public int markTimer;

    public override void PostAI(NPC npc) {
        if (marks <= 0)
            return;
        if (--markTimer <= 0)
            marks = 0;
    }

    public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
        if (marks <= 0 || Main.dedServ)
            return;
        Texture2D star = ACMAsset.BlankStar;
        if (star == null)
            return;

        // 头顶环绕的翡翠碎片 (AlphaBlend 下 A=0 呈加法观感)
        for (int i = 0; i < marks; i++) {
            float ang = Main.GlobalTimeWrappedHourly * 2.4f + i * MathHelper.TwoPi / MaxMarks;
            Vector2 pos = npc.Top + new Vector2(MathF.Cos(ang) * 15f, -12f + MathF.Sin(ang * 2f) * 3f);
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + i * 2f);
            spriteBatch.Draw(star, pos - screenPos, null, new Color(120, 255, 140, 0) * pulse, ang,
                star.Size() * 0.5f, 0.14f * pulse, SpriteEffects.None, 0f);
        }
    }
}

/// <summary>
/// 共鸣爆裂 - 3 枚翡翠碎片同时炸开 (90px AoE)。ai[0] = 主题 (0=翡翠, 1=赤铜熔爆)。
/// </summary>
public class EmeraldResonanceBlast : ModProjectile
{
    public override string Texture => "InnoVault/Assets/placeholder";

    private const int LifeTime = 22;
    private const float Radius = 90f;

    private bool Cuprite => Projectile.ai[0] == 1f;
    private float Life => 1f - Projectile.timeLeft / (float)LifeTime;

    public override void SetStaticDefaults() {
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.EmeraldResonanceBlast.DisplayName", () => "翡翠共鸣");
    }

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.timeLeft = LifeTime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = LifeTime; // 每目标一次
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        if (Projectile.timeLeft == LifeTime) {
            // 起爆帧: 碎片飞散 + 音效 + 震屏 + 命中演出
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.9f, Pitch = Cuprite ? -0.3f : -0.1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = 0.5f }, Projectile.Center);
            WeaponVFX.AddScreenShake(Projectile.Center, 3f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_FromAI(), Projectile.Center,
                Cuprite ? ACMWeaponBurst.CupriteBurn : ACMWeaponBurst.Nature, 1.4f, Projectile.owner);

            int gemDust = Cuprite ? DustID.Torch : DustID.GemEmerald;
            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f) * Main.rand.NextFloat(0.5f, 1f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, gemDust, vel, 30, default, Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = true;
            }
        }

        // 判定只在前 6 帧 (视觉与判定对齐)
        Projectile.friendly = Projectile.timeLeft > LifeTime - 6;
        Lighting.AddLight(Projectile.Center, Cuprite ? new Vector3(0.5f, 0.25f, 0.05f) : new Vector3(0.15f, 0.45f, 0.2f));
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, Radius, targetHitbox);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        if (Cuprite)
            target.AddBuff(BuffID.OnFire, 150);
    }

    public override bool PreDraw(ref Color lightColor) {
        if (Main.dedServ)
            return false;
        float life = Life;
        float fade = 1f - life;
        Color inner = Cuprite ? new Color(255, 200, 110) : new Color(190, 255, 160);
        Color outer = Cuprite ? new Color(200, 70, 20) : new Color(40, 170, 80);

        // 扩张冲击环 + 三枚碎片闪光飞散
        WeaponVFX.DrawShockwaveRing(Projectile.Center, 12f + life * Radius, 9f, fade * 0.9f, inner, outer);
        Texture2D star = ACMAsset.BlankStar;
        if (star != null) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            for (int i = 0; i < 3; i++) {
                float ang = i * MathHelper.TwoPi / 3f + life * 1.5f;
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * life * Radius * 0.8f - Main.screenPosition;
                sb.Draw(star, pos, null, inner * (fade * 0.9f), ang, star.Size() * 0.5f,
                    0.28f * fade + 0.06f, SpriteEffects.None, 0f);
            }
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }
        WeaponVFX.DrawGlowBurst(Projectile.Center, 1.4f * fade, inner * 0.8f);
        return false;
    }
}
