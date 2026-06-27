using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors
{
    /// <summary>
    /// 阴天子 - 柱状激光
    /// 由MagicPixel反复叠加绘制的巨大柱状光束
    /// 多层渐变+粒子营造帝冥威压感
    /// ai[0] = 激光方向角度
    /// ai[1] = 持续时间
    /// </summary>
    public class YinEmperorLaser : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float LaserDirection => ref Projectile.ai[0];
        private ref float Duration => ref Projectile.ai[1];

        private float currentLength;
        private float currentWidth;
        private float laserTimer;
        private float pulsePhase;

        private const float TargetLength = 2000f;
        private const float MaxBeamWidth = 50f;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 0;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            laserTimer++;
            pulsePhase += 0.2f;

            float duration = Duration > 0 ? Duration : 60f;
            float expandTime = 8f;
            float fadeTime = 15f;

            // 展开
            if (laserTimer <= expandTime) {
                float t = laserTimer / expandTime;
                currentLength = MathHelper.Lerp(0f, TargetLength, ACMUtils.QuadOut(t));
                currentWidth = MathHelper.Lerp(0f, MaxBeamWidth, ACMUtils.ElasticOut(t));
            }
            // 持续
            else if (laserTimer <= duration - fadeTime) {
                currentLength = TargetLength;
                // 脉动宽度
                currentWidth = MaxBeamWidth * (1f + MathF.Sin(pulsePhase) * 0.08f);
            }
            // 消退
            else if (laserTimer <= duration) {
                float fadeT = (laserTimer - (duration - fadeTime)) / fadeTime;
                currentWidth = MaxBeamWidth * (1f - ACMUtils.QuadIn(fadeT));
                currentLength = MathHelper.Lerp(TargetLength, TargetLength * 0.3f, fadeT);
            }
            else {
                Projectile.Kill();
                return;
            }

            // 粒子效果
            if (Main.netMode != NetmodeID.Server) {
                CreateLaserParticles();
            }

            // 光照
            Vector2 dir = LaserDirection.ToRotationVector2();
            for (int i = 0; i < 5; i++) {
                float progress = i / 5f;
                Vector2 lightPos = Projectile.Center + dir * currentLength * progress;
                Lighting.AddLight(lightPos, YinEmperorHelper.ImperialGold.ToVector3() * 0.6f * (currentWidth / MaxBeamWidth));
            }

            // 起始音效
            if (laserTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = -0.6f, Volume = 1.2f }, Projectile.Center);
            }
        }

        private void CreateLaserParticles() {
            Vector2 dir = LaserDirection.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float widthRatio = currentWidth / MaxBeamWidth;

            // 沿光束散布粒子
            if (Main.rand.NextBool(2) && currentWidth > 5f) {
                float dist = Main.rand.NextFloat(0.1f, 1f) * currentLength;
                float offset = Main.rand.NextFloat(-1f, 1f) * currentWidth * 0.6f;
                Vector2 dustPos = Projectile.Center + dir * dist + perp * offset;

                int dustType = Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.Shadowflame;
                var d = Dust.NewDustPerfect(dustPos, dustType);
                d.noGravity = true;
                d.scale = 1.2f * widthRatio;
                d.velocity = perp * Main.rand.NextFloat(-2f, 2f) + dir * Main.rand.NextFloat(-1f, 1f);
                d.alpha = 80;
            }

            // 起点爆裂粒子
            if (Main.rand.NextBool(2) && currentWidth > 10f) {
                Vector2 startPos = Projectile.Center + Main.rand.NextVector2Circular(currentWidth * 0.3f, currentWidth * 0.3f);
                var d = Dust.NewDustPerfect(startPos, DustID.GoldFlame);
                d.noGravity = true;
                d.scale = 1.5f * widthRatio;
                d.velocity = dir * Main.rand.NextFloat(2f, 6f);
            }

            // 末端消散粒子
            if (Main.rand.NextBool(3) && currentWidth > 10f) {
                Vector2 endPos = Projectile.Center + dir * currentLength + Main.rand.NextVector2Circular(currentWidth, currentWidth);
                var d = Dust.NewDustPerfect(endPos, DustID.Shadowflame);
                d.noGravity = true;
                d.scale = 1f * widthRatio;
                d.velocity = Main.rand.NextVector2Circular(3, 3);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<YinJudgmentPlayer>().AddDecreeStack();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (currentWidth < 5f) return false;

            Vector2 start = Projectile.Center;
            Vector2 end = start + LaserDirection.ToRotationVector2() * currentLength;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, currentWidth, ref point);
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) {
            if (currentWidth < 1f) return false;

            SpriteBatch sb = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            Vector2 screenStart = Projectile.Center - Main.screenPosition;
            float rotation = LaserDirection;
            float widthRatio = currentWidth / MaxBeamWidth;

            // === 第1层：最外层暗紫色晕染 ===
            DrawBeamLayer(sb, pixel, screenStart, rotation, currentLength,
                currentWidth * 3f, YinEmperorHelper.AbyssPurple, 0.08f * widthRatio, pulsePhase * 0.7f);

            // === 第2层：外层帝冥金晕 ===
            DrawBeamLayer(sb, pixel, screenStart, rotation, currentLength,
                currentWidth * 2.2f, YinEmperorHelper.ImperialGold, 0.12f * widthRatio, pulsePhase * 0.9f);

            // === 第3层：中层紫色能量 ===
            DrawBeamLayer(sb, pixel, screenStart, rotation, currentLength,
                currentWidth * 1.5f, YinEmperorHelper.AbyssPurple, 0.2f * widthRatio, pulsePhase);

            // === 第4层：核心金色光柱 ===
            DrawBeamLayer(sb, pixel, screenStart, rotation, currentLength,
                currentWidth * 1f, YinEmperorHelper.DragonVeinGold, 0.35f * widthRatio, pulsePhase * 1.2f);

            // === 第5层：最内层高亮白核 ===
            DrawBeamLayer(sb, pixel, screenStart, rotation, currentLength,
                currentWidth * 0.5f, Color.White, 0.4f * widthRatio, pulsePhase * 1.5f);

            // === 起始爆点 ===
            DrawBeamOrigin(sb, pixel, screenStart, widthRatio);

            // === 末端扩散 ===
            Vector2 endPos = screenStart + LaserDirection.ToRotationVector2() * currentLength;
            DrawBeamEnd(sb, pixel, endPos, widthRatio);

            // === V2：BeamGrad 流动金核（着色器缺失时自动回退到上方像素层）===
            Vector2 worldStart = Projectile.Center;
            Vector2 worldEnd = worldStart + LaserDirection.ToRotationVector2() * currentLength;
            ACMShaders.DrawBeam(worldStart, worldEnd, currentWidth * 0.7f,
                YinEmperorHelper.DragonVeinGold, YinEmperorHelper.AbyssPurple, widthRatio,
                flowSpeed: 1.6f, flowScale: 2.2f, coreSharp: 2.4f);

            return false;
        }

        /// <summary>
        /// 绘制单层光束 - 使用多段MagicPixel拼接，带波动效果
        /// </summary>
        private void DrawBeamLayer(SpriteBatch sb, Texture2D pixel, Vector2 screenStart,
            float rotation, float length, float width, Color color, float alpha, float waveOffset) {
            Vector2 dir = rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

            int segmentCount = (int)(length / 8f);
            if (segmentCount < 2) segmentCount = 2;

            Color drawColor = color;
            drawColor.A = 0;

            for (int i = 0; i < segmentCount; i++) {
                float progress = i / (float)segmentCount;
                Vector2 segPos = screenStart + dir * (length * progress);

                // 边缘淡出
                float edgeFade = 1f;
                if (progress < 0.05f)
                    edgeFade = progress / 0.05f;
                if (progress > 0.92f)
                    edgeFade = (1f - progress) / 0.08f;

                // 波动宽度变化
                float wave = 1f + MathF.Sin(progress * MathHelper.Pi * 6f + waveOffset) * 0.15f;
                float segWidth = width * wave * edgeFade;

                // 脉动亮度变化
                float pulse = 1f + MathF.Sin(progress * MathHelper.Pi * 4f + waveOffset * 1.3f) * 0.2f;

                // 横向微偏移营造不稳定感
                float drift = MathF.Sin(progress * MathHelper.Pi * 8f + waveOffset * 0.5f) * width * 0.05f;
                Vector2 finalPos = segPos + perp * drift;

                sb.Draw(pixel, finalPos, new Rectangle(0, 0, 1, 1),
                    drawColor * alpha * edgeFade * pulse,
                    rotation,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(length / segmentCount + 2f, segWidth),
                    SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绘制光束起点爆裂效果
        /// </summary>
        private void DrawBeamOrigin(SpriteBatch sb, Texture2D pixel, Vector2 pos, float widthRatio) {
            Color goldGlow = YinEmperorHelper.DragonVeinGold;
            goldGlow.A = 0;

            // 多层圆形叠加
            for (int i = 4; i >= 0; i--) {
                float size = currentWidth * (1.5f + i * 0.5f);
                float layerAlpha = 0.15f / (i + 1) * widthRatio;
                float pulse = 1f + MathF.Sin(pulsePhase + i * 0.5f) * 0.15f;

                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1),
                    goldGlow * layerAlpha * pulse,
                    pulsePhase * 0.3f + i * 0.2f,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(size, size),
                    SpriteEffects.None, 0);
            }

            // 十字高光
            Color white = Color.White;
            white.A = 0;
            float crossSize = currentWidth * 1.2f;
            float crossWidth = currentWidth * 0.3f;
            float crossAlpha = 0.3f * widthRatio;

            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1),
                white * crossAlpha,
                0f, new Vector2(0.5f, 0.5f),
                new Vector2(crossSize * 2f, crossWidth), SpriteEffects.None, 0);
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1),
                white * crossAlpha,
                MathHelper.PiOver2, new Vector2(0.5f, 0.5f),
                new Vector2(crossSize * 2f, crossWidth), SpriteEffects.None, 0);
        }

        /// <summary>
        /// 绘制光束末端扩散效果
        /// </summary>
        private void DrawBeamEnd(SpriteBatch sb, Texture2D pixel, Vector2 pos, float widthRatio) {
            Color purpleGlow = YinEmperorHelper.AbyssPurple;
            purpleGlow.A = 0;

            for (int i = 3; i >= 0; i--) {
                float size = currentWidth * (1f + i * 0.6f) * (1f + MathF.Sin(pulsePhase + i) * 0.1f);
                float layerAlpha = 0.1f / (i + 1) * widthRatio;

                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1),
                    purpleGlow * layerAlpha,
                    pulsePhase * 0.2f + i * 0.3f,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(size, size),
                    SpriteEffects.None, 0);
            }
        }
    }
}
