using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Hoqings
{
    internal class HoqingSky : CustomSky
    {
        private bool active;
        private float intensity;
        private float maxIntensity = 0.8f; //更高压迫感
        private Color skyColor;

        internal static string name;
        internal static Asset<Texture2D> HanbaSkySun;
        internal static Asset<Texture2D> HanbaSkyColorBar;

        public static void LoadInstance() {
            name = "AncientChineseMythology:HoqingSky";
            SkyManager.Instance[name] = new HoqingSky();
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
            if (NPC.AnyNPCs(ModContent.NPCType<Hoqing>())) {
                NPC boss = null;
                foreach (var npc in Main.ActiveNPCs) {
                    if (npc.type == ModContent.NPCType<Hoqing>()) {
                        boss = npc;
                        break;
                    }
                }

                if (boss != null) {
                    float distance = Main.LocalPlayer.Distance(boss.Center);
                    float t = MathHelper.Clamp(distance / 1600f, 0f, 1f);

                    //瘟疫亡灵色阶：腐绿 -> 病黄绿 -> 尸绿魂光（越近越病态）
                    skyColor = VaultUtils.MultiStepColorLerp(t,
                        new Color(18, 34, 22),   //腐暗绿（最压迫）
                        new Color(46, 78, 40),   //病黄绿
                        new Color(96, 170, 90));  //尸绿魂光（近Boss时）

                    if (intensity < maxIntensity)
                        intensity += 0.01f;

                    active = true;
                }
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
            Vector2 shake = Main.rand.NextVector2Circular(1.5f * intensity, 1.5f * intensity); //更幽柔的震颤

            //背景主色调（幽蓝调）
            spriteBatch.Draw(VaultAsset.placeholder2.Value,
                new Rectangle((int)shake.X, (int)shake.Y, Main.screenWidth, Main.screenHeight),
                skyColor * intensity);

            //渐变疠气雾层（病绿）
            if (HanbaSkyColorBar?.Value != null) {
                Color mistColor = VaultUtils.MultiStepColorLerp(0.4f, new Color(120, 220, 100), new Color(40, 120, 60));
                spriteBatch.Draw(HanbaSkyColorBar.Value,
                    new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                    mistColor * intensity);
            }

            //冥月 / 疫眼（病绿光晕）
            if (HanbaSkySun?.Value != null) {
                Vector2 sunPos = new Vector2(Main.screenWidth / 2f, 140);
                Color sunColor = new Color(150, 255, 130, 0) * intensity * 1.5f;

                spriteBatch.Draw(HanbaSkySun.Value,
                    sunPos, null, sunColor, 0f, HanbaSkySun.Size() / 2f, 1.8f, SpriteEffects.None, 0f);
            }
        }

        public override Color OnTileColor(Color inColor) {
            //所有地表颜色染上病态尸绿/失色
            Color desaturated = Color.Lerp(inColor, new Color(60, 90, 55), 0.45f);
            return Color.Lerp(inColor, desaturated, intensity);
        }
    }
}
