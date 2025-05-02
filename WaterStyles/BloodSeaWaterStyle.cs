using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;
using ReLogic.Content;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace AncientChineseMythology.WaterStyles
{
    public class BloodSeaWaterStyle : ModWaterStyle
    {
        private Asset<Texture2D> rainTexture;
		public override void Load() {
			rainTexture = Mod.Assets.Request<Texture2D>("Textures/WaterStyles/BloodSeaWaterStyle_Rain");
		}
        
        private const string Base = "Textures/WaterStyles/BloodSeaWaterStyle";   // <-- 和 PNG 同级

        public override string Texture      => $"{Mod.Name}/{Base}";
        public override string BlockTexture => $"{Mod.Name}/{Base}_Block";
        public override string SlopeTexture => $"{Mod.Name}/{Base}_Slope";

        public override int ChooseWaterfallStyle() {
			return ModContent.GetInstance<BloodSeaWaterStyle>().Slot;
		}

        public override int GetSplashDust()  => DustID.Blood;
        public override int GetDropletGore() => GoreID.WaterDrip;

        public override byte GetRainVariant() {
			return (byte)Main.rand.Next(3);
		}

        public override Asset<Texture2D> GetRainTexture() => rainTexture;
        
    }
}
