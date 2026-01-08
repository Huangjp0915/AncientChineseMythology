using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AncestralDragonSouls.Items
{
    /// <summary>
    /// 祖龙残剑 - 祖龙残魂掉落的近战武器
    /// 一把由远古龙魂凝聚而成的迷幻长剑，挥舞时留下龙魂残影
    /// 特效：攻击时释放龙魂波动，连续攻击积累龙息能量后释放祖龙吐息
    /// </summary>
    public class ArchosaurFerrara : ModItem
    {
        private int comboCounter = 0;
        private const int MaxCombo = 8;

        public override void SetDefaults() {
            Item.damage = 4800;
            Item.DamageType = DamageClass.Melee;
            Item.width = 60;
            Item.height = 60;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6f;
            Item.value = Item.sellPrice(platinum: 1, gold: 50);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<ArchosaurSoulWave>();
            Item.shootSpeed = 16f;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            // 龙魂残影粒子
            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Cloud,
                    1 => DustID.WhiteTorch,
                    _ => DustID.Clentaminator_Cyan
                };
                Vector2 dustPos = new Vector2(
                    hitbox.X + Main.rand.Next(hitbox.Width),
                    hitbox.Y + Main.rand.Next(hitbox.Height)
                );
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, player.direction * 2f, 0, 180, Color.White, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].fadeIn = 1.2f;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            comboCounter++;

            // 发射龙魂波动
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);

            // 连击满后释放祖龙吐息
            if (comboCounter >= MaxCombo) {
                comboCounter = 0;

                // 释放祖龙吐息（多方向龙魂冲击）
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 0.8f }, player.Center);

                for (int i = -2; i <= 2; i++) {
                    float angle = velocity.ToRotation() + MathHelper.ToRadians(i * 12);
                    Vector2 breathVel = angle.ToRotationVector2() * 20f;
                    Projectile.NewProjectile(
                        source,
                        player.Center,
                        breathVel,
                        ModContent.ProjectileType<ArchosaurBreathWave>(),
                        (int)(damage * 1.5f),
                        knockback * 1.5f,
                        player.whoAmI
                    );
                }

                // 祖龙吐息特效
                for (int i = 0; i < 30; i++) {
                    float angle = velocity.ToRotation() + Main.rand.NextFloat(-0.5f, 0.5f);
                    Vector2 dustVel = angle.ToRotationVector2() * Main.rand.NextFloat(4, 10);
                    int dust = Dust.NewDust(player.Center, 0, 0, DustID.Cloud, dustVel.X, dustVel.Y, 180, Color.White, 2.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            return false;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            // 龙魂侵蚀效果
            target.AddBuff(BuffID.Frostburn2, 300);

            // 命中特效
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.WhiteTorch : DustID.Cloud;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 150, Color.White, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 龙魂波动弹幕
    /// </summary>
    public class ArchosaurSoulWave : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float wavePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 15;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 5;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 60;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            wavePhase += 0.15f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 龙魂波动效果
            float wave = MathF.Sin(wavePhase) * 2f;
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            Projectile.position += perpendicular * wave * 0.3f;

            // 渐隐
            Projectile.alpha = (int)(255 * (1f - Projectile.timeLeft / 60f) * 0.5f);

            // 仙气粒子
            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 200, Color.White, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.9f, 0.95f, 1f) * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float alpha = 1f - Projectile.alpha / 255f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(220, 240, 255) * progress * 0.4f * alpha;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin,
                    new Vector2(0.8f * progress, 0.2f * progress), SpriteEffects.None, 0f);
            }

            // 主体
            Color mainColor = new Color(255, 255, 255) * alpha;
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mainColor, Projectile.rotation, origin,
                new Vector2(1f, 0.25f), SpriteEffects.None, 0f);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 命中特效
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.WhiteTorch, vel.X, vel.Y, 150, Color.White, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 祖龙吐息波弹幕 - 连击满后释放的强力攻击
    /// </summary>
    public class ArchosaurBreathWave : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float breathPhase;

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
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 45;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            breathPhase += 0.2f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 加速
            if (Projectile.velocity.Length() < 30f) {
                Projectile.velocity *= 1.03f;
            }

            // 龙息雾气
            for (int i = 0; i < 3; i++) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(30, 30);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Cloud,
                    1 => DustID.WhiteTorch,
                    _ => DustID.Clentaminator_Cyan
                };
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 200, Color.White, 2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(1, 1);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.95f, 1f, 1f) * 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 多层绘制
            for (int layer = 2; layer >= 0; layer--) {
                float layerWidth = 0.4f + layer * 0.15f;
                float layerAlpha = 0.8f - layer * 0.2f;

                Color layerColor = layer switch {
                    0 => new Color(255, 255, 255),
                    1 => new Color(220, 240, 255),
                    _ => new Color(200, 225, 255)
                };
                layerColor *= layerAlpha;
                layerColor.A = 0;

                Main.spriteBatch.Draw(tex, drawPos, null, layerColor, Projectile.rotation, origin,
                    new Vector2(1.5f, layerWidth), SpriteEffects.None, 0f);
            }

            // 头部光球
            if (ACMAsset.LightShot != null) {
                Color orbColor = new Color(255, 255, 255) * 0.6f;
                orbColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos, null, orbColor, 0f,
                    ACMAsset.LightShot.Size() / 2f, 1.5f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 480);

            // 龙息爆发
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8, 8);
                int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 150, Color.White, 2.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            // 消散特效
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Cloud,
                    1 => DustID.WhiteTorch,
                    _ => DustID.Frost
                };
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 180, Color.White, 2f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);
        }
    }
}
