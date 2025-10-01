using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Yingous
{
    internal class YingouSky : CustomSky
    {
        private bool active;
        private float intensity;
        private const float maxIntensity = 0.6f;
        private Color skyColor;
        internal static string name;
        public static void LoadInstance() {
            name = "AncientChineseMythology:YingouSky";
            SkyManager.Instance[name] = new YingouSky();
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0.01f;
        }

        public override void Deactivate(params object[] args) { active = false; }
        public override bool IsActive() => active;
        public override void Reset() { active = false; intensity = 0.01f; }
        public override Color OnTileColor(Color inColor) => inColor * (1f - intensity);

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            NPC boss = GetBoss();
            Vector2 pullShake = Vector2.Zero;
            if (boss != null) pullShake = (boss.Center - Main.LocalPlayer.Center).SafeNormalize(Vector2.Zero) * (2f * intensity);
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle((int)pullShake.X, (int)pullShake.Y, Main.screenWidth, Main.screenHeight),
                skyColor * intensity);
        }

        public override void Update(GameTime gameTime) {
            NPC boss = GetBoss();
            if (boss != null) {
                float distance = Main.LocalPlayer.Distance(boss.Center);
                float t = MathHelper.Clamp(distance / 1600f, 0f, 1f);
                skyColor = VaultUtils.MultiStepColorLerp(t,
                    new Color(20, 10, 40),
                    new Color(10, 40, 40),
                    new Color(120, 0, 0));
                if (intensity < maxIntensity) intensity += 0.01f;
                active = true;
            }
            else {
                intensity -= 0.01f;
                if (intensity <= 0f) { intensity = 0f; Deactivate(); }
            }
        }

        private static NPC GetBoss() {
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == ModContent.NPCType<Yingou>()) return npc;
            }
            return null;
        }
    }
}
