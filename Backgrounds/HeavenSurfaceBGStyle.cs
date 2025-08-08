using Terraria.ModLoader;

namespace AncientChineseMythology.Backgrounds
{
    public class HeavenSurfaceBGStyle : ModSurfaceBackgroundStyle
    {
        public override int ChooseFarTexture() =>
            BackgroundTextureLoader.GetBackgroundSlot(Mod, "Textures/Backgrounds/Heaven_Far");
        public override int ChooseMiddleTexture() =>
            BackgroundTextureLoader.GetBackgroundSlot(Mod, "Textures/Backgrounds/Heaven_Middle");
        public override int ChooseCloseTexture(ref float scale,
                                               ref double parallax,
                                               ref float a, ref float b) =>
            BackgroundTextureLoader.GetBackgroundSlot(Mod, "Textures/Backgrounds/Heaven_Close");

        /* 缓存 3 个真实 slot */
        private int farSlot, midSlot, closeSlot;
        private bool loaded;

        private void Ensure() {
            if (loaded) return;
            farSlot = ChooseFarTexture();
            midSlot = ChooseMiddleTexture();
            closeSlot = BackgroundTextureLoader.GetBackgroundSlot(Mod, "Textures/Backgrounds/Heaven_Close");
            loaded = true;
        }

        /* 只有 Far 这一个钩子，把 3 层一次性处理 */
        public override void ModifyFarFades(float[] fades, float transitionSpeed) {
            Ensure();
            for (int i = 0; i < fades.Length; i++) {
                bool isMine = (i == farSlot) || (i == midSlot) || (i == closeSlot);
                float target = isMine ? 1f : 0f;
                fades[i] = MathHelper.Lerp(fades[i], target, transitionSpeed);
            }
        }
    }
}