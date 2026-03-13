using System;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.BAWImpermanences
{
    /// <summary>
    /// 黑白无常Boss战专用玩家效果类
    /// 处理屏幕效果、镜头控制等
    /// </summary>
    public class BAWPlayer : ModPlayer
    {
        #region 屏幕位置控制

        private Vector2 screenPos;
        private bool startSetScreenPos = false;
        private float timerSetScreenPos = 1;

        /// <summary>
        /// 设置屏幕聚焦位置
        /// </summary>
        public void SetScreenPos(Vector2 toVec) {
            screenPos = Vector2.Lerp(screenPos, toVec - Main.ScreenSize.ToVector2() * 0.5f, 0.04f);
            startSetScreenPos = true;
            timerSetScreenPos = 0;
        }

        #endregion

        #region 屏幕震动

        private int shakeScale = 0;
        private int shakeTime = 0;

        /// <summary>
        /// 设置屏幕震动
        /// </summary>
        public void SetScreenShake(double scale, double time) {
            shakeScale = (int)scale;
            shakeTime = (int)time;
        }

        #endregion

        #region 缩放控制

        private float oldZoom;
        private float targetZoom = 1;
        private float timerZoom = 1;
        private bool startSetZoom = false;

        /// <summary>
        /// 设置画面缩放
        /// </summary>
        public void SetZoom(float zoom) {
            targetZoom = MathHelper.Lerp(targetZoom, zoom, 0.02f);
            startSetZoom = true;
            timerZoom = 0;
        }

        #endregion

        #region 特殊效果

        /// <summary>
        /// 灵魂锁定效果（被黑白无常共同锁定时的减速效果）
        /// </summary>
        public bool SoulLocked { get; set; } = false;
        public int SoulLockTimer { get; set; } = 0;

        /// <summary>
        /// 阴气侵蚀效果（白无常的debuff）
        /// </summary>
        public bool YinQiCorrosion { get; set; } = false;
        public int YinQiTimer { get; set; } = 0;

        /// <summary>
        /// 锁链束缚效果（黑无常的debuff）
        /// </summary>
        public bool ChainBound { get; set; } = false;
        public int ChainBoundTimer { get; set; } = 0;

        #endregion

        public override void ResetEffects() {
            // 重置每帧的临时效果
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
            // 屏幕位置控制
            if (!startSetScreenPos) {
                timerSetScreenPos = 1;
                screenPos = Main.screenPosition;
            }
            else {
                Main.screenPosition = screenPos;
                if (timerSetScreenPos < 0.9f) {
                    timerSetScreenPos = MathHelper.Lerp(timerSetScreenPos, 1, 0.05f);
                    screenPos = Vector2.Lerp(screenPos, Player.Center - Main.ScreenSize.ToVector2() * 0.5f, timerSetScreenPos * 0.1f);
                }
                else {
                    startSetScreenPos = false;
                }
            }

            // 屏幕震动
            if (shakeTime > 0) {
                shakeTime--;
                Main.screenPosition += new Vector2(shakeScale).RotatedByRandom(MathHelper.TwoPi);
            }

            // 缩放控制
            if (startSetZoom) {
                Main.GameZoomTarget = targetZoom;

                if (timerZoom < 0.9f || Math.Abs(Main.GameZoomTarget - oldZoom) > 0.08f) {
                    timerZoom = MathHelper.Lerp(timerZoom, 1, 0.05f);
                    targetZoom = MathHelper.Lerp(targetZoom, oldZoom, timerSetScreenPos * 0.1f);
                }
                else {
                    Main.GameZoomTarget = oldZoom;
                    startSetZoom = false;
                }
            }
            else {
                targetZoom = oldZoom = Main.GameZoomTarget;
            }

            base.ModifyScreenPosition();
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            // 检查是否被黑白无常同时锁定（增加伤害）
            bool blackActive = false;
            bool whiteActive = false;
            bool blackHalfHealth = false;
            bool whiteHalfHealth = false;

            foreach (var n in Main.npc) {
                if (n != null && n.active) {
                    if (n.type == ModContent.NPCType<BlackImpermanence>()) {
                        blackActive = true;
                        if (n.life < n.lifeMax * 0.5f)
                            blackHalfHealth = true;
                    }
                    else if (n.type == ModContent.NPCType<WhiteImpermanence>()) {
                        whiteActive = true;
                        if (n.life < n.lifeMax * 0.5f)
                            whiteHalfHealth = true;
                    }
                }
            }

            // 双半血狂暴状态：伤害增加30%
            if (blackHalfHealth && whiteHalfHealth) {
                modifiers.FinalDamage *= 1.3f;
            }

            base.ModifyHitByNPC(npc, ref modifiers);
        }

        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            // 同上，弹幕伤害也增加
            bool blackHalfHealth = false;
            bool whiteHalfHealth = false;

            foreach (var n in Main.npc) {
                if (n != null && n.active) {
                    if (n.type == ModContent.NPCType<BlackImpermanence>()) {
                        if (n.life < n.lifeMax * 0.5f)
                            blackHalfHealth = true;
                    }
                    else if (n.type == ModContent.NPCType<WhiteImpermanence>()) {
                        if (n.life < n.lifeMax * 0.5f)
                            whiteHalfHealth = true;
                    }
                }
            }

            if (blackHalfHealth && whiteHalfHealth) {
                modifiers.FinalDamage *= 1.3f;
            }

            base.ModifyHitByProjectile(proj, ref modifiers);
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
