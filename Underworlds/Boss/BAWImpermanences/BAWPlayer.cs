using System;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.BAWImpermanences
{
    /// <summary>
    /// 黑白无常Boss战专用玩家效果类 (V3 收敛)。
    /// - 震屏统一转发 <see cref="ACMScreenShakeSystem"/> (同帧取 max 预算, 尊重配置缩放);
    /// - 镜头辅助改为短时自恢复包络 (不再每帧硬控 Main.screenPosition);
    /// - 删除旧"双半血隐藏 +30% 全伤" (强化改为演出驱动的显式数值);
    /// - 三种 debuff (灵魂锁定/阴气侵蚀/锁链束缚) 公开 API 保留。
    /// </summary>
    public class BAWPlayer : ModPlayer
    {
        #region 屏幕位置控制 (兼容 API, 短时软引导)

        private Vector2 focusPos;
        private int focusTimer;

        /// <summary>
        /// 请求短时镜头聚焦 (每次调用刷新 ~18f 的软引导窗口, 权重很低, 不劫持操作感)。
        /// </summary>
        public void SetScreenPos(Vector2 toVec) {
            focusPos = toVec;
            focusTimer = 18;
        }

        #endregion

        #region 屏幕震动 (转发统一预算)

        /// <summary>
        /// 设置屏幕震动 —— 转发到 <see cref="ACMScreenShakeSystem"/> 统一预算 (取 max 不累加)。
        /// </summary>
        public void SetScreenShake(double scale, double time) {
            ACMScreenShakeSystem.Add((float)scale);
        }

        #endregion

        #region 缩放控制 (短时自恢复)

        private float zoomTarget = 1f;
        private int zoomTimer;
        private float zoomBaseline = 1f;
        private bool zoomActive;

        /// <summary>
        /// 请求短时画面缩放 (~50f 后自动回落到玩家原有缩放)。
        /// </summary>
        public void SetZoom(float zoom) {
            if (!zoomActive) {
                zoomBaseline = Main.GameZoomTarget;
                zoomActive = true;
            }
            zoomTarget = zoom;
            zoomTimer = 50;
        }

        #endregion

        #region 特殊效果 (debuff 轴)

        /// <summary>灵魂锁定效果 (勾魂链命中: 大幅减速)。</summary>
        public bool SoulLocked { get; set; }
        public int SoulLockTimer { get; set; }

        /// <summary>阴气侵蚀效果 (白无常: 轻微减速)。</summary>
        public bool YinQiCorrosion { get; set; }
        public int YinQiTimer { get; set; }

        /// <summary>锁链束缚效果 (黑无常: 中度减速)。</summary>
        public bool ChainBound { get; set; }
        public int ChainBoundTimer { get; set; }

        #endregion

        public override void ResetEffects() {
            if (SoulLockTimer > 0) {
                SoulLockTimer--;
                SoulLocked = true;
            }
            else {
                SoulLocked = false;
            }

            if (YinQiTimer > 0) {
                YinQiTimer--;
                YinQiCorrosion = true;
            }
            else {
                YinQiCorrosion = false;
            }

            if (ChainBoundTimer > 0) {
                ChainBoundTimer--;
                ChainBound = true;
            }
            else {
                ChainBound = false;
            }
        }

        public override void PostUpdateRunSpeeds() {
            // 灵魂锁定时大幅减速
            if (SoulLocked) {
                Player.maxRunSpeed *= 0.5f;
                Player.runAcceleration *= 0.5f;
                Player.jumpSpeedBoost -= 2f;
            }

            // 锁链束缚时减速
            if (ChainBound) {
                Player.maxRunSpeed *= 0.7f;
                Player.runAcceleration *= 0.7f;
            }

            // 阴气侵蚀时轻微减速
            if (YinQiCorrosion) {
                Player.maxRunSpeed *= 0.9f;
            }
        }

        public override void ModifyScreenPosition() {
            // 软镜头聚焦: 低权重 lerp, 窗口结束即完全交还
            if (focusTimer > 0) {
                focusTimer--;
                Vector2 want = focusPos - Main.ScreenSize.ToVector2() * 0.5f;
                float weight = 0.05f * (focusTimer / 18f);
                Main.screenPosition = Vector2.Lerp(Main.screenPosition, want, weight);
            }

            // 短时缩放包络: 推进 → 回落 → 释放
            if (zoomActive) {
                if (zoomTimer > 0) {
                    zoomTimer--;
                    Main.GameZoomTarget = MathHelper.Lerp(Main.GameZoomTarget, zoomTarget, 0.08f);
                }
                else {
                    Main.GameZoomTarget = MathHelper.Lerp(Main.GameZoomTarget, zoomBaseline, 0.1f);
                    if (Math.Abs(Main.GameZoomTarget - zoomBaseline) < 0.01f) {
                        Main.GameZoomTarget = zoomBaseline;
                        zoomActive = false;
                    }
                }
            }
        }

        /// <summary>
        /// 应用灵魂锁定效果
        /// </summary>
        public void ApplySoulLock(int duration) {
            SoulLockTimer = Math.Max(SoulLockTimer, duration);
        }

        /// <summary>
        /// 应用阴气侵蚀效果
        /// </summary>
        public void ApplyYinQiCorrosion(int duration) {
            YinQiTimer = Math.Max(YinQiTimer, duration);
        }

        /// <summary>
        /// 应用锁链束缚效果
        /// </summary>
        public void ApplyChainBound(int duration) {
            ChainBoundTimer = Math.Max(ChainBoundTimer, duration);
        }
    }
}
