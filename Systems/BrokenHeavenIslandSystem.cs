using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;
using Terraria.GameContent.Generation;
using Terraria.DataStructures;   // PassLegacy & GenerationProgress
using static StructureHelper.API.Generator;
using StructureHelper;
using Terraria.GameContent;
using System.Reflection;
using System.Collections;
using System;
using Terraria.Utilities;   // 省掉类名前缀

namespace AncientChineseMythology.Systems
{
    public class BrokenHeavenIslandSystem : ModSystem
    {
        public static Rectangle IslandRect;   // 供后续屏障 / 传送逻辑使用
        public static bool unlockedSkyIsland;
        public static Point SkySpawnTile;
        public override void OnWorldLoad()  => unlockedSkyIsland = false;
        public override void OnWorldUnload()=> unlockedSkyIsland = false;
        private const float OreChanceMain = 0.2f; // 主岛：8 %
        private const float OreChanceSat  = 0.1f; // 卫星：6 %
        private const ushort OreType = TileID.Gold; // 以后可改成 ModContent.TileType<MythOre>()

        // 预览版：传入 totalWeight 的类型为 double:contentReference[oaicite:0]{index=0}  
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            // 把 Pass 插到 “Micro Biomes” 后；若找不到就加到末尾
            int idx = tasks.FindIndex(p => p.Name.Equals("Micro Biomes"));
            if (idx == -1) idx = tasks.Count - 1;
            tasks.Insert(idx + 1, new PassLegacy("Broken Heaven Island", GenerateIslandPass));
        }

