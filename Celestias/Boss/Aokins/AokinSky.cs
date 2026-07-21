using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    /// <summary>
    /// 南海龙王敖钦天空效果 - 烈焰压迫天幕
    /// V3 六层：暗红天幕底色(随阶段/温度加深) + Smoke 帧动画火烧云 + GlaciateWave 热霾横漂 +
    /// 余烬飘浮层 + 底部沸海映照呼吸 + 死亡冷却/白闪联动（读 <see cref="AokinHeatScreenSystem.WhiteFlash"/>）。
    /// 狂暴期天幕泛白泛红、呼吸加速（读 Boss <see cref="Aokin.IsEnraged"/>）。
    /// 注册名与 LoadInstance 不可变（ACMMod.Load 调用）。
    /// </summary>
    internal class AokinSky : CustomSky
    {
        private bool active;
        private float intensity;
        private float maxIntensity = 0.7f;
        private Color skyColor;
        private float pulsePhase;
        private int bossPhase = 1;
        private bool bossDying;
        private float heatRatio;
        private float rageGlow;

        // 火烧云（对象池, 循环复用）
        private const int FireCloudCount = 30;
        private readonly FireCloud[] fireClouds = new FireCloud[FireCloudCount];
        private bool cloudsInit;

        // 热霾横漂层
        private const int HazeLayerCount = 3;
        private readonly float[] hazeOffsets = new float[HazeLayerCount];
        private static readonly float[] HazeSpeeds = [0.010f, 0.016f, 0.007f];

        internal static string name;

        public static void LoadInstance() {
            name = "AncientChineseMythology:AokinSky";
            SkyManager.Instance[name] = new AokinSky();
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
            if (!cloudsInit) {
                for (int i = 0; i < FireCloudCount; i++) fireClouds[i] = new FireCloud();
                cloudsInit = true;
            }
            for (int i = 0; i < FireCloudCount; i++) fireClouds[i].Reset();
            for (int i = 0; i < HazeLayerCount; i++) hazeOffsets[i] = i * 300f;
        }

        public override void Deactivate(params object[] args) => active = false;
        public override void Reset() { active = false; intensity = 0.01f; }
        public override bool IsActive() => active;

        public override void Update(GameTime gameTime) {
            pulsePhase += 0.03f;

            NPC boss = GetBoss();
            if (boss != null) {
                float distance = Main.LocalPlayer.Distance(boss.Center);
                float t = MathHelper.Clamp(distance / 1600f, 0f, 1f);

                // 火焰色阶：深暗红 -> 暗橙 -> 焦黑
                skyColor = VaultUtils.MultiStepColorLerp(t,
                    new Color(50, 12, 8),    // 深暗红（最压迫）
                    new Color(80, 30, 10),   // 暗橙红
                    new Color(40, 20, 15));  // 焦黑（远距离）

                if (intensity < maxIntensity)
                    intensity += 0.01f;

                // 阶段递进：P2 更红更暗, P3 焚海劫深红档; 温度与狂暴读 Boss 实例
                float lifePercent = (float)boss.life / boss.lifeMax;
                bossPhase = lifePercent < Aokin.Phase3Threshold ? 3 : (lifePercent < Aokin.Phase2Threshold ? 2 : 1);
                bossDying = boss.ai[0] == (float)Aokin.MainState.DeathAnimation;
                if (boss.ModNPC is Aokin aokin) {
                    heatRatio = aokin.HeatRatio;
                    rageGlow = MathHelper.Lerp(rageGlow, aokin.IsEnraged ? 1f : 0f, 0.06f);
                }

                if (bossPhase >= 2) {
                    maxIntensity = 0.85f;
                    skyColor = Color.Lerp(skyColor, new Color(70, 10, 5), 0.3f);
                }
                if (bossPhase >= 3) {
                    maxIntensity = 0.95f;
                    skyColor = Color.Lerp(skyColor, new Color(85, 8, 4), 0.45f);
                }
                // 温度推高天幕红度（余烬温度条的世界层回声）
                skyColor = Color.Lerp(skyColor, new Color(95, 22, 8), heatRatio * 0.25f);
                // 狂暴：天幕泛白红
                skyColor = Color.Lerp(skyColor, new Color(120, 34, 20), rageGlow * 0.4f);

                // 死亡演出：天空迅速冷却熄灭
                if (bossDying) {
                    maxIntensity = 0.6f;
                    skyColor = Color.Lerp(skyColor, new Color(25, 18, 22), 0.5f);
                    if (intensity > maxIntensity)
                        intensity -= 0.008f;
                }

                active = true;
            }
            else {
                rageGlow = 0f;
                intensity -= 0.01f;
                if (intensity <= 0f) {
                    intensity = 0f;
                    Deactivate();
                }
            }

            // 云与热霾推进
            if (cloudsInit) {
                float stormMul = bossPhase >= 3 ? 1.5f : (bossPhase >= 2 ? 1.2f : 1f);
                stormMul *= 1f + rageGlow * 0.5f;
                for (int i = 0; i < FireCloudCount; i++) fireClouds[i].Update(stormMul);
            }
            for (int i = 0; i < HazeLayerCount; i++) hazeOffsets[i] += HazeSpeeds[i] * (1f + heatRatio);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (maxDepth < 0 || minDepth >= 0)
                return;
            if (intensity <= 0f) return;

            // 火焰脉冲微颤
            float pulse = MathF.Sin(pulsePhase) * 0.8f * intensity;
            Vector2 shake = Main.rand.NextVector2Circular(pulse, pulse);

            // 层1: 天幕底色
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle((int)shake.X, (int)shake.Y, Main.screenWidth, Main.screenHeight),
                skyColor * intensity);

            // 层2: 火烧云（Smoke 帧动画）
            DrawFireClouds(spriteBatch);

            // 层3: 热霾横漂（GlaciateWave 拉伸条带, 温度越高越浓）
            DrawHeatHazeBands(spriteBatch);

            // 层4: 火焰脉冲叠加 - 暗红呼吸感（阶段/狂暴越高呼吸越快）
            float breathFreq = (bossPhase >= 3 ? 2.4f : 1.5f) * (1f + rageGlow * 0.6f);
            float breathAlpha = (0.5f + MathF.Sin(pulsePhase * breathFreq) * 0.5f) * intensity * (0.15f + rageGlow * 0.06f);
            Color breathColor = Color.Lerp(new Color(120, 30, 10), new Color(170, 50, 25), rageGlow) * breathAlpha;
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                breathColor);

            // 层5: 底部沸海映照
            Color bottomGlow = new Color(100, 40, 10) * intensity * ((bossPhase >= 3 ? 0.45f : 0.3f) + heatRatio * 0.12f);
            int glowHeight = Main.screenHeight / 3;
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, Main.screenHeight - glowHeight, Main.screenWidth, glowHeight),
                bottomGlow);

            // 层6: 余烬飘浮
            DrawEmberDrift(spriteBatch);

            // 死亡冲击白闪联动（本战唯一冲击帧, 天空同步泛白）
            float flash = AokinHeatScreenSystem.WhiteFlash;
            if (flash > 0.01f) {
                Color flashColor = Color.Lerp(Color.White, new Color(255, 226, 160), 0.3f) * (flash * 0.85f);
                spriteBatch.Draw(VaultAsset.placeholder2.Value,
                    new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                    flashColor);
            }
        }

        /// <summary>火烧云层：Smoke 4x4 帧图集, 暗红/焦橙交替, 缓慢横漂。</summary>
        private void DrawFireClouds(SpriteBatch sb) {
            if (!cloudsInit) return;
            Texture2D tex = ACMAsset.Smoke;
            if (tex == null) return;
            int fs = tex.Width / 4;
            Vector2 origin = new(fs / 2f);

            for (int i = 0; i < FireCloudCount; i++) {
                FireCloud c = fireClouds[i];
                if (!c.IsActive) continue;

                Vector2 dp = c.Position - Main.screenPosition;
                float lerp = MathF.Sin(pulsePhase * 0.4f + i * 0.31f) * 0.5f + 0.5f;
                Color cc = Color.Lerp(new Color(46, 12, 6), new Color(70, 26, 9), lerp);
                if (i % 6 == 0) cc = Color.Lerp(cc, new Color(120, 48, 16), 0.25f + rageGlow * 0.3f);

                float alpha = MathF.Sin(c.AnimProgress * MathHelper.Pi) * intensity * 0.5f;
                cc *= alpha;
                cc.A = 0;

                Rectangle src = new((i % 4) * fs, (i / 4 % 4) * fs, fs, fs);
                sb.Draw(tex, dp, src, cc, c.Rotation, origin, c.Scale, SpriteEffects.None, 0f);

                // 云底熔光映照
                Color glow = Color.Lerp(new Color(90, 30, 8), new Color(150, 70, 20), lerp) * (alpha * 0.2f);
                glow.A = 0;
                sb.Draw(tex, dp + new Vector2(0, fs * 0.1f * c.Scale), src, glow, c.Rotation * 0.9f, origin, c.Scale * 1.15f, SpriteEffects.None, 0f);
            }
        }

        /// <summary>热霾横漂层：GlaciateWave 拉伸条带, 温度越高越浓, 模拟远景热对流。</summary>
        private void DrawHeatHazeBands(SpriteBatch sb) {
            Texture2D tex = ACMAsset.GlaciateWave;
            if (tex == null) return;
            Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);

            for (int layer = 0; layer < HazeLayerCount; layer++) {
                float alpha = (0.07f - layer * 0.015f + heatRatio * 0.05f) * intensity;
                Color mc = Color.Lerp(new Color(180, 70, 25), new Color(90, 24, 10), layer / (float)HazeLayerCount) * alpha;
                mc.A = 0;

                for (int band = 0; band < 2; band++) {
                    float xOff = hazeOffsets[layer] * 55f + band * 700f;
                    float yOff = MathF.Sin(pulsePhase * 0.3f + layer * 1.3f + band * 2.1f) * 26f;
                    Vector2 pos = new(
                        (xOff % (Main.screenWidth + 800)) - 400,
                        Main.screenHeight * (0.22f + band * 0.34f + layer * 0.09f) + yOff);
                    float rot = MathF.Sin(pulsePhase * 0.12f + layer) * 0.04f;
                    Vector2 scale = new(
                        Main.screenWidth * 0.85f / tex.Width * (1.15f + layer * 0.25f),
                        0.20f * (1f + layer * 0.2f));
                    sb.Draw(tex, pos, null, mc, rot, origin, scale, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>余烬飘浮层：hash 定位, 循环上升, 亮度闪烁。仅天空层, 不遮挡游戏元素。</summary>
        private void DrawEmberDrift(SpriteBatch spriteBatch) {
            Texture2D dot = VaultAsset.placeholder2.Value;
            int count = bossPhase >= 3 ? 44 : 26;
            float ascendSpeed = (bossPhase >= 3 ? 46f : 28f) * (1f + rageGlow * 0.5f);

            for (int i = 0; i < count; i++) {
                // 确定性散列（不依赖随机数, 帧间连续）
                float hx = MathF.Abs(MathF.Sin(i * 12.9898f) * 43758.547f % 1f);
                float hy = MathF.Abs(MathF.Sin(i * 78.233f) * 12578.12f % 1f);
                float hs = MathF.Abs(MathF.Sin(i * 37.719f) * 9631.3f % 1f);

                float x = (hx * Main.screenWidth + MathF.Sin(pulsePhase * 0.6f + i) * 30f) % Main.screenWidth;
                float y = (hy * Main.screenHeight - pulsePhase * ascendSpeed * (0.5f + hs * 0.8f)) % Main.screenHeight;
                if (y < 0) y += Main.screenHeight;

                float flicker = 0.4f + 0.6f * MathF.Abs(MathF.Sin(pulsePhase * 2.5f + i * 1.7f));
                float size = 1.5f + hs * 2.5f;
                Color ember = Color.Lerp(new Color(255, 120, 40), new Color(255, 200, 90), hs)
                    * (intensity * 0.5f * flicker);

                spriteBatch.Draw(dot, new Rectangle((int)x, (int)y, (int)size, (int)size), ember);
            }
        }

        public override Color OnTileColor(Color inColor) {
            // 所有地表颜色偏红/变暗
            Color desaturated = Color.Lerp(inColor, new Color(80, 40, 30), 0.3f);
            return Color.Lerp(inColor, desaturated, intensity);
        }

        public override float GetCloudAlpha() => 1f - intensity * 0.6f;

        private static NPC GetBoss() {
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<Aokin>()) return npc;
            }
            return null;
        }

        /// <summary>火烧云粒子（对象池成员, 循环激活）。</summary>
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
                cooldown = Main.rand.Next(5, 50);
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
                AnimSpeed = Main.rand.NextFloat(0.0012f, 0.0033f);
                Position = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-450, Main.screenWidth + 450),
                    Main.screenPosition.Y + Main.rand.Next(-280, (int)(Main.screenHeight * 0.55f)));
                Velocity = new Vector2(Main.rand.NextFloat(0.1f, 0.5f) * mul, Main.rand.NextFloat(-0.08f, 0.08f));
                Scale = Main.rand.NextFloat(2.4f, 5.2f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }
    }
}
