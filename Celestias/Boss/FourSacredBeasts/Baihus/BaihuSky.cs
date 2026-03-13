using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Baihus
{
    /// <summary>
    /// 白虎场景效果控制器 — 自动检测Boss存在并管理天空激活
    /// </summary>
    internal class BaihuSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(ModContent.NPCType<Baihu>());
        public override void SpecialVisuals(Player player, bool isActive) {
            if (player.Alives()) {
                player.ManageSpecialBiomeVisuals(BaihuSky.SkyName, isActive);
            }
        }
    }

    /// <summary>
    /// 白虎天空效果 — 金虎裂空天幕
    /// 
    /// 多层绘制结构：
    ///  1. 暗金/铁灰渐变底色
    ///  2. Smoke风沙尘云 — 金色/铁灰交替
    ///  3. SlashBurst金属裂痕闪光
    ///  4. EmberShards金属碎片飘落
    ///  5. Sparkle金色火花粒子
    ///  6. 暗角 + 金色脉冲
    /// </summary>
    internal class BaihuSky : CustomSky, IACMLoader
    {
        public const string SkyName = "ACM:BaihuSky";

        private bool active;
        private float intensity;
        private float globalTime;

        private const float MaxIntensity = 1f;
        private const float FadeInSpeed = 0.014f;
        private const float FadeOutSpeed = 0.02f;

        private float bossHealthPercent = 1f;
        private bool isPhase2;
        private bool isPhase3;

        // 颜色定义 — 白虎：金白 / 铁灰 / 血金
        private static readonly Color DarkIron = new(18, 16, 14);
        private static readonly Color MetalGray = new(60, 55, 50);
        private static readonly Color GoldWhite = new(220, 200, 140);
        private static readonly Color BloodGold = new(180, 120, 40);
        private static readonly Color SteelFlash = new(240, 240, 220);

        // 风沙云
        private const int DustCloudCount = 40;
        private readonly DustCloud[] dustClouds = new DustCloud[DustCloudCount];

        // 金属裂痕
        private const int SlashCount = 3;
        private readonly MetalSlash[] slashes = new MetalSlash[SlashCount];

        // 碎片粒子
        private const int ShardCount = 18;
        private readonly MetalShard[] shards = new MetalShard[ShardCount];

        // 火花粒子
        private const int SparkCount = 15;
        private readonly GoldSpark[] sparks = new GoldSpark[SparkCount];

        #region IACMLoader 注册

        void IACMLoader.LoadData() {
            SkyManager.Instance[SkyName] = this;
            Filters.Scene[SkyName] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.12f, 0.1f, 0.06f)
                .UseOpacity(0.35f), EffectPriority.High);

            for (int i = 0; i < DustCloudCount; i++) dustClouds[i] = new DustCloud();
            for (int i = 0; i < SlashCount; i++) slashes[i] = new MetalSlash(i);
            for (int i = 0; i < ShardCount; i++) shards[i] = new MetalShard();
            for (int i = 0; i < SparkCount; i++) sparks[i] = new GoldSpark();
        }

        #endregion

        #region CustomSky 生命周期

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            bossHealthPercent = 1f;
            isPhase2 = false;
            isPhase3 = false;

            for (int i = 0; i < DustCloudCount; i++) dustClouds[i].Reset();
            for (int i = 0; i < SlashCount; i++) slashes[i].Reset();
            for (int i = 0; i < ShardCount; i++) shards[i].Reset();
            for (int i = 0; i < SparkCount; i++) sparks[i].Reset();
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
                isPhase2 = bossHealthPercent < Baihu.Phase2Threshold;
                isPhase3 = bossHealthPercent < Baihu.Phase3Threshold;

                float target = isPhase3 ? MaxIntensity * 1.25f : isPhase2 ? MaxIntensity * 1.1f : MaxIntensity;
                intensity = MathHelper.Lerp(intensity, target, FadeInSpeed);
            }
            else {
                intensity -= FadeOutSpeed;
                if (intensity <= 0f) { intensity = 0f; if (active) Deactivate(); }
            }

            float stormMul = isPhase3 ? 1.5f : isPhase2 ? 1.2f : 1f;
            for (int i = 0; i < DustCloudCount; i++) dustClouds[i].Update(stormMul);
            for (int i = 0; i < SlashCount; i++) slashes[i].Update(globalTime, isPhase2 || isPhase3);
            for (int i = 0; i < ShardCount; i++) shards[i].Update();
            for (int i = 0; i < SparkCount; i++) sparks[i].Update();
        }

        private static NPC FindBoss() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<Baihu>() && npc.active) return npc;
            }
            return null;
        }

        #endregion

        #region Draw

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (maxDepth >= 0 && minDepth < 0 && intensity > 0.01f) {
                DrawBackground(spriteBatch);
                DrawDustClouds(spriteBatch);
                DrawSlashes(spriteBatch);
                DrawShards(spriteBatch);
                DrawSparks(spriteBatch);
                DrawVignette(spriteBatch);
            }
        }

        #endregion

        #region 层1 — 暗金铁灰底色

        private void DrawBackground(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle screen = new(0, 0, Main.screenWidth, Main.screenHeight);

            sb.Draw(pixel, screen, DarkIron * intensity * 0.92f);

            int bands = 10;
            for (int i = 0; i < bands; i++) {
                float t = (float)i / bands;
                int h = Main.screenHeight / bands;
                Color c = Color.Lerp(MetalGray, DarkIron, t) * intensity * 0.4f;
                sb.Draw(pixel, new Rectangle(0, i * h, Main.screenWidth, h), c);
            }

            // 金属光泽脉冲
            float breath = (0.5f + MathF.Sin(globalTime * 1.0f) * 0.5f) * intensity * 0.05f;
            if (isPhase3) breath *= 2.2f;
            else if (isPhase2) breath *= 1.5f;
            Color breathC = BloodGold * breath;
            breathC.A = 0;
            sb.Draw(pixel, screen, breathC);
        }

        #endregion

        #region 层2 — 风沙尘云

        private void DrawDustClouds(SpriteBatch sb) {
            Texture2D tex = ACMAsset.Smoke;
            if (tex == null) return;
            int fs = tex.Width / 4;
            Vector2 origin = new(fs / 2f);

            for (int i = 0; i < DustCloudCount; i++) {
                DustCloud c = dustClouds[i];
                if (!c.IsActive) continue;

                Vector2 dp = c.Position - Main.screenPosition;
                float lerp = MathF.Sin(globalTime * 0.4f + i * 0.35f) * 0.5f + 0.5f;
                Color cc = Color.Lerp(new Color(40, 35, 25), MetalGray, lerp);
                if (i % 5 == 0) cc = Color.Lerp(cc, GoldWhite, 0.1f);

                float alpha = MathF.Sin(c.AnimProgress * MathHelper.Pi) * intensity * 0.45f;
                cc *= alpha;
                cc.A = 0;

                Rectangle src = new((i % 4) * fs, (i / 4 % 4) * fs, fs, fs);
                sb.Draw(tex, dp, src, cc, c.Rotation, origin, c.Scale, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层3 — 金属裂痕 (SlashBurst)

        private void DrawSlashes(SpriteBatch sb) {
            Texture2D tex = ACMAsset.SlashBurst;
            if (tex == null) return;
            Vector2 origin = new(tex.Width / 2f, tex.Height * 0.8f);

            for (int i = 0; i < SlashCount; i++) {
                MetalSlash s = slashes[i];
                if (s.FlashAlpha <= 0.01f) continue;

                float alpha = s.FlashAlpha * intensity;

                Color slashC = Color.Lerp(GoldWhite, SteelFlash, s.FlashAlpha) * alpha;
                slashC.A = 0;

                Vector2 dp = new(s.ScreenX, s.ScreenY);
                SpriteEffects flip = s.Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                sb.Draw(tex, dp, null, slashC, s.Rotation, origin, new Vector2(0.3f * s.Scale, 0.5f * s.Scale), flip, 0f);

                Color glowC = BloodGold * alpha * 0.3f;
                glowC.A = 0;
                sb.Draw(tex, dp, null, glowC, s.Rotation, origin, new Vector2(0.4f * s.Scale, 0.6f * s.Scale), flip, 0f);
            }
        }

        #endregion

        #region 层4 — 金属碎片 (EmberShards)

        private void DrawShards(SpriteBatch sb) {
            Texture2D tex = ACMAsset.EmberShards;
            if (tex == null) return;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < ShardCount; i++) {
                MetalShard s = shards[i];
                if (!s.IsActive) continue;

                Vector2 dp = s.Position - Main.screenPosition;
                float progress = MathF.Sin(s.AnimProgress * MathHelper.Pi);
                float alpha = progress * intensity * 0.35f;

                Color sc = Color.Lerp(GoldWhite, BloodGold, s.AnimProgress) * alpha;
                sc.A = 0;
                float scale = 0.04f + progress * 0.06f;
                sb.Draw(tex, dp, null, sc, s.Rotation, origin, scale, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层5 — 金色火花

        private void DrawSparks(SpriteBatch sb) {
            Texture2D tex = ACMAsset.Sparkle ?? ACMAsset.BlankStar;
            if (tex == null) return;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < SparkCount; i++) {
                GoldSpark s = sparks[i];
                if (!s.IsActive) continue;

                Vector2 dp = s.Position - Main.screenPosition;
                float progress = MathF.Sin(s.AnimProgress * MathHelper.Pi);
                float alpha = progress * intensity * 0.45f;

                Color sc = Color.Lerp(GoldWhite, SteelFlash, progress) * alpha;
                sc.A = 0;
                float scale = s.Scale * (0.05f + progress * 0.08f);
                sb.Draw(tex, dp, null, sc, globalTime * 3f + i, origin, scale, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层6 — 暗角 + 金色脉冲

        private void DrawVignette(SpriteBatch sb) {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Vector2 go = glow.Size() / 2f;
                float va = intensity * 0.5f;
                if (isPhase3) va *= 1.3f;
                Color vc = DarkIron with { A = 0 } * va;
                float cs = MathF.Min(Main.screenWidth, Main.screenHeight) * 0.5f / glow.Width;

                sb.Draw(glow, new Vector2(0, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(0, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            float pulse = (MathF.Sin(globalTime * 1.5f) * 0.5f + 0.5f) * intensity * 0.06f;
            if (isPhase3) pulse *= 2f;
            else if (isPhase2) pulse *= 1.4f;

            Color topC = Color.Lerp(BloodGold, GoldWhite, MathF.Sin(globalTime * 0.7f) * 0.5f + 0.5f) * pulse;
            topC.A = 0;
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight / 5), topC);
        }

        #endregion

        #region 地表着色

        public override Color OnTileColor(Color inColor) {
            Color tint = Color.Lerp(Color.White, new Color(70, 60, 45), intensity * 0.3f);
            return new Color(
                (int)(inColor.R * tint.R / 255f),
                (int)(inColor.G * tint.G / 255f),
                (int)(inColor.B * tint.B / 255f),
                inColor.A
            );
        }

        public override float GetCloudAlpha() => 1f - intensity * 0.65f;

        #endregion

        // ================================================================
        // 内部粒子类
        // ================================================================

        private class DustCloud
        {
            public Vector2 Position;
            public float Scale, Rotation, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(5, 35);
            }

            public void Update(float mul) {
                if (!IsActive) {
                    if (--cooldown <= 0) Activate(mul);
                    return;
                }
                AnimProgress += AnimSpeed * mul;
                Position += Velocity * mul;
                Rotation += 0.0004f * mul;
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate(float mul) {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.0012f, 0.004f);
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-400, Main.screenWidth + 400),
                    Main.screenPosition.Y + Main.rand.Next(-200, (int)(Main.screenHeight * 0.7f))
                );
                Velocity = new Vector2(Main.rand.NextFloat(0.3f, 1.2f) * mul, Main.rand.NextFloat(-0.1f, 0.15f));
                Scale = Main.rand.NextFloat(2f, 4.5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        private class MetalSlash
        {
            public float ScreenX, ScreenY, FlashAlpha, Scale, Rotation;
            public bool Flip;
            private readonly int index;
            private float timer;
            private readonly float basePeriod, flashDuration;
            private bool flashing;

            public MetalSlash(int i) {
                index = i;
                basePeriod = 3.5f + i * 1.5f;
                flashDuration = 0.22f + i * 0.05f;
                Reset();
            }

            public void Reset() {
                FlashAlpha = 0f;
                timer = 0f;
                flashing = false;
                ScreenX = Main.screenWidth * (0.2f + index * 0.3f);
                ScreenY = Main.screenHeight * 0.3f;
                Scale = 0.7f + index * 0.15f;
                Rotation = -0.3f + index * 0.3f;
                Flip = index % 2 == 0;
            }

            public void Update(float gTime, bool intense) {
                timer += 1f / 60f;
                float period = intense ? basePeriod * 0.6f : basePeriod;
                float pos = timer % period;

                if (pos < flashDuration && !flashing) {
                    flashing = true;
                    ScreenX = Main.screenWidth * (0.15f + index * 0.3f)
                            + MathF.Sin(gTime * 0.5f + index * 3f) * (Main.screenWidth * 0.1f);
                    ScreenY = Main.screenHeight * (0.2f + MathF.Sin(gTime * 0.3f + index) * 0.15f);
                    Rotation = MathF.Sin(gTime + index) * 0.5f;
                    Flip = ((int)(timer / period) + index) % 2 == 0;
                }

                if (flashing) {
                    if (pos < flashDuration) {
                        float p = pos / flashDuration;
                        FlashAlpha = p < 0.2f ? p / 0.2f : 1f - (p - 0.2f) / 0.8f;
                        FlashAlpha = MathHelper.Clamp(FlashAlpha, 0f, 1f);
                    }
                    else {
                        flashing = false;
                        FlashAlpha = 0f;
                    }
                }
                else {
                    FlashAlpha = MathHelper.Lerp(FlashAlpha, 0f, 0.12f);
                }
            }
        }

        private class MetalShard
        {
            public Vector2 Position;
            public float Rotation, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(20, 90);
            }

            public void Update() {
                if (!IsActive) {
                    if (--cooldown <= 0) Activate();
                    return;
                }
                AnimProgress += AnimSpeed;
                Position += Velocity;
                Velocity.Y += 0.025f;
                Rotation += 0.03f;
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.005f, 0.015f);
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(0, Main.screenWidth),
                    Main.screenPosition.Y + Main.rand.Next(-100, (int)(Main.screenHeight * 0.3f))
                );
                Velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.5f, 2f));
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        private class GoldSpark
        {
            public Vector2 Position;
            public float Scale, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(15, 80);
            }

            public void Update() {
                if (!IsActive) {
                    if (--cooldown <= 0) Activate();
                    return;
                }
                AnimProgress += AnimSpeed;
                Position += Velocity;
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.008f, 0.02f);
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(30, Main.screenWidth - 30),
                    Main.screenPosition.Y + Main.rand.Next(0, Main.screenHeight)
                );
                Velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.5f, 0.5f));
                Scale = Main.rand.NextFloat(0.6f, 1.5f);
            }
        }
    }
}
