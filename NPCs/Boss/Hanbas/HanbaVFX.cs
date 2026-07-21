using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.Hanbas
{
    /// <summary>
    /// 旱魃 V3 视觉资产中心: 专属着色器静态缓存 + 色板 + 焦日/焦土/柔光绘制助手。
    /// 着色器为旱魃专属 (Hanba 前缀, ps_3_0), 按并行纪律不注册进 <see cref="ACMShaders"/>。
    /// </summary>
    internal static class HanbaVFX
    {
        // ===== 色板 (§6.1: 纯红只给致命预警, 取 TelegraphColors.Lethal) =====
        /// <summary>焦橙 — 旱魃主题火色。</summary>
        public static readonly Color EmberOrange = new(255, 150, 40);
        /// <summary>烈金 — 焦日核心 / 太阳柱。</summary>
        public static readonly Color SunGold = new(255, 225, 110);
        /// <summary>灰烬黑 — 焦土底色 / 旱情低处。</summary>
        public static readonly Color AshBlack = new(45, 30, 24);
        /// <summary>封尸灰 — 符纸期躯体降饱和。</summary>
        public static readonly Color CorpseGrey = new(158, 150, 138);
        /// <summary>尸气灰绿 — 封尸期点缀 (少量)。</summary>
        public static readonly Color GhostMoss = new(112, 138, 92);

        // ===== 专属着色器缓存 (惰性 ImmediateLoad, 参考 Xuanwu 写法) =====
        private static Asset<Effect> _scorchGround;
        private static Asset<Effect> _scorchSun;

        /// <summary>干裂焦土贴花 (s0=共享噪声满屏; 世界锚定 decal, 不占全屏名额)。</summary>
        public static Effect ScorchGround => Get(ref _scorchGround, "HanbaScorchGround");
        /// <summary>焦日日轮 (s0=共享噪声满屏; Additive overlay, 不占全屏名额)。</summary>
        public static Effect ScorchSun => Get(ref _scorchSun, "HanbaScorchSun");

        private static Effect Get(ref Asset<Effect> slot, string name) {
            if (Main.dedServ)
                return null;
            slot ??= ModContent.Request<Effect>("AncientChineseMythology/Effects/" + name, AssetRequestMode.ImmediateLoad);
            return slot?.Value;
        }

        internal static void Unload() {
            _scorchGround = null;
            _scorchSun = null;
        }

        // ===== 绘制助手 =====

        /// <summary>
        /// 在世界点绘制焦日日轮 (加性 overlay, 完全程序化)。
        /// **须在已有活动批的阶段调用** (如 ModProjectile.PreDraw): 内部 End 当前批 → 画 overlay → 恢复默认批。
        /// 不读 screenTarget, 不占全屏名额 (自带 uIntensity 早退)。
        /// </summary>
        /// <param name="worldCenter">日心世界坐标。</param>
        /// <param name="radiusFrac">日盘半径 (屏幕高度比例, 如 0.18)。</param>
        /// <param name="intensity">整体强度 0~1。</param>
        /// <param name="flare">爆发度 0~1。</param>
        /// <param name="ash">灰蚀度 0~1。</param>
        public static void DrawSunDiscAt(Vector2 worldCenter, float radiusFrac, float intensity, float flare, float ash = 0f) {
            if (Main.dedServ || Main.gameMenu || intensity <= 0.01f)
                return;
            Effect fx = ScorchSun;
            if (fx == null)
                return;

            SetSunParams(fx, (worldCenter - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight),
                radiusFrac, intensity, flare, ash);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>设置 HanbaScorchSun 全部 uniform (uCenter 为归一化屏幕坐标)。供天幕/世界两种调用点复用。</summary>
        public static void SetSunParams(Effect fx, Vector2 centerUV, float radiusFrac, float intensity, float flare, float ash) {
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uRadius"]?.SetValue(MathF.Max(radiusFrac, 0.01f));
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uFlare"]?.SetValue(MathHelper.Clamp(flare, 0f, 1f));
            fx.Parameters["uAsh"]?.SetValue(MathHelper.Clamp(ash, 0f, 1f));
            fx.Parameters["uColorCore"]?.SetValue(SunGold.ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(EmberOrange.ToVector4());
        }

        /// <summary>加性柔光点 (SoftGlow, A=0 走 AlphaBlend 加性混合惯例)。须在活动批内调用。</summary>
        public static void DrawGlow(SpriteBatch sb, Vector2 worldPos, float scale, Color color) {
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null)
                return;
            color.A = 0;
            sb.Draw(glow, worldPos - Main.screenPosition, null, color, 0f, glow.Size() / 2f, scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>HanbaVFX 生命周期挂钩 (ACMMod 反射自动收集, 无需改 ACMMod.cs)。</summary>
    internal class HanbaVFXLoader : IACMLoader
    {
        void IACMLoader.UnLoadData() => HanbaVFX.Unload();
    }

    /// <summary>
    /// 旱魃 V3 屏幕氛围演出系统 (廉价 overlay 层, 参考 AokinHeatScreenSystem)。
    /// 由 Boss / 弹幕每帧发布 0~1 标量与焦土印记, 在 <see cref="PostDrawTiles"/> (实体之下,
    /// 不遮挡弹幕躲避信息) 集中绘制:
    ///   ● <b>ElementalScreenTint</b> — 旱情底色 (上焦橙热雾 / 下灰烬压暗, 覆盖保守 ≤0.26)。
    ///   ● <b>HanbaScorchGround</b> — 干裂焦土印记 (汲取三环 / 坠日焦土场 / 蚀日场), 每帧发布制, ≤8 个。
    ///   ● <b>白闪</b> — 冲击帧 (坠日 1.0 / 死亡 0.5, 本战 ≤2 次)。
    /// 昂贵的全屏 screenTarget 热浪扭曲 (GenericWarp heat) 由 <see cref="Hanba.PostDraw"/> 单独申请名额,
    /// 其强度标量 (脉冲+环境) 由本系统托管衰减。
    /// 纯本地视觉, 服务端零绘制, 受 <see cref="MythologyConfig"/> 降级。
    /// </summary>
    public class HanbaScorchScreenSystem : ModSystem
    {
        private struct ScorchMark
        {
            public Vector2 Center;
            public float Radius;
            public float Progress;
            public float Intensity;
            public bool Ring;
            public float RingWidth;
            public Color Ember;
        }

        private const int MaxMarks = 8;
        private static readonly ScorchMark[] _marks = new ScorchMark[MaxMarks];
        private static int _markCount;
        private static ulong _markFrame;

        private static float _tint;        // 旱情底色 (平滑后)
        private static float _tintTarget;
        private static float _heatWarp;    // 热浪扭曲脉冲 (指数衰减)
        private static float _heatAmbient; // 热浪扭曲环境值 (Boss 每帧发布)
        private static float _flash;       // 白闪
        private static float _time;
        private static ulong _lastPublishFrame;

        /// <summary>由 Boss 每帧调用, 发布旱情氛围标量 (纯本地视觉)。</summary>
        public static void Publish(float droughtTint, float heatAmbient, float time) {
            _tintTarget = MathHelper.Clamp(droughtTint, 0f, 1f);
            _heatAmbient = MathHelper.Clamp(heatAmbient, 0f, 1f);
            _time = time;
            _lastPublishFrame = Main.GameUpdateCount;
        }

        /// <summary>
        /// 登记一个本帧焦土印记 (每帧发布制: 存活期间每帧调用, 无需生命周期管理)。
        /// </summary>
        public static void AddScorchMark(Vector2 worldCenter, float worldRadius, float progress, float intensity,
            bool ring = false, float ringWidth = 0.16f, Color? ember = null) {
            if (Main.dedServ || intensity <= 0.01f)
                return;
            if (_markFrame != Main.GameUpdateCount) {
                _markFrame = Main.GameUpdateCount;
                _markCount = 0;
            }
            if (_markCount >= MaxMarks)
                return;
            _marks[_markCount++] = new ScorchMark {
                Center = worldCenter,
                Radius = worldRadius,
                Progress = MathHelper.Clamp(progress, 0f, 1f),
                Intensity = MathHelper.Clamp(intensity, 0f, 1f),
                Ring = ring,
                RingWidth = ringWidth,
                Ember = ember ?? HanbaVFX.EmberOrange
            };
        }

        /// <summary>热浪扭曲脉冲 (取 max 不累加; 解封尖啸 / 坠日冲击等签名时刻)。</summary>
        public static void PulseHeat(float amount) => _heatWarp = MathF.Max(_heatWarp, MathHelper.Clamp(amount, 0f, 1f));

        /// <summary>全屏白闪 (冲击帧)。本战 ≤2 次: 坠日 1.0 / 死亡 0.5。</summary>
        public static void FlashWhite(float amount) => _flash = MathF.Max(_flash, MathHelper.Clamp(amount, 0f, 1f));

        /// <summary>当前热浪扭曲总强度 (脉冲+环境取 max), 供 Hanba.PostDraw 的 GenericWarp 读取。</summary>
        public static float CurrentHeatWarp => MathF.Max(_heatWarp, _heatAmbient);

        public override void OnWorldUnload() {
            _tint = _tintTarget = _heatWarp = _heatAmbient = _flash = 0f;
            _markCount = 0;
        }

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu)
                return;

            // 标量衰减 (每帧一次, 放在绘制入口统一推进)
            _heatWarp *= 0.94f;
            if (_heatWarp < 0.01f)
                _heatWarp = 0f;
            _flash *= 0.86f;
            if (_flash < 0.01f)
                _flash = 0f;

            // Boss 不在场/未发布时平滑淡出, 避免氛围残留
            if (Main.GameUpdateCount - _lastPublishFrame > 2) {
                _tintTarget = 0f;
                _heatAmbient = 0f;
            }
            _tint = MathHelper.Lerp(_tint, _tintTarget, 0.06f);

            if (!MythologyConfig.FullscreenShadersEnabled) {
                DrawFlash(); // 白闪属冲击帧节拍, 不依赖着色器, 保留
                return;
            }

            DrawDroughtTint();
            DrawScorchMarks();
            DrawFlash();
        }

        // ===== ElementalScreenTint: 旱情底色 =====
        private static void DrawDroughtTint() {
            if (_tint <= 0.01f)
                return;
            Effect fx = ACMShaders.ElementalScreenTint;
            if (fx == null)
                return;

            fx.Parameters["uTime"]?.SetValue(_time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(_tint, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            // 上=焦橙热雾, 下=灰烬压暗; 覆盖保守 (≤0.26), 始终能看清弹幕与红色致命预警
            fx.Parameters["uTint"]?.SetValue(new Vector4(TelegraphColors.Flame.ToVector3(), 0.26f));
            fx.Parameters["uTint2"]?.SetValue(new Vector4(HanbaVFX.AshBlack.ToVector3(), 0f));
            fx.Parameters["uVignette"]?.SetValue(0.40f);
            fx.Parameters["uFogScale"]?.SetValue(2.4f);

            ACMShaders.DrawFullscreenOverlay(fx, BlendState.AlphaBlend);
        }

        // ===== HanbaScorchGround: 焦土印记 =====
        private static void DrawScorchMarks() {
            if (_markFrame != Main.GameUpdateCount || _markCount <= 0)
                return;
            Effect fx = HanbaVFX.ScorchGround;
            if (fx == null)
                return;

            for (int i = 0; i < _markCount; i++) {
                ref ScorchMark m = ref _marks[i];
                ACMShaders.WorldDecalParams(m.Center, m.Radius, out Vector2 uv, out float radiusFrac, out float aspect);
                // 离屏太远的印记直接跳过
                if (uv.X < -0.8f || uv.X > 1.8f || uv.Y < -0.8f || uv.Y > 1.8f)
                    continue;

                fx.Parameters["uTime"]?.SetValue(_time);
                fx.Parameters["uCenter"]?.SetValue(uv);
                fx.Parameters["uRadius"]?.SetValue(radiusFrac);
                fx.Parameters["uIntensity"]?.SetValue(m.Intensity);
                fx.Parameters["uAspect"]?.SetValue(aspect);
                fx.Parameters["uProgress"]?.SetValue(m.Progress);
                fx.Parameters["uRingMode"]?.SetValue(m.Ring ? 1f : 0f);
                fx.Parameters["uRingWidth"]?.SetValue(m.RingWidth);
                fx.Parameters["uColorEmber"]?.SetValue(m.Ember.ToVector4());
                fx.Parameters["uColorAsh"]?.SetValue(new Vector4(HanbaVFX.AshBlack.ToVector3(), 0.85f));

                ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
            }
        }

        // ===== 白闪冲击帧 =====
        private static void DrawFlash() {
            if (_flash <= 0.01f)
                return;
            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Color(255, 248, 230) * _flash);
            sb.End();
        }
    }
}
