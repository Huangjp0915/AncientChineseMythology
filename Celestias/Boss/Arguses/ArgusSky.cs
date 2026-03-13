using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Arguses
{
    /// <summary>
    /// 天目·追魂弧 场景效果控制器
    /// </summary>
    internal class ArgusSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(ModContent.NPCType<Argus>());
        public override void SpecialVisuals(Player player, bool isActive) {
            if (player.Alives()) {
                player.ManageSpecialBiomeVisuals(ArgusSky.SkyName, isActive);
            }
        }
    }

    /// <summary>
    /// 天目·追魂弧天空效果 — 星瞳虚空天幕
    /// 
    /// 多层绘制结构：
    ///  1. 深紫/墨蓝渐变底色 — 无尽虚空的星海
    ///  2. Smoke帧动画星云烟雾 — 紫/蓝交替,朦胧深邃
    ///  3. SoftGlow星辰散布 — 远景的静谧星点,被天目"注视"时加亮
    ///  4. 瞳孔投影 — 天空中浮现巨大的"天目"轮廓(SoftGlow组合+Sparkle)
    ///  5. LightningBranch 紫色凝视射线闪烁
    ///  6. 暗角 + 紫色脉冲(三阶段时天空仿佛被独眼完全占据)
    /// 
    /// 二阶段: 瞳孔更加清晰,星云加速旋转
    /// 三阶段: 天空被巨大紫瞳笼罩,强烈的"被注视"压迫感
    /// </summary>
    internal class ArgusSky : CustomSky, IACMLoader
    {
        public const string SkyName = "ACM:ArgusSky";

        private bool active;
        private float intensity;
        private float globalTime;

        private const float MaxIntensity = 1f;
        private const float FadeInSpeed = 0.012f;
        private const float FadeOutSpeed = 0.018f;

        private float bossHealthPercent = 1f;
        private bool isPhase2;
        private bool isPhase3;

        // 颜色定义 — 天目: 深紫 / 墨蓝 / 星白
        private static readonly Color VoidPurple = new(12, 6, 25);
        private static readonly Color DeepIndigo = new(25, 15, 55);
        private static readonly Color GazePurple = new(180, 80, 255);
        private static readonly Color StarBlue = new(80, 150, 255);
        private static readonly Color StarWhite = new(220, 230, 255);

        // 星云烟雾
        private const int NebulaCloudCount = 45;
        private readonly NebulaCloud[] nebulaClouds = new NebulaCloud[NebulaCloudCount];

        // 星辰散布
        private const int StarCount = 35;
        private readonly StarMote[] stars = new StarMote[StarCount];

        // 凝视射线
        private const int GazeRayCount = 3;
        private readonly GazeRay[] gazeRays = new GazeRay[GazeRayCount];

        // 星云层
        private const int NebulaLayerCount = 3;
        private readonly float[] nebulaOffsets = new float[NebulaLayerCount];
        private static readonly float[] NebulaSpeeds = [0.01f, 0.007f, 0.013f];

        // 天目瞳孔参数
        private float pupilScale;
        private float pupilAlpha;

        #region IACMLoader 注册

        void IACMLoader.LoadData() {
            SkyManager.Instance[SkyName] = this;
            Filters.Scene[SkyName] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.06f, 0.03f, 0.12f)  // 深紫色调
                .UseOpacity(0.4f), EffectPriority.High);

            for (int i = 0; i < NebulaCloudCount; i++) nebulaClouds[i] = new NebulaCloud();
            for (int i = 0; i < StarCount; i++) stars[i] = new StarMote();
            for (int i = 0; i < GazeRayCount; i++) gazeRays[i] = new GazeRay(i);
        }

        #endregion

        #region CustomSky 生命周期

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            bossHealthPercent = 1f;
            isPhase2 = false;
            isPhase3 = false;
            pupilScale = 0f;
            pupilAlpha = 0f;

            for (int i = 0; i < NebulaCloudCount; i++) nebulaClouds[i].Reset();
            for (int i = 0; i < StarCount; i++) stars[i].Reset();
            for (int i = 0; i < GazeRayCount; i++) gazeRays[i].Reset();
            for (int i = 0; i < NebulaLayerCount; i++) nebulaOffsets[i] = i * 220f;
        }

        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active || intensity > 0.01f;
        public override void Reset() { active = false; intensity = 0f; }

        public override void Update(GameTime gameTime) {
            globalTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

            NPC boss = FindBoss();
            bool shouldBeActive = boss != null && boss.active;

            if (shouldBeActive) {
                if (!active) Activate(Vector2.Zero);
                bossHealthPercent = (float)boss.life / boss.lifeMax;
                isPhase2 = bossHealthPercent < Argus.Phase2Threshold;
                isPhase3 = bossHealthPercent < Argus.Phase3Threshold;

                float target = isPhase3 ? MaxIntensity * 1.35f : isPhase2 ? MaxIntensity * 1.15f : MaxIntensity;
                intensity = MathHelper.Lerp(intensity, target, FadeInSpeed);
            }
            else {
                intensity -= FadeOutSpeed;
                if (intensity <= 0f) { intensity = 0f; if (active) Deactivate(); }
            }

            for (int i = 0; i < NebulaLayerCount; i++) nebulaOffsets[i] += NebulaSpeeds[i];
            float mul = isPhase3 ? 1.6f : isPhase2 ? 1.3f : 1f;
            for (int i = 0; i < NebulaCloudCount; i++) nebulaClouds[i].Update(mul);
            for (int i = 0; i < StarCount; i++) stars[i].Update(globalTime);
            for (int i = 0; i < GazeRayCount; i++) gazeRays[i].Update(globalTime, isPhase2, isPhase3);

            // 天目瞳孔缓慢浮现
            float targetPupilAlpha = isPhase3 ? 0.6f : isPhase2 ? 0.35f : 0.15f;
            float targetPupilScale = isPhase3 ? 1.2f : isPhase2 ? 0.9f : 0.6f;
            pupilAlpha = MathHelper.Lerp(pupilAlpha, targetPupilAlpha, 0.008f);
            pupilScale = MathHelper.Lerp(pupilScale, targetPupilScale, 0.006f);
        }

        private static NPC FindBoss() {
            foreach (NPC npc in Main.ActiveNPCs)
                if (npc.type == ModContent.NPCType<Argus>() && npc.active) return npc;
            return null;
        }

        #endregion

        #region Draw

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (maxDepth >= 0 && minDepth < 0 && intensity > 0.01f) {
                DrawBackground(spriteBatch);
                DrawNebulaClouds(spriteBatch);
                DrawStars(spriteBatch);
                DrawPupil(spriteBatch);
                DrawGazeRays(spriteBatch);
                DrawVignette(spriteBatch);
            }
        }

        #endregion

        #region 层1 — 深紫/墨蓝虚空底色

        private void DrawBackground(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle screen = new(0, 0, Main.screenWidth, Main.screenHeight);

            sb.Draw(pixel, screen, VoidPurple * intensity * 0.97f);

            int bands = 12;
            for (int i = 0; i < bands; i++) {
                float t = (float)i / bands;
                int h = Main.screenHeight / bands;
                // 从顶部墨蓝到底部深紫——深邃虚空
                Color c = Color.Lerp(DeepIndigo, VoidPurple, t) * intensity * 0.4f;
                sb.Draw(pixel, new Rectangle(0, i * h, Main.screenWidth, h), c);
            }

            // 虚空脉冲——三阶段时整个天空被紫色浸染
            float breath = (0.5f + MathF.Sin(globalTime * 1.2f) * 0.5f) * intensity * 0.05f;
            if (isPhase3) breath *= 3f;
            else if (isPhase2) breath *= 1.8f;
            Color breathC = GazePurple * breath;
            breathC.A = 0;
            sb.Draw(pixel, screen, breathC);
        }

        #endregion

        #region 层2 — 星云烟雾

        private void DrawNebulaClouds(SpriteBatch sb) {
            Texture2D tex = ACMAsset.Smoke;
            if (tex == null) return;
            int fs = tex.Width / 4;
            Vector2 origin = new(fs / 2f);

            for (int i = 0; i < NebulaCloudCount; i++) {
                NebulaCloud c = nebulaClouds[i];
                if (!c.IsActive) continue;

                Vector2 dp = c.Position - Main.screenPosition;
                float lerp = MathF.Sin(globalTime * 0.4f + i * 0.18f) * 0.5f + 0.5f;
                // 深紫与墨蓝交替的星云
                Color cc = Color.Lerp(new Color(20, 8, 45), new Color(10, 20, 50), lerp);
                if (i % 6 == 0) cc = Color.Lerp(cc, GazePurple, 0.06f); // 偶尔闪紫

                float alpha = MathF.Sin(c.AnimProgress * MathHelper.Pi) * intensity * 0.4f;
                cc *= alpha;
                cc.A = 0;

                Rectangle src = new((i % 4) * fs, (i / 4 % 4) * fs, fs, fs);
                sb.Draw(tex, dp, src, cc, c.Rotation, origin, c.Scale, SpriteEffects.None, 0f);

                // 星云微光
                if (i % 3 == 0) {
                    Color glowC = Color.Lerp(GazePurple, StarBlue, lerp) * alpha * 0.12f;
                    glowC.A = 0;
                    sb.Draw(tex, dp, src, glowC, c.Rotation * 0.9f, origin, c.Scale * 1.2f, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        #region 层3 — 星辰散布

        private void DrawStars(SpriteBatch sb) {
            Texture2D tex = ACMAsset.BlankStar ?? ACMAsset.SoftGlow;
            if (tex == null) return;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < StarCount; i++) {
                StarMote s = stars[i];
                if (s.Alpha <= 0.01f) continue;

                float alpha = s.Alpha * intensity;
                Color sc = Color.Lerp(StarWhite, StarBlue, s.BlueShift) * alpha;
                sc.A = 0;
                sb.Draw(tex, s.ScreenPos, null, sc, s.Rotation, origin, s.Scale, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层4 — 天目瞳孔投影(标志性视觉)

        private void DrawPupil(SpriteBatch sb) {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null || pupilAlpha < 0.01f) return;
            Vector2 origin = glow.Size() / 2f;

            float alpha = pupilAlpha * intensity;
            float centerX = Main.screenWidth * 0.5f;
            float centerY = Main.screenHeight * 0.3f;

            // 外层: 椭圆形"眼白" — 柔和紫色
            float outerScaleX = pupilScale * Main.screenWidth * 0.25f / glow.Width;
            float outerScaleY = outerScaleX * 0.55f; // 椭圆
            Color outerC = DeepIndigo * alpha * 0.5f;
            outerC.A = 0;
            sb.Draw(glow, new Vector2(centerX, centerY), null, outerC, 0f, origin,
                new Vector2(outerScaleX, outerScaleY), SpriteEffects.None, 0f);

            // 中层: 虹膜——紫色脉冲环
            float irisScale = pupilScale * 0.6f;
            float irisPulse = 1f + MathF.Sin(globalTime * 2.5f) * 0.08f;
            float irisScaleVal = irisScale * Main.screenWidth * 0.15f / glow.Width * irisPulse;
            Color irisC = GazePurple * alpha * 0.45f;
            irisC.A = 0;
            sb.Draw(glow, new Vector2(centerX, centerY), null, irisC, 0f, origin,
                new Vector2(irisScaleVal, irisScaleVal * 0.9f), SpriteEffects.None, 0f);

            // 内核: 白色瞳孔——凝视的焦点
            float coreScale = pupilScale * 0.25f;
            float corePulse = 1f + MathF.Sin(globalTime * 4f) * 0.15f;
            float coreScaleVal = coreScale * Main.screenWidth * 0.08f / glow.Width * corePulse;
            Color coreC = StarWhite * alpha * 0.6f;
            coreC.A = 0;
            sb.Draw(glow, new Vector2(centerX, centerY), null, coreC, 0f, origin,
                new Vector2(coreScaleVal, coreScaleVal), SpriteEffects.None, 0f);

            // 三阶段: 瞳孔外圈扩展紫色光晕——天空被瞳孔占据
            if (isPhase3) {
                float hugeScale = outerScaleX * 1.8f;
                Color hugeC = GazePurple * alpha * 0.15f;
                hugeC.A = 0;
                sb.Draw(glow, new Vector2(centerX, centerY), null, hugeC, 0f, origin,
                    new Vector2(hugeScale, hugeScale * 0.6f), SpriteEffects.None, 0f);
            }

            // 使用Sparkle绘制瞳孔周围的光芒——虹膜纹理
            Texture2D sparkle = ACMAsset.Sparkle;
            if (sparkle != null) {
                Vector2 spOrigin = sparkle.Size() / 2f;
                int rayCount = isPhase3 ? 8 : isPhase2 ? 6 : 4;
                for (int i = 0; i < rayCount; i++) {
                    float angle = MathHelper.TwoPi / rayCount * i + globalTime * 0.3f;
                    float dist = irisScaleVal * glow.Width * 0.5f;
                    Vector2 pos = new Vector2(centerX, centerY) + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.6f) * dist;
                    Color rayC = Color.Lerp(GazePurple, StarBlue, MathF.Sin(angle + globalTime) * 0.5f + 0.5f) * alpha * 0.25f;
                    rayC.A = 0;
                    float rayScale = 0.04f + pupilScale * 0.02f;
                    sb.Draw(sparkle, pos, null, rayC, angle + MathHelper.PiOver2, spOrigin, rayScale, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        #region 层5 — 凝视射线 (LightningBranch)

        private void DrawGazeRays(SpriteBatch sb) {
            Texture2D tex = ACMAsset.LightningBranch;
            if (tex == null) return;
            Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);

            for (int i = 0; i < GazeRayCount; i++) {
                GazeRay r = gazeRays[i];
                if (r.Alpha <= 0.01f) continue;

                float alpha = r.Alpha * intensity;
                Color rayC = Color.Lerp(GazePurple, StarBlue, r.BlueShift) * alpha;
                rayC.A = 0;

                // 从天目瞳孔中心射出的紫色光线
                Vector2 pupilCenter = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.3f);
                float rot = r.Angle;
                Vector2 pos = pupilCenter + new Vector2(MathF.Cos(rot), MathF.Sin(rot)) * r.Distance;

                float scaleX = 0.15f * r.Scale;
                float scaleY = Main.screenHeight * 0.6f / tex.Height * r.Scale;
                sb.Draw(tex, pos, null, rayC, rot + MathHelper.PiOver2, origin,
                    new Vector2(scaleX, scaleY), SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层6 — 暗角 + 紫色脉冲

        private void DrawVignette(SpriteBatch sb) {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Vector2 go = glow.Size() / 2f;
                float va = intensity * 0.6f;
                if (isPhase3) va *= 1.5f;
                Color vc = VoidPurple with { A = 0 } * va;
                float cs = MathF.Min(Main.screenWidth, Main.screenHeight) * 0.5f / glow.Width;

                sb.Draw(glow, new Vector2(0, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(0, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;

            // 紫色凝视脉冲——从屏幕中央扩散
            float pulse = (MathF.Sin(globalTime * 1.8f) * 0.5f + 0.5f) * intensity * 0.07f;
            if (isPhase3) pulse *= 2.5f;
            else if (isPhase2) pulse *= 1.6f;

            Color topC = GazePurple * pulse;
            topC.A = 0;
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight / 3), topC);

            // 底部深紫沉淀
            float btmPulse = pulse * 0.3f;
            Color btmC = DeepIndigo * btmPulse;
            btmC.A = 0;
            sb.Draw(pixel, new Rectangle(0, Main.screenHeight * 2 / 3, Main.screenWidth, Main.screenHeight / 3), btmC);
        }

        #endregion

        #region 地表着色

        public override Color OnTileColor(Color inColor) {
            // 紫色/蓝色偏移——被虚空凝视的大地
            float purpleShift = isPhase3 ? 0.4f : isPhase2 ? 0.3f : 0.2f;
            Color tint = Color.Lerp(Color.White, new Color(50, 35, 80), intensity * purpleShift);
            return new Color(
                (int)(inColor.R * tint.R / 255f),
                (int)(inColor.G * tint.G / 255f),
                (int)(inColor.B * tint.B / 255f),
                inColor.A
            );
        }

        public override float GetCloudAlpha() => 1f - intensity * 0.9f;

        #endregion

        // ================================================================
        // 内部粒子类
        // ================================================================

        private class NebulaCloud
        {
            public Vector2 Position;
            public float Scale, Rotation, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(3, 20);
            }

            public void Update(float mul) {
                if (!IsActive) {
                    if (--cooldown <= 0) Activate(mul);
                    return;
                }
                AnimProgress += AnimSpeed * mul;
                Position += Velocity * mul;
                // 星云缓慢旋转飘移——虚空中的寂静感
                Rotation += 0.0005f * mul;
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate(float mul) {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.001f, 0.0035f);
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-400, Main.screenWidth + 400),
                    Main.screenPosition.Y + Main.rand.Next(-300, (int)(Main.screenHeight * 0.65f))
                );
                Velocity = new Vector2(Main.rand.NextFloat(-0.15f, 0.15f) * mul, Main.rand.NextFloat(-0.1f, 0.1f));
                Scale = Main.rand.NextFloat(2f, 5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        private class StarMote
        {
            public Vector2 ScreenPos;
            public float Alpha, Scale, Rotation, BlueShift;
            private float twinklePhase;
            private float twinkleSpeed;

            public void Reset() {
                ScreenPos = new Vector2(
                    Main.rand.Next(0, Main.screenWidth),
                    Main.rand.Next(0, (int)(Main.screenHeight * 0.8f))
                );
                Scale = Main.rand.NextFloat(0.01f, 0.04f);
                BlueShift = Main.rand.NextFloat();
                twinklePhase = Main.rand.NextFloat(MathHelper.TwoPi);
                twinkleSpeed = Main.rand.NextFloat(1f, 3f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            public void Update(float gTime) {
                // 星星闪烁
                Alpha = (MathF.Sin(gTime * twinkleSpeed + twinklePhase) * 0.5f + 0.5f) * 0.35f;
                Rotation += 0.002f;
            }
        }

        private class GazeRay
        {
            public float Alpha, Scale, Angle, Distance, BlueShift;
            private readonly int index;
            private float timer;
            private readonly float basePeriod;
            private bool flashing;

            public GazeRay(int i) {
                index = i;
                basePeriod = 4f + i * 1.5f;
                Reset();
            }

            public void Reset() {
                Alpha = 0f;
                timer = 0f;
                flashing = false;
                Scale = 0.6f + index * 0.15f;
                BlueShift = index % 2 == 0 ? 0.2f : 0.6f;
                Angle = MathHelper.TwoPi / GazeRayCount * index;
                Distance = 50f + index * 30f;
            }

            public void Update(float gTime, bool phase2, bool phase3) {
                timer += 1f / 60f;
                float period = (phase2 || phase3) ? basePeriod * 0.5f : basePeriod;
                float pos = timer % period;
                float flashDur = 0.6f;

                if (pos < flashDur && !flashing) {
                    flashing = true;
                    Angle = MathHelper.TwoPi / GazeRayCount * index + MathF.Sin(gTime * 0.4f + index * 2f) * 0.8f;
                }

                if (flashing) {
                    if (pos < flashDur) {
                        float p = pos / flashDur;
                        Alpha = p < 0.15f ? p / 0.15f : 1f - (p - 0.15f) / 0.85f;
                        Alpha = MathHelper.Clamp(Alpha, 0f, 1f);
                        if (phase3) Alpha *= 1.5f;
                        else if (phase2) Alpha *= 1.2f;
                    }
                    else {
                        flashing = false;
                        Alpha = 0f;
                    }
                }
                else {
                    Alpha = MathHelper.Lerp(Alpha, 0f, 0.08f);
                }
            }
        }
    }
}
