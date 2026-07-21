using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.NiutouMamian
{
    /// <summary>
    /// 牛头马面 V3 演出屏幕系统。集中绘制四类"非 screenTarget"廉价层 (全部不占全屏后处理名额):
    ///   1. 地府氛围染屏 (ElementalScreenTint, 幽蓝紫鬼绿) —— Boss 每帧 <see cref="Publish"/>;
    ///   2. 阎罗令红印晕影 (ElementalScreenTint 第二层, 处决红) —— P3 起 <see cref="PublishVignette"/>;
    ///   3. 鬼门法阵 (专属 NiuMaNetherGate 着色器): 连续槽 (入场之门/锁命中枢) + 一次性环形缓冲 (落点印记);
    ///   4. 全屏白闪冲击帧 (PostDrawInterface 直画, 全场仅死亡演出使用一次)。
    /// 1~3 绘制于 <see cref="PostDrawTiles"/> (无活动批, 实体之下, 不遮挡弹幕信息)。
    /// 纯本地视觉: 服务端零绘制, 受 MythologyConfig 降级。
    /// </summary>
    public class NiuMaScreenSystem : ModSystem
    {
        // ===== 染屏 (同帧多源取 max) =====
        private static float _target;
        private static float _draw;
        private static ulong _frame;
        private static ulong _lastPublish;

        // ===== 处决晕影 =====
        private static float _vigTarget;
        private static float _vigDraw;
        private static ulong _vigLastPublish;

        // ===== 连续鬼门槽 (单槽, 每帧发布) =====
        private static Vector2 _gatePos;
        private static float _gateRadius;
        private static float _gateOpen;
        private static float _gateTarget;
        private static float _gateDraw;
        private static bool _gateJade;          // 翠玉配色 (复生反制) / 默认红紫
        private static ulong _gateLastPublish;

        // ===== 一次性法阵印记环形缓冲 (落点/换岗火花) =====
        private struct GateMark
        {
            public Vector2 Pos;
            public float Radius;
            public int Age, Life;
            public bool Active;
        }
        private const int MaxMarks = 4;
        private static readonly GateMark[] _marks = new GateMark[MaxMarks];

        // ===== 白闪冲击帧 =====
        private static int _flashLeft;
        private static int _flashTotal;
        private static float _flashPeak;

        /// <summary>由 Boss 每帧调用, 发布地府氛围强度 (同帧多源取 max)。</summary>
        public static void Publish(Vector2 center, float intensity) {
            if (Main.dedServ)
                return;
            if (Main.GameUpdateCount != _frame) {
                _frame = Main.GameUpdateCount;
                _target = 0f;
            }
            if (intensity > _target)
                _target = intensity;
            _lastPublish = Main.GameUpdateCount;
        }

        /// <summary>发布阎罗令红印晕影强度 (P3 处决氛围, 每帧调用; 覆盖度保守)。</summary>
        public static void PublishVignette(float intensity) {
            if (Main.dedServ)
                return;
            _vigTarget = MathHelper.Clamp(intensity, 0f, 1f);
            _vigLastPublish = Main.GameUpdateCount;
        }

        /// <summary>发布连续鬼门法阵 (入场之门/锁命中枢/引魂法阵)。每帧调用, 停止发布即淡出。</summary>
        public static void PublishGate(Vector2 worldPos, float worldRadius, float open, float intensity, bool jade = false) {
            if (Main.dedServ)
                return;
            _gatePos = worldPos;
            _gateRadius = worldRadius;
            _gateOpen = MathHelper.Clamp(open, 0f, 1f);
            _gateTarget = MathHelper.Clamp(intensity, 0f, 1f);
            _gateJade = jade;
            _gateLastPublish = Main.GameUpdateCount;
        }

        /// <summary>登记一处一次性法阵印记 (链锤落点/换岗交汇), 自带渐强→淡出包络。</summary>
        public static void AddGateMark(Vector2 worldPos, float worldRadius, int life) {
            if (Main.dedServ)
                return;
            int slot = 0;
            int oldest = -1;
            for (int i = 0; i < MaxMarks; i++) {
                if (!_marks[i].Active) {
                    slot = i;
                    oldest = -1;
                    break;
                }
                if (_marks[i].Age > oldest) {
                    oldest = _marks[i].Age;
                    slot = i;
                }
            }
            _marks[slot] = new GateMark { Pos = worldPos, Radius = worldRadius, Age = 0, Life = life, Active = true };
        }

        /// <summary>全屏白闪冲击帧 (本战唯一大节拍: 终幕死亡)。frames 建议 3~6, peak ≤0.9。</summary>
        public static void FlashWhite(int frames, float peak) {
            if (Main.dedServ || !MythologyConfig.FullscreenShadersEnabled)
                return;
            _flashLeft = _flashTotal = System.Math.Max(frames, 1);
            _flashPeak = MathHelper.Clamp(peak, 0f, 0.92f);
        }

        public override void OnWorldUnload() {
            _target = _draw = _vigTarget = _vigDraw = _gateTarget = _gateDraw = 0f;
            _flashLeft = 0;
            for (int i = 0; i < MaxMarks; i++)
                _marks[i].Active = false;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu || !MythologyConfig.FullscreenShadersEnabled)
                return;

            DrawTint();
            DrawVignette();
            DrawGateContinuous();
            DrawGateMarks();
        }

        // ===== 染屏 (幽蓝紫鬼绿地府氛围) =====
        private void DrawTint() {
            float aim = (Main.GameUpdateCount - _lastPublish > 2) ? 0f : _target;
            _draw = MathHelper.Lerp(_draw, aim, aim > _draw ? 0.04f : 0.08f);
            if (_draw <= 0.01f)
                return;

            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_draw, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.NetherViolet.ToVector3(), 0.26f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(TelegraphColors.GhostGreen.ToVector3() * 0.4f, 0f));
            fx.Parameters["uVignette"]?.SetValue(0.42f);
            fx.Parameters["uFogScale"]?.SetValue(2.2f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== 阎罗令红印晕影 (心跳脉动, 以暗角为主) =====
        private void DrawVignette() {
            float aim = (Main.GameUpdateCount - _vigLastPublish > 2) ? 0f : _vigTarget;
            _vigDraw = MathHelper.Lerp(_vigDraw, aim, 0.07f);
            if (_vigDraw <= 0.01f)
                return;

            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            float heartbeat = 0.85f + 0.15f * System.MathF.Sin((float)Main.GlobalTimeWrappedHourly * 4.6f);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_vigDraw * heartbeat, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.Execution.ToVector3(), 0.10f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(NiuMaHelper.EmberRed.ToVector3() * 0.3f, 0f));
            fx.Parameters["uVignette"]?.SetValue(0.55f);
            fx.Parameters["uFogScale"]?.SetValue(2.0f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== 连续鬼门 =====
        private void DrawGateContinuous() {
            float aim = (Main.GameUpdateCount - _gateLastPublish > 2) ? 0f : _gateTarget;
            _gateDraw = MathHelper.Lerp(_gateDraw, aim, aim > _gateDraw ? 0.12f : 0.08f);
            if (_gateDraw <= 0.02f)
                return;

            Effect fx = NiuMaHelper.NetherGate;
            if (fx == null)
                return;

            Color a = _gateJade ? TelegraphColors.Safe : NiuMaHelper.EmberRed;
            Color b = _gateJade ? TelegraphColors.GhostGreen : NiuMaHelper.GhostViolet;
            NiuMaHelper.SetGateParams(fx, _gatePos, _gateRadius, a, b, _gateOpen, 0f, _gateDraw);
            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
        }

        // ===== 一次性印记 =====
        private void DrawGateMarks() {
            Effect fx = NiuMaHelper.NetherGate;
            for (int i = 0; i < MaxMarks; i++) {
                ref GateMark m = ref _marks[i];
                if (!m.Active)
                    continue;
                m.Age++;
                if (m.Age >= m.Life) {
                    m.Active = false;
                    continue;
                }
                if (fx == null)
                    continue;

                float p = m.Age / (float)m.Life;
                float fade = p < 0.3f ? p / 0.3f : (1f - p) / 0.7f;
                fade = MathHelper.Clamp(fade, 0f, 1f) * 0.8f;
                if (fade <= 0.02f)
                    continue;

                NiuMaHelper.SetGateParams(fx, m.Pos, m.Radius, NiuMaHelper.EmberRed, NiuMaHelper.GhostViolet,
                    0.2f + 0.5f * p, p * 2.4f, fade);
                ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.Additive);
            }
        }

        // ===== 白闪冲击帧 (界面层之上短暂覆盖, 2~6 帧) =====
        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            if (_flashLeft <= 0 || Main.gameMenu)
                return;
            _flashLeft--;
            float t = _flashTotal <= 0 ? 0f : _flashLeft / (float)_flashTotal;
            float a = _flashPeak * t;
            if (a <= 0.01f)
                return;
            spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White * a);
        }
    }
}
