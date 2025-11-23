using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology
{
    /// <summary>
    /// 处理屏幕震动效果
    /// </summary>
    internal class ScreenShakePlayer : ModPlayer
    {
        private float shakeIntensity = 0f;
        private int shakeDuration = 0;

        /// <summary>
        /// 触发屏幕震动
        /// </summary>
        public void ShakeScreen(float intensity, int duration)
        {
            if (intensity > shakeIntensity)
            {
                shakeIntensity = intensity;
                shakeDuration = duration;
            }
        }

        public override void ModifyScreenPosition()
        {
            if (shakeDuration > 0)
            {
                // 应用随机震动
                float currentIntensity = shakeIntensity * (shakeDuration / 20f);
                Main.screenPosition += Main.rand.NextVector2Circular(currentIntensity, currentIntensity);
                
                shakeDuration--;
                
                if (shakeDuration == 0)
                {
                    shakeIntensity = 0f;
                }
            }
        }

        public override void ResetEffects()
        {
            // 每帧逐渐减弱震动
            if (shakeIntensity > 0f)
            {
                shakeIntensity *= 0.95f;
            }
        }
    }
}
