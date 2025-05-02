using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using AncientChineseMythology.Players;
using AncientChineseMythology.Systems;
using AncientChineseMythology.WaterStyles;
using Terraria.Graphics.Capture;

namespace AncientChineseMythology.Biomes
{
    public class BloodSeaBiome : ModBiome
    {
        public override ModWaterStyle WaterStyle => ModContent.GetInstance<BloodSeaWaterStyle>();

        public override CaptureBiome.TileColorStyle TileColorStyle => CaptureBiome.TileColorStyle.Crimson;
        
        public override SceneEffectPriority Priority  
            => SceneEffectPriority.BiomeHigh;

        public override int Music 
            => MusicLoader.GetMusicSlot(Mod, "Sounds/Music/BloodSeaTheme");

        public override ModSurfaceBackgroundStyle SurfaceBackgroundStyle 
            => ModContent.GetInstance<Backgrounds.BloodSeaSurfaceBGStyle>();

        public override bool IsBiomeActive(Player player)
            => player.ZoneBeach && BloodSeaSystem.NearbyBloodTiles >= 50;

        public override void SpecialVisuals(Player player, bool isActive)
        {
            // ① 云朵淡出 / 淡入
            float target = isActive ? 0f : 1f;
            Main.cloudAlpha = MathHelper.Lerp(Main.cloudAlpha, target, 0.05f);

            // ② 太阳 & 月亮移出／归位
            int offScreen = isActive ? 10_000 : 0;
            Main.sunModY  = (short)offScreen;
            Main.moonModY = (short)offScreen;
        }
    }
}
