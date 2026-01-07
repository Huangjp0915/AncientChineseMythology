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
    /// 玉龙云刀 - 天庭观察者掉落的近战刀
    /// 挥舞时发射云龙剑气，蓄力后释放龙形斩击
    /// </summary>
    public class JadeDragonCloudDao : ModItem
    {
        private int chargeCounter = 0;
        private const int MaxCharge = 3;

        public override void SetDefaults() {
            Item.damage = 3380;
            Item.DamageType = DamageClass.Melee;
            Item.width = 60;
            Item.height = 60;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<JadeDragonSlash>();
            Item.shootSpeed = 14f;
            Item.scale = 1.15f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            chargeCounter++;

            // 普通攻击发射玉龙剑气
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);

            // 每3次攻击发射一次龙形大斩击
            if (chargeCounter >= MaxCharge) {
                chargeCounter = 0;

                // 发射龙形斩击
                Projectile.NewProjectile(source, position, velocity * 0.8f,
                    ModContent.ProjectileType<JadeDragonWave>(), (int)(damage * 1.8f), knockback * 1.5f, player.whoAmI);

                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 0.6f }, player.Center);
                player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(5, 15);
            }

            return false;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            // 挥舞时产生玉绿色云气粒子
            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = new Vector2(hitbox.X + Main.rand.Next(hitbox.Width), hitbox.Y + Main.rand.Next(hitbox.Height));
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GreenTorch, player.velocity.X * 0.2f, player.velocity.Y * 0.2f, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            // 金色光粒
            if (Main.rand.NextBool(4)) {
                Vector2 dustPos = new Vector2(hitbox.X + Main.rand.Next(hitbox.Width), hitbox.Y + Main.rand.Next(hitbox.Height));
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            // 有几率附加天机印记减速
            if (Main.rand.NextBool(4)) {
                target.AddBuff(BuffID.Slow, 120);
            }

            // 暴击时爆发玉龙之息
            if (hit.Crit) {
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                    int dust = Dust.NewDust(target.Center, 0, 0, DustID.GreenTorch, vel.X, vel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }

                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.3f, Volume = 0.5f }, target.Center);
            }
        }

        public override void AddRecipes() {
            // 可添加合成配方
        }
    }

    /// <summary>
    /// 玉龙剑气 - 普通攻击发射的剑气
    /// </summary>
    public class JadeDragonSlash : ModProjectile
    {
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            // 缩小效果
            if (Projectile.timeLeft < 20) {
                Projectile.scale *= 0.95f;
            }

            // 云气粒子
            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GreenTorch, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            // 金色粒子
            if (Main.rand.NextBool(4)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 0.8f);
                Main.dust[dust].noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.3f, 0.8f, 0.4f) * 0.6f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 击中粒子效果
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.GreenTorch, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // 使用剑气纹理绘制
            Texture2D texture = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, texture.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 计算拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(100, 255, 150) * progress * 0.4f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float scale = Projectile.scale * progress * 0.8f;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i] - MathHelper.PiOver4,
                    origin, new Vector2(0.6f, 0.15f * scale), SpriteEffects.None, 0f);
            }

            // 主体
            Color mainColor = new Color(150, 255, 180);
            mainColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, mainColor, Projectile.rotation - MathHelper.PiOver4,
                origin, new Vector2(0.8f, 0.2f * Projectile.scale), SpriteEffects.None, 0f);

            // 核心高光
            Color coreColor = new Color(200, 255, 220);
            coreColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, coreColor * 0.8f, Projectile.rotation - MathHelper.PiOver4,
                origin, new Vector2(0.6f, 0.1f * Projectile.scale), SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            // 消散粒子
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3, 3);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GreenTorch, vel.X, vel.Y, 100, default, 1.3f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 玉龙波 - 蓄力后发射的龙形大斩击
    /// </summary>
    public class JadeDragonWave : ModProjectile
    {
        private float wavePhase = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 20;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.scale = 1.5f;
        }

        public override void AI() {
            wavePhase += 0.15f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 龙形波动
            float waveOffset = MathF.Sin(wavePhase * 2f) * 3f;
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            Projectile.position += perpendicular * waveOffset * 0.3f;

            // 缓慢减速
            Projectile.velocity *= 0.995f;

            // 龙气粒子
            if (Main.rand.NextBool()) {
                Vector2 offset = Main.rand.NextVector2Circular(30, 15);
                int dust = Dust.NewDust(Projectile.Center + offset, 0, 0, DustID.GreenTorch, 0, 0, 100, default, 2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(2, 2);
            }

            // 金色龙鳞粒子
            if (Main.rand.NextBool(3)) {
                Vector2 offset = Main.rand.NextVector2Circular(25, 10);
                int dust = Dust.NewDust(Projectile.Center + offset, 0, 0, DustID.GoldCoin, 0, 0, 50, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            // 云气尾迹
            for (int i = 0; i < 2; i++) {
                float angle = wavePhase + i * MathHelper.Pi;
                Vector2 dustPos = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;
                dustPos += perpendicular * MathF.Sin(angle) * 15f;
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.Cloud, 0, 0, 150, new Color(180, 255, 200), 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0.3f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.4f, 1f, 0.5f) * 0.8f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 龙息爆发
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(8, 8);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.GreenTorch, vel.X, vel.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.3f, Volume = 0.6f }, target.Center);

            // 附加减速
            target.AddBuff(BuffID.Slow, 180);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, texture.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 龙形拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                // 龙身波动
                float bodyWave = MathF.Sin(wavePhase + i * 0.5f) * 0.3f;
                float bodyScale = progress * (0.8f + bodyWave * 0.2f);

                Color trailColor = Color.Lerp(new Color(100, 255, 150), new Color(200, 255, 100), 1f - progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i],
                    origin, new Vector2(1.2f * bodyScale, 0.35f * bodyScale * Projectile.scale), SpriteEffects.None, 0f);
            }

            // 龙头（主体）
            float headPulse = 1f + MathF.Sin(wavePhase * 3f) * 0.1f;
            Color headColor = new Color(150, 255, 180);
            headColor.A = 0;

            // 龙眼高光
            Color eyeColor = new Color(255, 255, 200);
            eyeColor.A = 0;

            // 龙须光效
            if (ACMAsset.Sparkle != null) {
                Color sparkleColor = new Color(200, 255, 220) * 0.4f;
                sparkleColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.Sparkle, drawPos + Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f,
                    null, sparkleColor, wavePhase, ACMAsset.Sparkle.Size() / 2f, 0.8f * headPulse, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            // 龙魂消散
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.2f }, Projectile.Center);

            for (int i = 0; i < 25; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.GreenTorch : DustID.GoldCoin;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            // 云气爆散
            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10;
                Vector2 vel = angle.ToRotationVector2() * 5f;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Cloud, vel.X, vel.Y, 150, new Color(180, 255, 200), 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
