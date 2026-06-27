using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialOverseers.Items
{
    /// <summary>
    /// 天机弓 - 天庭观察者掉落的远程弓
    /// 发射追踪的神圣光箭，蓄力后发射光柱箭雨
    /// </summary>
    public class CelestialMechanismBow : ModItem
    {
        private int chargeCounter = 0;
        private const int MaxCharge = 5;

        public override void SetDefaults() {
            Item.damage = 2280;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 32;
            Item.height = 64;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.Arrow;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            // 转换为神圣光箭
            type = ModContent.ProjectileType<CelestialLightArrow>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            chargeCounter++;

            // 发射主箭
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);

            // 左右各发射一支辅助箭
            float spread = MathHelper.ToRadians(8);
            Projectile.NewProjectile(source, position, velocity.RotatedBy(spread), type, (int)(damage * 0.7f), knockback, player.whoAmI);
            Projectile.NewProjectile(source, position, velocity.RotatedBy(-spread), type, (int)(damage * 0.7f), knockback, player.whoAmI);

            // 每5次发射触发光柱箭雨
            if (chargeCounter >= MaxCharge) {
                chargeCounter = 0;

                // 从天空降下光柱箭
                Vector2 targetPos = Main.MouseWorld;
                for (int i = 0; i < 5; i++) {
                    float offsetX = (i - 2) * 100f + Main.rand.NextFloat(-20, 20);
                    Vector2 spawnPos = new Vector2(targetPos.X + offsetX, targetPos.Y - 600);
                    Vector2 arrowVel = new Vector2(0, 20f);

                    Projectile.NewProjectile(source, spawnPos, arrowVel,
                        ModContent.ProjectileType<CelestialRainArrow>(), (int)(damage * 1.5f), knockback, player.whoAmI);
                }

                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f, Volume = 0.8f }, player.Center);
                player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(6, 20);
            }

            return false;
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-2, 0);
        }
    }

    /// <summary>
    /// 神圣光箭 - 普通发射的追踪光箭
    /// </summary>
    public class CelestialLightArrow : ModProjectile
    {
        private bool hasHomed = false;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 光箭粒子
            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.7f) * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 光箭爆发
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.ClockworkGold, 0.8f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 机关金 + 天青双层 ribbon
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 9f,
                outerColor: new Color(80, 200, 180, 120), innerColor: new Color(255, 240, 180, 180),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.5f);

            Texture2D texture = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, texture.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(255, 240, 180) * progress * 0.4f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor,
                    Projectile.oldRot[i] - MathHelper.PiOver2, origin, new Vector2(0.5f * progress, 0.08f * progress), SpriteEffects.None, 0f);
            }

            // 主体
            Color mainColor = new Color(255, 250, 200);
            mainColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, mainColor, Projectile.rotation - MathHelper.PiOver2,
                origin, new Vector2(0.6f, 0.1f), SpriteEffects.None, 0f);

            // 箭头光点
            if (ACMAsset.LightShot != null) {
                Color coreColor = new Color(255, 255, 220);
                coreColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos, null, coreColor * 0.6f, 0f,
                    ACMAsset.LightShot.Size() / 2f, 0.3f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3, 3);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 天降光柱箭 - 从天空降下的大型光箭
    /// </summary>
    public class CelestialRainArrow : ModProjectile
    {
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 20;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 加速下落
            if (Projectile.velocity.Y < 25f) {
                Projectile.velocity.Y += 0.3f;
            }

            // 光柱粒子
            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.15f;
            }

            // 星光粒子
            if (Main.rand.NextBool(4)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.YellowStarDust, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.95f, 0.7f) * 0.8f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 光柱爆发
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15;
                Vector2 vel = angle.ToRotationVector2() * 5f;
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.YellowStarDust;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            target.AddBuff(BuffID.Slow, 90);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.5f, Volume = 0.5f }, target.Center);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.ClockworkGold, 1.4f, Projectile.owner);
            WeaponVFX.AddScreenShake(target.Center, 3f);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 光柱箭金芒双层 ribbon
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 16f,
                outerColor: new Color(200, 130, 30, 120), innerColor: new Color(255, 245, 190, 185),
                tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 1.6f);

            Texture2D texture = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, texture.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 长光柱拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(255, 240, 180) * progress * 0.5f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor,
                    Projectile.oldRot[i] - MathHelper.PiOver2, origin, new Vector2(0.8f * progress, 0.15f * progress), SpriteEffects.None, 0f);
            }

            // 外层光晕
            Color glowColor = new Color(255, 220, 150) * 0.5f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, glowColor, Projectile.rotation - MathHelper.PiOver2,
                origin, new Vector2(1f, 0.25f), SpriteEffects.None, 0f);

            // 核心
            Color mainColor = new Color(255, 250, 200);
            mainColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, mainColor, Projectile.rotation - MathHelper.PiOver2,
                origin, new Vector2(0.8f, 0.15f), SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            // 落地爆发
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.3f }, Projectile.Center);

            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12;
                Vector2 vel = angle.ToRotationVector2() * 4f;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
