using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    #region 雷球

    /// <summary>
    /// 敖顺雷球 - 带微弱追踪的雷电弹幕
    /// </summary>
    public class AoshunThunderball : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float thunderPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
            Projectile.alpha = 0;
        }

        public override void AI() {
            thunderPhase += 0.12f;

            // 微弱追踪
            if (Projectile.timeLeft > 220) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float targetAngle = toTarget.ToRotation();
                    float currentAngle = Projectile.velocity.ToRotation();
                    float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.015f);
                    Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            // 雷电粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 180, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.5f, 0.5f);
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(thunderPhase * 2f) * 0.2f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoshunHelper.LightningBlue, AoshunHelper.ThunderPurple, 1f - progress);
                trailColor *= progress * 0.4f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, 0.5f * progress * pulse, SpriteEffects.None, 0f);
            }

            // 外层光晕
            Color outerColor = AoshunHelper.ThunderPurple * 0.35f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, 0f, origin, 0.9f * pulse, SpriteEffects.None, 0f);

            // 中层
            Color midColor = AoshunHelper.LightningBlue * 0.5f * pulse;
            midColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, midColor, 0f, origin, 0.55f * pulse, SpriteEffects.None, 0f);

            // 核心
            Color coreColor = AoshunHelper.ElectricWhite * 0.8f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, 0f, origin, 0.3f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 150, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region 雷柱

    /// <summary>
    /// 敖顺雷柱 - 从空中下落的雷电柱
    /// </summary>
    public class AoshunLightningBolt : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float thunderPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            thunderPhase += 0.1f;
            Projectile.rotation += 0.1f;

            // 加速下落
            if (Projectile.velocity.Y < 18f)
                Projectile.velocity.Y += 0.3f;

            // 雷电尾迹
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 2; i++) {
                    Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(10, 10);
                    int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, -2, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(thunderPhase * 3f) * 0.15f;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoshunHelper.ElectricWhite, AoshunHelper.ThunderPurple, 1f - progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;

                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, (0.6f + progress * 0.4f) * pulse, SpriteEffects.None, 0f);
            }

            // 外层光晕
            Color outerColor = AoshunHelper.ThunderPurple * 0.4f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, 0f, origin, 1.4f * pulse, SpriteEffects.None, 0f);

            // 中层
            Color midColor = AoshunHelper.LightningBlue * 0.6f * pulse;
            midColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, midColor, 0f, origin, 0.9f * pulse, SpriteEffects.None, 0f);

            // 核心
            Color coreColor = AoshunHelper.ElectricWhite * 0.9f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, 0f, origin, 0.5f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            // 落地雷电爆发
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }

            // 电弧飞溅
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3, 6);
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric);
                d.noGravity = false;
                d.scale = 1.5f;
                d.velocity = vel;
            }
        }
    }

    #endregion

    #region 雷电旋涡

    /// <summary>
    /// 敖顺雷电旋涡 - 停留在原地的旋转雷电区域
    /// </summary>
    public class AoshunThunderVortex : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float vortexAngle;
        private float vortexAlpha;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            vortexAngle += 0.15f;
            vortexAlpha = MathHelper.Lerp(vortexAlpha, 1f, 0.05f);

            Projectile.velocity *= 0.95f;

            // 旋转雷电粒子
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 4; i++) {
                    float angle = vortexAngle + MathHelper.TwoPi * i / 4;
                    float radius = 60f + MathF.Sin(vortexAngle * 2f + i) * 20f;
                    Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * radius;
                    int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 5f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 0.6f * vortexAlpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            float distance = Vector2.Distance(Projectile.Center, targetCenter);
            return distance < 70f;
        }

        public override bool PreDraw(ref Color lightColor) {
            AoshunHelper.DrawThunderAura(Main.spriteBatch, Projectile.Center, 70f, vortexAngle, vortexAlpha);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20;
                Vector2 vel = angle.ToRotationVector2() * 5f;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Electric, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region 雷柱激光

    /// <summary>
    /// 敖顺雷柱激光 - 从Boss口中射出的柱状雷束大招
    /// 蓄力后释放持续雷束，缓慢追踪玩家方向扫射
    /// ai[0]: 持续时间计数, ai[1]: 目标角度
    /// </summary>
    public class AoshunThunderBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float BeamLength = 1800f;
        private const float BeamWidth = 40f;
        private const int ChargeTime = 60;
        private const int BeamDuration = 180;

        private float beamAlpha;
        private float beamPhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = ChargeTime + BeamDuration;
        }

        public override void AI() {
            beamPhase += 0.1f;
            int timer = (ChargeTime + BeamDuration) - Projectile.timeLeft;

            // 找到头部NPC保持位置
            bool foundOwner = false;
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<Aoshun>()) {
                    Projectile.Center = Main.npc[i].Center;
                    foundOwner = true;
                    break;
                }
            }
            if (!foundOwner) {
                Projectile.Kill();
                return;
            }

            if (timer < ChargeTime) {
                // 蓄力阶段 - 雷电汇聚粒子
                beamAlpha = (float)timer / ChargeTime * 0.5f;

                // 缓慢追踪玩家
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    float targetAngle = (target.Center - Projectile.Center).ToRotation();
                    Projectile.ai[1] = MathHelper.Lerp(Projectile.ai[1], targetAngle, 0.08f);
                }

                if (Main.netMode != NetmodeID.Server && timer % 3 == 0) {
                    Vector2 dir = Projectile.ai[1].ToRotationVector2();
                    for (int i = 0; i < 6; i++) {
                        float dist = Main.rand.NextFloat(100, 300);
                        Vector2 offset = dir.RotatedByRandom(1.2f) * dist;
                        Vector2 dustPos = Projectile.Center + offset;
                        Vector2 dustVel = (Projectile.Center - dustPos).SafeNormalize(Vector2.Zero) * (6f + dist * 0.02f);
                        int d = Dust.NewDust(dustPos, 0, 0, DustID.Electric, dustVel.X, dustVel.Y, 150, default, 2f);
                        Main.dust[d].noGravity = true;
                    }
                }

                // 蓄力音效
                if (timer == 10) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.5f, Volume = 1.5f }, Projectile.Center);
                }
            }
            else {
                // 激光阶段
                int beamTimer = timer - ChargeTime;
                float fadeIn = Math.Min(beamTimer / 15f, 1f);
                float fadeOut = Math.Min((BeamDuration - beamTimer) / 20f, 1f);
                beamAlpha = fadeIn * fadeOut;

                // 缓慢扫射追踪
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    float targetAngle = (target.Center - Projectile.Center).ToRotation();
                    Projectile.ai[1] = MathHelper.Lerp(Projectile.ai[1], targetAngle, 0.012f);
                }

                // 激光沿线粒子
                if (Main.netMode != NetmodeID.Server) {
                    Vector2 dir = Projectile.ai[1].ToRotationVector2();
                    for (int i = 0; i < 8; i++) {
                        float dist = Main.rand.NextFloat(0, BeamLength);
                        Vector2 dustPos = Projectile.Center + dir * dist;
                        dustPos += dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-BeamWidth * 0.5f, BeamWidth * 0.5f);
                        int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                        int d = Dust.NewDust(dustPos, 0, 0, dustType, 0, -1, 100, default, 2.5f);
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity *= 0.3f;
                    }

                    // 起点爆花
                    for (int i = 0; i < 3; i++) {
                        Vector2 dustVel = dir.RotatedByRandom(0.8f) * Main.rand.NextFloat(3, 8);
                        int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.Electric, dustVel.X, dustVel.Y, 100, default, 3f);
                        Main.dust[d].noGravity = true;
                    }
                }

                // 激光音效
                if (beamTimer == 0) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.3f, Volume = 1.5f }, Projectile.Center);
                }

                Lighting.AddLight(Projectile.Center, AoshunHelper.LightningBlue.ToVector3() * 2f * beamAlpha);
            }

            Projectile.rotation = Projectile.ai[1];
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            int timer = (ChargeTime + BeamDuration) - Projectile.timeLeft;
            if (timer < ChargeTime) return false;

            Vector2 dir = Projectile.ai[1].ToRotationVector2();
            Vector2 start = Projectile.Center;
            Vector2 end = start + dir * BeamLength;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, BeamWidth * 0.6f, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (beamAlpha <= 0.01f) return false;

            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 dir = Projectile.ai[1].ToRotationVector2();
            float rot = Projectile.ai[1] + MathHelper.PiOver2;

            int timer = (ChargeTime + BeamDuration) - Projectile.timeLeft;
            bool isCharging = timer < ChargeTime;

            if (isCharging) {
                // 蓄力光球
                float chargeProgress = (float)timer / ChargeTime;
                float pulse = 1f + MathF.Sin(beamPhase * 4f) * 0.3f;

                Color chargeColor = AoshunHelper.LightningBlue * chargeProgress * 0.6f * pulse;
                chargeColor.A = 0;
                Vector2 drawPos = Projectile.Center - Main.screenPosition;
                Main.spriteBatch.Draw(tex, drawPos, null, chargeColor, 0f, origin, 2f * chargeProgress * pulse, SpriteEffects.None, 0f);

                Color innerColor = AoshunHelper.ElectricWhite * chargeProgress * 0.4f;
                innerColor.A = 0;
                Main.spriteBatch.Draw(tex, drawPos, null, innerColor, 0f, origin, 1f * chargeProgress, SpriteEffects.None, 0f);
            }
            else {
                // 激光柱绘制 - 沿射线方向铺设多层光点
                float pulse = 1f + MathF.Sin(beamPhase * 3f) * 0.15f;
                float segmentStep = 30f;
                int segments = (int)(BeamLength / segmentStep);

                for (int layer = 2; layer >= 0; layer--) {
                    float layerScale;
                    Color layerColor;
                    switch (layer) {
                        case 2:
                            layerScale = 2.8f * pulse;
                            layerColor = AoshunHelper.ThunderPurple * 0.2f * beamAlpha;
                            break;
                        case 1:
                            layerScale = 1.8f * pulse;
                            layerColor = AoshunHelper.LightningBlue * 0.5f * beamAlpha;
                            break;
                        default:
                            layerScale = 0.9f;
                            layerColor = AoshunHelper.ElectricWhite * 0.8f * beamAlpha;
                            break;
                    }
                    layerColor.A = 0;

                    for (int s = 0; s < segments; s++) {
                        Vector2 segPos = Projectile.Center + dir * (s * segmentStep) - Main.screenPosition;
                        float wave = MathF.Sin(beamPhase * 2f + s * 0.3f + layer) * 3f;
                        segPos += dir.RotatedBy(MathHelper.PiOver2) * wave;
                        Main.spriteBatch.Draw(tex, segPos, null, layerColor, rot, origin, layerScale, SpriteEffects.None, 0f);
                    }
                }

                // 起点高亮光球
                Vector2 startDraw = Projectile.Center - Main.screenPosition;
                Color startGlow = AoshunHelper.ElectricWhite * beamAlpha;
                startGlow.A = 0;
                Main.spriteBatch.Draw(tex, startDraw, null, startGlow, 0f, origin, 3f * pulse, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            Vector2 dir = Projectile.ai[1].ToRotationVector2();
            for (int i = 0; i < 40; i++) {
                float dist = Main.rand.NextFloat(0, 400);
                Vector2 dustPos = Projectile.Center + dir * dist + Main.rand.NextVector2Circular(30, 30);
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.PurpleTorch;
                int d = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 100, default, 2.5f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = Main.rand.NextVector2Circular(5, 5);
            }
        }
    }

    #endregion
}
