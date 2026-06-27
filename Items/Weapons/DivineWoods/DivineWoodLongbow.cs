using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using AncientChineseMythology.Celestias.Boss.Dryades.Items;
using AncientChineseMythology.Helpers;

namespace AncientChineseMythology.Items.Weapons.DivineWoods;

/// <summary>
/// 神木长弓 - 蓄力型弓，短按快速发射叶刃箭，长按蓄力释放螺旋叶暴风
/// 蓄力弹幕参照CelestialCircletScepter的螺旋收束→追踪冲刺模式
/// </summary>
public class DivineWoodLongbow : ModItem
{
    public override void SetDefaults() {
        Item.damage = 155;
        Item.crit = 14;
        Item.DamageType = DamageClass.Ranged;
        Item.width = 24;
        Item.height = 56;
        Item.useTime = 18;
        Item.useAnimation = 18;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.knockBack = 4f;
        Item.value = Item.buyPrice(gold: 50);
        Item.rare = ItemRarityID.Purple;
        Item.UseSound = SoundID.Item5;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<DivineWoodLeafBolt>();
        Item.shootSpeed = 16f;
        Item.useAmmo = AmmoID.Arrow;
    }

    public override Vector2? HoldoutOffset() => new Vector2(-2, 0);

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
        type = ModContent.ProjectileType<DivineWoodLeafBolt>();
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
        // 主箭
        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);

        // 两侧螺旋叶刃
        for (int i = 0; i < 2; i++) {
            float offsetAngle = (i == 0 ? -1 : 1) * MathHelper.ToRadians(18);
            Vector2 perturbedVel = velocity.RotatedBy(offsetAngle) * 0.9f;
            Projectile.NewProjectile(source, position, perturbedVel,
                ModContent.ProjectileType<DivineWoodSpiralLeaf>(),
                (int)(damage * 0.5f), knockback * 0.3f, player.whoAmI,
                ai0: MathHelper.TwoPi * i / 2, ai1: 0);
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
/// 神木叶刃箭 - 高速主箭，命中后释放LightShot绿色穿透能量
/// </summary>
public class DivineWoodLeafBolt : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/LightShot";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
    }

    public override void SetDefaults() {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.extraUpdates = 1;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 8;
    }

    public override void AI() {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.2f, 0.7f, 0.2f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 300);
        target.velocity *= 0.7f;
        for (int i = 0; i < 10; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(5f, 5f), 60, default, 1.8f);
            d.noGravity = true;
        }
        ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
            ACMWeaponBurst.DivineWood, scale: 0.9f, owner: Projectile.owner);
    }

    public override bool PreDraw(ref Color lightColor) {
        // 出膛瞬间的小型径向辉光预警 (满拉释放)
        float release = MathHelper.Clamp((Projectile.timeLeft - 168f) / 12f, 0f, 1f);
        if (release > 0.01f)
            WeaponVFX.DrawRadialBloom(Projectile.Center, 0.06f, release * 0.6f,
                new Color(120, 255, 140), 6f);

        // 绿芯双层 ribbon 拖尾
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 8f,
            outerColor: new Color(20, 110, 55, 160), innerColor: new Color(170, 255, 150, 210),
            tex: ACMAsset.LightShot, uvScroll: -Main.GlobalTimeWrappedHourly * 2.4f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D lsh = ACMAsset.LightShot;
        Texture2D sg = ACMAsset.SoftGlow;

        sb.Draw(lsh, Projectile.Center - Main.screenPosition, null,
            new Color(100, 255, 120),
            Projectile.rotation, lsh.Size() * 0.5f,
            new Vector2(0.85f, 0.20f), SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(80, 220, 90) * 0.75f, 0f,
            sg.Size() * 0.5f,
            0.45f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }

    public override void OnKill(int timeLeft) {
        SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f }, Projectile.Center);
        for (int i = 0; i < 8; i++) {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(4f, 4f), 60, default, 1.5f);
            d.noGravity = true;
        }
    }
}

/// <summary>
/// 螺旋叶刃 - 两侧释放的螺旋飞行叶刃，初期螺旋飞行后追踪最近敌人
/// 参照CelestialCircletOrb的 orbit → homing 模式
/// </summary>
public class DivineWoodSpiralLeaf : ModProjectile
{
    public override string Texture
        => "AncientChineseMythology/Textures/Masking/BlankStar";

    public override void SetStaticDefaults() {
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.TrailCacheLength[Type] = 10;
    }

    private bool _homing;
    private float _spiralTimer;
    private const float SPIRAL_DURATION = 40f;

    public override void SetDefaults() {
        Projectile.width = 20;
        Projectile.height = 20;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override void AI() {
        _spiralTimer++;
        Projectile.rotation += 0.25f;

        if (!_homing && _spiralTimer < SPIRAL_DURATION) {
            float baseAngle = Projectile.ai[0];
            float speed = Projectile.velocity.Length();
            float spiralAngle = baseAngle + _spiralTimer * 0.15f;
            float spiralRadius = MathHelper.Lerp(40f, 8f, _spiralTimer / SPIRAL_DURATION);
            Vector2 spiralOffset = new Vector2(spiralRadius, 0).RotatedBy(spiralAngle);
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = new Vector2(-forward.Y, forward.X);
            Projectile.Center += perp * spiralOffset.X * 0.15f;
        }
        else {
            _homing = true;
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

        Lighting.AddLight(Projectile.Center, 0.15f, 0.5f, 0.15f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        target.AddBuff(BuffID.Poisoned, 240);
        for (int i = 0; i < 6; i++) {
            Dust d = Dust.NewDustPerfect(target.Center, DustID.JungleTorch,
                Main.rand.NextVector2Circular(4f, 4f), 60, default, 1.5f);
            d.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) {
        // 细双层 ribbon 拖尾 (螺旋叶刃)
        WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 4f,
            outerColor: new Color(20, 110, 55, 140), innerColor: new Color(170, 255, 150, 200),
            uvScroll: -Main.GlobalTimeWrappedHourly * 2f);

        SpriteBatch sb = Main.spriteBatch;
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);

        Texture2D star = ACMAsset.BlankStar;
        Texture2D sg = ACMAsset.SoftGlow;
        float pulse = 0.6f + 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 0.18f);

        sb.Draw(star, Projectile.Center - Main.screenPosition, null,
            new Color(80, 255, 100) * (0.85f * pulse),
            Projectile.rotation, star.Size() * 0.5f,
            0.60f, SpriteEffects.None, 0);
        sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
            new Color(140, 255, 160) * (0.65f * pulse), 0f,
            sg.Size() * 0.5f,
            0.50f, SpriteEffects.None, 0);

        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null,
            Main.GameViewMatrix.TransformationMatrix);
        return false;
    }
}
