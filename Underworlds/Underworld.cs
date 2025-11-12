using AncientChineseMythology.Underworlds.Tiles;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds
{
    internal class Underworld : ModSystem
    {
        public static int UmbralStoneTileID => ModContent.TileType<UmbralStone>();

        /// <summary>
        /// 生成地府地形的接口方法
        /// 可在任何时候调用以生成地府地形
        /// </summary>
        /// <param name="seed">可选的随机种子</param>
        public static void GenerateTerrain(int? seed = null) {
            UnderworldTerrainGenerator.GenerateUnderworldTerrain(seed);
        }
    }
}
