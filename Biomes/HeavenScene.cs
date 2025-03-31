using Terraria;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.ID;

namespace AncientChineseMythology.SceneEffects
{
    public class HeavenScene : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot(Mod, "Sounds/Music/HeavenTheme");
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;
        
        public override bool IsSceneEffectActive(Player player)
        {
            // 检测周围是否有足够的彩虹砖和云砖
            int rainbowBrickCount = 0;
            int cloudBrickCount = 0;
            
            // 检测玩家周围一定范围内的方块
            int range = 50; // 检测范围
            int startX = (int)(player.position.X / 16) - range;
            int startY = (int)(player.position.Y / 16) - range;
            int endX = startX + range * 2;
            int endY = startY + range * 2;
            
            for (int i = startX; i < endX; i++)
            {
                for (int j = startY; j < endY; j++)
                {
                    if (i < 0 || j < 0 || i >= Main.maxTilesX || j >= Main.maxTilesY)
                        continue;
                        
                    Tile tile = Main.tile[i, j];
                    if (tile.HasTile)
                    {
                        // 检查是否是彩虹砖
                        if (tile.TileType == TileID.RainbowBrick)
                            rainbowBrickCount++;
                        // 检查是否是云砖
                        else if (tile.TileType == TileID.Cloud || tile.TileType == TileID.RainbowBrick)
                            cloudBrickCount++;
                    }
                }
            }
            
            // 总数量超过100时激活环境
            return (rainbowBrickCount >= 100 && cloudBrickCount >= 100);
        }
    }
}