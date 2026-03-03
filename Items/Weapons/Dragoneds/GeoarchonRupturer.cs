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
    /// 坤铉破渊权杖 —— 超级毕业法杖，在鼠标位置召唤7根巨型地能裂穴能量柱，
    /// 震碎大地、喷涌岩浆晶体，对附近所有敌人造成持续灼烧伤害
    /// </summary>
    public class GeoarchonRupturer : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 500;
            Item.DamageType = DamageClass.Magic;
            Item.width = 52;
            Item.height = 52;
            Item.useTime = 42;
            Item.useAnimation = 42;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 12;
            Item.crit = 20;
            Item.mana = 35;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare = ItemRarityID.Purple;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<GeoarchonMarker>();
            Item.shootSpeed = 22f;
            Item.staff[Type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            SoundEngine.PlaySound(SoundID.Item46, player.position);
            Projectile.NewProjectile(source, position, velocity,
                type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 地标弹幕：飞向并到达鼠标后召唤裂穴柱阵
    // ──────────────────────────────────────────────────────────────
    public class GeoarchonMarker : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Textures/Masking/SoftGlow";

        private bool _erupted = false;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.light = 0.8f;
        }

        public override void AI() {
            Projectile.rotation += 0.18f;

            // 接近目标或碰墙则引爆
            if (!_erupted && Projectile.velocity.Length() < 0.5f) {
                Erupt();
            }
        }

        public override void OnKill(int timeLeft) {
            if (!_erupted) Erupt();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (!_erupted) Erupt();
            return true;
        }

        private void Erupt() {
            _erupted = true;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode, Projectile.position);
                Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>()
                    .ShakeScreen(14f, 22);
            }

            // 7根裂穴柱：中心1根 + 外围6根（正六边形）
            int count = 7;
            float radius = 100f;
            for (int i = 0; i < count; i++) {
                float angle = i == 0 ? 0f : MathHelper.TwoPi * (i - 1) / (count - 1);
                Vector2 spawnPos = i == 0
                    ? Projectile.Center
                    : Projectile.Center + new Vector2(radius, 0).RotatedBy(angle);
                // 稍微错开延迟 → 使用 ai[0] 保存偏移
                Projectile.NewProjectile(
                    Projectile.GetSource_Death(),
                    spawnPos, Projectile.velocity.UnitVector(),
                    ModContent.ProjectileType<GeoarchonPillar>(),
                    Projectile.damage, Projectile.knockBack, Projectile.owner,
                    ai0: i * 4f); // 延迟帧数
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D sg = ACMAsset.SoftGlow;
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(255, 140, 20) * 0.90f, Projectile.rotation,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                0.6f, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 裂穴能量柱：垂直向上的巨型能量柱，持续伤害
    // ──────────────────────────────────────────────────────────────
    public class GeoarchonPillar : ModProjectile
    {
        public override string Texture
            => "AncientChineseMythology/Textures/Masking/SlashBurst";

        // ai[0] = 延迟帧数（发光之前等待）
        private ref float Delay => ref Projectile.ai[0];
        private ref float LiveTime => ref Projectile.localAI[0];
        private const float LIFETIME = 65f;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = (int)(LIFETIME + 30f);
            Projectile.DamageType = DamageClass.Magic;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Delay > 0) { Delay--; return; }
            LiveTime++;
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
            Projectile.alpha = (int)MathHelper.Lerp(0, 255,
                LiveTime > LIFETIME ? (LiveTime - LIFETIME) / 16f : 0f);
        }

        public override bool? CanDamage() => LiveTime > 0 && LiveTime < LIFETIME ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 用中线碰撞检测（垂直柱）
            Vector2 top = Projectile.Center - new Vector2(0, Projectile.height * 0.5f);
            Vector2 bot = Projectile.Center + new Vector2(0, Projectile.height * 0.5f);
            float col = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                top, bot, 28f, ref col);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Delay > 0) return false;

            float prog = Math.Min(LiveTime / LIFETIME, 1f);
            float enter = Math.Min(LiveTime / 12f, 1f);
            float exit = LiveTime > LIFETIME ? 1f - (LiveTime - LIFETIME) / 16f : 1f;
            float alpha = enter * exit;

            // 纵向拉伸 Y
            float scaleY = MathHelper.SmoothStep(0f, 1f, enter);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D slash = ACMAsset.SlashBurst;
            Texture2D sg = ACMAsset.SoftGlow;
            Texture2D em = ACMAsset.EmberShards;
            Texture2D bolt = ACMAsset.LightningBranch;
            Texture2D spark = ACMAsset.Sparkle;

            // ── 主柱（SlashBurst 向上，超大纥向拉伸）──
            sb.Draw(slash, Projectile.Center - Main.screenPosition, null,
                new Color(255, 160, 20) * alpha,
                Projectile.rotation,
                new Vector2(slash.Width * 0.5f, slash.Height),
                new Vector2(1.65f, scaleY * 3.6f), SpriteEffects.None, 0);

            // 绿色地能光晕叠层
            sb.Draw(slash, Projectile.Center - Main.screenPosition, null,
                new Color(80, 230, 60) * (alpha * 0.55f),
                Projectile.rotation,
                new Vector2(slash.Width * 0.5f, slash.Height),
                new Vector2(1.15f, scaleY * 2.80f), SpriteEffects.None, 0);

            // ── 两侧 LightningBranch 闪电 ──
            sb.Draw(bolt, Projectile.Center - Main.screenPosition, null,
                new Color(255, 200, 50) * (alpha * 0.70f),
                Projectile.rotation + 0.10f,
                new Vector2(bolt.Width * 0.5f, bolt.Height),
                new Vector2(0.55f, scaleY * 2.50f), SpriteEffects.None, 0);
            sb.Draw(bolt, Projectile.Center - Main.screenPosition, null,
                new Color(255, 200, 50) * (alpha * 0.70f),
                Projectile.rotation - 0.10f,
                new Vector2(bolt.Width * 0.5f, bolt.Height),
                new Vector2(-0.55f, scaleY * 2.50f), SpriteEffects.None, 0);

            // ── 柱顶 Sparkle 星芒 ──
            Vector2 topPos = Projectile.Center
                - new Vector2(0, Projectile.height * scaleY * 2.20f);
            sb.Draw(spark, topPos - Main.screenPosition, null,
                new Color(255, 220, 80) * (alpha * 0.80f),
                (float)Main.timeForVisualEffects * 0.06f,
                new Vector2(spark.Width * 0.5f, spark.Height * 0.5f),
                1.10f, SpriteEffects.None, 0);

            // ── 底部 SoftGlow 大光团 ──
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(255, 180, 30) * (alpha * 0.90f), 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                2.8f, SpriteEffects.None, 0);

            if (LiveTime < LIFETIME * 0.55f) {
                sb.Draw(em, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 140, 20) * (alpha * 0.55f),
                    prog * MathHelper.TwoPi,
                    new Vector2(em.Width * 0.5f, em.Height * 0.5f),
                    0.55f, SpriteEffects.None, 0);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}

