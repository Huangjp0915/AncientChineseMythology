using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology
{
    /// <summary>
    /// 统一屏幕震动预算 (全局观感契约 §6.2)。
    /// 同帧多源**取 max 而非累加**, 每帧 *=0.9 指数衰减, 经 MythologyConfig.ScreenShakeScale 缩放。
    /// 调用入口: <see cref="ACMUtils.AddScreenShake(float)"/> / <see cref="Helpers.WeaponVFX.AddScreenShake(Player, float, int)"/>;
    /// 旧版 <see cref="ScreenShakePlayer.ShakeScreen(float, int)"/> 亦转发至此 (统一纳入预算 + 配置缩放)。
    /// 实际位移**仅**由 <see cref="ACMScreenShakePlayer.ModifyScreenPosition"/> 施加一次(纯本地视觉)。
    /// </summary>
    public class ACMScreenShakeSystem : ModSystem
    {
        /// <summary>当前震动峰值(像素)。</summary>
        public static float Budget { get; private set; }

        /// <summary>追加一次震动: 取 max(不累加), 并按玩家配置缩放。</summary>
        public static void Add(float amount) {
            if (Main.dedServ)
                return;
            amount *= MythologyConfig.ShakeScale;
            if (amount > Budget)
                Budget = amount;
        }

        public override void OnWorldUnload() => Budget = 0f;

        public override void PostUpdateEverything() {
            Budget *= 0.9f;
            if (Budget < 0.05f)
                Budget = 0f;
        }
    }

    /// <summary>
    /// 将屏幕震动预算转化为镜头随机抖动(本地玩家渲染时)。
    /// </summary>
    public class ACMScreenShakePlayer : ModPlayer
    {
        public override void ModifyScreenPosition() {
            float b = ACMScreenShakeSystem.Budget;
            if (b <= 0.05f || Main.gameMenu)
                return;
            Main.screenPosition += Main.rand.NextVector2Circular(b, b);
        }
    }
}
