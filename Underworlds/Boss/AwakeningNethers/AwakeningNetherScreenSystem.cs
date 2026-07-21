using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.AwakeningNethers
{
    /// <summary>
    /// 觉醒冥龙 V3 演出标量中枢 + 非 screenTarget 全屏 overlay / 专属 decal 批量绘制。
    ///
    /// 由 <see cref="AwakeningNetherHead"/> 每帧 <see cref="Publish"/> 一组 0~1 标量驱动:
    ///   ● <b>ElementalScreenTint</b> (nether-fog) —— 冥雾氛围, 每幕递进加深 (巡游→裂隙→吞噬)。
    ///   ● <b>ArenaRunic</b> (rift/vortex tell) —— 裂隙门 / 奇点的向心收口符阵预警 (可读落点)。
    ///   ● <b>RadialBloom</b> (breath/laser/finality bloom) —— 吐息 / 激光帘幕 / 终末喷发的加性紫泛光。
    ///
    /// V3 新增两条<b>专属 decal 批量队列</b>(魂焰 Soulflame / 虚空裂隙 VoidRift):
    /// 各弹幕与 Boss 在 <b>AI(tick) 阶段</b>调用 Request* 入队, 本系统在 PostDrawTiles 用
    /// 各自专属着色器<b>一次开合批画完全部实例</b>(Immediate 模式逐实例改参), 把每帧批次开销
    /// 压到常数 2 次, 与实例数解耦。队列在 PreUpdateEntities 清空 → 跳帧不重影、掉帧可复用。
    ///
    /// 以上皆<b>不读 screenTarget</b> → 不占全屏后处理名额 (§C.4#2)。昂贵的 GenericWarp 全屏扭曲
    /// 由 <see cref="AwakeningNetherHead.PostDraw"/> 单独申请唯一全屏名额绘制。
    /// 绘制位于 PostDrawTiles (实体之下), 危险弹幕在其上层 → 不遮挡需躲避信息 (§6.6)。
    /// 纯本地视觉, 服务端零绘制, 受 MythologyConfig 降级。
    /// </summary>
    public class AwakeningNetherScreenSystem : ModSystem
    {
        private static float _fog;          // ElementalScreenTint 强度 (冥雾, 每幕加深)
        private static float _bloom;        // RadialBloom 强度 (吐息/激光/喷发)
        private static float _runic;        // ArenaRunic 强度 (裂隙门/奇点向心收口预警)
        private static Vector2 _bloomCenter;
        private static Vector2 _runicCenter;
        private static float _runicRadius;  // 世界半径 → 着色器
        private static bool _runicLethal;   // true=红色致命落点 false=主题色(幽蓝紫)预备
        private static float _time;
        private static ulong _lastPublishFrame;

        /// <summary>由 AwakeningNetherHead 每帧调用, 发布演出标量 (纯本地视觉)。</summary>
        public static void Publish(float fog, float bloom, Vector2 bloomCenter,
            float runic, Vector2 runicCenter, float runicRadius, bool runicLethal, float time) {
            _fog = fog;
            _bloom = bloom;
            _bloomCenter = bloomCenter;
            _runic = runic;
            _runicCenter = runicCenter;
            _runicRadius = runicRadius;
            _runicLethal = runicLethal;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        // ============================================================
        //  专属 decal 批量队列 (V3)
        // ============================================================

        private struct SoulflameReq
        {
            public Vector2 Center;   // 世界坐标
            public Vector2 Dir;      // 火舌流向 (单位向量)
            public float Size;       // 世界像素边长
            public float Intensity;
            public float Seed;
            public float Round;      // 0=火舌 1=径向魂雾场
            public Color Core;
            public Color Edge;
        }

        private struct VoidRiftReq
        {
            public Vector2 Center;
            public float Size;       // 世界像素边长(直径)
            public float Progress;   // 旋开进度 0~1
            public float Spin;       // 附加相位
            public float Lethal;     // 0~1 致命红混合
            public float Intensity;
            public Color Glow;
            public Color Edge;
        }

        private static readonly List<SoulflameReq> soulflameQueue = new();
        private static readonly List<VoidRiftReq> voidRiftQueue = new();
        private const int MaxDecalsPerKind = 24;

        /// <summary>
        /// 入队一张魂焰 decal (只允许在 AI/tick 阶段调用; 绘制在 PostDrawTiles, 实体之下)。
        /// </summary>
        public static void RequestSoulflame(Vector2 worldCenter, Vector2 dir, float sizePx,
            float intensity, float seed, float round, Color core, Color edge) {
            if (Main.dedServ || intensity <= 0.01f || soulflameQueue.Count >= MaxDecalsPerKind)
                return;
            soulflameQueue.Add(new SoulflameReq {
                Center = worldCenter, Dir = dir, Size = sizePx, Intensity = intensity,
                Seed = seed, Round = round, Core = core, Edge = edge
            });
        }

        /// <summary>
        /// 入队一张虚空裂隙/奇点 decal (只允许在 AI/tick 阶段调用)。
        /// </summary>
        public static void RequestVoidRift(Vector2 worldCenter, float sizePx, float progress,
            float spin, float lethal, float intensity, Color glow, Color edge) {
            if (Main.dedServ || intensity <= 0.01f || voidRiftQueue.Count >= MaxDecalsPerKind)
                return;
            voidRiftQueue.Add(new VoidRiftReq {
                Center = worldCenter, Size = sizePx, Progress = progress, Spin = spin,
                Lethal = lethal, Intensity = intensity, Glow = glow, Edge = edge
            });
        }

        /// <summary>每个 tick 开始清空上一 tick 的 decal 请求 (跳帧不叠影, 掉帧可复用上帧内容)。</summary>
        public override void PreUpdateEntities() {
            soulflameQueue.Clear();
            voidRiftQueue.Clear();
        }

        public override void OnWorldUnload() {
            _fog = _bloom = _runic = 0f;
            soulflameQueue.Clear();
            voidRiftQueue.Clear();
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            // Boss 不在场/未发布时平滑淡出, 避免状态残留
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _fog = MathHelper.Lerp(_fog, 0f, 0.05f);
                _bloom = MathHelper.Lerp(_bloom, 0f, 0.15f);
                _runic = MathHelper.Lerp(_runic, 0f, 0.15f);
            }

            DrawNetherFog();
            DrawVoidRiftDecals();
            DrawSoulflameDecals();
            DrawTell();
            DrawBloom();
        }

        // ===== ElementalScreenTint(冥雾): 每幕递进加深的氛围底色 =====
        private static void DrawNetherFog() {
            if (_fog <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_fog, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.NetherViolet.ToVector3(), 0.5f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(UnderworldField.DecreeColor.ToVector3(), 1f));
            fx.Parameters["uVignette"]?.SetValue(0.45f);
            fx.Parameters["uFogScale"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== 虚空裂隙 decal 批 (AlphaBlend 预乘: 暗核压暗场景) =====
        private static void DrawVoidRiftDecals() {
            if (voidRiftQueue.Count == 0)
                return;
            Effect fx = AwakeningNetherHelper.VoidRiftShader;
            if (fx == null)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            fx.Parameters["uTime"]?.SetValue(_time);
            foreach (var req in voidRiftQueue) {
                fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(req.Intensity, 0f, 1f));
                fx.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(req.Progress, 0f, 1f));
                fx.Parameters["uSpin"]?.SetValue(req.Spin);
                fx.Parameters["uLethal"]?.SetValue(MathHelper.Clamp(req.Lethal, 0f, 1f));
                fx.Parameters["uColorGlow"]?.SetValue(req.Glow.ToVector4());
                fx.Parameters["uColorEdge"]?.SetValue(req.Edge.ToVector4());
                fx.CurrentTechnique.Passes[0].Apply();
                // MagicPixel 为 1×1000, 必须走目标矩形重载 (UV 自动铺满 0~1)
                sb.Draw(pixel, DecalDest(req.Center, req.Size), Color.White);
            }

            sb.End();
        }

        /// <summary>世界中心 + 边长 → 屏幕空间目标矩形。</summary>
        private static Rectangle DecalDest(Vector2 worldCenter, float size) {
            Vector2 topLeft = worldCenter - Main.screenPosition - new Vector2(size * 0.5f);
            return new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)size, (int)size);
        }

        // ===== 魂焰 decal 批 (Additive: 冥火辉光) =====
        private static void DrawSoulflameDecals() {
            if (soulflameQueue.Count == 0)
                return;
            Effect fx = AwakeningNetherHelper.SoulflameShader;
            if (fx == null)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            fx.Parameters["uTime"]?.SetValue(_time);
            foreach (var req in soulflameQueue) {
                fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(req.Intensity, 0f, 1f));
                fx.Parameters["uFlameDir"]?.SetValue(req.Dir);
                fx.Parameters["uSeed"]?.SetValue(req.Seed);
                fx.Parameters["uRound"]?.SetValue(MathHelper.Clamp(req.Round, 0f, 1f));
                fx.Parameters["uCoreColor"]?.SetValue(req.Core.ToVector4());
                fx.Parameters["uEdgeColor"]?.SetValue(req.Edge.ToVector4());
                fx.CurrentTechnique.Passes[0].Apply();
                sb.Draw(pixel, DecalDest(req.Center, req.Size), Color.White);
            }

            sb.End();
        }

        // ===== ArenaRunic(法阵): 裂隙门 / 奇点的向心收口预警(可读落点) =====
        private static void DrawTell() {
            if (_runic <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_runicCenter, _runicRadius, out Vector2 uv, out float radiusFrac, out float aspect);
            // 预备以主题色(幽蓝紫); 落点将至(_runicLethal)切纯红致命 (§6.1 红=致命)
            Color primary = _runicLethal ? TelegraphColors.Lethal : TelegraphColors.NetherViolet;
            Color secondary = _runicLethal ? TelegraphColors.Execution : UnderworldField.DecreeColor;

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_runic, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(12f);
            fx.Parameters["uMode"]?.SetValue(0f);   // 法阵(非牢笼)
            fx.Parameters["uShape"]?.SetValue(0f);  // 圆

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== RadialBloom: 吐息/激光帘幕/终末喷发的紫泛光 =====
        private static void DrawBloom() {
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
            fx.Parameters["uRadius"]?.SetValue(0.3f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColor"]?.SetValue(new Vector4(TelegraphColors.NetherViolet.ToVector3(), 1f));
            fx.Parameters["uRayCount"]?.SetValue(10f);
            fx.Parameters["uFalloff"]?.SetValue(2.6f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
        }
    }
}
