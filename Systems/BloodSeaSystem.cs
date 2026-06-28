using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;

namespace AncientChineseMythology.Systems
{
    /// <summary>
    /// 血海地形系统：在世界创建时于地下洞穴层雕出一个较小的血海盆地（地下血湖）。
    /// 盆地由血海砂外壳包裹、血海墙作背景、下半部灌注血色之水（由 BloodSeaWaterStyle 渲染）。
    /// </summary>
    public class BloodSeaSystem : ModSystem
    {
        /// <summary>每帧由 TileCountsAvailable 更新，供生物群系与怪物判定。</summary>
        public static int NearbyBloodTiles;

        /// <summary>盆地中心 tile 坐标（世界存档持久化），未生成时为 default。</summary>
        public static Point BasinCenter { get; private set; }

        /// <summary>该世界是否已生成血海盆地。</summary>
        public static bool BasinGenerated { get; private set; }

        internal static LocalizedText PassText;

        public override void SetStaticDefaults() {
            PassText = Mod.GetLocalization("WorldGen.BloodSeaBasinPass");
        }

        /* ---------- Biome 判定：统计附近血海砂数量 ---------- */
        public override void TileCountsAvailable(ReadOnlySpan<int> tileCounts) {
            NearbyBloodTiles = tileCounts[ModContent.TileType<Tiles.Placable.BloodSeaSand>()];
        }

        /* ---------- 世界生成：插入地下血海盆地 Pass ---------- */
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight) {
            //在液体沉降之后插入，确保灌入的血水能被随后的 "Settle Liquids Again" 收尾
            int idx = tasks.FindIndex(p => p.Name == "Settle Liquids");
            if (idx == -1) idx = tasks.FindIndex(p => p.Name == "Settle Liquids Again");
            if (idx == -1) idx = tasks.Count - 1;

            tasks.Insert(idx + 1, new PassLegacy(PassText?.Value ?? "Blood Sea Basin", GenerateBloodSeaBasin));
        }

        private void GenerateBloodSeaBasin(GenerationProgress progress, GameConfiguration config) {
            progress.Message = PassText?.Value ?? "Carving the blood sea basin...";
            progress.Value = 0f;

            ushort bloodSand = (ushort)ModContent.TileType<Tiles.Placable.BloodSeaSand>();
            ushort bloodWall = (ushort)ModContent.WallType<Walls.BloodSeaWall>();

            //盆地尺寸（"较小"：宽约 140~190 tile，高约 70~92 tile）
            int halfW = WorldGen.genRand.Next(70, 96);
            int halfH = WorldGen.genRand.Next(35, 47);

            if (!TryFindBasinSite(halfW, halfH, out int cx, out int cy)) {
                progress.Value = 1f;
                return; //找不到合适地点则优雅放弃，不影响世界生成
            }

            CarveBasin(cx, cy, halfW, halfH, bloodSand, bloodWall, progress);

            BasinCenter = new Point(cx, cy);
            BasinGenerated = true;
            progress.Value = 1f;
        }

        /// <summary>在地下石层中寻找一处被实心地块包裹、远离出生点与结构的盆地选址。</summary>
        private static bool TryFindBasinSite(int halfW, int halfH, out int cx, out int cy) {
            cx = cy = 0;
            int minX = (int)(Main.maxTilesX * 0.18f);
            int maxX = (int)(Main.maxTilesX * 0.82f);
            int yMin = (int)Main.rockLayer + halfH + 30;
            int yMax = Main.UnderworldLayer - halfH - 60;
            if (yMax <= yMin) return false;

            for (int attempt = 0; attempt < 240; attempt++) {
                int x = WorldGen.genRand.Next(minX, maxX);
                if (Math.Abs(x - Main.spawnTileX) < halfW + 70) continue; //避开出生点
                int y = WorldGen.genRand.Next(yMin, yMax);

                if (IsSiteSolidEnough(x, y, (int)(halfW * 0.85f), (int)(halfH * 0.85f))) {
                    cx = x;
                    cy = y;
                    return true;
                }
            }
            return false;
        }

        /// <summary>采样选址矩形：要求绝大多数为实心地块，且不含地牢/丛林神庙/地狱石等受保护或越界地块。</summary>
        private static bool IsSiteSolidEnough(int cx, int cy, int rw, int rh) {
            int solid = 0, total = 0;
            for (int x = cx - rw; x <= cx + rw; x += 3) {
                for (int y = cy - rh; y <= cy + rh; y += 3) {
                    if (x < 8 || x >= Main.maxTilesX - 8 || y < 8 || y >= Main.maxTilesY - 8)
                        return false;

                    Tile t = Main.tile[x, y];
                    if (t.HasTile) {
                        int tt = t.TileType;
                        if (tt == TileID.BlueDungeonBrick || tt == TileID.GreenDungeonBrick ||
                            tt == TileID.PinkDungeonBrick || tt == TileID.LihzahrdBrick ||
                            tt == TileID.Hellstone)
                            return false;
                        if (Main.tileSolid[tt])
                            solid++;
                    }
                    total++;
                }
            }
            return total > 0 && solid >= total * 0.72f;
        }

