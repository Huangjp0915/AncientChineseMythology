using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers
{
    /// <summary>
    /// 觉醒幽冥龙 - 虚空吐息
    /// 扇形扩散的毁灭性吐息攻击，带有强烈的视觉冲击
    /// </summary>
    public class AwakeningNetherBreath : ModProjectile
    {
        public override string Texture => AwakeningNetherHelper.Path + "VoidCore";

        private float growthPhase = 0f;
        private float pulsePhase = 0f;
        private float intensity = 0f;
        private float waveWidth = 1f;

        // 是否为狂暴版本
        private bool IsEnraged => Projectile.ai[0] > 0;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 25;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.alpha = 60;
        }

        public override void AI() {
            growthPhase += 0.02f;
            pulsePhase += 0.18f;
            intensity = MathHelper.Lerp(intensity, 1f, 0.05f);

            // 波形宽度扩展
            waveWidth = MathHelper.Lerp(waveWidth, IsEnraged ? 3.5f : 2.8f, 0.015f);

            // 旋转朝向运动方向
            Projectile.rotation = Projectile.velocity.ToRotation();

            // 波浪运动
            float waveOffset = MathF.Sin(pulsePhase * 0.6f) * 5f * intensity;
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            Projectile.position += perpendicular * waveOffset;

            // 加速
            if (Projectile.velocity.Length() < (IsEnraged ? 20f : 16f)) {
                Projectile.velocity *= 1.018f;
            }

            // 粒子效果
            CreateBreathParticles();

            // 发光
            float lightMod = intensity * (IsEnraged ? 1.3f : 1f);
            Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.AwakeningPurple.ToVector3() * 0.6f * lightMod);
        }

        private void CreateBreathParticles() {
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);

            // 多层吐息粒子
            for (int layer = -2; layer <= 2; layer++) {
                float layerOffset = layer * 18f * waveWidth;
                Vector2 dustPos = Projectile.Center + perpendicular * layerOffset;

                if (Main.rand.NextBool(2)) {
                    dustPos += Main.rand.NextVector2Circular(12, 12);
                    int dustType = Main.rand.NextBool() ? DustID.Shadowflame : DustID.PurpleTorch;
                    var d = Dust.NewDustPerfect(dustPos, dustType);
                    d.noGravity = true;
                    d.scale = 1.3f * intensity * (1f - MathF.Abs(layer) / 3f);
                    d.velocity = -Projectile.velocity * 0.12f + perpendicular * layer * 0.5f;
                    d.alpha = 60;
                }
            }

            // 能量涡流
            if (Main.rand.NextBool(3)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = 30f * waveWidth;
                Vector2 vortexPos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                var d = Dust.NewDustPerfect(vortexPos, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.8f;
                d.velocity = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 3f;
            }

            // 狂暴版额外效果
            if (IsEnraged && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(25, 25), DustID.ShadowbeamStaff);
                d.noGravity = true;
                d.scale = 1.0f;
                d.velocity = Main.rand.NextVector2Circular(2, 2);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            // 绘制多层波形拖尾
            DrawWaveTrail(sb);

            // 绘制核心
            DrawBreathCore(sb);

            return false;
        }

        private void DrawWaveTrail(SpriteBatch sb) {
            var tex = BAWImpermanences.BAWHelper.DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);

            // 多层拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                float trailWidth = waveWidth * progress;

                // 波形效果
                for (int layer = -3; layer <= 3; layer++) {
                    float layerOffset = layer * 15f * trailWidth;
                    float layerAlpha = (1f - MathF.Abs(layer) / 4f) * progress * 0.4f * intensity;

                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 + perpendicular * layerOffset - Main.screenPosition;

                    // 波浪形变
                    float waveY = MathF.Sin(pulsePhase + i * 0.25f + layer * 0.6f) * 4f;
                    drawPos.Y += waveY;

                    // 颜色渐变
                    Color trailColor = Color.Lerp(AwakeningNetherHelper.VoidDarkPurple,
                        AwakeningNetherHelper.AwakeningPurple, progress);
                    if (IsEnraged) {
                        trailColor = Color.Lerp(trailColor, AwakeningNetherHelper.DestructionRed, 0.3f);
                    }
                    trailColor *= layerAlpha;
                    trailColor.A = 0;

                    Vector2 trailScale = new Vector2(1.2f + MathF.Abs(layer) * 0.15f, 0.9f * progress);
                    sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
                }
            }

            // 能量连接线
            for (int i = 1; i < Projectile.oldPos.Length; i += 2) {
                if (Projectile.oldPos[i] == Vector2.Zero || Projectile.oldPos[i - 1] == Vector2.Zero) continue;

                Vector2 start = Projectile.oldPos[i - 1] + Projectile.Size / 2;
                Vector2 end = Projectile.oldPos[i] + Projectile.Size / 2;

                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color lineColor = AwakeningNetherHelper.NetherCyan * progress * 0.25f * intensity;

                AwakeningNetherHelper.DrawEnergyBeam(sb, start, end, lineColor, 6f * progress * waveWidth, pulsePhase);
            }
        }

        private void DrawBreathCore(SpriteBatch sb) {
            var tex = BAWImpermanences.BAWHelper.DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            float pulse = 1f + MathF.Sin(pulsePhase) * 0.2f;

            // 多点核心（模拟吐息宽度）
            for (int point = -2; point <= 2; point++) {
                float pointOffset = point * 20f * waveWidth;
                Vector2 corePos = Projectile.Center + perpendicular * pointOffset;

                float pointScale = (1f - MathF.Abs(point) / 3f) * pulse * intensity;
                Color coreColor = IsEnraged
                    ? Color.Lerp(AwakeningNetherHelper.AwakeningPurple, AwakeningNetherHelper.DestructionRed, 0.4f)
                    : AwakeningNetherHelper.AwakeningPurple;

                // 外层光晕
                Color glowColor = coreColor;
                glowColor.A = 0;
                for (int g = 3; g >= 0; g--) {
                    float glowScale = pointScale * (1.5f + g * 0.4f);
                    sb.Draw(tex, corePos - Main.screenPosition, null, glowColor * (0.15f / (g + 1)),
                        Projectile.rotation, origin, glowScale, SpriteEffects.None, 0);
                }

                // 核心
                sb.Draw(tex, corePos - Main.screenPosition, null, coreColor * 0.8f,
                    Projectile.rotation, origin, pointScale, SpriteEffects.None, 0);
            }

            // 前端能量聚集
            Color frontColor = Color.White;
            frontColor.A = 0;
            Vector2 frontPos = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;
            sb.Draw(tex, frontPos - Main.screenPosition, null, frontColor * 0.5f * intensity,
                Projectile.rotation, origin, pulse * 0.8f, SpriteEffects.None, 0);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            // 虚空灼烧 + 魂蚀
            target.GetModPlayer<AwakeningNetherPlayer>().AddSoulErosion(IsEnraged ? 4 : 3);
            target.AddBuff(BuffID.ShadowFlame, 240);

            SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f, Volume = 0.8f }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.2f }, Projectile.Center);

            // 扩散消散
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < 25; i++) {
                float offset = (i - 12) * 10f;
                var d = Dust.NewDustPerfect(Projectile.Center + perpendicular * offset, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = Main.rand.NextVector2Circular(5, 5);
            }
        }
    }

    /// <summary>
    /// 觉醒幽冥龙 - 次元裂隙之门 (Dimension Rift Gate)
    /// 第二幕「次元裂隙」的核心机制：成对生成的传送门。
    /// 有清晰的预告开启动画，只有「门口」小范围造成伤害，不再随机拉扯/喷弹。
    /// 觉醒冥龙会冲入其中一扇门、从另一扇门冲出；玩家自行选择跟随哪扇门 (近道 vs 安全)。
    /// </summary>
    public class AwakeningNetherRift : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ShadowOrb;

        public const int OpenTime = 45;   // 预告开启
        public const int CloseTime = 50;  // 关闭动画

        private float growthPhase = 0f;
        private float pulsePhase = 0f;
        private float riftScale = 0f;
        private bool isClosing = false;

        // ai[0] = 门的大小等级
        private int SizeLevel => (int)Projectile.ai[0];
        private float Timer => Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 260;
            Projectile.alpha = 0;
        }

        public override void AI() {
            growthPhase += 0.02f;
            pulsePhase += 0.1f;
            Projectile.localAI[0]++;

            // 开门 / 关门动画
            float targetScale = 1f + SizeLevel * 0.3f;
            if (Projectile.timeLeft < CloseTime) {
                isClosing = true;
                targetScale = 0f;
            }
            // 开门阶段平滑放大
            float openLerp = isClosing ? 0.1f : (Timer < OpenTime ? 0.12f : 0.05f);
            riftScale = MathHelper.Lerp(riftScale, targetScale, openLerp);

            if (isClosing && riftScale < 0.1f) {
                Projectile.Kill();
                return;
            }

            Projectile.rotation += 0.02f * riftScale;

            if (riftScale > 0.4f)
                CreateRiftEffects();

            Lighting.AddLight(Projectile.Center, AwakeningNetherHelper.VoidDarkPurple.ToVector3() * 0.8f * riftScale);
        }

        // 只有完全开启后、门口才会判定伤害 (可读的安全区设计)
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Timer < OpenTime || isClosing)
                return false;
            float mouth = 60f * riftScale;
            return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2()) < mouth;
        }

        private void CreateRiftEffects() {
            if (Main.netMode == NetmodeID.Server)
                return;

            // 吸入漩涡
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = 100f * riftScale + Main.rand.NextFloat(50f);
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

                Vector2 toCenter = (Projectile.Center - pos).SafeNormalize(Vector2.Zero);

                var d = Dust.NewDustPerfect(pos, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = toCenter * 6f + new Vector2(-toCenter.Y, toCenter.X) * 3f;
                d.alpha = 80;
            }

            // 边缘能量
            if (Main.rand.NextBool(3)) {
                float edgeAngle = pulsePhase + Main.rand.NextFloat(MathHelper.TwoPi);
                float edgeDist = 50f * riftScale;
                Vector2 edgePos = Projectile.Center + new Vector2(MathF.Cos(edgeAngle), MathF.Sin(edgeAngle) * 0.4f) * edgeDist;

                var d = Dust.NewDustPerfect(edgePos, DustID.PurpleTorch);
                d.noGravity = true;
                d.scale = 1.0f;
                d.velocity = Main.rand.NextVector2Circular(2, 2);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<AwakeningNetherPlayer>().AddSoulErosion(3);
            SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.2f }, Projectile.Center);
            AwakeningNetherHelper.CreateDimensionTear(Projectile.Center, target.Center, 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            // 绘制次元裂隙
            AwakeningNetherHelper.DrawDimensionRift(sb, Projectile.Center, riftScale, Projectile.rotation,
                pulsePhase, isClosing);

            // 外层能量环
            if (riftScale > 0.3f) {
                DrawOuterRings(sb);
            }

            return false;
        }

        private void DrawOuterRings(SpriteBatch sb) {
            var tex = BAWImpermanences.BAWHelper.DustTexture;
            if (tex == null) return;

            Vector2 origin = tex.Size() / 2f;

            // 旋转的能量环
            int ringCount = 2;
            for (int ring = 0; ring < ringCount; ring++) {
                float ringRadius = (60f + ring * 30f) * riftScale;
                float ringRotation = pulsePhase * (ring % 2 == 0 ? 1 : -1) * 0.8f;
                int segments = 16 - ring * 4;

                for (int i = 0; i < segments; i++) {
                    float angle = ringRotation + MathHelper.TwoPi * i / segments;
                    Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ringRadius;

                    float segPulse = MathF.Sin(pulsePhase + angle * 2) * 0.3f + 0.7f;
                    Color segColor = Color.Lerp(AwakeningNetherHelper.VoidDarkPurple,
                        AwakeningNetherHelper.AwakeningPurple, segPulse);
                    segColor.A = 0;
                    segColor *= (0.5f - ring * 0.15f) * riftScale;

                    sb.Draw(tex, pos - Main.screenPosition, null, segColor,
                        angle + MathHelper.PiOver4, origin, 0.8f * segPulse, SpriteEffects.None, 0);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f, Volume = 1.2f }, Projectile.Center);

            // 裂隙崩溃爆炸
            AwakeningNetherHelper.CreateVoidVortex(Projectile.Center, 80f, 1f, 40);
            AwakeningNetherHelper.CreateSoulBurst(Projectile.Center, 100f, 3, 20);

            // 屏幕闪烁
            AwakeningNetherHelper.CreateScreenFlash(Projectile.Center, AwakeningNetherHelper.AwakeningPurple, 0.5f);
        }
    }
}
