using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.WaterfallStyles
{
    public class BloodSeaWaterfallStyle : ModWaterfallStyle
    {
        public override string Texture => "AncientChineseMythology/Textures/Waterfall/BloodSeaWaterfallStyle";

        public override void AddLight(int i, int j) =>
			Lighting.AddLight(new Vector2(i, j).ToWorldCoordinates(), Color.White.ToVector3() * 0.5f);
    }
}
