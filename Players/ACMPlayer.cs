using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria;

namespace AncientChineseMythology.Players
{
    public class ACMPlayer : ModPlayer
    {
        //控制 Buff 重置
        public bool shenxianLightPet;

        // 屏幕震动相关
        public int screenShakeTimer = 0;
        public float screenShakeIntensity = 0f;

        public override void ResetEffects() {
            shenxianLightPet = false;
        }

        public override void PostUpdate()
        {
            // 更新屏幕震动
            if (screenShakeTimer > 0)
            {
                screenShakeTimer--;
                screenShakeIntensity *= 0.9f; // 逐渐减弱
            }
        }

        /// <summary>
        /// 触发屏幕震动效果
        /// </summary>
        /// <param name="intensity">震动强度</param>
        /// <param name="duration">持续时间（帧）</param>
        public void ScreenShake(float intensity, int duration)
        {
            if (intensity > screenShakeIntensity)
            {
                screenShakeIntensity = intensity;
                screenShakeTimer = duration;
            }
        }

        public override void ModifyScreenPosition()
        {
            // 应用屏幕震动
            if (screenShakeTimer > 0 && screenShakeIntensity > 0)
            {
                Vector2 shake = Main.rand.NextVector2Circular(screenShakeIntensity, screenShakeIntensity);
                Main.screenPosition += shake;
            }
        }
    }
}
