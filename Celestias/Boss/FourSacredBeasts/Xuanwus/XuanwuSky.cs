using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Xuanwus
{
    /// <summary>
    /// 玄武场景效果控制器 — 自动检测Boss存在并管理天空激活
    /// </summary>
    internal class XuanwuSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(ModContent.NPCType<Xuanwu>());
        public override void SpecialVisuals(Player player, bool isActive) {
            if (player.Alives()) {
                player.ManageSpecialBiomeVisuals(XuanwuSky.SkyName, isActive);
            }
        }
    }

    /// <summary>
    /// 玄武天空效果 — 玄冰寒潮天幕
    /// 
    /// 多层绘制结构：
    ///  1. 深蓝/墨黑渐变底色 — 极寒夜空
    ///  2. Smoke帧动画冰雾云 — 深蓝/青灰交替
    ///  3. GlaciateWave冰霜雾层横漂
    ///  4. BlankStar冰晶/雪花粒子
    ///  5. Sparkle冰霜火花
    ///  6. 暗角 + 冰蓝脉冲
    /// 
    /// 三阶段绝对防御时天空趋向极黑冰蓝
    /// </summary>
    internal class XuanwuSky : CustomSky, IACMLoader
    {
        public const string SkyName = "ACM:XuanwuSky";

        private bool active;
        private float intensity;
        private float globalTime;

        private const float MaxIntensity = 1f;
        private const float FadeInSpeed = 0.01f;
        private const float FadeOutSpeed = 0.016f;

        private float bossHealthPercent = 1f;
        private bool isPhase2;
        private bool isPhase3;

        // 颜色定义 — 玄武：玄黑 / 寒蓝 / 冰白
        private static readonly Color AbyssBlack = new(5, 8, 15);
        private static readonly Color DeepOcean = new(10, 25, 50);
        private static readonly Color FrostBlue = new(60, 140, 200);
        private static readonly Color IceCyan = new(120, 220, 240);
        private static readonly Color FrostWhite = new(210, 235, 255);

        // 冰雾云
        private const int IceCloudCount = 45;
        private readonly IceCloud[] iceClouds = new IceCloud[IceCloudCount];

        // 冰晶粒子
        private const int CrystalCount = 25;
        private readonly IceCrystal[] crystals = new IceCrystal[CrystalCount];

        // 冰霜火花
        private const int FrostSparkCount = 15;
        private readonly FrostSpark[] frostSparks = new FrostSpark[FrostSparkCount];

        // 冰霜雾层
        private const int FrostLayerCount = 4;
        private readonly float[] frostOffsets = new float[FrostLayerCount];
        private static readonly float[] FrostSpeeds = [0.008f, 0.005f, 0.012f, 0.007f];

        #region IACMLoader 注册

        void IACMLoader.LoadData() {
            SkyManager.Instance[SkyName] = this;
            Filters.Scene[SkyName] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.04f, 0.06f, 0.12f)
                .UseOpacity(0.4f), EffectPriority.High);

            for (int i = 0; i < IceCloudCount; i++) iceClouds[i] = new IceCloud();
            for (int i = 0; i < CrystalCount; i++) crystals[i] = new IceCrystal();
            for (int i = 0; i < FrostSparkCount; i++) frostSparks[i] = new FrostSpark();
        }

        #endregion

        #region CustomSky 生命周期

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            bossHealthPercent = 1f;
            isPhase2 = false;
            isPhase3 = false;

            for (int i = 0; i < IceCloudCount; i++) iceClouds[i].Reset();
            for (int i = 0; i < CrystalCount; i++) crystals[i].Reset();
            for (int i = 0; i < FrostSparkCount; i++) frostSparks[i].Reset();
            for (int i = 0; i < FrostLayerCount; i++) frostOffsets[i] = i * 250f;
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
                isPhase2 = bossHealthPercent < Xuanwu.Phase2Threshold;
                isPhase3 = bossHealthPercent < Xuanwu.Phase3Threshold;

                float target = isPhase3 ? MaxIntensity * 1.25f : isPhase2 ? MaxIntensity * 1.1f : MaxIntensity;
                intensity = MathHelper.Lerp(intensity, target, FadeInSpeed);
            } else {
                intensity -= FadeOutSpeed;
                if (intensity <= 0f) { intensity = 0f; if (active) Deactivate(); }
            }

            for (int i = 0; i < FrostLayerCount; i++) frostOffsets[i] += FrostSpeeds[i];
            float stormMul = isPhase3 ? 1.5f : isPhase2 ? 1.25f : 1f;
            for (int i = 0; i < IceCloudCount; i++) iceClouds[i].Update(stormMul);
            for (int i = 0; i < CrystalCount; i++) crystals[i].Update();
            for (int i = 0; i < FrostSparkCount; i++) frostSparks[i].Update();
        }

        private static NPC FindBoss() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<Xuanwu>() && npc.active) return npc;
            }
            return null;
        }

        #endregion

        #region Draw

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (maxDepth >= 0 && minDepth < 0 && intensity > 0.01f) {
                DrawBackground(spriteBatch);
                DrawIceClouds(spriteBatch);
                DrawFrostMist(spriteBatch);
                DrawCrystals(spriteBatch);
                DrawFrostSparks(spriteBatch);
                DrawVignette(spriteBatch);
            }
        }

        #endregion

        #region 层1 — 玄黑深蓝底色

        private void DrawBackground(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle screen = new(0, 0, Main.screenWidth, Main.screenHeight);

            sb.Draw(pixel, screen, AbyssBlack * intensity * 0.95f);

            int bands = 12;
            for (int i = 0; i < bands; i++) {
                float t = (float)i / bands;
                int h = Main.screenHeight / bands;
                // 顶部深蓝区域（天空映射），底部纯黑（深渊）
                Color c = Color.Lerp(DeepOcean, AbyssBlack, t) * intensity * 0.4f;
                sb.Draw(pixel, new Rectangle(0, i * h, Main.screenWidth, h), c);
            }

            // 寒潮呼吸脉冲 — 缓慢而沉重
            float breath = (0.5f + MathF.Sin(globalTime * 0.8f) * 0.5f) * intensity * 0.05f;
            if (isPhase3) breath *= 2f;
            else if (isPhase2) breath *= 1.4f;
            Color breathC = FrostBlue * breath;
            breathC.A = 0;
            sb.Draw(pixel, screen, breathC);
        }

        #endregion

        #region 层2 — 冰雾云

        private void DrawIceClouds(SpriteBatch sb) {
            Texture2D tex = ACMAsset.Smoke;
            if (tex == null) return;
            int fs = tex.Width / 4;
            Vector2 origin = new(fs / 2f);

            for (int i = 0; i < IceCloudCount; i++) {
                IceCloud c = iceClouds[i];
                if (!c.IsActive) continue;

                Vector2 dp = c.Position - Main.screenPosition;
                float lerp = MathF.Sin(globalTime * 0.35f + i * 0.28f) * 0.5f + 0.5f;
                Color cc = Color.Lerp(new Color(12, 20, 40), new Color(25, 40, 60), lerp);
                if (i % 7 == 0) cc = Color.Lerp(cc, FrostBlue, 0.1f);

                float alpha = MathF.Sin(c.AnimProgress * MathHelper.Pi) * intensity * 0.45f;
                cc *= alpha;
                cc.A = 0;

                Rectangle src = new((i % 4) * fs, (i / 4 % 4) * fs, fs, fs);
                sb.Draw(tex, dp, src, cc, c.Rotation, origin, c.Scale, SpriteEffects.None, 0f);

                // 冰光底映
                Color glow = Color.Lerp(DeepOcean, FrostBlue, lerp) * alpha * 0.15f;
                glow.A = 0;
                sb.Draw(tex, dp, src, glow, c.Rotation * 0.9f, origin, c.Scale * 1.2f, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层3 — 冰霜雾层 (GlaciateWave)

        private void DrawFrostMist(SpriteBatch sb) {
            Texture2D tex = ACMAsset.GlaciateWave;
            if (tex == null) return;
            Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);

            for (int layer = 0; layer < FrostLayerCount; layer++) {
                float alpha = (0.1f - layer * 0.02f) * intensity;
                if (isPhase3) alpha *= 1.5f;
                else if (isPhase2) alpha *= 1.2f;

                Color mc = Color.Lerp(FrostBlue, DeepOcean, layer / (float)FrostLayerCount) * alpha;
                mc.A = 0;

                for (int band = 0; band < 2; band++) {
                    // 冰层移动缓慢，有厚重感
                    float xOff = frostOffsets[layer] * 50f + band * 600f;
                    float yOff = MathF.Sin(globalTime * 0.25f + layer + band * 1.8f) * 20f;
                    Vector2 pos = new(
                        (xOff % (Main.screenWidth + 700)) - 350,
                        Main.screenHeight * (0.1f + band * 0.4f + layer * 0.08f) + yOff
                    );
                    float rot = MathF.Sin(globalTime * 0.15f + layer) * 0.05f;
                    Vector2 scale = new(
                        Main.screenWidth * 0.9f / tex.Width * (1.2f + layer * 0.2f),
                        0.25f * (1f + layer * 0.15f)
                    );
                    sb.Draw(tex, pos, null, mc, rot, origin, scale, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        #region 层4 — 冰晶/雪花飘落

        private void DrawCrystals(SpriteBatch sb) {
            Texture2D tex = ACMAsset.BlankStar;
            if (tex == null) return;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < CrystalCount; i++) {
                IceCrystal c = crystals[i];
                if (!c.IsActive) continue;

                Vector2 dp = c.Position - Main.screenPosition;
                float progress = MathF.Sin(c.AnimProgress * MathHelper.Pi);
                float alpha = progress * intensity * 0.4f;

                // 冰蓝 → 冰白渐变
                Color cc = Color.Lerp(IceCyan, FrostWhite, progress) * alpha;
                cc.A = 0;
                float scale = c.Scale * (0.04f + progress * 0.06f);

                // 缓慢旋转，模拟雪花
                sb.Draw(tex, dp, null, cc, c.Rotation, origin, scale, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层5 — 冰霜火花

        private void DrawFrostSparks(SpriteBatch sb) {
            Texture2D tex = ACMAsset.Sparkle ?? ACMAsset.BlankStar;
            if (tex == null) return;
            Vector2 origin = tex.Size() / 2f;

            for (int i = 0; i < FrostSparkCount; i++) {
                FrostSpark s = frostSparks[i];
                if (!s.IsActive) continue;

                Vector2 dp = s.Position - Main.screenPosition;
                float progress = MathF.Sin(s.AnimProgress * MathHelper.Pi);
                float alpha = progress * intensity * 0.4f;

                Color sc = Color.Lerp(FrostBlue, IceCyan, progress) * alpha;
                sc.A = 0;
                float scale = s.Scale * (0.05f + progress * 0.08f);
                sb.Draw(tex, dp, null, sc, globalTime * 2f + i, origin, scale, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 层6 — 暗角 + 冰蓝脉冲

        private void DrawVignette(SpriteBatch sb) {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Vector2 go = glow.Size() / 2f;
                float va = intensity * 0.55f;
                if (isPhase3) va *= 1.4f;
                Color vc = AbyssBlack with { A = 0 } * va;
                float cs = MathF.Min(Main.screenWidth, Main.screenHeight) * 0.55f / glow.Width;

                sb.Draw(glow, new Vector2(0, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(0, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
                sb.Draw(glow, new Vector2(Main.screenWidth, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            // 冰蓝脉冲 — 节奏缓慢沉重
            float pulse = (MathF.Sin(globalTime * 1.0f) * 0.5f + 0.5f) * intensity * 0.07f;
            if (isPhase3) pulse *= 2f;
            else if (isPhase2) pulse *= 1.3f;

            Color topC = Color.Lerp(FrostBlue, IceCyan, MathF.Sin(globalTime * 0.6f) * 0.5f + 0.5f) * pulse;
            topC.A = 0;
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight / 5), topC);

            // 底部深渊映射
            float btmPulse = pulse * 0.4f;
            Color btmC = DeepOcean * btmPulse;
            btmC.A = 0;
            sb.Draw(pixel, new Rectangle(0, Main.screenHeight * 4 / 5, Main.screenWidth, Main.screenHeight / 5), btmC);
        }

        #endregion

        #region 地表着色

        public override Color OnTileColor(Color inColor) {
            float shift = isPhase3 ? 0.4f : 0.3f;
            Color tint = Color.Lerp(Color.White, new Color(40, 50, 70), intensity * shift);
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

        private class IceCloud
        {
            public Vector2 Position;
            public float Scale, Rotation, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(5, 45);
            }

            public void Update(float mul) {
                if (!IsActive) {
                    if (--cooldown <= 0) Activate(mul);
                    return;
                }
                AnimProgress += AnimSpeed * mul;
                Position += Velocity * mul;
                Rotation += 0.0003f * mul;
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate(float mul) {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.001f, 0.003f);
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-450, Main.screenWidth + 450),
                    Main.screenPosition.Y + Main.rand.Next(-300, (int)(Main.screenHeight * 0.65f))
                );
                // 冰雾云移动缓慢
                Velocity = new Vector2(Main.rand.NextFloat(0.05f, 0.4f) * mul, Main.rand.NextFloat(-0.1f, 0.1f));
                Scale = Main.rand.NextFloat(2.5f, 5.5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        private class IceCrystal
        {
            public Vector2 Position;
            public float Rotation, Scale, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(10, 70);
            }

            public void Update() {
                if (!IsActive) {
                    if (--cooldown <= 0) Activate();
                    return;
                }
                AnimProgress += AnimSpeed;
                Position += Velocity;
                // 雪花缓慢飘落 + 左右摆动
                Velocity.X = MathF.Sin(AnimProgress * 6f) * 0.3f;
                Rotation += 0.01f;
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.003f, 0.01f);
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-50, Main.screenWidth + 50),
                    Main.screenPosition.Y + Main.rand.Next(-100, -10)
                );
                Velocity = new Vector2(0f, Main.rand.NextFloat(0.3f, 1.2f));
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                Scale = Main.rand.NextFloat(0.6f, 1.8f);
            }
        }

        private class FrostSpark
        {
            public Vector2 Position;
            public float Scale, AnimProgress, AnimSpeed;
            public bool IsActive;
            public Vector2 Velocity;
            private int cooldown;

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(20, 100);
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
                AnimSpeed = Main.rand.NextFloat(0.006f, 0.016f);
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(40, Main.screenWidth - 40),
                    Main.screenPosition.Y + Main.rand.Next(0, (int)(Main.screenHeight * 0.5f))
                );
                Velocity = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-0.2f, 0.8f));
                Scale = Main.rand.NextFloat(0.5f, 1.5f);
            }
        }
    }
}
