using System.Collections.Generic;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AncientChineseMythology.Players
{
    public class GrowthPlayer : ModPlayer
    {
        public float growthBonus = 0f;
        public List<int> growthEnemies = new List<int>();

        public override void ResetEffects() {
            //如果需要每帧重置某些数据，可以放这里
        }

        public override void SaveData(TagCompound tag) {
            tag["growthBonus"] = growthBonus;
            tag["growthEnemies"] = growthEnemies;
        }

        public override void LoadData(TagCompound tag) {
            growthBonus = tag.GetFloat("growthBonus");
            growthEnemies = tag.Get<List<int>>("growthEnemies");
        }
    }
}
