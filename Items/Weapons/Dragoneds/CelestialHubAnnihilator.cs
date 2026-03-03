using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Items.Weapons.Dragoneds
{
    /// <summary>
    /// 天枢聚能湮灭炮 —— 超级毕业炮，发射贯穿全场的天枢星力炮弹，
    /// 弹着点引发全屏级湮灭爆炸，并生成残留星力漩涡
    /// </summary>
    public class CelestialHubAnnihilator : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 560;
            Item.DamageType = DamageClass.Ranged;
            Item.width  = 80;
            Item.height = 30;
            Item.useTime      = 38;
            Item.useAnimation = 38;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 16;
            Item.crit  = 18;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare  = ItemRarityID.Purple;
            Item.autoReuse    = true;
            Item.notAmmo       = true;
            Item.shoot = ModContent.ProjectileType<CelestialHubShell>();
            Item.shootSpeed = 26f;
            Item.channel      = true; // 充能手感
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            SoundEngine.PlaySound(SoundID.Item92, player.position);
            // 3枚叉展散射，增强毕业感
            for (int i = -1; i <= 1; i++) {
                float spread = MathHelper.ToRadians(i * 2.8f);
                Vector2 vel = velocity.RotatedBy(spread);
                Projectile.NewProjectile(source, position, vel, type,
                    damage + i * 20, knockback, player.whoAmI, ai1: i);
            }
            return false;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 湮灭炮弹：高速穿透炮弹，撞击后制造巨型爆炸
    // ──────────────────────────────────────────────────────────────
    public class CelestialHubShell : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Textures/Masking/LightShot";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type]    = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 18;
        }

        public override void SetDefaults() {
            Projectile.width  = 28;
            Projectile.height = 28;
            Projectile.friendly    = true;
            Projectile.tileCollide = true;
            Projectile.penetrate   = 8; // 穿透8个敌人
            Projectile.timeLeft    = 180;
            Projectile.DamageType  = DamageClass.Ranged;
            Projectile.light       = 1.2f;
            Projectile.usesLocalNPCImmunity  = true;
            Projectile.localNPCHitCooldown   = 6;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Electrified, 180);
            // 命中粒子
            for (int i = 0; i < 12; i++) {
                Dust d = Dust.NewDustPerfect(target.Center,
                    DustID.BlueTorch,
                    Main.rand.NextVector2CircularEdge(8f, 8f),
                    0, new Color(80, 200, 255), 2.5f);
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact, Projectile.position);
            // 震屏
            if (Main.myPlayer >= 0) {
                Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>()
                    .ShakeScreen(20f, 30);
            }
            // 爆炸粒子群
            for (int i = 0; i < 35; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(14f, 14f)
                              * Main.rand.NextFloat(0.4f, 1.6f);
                Dust d = Dust.NewDustPerfect(Projectile.Center,
                    DustID.BlueTorch, vel, 0,
                    new Color(60, 180, 255), Main.rand.NextFloat(2f, 5f));
                d.noGravity = true;
            }
            for (int i = 0; i < 20; i++) {
                Dust ds = Dust.NewDustPerfect(Projectile.Center,
                    DustID.GoldFlame,
                    Main.rand.NextVector2Circular(18f, 18f), 0,
                    new Color(255, 230, 50), Main.rand.NextFloat(2.5f, 5.5f));
                ds.noGravity = true;
            }
            // 生成视觉爆炸弹幕
            Projectile.NewProjectile(
                Projectile.GetSource_Death(),
                Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<CelestialHubExplosion>(),
                0, 0f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D tex = ACMAsset.LightShot;

            // 拖尾——渐变蓝白光束
            for (int i = 1; i < ProjectileID.Sets.TrailCacheLength[Type]; i++) {
                float a = (1f - i / (float)ProjectileID.Sets.TrailCacheLength[Type]) * 0.75f;
                sb.Draw(tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, new Color(80, 200, 255) * a, Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2(0.55f + i * 0.015f, 0.25f), SpriteEffects.None, 0);
                // 白核心补层
                sb.Draw(tex,
                    Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, new Color(220, 245, 255) * (a * 0.45f), Projectile.oldRot[i],
                    new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                    new Vector2(0.30f, 0.12f), SpriteEffects.None, 0);
            }

            // 本体（LightShot 正面朝右，旋转对齐速度）
            sb.Draw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(210, 245, 255),
                Projectile.rotation,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                new Vector2(0.90f, 0.30f), SpriteEffects.None, 0);

            // 中心光核
            Texture2D sg = ACMAsset.SoftGlow;
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(120, 220, 255) * 0.85f, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                0.75f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 视觉爆炸：扩散的星力湮灭爆炸
    // ──────────────────────────────────────────────────────────────
    public class CelestialHubExplosion : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Items/Weapons/Dragoneds/CelestialHubAnnihilator";

        public override void SetDefaults() {
            Projectile.width     = 10;
            Projectile.height    = 10;
            Projectile.friendly  = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft  = 65;
            Projectile.alpha     = 255;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) {
            float prog  = 1f - Projectile.timeLeft / 65f;
            float alpha = MathHelper.SmoothStep(0.9f, 0f, prog);
            float scale = MathHelper.SmoothStep(0f, 24f, ACMUtils.QuadOut(prog));

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D burst   = ACMAsset.SlashBurst;
            Texture2D sparkle = ACMAsset.Sparkle;
            Texture2D sg      = ACMAsset.SoftGlow;
            Texture2D star    = ACMAsset.BlankStar;

            // 四向辐射冲击波（SlashBurst 旋转四次，45° 间隔）
            for (int k = 0; k < 4; k++) {
                float ang = k * MathHelper.PiOver2 + MathHelper.PiOver4;
                sb.Draw(burst, Projectile.Center - Main.screenPosition, null,
                    new Color(50, 180, 255) * (alpha * 0.70f), ang,
                    new Vector2(burst.Width * 0.5f, burst.Height),
                    scale * 0.55f, SpriteEffects.None, 0);
            }

            // 外层双 Sparkle 十字
            sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
                new Color(80, 210, 255) * alpha,
                (float)Main.timeForVisualEffects * 0.015f,
                new Vector2(sparkle.Width * 0.5f, sparkle.Height * 0.5f),
                scale * 0.80f, SpriteEffects.None, 0);
            sb.Draw(sparkle, Projectile.Center - Main.screenPosition, null,
                new Color(200, 240, 255) * (alpha * 0.65f),
                (float)Main.timeForVisualEffects * 0.015f + MathHelper.PiOver4,
                new Vector2(sparkle.Width * 0.5f, sparkle.Height * 0.5f),
                scale * 0.62f, SpriteEffects.None, 0);

            // BlankStar 大星核
            sb.Draw(star, Projectile.Center - Main.screenPosition, null,
                new Color(160, 220, 255) * (alpha * 1.2f),
                (float)Main.timeForVisualEffects * 0.02f,
                new Vector2(star.Width * 0.5f, star.Height * 0.5f),
                scale * 0.50f, SpriteEffects.None, 0);

            // SoftGlow 核心白光
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(240, 255, 255) * alpha,
                0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                scale * 0.30f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}

