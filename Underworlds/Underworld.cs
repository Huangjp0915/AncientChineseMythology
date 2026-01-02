using AncientChineseMythology.Underworlds.Tiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds
{
    internal class Underworld : ModSystem
    {
        public static int UmbralStoneTileID => ModContent.TileType<UmbralStone>();
        [VaultLoaden("{@namespace}/")]
        public static Texture2D Fog;//反射加载雾气灰度纹理

        /// <summary>
        /// 是否正在生成地府地形
        /// </summary>
        public static bool IsGenerating => UnderworldTerrainGenerator.IsGenerating;

        /// <summary>
        /// 当前生成进度 (0-1)
        /// </summary>
        public static float GenerationProgress => UnderworldTerrainGenerator.GenerationProgress;

        /// <summary>
        /// 异步生成地府地形的接口方法（推荐）
        /// 不会阻塞主线程，适合在游戏内调用
        /// </summary>
        /// <param name="seed">可选的随机种子</param>
        /// <param name="onProgress">进度回调 (progress 0-1, stepName)</param>
        /// <param name="onComplete">完成回调 (success)</param>
        public static Task GenerateTerrainAsync(int? seed = null, Action<float, string> onProgress = null, Action<bool> onComplete = null) {
            return UnderworldTerrainGenerator.GenerateUnderworldTerrainAsync(seed, onProgress, onComplete);
        }

        /// <summary>
        /// 取消正在进行的地形生成
        /// </summary>
        public static void CancelGeneration() {
            UnderworldTerrainGenerator.CancelGeneration();
        }

        /// <summary>
        /// 生成地府地形的同步接口方法（会阻塞主线程）
        /// 建议使用 GenerateTerrainAsync 代替
        /// </summary>
        /// <param name="seed">可选的随机种子</param>
        public static void GenerateTerrain(int? seed = null) {
            UnderworldTerrainGenerator.GenerateUnderworldTerrain(seed);
        }
    }
}
