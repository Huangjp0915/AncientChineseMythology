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
            const int range     = 50;  // 检测半径（单位：tile）
            const int needCount = 50;  // 触发所需方块数

            int cloudySandCount = 0;
            int FloatingBasaltCount = 0;


            int startX = (int)player.Center.X / 16 - range;
            int startY = (int)player.Center.Y / 16 - range;
            int endX   = startX + range * 2;
            int endY   = startY + range * 2;

            ushort cloudySandType = (ushort)ModContent.TileType<Tiles.Placable.CloudyGoldSand>();
            ushort FloatingBasaltType = (ushort)ModContent.TileType<Tiles.Placable.FloatingBasalt>();

            // 扫描正方形区域
            for (int x = startX; x <= endX; x++)
            {
                if (x < 0 || x >= Main.maxTilesX) continue;

                for (int y = startY; y <= endY; y++)
                {
                    if (y < 0 || y >= Main.maxTilesY) continue;

                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && tile.TileType == cloudySandType)
                    {
                        cloudySandCount++;
                        if (cloudySandCount >= needCount)   // 够数即可提前返回
                            return true;
                    }
                    else if (tile.HasTile && tile.TileType == FloatingBasaltType)
                    {
                        FloatingBasaltCount++;
                        if (FloatingBasaltCount >= needCount)   // 够数即可提前返回
                            return true;
                    }
                }
            }
            return false;
        }
    }
}
