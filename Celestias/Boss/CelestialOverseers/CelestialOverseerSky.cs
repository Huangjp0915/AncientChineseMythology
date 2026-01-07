using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialOverseers
{
    /// <summary>
    /// 天庭观察者 - 神圣天空效果
    /// 金色神圣的天空氛围
    /// </summary>
    internal class CelestialOverseerSky : CustomSky
    {
        private bool active;
        private float intensity;
        private const float maxIntensity = 0.5f;
        private Color skyColor;

        public const string SkyName = "AncientChineseMythology:CelestialOverseerSky";

        public static void LoadInstance() {
            SkyManager.Instance[SkyName] = new CelestialOverseerSky();
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override bool IsActive() => active;

        public override void Reset() {
            active = false;
            intensity = 0.01f;
        }

        public override Color OnTileColor(Color inColor) {
            // 添加神圣的金色色调
            Color holyTint = new Color(255, 245, 220);
            return Color.Lerp(inColor, holyTint, intensity * 0.3f);
        }

        public override float GetCloudAlpha() {
            return 1f - intensity * 0.5f;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (maxDepth >= 0 && minDepth < 0) {
                NPC boss = GetBoss();

                // 计算神圣光芒的偏移效果
                Vector2 pullShake = Vector2.Zero;
                if (boss != null) {
                    pullShake = (boss.Center - Main.LocalPlayer.Center).SafeNormalize(Vector2.Zero) * (2f * intensity);
                }

                // 绘制神圣天空背景
                Rectangle screenRect = new Rectangle(
                    (int)pullShake.X,
                    (int)pullShake.Y,
                    Main.screenWidth,
                    Main.screenHeight
                );

                // 主天空层 - 神圣金色
                spriteBatch.Draw(
                    VaultAsset.placeholder2.Value,
                    screenRect,
                    skyColor * intensity
                );

                // 额外的神圣光辉层
                if (boss != null && intensity > 0.1f) {
                    DrawDivineRays(spriteBatch, boss);
                }
            }
        }

        private void DrawDivineRays(SpriteBatch spriteBatch, NPC boss) {
            // 从Boss位置发出的神圣光芒
            if (ACMAsset.BlankStar == null) return;

            Vector2 bossScreenPos = boss.Center - Main.screenPosition;

            // 绘制光芒
            Color rayColor = new Color(255, 240, 180) * intensity * 0.2f;
            rayColor.A = 0;

            float time = (float)Main.GameUpdateCount / 60f;

            // 多层旋转光芒
            for (int i = 0; i < 3; i++) {
                float rotation = time * (0.1f + i * 0.05f);
                float scale = 15f + i * 5f;

                spriteBatch.Draw(
                    ACMAsset.BlankStar,
                    bossScreenPos,
                    null,
                    rayColor * (0.3f - i * 0.08f),
                    rotation,
                    ACMAsset.BlankStar.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        public override void Update(GameTime gameTime) {
            NPC boss = GetBoss();

            if (boss != null) {
                // 根据Boss状态更新天空效果
                float bossHealthPercent = (float)boss.life / boss.lifeMax;

                // Boss血量越低，天空效果越强烈
                float targetIntensity = maxIntensity;
                if (bossHealthPercent < 0.3f) {
                    targetIntensity = maxIntensity * 1.3f; // 三阶段更强烈
                }
                else if (bossHealthPercent < 0.65f) {
                    targetIntensity = maxIntensity * 1.15f; // 二阶段稍强
                }

                // 根据与玩家的距离调整颜色
                float distance = Main.LocalPlayer.Distance(boss.Center);
                float t = MathHelper.Clamp(distance / 2000f, 0f, 1f);

                // 神圣金色渐变
                skyColor = VaultUtils.MultiStepColorLerp(t,
                    new Color(80, 60, 30),    // 近处：深金色
                    new Color(60, 50, 40),    // 中距离：暖褐色
                    new Color(40, 35, 50)     // 远处：暗紫色
                );

                // 根据Boss阶段调整颜色
                if (bossHealthPercent < 0.3f) {
                    // 三阶段：更加神圣的金白色
                    skyColor = Color.Lerp(skyColor, new Color(100, 90, 70), 0.3f);
                }

                // 渐变增加强度
                if (intensity < targetIntensity) {
                    intensity += 0.008f;
                }
                else if (intensity > targetIntensity) {
                    intensity -= 0.005f;
                }

                active = true;
            }
            else {
                // Boss消失后渐渐消退
                intensity -= 0.015f;
                if (intensity <= 0f) {
                    intensity = 0f;
                    Deactivate();
                }
            }
        }

        private static NPC GetBoss() {
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<CelestialOverseer>()) {
                    return npc;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// 天空效果加载器
    /// </summary>
    internal class CelestialOverseerSkyLoader : ModSystem
    {
        public override void Load() {
            if (!Main.dedServ) {
                CelestialOverseerSky.LoadInstance();
            }
        }
    }
}
