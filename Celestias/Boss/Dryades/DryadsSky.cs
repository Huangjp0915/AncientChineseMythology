using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dryades
{
    /// <summary>
    /// 树精场景效果控制器 — 自动检测Boss存在并管理天空激活
    /// </summary>
    internal class DryadsSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(ModContent.NPCType<Dryads>());
        public override void SpecialVisuals(Player player, bool isActive)
        {
            if (player.Alives())
            {
                player.ManageSpecialBiomeVisuals(DryadsSky.SkyName, isActive);
            }
        }
    }

    /// <summary>
    /// 树精天空效果 — 幽暗灵木之森
    ///
    /// 多层绘制结构：
    ///  1. 幽暗森林渐变底色 — 深绿/苔藓色调
    ///  2. Smoke帧动画雾气层 — 低矮的森林迷雾
    ///  3. GlaciateWave根须光脉 — 从地面向上的翠绿脉动
    ///  4. Sparkle/BlankStar孢子粒子 — 浮游的绿色光点
    ///  5. SoftGlow树影暗角 — 屏幕边缘的幽暗林影
    ///  6. 暗角 + 脉冲呼吸
    /// </summary>
    internal class DryadsSky : CustomSky, IACMLoader
    {
        public const string SkyName = "ACM:DryadsSky";

        private bool active;
        private float intensity;
        private float globalTime;

        private const float MaxIntensity = 1f;
        private const float FadeInSpeed = 0.01f;
        private const float FadeOutSpeed = 0.018f;

        private float bossHealthPercent = 1f;
        private bool isPhase2;

        // 颜色定义 — 树精：幽暗森林 / 苔藓绿 / 灵光翠
        private static readonly Color DeepForest = new(6, 18, 8);
        private static readonly Color MossGreen = new(20, 50, 18);
        private static readonly Color SpiritGreen = new(80, 200, 60);
        private static readonly Color BarkDark = new(25, 16, 8);
        private static readonly Color FogGreen = new(12, 35, 14);
        private static readonly Color SporeGlow = new(120, 220, 80);
        private static readonly Color RootBrown = new(35, 22, 10);

        // 雾气层
        private const int FogCount = 25;
        private readonly FogCloud[] fogs = new FogCloud[FogCount];

        // 根须光脉
        private const int RootPulseCount = 4;
        private readonly RootPulse[] rootPulses = new RootPulse[RootPulseCount];

        // 孢子粒子
        private const int SporeCount = 35;
        private readonly Spore[] spores = new Spore[SporeCount];

        // 林影暗角
        private const int ShadowCount = 5;
        private readonly float[] shadowPhases = new float[ShadowCount];

        #region IACMLoader 注册

        void IACMLoader.LoadData()
        {
            SkyManager.Instance[SkyName] = this;
            Filters.Scene[SkyName] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.04f, 0.07f, 0.03f)
                .UseOpacity(0.25f), EffectPriority.High);

            for (int i = 0; i < FogCount; i++) fogs[i] = new FogCloud();
            for (int i = 0; i < RootPulseCount; i++) rootPulses[i] = new RootPulse(i);
            for (int i = 0; i < SporeCount; i++) spores[i] = new Spore();
        }

        #endregion

        #region CustomSky 生命周期

        public override void Activate(Vector2 position, params object[] args)
        {
            active = true;
            intensity = 0f;
            bossHealthPercent = 1f;
            isPhase2 = false;

            for (int i = 0; i < FogCount; i++) fogs[i].Reset();
            for (int i = 0; i < RootPulseCount; i++) rootPulses[i].Reset();
            for (int i = 0; i < SporeCount; i++) spores[i].Reset();
            for (int i = 0; i < ShadowCount; i++) shadowPhases[i] = i * 1.4f;
        }

        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active || intensity > 0.01f;
        public override void Reset() { active = false; intensity = 0f; }

        public override void Update(GameTime gameTime)
        {
            globalTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

            NPC boss = FindBoss();
            bool shouldBeActive = boss != null && boss.active;

            if (shouldBeActive)
            {
                if (!active) Activate(Vector2.Zero);
                bossHealthPercent = (float)boss.life / boss.lifeMax;
                isPhase2 = bossHealthPercent < Dryads.Phase2Threshold;

                float target = isPhase2 ? MaxIntensity * 1.1f : MaxIntensity;
                intensity = MathHelper.Lerp(intensity, target, FadeInSpeed);
            }
            else
            {
                intensity -= FadeOutSpeed;
                if (intensity <= 0f) { intensity = 0f; if (active) Deactivate(); }
            }

            float driftMul = isPhase2 ? 1.3f : 1f;
            for (int i = 0; i < FogCount; i++) fogs[i].Update(driftMul);
            for (int i = 0; i < RootPulseCount; i++) rootPulses[i].Update(globalTime, isPhase2);
            for (int i = 0; i < SporeCount; i++) spores[i].Update(globalTime);
            for (int i = 0; i < ShadowCount; i++) shadowPhases[i] += 0.007f;
        }

        private static NPC FindBoss()
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type == ModContent.NPCType<Dryads>() && npc.active) return npc;
            }
            return null;
        }

        #endregion

        #region Draw

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
        {
            if (maxDepth >= 0 && minDepth < 0 && intensity > 0.01f)
            {
                DrawBackground(spriteBatch);
                DrawFog(spriteBatch);
                DrawRootPulses(spriteBatch);
                DrawSpores(spriteBatch);
                DrawTreeShadows(spriteBatch);
                DrawVignette(spriteBatch);
            }
        }

        #endregion

        #region 层1 — 幽暗森林渐变底色

        private void DrawBackground(SpriteBatch sb)
        {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle screen = new(0, 0, Main.screenWidth, Main.screenHeight);

            sb.Draw(pixel, screen, DeepForest * intensity * 0.8f);

            int bands = 10;
            for (int i = 0; i < bands; i++)
            {
                float t = (float)i / bands;
                int h = Main.screenHeight / bands;
                Rectangle r = new(0, i * h, Main.screenWidth, h + 1);

                Color c;
                if (t < 0.35f)
                    c = Color.Lerp(DeepForest, FogGreen, t / 0.35f);
                else if (t < 0.65f)
                    c = Color.Lerp(FogGreen, BarkDark, (t - 0.35f) / 0.3f);
                else
                    c = Color.Lerp(BarkDark, RootBrown, (t - 0.65f) / 0.35f);

                sb.Draw(pixel, r, c * intensity * 0.35f);
            }

            // 二阶段：翠绿色呼吸脉冲
            float breath = (0.5f + MathF.Sin(globalTime * 1.5f) * 0.5f) * intensity * 0.04f;
            if (isPhase2) breath *= 2.5f;
            Color breathC = SpiritGreen * breath;
            breathC.A = 0;
            sb.Draw(pixel, screen, breathC);
        }

        #endregion

        #region 层2 — 森林迷雾

        private void DrawFog(SpriteBatch sb)
        {
            Texture2D tex = ACMAsset.Smoke;
            if (tex == null) return;
            int fs = tex.Width / 4;
            Vector2 origin = new(fs / 2f);

            for (int i = 0; i < FogCount; i++)
            {
                FogCloud f = fogs[i];
                if (!f.IsActive) continue;

                Vector2 dp = f.Position - Main.screenPosition;

                // 幽暗迷雾：深绿色调
                float lerp = MathF.Sin(globalTime * 0.25f + i * 0.5f) * 0.5f + 0.5f;
                Color fc = Color.Lerp(new Color(6, 22, 8), FogGreen, lerp * 0.5f);

                float alpha = MathF.Sin(f.AnimProgress * MathHelper.Pi) * intensity * 0.4f;
                fc *= alpha;
                fc.A = 0;

                Rectangle src = new((i % 4) * fs, (i / 4 % 4) * fs, fs, fs);
                sb.Draw(tex, dp, src, fc, f.Rotation, origin, f.Scale, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层3 — 根须光脉

        private void DrawRootPulses(SpriteBatch sb)
        {
            Texture2D tex = ACMAsset.GlaciateWave;
            if (tex == null) return;
            Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);

            for (int i = 0; i < RootPulseCount; i++)
            {
                RootPulse pulse = rootPulses[i];
                if (pulse.Alpha <= 0.01f) continue;

                float alpha = pulse.Alpha * intensity;

                // 翠绿色根须光脉，从底部向上
                Color pulseC = Color.Lerp(MossGreen, SpiritGreen, pulse.Alpha) * alpha * 0.3f;
                pulseC.A = 0;

                Vector2 pos = new(pulse.ScreenX, Main.screenHeight + tex.Height * 0.1f);
                // 竖直翻转：从下向上生长的光脉
                Vector2 scale = new(0.12f * pulse.Width, Main.screenHeight * 1.2f / tex.Height);
                sb.Draw(tex, pos, null, pulseC, MathHelper.Pi + pulse.Angle, origin, scale,
                    SpriteEffects.None, 0f);

                // 外层柔光
                Color outerC = SpiritGreen * alpha * 0.1f;
                outerC.A = 0;
                sb.Draw(tex, pos, null, outerC, MathHelper.Pi + pulse.Angle, origin,
                    scale * new Vector2(1.5f, 1.03f), SpriteEffects.None, 0f);
            }

            // 二阶段：底部翠绿光晕
            if (isPhase2)
            {
                Texture2D glow = ACMAsset.SoftGlow;
                if (glow == null) return;
                Vector2 glowOrigin = glow.Size() / 2f;

                float pulse2 = (MathF.Sin(globalTime * 2f) * 0.5f + 0.5f) * intensity * 0.06f;
                Color gc = SpiritGreen * pulse2;
                gc.A = 0;

                float gs = MathF.Max(Main.screenWidth, Main.screenHeight) * 0.5f / glow.Width;
                sb.Draw(glow, new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.75f),
                    null, gc, 0f, glowOrigin, gs, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层4 — 孢子粒子

        private void DrawSpores(SpriteBatch sb)
        {
            Texture2D tex = ACMAsset.SoftGlow ?? ACMAsset.BlankStar;
            if (tex == null) return;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < SporeCount; i++)
            {
                Spore spore = spores[i];
                if (!spore.IsActive) continue;

                Vector2 dp = spore.Position - Main.screenPosition;
                float progress = MathF.Sin(spore.AnimProgress * MathHelper.Pi);
                float alpha = progress * intensity * 0.5f;

                // 绿色荧光孢子
                float colorLerp = MathF.Sin(globalTime * 0.6f + i * 1.7f) * 0.5f + 0.5f;
                Color sc;
                if (i % 4 == 0)
                    sc = Color.Lerp(SporeGlow, SpiritGreen, colorLerp) * alpha;
                else
                    sc = Color.Lerp(MossGreen, SporeGlow, colorLerp * 0.7f) * alpha;
                sc.A = 0;

                // 孢子大小呼吸
                float scale = spore.Scale * (0.03f + progress * 0.05f);
                float breathScale = 1f + MathF.Sin(globalTime * 3f + i * 0.8f) * 0.15f;

                sb.Draw(tex, dp, null, sc, 0f, origin, scale * breathScale,
                    SpriteEffects.None, 0f);

                // 外圈微弱光晕
                if (i % 3 == 0)
                {
                    Color glowC = SporeGlow * alpha * 0.15f;
                    glowC.A = 0;
                    sb.Draw(tex, dp, null, glowC, 0f, origin, scale * breathScale * 1.8f,
                        SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        #region 层5 — 树影暗角

        private void DrawTreeShadows(SpriteBatch sb)
        {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null) return;
            Vector2 go = glow.Size() / 2f;

            float baseAlpha = intensity * 0.25f;
            if (isPhase2) baseAlpha *= 1.4f;

            for (int i = 0; i < ShadowCount; i++)
            {
                float phase = shadowPhases[i];
                float sway = MathF.Sin(phase * 2f + i * 0.9f) * 25f;
                float breathe = 0.85f + MathF.Sin(phase * 1.5f + i * 0.7f) * 0.15f;

                Vector2 pos;
                float baseScale;
                switch (i % 4)
                {
                    case 0:
                        pos = new Vector2(-25 + sway * 0.3f, Main.screenHeight * (0.2f + i * 0.13f));
                        baseScale = 2f;
                        break;
                    case 1:
                        pos = new Vector2(Main.screenWidth + 25 - sway * 0.3f,
                            Main.screenHeight * (0.18f + i * 0.12f));
                        baseScale = 2f;
                        break;
                    case 2:
                        pos = new Vector2(Main.screenWidth * (0.12f + i * 0.12f) + sway, -20);
                        baseScale = 2.4f;
                        break;
                    default:
                        pos = new Vector2(Main.screenWidth * (0.2f + i * 0.1f) + sway,
                            Main.screenHeight + 20);
                        baseScale = 1.8f;
                        break;
                }

                float scale = baseScale * breathe * Main.screenHeight * 0.28f / glow.Width;
                Color sc = DeepForest * baseAlpha * (0.5f + MathF.Sin(phase * 2.5f) * 0.2f);
                sc.A = 0;
                sb.Draw(glow, pos, null, sc, phase * 0.08f, go, scale, SpriteEffects.None, 0f);

                Color darkC = BarkDark * baseAlpha * 0.35f;
                darkC.A = 0;
                sb.Draw(glow, pos, null, darkC, phase * 0.06f, go, scale * 0.8f,
                    SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层6 — 暗角 + 脉冲

        private void DrawVignette(SpriteBatch sb)
        {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null)
            {
                Vector2 go = glow.Size() / 2f;
                float va = intensity * 0.45f;
                if (isPhase2) va *= 1.3f;
                Color vc = DeepForest with { A = 0 } * va;
                float cs = MathF.Min(Main.screenWidth, Main.screenHeight) * 0.5f / glow.Width;

                sb.Draw(glow, new Vector2(0, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, 0), null, vc, 0f, go, cs,
                    SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(0, Main.screenHeight), null, vc, 0f, go, cs,
                    SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, Main.screenHeight), null, vc, 0f, go, cs,
                    SpriteEffects.None, 0f);
            }

            // 翠绿脉冲呼吸
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float pulse = (MathF.Sin(globalTime * 1.8f) * 0.5f + 0.5f) * intensity * 0.04f;
            if (isPhase2) pulse *= 2f;

            // 底部根须褐色涌动
            Color bottomC = Color.Lerp(RootBrown, MossGreen,
                MathF.Sin(globalTime * 0.8f) * 0.5f + 0.5f) * pulse;
            bottomC.A = 0;
            sb.Draw(pixel, new Rectangle(0, Main.screenHeight * 3 / 4, Main.screenWidth,
                Main.screenHeight / 4), bottomC);

            // 顶部幽暗树冠
            Color topC = DeepForest * pulse * 0.7f;
            topC.A = 0;
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight / 5), topC);
        }

        #endregion

        #region 地表着色

        public override Color OnTileColor(Color inColor)
        {
            Color tint = Color.Lerp(Color.White, new Color(40, 55, 30), intensity * 0.35f);
            return new Color(
                (int)(inColor.R * tint.R / 255f),
                (int)(inColor.G * tint.G / 255f),
                (int)(inColor.B * tint.B / 255f),
                inColor.A
            );
        }

        public override float GetCloudAlpha() => 1f - intensity * 0.85f;

        #endregion

        // ================================================================
        // 内部粒子类
        // ================================================================

        #region FogCloud — 森林迷雾

        private class FogCloud
        {
            public Vector2 Position;
            public float Scale, Rotation, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public void Reset()
            {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(5, 40);
            }

            public void Update(float mul)
            {
                if (!IsActive)
                {
                    if (--cooldown <= 0) Activate(mul);
                    return;
                }
                AnimProgress += AnimSpeed * mul;
                Position += Velocity * mul;
                Rotation += 0.0002f * mul;
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate(float mul)
            {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.001f, 0.004f);
                // 雾气集中在屏幕中下部
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-400, Main.screenWidth + 400),
                    Main.screenPosition.Y + Main.rand.Next((int)(Main.screenHeight * 0.3f),
                        Main.screenHeight + 200)
                );
                Velocity = new Vector2(Main.rand.NextFloat(0.05f, 0.35f) * mul,
                    Main.rand.NextFloat(-0.03f, 0.05f));
                Scale = Main.rand.NextFloat(2.5f, 5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        #endregion

        #region RootPulse — 根须光脉

        private class RootPulse
        {
            public float ScreenX, Alpha, Width, Angle;
            private readonly int index;
            private float timer;
            private readonly float period;
            private readonly float glowDuration;
            private bool glowing;

            public RootPulse(int i)
            {
                index = i;
                period = 5f + i * 1.8f;
                glowDuration = 2f + i * 0.4f;
                Reset();
            }

            public void Reset()
            {
                Alpha = 0f;
                timer = 0f;
                glowing = false;
                ScreenX = Main.screenWidth * (0.15f + index * 0.2f);
                Width = 0.6f + index * 0.12f;
                Angle = -0.03f + index * 0.018f;
            }

            public void Update(float gTime, bool intense)
            {
                timer += 1f / 60f;
                float p = intense ? period * 0.65f : period;
                float pos = timer % p;

                if (pos < glowDuration && !glowing)
                {
                    glowing = true;
                    ScreenX = Main.screenWidth * (0.15f + index * 0.2f)
                            + MathF.Sin(gTime * 0.3f + index * 1.3f) * (Main.screenWidth * 0.04f);
                    Angle = -0.03f + index * 0.018f + MathF.Sin(gTime * 0.2f + index) * 0.02f;
                }

                if (glowing)
                {
                    if (pos < glowDuration)
                    {
                        float t = pos / glowDuration;
                        Alpha = t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f;
                        Alpha = MathHelper.Clamp(Alpha, 0f, 1f);
                    }
                    else
                    {
                        glowing = false;
                        Alpha = 0f;
                    }
                }
                else
                {
                    Alpha = MathHelper.Lerp(Alpha, 0f, 0.1f);
                }
            }
        }

        #endregion

        #region Spore — 孢子粒子

        private class Spore
        {
            public Vector2 Position;
            public float Scale, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;
            private float swayPhase;
            private float driftPhase;

            public void Reset()
            {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(8, 60);
            }

            public void Update(float gTime)
            {
                if (!IsActive)
                {
                    if (--cooldown <= 0) Activate();
                    return;
                }
                AnimProgress += AnimSpeed;
                // 孢子缓慢上浮+轻微摇摆
                float swayX = MathF.Sin(gTime * 1.2f + swayPhase) * 0.4f;
                float driftY = MathF.Sin(gTime * 0.8f + driftPhase) * 0.15f;
                Position += Velocity + new Vector2(swayX, driftY);
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate()
            {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.003f, 0.01f);
                // 孢子从屏幕各处随机出现，缓慢上浮
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-30, Main.screenWidth + 30),
                    Main.screenPosition.Y + Main.rand.Next((int)(Main.screenHeight * 0.3f),
                        Main.screenHeight + 50)
                );
                Velocity = new Vector2(Main.rand.NextFloat(-0.15f, 0.15f),
                    Main.rand.NextFloat(-0.5f, -0.1f));
                Scale = Main.rand.NextFloat(0.5f, 1.2f);
                swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
                driftPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        #endregion
    }
}
