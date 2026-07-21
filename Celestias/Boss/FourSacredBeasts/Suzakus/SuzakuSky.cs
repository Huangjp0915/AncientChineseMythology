using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Suzakus
{
    /// <summary>
    /// 朱雀场景效果控制器 — 自动检测Boss存在并管理天空激活
    /// </summary>
    internal class SuzakuSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(ModContent.NPCType<Suzaku>());
        public override void SpecialVisuals(Player player, bool isActive) {
            if (player.Alives()) {
                player.ManageSpecialBiomeVisuals(SuzakuSky.SkyName, isActive);
            }
        }
    }

    /// <summary>
    /// 朱雀天空效果 — 涅槃烈焰天幕
    /// 
    /// 多层绘制结构：
    ///  1. 深红/暗橙渐变底色 — 燃烧的天空
    ///  2. Smoke帧动画火焰云 — 赤红/暗橙交替
    ///  3. SlashBurst火柱闪烁
    ///  4. GlaciateWave热浪扭曲
    ///  5. EmberShards余烬飘落 + Sparkle火花
    ///  6. 暗角 + 朱红脉冲
    /// 
    /// 三阶段涅槃时整个天空趋向白热化
    /// </summary>
    internal class SuzakuSky : CustomSky, IACMLoader
    {
        public const string SkyName = "ACM:SuzakuSky";

        private bool active;
        private float intensity;
        private float globalTime;

        private const float MaxIntensity = 1f;
        private const float FadeInSpeed = 0.015f;
        private const float FadeOutSpeed = 0.018f;

        private float bossHealthPercent = 1f;
        private bool isPhase2;
        private bool isPhase3;

        // 颜色定义 — 朱雀：赤红 / 金橙 / 白热
        private static readonly Color DeepCrimson = new(30, 5, 5);
        private static readonly Color BurntOrange = new(80, 30, 8);
        private static readonly Color VermillionRed = new(200, 40, 20);
        private static readonly Color SolarGold = new(255, 200, 80);
        private static readonly Color WhiteHot = new(255, 240, 220);

        // 火焰云
        private const int FireCloudCount = 50;
        private readonly FireCloud[] fireClouds = new FireCloud[FireCloudCount];

        // 火柱闪烁
        private const int PillarCount = 4;
        private readonly FlamePillar[] pillars = new FlamePillar[PillarCount];

        // 余烬粒子
        private const int EmberCount = 22;
        private readonly Ember[] embers = new Ember[EmberCount];

        // 热浪迷雾
        private const int HeatLayerCount = 3;
        private readonly float[] heatOffsets = new float[HeatLayerCount];
        private static readonly float[] HeatSpeeds = [0.015f, 0.01f, 0.02f];

        #region IACMLoader 注册

        void IACMLoader.LoadData() {
            SkyManager.Instance[SkyName] = this;
            Filters.Scene[SkyName] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.15f, 0.05f, 0.02f)
                .UseOpacity(0.4f), EffectPriority.High);

            for (int i = 0; i < FireCloudCount; i++) fireClouds[i] = new FireCloud();
            for (int i = 0; i < PillarCount; i++) pillars[i] = new FlamePillar(i);
            for (int i = 0; i < EmberCount; i++) embers[i] = new Ember();
        }

        #endregion

        #region CustomSky 生命周期

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            bossHealthPercent = 1f;
            isPhase2 = false;
            isPhase3 = false;

            for (int i = 0; i < FireCloudCount; i++) fireClouds[i].Reset();
            for (int i = 0; i < PillarCount; i++) pillars[i].Reset();
            for (int i = 0; i < EmberCount; i++) embers[i].Reset();
            for (int i = 0; i < HeatLayerCount; i++) heatOffsets[i] = i * 280f;
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
                isPhase2 = bossHealthPercent < Suzaku.HpPhase2;
                isPhase3 = bossHealthPercent < Suzaku.HpPhase3;

                float target = isPhase3 ? MaxIntensity * 1.3f : isPhase2 ? MaxIntensity * 1.15f : MaxIntensity;
                intensity = MathHelper.Lerp(intensity, target, FadeInSpeed);
            }
            else {
                intensity -= FadeOutSpeed;
                if (intensity <= 0f) { intensity = 0f; if (active) Deactivate(); }
            }

            for (int i = 0; i < HeatLayerCount; i++) heatOffsets[i] += HeatSpeeds[i];
            float stormMul = isPhase3 ? 1.7f : isPhase2 ? 1.3f : 1f;
            for (int i = 0; i < FireCloudCount; i++) fireClouds[i].Update(stormMul);
            for (int i = 0; i < PillarCount; i++) pillars[i].Update(globalTime, isPhase2 || isPhase3);
            for (int i = 0; i < EmberCount; i++) embers[i].Update();
        }

        private static NPC FindBoss() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<Suzaku>() && npc.active) return npc;
            }
            return null;
        }

        #endregion

        #region Draw

        // 涅槃联动标量（由 SuzakuScreenSystem 发布, 每帧 Draw 起始采样）
        private float ashen;    // 灰烬去饱和 0~1
        private float sunBurst; // 日轮爆亮 0~1

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (maxDepth >= 0 && minDepth < 0 && intensity > 0.01f) {
                ashen = MathHelper.Clamp(SuzakuScreenSystem.AshenLevel, 0f, 1f);
                sunBurst = MathHelper.Clamp(SuzakuScreenSystem.SunBurstLevel, 0f, 1f);

                DrawBackground(spriteBatch);
                DrawFireClouds(spriteBatch);
                DrawFlamePillars(spriteBatch);
                DrawHeatWaves(spriteBatch);
                DrawEmbers(spriteBatch);
                DrawBurstSun(spriteBatch);
                DrawVignette(spriteBatch);
            }
        }

        /// <summary>涅槃灰烬期的天幕去饱和（按亮度回灰）。</summary>
        private Color Grey(Color c) {
            if (ashen <= 0.01f) return c;
            float l = (c.R * 0.3f + c.G * 0.59f + c.B * 0.11f) / 255f;
            return Color.Lerp(c, new Color(l * 0.9f, l * 0.9f, l * 0.92f), ashen * 0.85f);
        }

        /// <summary>爆燃日轮：入场日轮开屏 / 涅槃爆燃时的天空巨日同步爆亮。</summary>
        private void DrawBurstSun(SpriteBatch sb) {
            if (sunBurst <= 0.02f) return;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null) return;

            Vector2 pos = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.30f);
            Vector2 go = glow.Size() / 2f;
            float baseScale = MathF.Min(Main.screenWidth, Main.screenHeight) / glow.Width;

            Color outer = Color.Lerp(VermillionRed, SolarGold, 0.5f) with { A = 0 };
            sb.Draw(glow, pos, null, outer * (sunBurst * 0.65f), 0f, go, baseScale * (1.4f + sunBurst * 2.0f), SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, (SolarGold with { A = 0 }) * (sunBurst * 0.8f), 0f, go, baseScale * (0.8f + sunBurst * 1.2f), SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, (WhiteHot with { A = 0 }) * sunBurst, 0f, go, baseScale * (0.35f + sunBurst * 0.6f), SpriteEffects.None, 0f);

            Texture2D spark = ACMAsset.Sparkle;
            if (spark != null) {
                Color rc = (SolarGold with { A = 0 }) * (sunBurst * 0.7f);
                sb.Draw(spark, pos, null, rc, globalTime * 0.1f, spark.Size() / 2f, baseScale * (1.2f + sunBurst * 1.6f), SpriteEffects.None, 0f);
                sb.Draw(spark, pos, null, rc * 0.7f, -globalTime * 0.07f, spark.Size() / 2f, baseScale * (0.9f + sunBurst * 1.2f), SpriteEffects.None, 0f);
            }

            // 全屏金白闪（爆燃一瞬）
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color flash = (WhiteHot with { A = 0 }) * (sunBurst * sunBurst * 0.30f);
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), flash);
        }

        #endregion

        #region 层1 — 赤红渐变底色

        private void DrawBackground(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle screen = new(0, 0, Main.screenWidth, Main.screenHeight);

            sb.Draw(pixel, screen, Grey(DeepCrimson) * intensity * 0.95f);

            int bands = 10;
            for (int i = 0; i < bands; i++) {
                float t = (float)i / bands;
                int h = Main.screenHeight / bands;
                // 顶部更亮（火的照映），底部更暗
                Color c = Grey(Color.Lerp(BurntOrange, DeepCrimson, t)) * intensity * 0.5f;
                sb.Draw(pixel, new Rectangle(0, i * h, Main.screenWidth, h), c);
            }

            // 火焰呼吸脉冲 — 三阶段涅槃时白热化；灰烬寂静期呼吸熄止
            float breath = (0.5f + MathF.Sin(globalTime * 1.8f) * 0.5f) * intensity * 0.08f * (1f - ashen);
            if (isPhase3) breath *= 2.5f;
            else if (isPhase2) breath *= 1.6f;
            Color breathC = isPhase3
                ? Color.Lerp(VermillionRed, WhiteHot, MathF.Sin(globalTime * 2f) * 0.5f + 0.5f) * breath
                : VermillionRed * breath;
            breathC.A = 0;
            sb.Draw(pixel, screen, breathC);
        }

        #endregion

        #region 层2 — 火焰云

        private void DrawFireClouds(SpriteBatch sb) {
            Texture2D tex = ACMAsset.Smoke;
            if (tex == null) return;
            int fs = tex.Width / 4;
            Vector2 origin = new(fs / 2f);

            for (int i = 0; i < FireCloudCount; i++) {
                FireCloud c = fireClouds[i];
                if (!c.IsActive) continue;

                Vector2 dp = c.Position - Main.screenPosition;
                float lerp = MathF.Sin(globalTime * 0.6f + i * 0.25f) * 0.5f + 0.5f;
                Color cc = Grey(Color.Lerp(new Color(60, 15, 5), BurntOrange, lerp));
                if (i % 4 == 0) cc = Color.Lerp(cc, Grey(SolarGold), 0.15f);

                float alpha = MathF.Sin(c.AnimProgress * MathHelper.Pi) * intensity * 0.5f * (1f - ashen * 0.5f);
                cc *= alpha;
                cc.A = 0;

                Rectangle src = new((i % 4) * fs, (i / 4 % 4) * fs, fs, fs);
                sb.Draw(tex, dp, src, cc, c.Rotation, origin, c.Scale, SpriteEffects.None, 0f);

                // 火光映底（灰烬期熄灭）
                Color glow = Grey(Color.Lerp(VermillionRed, SolarGold, lerp)) * (alpha * 0.2f * (1f - ashen));
                glow.A = 0;
                sb.Draw(tex, dp, src, glow, c.Rotation * 0.85f, origin, c.Scale * 1.25f, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层3 — 火柱闪烁 (SlashBurst)

        private void DrawFlamePillars(SpriteBatch sb) {
            Texture2D tex = ACMAsset.SlashBurst;
            if (tex == null) return;
            Vector2 origin = new(tex.Width / 2f, tex.Height * 0.85f);

            for (int i = 0; i < PillarCount; i++) {
                FlamePillar p = pillars[i];
                if (p.FlashAlpha <= 0.01f) continue;

                float alpha = p.FlashAlpha * intensity * (1f - ashen * 0.9f); // 灰烬期天幕火柱熄灭

                // 火柱 — 从底部喷射
                Color pillarC = Color.Lerp(VermillionRed, SolarGold, p.FlashAlpha) * alpha;
                pillarC.A = 0;

                Vector2 dp = new(p.ScreenX, Main.screenHeight);
                sb.Draw(tex, dp, null, pillarC, 0f, origin, new Vector2(0.25f * p.Scale, 0.65f * p.Scale), SpriteEffects.None, 0f);

                Color glowC = SolarGold * alpha * 0.25f;
                glowC.A = 0;
                sb.Draw(tex, dp, null, glowC, 0f, origin, new Vector2(0.35f * p.Scale, 0.75f * p.Scale), SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层4 — 热浪 (GlaciateWave)

        private void DrawHeatWaves(SpriteBatch sb) {
            Texture2D tex = ACMAsset.GlaciateWave;
            if (tex == null) return;
            Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);

            for (int layer = 0; layer < HeatLayerCount; layer++) {
                float alpha = (0.08f - layer * 0.02f) * intensity * (1f - ashen * 0.9f);
                if (isPhase3) alpha *= 1.5f;
                else if (isPhase2) alpha *= 1.2f;

                Color mc = Color.Lerp(VermillionRed, BurntOrange, layer / (float)HeatLayerCount) * alpha;
                mc.A = 0;

                for (int band = 0; band < 2; band++) {
                    float xOff = heatOffsets[layer] * 70f + band * 500f;
                    float yOff = MathF.Sin(globalTime * 0.5f + layer + band * 2.5f) * 30f;
                    Vector2 pos = new(
                        (xOff % (Main.screenWidth + 600)) - 300,
                        Main.screenHeight * (0.3f + band * 0.3f + layer * 0.08f) + yOff
                    );
                    float rot = MathF.Sin(globalTime * 0.35f + layer) * 0.08f;
                    Vector2 scale = new(
                        Main.screenWidth * 0.8f / tex.Width * (1.1f + layer * 0.25f),
                        0.18f * (1f + layer * 0.15f)
                    );
                    sb.Draw(tex, pos, null, mc, rot, origin, scale, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        #region 层5 — 余烬飘落

        private void DrawEmbers(SpriteBatch sb) {
            Texture2D tex = ACMAsset.EmberShards ?? ACMAsset.Sparkle;
            if (tex == null) return;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < EmberCount; i++) {
                Ember e = embers[i];
                if (!e.IsActive) continue;

                Vector2 dp = e.Position - Main.screenPosition;
                float progress = MathF.Sin(e.AnimProgress * MathHelper.Pi);
                float alpha = progress * intensity * 0.5f * (1f - ashen * 0.85f);

                // 从金色到朱红渐变
                Color ec = Color.Lerp(SolarGold, VermillionRed, e.AnimProgress) * alpha;
                ec.A = 0;
                float scale = 0.03f + progress * 0.05f;
                sb.Draw(tex, dp, null, ec, e.Rotation, origin, scale, SpriteEffects.None, 0f);

                // 余烬发光
                Texture2D glowTex = ACMAsset.SoftGlow;
                if (glowTex != null) {
                    Color gc = SolarGold * alpha * 0.3f;
                    gc.A = 0;
                    sb.Draw(glowTex, dp, null, gc, 0f, glowTex.Size() / 2f, scale * 3f, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        #region 层6 — 暗角 + 朱红脉冲

        private void DrawVignette(SpriteBatch sb) {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Vector2 go = glow.Size() / 2f;
                float va = intensity * 0.55f;
                if (isPhase3) va *= 1.4f;
                Color vc = DeepCrimson with { A = 0 } * va;
                float cs = MathF.Min(Main.screenWidth, Main.screenHeight) * 0.5f / glow.Width;

                sb.Draw(glow, new Vector2(0, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(0, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            float pulse = (MathF.Sin(globalTime * 2.2f) * 0.5f + 0.5f) * intensity * 0.1f;
            if (isPhase3) pulse *= 2f;
            else if (isPhase2) pulse *= 1.5f;

            // 整屏朱红脉冲
            Color topC = Color.Lerp(VermillionRed, SolarGold, MathF.Sin(globalTime * 1.2f) * 0.5f + 0.5f) * pulse;
            topC.A = 0;
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight / 4), topC);

            // 底部热映
            float btmPulse = pulse * 0.6f;
            Color btmC = BurntOrange * btmPulse;
            btmC.A = 0;
            sb.Draw(pixel, new Rectangle(0, Main.screenHeight * 3 / 4, Main.screenWidth, Main.screenHeight / 4), btmC);
        }

        #endregion

        #region 地表着色

        public override Color OnTileColor(Color inColor) {
            float redShift = isPhase3 ? 0.4f : 0.3f;
            Color tint = Color.Lerp(Color.White, new Color(80, 40, 30), intensity * redShift);
            return new Color(
                (int)(inColor.R * tint.R / 255f),
                (int)(inColor.G * tint.G / 255f),
                (int)(inColor.B * tint.B / 255f),
                inColor.A
            );
        }

        public override float GetCloudAlpha() => 1f - intensity * 0.8f;

        #endregion

        // ================================================================
        // 内部粒子类
        // ================================================================

        private class FireCloud
        {
            public Vector2 Position;
            public float Scale, Rotation, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(3, 30);
            }

            public void Update(float mul) {
                if (!IsActive) {
                    if (--cooldown <= 0) Activate(mul);
                    return;
                }
                AnimProgress += AnimSpeed * mul;
                Position += Velocity * mul;
                // 火焰云缓慢上升
                Position.Y -= 0.15f * mul;
                Rotation += 0.0006f * mul;
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate(float mul) {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.0015f, 0.005f);
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-500, Main.screenWidth + 500),
                    Main.screenPosition.Y + Main.rand.Next(-200, (int)(Main.screenHeight * 0.75f))
                );
                Velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.5f) * mul, Main.rand.NextFloat(-0.4f, 0.1f));
                Scale = Main.rand.NextFloat(2f, 5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        private class FlamePillar
        {
            public float ScreenX, FlashAlpha, Scale;
            private readonly int index;
            private float timer;
            private readonly float basePeriod, flashDuration;
            private bool flashing;

            public FlamePillar(int i) {
                index = i;
                basePeriod = 3f + i * 1.3f;
                flashDuration = 0.3f + i * 0.05f;
                Reset();
            }

            public void Reset() {
                FlashAlpha = 0f;
                timer = 0f;
                flashing = false;
                ScreenX = Main.screenWidth * (0.1f + index * 0.25f);
                Scale = 0.6f + index * 0.12f;
            }

            public void Update(float gTime, bool intense) {
                timer += 1f / 60f;
                float period = intense ? basePeriod * 0.5f : basePeriod;
                float pos = timer % period;

                if (pos < flashDuration && !flashing) {
                    flashing = true;
                    ScreenX = Main.screenWidth * (0.05f + index * 0.25f)
                            + MathF.Sin(gTime * 0.6f + index * 2.5f) * (Main.screenWidth * 0.08f);
                }

                if (flashing) {
                    if (pos < flashDuration) {
                        float p = pos / flashDuration;
                        // 火柱：快速升起，缓慢消散
                        FlashAlpha = p < 0.25f ? p / 0.25f : 1f - (p - 0.25f) / 0.75f;
                        FlashAlpha = MathHelper.Clamp(FlashAlpha, 0f, 1f);
                    }
                    else {
                        flashing = false;
                        FlashAlpha = 0f;
                    }
                }
                else {
                    FlashAlpha = MathHelper.Lerp(FlashAlpha, 0f, 0.1f);
                }
            }
        }

        private class Ember
        {
            public Vector2 Position;
            public float Rotation, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(10, 60);
            }

            public void Update() {
                if (!IsActive) {
                    if (--cooldown <= 0) Activate();
                    return;
                }
                AnimProgress += AnimSpeed;
                Position += Velocity;
                // 余烬向上飘，受热气流影响
                Velocity.Y -= 0.01f;
                Velocity.X += MathF.Sin(AnimProgress * 8f) * 0.02f;
                Rotation += 0.04f;
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.005f, 0.015f);
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(0, Main.screenWidth),
                    Main.screenPosition.Y + Main.rand.Next((int)(Main.screenHeight * 0.4f), Main.screenHeight + 100)
                );
                Velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1.5f, -0.3f));
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }
    }
}
