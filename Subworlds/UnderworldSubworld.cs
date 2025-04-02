using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;  // for Color
using Terraria;
using Terraria.ID;
using Terraria.WorldBuilding;
using Terraria.IO;
using SubworldLibrary;

namespace AncientChineseMythology.Subworlds
{
    public class UnderworldSubworld : Subworld
    {
        // 子世界尺寸，可自由调整
        public override int Width => 800;
        public override int Height => 400;
        public override bool ShouldSave => true ;
        public override bool NoPlayerSaving => false;

        // 记录玩家出生点
        internal static int SpawnX = 0;
        internal static int SpawnY = 0;

        // 记录最高山峰的最小 peakY，用于计算岩浆上限
        internal static int HighestMountainPeakY = 999999; // 或 int.MaxValue

        public override List<GenPass> Tasks => new List<GenPass>()
        {
            new ClearPass(),              // Pass1: 清空
            new GenerateMountainsPass(),  // Pass2: 生成圆润山脉 & 记录 HighestMountainPeakY
            new FillMagmaUpToOneThirdPass(), // Pass3: 只填充到最高山峰 1/3 处
            new FinalFramePass(),         // Pass4: 刷新方块帧
        };

        public override void OnLoad()
        {
            Main.dayTime = true;
            Main.time = 13500; // 中午

            // 仅影响地图UI显示
            Main.worldSurface = Height * 0.3;
            Main.rockLayer = Height * 0.5;

            
        }

        public override void OnEnter()
        {
            base.OnEnter();
            if (Main.netMode != NetmodeID.Server)
            {
                Main.NewText("进入子世界：地府", Color.OrangeRed);
            }
        }
    }

    /// <summary>
    /// Pass1：清空子世界地形/墙/液体
    /// </summary>
    public class ClearPass : GenPass
    {
        public ClearPass() : base("ClearUnderworld", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "清空地形...";
            int width = ((Subworld)SubworldSystem.Current).Width;
            int height = ((Subworld)SubworldSystem.Current).Height;

            Tile empty = new Tile();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Main.tile[x, y].CopyFrom(empty);
                }
            }
        }
    }

    /// <summary>
    /// Pass2：生成多座圆润肥胖的山，并记录最高山峰(peakY最小)
    ///       -> 用来计算岩浆高度1/3限制
    /// </summary>
    public class GenerateMountainsPass : GenPass
    {
        public GenerateMountainsPass() : base("GenRoundMountains", 1f) { }

        private List<Point> peaks = new List<Point>();

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "生成圆润山脉...";

            int width = ((Subworld)SubworldSystem.Current).Width;
            int height = ((Subworld)SubworldSystem.Current).Height;

            // 随机 4~7 座山
            int mountainCount = Main.rand.Next(4, 8);

            // 出生点峰
            int chosenPeakX = width / 2;
            int chosenPeakY = height / 2;

            int attempts = 0;
            for (int i = 0; i < mountainCount; i++)
            {
                bool placed = false;
                for (int tries = 0; tries < 20; tries++)
                {
                    attempts++;
                    if (attempts > 200) break;

                    // 随机峰顶
                    int peakX = Main.rand.Next(40, width - 40);
                    int peakY = height - Main.rand.Next(90, 141);
                    if (peakY < 0) peakY = 0;

                    // 与已有山峰距离判定
                    float minDist = 80f;
                    bool tooClose = false;
                    foreach (Point p in peaks)
                    {
                        float dx = p.X - peakX;
                        float dy = p.Y - peakY;
                        float dist = (float)Math.Sqrt(dx*dx + dy*dy);
                        if (dist < minDist)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;

                    // 放置此山
                    GenerateOneRoundMountain(peakX, peakY);
                    peaks.Add(new Point(peakX, peakY));

                    // 记录最小 peakY
                    if (peakY < UnderworldSubworld.HighestMountainPeakY)
                        UnderworldSubworld.HighestMountainPeakY = peakY;

                    chosenPeakX = peakX;
                    chosenPeakY = peakY;
                    placed = true;
                    break;
                }
                if (!placed) break;
            }

            // 用最后成功的山峰当出生点
            UnderworldSubworld.SpawnX = chosenPeakX;
            UnderworldSubworld.SpawnY = chosenPeakY;
        }

        /// <summary>
        /// 单座圆润山: ratio^(exponent<1) => 圆顶
        /// </summary>
        private void GenerateOneRoundMountain(int peakX, int peakY)
        {
            int width = ((Subworld)SubworldSystem.Current).Width;
            int height = ((Subworld)SubworldSystem.Current).Height;

            int bottomY = height - 1;
            int mountainHeight = bottomY - peakY;
            if (mountainHeight < 1) return;

            // slope => 越大越胖
            float slope = Main.rand.NextFloat(0.4f, 0.6f);
            // exponent < 1 => 更圆
            float exponent = Main.rand.NextFloat(0.3f, 0.5f);

            for (int y = bottomY; y >= peakY; y--)
            {
                int dist = y - peakY; 
                float ratio = (float)dist / mountainHeight; // 0 ~ 1
                // 小于1 => 上部展开更快
                float curve = (float)Math.Pow(ratio, exponent);

                float baseRadius = slope * mountainHeight * curve;

                int halfWidth = (int)baseRadius;
                if (halfWidth < 0) halfWidth = 0;

                int leftX = peakX - halfWidth;
                int rightX = peakX + halfWidth;

                if (leftX < 0) leftX = 0;
                if (rightX >= width) rightX = width - 1;

                // 用Ash堆山
                for (int x = leftX; x <= rightX; x++)
                {
                    WorldGen.PlaceTile(x, y, TileID.Ash, forced: true);
                }
            }
        }
    }

    

    /// <summary>
    /// Pass4：只将岩浆填充到最高山峰的 1/3 处以下 (非山体)
    /// </summary>
    public class FillMagmaUpToOneThirdPass : GenPass
    {
        public FillMagmaUpToOneThirdPass() : base("FillMagmaOneThird", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "填充岩浆至最高山脉的 1/3 处...";

            int width = ((Subworld)SubworldSystem.Current).Width;
            int height = ((Subworld)SubworldSystem.Current).Height;
            int bottomY = height - 1;

            // 找到最高山脉的 peakY
            int highestPeakY = UnderworldSubworld.HighestMountainPeakY;
            if (highestPeakY > bottomY) highestPeakY = bottomY; // 安全

            // 山高
            int mountainHeight = bottomY - highestPeakY;
            if (mountainHeight < 1) return;

            // lavaTop = highestPeakY + (mountainHeight / 3)
            // 只填充 [lavaTop..bottomY]
            int lavaTop = highestPeakY + mountainHeight / 3;

            // 做一个"满岩浆"的 Tile
            Tile lavaTile = new Tile
            {
                HasTile = false,
                LiquidType = LiquidID.Lava,
                LiquidAmount = 255,
            };

            // 开始填充
            for (int x = 0; x < width; x++)
            {
                for (int y = lavaTop; y <= bottomY; y++)
                {
                    // 若此处没有方块 => 填岩浆
                    if (!Main.tile[x, y].HasTile)
                    {
                        Main.tile[x, y].CopyFrom(lavaTile);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Pass5：刷新方块帧
    /// </summary>
    public class FinalFramePass : GenPass
    {
        public FinalFramePass() : base("FinalFrameUnderworld", 1f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "刷新方块帧...";
            int width = ((Subworld)SubworldSystem.Current).Width;
            int height = ((Subworld)SubworldSystem.Current).Height;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    WorldGen.SquareTileFrame(x, y, true);
                }
            }
        }
    }
}
