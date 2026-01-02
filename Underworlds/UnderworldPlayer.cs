using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds
{
    internal class UnderworldPlayer : ModPlayer
    {
        public static bool UnderworldEffect;
        public override void ResetEffects() {
            UnderworldEffect = false;
        }
    }
}
