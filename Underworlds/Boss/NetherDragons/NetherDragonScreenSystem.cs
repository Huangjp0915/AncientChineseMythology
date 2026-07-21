using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙 V3 演出标量中枢 + 非 screenTarget 全屏 overlay 绘制。
    ///
    /// 由 <see cref="NetherDragonHead"/> / <see cref="NetherLaserBeam"/> 每帧发布 0~1 标量驱动:
    ///   ● <b>RadialBloom</b> (breath bloom) —— 吐息/破门瞬间的加性鬼绿泛光。
    ///   ● <b>ArenaRunic</b> (outlet tell) —— 换阶段真门落点 / 万魂门环的符阵预警。
    ///   ● <b>NetherDragonCone</b> (cone tell) —— 吐息锥与扫射扇的锥形危险区预警
    ///     (双槽: P3 双向剪刀扫射各占一槽), 紫→红收口。
    ///
    /// 昂贵的全屏 screenTarget 扭曲 (GenericWarp · fog/rift) 不在本系统, 由
    /// <see cref="NetherDragonHead.PostDraw"/> 单独申请唯一名额绘制 (性能契约)。
    /// 本系统全部 overlay 都走**不读 screenTarget** 的占位像素绘制, 不占全屏名额。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/> (实体之下), 危险弹幕在其上层 → 不遮挡需躲避信息。
    /// 纯本地视觉, 服务端零绘制, 受 MythologyConfig 降级。
    /// </summary>
    public class NetherDragonScreenSystem : ModSystem
    {
        private static float _bloom;       // RadialBloom 强度 (吐息喷发)
        private static float _runic;       // ArenaRunic 强度 (出口符阵预警)
        private static Vector2 _bloomCenter;
        private static Vector2 _runicCenter;
        private static float _runicRadius; // 世界半径 → 着色器
        private static bool _runicLethal;  // true=红色致命落点(出口将至) false=主题色预备
        private static float _time;
        private static ulong _lastPublishFrame;

        // 锥形预警双槽 (0=主吐息/正向扫束, 1=暴怒吐息/反向扫束)
        private struct ConeSlot
        {
            public float Intensity;
            public Vector2 Apex;
            public float Dir;
            public float Spread;
            public float Length;
            public float Progress;
            public ulong Frame;
        }
        private static readonly ConeSlot[] _cones = new ConeSlot[2];

        /// <summary>由 NetherDragonHead 每帧调用, 发布演出标量 (纯本地视觉)。</summary>
        public static void Publish(float bloom, Vector2 bloomCenter,
            float runic, Vector2 runicCenter, float runicRadius, bool runicLethal, float time) {
            _bloom = bloom;
            _bloomCenter = bloomCenter;
            _runic = runic;
            _runicCenter = runicCenter;
            _runicRadius = runicRadius;
            _runicLethal = runicLethal;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        /// <summary>
        /// 发布锥形危险区预警 (吐息锥 / 扫射扇共用; slot 0~1)。
        /// progress 0~1 推进配色 紫→红 (§6.1 红=致命收口)。
        /// </summary>
        public static void PublishCone(int slot, Vector2 apex, float dir, float spread,
            float length, float progress, float intensity) {
            if (slot < 0 || slot >= _cones.Length)
                return;
            _cones[slot] = new ConeSlot {
                Intensity = intensity,
                Apex = apex,
                Dir = dir,
                Spread = spread,
                Length = length,
                Progress = progress,
                Frame = Main.GameUpdateCount
            };
        }

        public override void OnWorldUnload() {
            _bloom = _runic = 0f;
            for (int i = 0; i < _cones.Length; i++)
                _cones[i] = default;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 不在场/未发布时平滑淡出, 避免状态残留
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _bloom = MathHelper.Lerp(_bloom, 0f, 0.15f);
                _runic = MathHelper.Lerp(_runic, 0f, 0.15f);
            }
            for (int i = 0; i < _cones.Length; i++) {
                if (Main.GameUpdateCount - _cones[i].Frame > 2)
                    _cones[i].Intensity = MathHelper.Lerp(_cones[i].Intensity, 0f, 0.2f);
            }

            DrawGates();
            DrawOutletRunic();
            DrawCones();
            DrawBreathBloom();
        }

        // ===== 冥界之门贴花: 画在实体之下 (龙从门内钻出时不被门内暗渊遮挡) =====
        private static void DrawGates() {
            int portalType = ModContent.ProjectileType<NetherPortal>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == portalType && p.ModProjectile is NetherPortal gate)
                    gate.DrawGateDecal();
            }
        }

        // ===== ArenaRunic(法阵): 真门落点 / 万魂门环的向心收口预警 (可读落点) =====
        private static void DrawOutletRunic() {
            if (_runic <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_runicCenter, _runicRadius, out Vector2 uv, out float radiusFrac, out float aspect);
            // 收口前以主题色(幽蓝紫)预备; 出口将至(_runicLethal)切纯红致命落点 (§6.1 红=致命)
            Color primary = _runicLethal ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
            Color secondary = _runicLethal ? TelegraphColors.Execution : UnderworldField.DecreeColor;

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_runic, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(11f);
            fx.Parameters["uMode"]?.SetValue(0f);   // 法阵(非牢笼)
            fx.Parameters["uShape"]?.SetValue(0f);  // 圆

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== NetherDragonCone: 吐息锥 / 扫射扇 危险区预警 (加性, 不遮挡视野) =====
        private static void DrawCones() {
            Effect fx = NetherDragonVFX.Cone;
            if (fx == null)
                return;

            for (int i = 0; i < _cones.Length; i++) {
                ref ConeSlot c = ref _cones[i];
                if (c.Intensity <= 0.02f)
                    continue;

                ACMShaders.WorldDecalParams(c.Apex, c.Length, out Vector2 uv, out float radiusFrac, out float aspect);

                fx.Parameters["uTime"]?.SetValue(_time);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(radiusFrac);
                fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(c.Intensity, 0f, 1f));
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uDir"]?.SetValue(c.Dir);
                fx.Parameters["uSpread"]?.SetValue(c.Spread);
                fx.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(c.Progress, 0f, 1f));
                fx.Parameters["uColorWarm"]?.SetValue(TelegraphColors.NetherViolet.ToVector4());
                fx.Parameters["uColorHot"]?.SetValue(TelegraphColors.Lethal.ToVector4());

                ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
            }
        }

        // ===== RadialBloom: 吐息/破门的鬼绿泛光 =====
        private static void DrawBreathBloom() {
            if (_bloom <= 0.01f)
                return;
            Effect fx = ACMShaders.RadialBloom;
            if (fx == null)
                return;

            Vector2 uv = (_bloomCenter - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_bloom, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(0.28f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColor"]?.SetValue(new Vector4(TelegraphColors.GhostGreen.ToVector3(), 1f));
            fx.Parameters["uRayCount"]?.SetValue(8f);
            fx.Parameters["uFalloff"]?.SetValue(2.6f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
        }
    }
}
