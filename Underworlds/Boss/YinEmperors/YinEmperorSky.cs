using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors
{
    /// <summary>
    /// 阴天子天空效果 - 帝冥黑金色调的压迫性天空
    /// 战斗时天空笼罩在腐朽帝王的冥域之中
    /// </summary>
    public class YinEmperorSky : CustomSky
    {
        private bool active;
        private float intensity;
        private float maxIntensity = 0.85f;
        private Color skyColor;

        // 动态效果参数
        private float dragonPulse;
        private float lightningTimer;
        private Vector2 bossCenter;
        /// <summary>死亡弧线静默期的骤暗（读取本体 DeathDarken）</summary>
        private float deathDarken;

        internal static string name;

        public static void LoadInstance() {
            name = "AncientChineseMythology:YinEmperorSky";
            SkyManager.Instance[name] = new YinEmperorSky();
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Reset() {
            active = false;
            intensity = 0.01f;
        }

        public override bool IsActive() => active;

        public override void Update(GameTime gameTime) {
            dragonPulse += 0.03f;
            lightningTimer += 0.02f;

            if (NPC.AnyNPCs(ModContent.NPCType<YinEmperor>())) {
                NPC boss = null;
                foreach (var npc in Main.ActiveNPCs) {
                    if (npc.type == ModContent.NPCType<YinEmperor>()) {
                        boss = npc;
                        break;
                    }
                }

                if (boss != null) {
                    bossCenter = boss.Center;
                    float distance = Main.LocalPlayer.Distance(boss.Center);
                    float t = MathHelper.Clamp(distance / 1600f, 0f, 1f);

                    // 帝冥风格多重色阶：深黑金 -> 腐朽紫 -> 幽暗蓝
                    skyColor = VaultUtils.MultiStepColorLerp(t,
                        new Color(10, 8, 15),     // 深渊黑（最压迫）
                        new Color(30, 15, 50),    // 帝冥紫
                        new Color(50, 35, 20),    // 腐朽金
                        new Color(20, 25, 40));   // 幽暗蓝

                    if (intensity < maxIntensity)
                        intensity += 0.008f;

                    // 阶段变化增强天空效果
                    float lifePercent = (float)boss.life / boss.lifeMax;
                    if (lifePercent < 0.5f) {
                        maxIntensity = 0.92f;
                        // 低血量时天空泛红
                        if (lifePercent < 0.25f) {
                            skyColor = Color.Lerp(skyColor, new Color(50, 10, 15), 0.3f);
                        }
                    }

                    // 死亡弧线：静默期天穹骤暗（终爆前的吸气）
                    float darkenTarget = boss.ModNPC is YinEmperor emp ? emp.DeathDarken : 0f;
                    deathDarken = MathHelper.Lerp(deathDarken, darkenTarget, 0.2f);
                    if (deathDarken > 0.01f)
                        skyColor = Color.Lerp(skyColor, new Color(2, 2, 5), deathDarken * 0.85f);

                    active = true;
                }
            }
            else {
                intensity -= 0.012f;
                if (intensity <= 0f) {
                    intensity = 0f;
                    Deactivate();
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (maxDepth >= 0 && minDepth < 0) {
                Vector2 shake = Main.rand.NextVector2Circular(1f * intensity, 1f * intensity);

                // 主色调背景
                spriteBatch.Draw(
                    TextureAssets.MagicPixel.Value,
                    new Rectangle((int)shake.X, (int)shake.Y, Main.screenWidth, Main.screenHeight),
                    skyColor * intensity
                );

                // 远景幡旗剪影（酆都仪仗森林，慢视差）
                DrawBannerSilhouettes(spriteBatch);

                // 龙气脉动层
                DrawDragonVeinLayer(spriteBatch);

                // 帝冥符文粒子
                DrawImperialParticles(spriteBatch);

                // 冥雷闪烁
                DrawNetherLightning(spriteBatch);

                // 酆帝诏书 - 阴阳半场判罚
                DrawYinYangField(spriteBatch);

                // 终诏 - 十字激光预告
                DrawFinalDecreeTelegraph(spriteBatch);
            }
        }

        /// <summary>
        /// 阴阳半场：以分界线把场地分为阴(左)/阳(右)两半，站错半场会被灼魂 DoT。
        /// 安全半场偏暗、危险半场泛红脉动，切换前有预警闪烁。
        /// </summary>
        private void DrawYinYangField(SpriteBatch sb) {
            if (!YinEmperor.YinYangActive) return;

            var pixel = TextureAssets.MagicPixel.Value;
            float cxScreen = YinEmperor.YinYangCenterX - Main.screenPosition.X;
            int divider = (int)MathHelper.Clamp(cxScreen, 0, Main.screenWidth);

            int safe = YinEmperor.YinYangSafeSide;
            float danger = 0.5f + 0.5f * MathF.Sin(dragonPulse * 4f);
            float warnBoost = YinEmperor.YinYangWarning ? (0.5f + 0.5f * MathF.Sin(dragonPulse * 16f)) * 0.18f : 0f;

            Color yin = new Color(45, 18, 80);
            Color yang = new Color(80, 62, 22);

            Color leftCol = safe == 0
                ? yin * 0.16f
                : Color.Lerp(yin, YinEmperorHelper.NetherBloodRed, 0.55f) * (0.16f + danger * 0.14f + warnBoost);
            Color rightCol = safe == 1
                ? yang * 0.16f
                : Color.Lerp(yang, YinEmperorHelper.NetherBloodRed, 0.55f) * (0.16f + danger * 0.14f + warnBoost);

            sb.Draw(pixel, new Rectangle(0, 0, divider, Main.screenHeight), leftCol);
            sb.Draw(pixel, new Rectangle(divider, 0, Main.screenWidth - divider, Main.screenHeight), rightCol);

            // 分界线
            Color line = Color.Lerp(YinEmperorHelper.ImperialGold, YinEmperorHelper.AbyssPurple, 0.4f);
            line.A = 0;
            float lineGlow = 0.5f + 0.5f * MathF.Sin(dragonPulse * 3f);
            sb.Draw(pixel, new Rectangle(divider - 3, 0, 6, Main.screenHeight), line * (0.5f + lineGlow * 0.3f));
            sb.Draw(pixel, new Rectangle(divider - 10, 0, 20, Main.screenHeight), line * 0.12f);
        }

        /// <summary>
        /// 终诏十字激光预告：八向充能警示线，随 FinalDecreeCharge 增亮，给玩家 ~4s 反应窗口。
        /// </summary>
        private void DrawFinalDecreeTelegraph(SpriteBatch sb) {
            float charge = YinEmperor.FinalDecreeCharge;
            if (charge <= 0.001f || charge >= 1f) return;

            var pixel = TextureAssets.MagicPixel.Value;
            Vector2 origin = bossCenter - Main.screenPosition;

            Color warn = YinEmperorHelper.NetherBloodRed;
            warn.A = 0;
            float alpha = charge * 0.5f;
            float width = 2f + charge * 8f;
            float len = 2600f;

            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.PiOver4 * i;
                sb.Draw(pixel, origin, new Rectangle(0, 0, 1, 1),
                    warn * alpha, angle, new Vector2(0f, 0.5f),
                    new Vector2(len, width), SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 远景幡旗剪影 - 酆都仪仗森林（慢视差 + 微摆，纯剪影不抢戏）
        /// </summary>
        private void DrawBannerSilhouettes(SpriteBatch sb) {
            if (intensity < 0.35f) return;

            var pixel = TextureAssets.MagicPixel.Value;
            float parallax = Main.screenPosition.X * 0.06f;
            float alpha = intensity * (1f - deathDarken) * 0.5f;
            Color silhouette = new Color(6, 4, 12) * alpha;
            Color clothCol = new Color(28, 14, 40) * alpha;

            const int count = 9;
            for (int i = 0; i < count; i++) {
                float seedF = i * 269.3f;
                float x = ((seedF * 7.7f - parallax) % (Main.screenWidth + 240f) + Main.screenWidth + 240f)
                          % (Main.screenWidth + 240f) - 120f;
                float baseY = Main.screenHeight * (0.28f + (i % 3) * 0.09f);
                float poleH = Main.screenHeight * (0.5f - (i % 3) * 0.06f);
                float sway = MathF.Sin(dragonPulse * 0.7f + seedF) * 6f;

                // 幡杆
                sb.Draw(pixel, new Rectangle((int)x, (int)baseY, 4, (int)poleH), silhouette);
                // 布幔（微摆的窄条）
                int clothW = 26 + (i % 3) * 6;
                int clothH = (int)(poleH * 0.55f);
                sb.Draw(pixel, new Rectangle((int)(x - clothW + sway), (int)baseY + 8, clothW, clothH), clothCol);
                // 布幔底部渐灭
                sb.Draw(pixel, new Rectangle((int)(x - clothW + sway * 1.4f), (int)baseY + 8 + clothH, clothW, 14),
                    clothCol * 0.5f);
            }
        }

        /// <summary>
        /// 龙气脉动 - 天空中隐约可见的龙气流动
        /// </summary>
        private void DrawDragonVeinLayer(SpriteBatch sb) {
            if (intensity < 0.3f) return;

            float time = Main.GlobalTimeWrappedHourly;
            int veinCount = 6;

            for (int i = 0; i < veinCount; i++) {
                float seed = i * 197.3f;
                float baseY = Main.screenHeight * (0.1f + 0.8f * i / veinCount);
                float wave = MathF.Sin(time * 0.5f + seed) * 40f;

                // 龙气光带
                Color veinColor = Color.Lerp(YinEmperorHelper.ImperialGold, YinEmperorHelper.AbyssPurple, 0.6f);
                veinColor *= intensity * 0.08f;
                veinColor.A = 0;

                int segmentCount = 20;
                for (int s = 0; s < segmentCount; s++) {
                    float progress = s / (float)segmentCount;
                    float x = Main.screenWidth * progress;
                    float y = baseY + MathF.Sin(progress * MathHelper.Pi * 3f + time + seed) * wave;
                    float pulse = MathF.Sin(dragonPulse * 2f + progress * MathHelper.Pi + seed) * 0.4f + 0.6f;

                    sb.Draw(
                        TextureAssets.MagicPixel.Value,
                        new Rectangle((int)x, (int)y, Main.screenWidth / segmentCount + 2, (int)(4f * pulse * intensity)),
                        veinColor * pulse
                    );
                }
            }
        }

        /// <summary>
        /// 帝冥符文粒子 - 天空中飘浮的古老符文碎片
        /// </summary>
        private void DrawImperialParticles(SpriteBatch sb) {
            if (intensity < 0.2f) return;

            float time = Main.GlobalTimeWrappedHourly;
            int particleCount = (int)(20 * intensity);

            for (int i = 0; i < particleCount; i++) {
                float seed = i * 173.7f;
                float x = (seed + time * 12f) % Main.screenWidth;
                float y = (seed * 2.7f + time * 8f) % Main.screenHeight;

                float pulse = MathF.Sin(time * 1.5f + seed * 0.1f) * 0.4f + 0.6f;
                float alpha = pulse * intensity * 0.25f;

                // 金色与紫色交替
                Color particleColor = i % 3 == 0
                    ? YinEmperorHelper.ImperialGold
                    : (i % 3 == 1 ? YinEmperorHelper.AbyssPurple : YinEmperorHelper.SoulLanternCyan);
                particleColor *= alpha;
                particleColor.A = 0;

                float size = 1.5f + pulse * 2f;
                sb.Draw(
                    TextureAssets.MagicPixel.Value,
                    new Rectangle((int)x, (int)y, (int)size, (int)size),
                    particleColor
                );
            }
        }

        /// <summary>
        /// 冥雷闪烁 - 天空中偶尔出现的幽暗雷光
        /// </summary>
        private void DrawNetherLightning(SpriteBatch sb) {
            if (intensity < 0.4f) return;

            // 低概率闪烁
            float flashChance = MathF.Sin(lightningTimer * 7f) * MathF.Sin(lightningTimer * 13f);
            if (flashChance > 0.85f) {
                Color flashColor = Color.Lerp(YinEmperorHelper.ImperialGold, YinEmperorHelper.AbyssPurple, 0.3f);
                flashColor *= intensity * 0.12f * (flashChance - 0.85f) / 0.15f;
                flashColor.A = 0;

                sb.Draw(
                    TextureAssets.MagicPixel.Value,
                    new Rectangle(0, 0, Main.screenWidth, Main.screenHeight / 3),
                    flashColor
                );
            }
        }

        public override Color OnTileColor(Color inColor) {
            Color tint = Color.Lerp(YinEmperorHelper.AbyssPurple, YinEmperorHelper.ImperialGold, 0.2f);
            return Color.Lerp(inColor, tint, intensity * 0.5f);
        }

        public override float GetCloudAlpha() {
            return 1f - intensity * 0.8f;
        }
    }

    /// <summary>
    /// 加载阴天子天空效果
    /// </summary>
    public class YinEmperorSkyLoader : ModSystem
    {
        public override void Load() {
            if (Main.dedServ) return;
            YinEmperorSky.LoadInstance();
        }
    }

    /// <summary>
    /// 阴天子Boss战斗系统 - 管理天空效果激活/关闭
    /// </summary>
    public class YinEmperorBossSystem : ModSystem
    {
        public override void PostUpdateNPCs() {
            if (NPC.AnyNPCs(ModContent.NPCType<YinEmperor>())) {
                if (!SkyManager.Instance[YinEmperorSky.name].IsActive()) {
                    SkyManager.Instance.Activate(YinEmperorSky.name);
                }
            }
            else {
                if (SkyManager.Instance[YinEmperorSky.name].IsActive()) {
                    SkyManager.Instance.Deactivate(YinEmperorSky.name);
                }
            }
        }
    }
}
