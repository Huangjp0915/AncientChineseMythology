using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes.Items
{
    /// <summary>
    /// 天柱守卫戟 - 天柱敌怪掉落的长枪类近战武器
    /// 金色+青色主题，突刺并释放天柱冲击
    /// </summary>
    public class PillarGuardiansHalberd : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 225;
            Item.DamageType = DamageClass.Melee;
            Item.width = 60;
            Item.height = 60;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6.5f;
            Item.value = Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<HalberdThrust>();
            Item.shootSpeed = 16f;
            Item.crit = 12;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.Center, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(Mod, "HeavenLore", "天柱守卫所持的神圣长戟"));
            tooltips.Add(new TooltipLine(Mod, "HeavenEffect", "突刺后释放天柱冲击波"));
        }
    }

    /// <summary>
    /// 长戟突刺弹幕 - 手持弹幕
    /// </summary>
    public class HalberdThrust : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Celestias/PillarofTheHeavenes/Items/PillarGuardiansHalberd";

        private float thrustProgress = 0f;
        private float maxExtend = 130f;
        private bool hasReleasedWave = false;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            Owner.itemAnimation = 2;
            Owner.itemTime = 2;
            Owner.heldProj = Projectile.whoAmI;

            // 突刺动画
            thrustProgress += 0.1f;
            float extend;
            if (thrustProgress < 0.5f) {
                extend = ACMUtils.QuadOut(thrustProgress * 2f) * maxExtend;
            }
            else {
                extend = (1f - ACMUtils.QuadIn((thrustProgress - 0.5f) * 2f)) * maxExtend;
            }

            if (thrustProgress >= 1f) {
                Projectile.Kill();
                return;
            }

            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.rotation = direction.ToRotation() + MathHelper.PiOver4;
            Projectile.Center = Owner.MountedCenter + direction * (-60f + extend);

            Owner.direction = direction.X >= 0 ? 1 : -1;
            float armRotation = direction.ToRotation() - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);

            // 突刺最远点时释放冲击波
            if (thrustProgress >= 0.45f && thrustProgress < 0.55f && !hasReleasedWave) {
                hasReleasedWave = true;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, direction * 14f,
                    ModContent.ProjectileType<HalberdShockwave>(), Projectile.damage, Projectile.knockBack, Projectile.owner);

                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.1f, Volume = 0.8f }, Projectile.Center);

                // 爆发粒子
                for (int i = 0; i < 15; i++) {
                    Vector2 dustVel = direction.RotatedByRandom(0.4f) * Main.rand.NextFloat(5, 10);
                    int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                    int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, dustVel.X, dustVel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            // 金色粒子
            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = Projectile.Center + direction * 20f;
                int dustType = Main.rand.NextBool() ? DustID.GoldCoin : DustID.IceTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 1.6f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = direction * 2f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.4f) * 0.6f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2;
            SpriteEffects effects = Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float drawRot = Projectile.rotation + (Owner.direction > 0 ? 0 : MathHelper.PiOver2);
            // 发光
            Color glowColor = Color.Gold * 0.4f;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, glowColor, drawRot, origin, Projectile.scale * 1.2f, effects, 0f);

            // 主体
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor, drawRot, origin, Projectile.scale, effects, 0f);

            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Owner.MountedCenter;
            Vector2 end = Projectile.Center + Projectile.rotation.ToRotationVector2() * 50f;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 30f, ref collisionPoint);
        }
    }

    /// <summary>
    /// 长戟冲击波
    /// </summary>
    public class HalberdShockwave : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DD2PhoenixBowShot;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 40;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.alpha = 80;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.96f;

            // 金色粒子
            for (int i = 0; i < 2; i++) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(15, 8);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 1.6f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.5f) * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.GoldCoin;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = tex.GetRectangle(2, 9);
            Vector2 origin = rectangle.Size() / 2f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                Color trailColor = Color.Lerp(new Color(100, 200, 180), Color.Gold, progress);
                trailColor *= progress * 0.6f;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, rectangle, trailColor, Projectile.oldRot[i], origin, Projectile.scale * progress, SpriteEffects.None, 0f);
            }

            Color glowColor = Color.Gold * 0.5f;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, rectangle, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 80, default, 1.6f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
