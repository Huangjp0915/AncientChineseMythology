using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.AoGuangs.Items
{
    #region 水龙斩波

    /// <summary>
    /// 水龙斩波 - 大刀每三刀释放
    /// </summary>
    public class DragonTidalSlash : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float slashPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 15;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 50;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            slashPhase += 0.12f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.97f;

            // 水龙斩粒子
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 2; i++) {
                    Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(15, 8);
                    int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * 0.6f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 90);

            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(target.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.EastSeaWater, 1.3f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            // 现代化双层 ribbon 拖尾 (外深青 + 内冰蓝, 叠在原斩波之上)
            WeaponVFX.DrawProjectileTrail(Projectile, baseWidth: 26f,
                outerColor: new Color(30, 90, 170, 140), innerColor: new Color(170, 240, 255, 180),
                tex: ACMAsset.GlaciateWave, uvScroll: -Main.GlobalTimeWrappedHourly * 1.4f);

            Texture2D tex = ACMAsset.GlaciateWave ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(0, tex.Height / 2f);

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;

                Color trailColor = Color.Lerp(AoGuangHelper.OceanTeal, AoGuangHelper.DragonBlue, 1f - progress);
                trailColor *= progress * 0.7f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.oldRot[i], origin,
                    new Vector2(1f * progress, 0.2f * progress), SpriteEffects.None, 0f);
            }

            // 主体
            Color mainColor = AoGuangHelper.WaterGlow;
            mainColor.A = 0;
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation,
                origin, new Vector2(1.2f, 0.25f), SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 120, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region 小型水龙卷

    /// <summary>
    /// 小型水龙卷 - 大刀满潮释放，复用BarrierWaterTornado视觉
    /// </summary>
    public class MiniWaterTornado : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float tornadoRotation;
        private float tornadoAlpha = 0f;
        private float tornadoHeight = 0f;
        private const float MaxHeight = 350f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            tornadoRotation += 0.2f;
            tornadoAlpha = MathHelper.Lerp(tornadoAlpha, 1f, 0.04f);
            tornadoHeight = MathHelper.Lerp(tornadoHeight, MaxHeight, 0.05f);

            // 缓慢追踪最近敌人
            NPC target = FindClosestNPC(400f);
            if (target != null) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 3f, 0.02f);
            }
            else {
                Projectile.velocity *= 0.95f;
            }

            // 吸引敌人
            foreach (NPC npc in Main.npc) {
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                float distance = Vector2.Distance(npc.Center, Projectile.Center);
                if (distance < 150f && distance > 30f) {
                    Vector2 pullDir = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    npc.velocity += pullDir * 0.6f;
                }
            }

            // 龙卷粒子
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 4; i++) {
                    float heightOffset = Main.rand.NextFloat(-tornadoHeight / 2, tornadoHeight / 2);
                    float angle = tornadoRotation + Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = 20f + MathF.Abs(heightOffset / tornadoHeight) * 35f;

                    Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(angle) * radius, heightOffset);
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Water,
                        1 => DustID.BlueTorch,
                        _ => DustID.Wet
                    };
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 1.6f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = new Vector2(MathF.Cos(angle + MathHelper.PiOver2) * 4f, Main.rand.NextFloat(-1, 1));
                }
            }

            Lighting.AddLight(Projectile.Center, AoGuangHelper.DragonBlue.ToVector3() * tornadoAlpha * 0.8f);
        }

        private NPC FindClosestNPC(float maxDistance) {
            NPC closest = null;
            float closestDist = maxDistance;

            foreach (var npc in Main.npc) {
                if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.CanBeChasedBy()) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float targetX = targetHitbox.Center.X;
            float distance = MathF.Abs(targetX - Projectile.Center.X);
            float targetY = targetHitbox.Center.Y;
            float heightDiff = MathF.Abs(targetY - Projectile.Center.Y);
            return distance < 40f && heightDiff < tornadoHeight / 2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 120);

            for (int i = 0; i < 8; i++) {
                float angle = tornadoRotation + MathHelper.TwoPi * i / 8;
                Vector2 vel = angle.ToRotationVector2() * 4f;
                int dust = Dust.NewDust(target.Center, 0, 0, DustID.Water, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }

            ACMWeaponBurst.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                ACMWeaponBurst.EastSeaWater, 1.1f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;

            // 龙卷核心东海冰蓝径向辉光 (走全屏名额仲裁, 满则退化柔光)
            if (tornadoAlpha > 0.5f)
                WeaponVFX.DrawRadialBloom(Projectile.Center, 0.12f, tornadoAlpha * 0.5f,
                    new Color(90, 200, 245), 0f);

            Main.instance.LoadProjectile(ProjectileID.SandnadoHostile);
            Texture2D tornadoTex = TextureAssets.Projectile[ProjectileID.SandnadoHostile].Value;
            Vector2 origin = tornadoTex.Size() / 2f;

            // 绘制小型水龙卷
            int segments = 10;
            for (int seg = 0; seg < segments; seg++) {
                float heightPercent = (float)seg / segments;
                float yOffset = (heightPercent - 0.5f) * tornadoHeight;
                float segRadius = 0.4f + MathF.Abs(heightPercent - 0.5f) * 0.5f;
                float segRot = tornadoRotation + seg * 0.35f;

                Vector2 segPos = screenPos + new Vector2(0, yOffset);

                // 外层
                Color outerColor = AoGuangHelper.OceanTeal * tornadoAlpha * 0.4f;
                outerColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, outerColor, segRot, origin, segRadius * 1.2f, SpriteEffects.None, 0f);

                // 中层
                Color midColor = AoGuangHelper.DragonBlue * tornadoAlpha * 0.6f;
                midColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, midColor, segRot * 1.25f, origin, segRadius, SpriteEffects.None, 0f);

                // 内层
                Color innerColor = AoGuangHelper.WaterGlow * tornadoAlpha * 0.35f;
                innerColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, innerColor, segRot * 1.5f, origin, segRadius * 0.65f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item21 with { Pitch = 0f, Volume = 0.8f }, Projectile.Center);

            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20;
                Vector2 vel = angle.ToRotationVector2() * 5f;
                int dustType = Main.rand.NextBool() ? DustID.Water : DustID.BlueTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.2f);
                Main.dust[dust].noGravity = true;
            }

            Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(5, 10);
        }
    }

    #endregion
}
