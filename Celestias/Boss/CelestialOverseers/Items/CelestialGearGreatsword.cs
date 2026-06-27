using AncientChineseMythology.Helpers;
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
    /// 天机齿轮巨剑 - 天庭观察者掉落的近战大剑
    /// 挥舞时旋转的齿轮造成多段伤害，蓄力释放齿轮风暴
    /// </summary>
    public class CelestialGearGreatsword : ModItem
    {
        private int swingCount = 0;
        private const int MaxSwings = 4;

        public override void SetDefaults() {
            Item.damage = 2420;
            Item.DamageType = DamageClass.Melee;
            Item.width = 80;
            Item.height = 80;
            Item.useTime = 28;
            Item.useAnimation = 28;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 8f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.scale = 1.3f;
            Item.shoot = ModContent.ProjectileType<CelestialGearProjectile>();
            Item.shootSpeed = 0f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            swingCount++;

            // 每次挥舞释放齿轮
            Vector2 toMouse = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(source, player.Center, toMouse * 12f, type, damage / 2, knockback, player.whoAmI);

            // 每4次挥舞释放齿轮风暴
            if (swingCount >= MaxSwings) {
                swingCount = 0;

                // 释放多个齿轮
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6 + Main.rand.NextFloat(-0.2f, 0.2f);
                    Vector2 vel = angle.ToRotationVector2() * 10f;
                    Projectile.NewProjectile(source, player.Center, vel,
                        ModContent.ProjectileType<CelestialGearStorm>(), (int)(damage * 0.8f), knockback, player.whoAmI, ai0: i);
                }

                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.3f }, player.Center);
                player.GetModPlayer<ScreenShakePlayer>().ShakeScreen(8, 20);
            }

            return false;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            // 挥舞时产生金色齿轮粒子
            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = new Vector2(hitbox.X + Main.rand.Next(hitbox.Width), hitbox.Y + Main.rand.Next(hitbox.Height));
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, player.velocity.X * 0.2f, player.velocity.Y * 0.2f, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            // 机械火花
            if (Main.rand.NextBool(5)) {
                Vector2 dustPos = new Vector2(hitbox.X + Main.rand.Next(hitbox.Width), hitbox.Y + Main.rand.Next(hitbox.Height));
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.Torch, Main.rand.NextFloat(-2, 2), Main.rand.NextFloat(-2, 2), 100, default, 1f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            // 齿轮撕裂效果
            if (hit.Crit) {
                // 造成额外流血效果（使用着火代替）
                target.AddBuff(BuffID.OnFire, 180);

                // 齿轮爆发
                for (int i = 0; i < 10; i++) {
                    float angle = MathHelper.TwoPi * i / 10;
                    Vector2 vel = angle.ToRotationVector2() * 5f;
                    int dust = Dust.NewDust(target.Center, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }

                SoundEngine.PlaySound(SoundID.Item22 with { Pitch = 0.5f }, target.Center);
            }

            ACMWeaponBurst.Spawn(player.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.ClockworkGold, hit.Crit ? 1.5f : 1f, player.whoAmI);
            WeaponVFX.AddScreenShake(target.Center, hit.Crit ? 3f : 2f);
        }
    }

    /// <summary>
    /// 天机齿轮 - 普通攻击发射的齿轮
    /// </summary>
    public class CelestialGearProjectile : ModProjectile
    {
        private float rotationSpeed = 0.3f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 45;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            Projectile.rotation += rotationSpeed;

            // 加速旋转
            rotationSpeed += 0.01f;
            if (rotationSpeed > 0.6f) rotationSpeed = 0.6f;

            // 齿轮粒子
            if (Main.rand.NextBool(3)) {
                float dustAngle = Projectile.rotation + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dustPos = Projectile.Center + dustAngle.ToRotationVector2() * 15f;
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = dustAngle.ToRotationVector2() * 2f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.5f) * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 齿轮切割粒子
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item22 with { Pitch = 0.8f, Volume = 0.5f }, target.Center);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.ClockworkGold, 0.8f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 14f,
                outerColor: new Color(200, 130, 30, 120), innerColor: new Color(255, 240, 180, 175),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.3f);

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(255, 220, 100) * progress * 0.4f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i],
                    origin, Projectile.scale * progress, SpriteEffects.None, 0f);
            }

            // 外层光晕
            Color glowColor = new Color(255, 230, 150) * 0.5f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, glowColor, Projectile.rotation,
                origin, Projectile.scale * 1.2f, SpriteEffects.None, 0f);

            // 主体
            Main.spriteBatch.Draw(texture, drawPos, null, lightColor, Projectile.rotation,
                origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10;
                Vector2 vel = angle.ToRotationVector2() * 3f;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.5f, Volume = 0.4f }, Projectile.Center);
        }
    }

    /// <summary>
    /// 天机齿轮风暴 - 蓄力后释放的多个旋转齿轮
    /// </summary>
    public class CelestialGearStorm : ModProjectile
    {
        private float orbitAngle;
        private float orbitRadius = 50f;
        private float expandTimer = 0f;
        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            expandTimer++;

            // 初始化轨道角度
            if (expandTimer == 1) {
                orbitAngle = Projectile.ai[0] * MathHelper.TwoPi / 6;
            }

            // 快速旋转
            Projectile.rotation += 0.4f;
            orbitAngle += 0.08f;

            // 扩散轨道
            float targetRadius = 150f + MathF.Sin(expandTimer * 0.1f) * 30f;
            orbitRadius = MathHelper.Lerp(orbitRadius, targetRadius, 0.05f);

            // 环绕玩家
            Vector2 targetPos = Owner.Center + orbitAngle.ToRotationVector2() * orbitRadius;
            Projectile.velocity = (targetPos - Projectile.Center) * 0.15f;

            // 齿轮粒子
            if (Main.rand.NextBool(4)) {
                float dustAngle = Projectile.rotation;
                Vector2 dustPos = Projectile.Center + dustAngle.ToRotationVector2() * 20f;
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldCoin, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }

            // 火花
            if (Main.rand.NextBool(6)) {
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Torch, Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(-3, 3), 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.5f) * 0.6f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 齿轮撕裂
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }

            target.AddBuff(BuffID.OnFire, 120);

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.ClockworkGold, 1.2f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 18f,
                outerColor: new Color(200, 110, 25, 110), innerColor: new Color(255, 235, 160, 175),
                uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(255, 200, 50) * progress * 0.3f;
                trailColor.A = 0;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, trailColor, Projectile.oldRot[i],
                    origin, Projectile.scale * progress * 0.9f, SpriteEffects.None, 0f);
            }

            // 光晕
            Color glowColor = new Color(255, 220, 100) * 0.4f;
            glowColor.A = 0;
            Main.spriteBatch.Draw(texture, drawPos, null, glowColor, Projectile.rotation,
                origin, Projectile.scale * 1.3f, SpriteEffects.None, 0f);

            // 主体
            Main.spriteBatch.Draw(texture, drawPos, null, lightColor, Projectile.rotation,
                origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12;
                Vector2 vel = angle.ToRotationVector2() * 4f;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.GoldCoin, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
