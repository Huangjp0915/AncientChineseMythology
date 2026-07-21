using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vigors
{
    /// <summary>
    /// 神威·断罪刃 场景效果控制器
    /// </summary>
    internal class VigorSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(ModContent.NPCType<Vigor>());
        public override void SpecialVisuals(Player player, bool isActive) {
            if (player.Alives()) {
                player.ManageSpecialBiomeVisuals(VigorSky.SkyName, isActive);
            }
        }
    }

    /// <summary>
    /// 神威·断罪刃天空效果 — 金纹裁决天幕
    /// 
    /// 多层绘制结构：
    ///  1. 深铁灰/暗金渐变底色 — 沉重的裁决天穹
    ///  2. Smoke帧动画符文烟云 — 铁灰/暗金交替,沉稳缓慢
    ///  3. 符文阵纹 — SoftGlow圆形符文印记脉冲,仿佛天空中浮现审判法阵
    ///  4. GlaciateWave 金色剑气横扫
    ///  5. BlankStar/Sparkle 金色碎光粒子
    ///  6. 暗角 + 金色裁决脉冲(反击架势时闪金)
    /// 
    /// 二阶段: 符文烟云加速、符文阵纹增多、底色偏金
    /// 三阶段: 天幕趋向白金色,强烈的审判压迫感
    /// </summary>
    internal class VigorSky : CustomSky, IACMLoader
    {
        public const string SkyName = "ACM:VigorSky";

        private bool active;
        private float intensity;
        private float globalTime;

        private const float MaxIntensity = 1f;
        private const float FadeInSpeed = 0.012f;
        private const float FadeOutSpeed = 0.018f;

        private float bossHealthPercent = 1f;
        private bool isPhase2;
        private bool isPhase3;
        private float deathDim;    // 死亡「阖卷」压暗 (读自 Vigor.DeathDimForSky)
        private float deathFlash;  // 终爆白金闪 (读自 Vigor.DeathFlashForSky)

        // 颜色定义 — 断罪刃: 铁灰 / 暗金 / 白金
        private static readonly Color IronGray = new(18, 16, 22);
        private static readonly Color DarkGold = new(60, 45, 12);
        private static readonly Color RuneGold = new(220, 180, 60);
        private static readonly Color RuneBlue = new(80, 140, 255);
        private static readonly Color WhiteGold = new(255, 245, 200);

        // 符文烟云
        private const int RuneCloudCount = 50;
        private readonly RuneCloud[] runeClouds = new RuneCloud[RuneCloudCount];

        // 符文阵纹 — 天空中的法阵印记
        private const int RuneGlyphCount = 6;
        private readonly RuneGlyph[] runeGlyphs = new RuneGlyph[RuneGlyphCount];

        // 金色碎光
        private const int GoldSparkCount = 20;
        private readonly GoldSpark[] goldSparks = new GoldSpark[GoldSparkCount];

        // 剑气横扫层
        private const int SwordWaveLayerCount = 3;
        private readonly float[] waveOffsets = new float[SwordWaveLayerCount];
        private static readonly float[] WaveSpeeds = [0.012f, 0.008f, 0.016f];

        #region IACMLoader 注册

        void IACMLoader.LoadData() {
            SkyManager.Instance[SkyName] = this;
            Filters.Scene[SkyName] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.10f, 0.08f, 0.03f)   // 暗金色调
                .UseOpacity(0.35f), EffectPriority.High);

            for (int i = 0; i < RuneCloudCount; i++) runeClouds[i] = new RuneCloud();
            for (int i = 0; i < RuneGlyphCount; i++) runeGlyphs[i] = new RuneGlyph(i, RuneGlyphCount);
            for (int i = 0; i < GoldSparkCount; i++) goldSparks[i] = new GoldSpark();
        }

        #endregion

        #region CustomSky 生命周期

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            bossHealthPercent = 1f;
            isPhase2 = false;
            isPhase3 = false;
            deathDim = 0f;
            deathFlash = 0f;

            for (int i = 0; i < RuneCloudCount; i++) runeClouds[i].Reset();
            for (int i = 0; i < RuneGlyphCount; i++) runeGlyphs[i].Reset();
            for (int i = 0; i < GoldSparkCount; i++) goldSparks[i].Reset();
            for (int i = 0; i < SwordWaveLayerCount; i++) waveOffsets[i] = i * 200f;
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
                isPhase2 = bossHealthPercent < Vigor.Phase2Threshold;
                isPhase3 = bossHealthPercent < Vigor.Phase3Threshold;

                // 死亡「阖卷」联动: 天幕熄灭 → 终爆白金闪
                if (boss.ModNPC is Vigor vigor) {
                    deathDim = MathHelper.Lerp(deathDim, vigor.DeathDimForSky, 0.15f);
                    deathFlash = vigor.DeathFlashForSky;
                }

                float target = isPhase3 ? MaxIntensity * 1.3f : isPhase2 ? MaxIntensity * 1.15f : MaxIntensity;
                intensity = MathHelper.Lerp(intensity, target, FadeInSpeed);
            }
            else {
                deathDim = MathHelper.Lerp(deathDim, 0f, 0.04f);
                deathFlash = MathHelper.Lerp(deathFlash, 0f, 0.1f);
                intensity -= FadeOutSpeed;
                if (intensity <= 0f) { intensity = 0f; if (active) Deactivate(); }
            }

            for (int i = 0; i < SwordWaveLayerCount; i++) waveOffsets[i] += WaveSpeeds[i];
            float mul = isPhase3 ? 1.5f : isPhase2 ? 1.2f : 1f;
            for (int i = 0; i < RuneCloudCount; i++) runeClouds[i].Update(mul);
            for (int i = 0; i < RuneGlyphCount; i++) runeGlyphs[i].Update(globalTime, isPhase2, isPhase3);
            for (int i = 0; i < GoldSparkCount; i++) goldSparks[i].Update();
        }

        private static NPC FindBoss() {
            foreach (NPC npc in Main.ActiveNPCs)
                if (npc.type == ModContent.NPCType<Vigor>() && npc.active) return npc;
            return null;
        }

        #endregion

        #region Draw

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (maxDepth >= 0 && minDepth < 0 && intensity > 0.01f) {
                DrawBackground(spriteBatch);
                DrawRuneClouds(spriteBatch);
                DrawRuneGlyphs(spriteBatch);
                DrawSwordWaves(spriteBatch);
                DrawGoldSparks(spriteBatch);
                DrawVignette(spriteBatch);
                DrawDeathVeil(spriteBatch);
            }
        }

        // 死亡「阖卷」: 天幕压暗渐熄 + 终爆整幕白金闪
        private void DrawDeathVeil(SpriteBatch sb) {
            if (deathDim <= 0.01f && deathFlash <= 0.01f)
                return;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle screen = new(0, 0, Main.screenWidth, Main.screenHeight);

            if (deathDim > 0.01f)
                sb.Draw(pixel, screen, Color.Black * (deathDim * 0.72f * intensity));

            if (deathFlash > 0.01f) {
                Color flash = WhiteGold * (deathFlash * 0.85f);
                flash.A = 0;
                sb.Draw(pixel, screen, flash);
            }
        }

        #endregion

        #region 层1 — 深铁灰/暗金渐变底色

        private void DrawBackground(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle screen = new(0, 0, Main.screenWidth, Main.screenHeight);

            sb.Draw(pixel, screen, IronGray * intensity * 0.95f);

            int bands = 10;
            for (int i = 0; i < bands; i++) {
                float t = (float)i / bands;
                int h = Main.screenHeight / bands;
                // 顶部暗金,底部铁灰——裁决的天穹从上方压下
                Color c = Color.Lerp(DarkGold, IronGray, t) * intensity * 0.45f;
                sb.Draw(pixel, new Rectangle(0, i * h, Main.screenWidth, h), c);
            }

            // 金色裁决脉冲——三阶段时白金化
            float breath = (0.5f + MathF.Sin(globalTime * 1.5f) * 0.5f) * intensity * 0.06f;
            if (isPhase3) breath *= 2.5f;
            else if (isPhase2) breath *= 1.5f;
            Color breathC = isPhase3
                ? Color.Lerp(RuneGold, WhiteGold, MathF.Sin(globalTime * 2f) * 0.5f + 0.5f) * breath
                : RuneGold * breath;
            breathC.A = 0;
            sb.Draw(pixel, screen, breathC);
        }

        #endregion

        #region 层2 — 符文烟云

        private void DrawRuneClouds(SpriteBatch sb) {
            Texture2D tex = ACMAsset.Smoke;
            if (tex == null) return;
            int fs = tex.Width / 4;
            Vector2 origin = new(fs / 2f);

            for (int i = 0; i < RuneCloudCount; i++) {
                RuneCloud c = runeClouds[i];
                if (!c.IsActive) continue;

                Vector2 dp = c.Position - Main.screenPosition;
                float lerp = MathF.Sin(globalTime * 0.5f + i * 0.2f) * 0.5f + 0.5f;
                // 铁灰与暗金交替的沉重烟云
                Color cc = Color.Lerp(new Color(30, 28, 35), DarkGold, lerp * 0.3f);
                if (i % 5 == 0) cc = Color.Lerp(cc, RuneGold, 0.08f); // 偶尔闪金

                float alpha = MathF.Sin(c.AnimProgress * MathHelper.Pi) * intensity * 0.45f;
                cc *= alpha;
                cc.A = 0;

                Rectangle src = new((i % 4) * fs, (i / 4 % 4) * fs, fs, fs);
                sb.Draw(tex, dp, src, cc, c.Rotation, origin, c.Scale, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层3 — 符文阵纹(天空法阵)

        private void DrawRuneGlyphs(SpriteBatch sb) {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null) return;
            Vector2 origin = glow.Size() / 2f;

            for (int i = 0; i < RuneGlyphCount; i++) {
                RuneGlyph g = runeGlyphs[i];
                if (g.Alpha <= 0.01f) continue;

                float alpha = g.Alpha * intensity;

                // 外圈——金色符文环
                Color ringColor = Color.Lerp(RuneGold, RuneBlue, g.BlueShift) * alpha * 0.3f;
                ringColor.A = 0;
                float ringScale = g.Scale * (1f + MathF.Sin(globalTime * 1.5f + i) * 0.1f);
                sb.Draw(glow, g.ScreenPos, null, ringColor, g.Rotation, origin, ringScale, SpriteEffects.None, 0f);

                // 内核——更亮
                Color coreColor = RuneGold * alpha * 0.5f;
                coreColor.A = 0;
                sb.Draw(glow, g.ScreenPos, null, coreColor, -g.Rotation * 0.7f, origin, ringScale * 0.5f, SpriteEffects.None, 0f);

                // 三阶段: 额外白金光芒
                if (isPhase3) {
                    Color whiteC = WhiteGold * alpha * 0.2f;
                    whiteC.A = 0;
                    sb.Draw(glow, g.ScreenPos, null, whiteC, g.Rotation * 0.3f, origin, ringScale * 1.3f, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        #region 层4 — 金色剑气横扫 (GlaciateWave)

        private void DrawSwordWaves(SpriteBatch sb) {
            Texture2D tex = ACMAsset.GlaciateWave;
            if (tex == null) return;
            Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);

            for (int layer = 0; layer < SwordWaveLayerCount; layer++) {
                float alpha = (0.06f - layer * 0.015f) * intensity;
                if (isPhase3) alpha *= 1.6f;
                else if (isPhase2) alpha *= 1.3f;

                // 金色/蓝色交替剑气
                Color mc = layer % 2 == 0
                    ? Color.Lerp(RuneGold, DarkGold, 0.4f) * alpha
                    : Color.Lerp(RuneBlue, DarkGold, 0.5f) * alpha;
                mc.A = 0;

                for (int band = 0; band < 2; band++) {
                    float xOff = waveOffsets[layer] * 60f + band * 500f;
                    float yOff = MathF.Sin(globalTime * 0.4f + layer + band * 2f) * 25f;
                    Vector2 pos = new(
                        (xOff % (Main.screenWidth + 600)) - 300,
                        Main.screenHeight * (0.25f + band * 0.35f + layer * 0.06f) + yOff
                    );
                    float rot = MathF.Sin(globalTime * 0.3f + layer) * 0.06f;
                    Vector2 scale = new(
                        Main.screenWidth * 0.7f / tex.Width * (1f + layer * 0.2f),
                        0.14f * (1f + layer * 0.12f)
                    );
                    sb.Draw(tex, pos, null, mc, rot, origin, scale, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        #region 层5 — 金色碎光

        private void DrawGoldSparks(SpriteBatch sb) {
            Texture2D tex = ACMAsset.Sparkle ?? ACMAsset.BlankStar;
            if (tex == null) return;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < GoldSparkCount; i++) {
                GoldSpark s = goldSparks[i];
                if (!s.IsActive) continue;

                Vector2 dp = s.Position - Main.screenPosition;
                float progress = MathF.Sin(s.AnimProgress * MathHelper.Pi);
                float alpha = progress * intensity * 0.4f;

                // 金色到蓝色渐变
                Color sc = Color.Lerp(RuneGold, RuneBlue, s.AnimProgress * 0.5f) * alpha;
                sc.A = 0;
                float scale = 0.02f + progress * 0.04f;
                sb.Draw(tex, dp, null, sc, s.Rotation, origin, scale, SpriteEffects.None, 0f);

                // 金色光晕
                Texture2D glowTex = ACMAsset.SoftGlow;
                if (glowTex != null) {
                    Color gc = RuneGold * alpha * 0.25f;
                    gc.A = 0;
                    sb.Draw(glowTex, dp, null, gc, 0f, glowTex.Size() / 2f, scale * 2.5f, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        #region 层6 — 暗角 + 金色裁决脉冲

        private void DrawVignette(SpriteBatch sb) {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Vector2 go = glow.Size() / 2f;
                float va = intensity * 0.5f;
                if (isPhase3) va *= 1.4f;
                Color vc = IronGray with { A = 0 } * va;
                float cs = MathF.Min(Main.screenWidth, Main.screenHeight) * 0.5f / glow.Width;

                sb.Draw(glow, new Vector2(0, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(0, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;

            // 顶部金色审判光映
            float pulse = (MathF.Sin(globalTime * 2f) * 0.5f + 0.5f) * intensity * 0.08f;
            if (isPhase3) pulse *= 2.2f;
            else if (isPhase2) pulse *= 1.4f;

            Color topC = Color.Lerp(RuneGold, WhiteGold, MathF.Sin(globalTime * 1.5f) * 0.5f + 0.5f) * pulse;
            topC.A = 0;
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight / 4), topC);

            // 底部铁灰压迫
            float btmPulse = pulse * 0.4f;
            Color btmC = DarkGold * btmPulse;
            btmC.A = 0;
            sb.Draw(pixel, new Rectangle(0, Main.screenHeight * 3 / 4, Main.screenWidth, Main.screenHeight / 4), btmC);
        }

        #endregion

        #region 地表着色

        public override Color OnTileColor(Color inColor) {
            // 暗金色偏移——仿佛裁决之光照射大地
            float goldShift = isPhase3 ? 0.35f : isPhase2 ? 0.25f : 0.15f;
            Color tint = Color.Lerp(Color.White, new Color(80, 65, 30), intensity * goldShift);
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

        private class RuneCloud
        {
            public Vector2 Position;
            public float Scale, Rotation, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(3, 25);
            }

            public void Update(float mul) {
                if (!IsActive) {
                    if (--cooldown <= 0) Activate(mul);
                    return;
                }
                AnimProgress += AnimSpeed * mul;
                Position += Velocity * mul;
                // 符文烟云缓慢沉降——沉重的审判天穹
                Position.Y += 0.08f * mul;
                Rotation += 0.0004f * mul;
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate(float mul) {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.0012f, 0.004f);
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-400, Main.screenWidth + 400),
                    Main.screenPosition.Y + Main.rand.Next(-300, (int)(Main.screenHeight * 0.6f))
                );
                Velocity = new Vector2(Main.rand.NextFloat(-0.2f, 0.2f) * mul, Main.rand.NextFloat(-0.1f, 0.15f));
                Scale = Main.rand.NextFloat(2.5f, 5.5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        private class RuneGlyph
        {
            public Vector2 ScreenPos;
            public float Alpha, Scale, Rotation, BlueShift;
            private readonly int index;
            private float timer;
            private readonly float basePeriod;
            private bool pulsing;

            public RuneGlyph(int i, int total) {
                index = i;
                basePeriod = 3.5f + i * 0.8f;
                Reset();
            }

            public void Reset() {
                Alpha = 0f;
                timer = 0f;
                pulsing = false;
                float angle = MathHelper.TwoPi / 6 * index;
                ScreenPos = new Vector2(
                    Main.screenWidth * 0.5f + MathF.Cos(angle) * Main.screenWidth * 0.3f,
                    Main.screenHeight * 0.35f + MathF.Sin(angle) * Main.screenHeight * 0.2f
                );
                Scale = 1.5f + index * 0.3f;
                BlueShift = index % 2 == 0 ? 0f : 0.4f;
            }

            public void Update(float gTime, bool phase2, bool phase3) {
                timer += 1f / 60f;
                float period = (phase2 || phase3) ? basePeriod * 0.6f : basePeriod;
                float pos = timer % period;
                float pulseDuration = 1.2f;

                if (pos < pulseDuration && !pulsing) {
                    pulsing = true;
                    // 随机偏移位置
                    float angle = MathHelper.TwoPi / 6 * index + MathF.Sin(gTime * 0.3f) * 0.5f;
                    ScreenPos = new Vector2(
                        Main.screenWidth * 0.5f + MathF.Cos(angle) * Main.screenWidth * 0.3f,
                        Main.screenHeight * 0.35f + MathF.Sin(angle) * Main.screenHeight * 0.22f
                    );
                }

                if (pulsing) {
                    if (pos < pulseDuration) {
                        float p = pos / pulseDuration;
                        Alpha = p < 0.2f ? p / 0.2f : 1f - (p - 0.2f) / 0.8f;
                        Alpha = MathHelper.Clamp(Alpha, 0f, 1f);
                        if (phase3) Alpha *= 1.4f;
                    }
                    else {
                        pulsing = false;
                        Alpha = 0f;
                    }
                }
                else {
                    Alpha = MathHelper.Lerp(Alpha, 0f, 0.08f);
                }

                Rotation += 0.005f;
            }
        }

        private class GoldSpark
        {
            public Vector2 Position;
            public float Rotation, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(8, 50);
            }

            public void Update() {
                if (!IsActive) {
                    if (--cooldown <= 0) Activate();
                    return;
                }
                AnimProgress += AnimSpeed;
                Position += Velocity;
                // 碎光缓慢下坠——沉重的裁决感
                Velocity.Y += 0.005f;
                Velocity.X += MathF.Sin(AnimProgress * 6f) * 0.01f;
                Rotation += 0.03f;
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.004f, 0.012f);
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(0, Main.screenWidth),
                    Main.screenPosition.Y + Main.rand.Next(-50, (int)(Main.screenHeight * 0.7f))
                );
                Velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.2f, 0.5f));
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }
    }
}
