using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using System.Collections.Generic;
using AncientChineseMythology.NPCs.Boss.BlackBear;

namespace AncientChineseMythology.Systems
{
    public class AncientChineseMythologySystem : ModSystem
    {
        // 标记本晚是否已经生成过 Boss
        public static bool blackBearSpawnedThisNight = false;
        // 标记本晚是否已经显示过提示
        public static bool blackBearTipShown = false;

        public static bool downedBlackBear;         // 击败 Boss
        public static bool triggeredShengZhuStatue;     // 激活圣主雕像

        public override void PostUpdateEverything()
        {
            // 检查是否处于夜晚
            if (!Main.dayTime)
            {
                // 循环所有玩家，判断是否有玩家在丛林地表
                foreach (Player player in Main.player)
                {
                    if (player.active && !player.dead && player.ZoneJungle && player.position.Y < Main.worldSurface * 16)
                    {
                        // 如果还没有显示过提示，则显示提示信息
                        if (!blackBearTipShown)
                        {
                            // 可以使用自定义颜色或其他样式
                            Main.NewText("你感觉到丛林有什么在注视你...", Microsoft.Xna.Framework.Color.Orange);
                            blackBearTipShown = true;
                        }
                        // 如果 Boss 还没有生成，则强制生成 Boss
                        if (!blackBearSpawnedThisNight)
                        {
                            int npcType = ModContent.NPCType<BlackBear>();
                            // 这里采用在玩家附近生成 Boss 的方式，你可以根据实际情况调整生成位置
                            int spawnX = (int)player.position.X;
                            int spawnY = (int)player.position.Y - 200; // 生成在玩家上方一定高度
                            NPC.NewNPC(player.GetSource_FromThis(), spawnX, spawnY, npcType);
                            blackBearSpawnedThisNight = true;
                        }
                    }
                }
            }
            else
            {
                // 白天重置标记，为新的一晚做准备
                blackBearSpawnedThisNight = false;
                blackBearTipShown = false;
            }
        }

        public override void OnWorldLoad() {
            downedBlackBear      = false;
            triggeredShengZhuStatue  = false;
        }

        public override void OnWorldUnload() {
            downedBlackBear      = false;
            triggeredShengZhuStatue  = false;
        }

        public override void SaveWorldData(TagCompound tag) {
            var flags = new List<string>();
            if (downedBlackBear)     flags.Add(nameof(downedBlackBear));
            if (triggeredShengZhuStatue) flags.Add(nameof(triggeredShengZhuStatue));
            tag["acmFlags"] = flags;
        }

        public override void LoadWorldData(TagCompound tag) {
            var flags = tag.GetList<string>("acmFlags");
            downedBlackBear      = flags.Contains(nameof(downedBlackBear));
            triggeredShengZhuStatue  = flags.Contains(nameof(triggeredShengZhuStatue));
        }
    }
}
