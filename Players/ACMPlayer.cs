using Terraria.ModLoader;

namespace AncientChineseMythology
{
    public class ACMPlayer : ModPlayer
    {
        // 控制 Buff 重置
        public bool shenxianLightPet;

        public override void ResetEffects() {
            shenxianLightPet = false;
        }
    }
}
