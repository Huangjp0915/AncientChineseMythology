using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers
{
    /// <summary>
    /// 觉醒幽冥龙 - 虚空弹
    /// 终局级追踪弹幕，带有强烈的视觉拖尾和能量效果
    /// </summary>
    public class AwakeningNetherVoidBolt : ModProjectile
    {
        public override string Texture => AwakeningNetherHelper.Path + "VoidCore";

        private float homingStrength = 0.04f;
        private float pulsePhase = 0f;
        private float chargeLevel = 0f;
        private float wobblePhase = 0f;

        // 是否为强化版本
        private bool IsEnhanced => Projectile.ai[0] > 0;
        // 追踪强度等级
        private int TrackingLevel => (int)Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 360;
            Projectile.alpha = 50;
        }

        public override void AI() {
            // 逐渐充能
            chargeLevel = MathHelper.Lerp(chargeLevel, 1f, 0.03f);
            pulsePhase += 0.15f;
            wobblePhase += 0.1f;

            // 旋转效果
            Projectile.rotation += MathF.Sin(wobblePhase) * 0.08f + 0.05f;

            // 追踪玩家
            Player target = FindTarget();
            if (target != null) {
                float trackingMod = 1f + TrackingLevel * 0.3f;
                float currentHomingStrength = homingStrength * trackingMod * chargeLevel;

                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float targetSpeed = IsEnhanced ? 16f : 12f;
                float distance = Vector2.Distance(target.Center, Projectile.Center);

                // 距离越近追踪越强
                if (distance < 400f) {
                    currentHomingStrength *= 1f + (1f - distance / 400f) * 0.5f;
                }

                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * targetSpeed, currentHomingStrength);
            }

            // 侧向飘移增加不可预测性
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            float drift = MathF.Sin(wobblePhase * 1.8f + Projectile.whoAmI * 0.7f) * 2.5f;
            Projectile.position += perpendicular * drift;

            // 粒子效果
            CreateParticleEffects();

            // 发光
            float lightIntensity = 0.5f + chargeLevel * 0.5f;
            Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.AwakeningPurple.ToVector3() * lightIntensity);
        }

        private Player FindTarget() {
            Player closest = null;
            float closestDist = 1000f;
            foreach (var p in Main.player) {
                if (p != null && p.active && !p.dead) {
                    float dist = p.Distance(Projectile.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = p;
                    }
                }
            }
            return closest;
        }

        private void CreateParticleEffects() {
            // 主拖尾粒子
            if (Main.rand.NextBool(2)) {
                Vector2 dustOffset = Main.rand.NextVector2Circular(15, 15);
                var d = Dust.NewDustPerfect(Projectile.Center + dustOffset, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.3f * chargeLevel;
                d.velocity = -Projectile.velocity * 0.15f + dustOffset * 0.08f;
                d.alpha = 80;
            }

            // 能量粒子
            if (Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20, 20), DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 1.0f;
                d.velocity = Main.rand.NextVector2Circular(2, 2);
            }

            // 强化版额外粒子
            if (IsEnhanced && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleCrystalShard);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = -Projectile.velocity * 0.2f;
            }

            // 尾迹粒子
            if (Projectile.oldPos.Length > 5 && Projectile.oldPos[5] != Vector2.Zero) {
                Vector2 tailPos = Projectile.oldPos[5] + Projectile.Size / 2;
                if (Main.rand.NextBool(3)) {
                    var d = Dust.NewDustPerfect(tailPos, DustID.Shadowflame);
                    d.noGravity = true;
                    d.scale = 0.7f;
                    d.velocity = Main.rand.NextVector2Circular(1, 1);
                    d.alpha = 150;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            // 绘制多层拖尾
            DrawMultiLayerTrail(sb);

            // 绘制核心
            DrawCore(sb);

            return false;
        }

        private void DrawMultiLayerTrail(SpriteBatch sb) {
            var tex = BAWImpermanences.BAWHelper.DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;

            // 外层光晕拖尾
            for (int layer = 0; layer < 2; layer++) {
                for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;

                    float progress = 1f - (float)i / Projectile.oldPos.Length;
                    float trailAlpha = progress * 0.5f * chargeLevel;
                    float trailScale = (0.6f + progress * 1.2f) * (layer == 0 ? 1.8f : 1.2f);

                    Color trailColor = layer == 0
                        ? AwakeningNetherHelper.VoidDarkPurple * trailAlpha * 0.3f
                        : AwakeningNetherHelper.AwakeningPurple * trailAlpha * 0.5f;
                    trailColor.A = 0;

                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;

                    // 波动偏移
                    float wobble = MathF.Sin(wobblePhase + i * 0.4f) * 4f;
                    drawPos.Y += wobble;

                    sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
                }
            }

            // 能量线拖尾（连接各点）
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero || Projectile.oldPos[i - 1] == Vector2.Zero) continue;

                Vector2 start = Projectile.oldPos[i - 1] + Projectile.Size / 2;
                Vector2 end = Projectile.oldPos[i] + Projectile.Size / 2;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color lineColor = AwakeningNetherHelper.NetherCyan * progress * 0.3f * chargeLevel;

                AwakeningNetherHelper.DrawEnergyBeam(sb, start, end, lineColor, 8f * progress, pulsePhase);
            }
        }

        private void DrawCore(SpriteBatch sb) {
            float coreScale = (IsEnhanced ? 1.5f : 1.2f) * chargeLevel;

            // 使用高级核心绘制
            AwakeningNetherHelper.DrawVoidCore(sb, Projectile.Center,
                AwakeningNetherHelper.AwakeningPurple,
                AwakeningNetherHelper.NetherCyan,
                coreScale, pulsePhase, IsEnhanced);

            // 强化版额外的能量环
            if (IsEnhanced) {
                var tex = BAWImpermanences.BAWHelper.DustTexture;
                if (tex == null) return;

                for (int i = 0; i < 4; i++) {
                    float angle = pulsePhase * 2f + i * MathHelper.PiOver2;
                    float dist = 25f * chargeLevel;
                    Vector2 ringPos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                    Color ringColor = AwakeningNetherHelper.SoulPink;
                    ringColor.A = 0;

                    sb.Draw(tex, ringPos - Main.screenPosition, null, ringColor * 0.6f,
                        angle, tex.Size() / 2f, 0.6f, SpriteEffects.None, 0);
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            // 虚空侵蚀效果
            target.AddBuff(BuffID.Darkness, 180);
            target.AddBuff(BuffID.Blackout, 120);

            // 命中音效
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = 0.2f, Volume = 1.1f }, Projectile.Center);

            // 命中特效
            AwakeningNetherHelper.CreateSoulBurst(target.Center, 60f, 2, 12);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.4f, Volume = 0.9f }, Projectile.Center);

            // 消散特效
            AwakeningNetherHelper.CreateVoidVortex(Projectile.Center, 50f, 0.5f, 15);

            for (int i = 0; i < 20; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = Main.rand.NextVector2Circular(8, 8);
                d.alpha = 50;
            }
        }
    }

    /// <summary>
    /// 觉醒幽冥龙 - 灵魂弹
    /// 环形扩散的灵魂弹幕
    /// </summary>
    public class AwakeningNetherSoulOrb : ModProjectile
    {
        public override string Texture => AwakeningNetherHelper.Path + "VoidCore";

        private float pulsePhase = 0f;
        private float spiralAngle = 0f;
        private Color orbColor;

        // 扩散模式：0=直线，1=螺旋
        private int SpreadMode => (int)Projectile.ai[0];
        // 颜色索引
        private int ColorIndex => (int)Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 80;
        }

        public override void AI() {
            // 初始化颜色
            if (pulsePhase == 0f) {
                Color[] colors = [
                    AwakeningNetherHelper.AwakeningPurple,
                    AwakeningNetherHelper.NetherCyan,
                    AwakeningNetherHelper.SoulPink
                ];
                orbColor = colors[ColorIndex % colors.Length];
            }

            pulsePhase += 0.12f;
            spiralAngle += 0.05f;

            // 螺旋模式
            if (SpreadMode == 1) {
                Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                float spiral = MathF.Sin(spiralAngle) * 3f;
                Projectile.position += perpendicular * spiral;
            }

            // 旋转
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 加速
            if (Projectile.velocity.Length() < 14f) {
                Projectile.velocity *= 1.015f;
            }

            // 粒子效果
            if (Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10, 10), DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.9f;
                d.velocity = -Projectile.velocity * 0.1f;
                d.color = orbColor;
            }

            // 能量环绕
            if (Main.rand.NextBool(4)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 15f;
                var d = Dust.NewDustPerfect(Projectile.Center + offset, DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 0.6f;
                d.velocity = offset.RotatedBy(MathHelper.PiOver2) * 0.3f;
            }

            Lighting.AddLight(Projectile.Center, orbColor.ToVector3() * 0.4f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = BAWImpermanences.BAWHelper.DustTexture;
            if (tex == null) return false;

            Vector2 origin = tex.Size() / 2f;

            // 拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                float trailAlpha = progress * 0.5f;
                float trailScale = 0.5f + progress * 0.8f;

                Color trailColor = orbColor * trailAlpha;
                trailColor.A = 0;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;

                sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
            }

            // 核心
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.2f;
            AwakeningNetherHelper.DrawVoidCore(sb, Projectile.Center, orbColor,
                Color.Lerp(orbColor, Color.White, 0.3f), pulse, pulsePhase);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Confused, 60);
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.3f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);

            for (int i = 0; i < 15; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = Main.rand.NextVector2Circular(6, 6);
                d.color = orbColor;
            }
        }
    }
}
