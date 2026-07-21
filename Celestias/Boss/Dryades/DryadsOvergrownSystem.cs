using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dryades
{
    /// <summary>
    /// 树精「疯长 Overgrowth」屏幕蔓延系统 (V3) — 发布-订阅 (Suzaku/Dazheng ScreenSystem 模式)。
    ///
    /// 树精每帧 <see cref="Publish"/> 蔓延强度/枯化度; 事件时刻 <see cref="Pulse"/> 打一记心跳
    /// (生长前沿瞬时推进后衰减)。绘制走专属 DryadsOvergrowth.fx: 屏幕边缘 SDF 卷须指状生长,
    /// 完全程序化, 经 <see cref="ACMShaders.DrawFullscreenOverlay"/> (预乘 AlphaBlend) 于
    /// <see cref="PostDrawTiles"/> (实体下层, 不遮挡弹幕信息) 绘制。
    ///
    /// 不读 screenTarget、不占全屏后处理名额; 纯本地视觉、服务端零绘制,
    /// 受 <see cref="MythologyConfig.FullscreenShadersEnabled"/> 降级开关控制。
    /// 另叠加毒孢区就近增稠 (靠近毒孢区时空气更浓)。
    /// </summary>
    public class DryadsOvergrownSystem : ModSystem
    {
        private static float _target;        // 发布的蔓延强度 0~1
        private static float _witherTarget;  // 发布的枯化度 0~1
        private static float _pulse;         // 心跳脉冲 (瞬升缓降)
        private static ulong _lastPublishFrame;

        private static float _intensity;     // 平滑后的当前强度
        private static float _wither;

        private static Asset<Effect> overgrowthRef;
        private static Effect OvergrowthEffect {
            get {
                if (Main.dedServ) return null;
                overgrowthRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/DryadsOvergrowth", AssetRequestMode.ImmediateLoad);
                return overgrowthRef?.Value;
            }
        }

        /// <summary>树精每帧发布蔓延强度与枯化度 (客户端路径调用)。</summary>
        public static void Publish(float intensity, float wither) {
            _target = MathHelper.Clamp(intensity, 0f, 1f);
            _witherTarget = MathHelper.Clamp(wither, 0f, 1f);
            _lastPublishFrame = Main.GameUpdateCount;
        }

        /// <summary>心跳脉冲: 生长前沿瞬时推进 (换阶段心跳/缠狱开阵/死亡搏动)。</summary>
        public static void Pulse(float strength) {
            _pulse = MathF.Max(_pulse, MathHelper.Clamp(strength, 0f, 1f));
        }

        public override void Unload() {
            overgrowthRef = null;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu || !MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 不在场/停止发布 → 平滑退潮
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _target = 0f;
                _witherTarget = 0f;
            }

            // 就近毒孢区增稠 (最多 +0.18)
            float proximity = ComputeSporeProximity();

            float aim = MathHelper.Clamp(_target + proximity * 0.18f, 0f, 1f);
            _intensity = MathHelper.Lerp(_intensity, aim, aim > _intensity ? 0.035f : 0.05f);
            _wither = MathHelper.Lerp(_wither, _witherTarget, 0.06f);
            _pulse = MathHelper.Lerp(_pulse, 0f, 0.07f);

            if (_intensity <= 0.01f && _pulse <= 0.02f)
                return;

            Effect fx = OvergrowthEffect;
            if (fx == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_intensity, 0f, 1f));
            fx.Parameters["uPulse"]?.SetValue(MathHelper.Clamp(_pulse, 0f, 1f));
            fx.Parameters["uWither"]?.SetValue(MathHelper.Clamp(_wither, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        /// <summary>本地玩家与最近毒孢区的接近度 0~1 (无 Boss 上下文也安全)。</summary>
        private static float ComputeSporeProximity() {
            Player p = Main.LocalPlayer;
            if (p == null || !p.active || p.dead)
                return 0f;

            float best = 0f;
            int zoneType = ModContent.ProjectileType<DryadsSporeZone>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != zoneType)
                    continue;
                float d = Vector2.Distance(p.Center, proj.Center);
                float near = MathHelper.Clamp(1f - d / 700f, 0f, 1f);
                if (near > best)
                    best = near;
            }
            return best;
        }
    }
}
