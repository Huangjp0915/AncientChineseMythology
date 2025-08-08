using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AncientChineseMythology.Systems
{
    public class DownedBossSystem : ModSystem
    {
        public static bool downedBlackBear = false; // 跟踪 BlackBear 是否已被击败
        public static bool downedArchosaur = false;

        public override void SaveWorldData(TagCompound tag) {
            tag["downedBlackBear"] = downedBlackBear; // 保存状态
            tag["downedArchosaur"] = downedArchosaur; // 保存状态
        }

        public override void LoadWorldData(TagCompound tag) {
            downedBlackBear = tag.GetBool("downedBlackBear"); // 加载状态
            downedArchosaur = tag.GetBool("downedArchosaur"); // 加载状态
        }

        public override void OnWorldLoad() {
            // 重置所有 Boss 的击败状态
            downedBlackBear = false;
            downedArchosaur = false;
        }
    }
}
