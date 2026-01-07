using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialOverseers.Items
{
    /// <summary>
    /// 天眼监察杖 - 天庭观察者掉落的魔法法杖
    /// 召唤神圣光柱从天而降，类似Boss的光柱审判攻击
    /// </summary>
    public class CelestialWatcherStaff : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 1400;
            Item.DamageType = DamageClass.Magic;
            Item.width = 48;
            Item.height = 48;
            Item.useTime = 35;
            Item.useAnimation = 35;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<CelestialWatcherPillar>();
            Item.shootSpeed = 0f;
            Item.mana = 20;
            Item.staff[Item.type] = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 targetPos = Main.MouseWorld;

            // 召唤3道光柱
            for (int i = -1; i <= 1; i++) {
                float offsetX = i * 120f;
                Vector2 pillarPos = new Vector2(targetPos.X + offsetX, targetPos.Y - 600);
                Projectile.NewProjectile(source, pillarPos, new Vector2(0, 25f), type, damage, knockback, player.whoAmI, ai0: i);
            }

            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f }, targetPos);
            player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(8, 25);

            // 预警粒子
            for (int i = -1; i <= 1; i++) {
                float offsetX = i * 120f;
                Vector2 warnPos = new Vector2(targetPos.X + offsetX, targetPos.Y);
                for (int j = 0; j < 8; j++) {
                    int dust = Dust.NewDust(warnPos + new Vector2(0, -400 + j * 100), 0, 0, DustID.GoldCoin, 0, 3f, 100, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            return false;
        }

        public override void ModifyManaCost(Player player, ref float reduce, ref float mult) {
            // 低生命时减少魔力消耗
            if (player.statLife < player.statLifeMax2 * 0.3f) {
                reduce += 0.2f;
            }
        }
    }

    /// <summary>
    /// 天眼审判光柱 - 从天而降的神圣光柱
    /// </summary>
    public class CelestialWatcherPillar : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";
        private float pillarAlpha = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 600;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 50;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            Projectile.ai[1]++;

            // 淡入淡出
            if (Projectile.ai[1] < 10) {
                pillarAlpha = MathHelper.Lerp(pillarAlpha, 1f, 0.2f);
            }
            else if (Projectile.timeLeft < 15) {
                pillarAlpha = MathHelper.Lerp(pillarAlpha, 0f, 0.15f);
            }

            // 光柱粒子
            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-25, 25), Main.rand.NextFloat(-300, 300));
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, -2f, 100, default, 1.5f * pillarAlpha);
                Main.dust[dust].noGravity = true;
            }

            // 强烈光照
            for (int i = 0; i < 6; i++) {
                Vector2 lightPos = Projectile.Center + new Vector2(0, -250 + i * 100);
                Lighting.AddLight(lightPos, new Vector3(1f, 0.95f, 0.7f) * 1.2f * pillarAlpha);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 审判效果
            target.AddBuff(BuffID.OnFire, 180);
            target.AddBuff(BuffID.Slow, 120);

            // 光柱爆发
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12;
                Vector2 vel = angle.ToRotationVector2() * 5f;
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.YellowStarDust;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.5f, Volume = 0.5f }, target.Center);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Rectangle pillarBox = new Rectangle(
                (int)Projectile.Center.X - 25,
                (int)Projectile.Center.Y - 300,
                50,
                600
            );
            return pillarBox.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (ACMAsset.GlaciateWave == null) return false;

            Texture2D pillarTex = ACMAsset.GlaciateWave;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = pillarTex.Size() / 2f;

            // 旋转90度使其垂直
            float rotation = MathHelper.PiOver2;

            // 外层光晕
            Color outerColor = new Color(255, 220, 150) * pillarAlpha * 0.4f;
            outerColor.A = 0;
            Main.spriteBatch.Draw(pillarTex, drawPos, null, outerColor, rotation, origin,
                new Vector2(1200f / pillarTex.Width, 0.35f), SpriteEffects.None, 0f);

            // 中层
            Color midColor = new Color(255, 240, 180) * pillarAlpha * 0.6f;
            midColor.A = 0;
            Main.spriteBatch.Draw(pillarTex, drawPos, null, midColor, rotation, origin,
                new Vector2(1200f / pillarTex.Width, 0.2f), SpriteEffects.None, 0f);

            // 核心
            Color coreColor = new Color(255, 255, 220) * pillarAlpha * 0.8f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(pillarTex, drawPos, null, coreColor, rotation, origin,
                new Vector2(1200f / pillarTex.Width, 0.1f), SpriteEffects.None, 0f);

            // 顶部星光
            if (ACMAsset.Sparkle != null) {
                Vector2 topPos = drawPos + new Vector2(0, -280);
                Color sparkleColor = new Color(255, 250, 200) * pillarAlpha * 0.5f;
                sparkleColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.Sparkle, topPos, null, sparkleColor,
                    Projectile.ai[1] * 0.1f, ACMAsset.Sparkle.Size() / 2f, 1.5f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            // 消散光效
            for (int i = 0; i < 15; i++) {
                Vector2 dustPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-25, 25), Main.rand.NextFloat(-300, 300));
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
