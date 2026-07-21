using AncientChineseMythology.Global;
using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Items.XuanTie;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Woodlands.Upgrades;

/// <summary>
/// 玄铁猎弓 — 完整继承"第 4 矢标记—收割"机制身份, 叠玄铁放血风味:
/// 弹药转化为玄铁血矢; 第 4 发为"血钩矢" (种血标记 + 流血 2 层);
/// 后续命中带标记目标 → 血刺收割 (55% 伤害, 对流血 ≥5 层目标 ×1.5)。
/// </summary>
public class XuanTieHunterBow : VineHunterBow
{
    protected override int BowDustType => DustID.Blood;

    public override void SetDefaults() {
        base.SetDefaults();
        Item.damage = 38;
        Item.crit = 8;
        Item.useTime = 24;
        Item.useAnimation = 24;
        Item.knockBack = 3f;
        Item.value = Item.buyPrice(gold: 2);
        Item.rare = ItemRarityID.Orange;
        Item.shootSpeed = 9f;
    }

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
        // 弹药一律转化为玄铁血矢 (精准, 不加散布)
        type = ModContent.ProjectileType<XuanTieArrow>();
    }

    internal override void OnArrowHit(Projectile arrow, NPC target, bool vineShot) {
        if (!target.active || target.friendly || target.lifeMax <= 5)
            return;

        var mark = target.GetGlobalNPC<VineMarkGlobalNPC>();
        if (vineShot) {
            // 血钩矢: 种血标记 + 流血 2 层
            mark.markTimer = VineMarkGlobalNPC.MarkDuration;
            mark.markTheme = 1;
            AddBleed(target, 2);
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = -0.15f }, target.Center);
        }
        else if (mark.markTimer > 0) {
            // 血刺收割: 消耗标记, 从目标体内爆血刺
            mark.markTimer = 0;
            var bleed = target.GetGlobalNPC<XuanTieBleedGlobalNPC>();
            float ratio = bleed.bleedStacks >= 5 ? 0.55f * 1.5f : 0.55f; // 撕裂: 深层流血伤害强化
            int spikeDamage = Math.Max((int)(arrow.damage * ratio), 1);
            Projectile.NewProjectile(arrow.GetSource_OnHit(target), target.Center, Vector2.Zero,
                ModContent.ProjectileType<XuanTieBloodSpike>(), spikeDamage, 2f, arrow.owner);
        }
    }

    /// <summary>叠加玄铁流血 (与套装 bleedStacks 体系协同)。</summary>
    internal static void AddBleed(NPC target, int stacks) {
        if (!target.active || target.friendly || target.dontTakeDamage)
            return;
        target.AddBuff(ModContent.BuffType<Buffs.XuanTieBleed>(), 60 * 3);
        var bleed = target.GetGlobalNPC<XuanTieBleedGlobalNPC>();
        bleed.bleedStacks = Math.Min(bleed.bleedStacks + stacks, 10);
    }

    public override void AddRecipes() {
        CreateRecipe()
            .AddIngredient<VineHunterBow>()
            .AddIngredient<XuanTieBar>(15)
            .AddIngredient<YaoQiFragment>(3)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

/// <summary>
/// 玄铁血矢 — 复用原版木箭 AI (重力/插地); 每次命中 +1 流血层。
/// 拖尾/命中演出由 <see cref="VineHunterBowGlobalProj"/> 按玄铁主题统一绘制。
/// </summary>
public class XuanTieArrow : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.WoodenArrowFriendly}";

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 600;
        Projectile.tileCollide = true;
        Projectile.arrow = true;
        Projectile.aiStyle = ProjAIStyleID.Arrow;
        AIType = ProjectileID.WoodenArrowFriendly;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        XuanTieHunterBow.AddBleed(target, 1);
        for (int i = 0; i < 6; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                Main.rand.NextVector2Circular(3f, 3f), 100, default, 1.1f);
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        Lighting.AddLight(Projectile.Center, 0.25f, 0.04f, 0.05f);
        return true; // 暗钢箭体 (拖尾由全局弹幕绘制)
    }
}

/// <summary>
/// 血刺收割 — 从标记目标体内爆出的放射血刺 (程序化绘制): 6f 判定窗口 + 流血 2 层。
/// </summary>
public class XuanTieBloodSpike : ModProjectile
{
    public override string Texture => "InnoVault/Assets/placeholder";

    private const int LifeTime = 20;
    private const float Radius = 80f;

    private float Life => 1f - Projectile.timeLeft / (float)LifeTime;

    public override void SetStaticDefaults() {
        Language.GetOrRegister("Mods.AncientChineseMythology.Projectiles.XuanTieBloodSpike.DisplayName", () => "血刺收割");
    }

    public override void SetDefaults() {
        Projectile.width = 10;
        Projectile.height = 10;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = -1;
        Projectile.timeLeft = LifeTime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = LifeTime;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI() {
        if (Projectile.timeLeft == LifeTime) {
            // 爆刺帧: 血泉 + 音效 + 命中演出
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.8f, Pitch = -0.2f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = -0.4f }, Projectile.Center);
            WeaponVFX.AddScreenShake(Projectile.Center, 2.5f);
            ACMWeaponBurst.Spawn(Projectile.GetSource_FromAI(), Projectile.Center,
                ACMWeaponBurst.XuanTieBleed, 1.3f, Projectile.owner);
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f) * Main.rand.NextFloat(0.4f, 1f)
                    - new Vector2(0, Main.rand.NextFloat(1f, 3f));
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, vel, 60, default, Main.rand.NextFloat(1.1f, 1.7f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        Projectile.friendly = Projectile.timeLeft > LifeTime - 6; // 判定窗口与视觉对齐
        Lighting.AddLight(Projectile.Center, 0.25f, 0.03f, 0.04f);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
        return VaultUtils.CircleIntersectsRectangle(Projectile.Center, Radius * MathHelper.Clamp(Life * 3f, 0.3f, 1f), targetHitbox);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        XuanTieHunterBow.AddBleed(target, 2);
    }

    public override bool PreDraw(ref Color lightColor) {
        if (Main.dedServ)
            return false;
        Texture2D tex = ACMAsset.SlashBurst;
        if (tex == null)
            return false;

        float life = Life;
        // 刺长: 前 30% 快速戳出 (poly ease-out), 之后回缩消散
        float len = life < 0.3f ? 1f - MathF.Pow(1f - life / 0.3f, 5f) : 1f - (life - 0.3f) / 0.7f;
        if (len <= 0.02f)
            return false;

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

        Vector2 center = Projectile.Center - Main.screenPosition;
        Vector2 origin = new(tex.Width * 0.5f, tex.Height);
        // 3 根放射血刺 (固定相位, 微转)
        for (int i = 0; i < 3; i++) {
            float ang = MathHelper.TwoPi / 3f * i - MathHelper.PiOver2 + life * 0.3f;
            float thornLen = Radius * 1.15f * len / tex.Height;
            sb.Draw(tex, center, null, new Color(90, 10, 12, 0) * (0.9f * len), ang + MathHelper.Pi,
                origin, new Vector2(0.09f, thornLen * 1.1f), SpriteEffects.None, 0f);
            sb.Draw(tex, center, null, new Color(220, 60, 60, 0) * len, ang + MathHelper.Pi,
                origin, new Vector2(0.045f, thornLen), SpriteEffects.None, 0f);
        }
        sb.End();
        ACMShaders.RestoreDefaultBatch(sb);

        WeaponVFX.DrawGlowBurst(Projectile.Center, 0.8f * len, new Color(190, 40, 40) * 0.8f);
        return false;
    }
}
