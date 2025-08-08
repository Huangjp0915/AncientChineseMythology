using Microsoft.Xna.Framework;
using StructureHelper.API;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AncientChineseMythology.Systems
{
    public class BrokenHeavenIslandSystem : ModSystem
    {
        public static bool HeavenPlaced { get; private set; }
        public static Rectangle HeavenRect;            // 进入此矩形则触发 HeavenBiome

        /* ── 生命周期 ───────────────────────── */
        public override void OnWorldLoad() => HeavenPlaced = false;
        public override void OnWorldUnload() => HeavenPlaced = false;

        /* ── 每帧检测：服务器端击败 ML → 生成建筑 ── */
        public override void PreUpdateWorld() {
            //if (HeavenPlaced || !NPC.downedMoonlord ||
            //    Main.netMode == NetmodeID.MultiplayerClient)
            //    return;

            //PlaceHeavenStructure();
            //HeavenPlaced = true;

            //if (Main.netMode == NetmodeID.Server)
            //    NetMessage.SendData(MessageID.WorldData);
        }

        /* ── 把 sky.shstruct 插进世界 ──────────── */
        private static void PlaceHeavenStructure() {
            Mod mod = ModContent.GetInstance<AncientChineseMythology>();

            // 1) 用 Generator API 先读取尺寸
            Point16 size = Generator.GetStructureDimensions("structures/sky", mod);
            int w = size.X;
            int h = size.Y;

            // 2) 在 worldSurface 30~40% 高度随机尝试无障碍区域
            int yMin = (int)(Main.worldSurface * 0.30);
            int yMax = (int)(Main.worldSurface * 0.40);

            for (int i = 0; i < 200; i++) {
                int xTry = WorldGen.genRand.Next(200, Main.maxTilesX - w - 200);
                int yTry = WorldGen.genRand.Next(yMin, yMax);

                if (!AreaHasSolid(new Rectangle(xTry, yTry, w, h))) {
                    Generator.GenerateStructure("structures/sky",
                        new Point16(xTry, yTry), mod);
                    FinishPlacement(xTry, yTry, w, h);
                    return;
                }
            }

            // 3) 200 次失败 ⇒ 塞在水平中心
            int xMid = Main.maxTilesX / 2 - w / 2;
            int yMid = (yMin + yMax) / 2;
            Generator.GenerateStructure("structures/sky",
                new Point16(xMid, yMid), mod);
            FinishPlacement(xMid, yMid, w, h);
        }

        private static bool AreaHasSolid(Rectangle area) {
            for (int x = area.Left; x < area.Right; x++)
                for (int y = area.Top; y < area.Bottom; y++)
                    if (Main.tile[x, y].HasTile)
                        return true;
            return false;
        }

        private static void FinishPlacement(int x, int y, int w, int h) {
            HeavenRect = new Rectangle(x, y, w, h);

            if (Main.netMode != NetmodeID.Server)
                Main.NewText("残破天庭在高空中重现！",
                             200, 225, 255);
        }

        /* ── 存档 & 读取 ─────────────────────── */
        public override void SaveWorldData(TagCompound tag) {
            if (!HeavenPlaced) return;                 // 没生成就不保存

            tag["HeavenPlaced"] = true;
            tag["HeavenRect"] = new List<int> {
                HeavenRect.X, HeavenRect.Y,
                HeavenRect.Width, HeavenRect.Height
            };
        }

        public override void LoadWorldData(TagCompound tag) {
            HeavenPlaced = tag.GetBool("HeavenPlaced");
            if (HeavenPlaced && tag.TryGet<List<int>>("HeavenRect", out var l) && l.Count == 4)
                HeavenRect = new Rectangle(l[0], l[1], l[2], l[3]);
        }

        /* ── 联机同步 ─────────────────────────── */
        public override void NetSend(BinaryWriter w) {
            w.Write(HeavenPlaced);
            if (!HeavenPlaced) return;

            w.Write(HeavenRect.X);
            w.Write(HeavenRect.Y);
            w.Write(HeavenRect.Width);
            w.Write(HeavenRect.Height);
        }

        public override void NetReceive(BinaryReader r) {
            HeavenPlaced = r.ReadBoolean();
            if (!HeavenPlaced) return;

            int x = r.ReadInt32();
            int y = r.ReadInt32();
            int w = r.ReadInt32();
            int h = r.ReadInt32();
            HeavenRect = new Rectangle(x, y, w, h);
        }
    }
}
