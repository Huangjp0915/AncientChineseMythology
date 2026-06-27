using Terraria.ModLoader;

namespace AncientChineseMythology
{
    /// <summary>
    /// 旧版屏幕震动入口 (兼容层)。历史调用点遍布各 Boss/武器, 以
    /// <c>player.GetModPlayer&lt;ScreenShakePlayer&gt;().ShakeScreen(intensity, duration)</c> 触发。
    ///
    /// <para>统一后本类**不再自行抖动屏幕**, 而是把请求转发到统一预算
    /// <see cref="ACMScreenShakeSystem"/> (同帧多源取 max 不累加, 经 <see cref="MythologyConfig.ShakeScale"/>
    /// 缩放, 每帧 *=0.9 指数衰减)。实际屏幕位移**只**由 <see cref="ACMScreenShakePlayer.ModifyScreenPosition"/>
    /// 施加一次, 故旧调用点自动纳入预算 + 配置缩放, 且与新系统不再各自 <c>Main.screenPosition +=</c> 叠加
    /// (杜绝双重位移、突破"取 max"契约)。</para>
    /// </summary>
    internal class ScreenShakePlayer : ModPlayer
    {
        /// <summary>
        /// 触发屏幕震动 (保留旧签名以兼容全部历史调用点)。转发到 <see cref="ACMScreenShakeSystem"/> 统一预算。
        /// </summary>
        /// <param name="intensity">峰值像素幅度 (取 max 不累加; 由统一预算施加配置缩放与逐帧衰减)。</param>
        /// <param name="duration">旧版持续帧数 — 统一预算改用全局指数衰减承载持续时间,
        /// 此参数仅为兼容旧调用点而保留, 当前不再单独消费。</param>
        public void ShakeScreen(float intensity, int duration) {
            ACMScreenShakeSystem.Add(intensity);
        }
    }
}
