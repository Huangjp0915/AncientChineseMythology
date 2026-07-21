using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Qinlongs
{
    /// <summary>
    /// 青龙场景效果控制器 — 自动检测Boss存在并管理天空激活
    /// </summary>
    internal class QinglongSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(ModContent.NPCType<Qinlong>());
        public override void SpecialVisuals(Player player, bool isActive) {
            if (player.Alives()) {
                player.ManageSpecialBiomeVisuals(QinglongSky.SkyName, isActive);
            }
        }
    }

    /// <summary>
    /// 青龙天空效果 — 苍龙风雷天幕
    /// 
    /// 多层绘制结构：
    ///  1. 墨翠风暴渐变底色 — 深翠/墨黑梯度
    ///  2. Smoke帧动画风暴云 — 翡翠色与墨色交替（雷暴天气云层加深）
    ///  3. LightningBranch青雷闪电
    ///  4. GlaciateWave风暴迷雾横漂
    ///  5. Sparkle风雷火花粒子
    ///  6. 四角暗角 + 雷映脉冲
    ///  7. 演出钩子：<see cref="FlashLightning"/> 雷击全屏白闪 / <see cref="DarkenSky"/> 相变瞬暗
    /// </summary>
    internal class QinglongSky : CustomSky, IACMLoader
    {
        public const string SkyName = "ACM:QinglongSky";

        private bool active;
        private float intensity;
        private float globalTime;

        // ---- 演出钩子 (AI 事件置位, 天幕自然衰减; 纯客户端) ----
        /// <summary>雷击白闪 0~1.6+ (雷击瞬间置 1, 每帧 ×0.86 衰减, 全屏白叠加)。</summary>
        internal static float s_lightningFlash;
        /// <summary>天空瞬暗 0~1 (相变/风暴压境时逐帧置位, 快速回落)。</summary>
        internal static float s_skyDarken;

        /// <summary>雷击瞬间调用: 天幕全屏白闪 (取 max, 不叠加)。</summary>
        public static void FlashLightning(float strength = 1f)
            => s_lightningFlash = MathF.Max(s_lightningFlash, strength);

        /// <summary>压暗天幕 (调用方逐帧维持, 不调用即自然回落)。</summary>
        public static void DarkenSky(float amount)
            => s_skyDarken = MathF.Max(s_skyDarken, MathHelper.Clamp(amount, 0f, 1f));

        /// <summary>世界卸载/Boss 死亡时清零钩子。</summary>
        public static void ResetHooks() {
            s_lightningFlash = 0f;
            s_skyDarken = 0f;
        }

        private const float MaxIntensity = 1f;
        private const float FadeInSpeed = 0.012f;
        private const float FadeOutSpeed = 0.018f;

        // 阶段阈值 (与 SacredBeastBase 默认一致; 本天幕自持以免耦合骨架实例属性)
        private const float Phase2Threshold = 0.60f;
        private const float Phase3Threshold = 0.30f;

        private float bossHealthPercent = 1f;
        private bool isPhase2;
        private bool isPhase3;

        // 颜色定义 — 青龙：翠绿 / 苍蓝 / 雷黄
        private static readonly Color JadeGreen = new(20, 80, 50);
        private static readonly Color DeepForest = new(8, 25, 18);
        private static readonly Color StormCyan = new(40, 180, 160);
        private static readonly Color ThunderGold = new(200, 220, 100);
        private static readonly Color WindSilver = new(180, 210, 200);

        // 风暴云
        private const int CloudCount = 45;
        private readonly WindCloud[] clouds = new WindCloud[CloudCount];

        // 闪电
        private const int LightningCount = 4;
        private readonly ThunderBolt[] bolts = new ThunderBolt[LightningCount];

        // 风粒子
        private const int SparkCount = 20;
        private readonly WindSpark[] sparks = new WindSpark[SparkCount];

        // 迷雾
        private const int MistLayerCount = 3;
        private readonly float[] mistOffsets = new float[MistLayerCount];
        private static readonly float[] MistSpeeds = [0.022f, 0.015f, 0.028f];

        #region IACMLoader 注册

        void IACMLoader.LoadData() {
            SkyManager.Instance[SkyName] = this;
            Filters.Scene[SkyName] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.05f, 0.12f, 0.08f)
                .UseOpacity(0.35f), EffectPriority.High);

            for (int i = 0; i < CloudCount; i++) clouds[i] = new WindCloud();
            for (int i = 0; i < LightningCount; i++) bolts[i] = new ThunderBolt(i);
            for (int i = 0; i < SparkCount; i++) sparks[i] = new WindSpark();
        }

        #endregion

        #region CustomSky 生命周期

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            bossHealthPercent = 1f;
            isPhase2 = false;
            isPhase3 = false;

            for (int i = 0; i < CloudCount; i++) clouds[i].Reset();
            for (int i = 0; i < LightningCount; i++) bolts[i].Reset();
            for (int i = 0; i < SparkCount; i++) sparks[i].Reset();
            for (int i = 0; i < MistLayerCount; i++) mistOffsets[i] = i * 300f;
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
                isPhase2 = bossHealthPercent < Phase2Threshold;
                isPhase3 = bossHealthPercent < Phase3Threshold;

                float target = isPhase3 ? MaxIntensity * 1.2f : isPhase2 ? MaxIntensity * 1.1f : MaxIntensity;
                intensity = MathHelper.Lerp(intensity, target, FadeInSpeed);
            }
            else {
                intensity -= FadeOutSpeed;
                if (intensity <= 0f) { intensity = 0f; if (active) Deactivate(); }
            }

            // Weather Deck「雷暴」窗口外溢到天幕: 雷更密、风暴更猛 (视觉契合 §4.3 表现)
            bool stormWeather = Qinlong.s_weatherMode == 2;

            for (int i = 0; i < MistLayerCount; i++) mistOffsets[i] += MistSpeeds[i];
            float stormMul = (isPhase3 ? 1.6f : isPhase2 ? 1.3f : 1f) * (stormWeather ? 1.3f : 1f);
            for (int i = 0; i < CloudCount; i++) clouds[i].Update(stormMul);
            for (int i = 0; i < LightningCount; i++) bolts[i].Update(globalTime, isPhase2 || isPhase3 || stormWeather);
            for (int i = 0; i < SparkCount; i++) sparks[i].Update();

            // 演出钩子自然衰减 (白闪快落, 瞬暗稍缓)
            s_lightningFlash *= 0.86f;
            if (s_lightningFlash < 0.01f) s_lightningFlash = 0f;
            s_skyDarken *= 0.92f;
            if (s_skyDarken < 0.01f) s_skyDarken = 0f;
        }

        private static NPC FindBoss() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<Qinlong>() && npc.active) return npc;
            }
            return null;
        }

        #endregion

        #region Draw

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (maxDepth >= 0 && minDepth < 0 && intensity > 0.01f) {
                DrawBackground(spriteBatch);
                DrawClouds(spriteBatch);
                DrawLightning(spriteBatch);
                DrawMist(spriteBatch);
                DrawSparks(spriteBatch);
                DrawVignette(spriteBatch);
                DrawHookOverlays(spriteBatch);
            }
        }

        /// <summary>层7 — 演出钩子叠加: 相变瞬暗压屏 + 雷击全屏白闪 (最顶层)。</summary>
        private void DrawHookOverlays(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle screen = new(0, 0, Main.screenWidth, Main.screenHeight);

            if (s_skyDarken > 0.01f)
                sb.Draw(pixel, screen, new Color(2, 6, 5) * (s_skyDarken * 0.62f * intensity));

            if (s_lightningFlash > 0.01f) {
                float f = MathHelper.Clamp(s_lightningFlash, 0f, 1.2f);
                Color flashC = new Color(225, 245, 255) * (f * 0.5f * intensity);
                flashC.A = 0; // 加性白闪, 不盖死场景
                sb.Draw(pixel, screen, flashC);
            }
        }

        #endregion

        #region 层1 — 墨翠渐变底色

        private void DrawBackground(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle screen = new(0, 0, Main.screenWidth, Main.screenHeight);

            sb.Draw(pixel, screen, DeepForest * intensity * 0.9f);

            int bands = 10;
            for (int i = 0; i < bands; i++) {
                float t = (float)i / bands;
                int h = Main.screenHeight / bands;
                Rectangle r = new(0, i * h, Main.screenWidth, h);
                Color c = Color.Lerp(JadeGreen, DeepForest, t) * intensity * 0.45f;
                sb.Draw(pixel, r, c);
            }

            // 风暴呼吸脉冲
            float breath = (0.5f + MathF.Sin(globalTime * 1.5f) * 0.5f) * intensity * 0.06f;
            if (isPhase3) breath *= 2f;
            else if (isPhase2) breath *= 1.4f;
            Color breathC = StormCyan * breath;
            breathC.A = 0;
            sb.Draw(pixel, screen, breathC);
        }

        #endregion

        #region 层2 — 风暴云

        private void DrawClouds(SpriteBatch sb) {
            Texture2D tex = ACMAsset.Smoke;
            if (tex == null) return;
            int fs = tex.Width / 4;
            Vector2 origin = new(fs / 2f);

            // 雷暴天气: 云层加深压暗 (承诺窗口的世界层反馈)
            bool stormWeather = Qinlong.s_weatherMode == 2;

            for (int i = 0; i < CloudCount; i++) {
                WindCloud c = clouds[i];
                if (!c.IsActive) continue;

                Vector2 dp = c.Position - Main.screenPosition;
                float lerp = MathF.Sin(globalTime * 0.5f + i * 0.3f) * 0.5f + 0.5f;
                Color cc = Color.Lerp(new Color(15, 50, 35), new Color(30, 60, 50), lerp);
                if (stormWeather) cc = Color.Lerp(cc, new Color(10, 22, 34), 0.5f);
                if (i % 6 == 0) cc = Color.Lerp(cc, StormCyan, 0.12f);

                float alpha = MathF.Sin(c.AnimProgress * MathHelper.Pi) * intensity * 0.5f;
                cc *= alpha;
                cc.A = 0;

                Rectangle src = new((i % 4) * fs, (i / 4 % 4) * fs, fs, fs);
                sb.Draw(tex, dp, src, cc, c.Rotation, origin, c.Scale, SpriteEffects.None, 0f);

                Color glow = Color.Lerp(JadeGreen, StormCyan, lerp) * alpha * 0.18f;
                glow.A = 0;
                sb.Draw(tex, dp, src, glow, c.Rotation * 0.9f, origin, c.Scale * 1.3f, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层3 — 青雷闪电

        private void DrawLightning(SpriteBatch sb) {
            Texture2D branchTex = ACMAsset.LightningBranch;
            if (branchTex == null) return;

            Vector2 origin = new(branchTex.Width / 2f, branchTex.Height * 0.15f);

            for (int i = 0; i < LightningCount; i++) {
                ThunderBolt b = bolts[i];
                if (b.FlashAlpha <= 0.01f) continue;

                float alpha = b.FlashAlpha * intensity;

                // 青色闪电
                Color boltC = Color.Lerp(StormCyan, WindSilver, b.FlashAlpha) * alpha;
                boltC.A = 0;

                Vector2 dp = new(b.ScreenX, 0f);
                float sx = b.Scale * 0.45f;
                float sy = b.Scale * 0.7f;
                SpriteEffects flip = b.Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                sb.Draw(branchTex, dp, null, boltC, 0f, origin, new Vector2(sx, sy), flip, 0f);

                // 外围雷黄辉光
                Color glowC = ThunderGold * alpha * 0.25f;
                glowC.A = 0;
                sb.Draw(branchTex, dp, null, glowC, 0f, origin, new Vector2(sx * 1.25f, sy * 1.08f), flip, 0f);
            }

            // 电弧
            Texture2D arcTex = ACMAsset.ElectricArcSheet;
            if (arcTex == null) return;
            int arcFrames = 4;
            int arcH = arcTex.Height / arcFrames;
            Vector2 arcOrigin = new(arcTex.Width / 2f, arcH / 2f);

            for (int i = 0; i < LightningCount; i++) {
                ThunderBolt b = bolts[i];
                if (b.FlashAlpha <= 0.05f) continue;

                int frame = ((int)(globalTime * 14f) + i * 3) % arcFrames;
                Rectangle src = new(0, frame * arcH, arcTex.Width, arcH);

                float alpha = b.FlashAlpha * intensity * 0.4f;
                Color arcC = StormCyan * alpha;
                arcC.A = 0;

                Vector2 arcPos = new(
                    b.ScreenX + MathF.Sin(globalTime * 3.5f + i) * 25f,
                    Main.screenHeight * 0.25f
                );
                float aScale = 0.32f * b.Scale;
                sb.Draw(arcTex, arcPos, src, arcC, MathHelper.PiOver2, arcOrigin,
                    new Vector2(aScale, aScale * 0.35f), SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层4 — 风暴迷雾

        private void DrawMist(SpriteBatch sb) {
            Texture2D tex = ACMAsset.GlaciateWave;
            if (tex == null) return;
            Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);

            for (int layer = 0; layer < MistLayerCount; layer++) {
                float alpha = (0.1f - layer * 0.025f) * intensity;
                if (isPhase3) alpha *= 1.4f;
                else if (isPhase2) alpha *= 1.2f;

                Color mc = Color.Lerp(JadeGreen, DeepForest, layer / (float)MistLayerCount) * alpha;
                mc.A = 0;

                for (int band = 0; band < 2; band++) {
                    float xOff = mistOffsets[layer] * 90f + band * 550f;
                    float yOff = MathF.Sin(globalTime * 0.45f + layer + band * 2f) * 35f;
                    Vector2 pos = new(
                        (xOff % (Main.screenWidth + 600)) - 300,
                        Main.screenHeight * (0.12f + band * 0.35f + layer * 0.1f) + yOff
                    );
                    float rot = MathF.Sin(globalTime * 0.3f + layer) * 0.1f;
                    Vector2 scale = new(
                        Main.screenWidth * 0.85f / tex.Width * (1.15f + layer * 0.25f),
                        0.22f * (1f + layer * 0.2f)
                    );
                    sb.Draw(tex, pos, null, mc, rot, origin, scale, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        #region 层5 — 风雷火花

        private void DrawSparks(SpriteBatch sb) {
            Texture2D tex = ACMAsset.Sparkle ?? ACMAsset.BlankStar;
            if (tex == null) return;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < SparkCount; i++) {
                WindSpark s = sparks[i];
                if (!s.IsActive) continue;

                Vector2 dp = s.Position - Main.screenPosition;
                float progress = MathF.Sin(s.AnimProgress * MathHelper.Pi);
                float alpha = progress * intensity * 0.5f;

                Color sc = Color.Lerp(StormCyan, ThunderGold, progress) * alpha;
                sc.A = 0;
                float scale = s.Scale * (0.06f + progress * 0.1f);
                sb.Draw(tex, dp, null, sc, globalTime * 4f + i, origin, scale, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层6 — 暗角 + 雷映

        private void DrawVignette(SpriteBatch sb) {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Vector2 go = glow.Size() / 2f;
                float va = intensity * 0.45f;
                if (isPhase3) va *= 1.3f;
                Color vc = DeepForest with { A = 0 } * va;
                float cs = MathF.Min(Main.screenWidth, Main.screenHeight) * 0.5f / glow.Width;

                sb.Draw(glow, new Vector2(0, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(0, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            float pulse = (MathF.Sin(globalTime * 2f) * 0.5f + 0.5f) * intensity * 0.08f;
            if (isPhase3) pulse *= 1.8f;
            else if (isPhase2) pulse *= 1.3f;

            Color topC = Color.Lerp(JadeGreen, StormCyan, MathF.Sin(globalTime * 0.9f) * 0.5f + 0.5f) * pulse;
            topC.A = 0;
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight / 5), topC);
        }

        #endregion

        #region 地表着色

        public override Color OnTileColor(Color inColor) {
            Color tint = Color.Lerp(Color.White, new Color(40, 70, 55), intensity * 0.3f);
            return new Color(
                (int)(inColor.R * tint.R / 255f),
                (int)(inColor.G * tint.G / 255f),
                (int)(inColor.B * tint.B / 255f),
                inColor.A
            );
        }

        public override float GetCloudAlpha() => 1f - intensity * 0.7f;

        #endregion

        // ================================================================
        // 内部粒子类
        // ================================================================

        private class WindCloud
        {
            public Vector2 Position;
            public float Scale, Rotation, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(5, 40);
            }

            public void Update(float mul) {
                if (!IsActive) {
                    if (--cooldown <= 0) Activate(mul);
                    return;
                }
                AnimProgress += AnimSpeed * mul;
                Position += Velocity * mul;
                Rotation += 0.0005f * mul;
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate(float mul) {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.001f, 0.0035f);
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-400, Main.screenWidth + 400),
                    Main.screenPosition.Y + Main.rand.Next(-250, (int)(Main.screenHeight * 0.65f))
                );
                Velocity = new Vector2(Main.rand.NextFloat(0.2f, 1f) * mul, Main.rand.NextFloat(-0.15f, 0.15f));
                Scale = Main.rand.NextFloat(2.5f, 5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        private class ThunderBolt
        {
            public float ScreenX, FlashAlpha, Scale;
            public bool Flip;
            private readonly int index;
            private float timer;
            private readonly float basePeriod, flashDuration;
            private bool flashing;

            public ThunderBolt(int i) {
                index = i;
                basePeriod = 2.8f + i * 1.2f;
                flashDuration = 0.16f + i * 0.03f;
                Reset();
            }

            public void Reset() {
                FlashAlpha = 0f;
                timer = 0f;
                flashing = false;
                ScreenX = Main.screenWidth * (0.12f + index * 0.22f);
                Scale = 0.65f + index * 0.1f;
                Flip = index % 2 == 0;
            }

            public void Update(float gTime, bool intense) {
                timer += 1f / 60f;
                float period = intense ? basePeriod * 0.5f : basePeriod;
                float pos = timer % period;

                if (pos < flashDuration && !flashing) {
                    flashing = true;
                    ScreenX = Main.screenWidth * (0.12f + index * 0.22f)
                            + MathF.Sin(gTime * 0.8f + index * 2f) * (Main.screenWidth * 0.07f);
                    Flip = ((int)(timer / period) + index) % 2 == 0;
                }

                if (flashing) {
                    if (pos < flashDuration) {
                        float p = pos / flashDuration;
                        FlashAlpha = p < 0.3f ? p / 0.3f : 1f - (p - 0.3f) / 0.7f;
                        FlashAlpha = MathHelper.Clamp(FlashAlpha, 0f, 1f);
                    }
                    else {
                        flashing = false;
                        FlashAlpha = 0f;
                    }
                }
                else {
                    FlashAlpha = MathHelper.Lerp(FlashAlpha, 0f, 0.15f);
                }
            }
        }

        private class WindSpark
        {
            public Vector2 Position;
            public float Scale, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(15, 100);
            }

            public void Update() {
                if (!IsActive) {
                    if (--cooldown <= 0) Activate();
                    return;
                }
                AnimProgress += AnimSpeed;
                Position += Velocity;
                Velocity.Y += 0.015f;
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.006f, 0.018f);
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(50, Main.screenWidth - 50),
                    Main.screenPosition.Y + Main.rand.Next(-40, (int)(Main.screenHeight * 0.4f))
                );
                Velocity = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-0.3f, 1.2f));
                Scale = Main.rand.NextFloat(0.7f, 1.8f);
            }
        }
    }
}
