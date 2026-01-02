using AncientChineseMythology.Underworlds.Tiles;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙击杀状态和幽冥矿生成系统
    /// </summary>
    public class NetherDragonDownedSystem : ModSystem
    {
        /// <summary>
        /// 幽冥龙是否被击杀过
        /// </summary>
        public static bool DownedNetherDragon { get; set; } = false;

        /// <summary>
        /// 幽冥矿是否已经生成过
        /// </summary>
        public static bool NetherOreGenerated { get; set; } = false;

        public override void OnWorldLoad() {
            DownedNetherDragon = false;
            NetherOreGenerated = false;
        }

        public override void OnWorldUnload() {
            DownedNetherDragon = false;
            NetherOreGenerated = false;
        }

        public override void SaveWorldData(TagCompound tag) {
            tag["downedNetherDragon"] = DownedNetherDragon;
            tag["netherOreGenerated"] = NetherOreGenerated;
        }

        public override void LoadWorldData(TagCompound tag) {
            DownedNetherDragon = tag.GetBool("downedNetherDragon");
            NetherOreGenerated = tag.GetBool("netherOreGenerated");
        }

        public override void NetSend(BinaryWriter writer) {
            var flags = new BitsByte();
            flags[0] = DownedNetherDragon;
            flags[1] = NetherOreGenerated;
            writer.Write(flags);
        }

        public override void NetReceive(BinaryReader reader) {
            BitsByte flags = reader.ReadByte();
            DownedNetherDragon = flags[0];
            NetherOreGenerated = flags[1];
        }

        /// <summary>
        /// 在幽冥龙死亡后调用，生成幽冥矿
        /// </summary>
        public static void OnNetherDragonKilled() {
            if (NetherOreGenerated) {
                return; // 已经生成过，不再生成
            }

            DownedNetherDragon = true;
            NetherOreGenerated = true;

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return; // 客户端不执行生成
            }

            // 生成幽冥矿
            GenerateNetherOre();

            // 通知玩家
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.WorldData);
            }
        }

        /// <summary>
        /// 在地府区域生成幽冥矿
        /// </summary>
        private static void GenerateNetherOre() {
            int netherOreType = ModContent.TileType<NetherOreTile>();
            int umbralStoneType = ModContent.TileType<UmbralStone>();

            // 地府区域范围（与地形生成器一致）
            int underworldStartX = Main.maxTilesX / 2;
            int underworldEndX = Main.maxTilesX - 200;
            int underworldStartY = (int)Main.rockLayer;
            int underworldEndY = Main.maxTilesY - 50;

            // 确保范围有效
            if (underworldEndX <= underworldStartX || underworldEndY <= underworldStartY) {
                Main.NewText("错误：地府区域无效，无法生成幽冥矿", Color.Red);
                return;
            }

            int oreCount = 0;
            int targetOreVeins = Main.rand.Next(80, 120); // 目标生成80-120个矿脉

            Main.NewText("幽冥龙的力量注入了大地...", new Color(100, 150, 255));

            for (int vein = 0; vein < targetOreVeins * 3; vein++) { // 多次尝试以确保生成足够数量
                if (oreCount >= targetOreVeins) break;

                // 随机选择位置
                int x = Main.rand.Next(underworldStartX + 50, underworldEndX - 50);
                int y = Main.rand.Next(underworldStartY + 100, underworldEndY - 100);

                // 检查是否在幽冥石内
                if (!Main.tile[x, y].HasTile) continue;
                if (Main.tile[x, y].TileType != umbralStoneType) continue;

                // 生成矿脉
                int veinSize = Main.rand.Next(4, 12);
                int veinStrength = Main.rand.Next(3, 6);

                WorldGen.TileRunner(
                    x, y,
                    veinSize,
                    veinStrength,
                    netherOreType,
                    false, 0f, 0f, false, true
                );

                oreCount++;
            }

            // 在地狱层也生成一些
            int hellStartY = Main.UnderworldLayer;
            int hellEndY = Main.maxTilesY - 50;

            for (int vein = 0; vein < targetOreVeins / 2; vein++) {
                int x = Main.rand.Next(underworldStartX + 50, underworldEndX - 50);
                int y = Main.rand.Next(hellStartY, hellEndY);

                if (!Main.tile[x, y].HasTile) continue;
                if (Main.tile[x, y].TileType != umbralStoneType) continue;

                int veinSize = Main.rand.Next(5, 15);
                int veinStrength = Main.rand.Next(4, 8);

                WorldGen.TileRunner(
                    x, y,
                    veinSize,
                    veinStrength,
                    netherOreType,
                    false, 0f, 0f, false, true
                );
            }

            Main.NewText("幽冥矿已在地府中显现！", new Color(100, 150, 255));
        }
    }
}