        /* ───────────────────────── 主生成逻辑 ───────────────────────── */
        private void GenerateIslandPass(GenerationProgress progress, GameConfiguration _)
        {
            progress.Message = "铸造破碎的小天庭…";

            /* ── 1. 主岛中心与尺寸 ── */
            int cx = Main.maxTilesX / 2 + WorldGen.genRand.Next(-200, 201);
            int cy = (int)(Main.worldSurface * 0.42f);          // 略高于浮岛
            const int mainW = 110;        // 主岛宽
            const int mainH = 28;         // 主岛高
            UnifiedRandom rand = WorldGen.genRand;

            /* ── 2. 主岛：上平下鼓的“原版浮岛”形状 ── */
            for (int dx = -mainW / 2; dx <= mainW / 2; dx++)
            for (int dy = 0; dy <= mainH; dy++)
            {
                float nx = dx / (mainW * 0.55f);
                float ny = dy / (mainH * 1.0f);

                if (nx * nx + ny * ny <= 1f + rand.NextFloat(-0.15f, 0.15f))
                {
                    ushort tileType;

                    // 只在下 75 % 区域按概率生成矿石
                    if (dy > mainH * 0.25f &&
                        rand.NextFloat() < OreChanceMain * MathHelper.Lerp(0.3f, 1f, ny))
                    {
                        tileType = OreType;
                    }
                    else
                    {
                        tileType = rand.NextFloat() < 0.3f
                                   ? (ushort)ModContent.TileType<Tiles.Placable.CloudyGoldSand>()
                                   : (ushort)ModContent.TileType<Tiles.Placable.FloatingBasalt>();
                    }

                    WorldGen.PlaceTile(cx + dx, cy + dy, tileType, mute: true, forced: true);
                }
            }

            /* ────────── 2. 卫星浮岛 (3-4 个) ────────── */
            int satellites = rand.Next(3, 5);
            for (int n = 0; n < satellites; n++)
            {
                double ang = MathHelper.ToRadians(-60 + n * 120 / satellites + rand.NextFloat(-12, 12));
                int hx = cx + (int)(rand.Next(36, 65) * System.Math.Cos(ang));
                int hy = cy - rand.Next(24, 39);

                int w = rand.Next(32, 49);
                int h = rand.Next(10, 17);

                for (int dx = -w / 2; dx <= w / 2; dx++)
                for (int dy = 0; dy <= h; dy++)
                {
                    float nx = dx / (w * 0.55f);
                    float ny = dy / (h * 1.0f);

                    if (nx * nx + ny * ny <= 1f + rand.NextFloat(-0.15f, 0.15f))
                    {
                        ushort tileType;

                        if (dy > h * 0.25f &&
                            rand.NextFloat() < OreChanceSat * MathHelper.Lerp(0.3f, 1f, ny))
                        {
                            tileType = OreType;
                        }
                        else
                        {
                            tileType = rand.NextFloat() < 0.6f
                                       ? (ushort)ModContent.TileType<Tiles.Placable.CloudyGoldSand>()
                                       : (ushort)ModContent.TileType<Tiles.Placable.FloatingBasalt>();
                        }

                        WorldGen.PlaceTile(hx + dx, hy + dy, tileType, mute: true, forced: true);
                    }
                }
            }

            /* ────────── 3. 结构保护矩形 ────────── */
            Rectangle islandRect = new Rectangle(
                cx - mainW / 2 - 10,
                cy - mainH - 20,
                mainW + 20,
                mainH + 40);
            GenVars.structures.AddProtectedStructure(islandRect);


            /* ── 4. 给所有石顶铺草 ── */
            SpreadSurfaceGrass(cx, cy, mainW / 2 + 48, mainH + 20);

            /* ── 5. 一些裂谷、洞、碎块、钟乳石 ── */
            Vector2 center = new(cx, cy + mainH / 2);
            for (int crack = 0; crack < 6; crack++)
            {
                Vector2 p1 = center + (Vector2.UnitX * (mainW / 2)).RotatedByRandom(MathHelper.TwoPi);
                Vector2 p2 = center + (Vector2.UnitX * (mainW / 2)).RotatedByRandom(MathHelper.TwoPi);
                CarveTunnel(p1.ToPoint(), p2.ToPoint(), WorldGen.genRand.Next(2, 4));
            }
            for (int hole = 0; hole < 4; hole++)
                CarveCircle(new Point(cx + WorldGen.genRand.Next(-20, 21),
                                    cy + WorldGen.genRand.Next(8, 18)),
                            WorldGen.genRand.Next(4, 7));

            //GenerateFragments(cx, cy, mainW / 2 + 40, mainH + 20);
            GenerateStalactites(cx, cy + 6, mainW / 2 + 10, mainH + 20);

            /* ── 6. 放置建筑与装饰 ── */
            BuildRuins(cx, cy);

            /* ── 7. 添加预制空岛 ── */
            string[] prefabPaths =
            {
                "structures/floatingisland",
                "structures/skyisland",
                "structures/skypalace1",
                "structures/skypalace2"
            };
            int prefabCount = WorldGen.genRand.Next(1, 3);
            HashSet<int> used = new();

            for (int n = 0; n < prefabCount; n++)
            {
                int idx;
                do { idx = WorldGen.genRand.Next(prefabPaths.Length); } while (!used.Add(idx));
                string path = prefabPaths[idx];

                Point16 dim = GetStructureDimensions(path, Mod, false);
                int w = dim.X, h = dim.Y;

                // 贴边锚点：水平 4–10 格，垂直 4–10 格
                int offsetX = WorldGen.genRand.NextBool()
                    ? -(w + WorldGen.genRand.Next(4, 11))
                    :  (mainW / 2) + WorldGen.genRand.Next(4, 11);
                int offsetY = -WorldGen.genRand.Next(4, 11);

                int placeX = Utils.Clamp(cx + offsetX, 10, Main.maxTilesX - w - 10);
                int placeY = Utils.Clamp(cy + offsetY - h, 10, (int)Main.worldSurface - h - 10);

                GenerateStructure(path, new Point16(placeX, placeY), Mod, false, false, GenFlags.None);

                // 并入屏障并立即登记保护
                Rectangle preRect = new Rectangle(placeX, placeY, w, h);

                RemoveRoomsInside(preRect);

                IslandRect = Rectangle.Union(IslandRect, preRect);
                GenVars.structures.AddProtectedStructure(preRect);
            }

            SkySpawnTile = new Point(cx, cy - 1);
        }

        public static Vector2 GetSkySpawnPixel()
        {
            int tx = SkySpawnTile.X;
            // 从圆台正上向上找 4 格空气（保证头顶空间）
            for (int y = SkySpawnTile.Y; y > 10; y--)
            {
                bool solidBelow = Main.tile[tx, y + 1].HasTile && Main.tileSolid[Main.tile[tx, y + 1].TileType];
                bool headClear  = !(Main.tile[tx, y - 1].HasUnactuatedTile ||
                                    Main.tile[tx, y - 2].HasUnactuatedTile);
                if (!Main.tile[tx, y].HasUnactuatedTile && solidBelow && headClear)
                    return new Vector2((tx + 0.5f) * 16f, (y - 1) * 16f);
            }
            // 找不到就退到出生点
            return new Vector2(Main.spawnTileX * 16f, (Main.spawnTileY - 3) * 16f);
        }

