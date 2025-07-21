using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace AncientChineseMythology.UI.Elements;

public class UIExpBar : UIElement
{
    private readonly Texture2D _pixel = TextureAssets.MagicPixel.Value;
    private float _percent;

    public void SetPercent(float p) => _percent = Utils.Clamp(p, 0f, 1f);

    protected override void DrawSelf(SpriteBatch sb) {
        var r = GetDimensions().ToRectangle();
        sb.Draw(_pixel, r, Color.Black * .6f);
        var fg = new Rectangle(r.X + 2, r.Y + 2,
            (int)((r.Width - 4) * _percent), r.Height - 4);
        sb.Draw(_pixel, fg, Color.SkyBlue);
    }
}
