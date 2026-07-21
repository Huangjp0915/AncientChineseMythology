using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.BlackBear
{
    /// <summary>
    /// 黑熊精场景效果 — Boss 在场时激活黑风山天幕。
    /// </summary>
    internal class BlackBearSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(ModContent.NPCType<BlackBear>());
        public override void SpecialVisuals(Player player, bool isActive) {
            if (player.Alives()) {
                player.ManageSpecialBiomeVisuals(BlackBearSky.SkyName, isActive);
            }
        }
    }

    /// <summary>
    /// 黑风山天幕 — 黑风大王的妖风领域。
    ///
    /// 多层结构:
    ///  1. 墨黑/暗紫渐变压暗底色 (妖风蔽日)
    ///  2. 高速横扫黑风带 (Smoke 帧动画, 横向拉伸、速度远快于普通云 → "风"而非"雾")
    ///  3. 零星袈裟金尘 (BlankStar, 上飘)
    ///  4. 暗角 + 妖风紫脉冲
    ///
    /// 强度: P1 低压 (0.55) → P2 黑风漫天 (1.0); Boss 经 <see cref="PublishStorm"/> /
    /// <see cref="PublishGold"/> 推送演出节拍加成 (入场砸落 / 怒嚎蓄力 / 死亡金光)。
    /// 纯本地视觉, 服务端零绘制。
    /// </summary>
    internal class BlackBearSky : CustomSky, IACMLoader
    {
        public const string SkyName = "ACM:BlackBearSky";

        private bool active;
        private float intensity;
        private float globalTime;

        private float bossLifeFrac = 1f;
        private bool fury;

        // —— 演出节拍加成 (Boss 每帧推送, 过期自动衰减) ——
        private static float _stormBoost;   // 额外风势 0~1
        private static float _goldTint;     // 袈裟金染 0~1
        private static ulong _lastPublish;

        private const float FadeOutSpeed = 0.012f;

        // 颜色 — 墨黑 / 妖风暗紫 / 袈裟金
        private static readonly Color InkBlack = new(8, 6, 14);
        private static readonly Color WindViolet = new(52, 36, 78);
        private static readonly Color WindGrey = new(30, 26, 42);
        private static readonly Color KasayaGold = new(255, 209, 107);

        // 黑风带
        private const int WindStreakCount = 34;
        private readonly WindStreak[] windStreaks = new WindStreak[WindStreakCount];

        // 金尘
        private const int MoteCount = 14;
        private readonly GoldMote[] motes = new GoldMote[MoteCount];

        /// <summary>Boss 演出节拍推送: 额外风势与金染 (取 max, 2 帧未续订自动退场)。</summary>
        public static void PublishStorm(float storm, float gold = 0f) {
            if (Main.dedServ)
                return;
            if (Main.GameUpdateCount != _lastPublish) {
                _stormBoost = 0f;
                _goldTint = 0f;
            }
            _stormBoost = Math.Max(_stormBoost, storm);
            _goldTint = Math.Max(_goldTint, gold);
            _lastPublish = Main.GameUpdateCount;
        }

        void IACMLoader.LoadData() {
            SkyManager.Instance[SkyName] = this;
            // ManageSpecialBiomeVisuals 需要同名 Filter 存在 (XuanwuSky 同款): 轻微妖风压暗
            Filters.Scene[SkyName] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.08f, 0.05f, 0.13f)
                .UseOpacity(0.32f), EffectPriority.High);
            for (int i = 0; i < WindStreakCount; i++) windStreaks[i] = new WindStreak();
            for (int i = 0; i < MoteCount; i++) motes[i] = new GoldMote();
        }

        #region CustomSky 生命周期

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            for (int i = 0; i < WindStreakCount; i++) windStreaks[i].Reset();
            for (int i = 0; i < MoteCount; i++) motes[i].Reset();
        }

        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active || intensity > 0.01f;
        public override void Reset() { active = false; intensity = 0f; }

        public override void Update(GameTime gameTime) {
            globalTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

            NPC boss = FindBoss();
            bool shouldBeActive = boss != null && boss.active;

            // 节拍加成过期衰减 (Boss 停止推送后)
            if (Main.GameUpdateCount - _lastPublish > 2) {
                _stormBoost = MathHelper.Lerp(_stormBoost, 0f, 0.05f);
                _goldTint = MathHelper.Lerp(_goldTint, 0f, 0.04f);
            }

            if (shouldBeActive) {
                if (!active) Activate(Vector2.Zero);
                bossLifeFrac = (float)boss.life / boss.lifeMax;
                fury = bossLifeFrac < 0.5f;

                float target = MathHelper.Clamp((fury ? 1.0f : 0.55f) + _stormBoost * 0.35f, 0f, 1.25f);
                intensity = MathHelper.Lerp(intensity, target, 0.012f);
            }
            else {
                intensity -= FadeOutSpeed;
                if (intensity <= 0f) { intensity = 0f; if (active) Deactivate(); }
            }

            float windMul = 1f + (fury ? 0.6f : 0f) + _stormBoost * 1.4f;
            for (int i = 0; i < WindStreakCount; i++) windStreaks[i].Update(windMul);
            for (int i = 0; i < MoteCount; i++) motes[i].Update();
        }

        private static NPC FindBoss() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<BlackBear>() && npc.active) return npc;
            }
            return null;
        }

        #endregion

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (maxDepth >= 0 && minDepth < 0 && intensity > 0.01f) {
                DrawBackdrop(spriteBatch);
                DrawWindStreaks(spriteBatch);
                DrawGoldMotes(spriteBatch);
                DrawVignette(spriteBatch);
            }
        }

        // —— 层1: 墨黑压暗底色 (顶部妖紫 → 底部墨黑) ——
        private void DrawBackdrop(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle screen = new(0, 0, Main.screenWidth, Main.screenHeight);

            sb.Draw(pixel, screen, InkBlack * intensity * 0.72f);

            int bands = 10;
            for (int i = 0; i < bands; i++) {
                float t = (float)i / bands;
                int h = Main.screenHeight / bands;
                Color c = Color.Lerp(WindViolet, InkBlack, t) * intensity * 0.30f;
                sb.Draw(pixel, new Rectangle(0, i * h, Main.screenWidth, h), c);
            }

            // 妖风呼吸 (风声起伏的视觉对应)
            float breath = (0.5f + MathF.Sin(globalTime * 1.1f) * 0.5f) * intensity * 0.05f;
            if (fury) breath *= 1.8f;
            Color breathC = WindViolet * breath;
            breathC.A = 0;
            sb.Draw(pixel, screen, breathC);

            // 金染 (死亡收服节拍): 全屏一层暖金
            if (_goldTint > 0.01f) {
                Color gold = KasayaGold * (_goldTint * 0.22f);
                gold.A = 0;
                sb.Draw(pixel, screen, gold);
            }
        }

        // —— 层2: 高速横扫黑风带 ——
        private void DrawWindStreaks(SpriteBatch sb) {
            Texture2D tex = ACMAsset.Smoke;
            if (tex == null) return;
            int fs = tex.Width / 4;
            Vector2 origin = new(fs / 2f);

            for (int i = 0; i < WindStreakCount; i++) {
                WindStreak w = windStreaks[i];
                if (!w.IsActive) continue;

                Vector2 dp = w.Position - Main.screenPosition;
                float lifeFade = MathF.Sin(w.AnimProgress * MathHelper.Pi);
                float alpha = lifeFade * intensity * 0.42f;

                Color c = Color.Lerp(WindGrey, WindViolet, w.Hue) * alpha;
                if (_goldTint > 0.01f)
                    c = Color.Lerp(c, KasayaGold * alpha * 0.6f, _goldTint * 0.5f);
                c.A = 0;

                Rectangle src = new((i % 4) * fs, (i / 4 % 4) * fs, fs, fs);
                // 横向重度拉伸 → 读作"风"而非"云"
                Vector2 scale = new(w.Scale * 3.4f, w.Scale * 0.55f);
                sb.Draw(tex, dp, src, c, w.Tilt, origin, scale, SpriteEffects.None, 0f);
            }
        }

        // —— 层3: 袈裟金尘 ——
        private void DrawGoldMotes(SpriteBatch sb) {
            Texture2D tex = ACMAsset.BlankStar;
            if (tex == null) return;
            Vector2 origin = tex.Size() / 2f;

            float boost = 1f + _goldTint * 2.5f;
            for (int i = 0; i < MoteCount; i++) {
                GoldMote m = motes[i];
                if (!m.IsActive) continue;

                Vector2 dp = m.Position - Main.screenPosition;
                float p = MathF.Sin(m.AnimProgress * MathHelper.Pi);
                Color c = KasayaGold * (p * intensity * 0.30f * boost);
                c.A = 0;
                sb.Draw(tex, dp, null, c, globalTime * 0.7f + i, origin, m.Scale * (0.03f + p * 0.04f), SpriteEffects.None, 0f);
            }
        }

        // —— 层4: 暗角 ——
        private void DrawVignette(SpriteBatch sb) {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null) return;
            Vector2 go = glow.Size() / 2f;
            float va = intensity * 0.5f;
            if (fury) va *= 1.3f;
            Color vc = InkBlack with { A = 0 } * va;
            float cs = MathF.Min(Main.screenWidth, Main.screenHeight) * 0.55f / glow.Width;

            sb.Draw(glow, new Vector2(0, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
            sb.Draw(glow, new Vector2(Main.screenWidth, 0), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
            sb.Draw(glow, new Vector2(0, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
            sb.Draw(glow, new Vector2(Main.screenWidth, Main.screenHeight), null, vc, 0f, go, cs, SpriteEffects.None, 0f);
        }

        public override Color OnTileColor(Color inColor) {
            // 黑风蔽日: 地表整体压暗偏紫; 金染节拍时转暖
            Color dark = Color.Lerp(Color.White, new Color(70, 60, 95), intensity * 0.38f);
            if (_goldTint > 0.01f)
                dark = Color.Lerp(dark, new Color(255, 226, 160), _goldTint * 0.45f);
            return new Color(
                (int)(inColor.R * dark.R / 255f),
                (int)(inColor.G * dark.G / 255f),
                (int)(inColor.B * dark.B / 255f),
                inColor.A);
        }

        public override float GetCloudAlpha() => 1f - intensity * 0.8f;

        // ================================================================
        //  内部粒子
        // ================================================================

        private class WindStreak
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Scale, Tilt, Hue, AnimProgress, AnimSpeed;
            public bool IsActive;
            private int cooldown;

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(3, 30);
            }

            public void Update(float mul) {
                if (!IsActive) {
                    if (--cooldown <= 0) Activate();
                    return;
                }
                AnimProgress += AnimSpeed;
                Position += Velocity * mul;
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.004f, 0.010f);
                // 从屏幕左侧外生成, 高速向右横扫 (黑风统一风向)
                Position = new Vector2(
                    Main.screenPosition.X - Main.rand.Next(200, 900),
                    Main.screenPosition.Y + Main.rand.Next(-120, (int)(Main.screenHeight * 0.85f)));
                Velocity = new Vector2(Main.rand.NextFloat(7f, 16f), Main.rand.NextFloat(-0.6f, 0.6f));
                Scale = Main.rand.NextFloat(1.6f, 3.6f);
                Tilt = Main.rand.NextFloat(-0.06f, 0.06f);
                Hue = Main.rand.NextFloat();
            }
        }

        private class GoldMote
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Scale, AnimProgress, AnimSpeed;
            public bool IsActive;
            private int cooldown;

            public void Reset() {
                IsActive = false;
                AnimProgress = 0f;
                cooldown = Main.rand.Next(30, 140);
            }

            public void Update() {
                if (!IsActive) {
                    if (--cooldown <= 0) Activate();
                    return;
                }
                AnimProgress += AnimSpeed;
                Position += Velocity;
                Velocity.X = MathF.Sin(AnimProgress * 8f) * 0.4f + 1.2f; // 随风微飘
                if (AnimProgress >= 1f) Reset();
            }

            private void Activate() {
                IsActive = true;
                AnimProgress = 0f;
                AnimSpeed = Main.rand.NextFloat(0.004f, 0.012f);
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(0, Main.screenWidth),
                    Main.screenPosition.Y + Main.rand.Next((int)(Main.screenHeight * 0.2f), Main.screenHeight));
                Velocity = new Vector2(1.2f, -Main.rand.NextFloat(0.3f, 1.0f));
                Scale = Main.rand.NextFloat(0.5f, 1.4f);
            }
        }
    }
}
