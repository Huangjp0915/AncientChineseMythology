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

        /*public override void SpecialVisuals(Player player, bool isActive)
        {
            if (player.whoAmI != Main.myPlayer) return;          // 只改本机视野

            const float minCloud = 0.4f;                         // 不要彻底清零
            float target = isActive ? minCloud : 1f;

            Main.cloudAlpha = MathHelper.Lerp(Main.cloudAlpha, target, 0.05f);

            // 太阳 / 月亮仍按原逻辑移入移出
            short off = (short)(isActive ? 10_000 : 0);
            Main.sunModY  = off;
            Main.moonModY = off;
        }*/
    }
}
