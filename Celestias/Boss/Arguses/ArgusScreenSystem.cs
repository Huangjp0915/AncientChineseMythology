using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Arguses
{
    /// <summary>
    /// 天目·追魂弧 屏幕氛围系统。由 <see cref="Argus.UpdateVisuals"/> 每帧 <see cref="Publish"/>
    /// 一组 0~1 标量驱动, 集中绘制**不占全屏后处理名额**的装饰层:
    ///   ● <b>ElementalScreenTint</b> — 虚空"被注视"压迫染色 (P2 轻染, P3 加重, 全视之域拉满);
    ///     狙击对决时 gazeFocus 抬升暗角 → 屏幕向猎场聚焦。
    ///   ● <b>ArenaRunic</b> — 全视之域签名: 以域中心为心的紫蓝虹环地纹。
    ///   ● <b>被凝视边缘警示</b> — 本地玩家处于任何锁定视线走廊内时, 屏幕四缘泛起紫红呼吸
    ///     (Boss/哨兵眼经 <see cref="ReportGazeThreat"/> 汇报, 同帧取 max 自衰减; SoftGlow 半出屏, 零着色器)。
    /// 昂贵的全屏 screenTarget 折射/调色 (rift / impact frame) 由 <see cref="Argus.PostDraw"/> 单独申请名额。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/> (无活动批): 氛围在实体之下, 致命弹幕在其上 → 不遮挡躲避信息。
    /// 纯本地视觉, 服务端零绘制, 受 <see cref="MythologyConfig"/> 降级。
    /// </summary>
    public class ArgusScreenSystem : ModSystem
    {
        private static float _tint;
        private static float _domain;          // 全视之域进度 (驱动 ArenaRunic 虹环)
        private static float _focus;           // 狙击对决聚焦暗角
        private static Vector2 _domainCenter;  // 域中心 (坍缩阵心/玩家)
        private static float _time;
        private static ulong _lastPublishFrame;

        private static float _gazeThreat;      // "被看见"边缘警示 (同帧取 max, 自衰减)
        private static float _gazeThreatDrawn; // 平滑后的绘制值

        private static readonly Color VoidTint = new(60, 30, 110);
        private static readonly Color VoidDeep = new(10, 6, 28);
        private static readonly Color IrisPurple = new(180, 80, 255);
        private static readonly Color IrisBlue = new(80, 150, 255);
        private static readonly Color ThreatRed = new(250, 60, 90);

        /// <summary>由 <see cref="Argus"/> 每帧调用, 发布当前氛围标量 (纯本地视觉)。</summary>
        public static void Publish(float tint, float domain, float gazeFocus, Vector2 domainCenter, float time) {
            _tint = tint;
            _domain = domain;
            _focus = gazeFocus;
            _domainCenter = domainCenter;
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        /// <summary>
        /// 任何锁定视线来源 (Boss 凝视/哨兵眼/悬停箭) 汇报"本地玩家正被看见"。
        /// 同帧取 max, 每帧自衰减 — 多目同时锁定不叠爆。
        /// </summary>
        public static void ReportGazeThreat(float amount) {
            if (Main.dedServ)
                return;
            if (amount > _gazeThreat)
                _gazeThreat = MathHelper.Clamp(amount, 0f, 1f);
        }

        public override void OnWorldUnload() {
            _tint = _domain = _focus = _gazeThreat = _gazeThreatDrawn = 0f;
            Argus.DomainSignal = 0f;
            Argus.SkyBlink = 0f;
            Argus.SkyDeathClose = 0f;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            //发布断流时自然回落 (Boss 消失/暂离)
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _tint = MathHelper.Lerp(_tint, 0f, 0.10f);
                _domain = MathHelper.Lerp(_domain, 0f, 0.12f);
                _focus = MathHelper.Lerp(_focus, 0f, 0.12f);
            }

            //警示通道: 上升快 (被盯上的骤然紧张), 回落慢 (余悸); 每帧自衰减
            _gazeThreatDrawn = MathHelper.Lerp(_gazeThreatDrawn, _gazeThreat,
                _gazeThreat > _gazeThreatDrawn ? 0.35f : 0.07f);
            _gazeThreat *= 0.8f;

            DrawVoidTint();
            DrawDomainIris();
            DrawGazeThreatEdges();
        }

        // ===== ElementalScreenTint: 虚空"被注视"压迫染色 + 狙击聚焦暗角 =====
        private static void DrawVoidTint() {
            float strength = MathHelper.Clamp(_tint + _focus * 0.5f, 0f, 1f);
            if (strength <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(strength);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            //上=幽紫雾, 下=深空墨, 覆盖度保守 — 始终看得清致命箭/视线
            fx.Parameters["uTint"]?.SetValue(new Vector4(VoidTint.ToVector3(), 0.22f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(VoidDeep.ToVector3(), 0f));
            //狙击对决: 暗角猛增 → "猎场聚焦"体感
            fx.Parameters["uVignette"]?.SetValue(0.5f + _focus * 0.45f);
            fx.Parameters["uFogScale"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== ArenaRunic: 全视之域虹环地纹 (签名) =====
        private static void DrawDomainIris() {
            if (_domain <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_domainCenter, 360f, out Vector2 uv, out float radiusFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(MathHelper.Clamp(radiusFrac, 0.15f, 0.9f));
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_domain, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(IrisPurple.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(IrisBlue.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(10f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== 被凝视边缘警示: 四缘 SoftGlow 半出屏呼吸 (零着色器, 不遮挡中央战场) =====
        private static void DrawGazeThreatEdges() {
            if (_gazeThreatDrawn <= 0.02f)
                return;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null)
                return;

            SpriteBatch sb = Main.spriteBatch;
            float breath = 0.8f + System.MathF.Sin(_time * 14f) * 0.2f; //高频呼吸 = 紧迫
            float a = _gazeThreatDrawn * breath;
            Color edge = Color.Lerp(IrisPurple, ThreatRed, _gazeThreatDrawn) * (0.45f * a);
            edge.A = 0;

            int w = Main.screenWidth, h = Main.screenHeight;
            Vector2 origin = glow.Size() * 0.5f;
            //长轴贴边拉伸, 短轴半出屏 — 只染屏幕边缘
            float sx = w / (float)glow.Width * 1.3f;
            float sy = h / (float)glow.Height * 1.3f;

            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            sb.Draw(glow, new Vector2(w * 0.5f, -h * 0.16f), null, edge, 0f, origin, new Vector2(sx, 0.5f), SpriteEffects.None, 0f);
            sb.Draw(glow, new Vector2(w * 0.5f, h + h * 0.16f), null, edge, 0f, origin, new Vector2(sx, 0.5f), SpriteEffects.None, 0f);
            sb.Draw(glow, new Vector2(-w * 0.10f, h * 0.5f), null, edge, 0f, origin, new Vector2(0.5f, sy), SpriteEffects.None, 0f);
            sb.Draw(glow, new Vector2(w + w * 0.10f, h * 0.5f), null, edge, 0f, origin, new Vector2(0.5f, sy), SpriteEffects.None, 0f);
            sb.End();
        }
    }
}
