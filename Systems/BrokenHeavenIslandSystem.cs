using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.WorldBuilding;
using Terraria.GameContent.Generation;
using System;
using System.IO;

namespace AncientChineseMythology.Systems
{
    public class BrokenHeavenIslandSystem : ModSystem
    {
        public static Rectangle IslandRect; 
        public static bool unlockedSkyIsland;
        public static Point SkySpawnTile;
        public override void OnWorldLoad()  => unlockedSkyIsland = false;
        public override void OnWorldUnload()=> unlockedSkyIsland = false;

        // 传入 totalWeight 的类型为 double:contentReference[oaicite:0]{index=0}  
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            // 把 Pass 插到 “Micro Biomes” 后；若找不到就加到末尾
            int idx = tasks.FindIndex(p => p.Name.Equals("Micro Biomes"));
            if (idx < 0) idx = tasks.Count - 1;

            tasks.Insert(idx + 1,
                new PassLegacy("Broken Heaven Island",
                    (GenerationProgress prog, GameConfiguration conf) =>
                        GenerateIslandPass(prog, conf)));   // 只保留这一处注册
        }

        /* ───────────────────────── 主生成逻辑 ───────────────────────── */
        private void GenerateIslandPass(GenerationProgress progress, GameConfiguration _)
        {
            const int topW = 480, botW = 240, h = 80;
            const int pillarW = 15, cloudSkin = 4;

            // ── 1. 选随机空域坐标 ─────────────────────────────────────────
            int cx, cyTop;
            int tries = 0;
            do {
                cx    = WorldGen.genRand.Next(Main.maxTilesX / 10, Main.maxTilesX * 9 / 10);
                cyTop = (int)(Main.worldSurface * WorldGen.genRand.NextFloat(0.25f, 0.35f));
            } while (++tries < 200 &&
                    !GenVars.structures.CanPlace(new Rectangle(cx - topW/2, cyTop, topW, h), 0));

            IslandRect = new Rectangle(cx - topW / 2 - cloudSkin, cyTop - cloudSkin,
                                    topW + cloudSkin * 2, h + cloudSkin * 2 + 60);
            GenVars.structures.AddProtectedStructure(IslandRect);

            // ── 2. 填主体（泥 / 石 / 金，外 2 格无墙）────────────────────
            for (int row = 0; row < h; row++) {
                float t = row / (float)h;
                int rowW = Math.Max( 
                    topW - (int)((topW - botW) * MathF.Pow(t, 1.5f)),
                    botW);
                int left = cx - rowW / 2, right = left + rowW - 1;
                int y = cyTop + row;

                double depth = row / (double)h;
                double dirtRate = MathHelper.Lerp(0.8f, 0.55f, (float)depth);
                double oreRate  = MathHelper.Lerp(0.05f, 0.35f, (float)depth);

                for (int x = left; x <= right; x++) {
                    ushort tile = (ushort)(WorldGen.genRand.NextDouble() < dirtRate
                                ? ModContent.TileType<Tiles.Placable.CloudyGoldSand>()
                                : (WorldGen.genRand.NextDouble() < oreRate ? TileID.Gold : ModContent.TileType<Tiles.Placable.FloatingBasalt>()));
                    WorldGen.PlaceTile(x, y, tile, mute:true, forced:true);

                    bool edge = (x - left) < 2 || (right - x) < 2 || row < 2 || row > h - 3;
                    if (!edge) Main.tile[x, y].WallType = WallID.Cloud;
                }
            }

            // ── 4. 挖中央洞 + 宝箱 ───────────────────────────────────────
            int caveX = cx, caveY = cyTop + h / 2;
            CarveEllipse(caveX, caveY, 16, 12, WallID.Cloud);
            WorldGen.AddBuriedChest(
            caveX, caveY,
            contain: ItemID.CopperShortsword,
            notNearOtherChests: false);

            // ── 5. 随机附加洞穴与通道（逻辑与上一版相同，函数重用）─────────
            CarveRandomCavesAndTunnels(cx, cyTop, topW, botW, h, 24, 32);

            // ── 6. 两根通天柱（最外侧）──────────────────────────────────
            int[] pillarCX = { cx - topW / 2 + pillarW / 2 + 2,
                            cx + topW / 2 - pillarW / 2 - 2 };

            foreach (int pcx in pillarCX) {
                for (int x = pcx - pillarW / 2; x <= pcx + pillarW / 2; x++)
                    for (int y = 0; y < cyTop - 6; y++) {
                        ushort t = (ushort)(WorldGen.genRand.NextBool(3) ? TileID.Gold : ModContent.TileType<Tiles.Placable.FloatingBasalt>());
                        WorldGen.PlaceTile(x, y, t, mute:true, forced:true);

                        bool edge = Math.Abs(x - pcx) >= pillarW / 2 - 1;
                        if (!edge) Main.tile[x, y].WallType = WallID.Cloud;
                    }
            }

            // ── 7. 草化顶部 ─────────────────────────────────────────────
            for (int x = IslandRect.Left; x <= IslandRect.Right; x++)
                for (int y = cyTop; y < cyTop + h; y++)
                    if (Main.tile[x, y].HasTile && !Main.tile[x, y - 1].HasTile) {
                        WorldGen.SpreadGrass(x, y, ModContent.TileType<Tiles.Placable.CloudyGoldGrass>(), TileID.Grass);
                        break;
                    }

            SkySpawnTile = new Point(cx, cyTop - 3); // 供 Teleport 用
            progress.Message = "Floating Island finished";
        }

