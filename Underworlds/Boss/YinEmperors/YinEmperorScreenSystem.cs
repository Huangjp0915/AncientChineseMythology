using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.YinEmperors
{
    /// <summary>
    /// 阴天子 V2 演出屏幕系统 —— 集中绘制三类"非 screenTarget"全屏 overlay（不占全屏后处理名额）：
    ///   ● <b>ElementalScreenTint</b> —— 帝裁/处决的<b>decree-vignette</b>（赤红压顶晕影 + 心跳脉动，覆盖度保守，弹幕始终可读）。
    ///   ● <b>ArenaRunic</b>(uMode=1 牢笼罩) —— 镇魂狱<b>prison-overlay</b>，沿封印收缩半径覆盖场地。
    ///   ● <b>ArenaRunic</b>(uMode=0 法阵) —— 鬼门关钥<b>弱点高亮</b> + 冥眼/帝冥弹<b>落点预告</b>（WorldDecalParams 定位）。
    ///
    /// 昂贵的全屏 screenTarget 重映射（PaletteLUT 阴阳分屏）与大节拍泛光（RadialBloom）由
    /// <see cref="YinEmperor.PostDraw"/> 单独申请 <see cref="ACMShaders.RequestFullscreenSlot"/> 名额绘制（每帧 ≤1）。
    ///
    /// 绘制位于 <see cref="PostDrawTiles"/>（无活动批，实体之下）：氛围/地纹在弹幕下层 → 不遮挡躲避信息（§6.6）。
    /// 由 <see cref="YinEmperor.AI"/> 每帧 <see cref="Publish"/>（AI 在全端运行，多人客户端亦可见）。纯本地视觉，服务端零绘制，受 MythologyConfig 降级。
    /// </summary>
    public class YinEmperorScreenSystem : ModSystem
    {
        // —— 连续发布态（每帧由 Boss 更新）——
        private static float _time;
        private static float _vignette;
        private static float _prison;
        private static Vector2 _prisonCenter;
        private static float _prisonRadiusWorld;
        private static float _weak;
        private static Vector2 _weakPos;
        private static ulong _lastPublishFrame;

        // —— 平滑后的当前强度 ——
        private static float _vignetteCur;
        private static float _prisonCur;
        private static float _weakCur;

        // —— 一次性落点预告环形缓冲（冥眼/帝冥弹）——
        private struct Telegraph
        {
            public Vector2 Pos;
            public float RadiusWorld;
            public int Age;
            public int Life;
            public Vector3 Color;
            public bool Active;
        }

        private const int MaxTelegraphs = 4;
        private static readonly Telegraph[] _tels = new Telegraph[MaxTelegraphs];

        /// <summary>由阴天子每帧调用，发布当前审判演出标量（纯本地视觉，全端运行）。</summary>
        public static void Publish(float time, float vignette,
            float prison, Vector2 prisonCenter, float prisonRadiusWorld,
            float weak, Vector2 weakPos) {
            _time = time;
            _vignette = vignette;
            _prison = prison;
            _prisonCenter = prisonCenter;
            _prisonRadiusWorld = prisonRadiusWorld;
            _weak = weak;
            _weakPos = weakPos;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        /// <summary>登记一处落点/列阵预告法阵（冥眼列阵、帝冥弹落点）。客户端本地视觉。</summary>
        public static void AddTelegraph(Vector2 world, float worldRadius, int life, Color color) {
            if (Main.dedServ)
                return;
            int slot = 0;
            int oldest = -1;
            for (int i = 0; i < MaxTelegraphs; i++) {
                if (!_tels[i].Active) {
                    slot = i;
                    oldest = -1;
                    break;
                }
                if (_tels[i].Age > oldest) {
                    oldest = _tels[i].Age;
                    slot = i;
                }
            }
            _tels[slot] = new Telegraph {
                Pos = world,
                RadiusWorld = worldRadius,
                Age = 0,
                Life = life,
                Color = color.ToVector3(),
                Active = true
            };
        }

        public override void OnWorldUnload() {
            _vignette = _prison = _weak = 0f;
            _vignetteCur = _prisonCur = _weakCur = 0f;
            for (int i = 0; i < MaxTelegraphs; i++)
                _tels[i].Active = false;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;
            if (!MythologyConfig.FullscreenShadersEnabled)
                return;

            bool stale = Main.GameUpdateCount - _lastPublishFrame > 2;
            _vignetteCur = MathHelper.Lerp(_vignetteCur, stale ? 0f : _vignette, 0.08f);
            _prisonCur = MathHelper.Lerp(_prisonCur, stale ? 0f : _prison, 0.1f);
            _weakCur = MathHelper.Lerp(_weakCur, stale ? 0f : _weak, 0.12f);

            DrawDecreeVignette();
            DrawPrisonOverlay();
            DrawWeakPointTell();
            DrawTelegraphs();
        }

        // ===== decree-vignette：帝裁/处决赤红压顶晕影（ElementalScreenTint）=====
        private static void DrawDecreeVignette() {
            if (_vignetteCur <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            // 心跳脉动：低频压迫感
            float heartbeat = 0.85f + 0.15f * System.MathF.Sin(_time * 4.2f);
            float intensity = MathHelper.Clamp(_vignetteCur * heartbeat, 0f, 1f);

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(intensity);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            // 覆盖度保守：上层赤红冥威、下层渊紫；以暗角为主，中心保持弹幕可读
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.Execution.ToVector3(), 0.10f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(YinEmperorHelper.AbyssPurple.ToVector3(), 0f));
            fx.Parameters["uVignette"]?.SetValue(0.5f);
            fx.Parameters["uFogScale"]?.SetValue(2.2f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== prison-overlay：镇魂狱牢笼罩（ArenaRunic uMode=1）=====
        private static void DrawPrisonOverlay() {
            if (_prisonCur <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_prisonCenter, _prisonRadiusWorld,
                out Vector2 uv, out float radiusFrac, out float aspect);

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(MathHelper.Clamp(radiusFrac, 0.1f, 1.2f));
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_prisonCur, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.NetherViolet.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(YinEmperorHelper.AbyssPurple.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(12f);
            fx.Parameters["uMode"]?.SetValue(1f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== 鬼门关钥弱点法阵高亮（ArenaRunic uMode=0，柔白青）=====
        private static void DrawWeakPointTell() {
            if (_weakCur <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(_weakPos, 150f, out Vector2 uv, out float radiusFrac, out float aspect);

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(MathHelper.Clamp(radiusFrac, 0.05f, 0.5f));
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_weakCur, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(YinEmperorHelper.SoulLanternCyan.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(TelegraphColors.Safe.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(8f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // ===== 落点预告法阵（冥眼列阵 / 帝冥弹落点）=====
        private static void DrawTelegraphs() {
            Effect fx = ACMShaders.ArenaRunic;
            for (int i = 0; i < MaxTelegraphs; i++) {
                ref Telegraph t = ref _tels[i];
                if (!t.Active)
                    continue;
                t.Age++;
                if (t.Age >= t.Life) {
                    t.Active = false;
                    continue;
                }
                if (fx == null)
                    continue;

                // 由细到实再淡出：前 70% 渐强，后 30% 淡出
                float p = t.Age / (float)t.Life;
                float fade = p < 0.7f ? p / 0.7f : (1f - p) / 0.3f;
                fade = MathHelper.Clamp(fade, 0f, 1f) * 0.85f;
                if (fade <= 0.01f)
                    continue;

                ACMShaders.WorldDecalParams(t.Pos, t.RadiusWorld, out Vector2 uv, out float radiusFrac, out float aspect);

                fx.Parameters["uTime"]?.SetValue(_time);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(MathHelper.Clamp(radiusFrac, 0.05f, 0.7f));
                fx.Parameters["uIntensity"]?.SetValue(fade);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(t.Color, 1f));
                fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(YinEmperorHelper.ImperialGold.ToVector3(), 1f));
                fx.Parameters["uRuneFreq"]?.SetValue(9f);
                fx.Parameters["uMode"]?.SetValue(0f);
                fx.Parameters["uShape"]?.SetValue(0f);

                ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
            }
        }
    }
}