        /// <summary>雕出盆地：内腔清空+血墙，均匀血砂外壳，下半部灌血水，少量钟乳点缀。</summary>
        private static void CarveBasin(int cx, int cy, int halfW, int halfH, ushort sand, ushort wall, GenerationProgress progress) {
            const int shell = 6;
            int waterLineY = cy - (int)(halfH * 0.30f); //平直水面，水占下方约 65%

            //角度扰动种子，让边缘不规则
            float seedA = cx * 0.013f;
            float seedB = cy * 0.017f;
            float seedC = (cx + cy) * 0.007f;

            int x0 = cx - halfW - shell - 4;
            int x1 = cx + halfW + shell + 4;
            int y0 = cy - halfH - shell - 4;
            int y1 = cy + halfH + shell + 4;

            int span = Math.Max(1, x1 - x0);
            for (int x = x0; x <= x1; x++) {
                if ((x & 7) == 0)
                    progress.Value = (x - x0) / (float)span;
                if (x < 6 || x >= Main.maxTilesX - 6) continue;

                for (int y = y0; y <= y1; y++) {
                    if (y < 6 || y >= Main.maxTilesY - 6) continue;

                    float dx = x - cx;
                    float dy = y - cy;
                    float ang = (float)Math.Atan2(dy, dx);
                    float wobble = 1f
                        + 0.12f * (float)Math.Sin(ang * 3f + seedA)
                        + 0.07f * (float)Math.Sin(ang * 7f - seedB)
                        + 0.05f * (float)Math.Sin(ang * 5f + seedC);

                    float erx = halfW * wobble;
                    float ery = halfH * wobble;

                    float dIn = (dx * dx) / (erx * erx) + (dy * dy) / (ery * ery);
                    float oerx = erx + shell;
                    float oery = ery + shell;
                    float dOut = (dx * dx) / (oerx * oerx) + (dy * dy) / (oery * oery);

                    Tile t = Main.tile[x, y];
                    if (dIn <= 1f) {
                        //内腔：清空地块，挂血海墙；水线以下灌血水
                        t.ClearTile();
                        t.WallType = wall;
                        if (y >= waterLineY) {
                            t.LiquidType = LiquidID.Water;
                            t.LiquidAmount = 255;
                        }
                        else {
                            t.LiquidAmount = 0;
                        }
                    }
                    else if (dOut <= 1f) {
                        //外壳：强制血海砂，背景血海墙
                        t.ClearTile();
                        t.HasTile = true;
                        t.TileType = sand;
                        t.WallType = wall;
                        t.LiquidAmount = 0;
                    }
                }
            }

            DecorateBasin(cx, cy, halfW, halfH, waterLineY, sand);

            //重新计算地块/墙的拼接帧
            WorldGen.RangeFrame(
                Math.Max(1, x0 - 1), Math.Max(1, y0 - 1),
                Math.Min(Main.maxTilesX - 1, x1 + 1), Math.Min(Main.maxTilesY - 1, y1 + 1));
        }

        /// <summary>顶部血砂钟乳 + 底部矮礁，增添观感。</summary>
        private static void DecorateBasin(int cx, int cy, int halfW, int halfH, int waterLineY, ushort sand) {
            int stalactites = WorldGen.genRand.Next(6, 11);
            for (int n = 0; n < stalactites; n++) {
                int x = cx + WorldGen.genRand.Next(-(int)(halfW * 0.62f), (int)(halfW * 0.62f) + 1);
                if (x < 6 || x >= Main.maxTilesX - 6) continue;

                //自内腔顶部向下找到第一格空气
                int topY = cy - halfH;
                int y = topY;
                while (y < waterLineY && Main.tile[x, y].HasTile) y++;
                if (y >= waterLineY) continue;

                int len = WorldGen.genRand.Next(3, 8);
                for (int k = 0; k < len && y + k < waterLineY; k++) {
                    Tile tt = Main.tile[x, y + k];
                    if (tt.LiquidAmount > 0) break;
                    tt.ClearTile();
                    tt.HasTile = true;
                    tt.TileType = sand;
                }
            }
        }

        /* ---------- 持久化 ---------- */
        public override void SaveWorldData(TagCompound tag) {
            tag["bloodSeaBasinGenerated"] = BasinGenerated;
            tag["bloodSeaBasinX"] = BasinCenter.X;
            tag["bloodSeaBasinY"] = BasinCenter.Y;
        }

        public override void LoadWorldData(TagCompound tag) {
            BasinGenerated = tag.GetBool("bloodSeaBasinGenerated");
            BasinCenter = new Point(tag.GetInt("bloodSeaBasinX"), tag.GetInt("bloodSeaBasinY"));
        }

        public override void ClearWorld() {
            BasinGenerated = false;
            BasinCenter = default;
            NearbyBloodTiles = 0;
        }
    }
}
