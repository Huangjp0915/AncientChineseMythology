using Microsoft.Xna.Framework;
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
    /// 祖龙神弓 - 祖龙残魂掉落的远程弓
    /// 一把由祖龙之息凝聚而成的迷幻神弓，发射龙魂箭矢
    /// 特效：发射追踪龙魂箭，蓄力后从天空召唤龙魂箭雨
    /// </summary>
    public class ArchosaurBow : ModItem
    {
        private int chargeCounter = 0;
        private const int MaxCharge = 6;

        public override void SetDefaults() {
            Item.damage = 4200;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 32;
            Item.height = 64;
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.value = Item.sellPrice(platinum: 1, gold: 50);
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item5;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 22f;
            Item.useAmmo = AmmoID.Arrow;
            Item.crit = 15;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            // 转换为龙魂箭
            type = ModContent.ProjectileType<ArchosaurSoulArrow>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            chargeCounter++;

            // 发射主箭 - 三连发散射
            for (int i = -1; i <= 1; i++) {
                float spread = MathHelper.ToRadians(i * 6);
                Vector2 spreadVel = velocity.RotatedBy(spread);
                float dmgMult = i == 0 ? 1f : 0.75f;
                Projectile.NewProjectile(source, position, spreadVel, type, (int)(damage * dmgMult), knockback, player.whoAmI);
            }

            // 蓄力满后召唤龙魂箭雨
            if (chargeCounter >= MaxCharge) {
                chargeCounter = 0;

                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f, Volume = 0.8f }, player.Center);

                // 从天空召唤龙魂箭雨
                Vector2 targetPos = Main.MouseWorld;
                int arrowCount = 8;

                for (int i = 0; i < arrowCount; i++) {
                    Vector2 spawnPos = targetPos + new Vector2(Main.rand.NextFloat(-200, 200), -600 - Main.rand.NextFloat(0, 150));
                    Vector2 arrowVel = (targetPos + Main.rand.NextVector2Circular(50, 50) - spawnPos).SafeNormalize(Vector2.UnitY) * 18f;

                    Projectile.NewProjectile(
                        source,
                        spawnPos,
                        arrowVel,
                        ModContent.ProjectileType<ArchosaurRainArrow>(),
                        (int)(damage * 1.3f),
                        knockback,
                        player.whoAmI
                    );
                }

                // 天空涟漪特效
                for (int i = 0; i < 20; i++) {
                    Vector2 dustPos = targetPos + new Vector2(Main.rand.NextFloat(-150, 150), -500);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Cloud, 0, 3f, 200, Color.White, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            // 发射特效
            for (int i = 0; i < 5; i++) {
                Vector2 dustVel = velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2, 5);
                int dust = Dust.NewDust(position, 0, 0, DustID.WhiteTorch, dustVel.X, dustVel.Y, 150, Color.White, 1.2f);
                Main.dust[dust].noGravity = true;
            }

            return false;
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-4, 0);
        }
    }

    /// <summary>
    /// 龙魂箭 - 主要攻击弹幕
    /// </summary>
    public class ArchosaurSoulArrow : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float arrowPhase;
        private bool isHoming = true;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 3;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;
            Projectile.extraUpdates = 1;
            Projectile.arrow = true;
        }

        public override void AI() {
            arrowPhase += 0.1f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 轻微追踪
            if (isHoming && Projectile.timeLeft > 120) {
                NPC target = FindClosestNPC(600f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float targetAngle = toTarget.ToRotation();
                    float currentAngle = Projectile.velocity.ToRotation();
                    float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.04f);
                    Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            // 龙魂粒子
            if (Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 180, Color.White, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.85f, 0.9f, 1f) * 0.4f);
        }

        private NPC FindClosestNPC(float maxDistance) {
            NPC closest = null;
            float closestDist = maxDistance;

            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.CanBeChasedBy()) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.BlankStar ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(new Color(255, 255, 255), new Color(200, 225, 255), 1f - progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin, 0.4f * progress, SpriteEffects.None, 0f);
            }

            // 主体
            Color mainColor = new Color(255, 255, 255) * 0.8f;
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mainColor, Projectile.rotation, origin, 0.5f, SpriteEffects.None, 0f);

            // 核心光效
            if (ACMAsset.LightShot != null) {
                Color coreColor = new Color(220, 240, 255) * 0.5f;
                coreColor.A = 0;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos, null, coreColor, 0f,
                    ACMAsset.LightShot.Size() / 2f, 0.3f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            isHoming = false;
            target.AddBuff(BuffID.Frostburn2, 180);

            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.WhiteTorch, vel.X, vel.Y, 150, Color.White, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3, 3);
                int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 180, Color.White, 1.2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    /// <summary>
    /// 龙魂箭雨 - 从天空降下的箭矢
    /// </summary>
    public class ArchosaurRainArrow : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

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
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 加速下落
            if (Projectile.velocity.Length() < 25f) {
                Projectile.velocity *= 1.02f;
            }

            // 龙魂拖尾
            for (int i = 0; i < 2; i++) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(8, 8);
                int dustType = Main.rand.Next(3) switch {
                    0 => DustID.Cloud,
                    1 => DustID.WhiteTorch,
                    _ => DustID.Clentaminator_Cyan
                };
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 200, Color.White, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.9f, 0.95f, 1f) * 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, 0);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 长拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(220, 240, 255) * progress * 0.4f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i] - MathHelper.PiOver2, origin,
                    new Vector2(0.1f * progress, 0.8f * progress), SpriteEffects.None, 0f);
            }

            // 主体
            Color mainColor = new Color(255, 255, 255) * 0.9f;
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mainColor, Projectile.rotation - MathHelper.PiOver2, origin,
                new Vector2(0.12f, 1f), SpriteEffects.None, 0f);

            // 箭头光点
            if (ACMAsset.LightShot != null) {
                Color tipColor = new Color(255, 255, 255) * 0.7f;
                tipColor.A = 0;
                Vector2 tipOffset = Projectile.velocity.SafeNormalize(Vector2.Zero) * 15f;
                Main.spriteBatch.Draw(ACMAsset.LightShot, drawPos + tipOffset, null, tipColor, 0f,
                    ACMAsset.LightShot.Size() / 2f, 0.4f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 240);

            // 落地爆发
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 150, Color.White, 2f);
                Main.dust[dust].noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.3f, Volume = 0.4f }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            // 落地特效
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15;
                Vector2 vel = angle.ToRotationVector2() * 4f;
                int dustType = Main.rand.NextBool() ? DustID.Cloud : DustID.WhiteTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 180, Color.White, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
