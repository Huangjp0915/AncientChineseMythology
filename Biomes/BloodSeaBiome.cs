using AncientChineseMythology.Systems;
using AncientChineseMythology.WaterStyles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Capture;
using Terraria.ModLoader;

namespace AncientChineseMythology.Biomes
{
    /// <summary>
    /// 血海生物群系：现为地下血海盆地（地下血湖）。
    /// 触发条件为玩家处于地表以下、且周围血海砂足够多。
    /// 视觉氛围由 BloodSeaAtmosphereSystem 的全屏着色器提供。
    /// </summary>
    public class BloodSeaBiome : ModBiome
    {
        public override ModWaterStyle WaterStyle => ModContent.GetInstance<BloodSeaWaterStyle>();

        public override CaptureBiome.TileColorStyle TileColorStyle => CaptureBiome.TileColorStyle.Crimson;

        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;

        public override int Music => MusicLoader.GetMusicSlot(Mod, "Sounds/Music/BloodSeaTheme");

        //图鉴背景色（暗血红）
        public override Color? BackgroundColor => new Color(58, 8, 12);

        public override bool IsBiomeActive(Player player) {
            bool underground = player.Center.Y / 16f > Main.worldSurface;
            return underground && BloodSeaSystem.NearbyBloodTiles >= 50;
        }
    }
}
