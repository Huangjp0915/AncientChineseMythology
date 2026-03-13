using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    /// <summary>
    /// 南海龙王敖钦天空效果 - 烈焰压迫天幕
    /// 暗红色天空，随Boss血量降低加深，带有火焰脉冲
    /// </summary>
    internal class AokinSky : CustomSky
    {
        private bool active;
        private float intensity;
        private float maxIntensity = 0.7f;
        private Color skyColor;
        private float pulsePhase;

        internal static string name;

        public static void LoadInstance() {
            name = "AncientChineseMythology:AokinSky";
            SkyManager.Instance[name] = new AokinSky();
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

                // 火焰色阶：深暗红 -> 暗橙 -> 焦黑
                skyColor = VaultUtils.MultiStepColorLerp(t,
                    new Color(50, 12, 8),    // 深暗红（最压迫）
                    new Color(80, 30, 10),   // 暗橙红
                    new Color(40, 20, 15));  // 焦黑（远距离）

                if (intensity < maxIntensity)
                    intensity += 0.01f;

                // 二阶段天空更红更暗
                float lifePercent = (float)boss.life / boss.lifeMax;
                if (lifePercent < 0.5f) {
                    maxIntensity = 0.85f;
                    skyColor = Color.Lerp(skyColor, new Color(70, 10, 5), 0.3f);
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

            // 火焰脉冲微颤
            float pulse = MathF.Sin(pulsePhase) * 0.8f * intensity;
            Vector2 shake = Main.rand.NextVector2Circular(pulse, pulse);

            // 天幕底色
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle((int)shake.X, (int)shake.Y, Main.screenWidth, Main.screenHeight),
                skyColor * intensity);

            // 火焰脉冲叠加层 - 暗红色呼吸感
            float breathAlpha = (0.5f + MathF.Sin(pulsePhase * 1.5f) * 0.5f) * intensity * 0.15f;
            Color breathColor = new Color(120, 30, 10) * breathAlpha;
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                breathColor);

            // 底部火焰渐变（模拟地面烈焰映照）
            Color bottomGlow = new Color(100, 40, 10) * intensity * 0.3f;
            int glowHeight = Main.screenHeight / 3;
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle(0, Main.screenHeight - glowHeight, Main.screenWidth, glowHeight),
                bottomGlow);
        }

        public override Color OnTileColor(Color inColor) {
            // 所有地表颜色偏红/变暗
            Color desaturated = Color.Lerp(inColor, new Color(80, 40, 30), 0.3f);
            return Color.Lerp(inColor, desaturated, intensity);
        }

        private static NPC GetBoss() {
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<Aokin>()) return npc;
            }
            return null;
        }
    }
}
