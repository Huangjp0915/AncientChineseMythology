using Terraria;
using Terraria.ModLoader;
using Terraria.ID;

namespace AncientChineseMythology.Biomes
{
    public class HeavenBiome : ModBiome
    {
        // 优先级 
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;

        public override int Music => MusicLoader.GetMusicSlot(Mod, "Sounds/Music/HeavenTheme");

        public override string BackgroundPath => "AncientChineseMythology/Textures/Backgrounds/Backgrounds/HeavenBackground";


        public override void OnEnter(Player player)
        {
        }

        public override void OnLeave(Player player)
        {
        }

        // 周围 50 格内的云砖 & 彩虹砖各 ≥100 时激活
        public override bool IsBiomeActive(Player player)
        {
            int rainbowBrickCount = 0;
            int cloudBrickCount = 0;

            int range = 50; // 检测半径 (格)
            int startX = (int)(player.position.X / 16) - range;
            int startY = (int)(player.position.Y / 16) - range;
            int endX = startX + range * 2;
            int endY = startY + range * 2;

            for (int i = startX; i < endX; i++)
            {
                for (int j = startY; j < endY; j++)
                {
                    // 越界检查
                    if (i < 0 || j < 0 || i >= Main.maxTilesX || j >= Main.maxTilesY)
                        continue;

                    Tile tile = Main.tile[i, j];
                    if (tile != null && tile.HasTile)
                    {
                        if (tile.TileType == TileID.RainbowBrick)
                            rainbowBrickCount++;
                        else if (tile.TileType == TileID.Cloud)
                            cloudBrickCount++;
                    }
                }
            }

            // 必须云砖≥100 并且 彩虹砖≥100 才激活
            return (rainbowBrickCount >= 100 && cloudBrickCount >= 100);
        }
    }
}
