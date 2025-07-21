using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AncientChineseMythology.Skies         // ← 修正命名空间
{
    public class TribulationSky : CustomSky
    {
        private bool _active;
        private float _opacity;

        /* ========= 必须实现的抽象方法 ========= */
        public override void Activate(Vector2 position, params object[] args) {
            _active = true;               // 或根据需要使用 position
        }
        public override void Deactivate(params object[] args) => _active = false;
        public override void Reset() => _opacity = 0f;

        public override bool IsActive() => _active || _opacity > 0f;

        public override void Update(GameTime gameTime) {
            float step = 0.01f;
            _opacity = MathHelper.Clamp(_opacity + (_active ? step : -step), 0f, 0.8f);
        }

        public override Color OnTileColor(Color inColor) =>
            Color.Lerp(inColor, Color.Black, _opacity);

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (_opacity <= 0f) return;

            Texture2D pixel = ModContent.Request<Texture2D>(
                "AncientChineseMythology/Textures/Blank").Value;

            spriteBatch.Draw(pixel,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                Color.Black * _opacity);
        }
    }
}
