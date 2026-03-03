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
            Item.width  = 52;
            Item.height = 52;
            Item.useTime      = 42;
            Item.useAnimation = 42;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 12;
            Item.crit  = 20;
            Item.mana  = 35;
            Item.value = Item.buyPrice(gold: 200);
            Item.rare  = ItemRarityID.Purple;
            Item.autoReuse    = true;
            Item.noMelee      = true;
            Item.shoot = ModContent.ProjectileType<GeoarchonMarker>();
            Item.shootSpeed = 22f;
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
            Projectile.width  = 14;
            Projectile.height = 14;
            Projectile.friendly    = true;
            Projectile.tileCollide = true;
            Projectile.penetrate   = 1;
            Projectile.timeLeft    = 120;
            Projectile.DamageType  = DamageClass.Magic;
            Projectile.light       = 0.8f;
        }

        public override void AI() {
            Projectile.rotation += 0.18f;

            // 飞行粒子
            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center,
                    DustID.AmberBolt,
                    Main.rand.NextVector2Circular(5, 5), 0,
                    new Color(255, 160, 30), 1.5f);
                d.noGravity = true;
            }

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
                // 地面碎裂尘埃
                for (int i = 0; i < 28; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center,
                        DustID.AmberBolt,
                        Main.rand.NextVector2Circular(12f, 12f), 0,
                        new Color(240, 150, 30), Main.rand.NextFloat(2f, 4.5f));
                    d.noGravity = false;
                }
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
                    spawnPos, Vector2.Zero,
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
                new Color(255, 140, 20, 0) * 0.85f, Projectile.rotation,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                0.5f, SpriteEffects.None, 0);

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
        private ref float Delay    => ref Projectile.ai[0];
        private ref float LiveTime => ref Projectile.localAI[0];
        private const float LIFETIME = 65f;

        public override void SetDefaults() {
            Projectile.width  = 60;
            Projectile.height = 120;
            Projectile.friendly    = true;
            Projectile.tileCollide = false;
            Projectile.penetrate   = -1;
            Projectile.timeLeft    = (int)(LIFETIME + 30f);
            Projectile.DamageType  = DamageClass.Magic;
            Projectile.alpha       = 255;
            Projectile.usesLocalNPCImmunity  = true;
            Projectile.localNPCHitCooldown   = 8;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Delay > 0) { Delay--; return; }
            LiveTime++;
            Projectile.alpha = (int)MathHelper.Lerp(0, 255,
                LiveTime > LIFETIME ? (LiveTime - LIFETIME) / 16f : 0f);

            // 粒子
            if (LiveTime < LIFETIME && Main.rand.NextBool(3)) {
                float h = Projectile.height * 0.5f;
                Vector2 pos = Projectile.Center +
                    new Vector2(Main.rand.NextFloat(-20f, 20f),
                                Main.rand.NextFloat(-h, 0));
                Dust d = Dust.NewDustPerfect(pos, DustID.AmberBolt,
                    new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-8f, -2f)),
                    0, new Color(255, 160, 20), Main.rand.NextFloat(1.5f, 3f));
                d.noGravity = true;
            }
            if (LiveTime < LIFETIME && Main.rand.NextBool(6)) {
                Dust de = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-25, 25), -Projectile.height * 0.5f),
                    DustID.GlowingMushroom,
                    new Vector2(0, Main.rand.NextFloat(-5, -2)), 0,
                    new Color(100, 240, 80), 2.0f);
                de.noGravity = true;
            }
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

            float prog  = Math.Min(LiveTime / LIFETIME, 1f);
            float enter = Math.Min(LiveTime / 12f, 1f);
            float exit  = LiveTime > LIFETIME ? 1f - (LiveTime - LIFETIME) / 16f : 1f;
            float alpha = enter * exit;

            // 纵向拉伸 Y
            float scaleY = MathHelper.SmoothStep(0f, 1f, enter);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D slash = ACMAsset.SlashBurst;
            Texture2D sg    = ACMAsset.SoftGlow;
            Texture2D em    = ACMAsset.EmberShards;

            // 主柱（SlashBurst，正面朝上，纵向扩展）
            Color mainCol = new Color(255, 160, 20, 0) * alpha;
            sb.Draw(slash, Projectile.Center - Main.screenPosition, null,
                mainCol,
                -MathHelper.PiOver2, // 旋转使纹理向上
                new Vector2(slash.Width * 0.5f, slash.Height), // 底部锚点
                new Vector2(0.55f, scaleY * 0.9f), SpriteEffects.None, 0);

            // 绿色地能光晕叠加
            Color geoCol = new Color(80, 230, 60, 0) * alpha * 0.55f;
            sb.Draw(slash, Projectile.Center - Main.screenPosition, null,
                geoCol, -MathHelper.PiOver2,
                new Vector2(slash.Width * 0.5f, slash.Height),
                new Vector2(0.4f, scaleY * 0.75f), SpriteEffects.None, 0);

            // 底部 SoftGlow
            sb.Draw(sg, Projectile.Center - Main.screenPosition, null,
                new Color(255, 180, 30, 0) * alpha * 0.9f, 0f,
                new Vector2(sg.Width * 0.5f, sg.Height * 0.5f),
                1.4f, SpriteEffects.None, 0);

            // EmberShards 碎片
            if (LiveTime < LIFETIME * 0.5f) {
                Color emCol = new Color(255, 130, 20, 0) * alpha * 0.35f;
                sb.Draw(em, Projectile.Center - Main.screenPosition, null,
                    emCol, prog * MathHelper.TwoPi,
                    new Vector2(em.Width * 0.5f, em.Height * 0.5f),
                    0.25f, SpriteEffects.None, 0);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}

