using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    /// <summary>
    /// 敖顺场景效果控制器 — 自动检测Boss存在并管理天空激活
    /// 替代旧的手动 SkyManager.Instance.Activate/Deactivate 调用
    /// </summary>
    internal class AoshunSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(ModContent.NPCType<Aoshun>());
        public override void SpecialVisuals(Player player, bool isActive) {
            if (player.Alives()) {
                player.ManageSpecialBiomeVisuals(AoshunSky.SkyName, isActive);
            }
        }
    }

    /// <summary>
    /// 北海龙王敖顺天空效果 — 北海雷暴天幕
    /// 
    /// 多层绘制结构：
    ///  1. 墨紫风暴渐变底色 — AbyssPurple / StormGray 梯度
    ///  2. Smoke帧动画雷云层 — 60朵云，暗紫/灰色交替
    ///  3. LightningBranch闪电分叉 + ElectricArcSheet电弧
    ///  4. GlaciateWave风暴迷雾横漂
    ///  5. Sparkle/BlankStar电火花粒子
    ///  6. 四角暗角 + 顶部雷映脉冲
    /// 
    /// Boss血量低于50%进入二阶段：云速加快、闪电频率翻倍、暗角加深
    /// 
    /// 采用 IACMLoader + ModSceneEffect 模式，修复旧版闪烁问题：
    ///  · IsActive() 以 intensity 兜底，防止天空突然消失
    ///  · Draw() 加 minDepth/maxDepth 检查，防止重复绘制
    ///  · 闪电采用确定性正弦周期，不用随机
    /// </summary>
    internal class AoshunSky : CustomSky, IACMLoader
    {
        public const string SkyName = "ACM:AoshunSky";

        private bool active;
        private float intensity;
        private float globalTime;

        private const float MaxIntensity = 1f;
        private const float FadeInSpeed = 0.012f;
        private const float FadeOutSpeed = 0.018f;

        // Boss状态
        private float bossHealthPercent = 1f;
        private bool isPhase2;

        // === 风暴云层 ===
        private const int StormCloudCount = 60;
        private readonly StormCloud[] stormClouds = new StormCloud[StormCloudCount];

        // === 闪电系统（确定性计时） ===
        private const int LightningBoltCount = 5;
        private readonly LightningBolt[] lightningBolts = new LightningBolt[LightningBoltCount];

        // === 电火花粒子 ===
        private const int SparkCount = 25;
        private readonly ElectricSpark[] sparks = new ElectricSpark[SparkCount];

        // === 迷雾层 ===
        private const int MistLayerCount = 3;
        private readonly float[] mistOffsets = new float[MistLayerCount];
        private static readonly float[] MistSpeeds = [0.018f, 0.012f, 0.022f];

        #region IACMLoader 注册

        void IACMLoader.LoadData() {
            SkyManager.Instance[SkyName] = this;
            Filters.Scene[SkyName] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.08f, 0.04f, 0.15f)   // 深紫色调滤镜
                .UseOpacity(0.4f), EffectPriority.High);

            for (int i = 0; i < StormCloudCount; i++)
                stormClouds[i] = new StormCloud();
            for (int i = 0; i < LightningBoltCount; i++)
                lightningBolts[i] = new LightningBolt(i);
            for (int i = 0; i < SparkCount; i++)
                sparks[i] = new ElectricSpark();
        }

        #endregion

        #region CustomSky 生命周期

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            bossHealthPercent = 1f;
            isPhase2 = false;

            for (int i = 0; i < StormCloudCount; i++) stormClouds[i].Reset();
            for (int i = 0; i < LightningBoltCount; i++) lightningBolts[i].Reset();
            for (int i = 0; i < SparkCount; i++) sparks[i].Reset();
            for (int i = 0; i < MistLayerCount; i++) mistOffsets[i] = i * 333f;
        }

        public override void Deactivate(params object[] args) => active = false;

        /// <summary>
        /// 关键修复：用 intensity 兜底，防止天空在消退过程中突然消失导致闪烁
        /// </summary>
        public override bool IsActive() => active || intensity > 0.01f;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            globalTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

            NPC boss = FindBoss();
            bool shouldBeActive = boss != null && boss.active;

            if (shouldBeActive) {
                if (!active) Activate(Vector2.Zero);

                bossHealthPercent = (float)boss.life / boss.lifeMax;
                isPhase2 = bossHealthPercent < 0.5f;

                float targetIntensity = MaxIntensity;
                if (bossHealthPercent < 0.3f)
                    targetIntensity = MaxIntensity * 1.2f;   // 末阶段最暴烈
                else if (isPhase2)
                    targetIntensity = MaxIntensity * 1.1f;   // 二阶段增强

                intensity = MathHelper.Lerp(intensity, targetIntensity, FadeInSpeed);
            }
            else {
                intensity -= FadeOutSpeed;
                if (intensity <= 0f) {
                    intensity = 0f;
                    if (active) Deactivate();
                }
            }

            // --- 更新子系统 ---
            for (int i = 0; i < MistLayerCount; i++)
                mistOffsets[i] += MistSpeeds[i];

            float stormMul = isPhase2 ? 1.4f : 1f;
            for (int i = 0; i < StormCloudCount; i++)
                stormClouds[i].Update(stormMul);
            for (int i = 0; i < LightningBoltCount; i++)
                lightningBolts[i].Update(globalTime, isPhase2);
            for (int i = 0; i < SparkCount; i++)
                sparks[i].Update();
        }

        private static NPC FindBoss() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<Aoshun>() && npc.active)
                    return npc;
            }
            return null;
        }

        #endregion

        #region Draw — 深度检查 + 六层绘制

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            // 关键修复：深度检查防止同一帧重复绘制导致闪烁
            if (maxDepth >= 0 && minDepth < 0 && intensity > 0.01f) {
                DrawStormBackground(spriteBatch);
                DrawStormClouds(spriteBatch);
                DrawLightningBolts(spriteBatch);
                DrawStormMist(spriteBatch);
                DrawElectricSparks(spriteBatch);
                DrawVignette(spriteBatch);
            }
        }

        #endregion

        #region 层1 — 墨紫风暴渐变底色

        private void DrawStormBackground(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle screenRect = new(0, 0, Main.screenWidth, Main.screenHeight);

            // 底层铺满 AbyssPurple (20,10,40)
            sb.Draw(pixel, screenRect, AoshunHelper.AbyssPurple * intensity * 0.85f);

            // 10段梯度渐变：顶部 StormGray → 底部 AbyssPurple
            int bands = 10;
            for (int i = 0; i < bands; i++) {
                float t = (float)i / bands;
                int bandH = Main.screenHeight / bands;
                Rectangle bandRect = new(0, i * bandH, Main.screenWidth, bandH);

                Color bandColor = Color.Lerp(AoshunHelper.StormGray, AoshunHelper.AbyssPurple, t) * intensity * 0.5f;
                sb.Draw(pixel, bandRect, bandColor);
            }

            // 缓慢呼吸叠加（确定性正弦，不随机）
            float breathAlpha = (0.5f + MathF.Sin(globalTime * 1.2f) * 0.5f) * intensity * 0.08f;
            if (isPhase2) breathAlpha *= 1.6f;
            Color breathColor = AoshunHelper.ThunderPurple * breathAlpha;
            breathColor.A = 0;
            sb.Draw(pixel, screenRect, breathColor);
        }

        #endregion

        #region 层2 — Smoke帧动画雷云

        private void DrawStormClouds(SpriteBatch sb) {
            Texture2D smokeTex = ACMAsset.Smoke;
            if (smokeTex == null) return;

            int frameSize = smokeTex.Width / 4;  // 4×4 = 16帧
            Vector2 origin = new(frameSize / 2f);

            for (int i = 0; i < StormCloudCount; i++) {
                StormCloud cloud = stormClouds[i];
                if (!cloud.IsActive) continue;

                Vector2 drawPos = cloud.Position - Main.screenPosition;

                // 暗紫 / 灰色交替，间歇带雷蓝
                float colorLerp = MathF.Sin(globalTime * 0.6f + i * 0.4f) * 0.5f + 0.5f;
                Color cloudColor = Color.Lerp(new Color(40, 30, 55), AoshunHelper.StormGray, colorLerp);
                if (i % 5 == 0)
                    cloudColor = Color.Lerp(cloudColor, AoshunHelper.NorthSeaCyan, 0.15f);

                float alpha = MathF.Sin(cloud.AnimProgress * MathHelper.Pi) * intensity * 0.55f;
                cloudColor *= alpha;
                cloudColor.A = 0;

                // 帧选择
                int fx = i % 4;
                int fy = (i / 4) % 4;
                Rectangle srcRect = new(fx * frameSize, fy * frameSize, frameSize, frameSize);

                sb.Draw(smokeTex, drawPos, srcRect, cloudColor, cloud.Rotation, origin, cloud.Scale, SpriteEffects.None, 0f);

                // 外层光晕 — 雷光映射在云底
                Color glowColor = Color.Lerp(AoshunHelper.ThunderPurple, AoshunHelper.LightningBlue, colorLerp) * alpha * 0.2f;
                glowColor.A = 0;
                sb.Draw(smokeTex, drawPos, srcRect, glowColor, cloud.Rotation * 0.85f, origin, cloud.Scale * 1.35f, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层3 — 闪电分叉 + 电弧

        private void DrawLightningBolts(SpriteBatch sb) {
            // --- LightningBranch 分叉闪电 ---
            Texture2D branchTex = ACMAsset.LightningBranch;
            if (branchTex != null) {
                Vector2 branchOrigin = new(branchTex.Width / 2f, branchTex.Height * 0.15f);

                for (int i = 0; i < LightningBoltCount; i++) {
                    LightningBolt bolt = lightningBolts[i];
                    if (bolt.FlashAlpha <= 0.01f) continue;

                    float alpha = bolt.FlashAlpha * intensity;

                    // 主闪电 — 蓝白色，快亮慢灭
                    Color boltColor = Color.Lerp(AoshunHelper.LightningBlue, AoshunHelper.ElectricWhite, bolt.FlashAlpha) * alpha;
                    boltColor.A = 0;

                    Vector2 drawPos = new(bolt.ScreenX, 0f);
                    float scaleX = bolt.Scale * 0.5f;
                    float scaleY = bolt.Scale * 0.75f;
                    SpriteEffects flip = bolt.Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                    sb.Draw(branchTex, drawPos, null, boltColor, 0f, branchOrigin, new Vector2(scaleX, scaleY), flip, 0f);

                    // 外围辉光 — ThunderPurple
                    Color glowColor = AoshunHelper.ThunderPurple * alpha * 0.35f;
                    glowColor.A = 0;
                    sb.Draw(branchTex, drawPos, null, glowColor, 0f, branchOrigin, new Vector2(scaleX * 1.3f, scaleY * 1.1f), flip, 0f);
                }
            }

            // --- ElectricArcSheet 电弧 (垂直4组) ---
            Texture2D arcTex = ACMAsset.ElectricArcSheet;
            if (arcTex != null) {
                int arcFrames = 4;
                int arcFrameH = arcTex.Height / arcFrames;
                Vector2 arcOrigin = new(arcTex.Width / 2f, arcFrameH / 2f);

                for (int i = 0; i < LightningBoltCount; i++) {
                    LightningBolt bolt = lightningBolts[i];
                    if (bolt.FlashAlpha <= 0.05f) continue;

                    int frame = ((int)(globalTime * 12f) + i * 3) % arcFrames;
                    Rectangle arcRect = new(0, frame * arcFrameH, arcTex.Width, arcFrameH);

                    float alpha = bolt.FlashAlpha * intensity * 0.45f;
                    Color arcColor = AoshunHelper.LightningBlue * alpha;
                    arcColor.A = 0;

                    // 电弧位于闪电下方延伸
                    Vector2 arcPos = new(
                        bolt.ScreenX + MathF.Sin(globalTime * 3f + i) * 30f,
                        Main.screenHeight * 0.28f
                    );
                    float arcScale = 0.35f * bolt.Scale;

                    sb.Draw(arcTex, arcPos, arcRect, arcColor, MathHelper.PiOver2, arcOrigin,
                        new Vector2(arcScale, arcScale * 0.4f), SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        #region 层4 — GlaciateWave 风暴迷雾

        private void DrawStormMist(SpriteBatch sb) {
            Texture2D mistTex = ACMAsset.GlaciateWave;
            if (mistTex == null) return;

            Vector2 mistOrigin = new(mistTex.Width / 2f, mistTex.Height / 2f);

            for (int layer = 0; layer < MistLayerCount; layer++) {
                float alpha = (0.12f - layer * 0.03f) * intensity;
                if (isPhase2) alpha *= 1.3f;

                Color mistColor = Color.Lerp(AoshunHelper.StormGray, AoshunHelper.AbyssPurple, layer / (float)MistLayerCount) * alpha;
                mistColor.A = 0;

                for (int band = 0; band < 2; band++) {
                    float xOff = mistOffsets[layer] * 80f + band * 600f;
                    float yOff = MathF.Sin(globalTime * 0.4f + layer + band * 2f) * 40f;

                    Vector2 pos = new(
                        (xOff % (Main.screenWidth + 600)) - 300,
                        Main.screenHeight * (0.15f + band * 0.35f + layer * 0.1f) + yOff
                    );

                    float rot = MathF.Sin(globalTime * 0.25f + layer) * 0.08f;
                    Vector2 drawScale = new(
                        Main.screenWidth * 0.9f / mistTex.Width * (1.2f + layer * 0.3f),
                        0.25f * (1f + layer * 0.2f)
                    );

                    sb.Draw(mistTex, pos, null, mistColor, rot, mistOrigin, drawScale, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        #region 层5 — 电火花粒子

        private void DrawElectricSparks(SpriteBatch sb) {
            Texture2D sparkTex = ACMAsset.Sparkle ?? ACMAsset.BlankStar;
            if (sparkTex == null) return;

            Vector2 origin = sparkTex.Size() / 2f;

            for (int i = 0; i < SparkCount; i++) {
                ElectricSpark spark = sparks[i];
                if (!spark.IsActive) continue;

                Vector2 drawPos = spark.Position - Main.screenPosition;

                float progress = MathF.Sin(spark.AnimProgress * MathHelper.Pi);
                float alpha = progress * intensity * 0.55f;

                Color sparkColor = Color.Lerp(AoshunHelper.LightningBlue, AoshunHelper.ElectricWhite, progress) * alpha;
                sparkColor.A = 0;

                float scale = spark.Scale * (0.08f + progress * 0.12f);

                sb.Draw(sparkTex, drawPos, null, sparkColor, globalTime * 5f + i, origin, scale, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层6 — 四角暗角 + 顶部雷映

        private void DrawVignette(SpriteBatch sb) {
            // --- 四角暗影（SoftGlow 圆形灰度图） ---
            Texture2D glowTex = ACMAsset.SoftGlow;
            if (glowTex != null) {
                Vector2 glowOrigin = glowTex.Size() / 2f;
                float vigAlpha = intensity * 0.5f;
                if (isPhase2) vigAlpha *= 1.2f;
                Color vigColor = AoshunHelper.AbyssPurple with { A = 0 } * vigAlpha;

                float cornerSize = MathF.Min(Main.screenWidth, Main.screenHeight) * 0.5f;
                float cornerScale = cornerSize / glowTex.Width;

                sb.Draw(glowTex, new Vector2(0, 0), null, vigColor, 0f, glowOrigin, cornerScale, SpriteEffects.None, 0f);
                sb.Draw(glowTex, new Vector2(Main.screenWidth, 0), null, vigColor, 0f, glowOrigin, cornerScale, SpriteEffects.None, 0f);
                sb.Draw(glowTex, new Vector2(0, Main.screenHeight), null, vigColor, 0f, glowOrigin, cornerScale, SpriteEffects.None, 0f);
                sb.Draw(glowTex, new Vector2(Main.screenWidth, Main.screenHeight), null, vigColor, 0f, glowOrigin, cornerScale, SpriteEffects.None, 0f);
            }

            // --- 顶部雷映脉冲 ---
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float topPulse = (MathF.Sin(globalTime * 1.8f) * 0.5f + 0.5f) * intensity * 0.1f;
            if (isPhase2) topPulse *= 1.5f;

            Color topColor = Color.Lerp(AoshunHelper.ThunderPurple, AoshunHelper.LightningBlue,
                MathF.Sin(globalTime * 0.8f) * 0.5f + 0.5f) * topPulse;
            topColor.A = 0;

            int topH = Main.screenHeight / 5;
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, topH), topColor);
        }

        #endregion

        #region 地表着色 / 云量

        public override Color OnTileColor(Color inColor) {
            Color stormTint = Color.Lerp(Color.White, new Color(50, 40, 70), intensity * 0.35f);
            return new Color(
                (int)(inColor.R * stormTint.R / 255f),
                (int)(inColor.G * stormTint.G / 255f),
                (int)(inColor.B * stormTint.B / 255f),
                inColor.A
            );
        }

        public override float GetCloudAlpha() => 1f - intensity * 0.75f;

        #endregion

        // =====================================================================
        //  内部粒子类
        // =====================================================================

        #region StormCloud — 雷暴云

        private class StormCloud
        {
            public Vector2 Position;
            public float Scale;
            public float Rotation;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public StormCloud() { Reset(); }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(5, 45);
            }

            public void Update(float stormMul) {
                if (!IsActive) {
                    cooldown--;
                    if (cooldown <= 0) Activate(stormMul);
                    return;
                }

                AnimProgress += AnimSpeed * stormMul;
                Position += Velocity * stormMul;
                Rotation += 0.0006f * stormMul;

                if (AnimProgress >= 1f) Reset();
            }

            private void Activate(float stormMul) {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.0012f, 0.004f);

                // 云集中在屏幕上 70% 区域
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-500, Main.screenWidth + 500),
                    Main.screenPosition.Y + Main.rand.Next(-300, (int)(Main.screenHeight * 0.7f))
                );

                // 带有向右偏的整体风向
                Velocity = new Vector2(
                    Main.rand.NextFloat(0.1f, 0.8f) * stormMul,
                    Main.rand.NextFloat(-0.2f, 0.2f)
                );
                Scale = Main.rand.NextFloat(2.5f, 5.5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        #endregion

        #region LightningBolt — 确定性闪电

        private class LightningBolt
        {
            public float ScreenX;
            public float FlashAlpha;
            public float Scale;
            public bool Flip;

            private readonly int index;
            private float timer;
            private readonly float baseCyclePeriod;
            private readonly float flashDuration;
            private bool flashing;

            public LightningBolt(int i) {
                index = i;
                // 每道闪电的周期错开，避免齐闪
                baseCyclePeriod = 2.5f + i * 1.1f;
                flashDuration = 0.18f + i * 0.04f;
                Reset();
            }

            public void Reset() {
                FlashAlpha = 0f;
                timer = 0f;
                flashing = false;
                ScreenX = Main.screenWidth * (0.1f + index * 0.2f);
                Scale = 0.7f + index * 0.12f;
                Flip = index % 2 == 0;
            }

            public void Update(float globalTime, bool phase2) {
                timer += 1f / 60f;

                float cyclePeriod = phase2 ? baseCyclePeriod * 0.55f : baseCyclePeriod;
                float cyclePos = timer % cyclePeriod;

                if (cyclePos < flashDuration && !flashing) {
                    flashing = true;
                    // 每次闪电位置通过正弦确定性微调
                    ScreenX = Main.screenWidth * (0.1f + index * 0.2f)
                            + MathF.Sin(globalTime * 0.7f + index * 2f) * (Main.screenWidth * 0.08f);
                    Flip = ((int)(timer / cyclePeriod) + index) % 2 == 0;
                }

                if (flashing) {
                    if (cyclePos < flashDuration) {
                        float p = cyclePos / flashDuration;
                        // 快亮（前30%）慢灭（后70%）
                        FlashAlpha = p < 0.3f
                            ? p / 0.3f
                            : 1f - (p - 0.3f) / 0.7f;
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

        #endregion

        #region ElectricSpark — 电火花

        private class ElectricSpark
        {
            public Vector2 Position;
            public float Scale;
            public float AnimProgress;
            public float AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public ElectricSpark() { Reset(); }

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(20, 120);
            }

            public void Update() {
                if (!IsActive) {
                    cooldown--;
                    if (cooldown <= 0) Activate();
                    return;
                }

                AnimProgress += AnimSpeed;
                Position += Velocity;
                Velocity.Y += 0.02f; // 微微下坠

                if (AnimProgress >= 1f) Reset();
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.008f, 0.02f);

                // 从雷云区域（上半屏）飞出
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(50, Main.screenWidth - 50),
                    Main.screenPosition.Y + Main.rand.Next(-50, (int)(Main.screenHeight * 0.45f))
                );
                Velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-0.5f, 1.5f));
                Scale = Main.rand.NextFloat(0.8f, 2f);
            }
        }

        #endregion
    }
}