        /* ──────────── 以下为辅助函数（与先前版本一致，仅删去日志） ──────────── */

        private static void CarveTunnel(Point start, Point end, int r)
        {
            int steps = 1 + (int)Vector2.Distance(start.ToVector2(), end.ToVector2());
            Vector2 cur = start.ToVector2();
            Vector2 dir = (end.ToVector2() - cur) / steps;

            for (int i = 0; i <= steps; i++)
            {
                CarveCircle(cur.ToPoint(), r);
                cur += dir + Main.rand.NextVector2CircularEdge(1f, 1f);  // 轻微抖动
            }
        }

        private static void CarveCircle(Point c, int r)
        {
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                int i = c.X + dx, j = c.Y + dy;
                if (!WorldGen.InWorld(i, j)) continue;
                if (dx * dx + dy * dy <= r * r)
                    Main.tile[i, j].ClearEverything();
            }
        }

        private static void GenerateFragments(int cx, int cy, int xRadius, int yRadius)
        {
            // 生成 12~18 个迷你浮岛
            int islands = WorldGen.genRand.Next(12, 19);
            for (int k = 0; k < islands; k++)
            {
                double ang = MathHelper.ToRadians(WorldGen.genRand.Next(-70, 71)); // 仅上半环
                int    dist = xRadius + WorldGen.genRand.Next(6, 14);
                int    icx = cx + (int)(dist * System.Math.Cos(ang));
                int    icy = cy + (int)(dist * System.Math.Sin(ang));

                int r = WorldGen.genRand.Next(3, 6);   // 半径 3~5 —— 实心小圆岛
                for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                    if (dx * dx + dy * dy <= r * r && WorldGen.InWorld(icx + dx, icy + dy))
                        WorldGen.PlaceTile(icx + dx, icy + dy, ModContent.TileType<Tiles.Placable.FloatingBasalt>(), mute:true, forced:true);
            }
        }

        private static void GenerateStalactites(int cx, int cy, int a, int b)
        {
            for (int x = cx - a; x <= cx + a; x++)
            {
                int y = cy + b;
                while (WorldGen.InWorld(x, y) && Main.tile[x, y].HasTile) y++;
                if (!WorldGen.InWorld(x, y) || !Main.tile[x, y - 1].HasTile) continue;

                if (WorldGen.genRand.NextBool(7))
                {
                    int len = WorldGen.genRand.Next(4, 9);
                    for (int l = 0; l < len && WorldGen.InWorld(x, y + l); l++)
                        WorldGen.PlaceTile(x, y + l, ModContent.TileType<Tiles.Placable.FloatingBasalt>(), mute: true, forced: true);
                }
            }
        }

        private static void SpreadSurfaceGrass(int cx, int cy, int a, int b)
        {
            for (int x = cx - a - 4; x <= cx + a + 4; x++)
            for (int y = cy - b - 4; y <= cy + b + 4; y++)
            {
                if (!WorldGen.InWorld(x, y, 10) || !IslandRect.Contains(x, y)) continue;
                if (Main.tile[x, y].TileType == ModContent.TileType<Tiles.Placable.FloatingBasalt>() && !Main.tile[x, y - 1].HasTile)
                {
                    WorldGen.PlaceTile(x, y, ModContent.TileType<Tiles.Placable.CloudyGoldSand>(), mute: true, forced: true);
                    WorldGen.SpreadGrass(x, y, ModContent.TileType<Tiles.Placable.CloudyGoldSand>(), TileID.Grass, repeat: false); // SpreadGrass:contentReference[oaicite:4]{index=4}
                }
            }
        }

        private static void BuildRuins(int cx, int cy)
        {
            int pr = 9; // 圆台半径

            /* —— 祥云砖圆台 —— */
            for (int dx = -pr; dx <= pr; dx++)
            for (int dy = -pr; dy <= pr; dy++)
                if (dx * dx + dy * dy <= pr * pr)
                    WorldGen.PlaceTile(cx + dx, cy + dy, ModContent.TileType<Tiles.Placable.CelestialJadeBrick>(), mute: true, forced: true);

            /* 缺口锯齿 */
            for (int i = 0; i < 28; i++)
                Main.tile[cx + WorldGen.genRand.Next(-pr, pr + 1),
                        cy + WorldGen.genRand.Next(-pr, pr + 1)].ClearEverything();

            /* —— 残破房顶 + 左右断墙 —— */
            int left = cx - 8, right = cx + 8, roofY = cy - 5;
            for (int x = left; x <= right; x++)
                WorldGen.PlaceTile(x, roofY, ModContent.TileType<Tiles.Placable.CelestialJadeBrick>(), mute: true, forced: true);
            for (int y = roofY - 1; y >= roofY - 4; y--)
            {
                WorldGen.PlaceTile(left,  y, ModContent.TileType<Tiles.Placable.CelestialJadeBrick>(), mute: true, forced: true);
                if (y != roofY - 2) // 右墙缺口
                    WorldGen.PlaceTile(right, y, ModContent.TileType<Tiles.Placable.CelestialJadeBrick>(), mute: true, forced: true);
            }

            /* —— 断柱 —— */
            foreach (int off in new[] { -12, -6, 6, 12 })
            {
                int h = WorldGen.genRand.Next(5, 8);
                for (int y = 0; y < h; y++)
                    WorldGen.PlaceTile(cx + off, cy - 1 - y, ModContent.TileType<Tiles.Placable.CelestialJadeBrick>(), mute: true, forced: true);
                if (WorldGen.genRand.NextBool())
                    Main.tile[cx + off, cy - h].ClearEverything();
            }

            /* —— 植被：树 & 高草 —— */
            for (int t = 0; t < 8; t++)                                              // 树
            {
                int tx = cx + WorldGen.genRand.Next(-pr - 6, pr + 7);
                int ty = cy - WorldGen.genRand.Next(2, 6);
                if (Main.tile[tx, ty].TileType == TileID.Grass)
                    WorldGen.GrowTree(tx, ty);
            }
            for (int f = 0; f < 12; f++)                                             // 高草
            {
                int fx = cx + WorldGen.genRand.Next(-pr - 10, pr + 11);
                int fy = cy - WorldGen.genRand.Next(1, 5);
                if (Main.tile[fx, fy].TileType == TileID.Grass && !Main.tile[fx, fy - 1].HasTile)
                    WorldGen.PlaceTile(fx, fy - 1, TileID.Plants, mute: true, forced: true);
            }
        }

        public static void RemoveRoomsInside(Rectangle rect)
        {
            // -------- 1. 取到 townRoomManager 单例 --------
            var mgrField = typeof(Main).GetField("townRoomManager",
                            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (mgrField == null)  // 极旧版（1.3-legacy）根本没有房屋管理器
                return;

            object manager = mgrField.GetValue(null);
            if (manager == null)
                return;

            Type mgrType = manager.GetType();

            // -------- 2. 新版优先：直接调 DeleteRoomsInArea --------
            var direct = mgrType.GetMethod("DeleteRoomsInArea", new[] { typeof(Rectangle) });
            if (direct != null) {               // 1.4.4+ 有这个方法
                direct.Invoke(manager, new object[] { rect });
                return;
            }

            // -------- 3. 旧版回退：自己删字典 --------
            var roomsField = mgrType.GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic);
            if (roomsField == null) return;

            var rooms = roomsField.GetValue(manager) as IDictionary;   //  key: Point16  value: int
            if (rooms == null) return;

            List<object> toRemove = new();
            foreach (DictionaryEntry entry in rooms) {
                Point16 p = (Point16)entry.Key;
                if (rect.Contains(p.X, p.Y))
                    toRemove.Add(entry.Key);
            }
            foreach (var key in toRemove)
                rooms.Remove(key);
        }

        public static void OpenSkyIsland(int who)
        {
            Vector2 pos = GetSkySpawnPixel();
            Player pl = Main.player[who];
            pl.Teleport(pos, TeleportationStyleID.RodOfDiscord);
            NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, who, pos.X, pos.Y, 1);
            // 给本人 60 帧免疫屏障
            pl.GetModPlayer<BrokenHeavenBarrierPlayer>().barrierTimer = 60;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            tag["SkyUnlocked"] = unlockedSkyIsland;
            tag["Rect"] = IslandRect;
        }
        public override void LoadWorldData(TagCompound tag)
        {
            unlockedSkyIsland = tag.GetBool("SkyUnlocked");
            if (tag.ContainsKey("Rect")) IslandRect = tag.Get<Rectangle>("Rect");
        }
    
    }
}
