using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology
{
    /// <summary>
    /// Boss V2 共享着色器注册中心 / 加载器 (地基件, 全 Boss 复用)。
    /// 规范着色器目录见 docs/BOSS_REDO_PLAN_V2.md §3.1。
    ///
    /// 约定 (toolkit §A.3 / §C.4):
    ///  - 每个 Effect 只 Request 一次, 缓存为 static <see cref="Asset{T}"/> (惰性 ImmediateLoad, !Main.dedServ 守卫)。
    ///  - 共享可平铺噪声纹理只生成一次 (<see cref="NoiseTexture"/>), Unload 时 Dispose + 置 null。
    ///  - 两类用法各有 Apply 助手:
    ///     1) 全屏后处理 (喂 Main.screenTarget): <see cref="ApplyScreenPostProcess"/> / <see cref="DrawFullscreenOverlay"/>。
    ///     2) 世界/图元绘制: <see cref="DrawScreenSpaceDecal"/> (地纹/牢笼) 与 <see cref="BeamGrad"/> 顶点带。
    ///  - **性能契约: 同屏全屏后处理 ≤ 1 个生效**。调用方在套 <see cref="ApplyScreenPostProcess"/> 前先
    ///    <see cref="RequestFullscreenSlot"/>, 仅最高优先级 Boss 拿到该帧名额; 强度 &lt; 0.01 直接 return。
    /// </summary>
    public class ACMShaders : IACMLoader
    {
        private const string Path = "AncientChineseMythology/Effects/";

        // ===== Effect 缓存 =====
        private static Asset<Effect> _dissolveBurn;
        private static Asset<Effect> _genericWarp;
        private static Asset<Effect> _elementalTint;
        private static Asset<Effect> _paletteLUT;
        private static Asset<Effect> _arenaRunic;
        private static Asset<Effect> _beamGrad;
        private static Asset<Effect> _radialBloom;
        private static Asset<Effect> _reflectWard;

        /// <summary>溶解/灼烧 — Boss 贴图单 pass (s0=贴图, s1=噪声)。</summary>
        public static Effect DissolveBurn => Get(ref _dissolveBurn, "DissolveBurn");
        /// <summary>泛化主题扭曲 — 全屏后处理 (s0=screenTarget, s1=噪声)。heat/frost/fog/rift/void/refraction 变体。</summary>
        public static Effect GenericWarp => Get(ref _genericWarp, "GenericWarp");
        /// <summary>元素屏幕染色 — 全屏氛围 overlay (预乘 Alpha, 传色即可)。</summary>
        public static Effect ElementalScreenTint => Get(ref _elementalTint, "ElementalScreenTint");
        /// <summary>屏幕调色 LUT — 全屏后处理 (阴影/高光重映射 + 阴阳分屏)。</summary>
        public static Effect PaletteLUT => Get(ref _paletteLUT, "PaletteLUT");
        /// <summary>地纹/法阵/牢笼 — 屏幕空间 SDF (s0=噪声)。uMode: 0=法阵 1=牢笼罩。</summary>
        public static Effect ArenaRunic => Get(ref _arenaRunic, "ArenaRunic");
        /// <summary>光束梯度/流动 — TriangleStrip 直带图元 (仅 PS, s1=流动噪声)。</summary>
        public static Effect BeamGrad => Get(ref _beamGrad, "BeamGrad");
        /// <summary>径向泛光 — 加性径向 bloom (全屏占位绘制, 建议 Additive)。</summary>
        public static Effect RadialBloom => Get(ref _radialBloom, "RadialBloom");
        /// <summary>折射护盾 — 六边面板折射护罩 (s0=screenTarget, s1=噪声)。</summary>
        public static Effect ReflectWard => Get(ref _reflectWard, "ReflectWard");

        private static Effect Get(ref Asset<Effect> slot, string name) {
            if (Main.dedServ)
                return null;
            slot ??= ModContent.Request<Effect>(Path + name, AssetRequestMode.ImmediateLoad);
            return slot?.Value;
        }

        // ===== 共享可平铺噪声 =====
        private static Texture2D _noise;

        /// <summary>共享可平铺三通道 FBM 噪声 (256², 5 octave)。惰性生成一次, 切勿每帧 new。</summary>
        public static Texture2D NoiseTexture {
            get {
                if (Main.dedServ)
                    return null;
                if (_noise == null || _noise.IsDisposed)
                    _noise = ACMUtils.GenerateTileableNoise(Main.graphics.GraphicsDevice, 256, 5);
                return _noise;
            }
        }

        // ===== 全屏后处理名额 (性能契约: 每帧 ≤ 1) =====
        private static ulong _lastFullscreenFrame;

        /// <summary>
        /// 申请本帧唯一的全屏后处理名额。返回 true 表示拿到名额(可绘制), false 表示本帧已被占用或被配置关闭。
        /// 高价值 Boss 应先调用并按优先级让位; 多 Boss 同屏只跑一个全屏后处理。
        /// </summary>
        public static bool RequestFullscreenSlot() {
            if (Main.dedServ || !MythologyConfig.FullscreenShadersEnabled)
                return false;
            if (_lastFullscreenFrame == Main.GameUpdateCount)
                return false;
            _lastFullscreenFrame = Main.GameUpdateCount;
            return true;
        }

        /// <summary>
        /// 本帧是否已有调用方通过 <see cref="RequestFullscreenSlot"/> 取得全屏后处理名额。
        /// 作为 <see cref="ApplyScreenPostProcess"/> 读写 <see cref="Main.screenTarget"/> 的"安全上下文"判据:
        /// 唯一合法用法是先取得名额、再立即做全屏后处理; 否则极可能是在普通弹幕 PreDraw 等阶段误调,
        /// 会把整屏画到世界上产生脏帧。
        /// </summary>
        public static bool FullscreenSlotGrantedThisFrame => !Main.dedServ && _lastFullscreenFrame == Main.GameUpdateCount;

        // ============================================================
        //  用法 1: 全屏后处理 (喂 Main.screenTarget)
        // ============================================================

        /// <summary>
        /// 在**已激活的 SpriteBatch**中插入一次全屏后处理: 结束当前批 → 以 effect 重画 screenTarget → 恢复项目默认批。
        /// 适用 GenericWarp / PaletteLUT / ReflectWard。
        ///
        /// <para><b>唯一合法调用点 / 安全护栏:</b> 本方法读写 <see cref="Main.screenTarget"/>, 仅允许在
        /// 已通过 <see cref="RequestFullscreenSlot"/> 取得本帧全屏名额后**立即**调用 (见
        /// <see cref="FullscreenSlotGrantedThisFrame"/>)。未取得名额即调用 (典型为在普通弹幕
        /// <see cref="ModProjectile.PreDraw"/> 中误用) 会把整屏画到世界上产生脏帧 — 此时直接 <c>return</c>
        /// 并 (开发期) 打日志警告。请始终用
        /// <c>if (ACMShaders.RequestFullscreenSlot()) { 设参数; ACMShaders.ApplyScreenPostProcess(sb, fx); }</c>
        /// 模式, 或经 <see cref="Helpers.WeaponVFX.ApplyPaletteTint"/> 入口。</para>
        /// </summary>
        public static void ApplyScreenPostProcess(SpriteBatch sb, Effect fx, bool bindNoise = true) {
            if (Main.dedServ || fx == null || sb == null)
                return;

            // 安全护栏: 全屏后处理(读写 screenTarget)只允许在取得名额后执行 — 否则疑似普通绘制阶段误用, 跳过避免脏帧。
            if (!FullscreenSlotGrantedThisFrame) {
                ModContent.GetInstance<ACMMod>()?.Logger?.Warn(
                    "ACMShaders.ApplyScreenPostProcess 在未取得全屏名额时被调用 (疑似在普通弹幕 PreDraw 等阶段误用), 已跳过。" +
                    "唯一合法用法: 先 if (ACMShaders.RequestFullscreenSlot()) 取得名额再调用 (或经 WeaponVFX.ApplyPaletteTint)。");
                return;
            }

            if (bindNoise) {
                GraphicsDevice gd = Main.graphics.GraphicsDevice;
                gd.Textures[1] = NoiseTexture;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 自行开/合批的全屏 overlay 绘制 (画满屏占位像素, 着色器完全程序化)。
        /// 适用 ElementalScreenTint (AlphaBlend 预乘) / RadialBloom (Additive)。
        /// 用于 ModSystem.PostDrawTiles 等无活动批的阶段。
        /// </summary>
        public static void DrawFullscreenOverlay(Effect fx, BlendState blend = null) {
            if (Main.dedServ || fx == null || Main.gameMenu)
                return;

            blend ??= BlendState.AlphaBlend;
            SpriteBatch sb = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            sb.Begin(SpriteSortMode.Immediate, blend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);
            sb.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
            sb.End();
        }

        // ============================================================
        //  用法 2: 世界/屏幕空间装饰 (地纹/法阵/牢笼)
        // ============================================================

        /// <summary>
        /// 在**已激活的 SpriteBatch**中绘制屏幕空间地纹/法阵/牢笼: 以共享噪声为载体满屏绘制 (uCenter/uRadius 走屏幕 UV)。
        /// 适用 ArenaRunic。绘制前请设好 effect 参数 (uCenter/uRadius/uColorPrimary 等)。绘制完恢复默认批。
        /// </summary>
        public static void DrawScreenSpaceDecal(SpriteBatch sb, Effect fx, BlendState blend = null) {
            if (Main.dedServ || fx == null || sb == null)
                return;

            blend ??= BlendState.AlphaBlend;
            Texture2D noise = NoiseTexture;
            if (noise == null)
                return;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, blend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);
            sb.Draw(noise, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
            sb.End();

            RestoreDefaultBatch(sb);
        }

        /// <summary>恢复项目默认 SpriteBatch 状态 (PointClamp + GameViewMatrix), 避免污染后续绘制 (§C.4#5)。</summary>
        public static void RestoreDefaultBatch(SpriteBatch sb) {
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// 自行开/合批的屏幕空间地纹/法阵/牢笼绘制 (§用法2 的"无活动批"补位)。
        /// 与 <see cref="DrawScreenSpaceDecal"/> 的区别: 后者假定已有活动批 (End→Begin→End→恢复),
        /// 本方法自行 Begin/End (类比 <see cref="DrawFullscreenOverlay"/>), 供 ModSystem.PostDrawTiles /
        /// ModProjectile.PreDraw 等"无活动批"阶段直接调用, 调用方无需手动开合批。
        /// 绘制前请设好 effect 参数 (uCenter/uRadius/uShape/uColorPrimary 等; 可用 <see cref="SetCommonParams"/> 减样板)。
        /// </summary>
        public static void DrawScreenSpaceDecalStandalone(Effect fx, BlendState blend = null) {
            if (Main.dedServ || fx == null || Main.gameMenu)
                return;
            Texture2D noise = NoiseTexture;
            if (noise == null)
                return;

            blend ??= BlendState.AlphaBlend;
            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Immediate, blend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Matrix.Identity);
            sb.Draw(noise, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
            sb.End();
        }

        // ============================================================
        //  共享坐标/参数助手 (Wave-1 反馈: 抽掉每 Boss 重抄的样板)
        // ============================================================

        /// <summary>
        /// 缩放感知的"世界圆/方 → 屏幕 UV"换算 (从 DazhengArenaBarrier/Hanba 抽取并补齐 Wave-1 遗漏的 zoom 项)。
        /// 同时考虑 <see cref="Main.GameViewMatrix"/>.Zoom, 故放大镜/缩放下中心与半径仍对齐世界。
        /// </summary>
        /// <param name="worldCenter">世界坐标中心。</param>
        /// <param name="worldRadius">世界半径(像素)。圆=半径; 方=半边长。</param>
        /// <param name="uvCenter">输出: 归一化屏幕中心 (0~1)。</param>
        /// <param name="radiusFrac">输出: 半径占屏幕高度比例 (着色器 uRadius)。</param>
        /// <param name="aspect">输出: 宽高比 width/height (着色器 uAspect)。</param>
        public static void WorldDecalParams(Vector2 worldCenter, float worldRadius,
            out Vector2 uvCenter, out float radiusFrac, out float aspect) {
            Vector2 worldOffset = worldCenter - Main.screenPosition;
            Vector2 halfScreen = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
            float zoom = Main.GameViewMatrix.Zoom.X;             // Wave-1 缺失项: 不乘 zoom 在缩放下会错位
            Vector2 screenPos = (worldOffset - halfScreen) * zoom + halfScreen;
            float screenRadius = worldRadius * zoom;

            uvCenter = screenPos / new Vector2(Main.screenWidth, Main.screenHeight);
            radiusFrac = screenRadius / Main.screenHeight;
            aspect = (float)Main.screenWidth / Main.screenHeight;
        }

        /// <summary>
        /// 一次性设置最常见的四个共享 uniform: uTime(秒)/uCenter(世界→屏幕UV)/uAspect/uIntensity。
        /// 省去每个调用点重复的 ~4 行样板; 仅设存在的参数 (?. 容错)。
        /// 时间统一用 <see cref="Main.GlobalTimeWrappedHourly"/> (秒); 需自定义时间/中心的高级调用可改用各自 Parameters。
        /// </summary>
        public static void SetCommonParams(Effect fx, Vector2 worldCenter, float intensity) {
            if (fx == null)
                return;
            Vector2 uv = (worldCenter - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
        }

        // ============================================================
        //  用法 3: 顶点图元 — 光束直带 (BeamGrad 原语)
        // ============================================================

        /// <summary>
        /// 通用光束直带绘制 (从旱魃 HanbaLaser.DrawBeamGrad 提升为共享原语; 供雷柱/链电/审判射线/金柱复用)。
        /// 用 <see cref="ACMUtils.BuildRibbonStrip"/> 由两端点退化成直线带, 设好 BeamGrad 全部 uniform,
        /// 自行 End→Begin(Immediate, Additive, LinearWrap, GameViewMatrix)→绑噪声到 s0/s1→Apply→DrawUserPrimitives→恢复默认批。
        ///
        /// <para><b>顶点契约:</b> 顶点位置为<b>世界坐标 - <see cref="Main.screenPosition"/></b> (即屏幕空间像素),
        /// 配合 <see cref="Main.GameViewMatrix"/>.TransformationMatrix 变换到裁剪空间 (与 §B.1 拖尾同约定)。
        /// uv.x=沿长 0~1, uv.y=横宽 0~1。**须在已有活动批的阶段调用** (如 ModProjectile.PreDraw), 本方法会 End 当前批。</para>
        /// </summary>
        /// <param name="worldStart">光束起点(世界坐标)。</param>
        /// <param name="worldEnd">光束终点(世界坐标)。</param>
        /// <param name="halfWidth">屏幕像素半宽。</param>
        /// <param name="core">核心色 (a=芯部不透明度权重)。</param>
        /// <param name="edge">边缘色 (a=边缘不透明度权重)。</param>
        /// <param name="intensity">整体强度 0~1 (兼作生长/淡入淡出)。</param>
        /// <param name="flowSpeed">流动速度 (默认 1.4)。</param>
        /// <param name="flowScale">流动纹理尺度 (默认 2.0)。</param>
        /// <param name="coreSharp">核心收窄锐度 (默认 2.2)。</param>
        /// <param name="coreGlow">芯部加法过曝辉度 (默认 &lt;0 → 取 core.A/255, 沿用旧 alpha 行为)。</param>
        public static void DrawBeam(Vector2 worldStart, Vector2 worldEnd, float halfWidth,
            Color core, Color edge, float intensity,
            float flowSpeed = 1.4f, float flowScale = 2.0f, float coreSharp = 2.2f, float coreGlow = -1f) {
            if (Main.dedServ || intensity <= 0.01f || halfWidth < 0.5f)
                return;

            Effect fx = BeamGrad;
            if (fx == null)
                return;

            Vector2 a = worldStart - Main.screenPosition;
            Vector2 b = worldEnd - Main.screenPosition;
            if ((b - a).LengthSquared() < 1f)
                return;

            var verts = ACMUtils.BuildRibbonStrip([a, b], _ => halfWidth, _ => Color.White, 0f, 1);
            if (verts.Length < 4)
                return;

            if (coreGlow < 0f)
                coreGlow = core.A / 255f;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uColorCore"]?.SetValue(core.ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(edge.ToVector4());
            fx.Parameters["uCoreGlow"]?.SetValue(coreGlow);
            fx.Parameters["uFlowSpeed"]?.SetValue(flowSpeed);
            fx.Parameters["uFlowScale"]?.SetValue(flowScale);
            fx.Parameters["uCoreSharp"]?.SetValue(coreSharp);
            fx.Parameters["uUseTexture"]?.SetValue(0f);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Texture2D noise = NoiseTexture;
            gd.Textures[0] = noise;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

            sb.End();
            RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 在世界点上叠一层加性径向泛光 (从旱魃 DrawRadialBloomOverlay 提升; 蓄力/爆发/处决配方通用)。
        /// 内部自动 <see cref="RequestFullscreenSlot"/> (占本帧唯一全屏名额, 多 Boss 同屏只跑一个), 完成
        /// 世界→屏幕UV 换算与全屏 overlay 开合批。**须在已有活动批的阶段调用** (如 ModProjectile.PreDraw):
        /// 本方法会先 End 当前批 → 画 overlay → 恢复默认批。
        /// </summary>
        /// <param name="worldCenter">泛光中心(世界坐标)。</param>
        /// <param name="radius">泛光半径(屏幕高度比例, 如 0.17)。</param>
        /// <param name="intensity">整体强度 0~1。</param>
        /// <param name="color">泛光色。</param>
        /// <param name="rayCount">光芒条数 (0=纯圆晕, 默认 10)。</param>
        /// <param name="falloff">衰减锐度 (默认 2.5)。</param>
        public static void DrawRadialBloomAt(Vector2 worldCenter, float radius, float intensity, Color color,
            float rayCount = 10f, float falloff = 2.5f) {
            if (Main.dedServ || Main.gameMenu || intensity <= 0.01f)
                return;
            if (!RequestFullscreenSlot())
                return;

            Effect fx = RadialBloom;
            if (fx == null)
                return;

            Vector2 uv = (worldCenter - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(radius);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uColor"]?.SetValue(color.ToVector4());
            fx.Parameters["uRayCount"]?.SetValue(rayCount);
            fx.Parameters["uFalloff"]?.SetValue(falloff);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            DrawFullscreenOverlay(fx, BlendState.Additive);
            RestoreDefaultBatch(sb);
        }

        // ===== 生命周期 (IACMLoader) =====
        void IACMLoader.UnLoadData() {
            _dissolveBurn = null;
            _genericWarp = null;
            _elementalTint = null;
            _paletteLUT = null;
            _arenaRunic = null;
            _beamGrad = null;
            _radialBloom = null;
            _reflectWard = null;

            _noise?.Dispose();
            _noise = null;
        }
    }
}