        /* ──────────── 以下为辅助函数（与先前版本一致，仅删去日志） ──────────── */

        private static void CarveEllipse(int cx,int cy,int rx,int ry,int wall) {
            for(int dx=-rx; dx<=rx; dx++)
                for(int dy=-ry; dy<=ry; dy++)
                    if((dx*dx)/(double)(rx*rx)+(dy*dy)/(double)(ry*ry)<=1) {
                        int x=cx+dx, y=cy+dy;
                        WorldGen.KillTile(x,y,false,false);
                        Main.tile[x,y].WallType=(ushort)wall;
                    }
        }

        // 生成额外洞与蛇形通道
        private void CarveRandomCavesAndTunnels(int cx,int topY,int topW,int botW,int h,
                                                int min,int max) {
            int count = WorldGen.genRand.Next(min, max+1);
            List<Point> centers = new();
            for(int i=0;i<count;i++){
                int row = WorldGen.genRand.Next(h/6, h*5/6);
                int rowW = Math.Max(topW - row*3, botW);
                int x = cx - rowW/2 + WorldGen.genRand.Next(rowW);
                int y = topY + row;
                centers.Add(new Point(x,y));
                CarveEllipse(x,y,10,6,WallID.Cloud);
            }
            for(int i=0;i<centers.Count-1;i+=2)
                CarveSnakeTunnel(centers[i], centers[i+1],4);
        }

        private static void CarveSnakeTunnel(Point a,Point b,int r) {
            Vector2 p=a.ToVector2(), t=b.ToVector2();
            while(Vector2.Distance(p,t)>4) {
                p += (t-p).SafeNormalize(Vector2.UnitX)
                    .RotatedByRandom(MathHelper.ToRadians(20))*2;
                for (int dx=-r; dx<=r; dx++)
                    for (int dy=-r; dy<=r; dy++)
                        if(dx*dx+dy*dy<=r*r) WorldGen.KillTile((int)p.X+dx,(int)p.Y+dy,false,false);
            }
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

        private static Vector2 GetSkySpawnPixel()
            => new Vector2(SkySpawnTile.X * 16f + 8f, SkySpawnTile.Y * 16f - 16f);

        public override void SaveWorldData(TagCompound tag)
        {
            tag["SkyUnlocked"] = unlockedSkyIsland;
            tag["Rect"] = IslandRect;
            tag["SkySpawn"] = SkySpawnTile;
        }
        public override void LoadWorldData(TagCompound tag)
        {
            unlockedSkyIsland = tag.GetBool("SkyUnlocked");
            if (tag.ContainsKey("Rect")) IslandRect = tag.Get<Rectangle>("Rect");
            if (tag.ContainsKey("SkySpawn")) SkySpawnTile = tag.Get<Point>("SkySpawn");
        }
        public override void NetSend(BinaryWriter w) {
            w.WriteVector2(SkySpawnTile.ToVector2());
            w.Write(BrokenHeavenIslandSystem.unlockedSkyIsland);
        }
        public override void NetReceive(BinaryReader r) {
            SkySpawnTile = r.ReadVector2().ToPoint();
            BrokenHeavenIslandSystem.unlockedSkyIsland = r.ReadBoolean();
        }
    
    }
}
