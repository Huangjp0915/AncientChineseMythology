using AncientChineseMythology.Tiles.Placable;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AncientChineseMythology.Systems
{
    public class PostMoonLordSystem : ModSystem
    {
        public static bool MoonLordDefeated = false;

        public override void Load()
        {
            On_NPC.NPCLoot += OnNPCLoot;
        }

        public override void Unload()
        {
            On_NPC.NPCLoot -= OnNPCLoot;
        }

        private void OnNPCLoot(On_NPC.orig_NPCLoot orig, NPC npc)
        {
            // 原始掉落逻辑
            orig(npc);

            // 检查月亮领主核心
            if (npc.type == NPCID.MoonLordCore)
            {
                MoonLordDefeated = true;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.WorldData);

                // 计算月亮领主死亡位置对应的 Tile 坐标
                int tileX = (int)(npc.Center.X / 16f);
                int tileY = (int)(npc.Center.Y / 16f);

                // 根据 Tile 的 Origin = (0,3)，需要让传送门底部对齐死亡点
                // 所以 placeY = tileY - 3
                int placeX = tileX;
                int placeY = tileY - 3;

                // 如果此处有 Tile，需要先清理
                WorldGen.KillTile(placeX, placeY, noItem: true);

                // 放置多格 Tile（4×4）
                //WorldGen.PlaceObject(placeX, placeY, ModContent.TileType<TeleportationTile>());
                WorldGen.SquareTileFrame(placeX, placeY, true);

                // 显示提示信息
                Main.NewText("欢迎来到洪荒...", 255, 50, 50);
                //Main.NewText("漩涡之门正在显现...", 255, 50, 50);
            }
        }

        public override void SaveWorldData(TagCompound tag)
        {
            tag["MoonLordDefeated"] = MoonLordDefeated;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            MoonLordDefeated = tag.GetBool("MoonLordDefeated");
        }
    }
}
