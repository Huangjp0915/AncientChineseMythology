using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// 西海龙王敖闰天空效果 - 寒霜压迫天幕
    /// 深蓝色天空，随Boss血量降低加深，带有冰霜脉冲
    /// </summary>
    internal class AoyuanSky : CustomSky
    {
        private bool active;
        private float intensity;
        private float maxIntensity = 0.7f;
        private Color skyColor;
        private float pulsePhase;

        internal static string name;

        public static void LoadInstance() {
            name = "AncientChineseMythology:AoyuanSky";
            SkyManager.Instance[name] = new AoyuanSky();
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

                // 冰霜色阶：深海蓝 -> 暗青 -> 极夜黑
                skyColor = VaultUtils.MultiStepColorLerp(t,
                    new Color(8, 20, 60),    // 深海蓝（最压迫）
                    new Color(15, 40, 80),   // 暗青蓝
                    new Color(10, 15, 30));   // 极夜黑（远距离）

                if (intensity < maxIntensity)
                    intensity += 0.01f;

                // 二阶段天空更暗更冷
                float lifePercent = (float)boss.life / boss.lifeMax;
                if (lifePercent < 0.5f) {
                    maxIntensity = 0.85f;
                    skyColor = Color.Lerp(skyColor, new Color(5, 10, 40), 0.3f);
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

            // 冰霜脉冲微颤
            float pulse = MathF.Sin(pulsePhase) * 0.8f * intensity;
            Vector2 shake = Main.rand.NextVector2Circular(pulse, pulse);

            // 天幕底色
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle((int)shake.X, (int)shake.Y, Main.screenWidth, Main.screenHeight),
                skyColor * intensity);

            // 冰霜脉冲叠加层 - 深蓝呼吸感
            float breathAlpha = (0.5f + MathF.Sin(pulsePhase * 1.5f) * 0.5f) * intensity * 0.15f;
            Color breathColor = new Color(20, 50, 120) * breathAlpha;
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                breathColor);

            // 顶部极光渐变（模拟天际冰霜映照）
            Color topGlow = new Color(30, 80, 130) * intensity * 0.25f;
            int glowHeight = Main.screenHeight / 4;
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, glowHeight),
                topGlow);
        }

        public override Color OnTileColor(Color inColor) {
            // 所有地表颜色偏蓝/变暗
            Color desaturated = Color.Lerp(inColor, new Color(30, 50, 80), 0.3f);
            return Color.Lerp(inColor, desaturated, intensity);
        }

        public override float GetCloudAlpha() {
            return 1f - intensity * 0.6f;
        }

        private NPC GetBoss() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == ModContent.NPCType<Aoyuan>())
                    return npc;
            }
            return null;
        }
    }
}
