using AncientChineseMythology.Biomes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology.Systems
{
    /// <summary>
    /// 地下血海盆地氛围渲染：当本地玩家位于血海生物群系时，按强度淡入淡出，
    /// 在 PostDrawTiles 阶段用全屏程序化着色器叠加血雾/焦散/体积光/血粒/暗角。
    /// （绘制于实体之下以保证可读性，参考 UnderworldFogSystem 的叠加范式。）
    /// </summary>
    public class BloodSeaAtmosphereSystem : ModSystem
    {
        private static Asset<Effect> effect;
        private float intensity;
        private float time;

        public override void Unload() => effect = null;

        private static Effect GetEffect() {
            effect ??= ModContent.Request<Effect>(
                "AncientChineseMythology/Effects/BloodSeaAtmosphere",
                AssetRequestMode.ImmediateLoad);
            return effect?.Value;
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) return;

            bool active = !Main.gameMenu
                && Main.LocalPlayer != null
                && Main.LocalPlayer.active
                && Main.LocalPlayer.InModBiome(ModContent.GetInstance<BloodSeaBiome>());

            float target = active ? 1f : 0f;
            intensity = MathHelper.Lerp(intensity, target, active ? 0.03f : 0.045f);
            if (intensity < 0.0015f) intensity = 0f;

            time += 1f / 60f;
        }

        public override void PostDrawTiles() {
            if (Main.gameMenu || intensity <= 0.001f)
                return;

            Effect fx = GetEffect();
            if (fx == null)
                return;

            //取模避免世界坐标过大导致噪声精度退化
            Vector2 screenPos = new Vector2(Main.screenPosition.X % 4096f, Main.screenPosition.Y % 4096f);

            var p = fx.Parameters;
            p["uTime"]?.SetValue(time);
            p["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            p["uResolution"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            p["uScreenPos"]?.SetValue(screenPos);
            p["uSubmerged"]?.SetValue(Main.LocalPlayer.wet ? 1f : 0f);

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            SpriteBatch sb = Main.spriteBatch;

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
            sb.End();
        }
    }
}
