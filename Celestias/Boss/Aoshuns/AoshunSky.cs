using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoshuns
{
    /// <summary>
    /// 北海龙王敖顺天空效果 - 雷暴压迫天幕
    /// 暗紫色天空，随Boss血量降低加深，带有雷电脉冲
    /// </summary>
    internal class AoshunSky : CustomSky
    {
        private bool active;
        private float intensity;
        private float maxIntensity = 0.7f;
        private Color skyColor;
        private float pulsePhase;

        internal static string name;

        public static void LoadInstance() {
            name = "AncientChineseMythology:AoshunSky";
            SkyManager.Instance[name] = new AoshunSky();
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
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

                // 雷暴色阶：深紫 -> 暗蓝紫 -> 墨黑
                skyColor = VaultUtils.MultiStepColorLerp(t,
                    new Color(25, 10, 50),    // 深紫（最压迫）
                    new Color(15, 20, 60),    // 暗蓝紫
                    new Color(8, 8, 20));      // 墨黑（远距离）

                if (intensity < maxIntensity)
                    intensity += 0.01f;

                // 二阶段天空更暗更压迫
                float lifePercent = (float)boss.life / boss.lifeMax;
                if (lifePercent < 0.5f) {
                    maxIntensity = 0.85f;
                    skyColor = Color.Lerp(skyColor, new Color(30, 5, 50), 0.3f);
                }

                active = true;
            }
            else {
                intensity -= 0.01f;
                if (intensity <= 0f) {
                    intensity = 0f;
                    Deactivate();
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0f) return;

            // 雷电脉冲微颤
            float pulse = MathF.Sin(pulsePhase) * 0.8f * intensity;
            Vector2 shake = Main.rand.NextVector2Circular(pulse, pulse);

            // 天幕底色
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle((int)shake.X, (int)shake.Y, Main.screenWidth, Main.screenHeight),
                skyColor * intensity);

            // 雷电脉冲叠加层 - 紫色呼吸感
            float breathAlpha = (0.5f + MathF.Sin(pulsePhase * 1.5f) * 0.5f) * intensity * 0.15f;
            Color breathColor = new Color(60, 20, 100) * breathAlpha;
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                breathColor);

            // 顶部雷云渐变（模拟天际雷暴映照）
            Color topGlow = new Color(40, 30, 90) * intensity * 0.25f;
            int glowHeight = Main.screenHeight / 4;
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, glowHeight),
                topGlow);

            // 偶尔闪电闪光
            if (Main.rand.NextBool(120)) {
                float flashAlpha = Main.rand.NextFloat(0.1f, 0.3f) * intensity;
                Color flashColor = new Color(180, 160, 255) * flashAlpha;
                spriteBatch.Draw(VaultAsset.placeholder2.Value,
                    new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                    flashColor);
            }
        }

        public override Color OnTileColor(Color inColor) {
            // 所有地表颜色偏紫/变暗
            Color desaturated = Color.Lerp(inColor, new Color(40, 30, 60), 0.3f);
            return Color.Lerp(inColor, desaturated, intensity);
        }

        public override float GetCloudAlpha() {
            return 1f - intensity * 0.6f;
        }

        private NPC GetBoss() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == ModContent.NPCType<Aoshun>())
                    return npc;
            }
            return null;
        }
    }
}
