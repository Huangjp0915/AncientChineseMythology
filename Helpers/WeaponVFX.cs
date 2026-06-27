using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics;
using Terraria.ModLoader;

namespace AncientChineseMythology.Helpers
{
    /// <summary>
    /// 共享武器 VFX 辅助层 (全武器重做地基件)。
    ///
    /// 把工具箱 §B 已实现原语 (ribbon / 冲击环 / 径向辉光 / 溶解 / 调色) 封装成
    /// **静态、可被任意武器弹幕/绘制复用**的方法, 严守 toolkit §C.4 性能护栏 / §C.5 多人守则:
    ///  - Effect/纹理只 Request 一次 (静态缓存, ImmediateLoad), 禁止每帧 Request。
    ///  - 所有绘制方法 <c>if (Main.dedServ) return;</c>; 自行 End→Begin→画→End→恢复项目默认批。
    ///  - 屏幕震动取 max 不累加 (走 <see cref="ACMScreenShakeSystem"/> 预算 + <see cref="MythologyConfig.ShakeScale"/>)。
    ///  - 全屏后处理 (PaletteLUT / RadialBloom) 走 <see cref="ACMShaders.RequestFullscreenSlot"/> 名额仲裁, 同屏 ≤ 1。
    ///
    /// <para><b>复用约定 (后续区域 worker 必读):</b></para>
    /// <list type="number">
    ///   <item>绘制类方法只能在**有绘制上下文**的阶段调用 (PreDraw / PostDraw / 绘制 System)。
    ///   命中反馈 (OnHitNPC 等更新阶段)请用 <see cref="ACMWeaponBurst"/> 生成一次性演出弹幕, 切勿在更新里直接绘制。</item>
    ///   <item>拖尾统一"外宽暗 + 内窄亮"双层 (§B.1 二次迭代建议); 顶点位置 = 世界坐标 - <see cref="Main.screenPosition"/>,
    ///   配 <see cref="Main.GameViewMatrix"/>.TransformationMatrix。</item>
    ///   <item>染屏 (<see cref="ApplyPaletteTint"/>) 强度 ≤ 0.15 且占全屏名额, 仅用于大招/处决"短暂定调", 基础武器请改用拖尾/边色承载主题色。</item>
    /// </list>
    /// </summary>
    public static class WeaponVFX
    {
        // ============================================================
        //  1) 着色器缓存加载 (惰性 ImmediateLoad, 禁止每帧 Request — §C.4#1)
        // ============================================================

        private static readonly Dictionary<string, Asset<Effect>> _effectCache = new();

        /// <summary>
        /// 按名取共享着色器 (相对 <c>AncientChineseMythology/Effects/</c>, 不带扩展名), 惰性缓存。
        /// 服务端直接返回 null。已在 <see cref="ACMShaders"/> 暴露强类型属性的 (DissolveBurn/PaletteLUT/…) 优先用那些;
        /// 本方法用于按名取任意 (含未来新增) 着色器, 避免每个调用点各写一份缓存。
        /// </summary>
        public static Effect GetEffect(string name) {
            if (Main.dedServ || string.IsNullOrEmpty(name))
                return null;
            if (!_effectCache.TryGetValue(name, out Asset<Effect> asset) || asset == null) {
                string path = "AncientChineseMythology/Effects/" + name;
                if (!ModContent.HasAsset(path))   // 坏名/未编译 fx → 返回 null 与本类容错风格一致 (不抛异常)
                    return null;
                asset = ModContent.Request<Effect>(path, AssetRequestMode.ImmediateLoad);
                _effectCache[name] = asset;
            }
            return asset?.Value;
        }

        // ============================================================
        //  2) 屏幕震动 (取 max 不累加, 距离衰减, 配置缩放 — §C.2)
        // ============================================================

        /// <summary>
        /// 追加一次屏幕震动 (世界点版): 按到本地玩家的距离衰减, 走 <see cref="ACMScreenShakeSystem"/> 预算 (取 max 不累加,
        /// 经 <see cref="MythologyConfig.ShakeScale"/> 缩放, 全局 *=0.9 衰减)。幅度请遵守 §C.2 预算
        /// (小命中 ≤2 / 落地爆炸 4-6 / 相变大招 8-12 / 入场死亡 ≤16)。
        /// </summary>
        /// <param name="worldPos">震源世界坐标 (用于距离衰减)。</param>
        /// <param name="amount">峰值像素幅度 (§C.2)。</param>
        /// <param name="frames">建议持续帧 — 统一预算 (<see cref="ACMScreenShakeSystem"/>) 以全局指数衰减承载持续时间,
        /// 故此参数当前**不被消费**, 仅作调用方意图标注 / 前向兼容 (保留签名以免破坏大量调用点)。</param>
        public static void AddScreenShake(Vector2 worldPos, float amount, int frames = 0) {
            if (Main.dedServ || amount <= 0f)
                return;
            float falloff = 1f;
            Player p = Main.LocalPlayer;
            if (p != null && p.active) {
                const float maxDist = 1800f;
                float dist = Vector2.Distance(worldPos, p.Center);
                falloff = MathHelper.Clamp(1f - dist / maxDist, 0f, 1f);
            }
            ACMUtils.AddScreenShake(amount * falloff);
        }

        /// <summary>追加一次屏幕震动 (玩家版, 以玩家中心为震源, 无距离衰减)。</summary>
        public static void AddScreenShake(Player player, float amount, int frames = 0) {
            if (Main.dedServ || player == null || amount <= 0f)
                return;
            ACMUtils.AddScreenShake(amount);
        }

        // ============================================================
        //  3) 拖尾 — "外宽暗 + 内窄亮"双层 ribbon (§B.1)
        // ============================================================

        /// <summary>
        /// 双层加性带状拖尾 (外宽暗 + 内窄亮)。基于 <see cref="ACMUtils.BuildRibbonStrip"/> + DrawUserPrimitives。
        /// 须在**有活动批**的阶段调用 (如 ModProjectile.PreDraw): 本方法 End 当前批 → Additive/GameViewMatrix 绘制 → 恢复默认批。
        /// 受 <see cref="MythologyConfig.Trail"/> 降级 (Off 直接跳过, Med 降细分)。
        /// </summary>
        /// <param name="worldPoints">中心线世界坐标点列 (头→尾, 至少 2 个)。</param>
        /// <param name="baseWidth">拖尾根部半宽 (像素), 内层取其 ~0.45。</param>
        /// <param name="outerColor">外层 (宽暗) 颜色; 建议主题暗色, a 通道用作整体不透明权重。</param>
        /// <param name="innerColor">内层 (窄亮) 颜色; 建议高亮芯色。</param>
        /// <param name="tex">带纹理 (默认 <see cref="ACMAsset.SoftGlow"/>); 可换 SwordTrail/GlaciateWave。</param>
        /// <param name="uvScroll">UV 纵向滚动 (流动感)。</param>
        /// <param name="subdivisions">CatmullRom 细分 (High=入参, Med=减半)。</param>
        public static void DrawRibbonTrail(Vector2[] worldPoints, float baseWidth, Color outerColor, Color innerColor,
            Texture2D tex = null, float uvScroll = 0f, int subdivisions = 3) {
            if (Main.dedServ || worldPoints == null || worldPoints.Length < 2)
                return;
            if (MythologyConfig.Trail == TrailQualityLevel.Off)
                return;
            if (MythologyConfig.Trail == TrailQualityLevel.Med)
                subdivisions = Math.Max(1, subdivisions / 2);

            // 转屏幕空间 (配 GameViewMatrix)
            Vector2[] pts = new Vector2[worldPoints.Length];
            for (int i = 0; i < worldPoints.Length; i++)
                pts[i] = worldPoints[i] - Main.screenPosition;

            tex ??= ACMAsset.SoftGlow;
            if (tex == null)
                return;

            float fade(int channelA, float p) => (1f - p) * (channelA / 255f);

            var outerVerts = ACMUtils.BuildRibbonStrip(
                pts,
                p => MathHelper.Lerp(baseWidth, baseWidth * 0.2f, p),
                p => {
                    Color c = outerColor * fade(outerColor.A, p);
                    c.A = 0; // 加法
                    return c;
                },
                uvScroll, subdivisions);

            var innerVerts = ACMUtils.BuildRibbonStrip(
                pts,
                p => MathHelper.Lerp(baseWidth * 0.45f, baseWidth * 0.08f, p),
                p => {
                    Color c = innerColor * fade(innerColor.A, p);
                    c.A = 0;
                    return c;
                },
                uvScroll * 1.5f, subdivisions);

            if (outerVerts.Length < 4 && innerVerts.Length < 4)
                return;

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            gd.Textures[0] = tex;
            if (outerVerts.Length >= 4)
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, outerVerts, 0, outerVerts.Length - 2);
            if (innerVerts.Length >= 4)
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, innerVerts, 0, innerVerts.Length - 2);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 便捷重载: 直接从弹幕历史点 (<see cref="Projectile.oldPos"/>) 构建双层拖尾。
        /// 调用方需在 SetStaticDefaults 里设好 <see cref="Terraria.ID.ProjectileID.Sets.TrailCacheLength"/>。
        /// </summary>
        public static void DrawProjectileTrail(Projectile proj, float baseWidth, Color outerColor, Color innerColor,
            Texture2D tex = null, float uvScroll = 0f, int subdivisions = 3) {
            if (Main.dedServ || proj == null)
                return;

            var list = new List<Vector2>(proj.oldPos.Length);
            Vector2 half = proj.Size * 0.5f;
            for (int i = 0; i < proj.oldPos.Length; i++) {
                if (proj.oldPos[i] == Vector2.Zero)
                    continue;
                list.Add(proj.oldPos[i] + half);
            }
            if (list.Count < 2)
                return;
            DrawRibbonTrail(list.ToArray(), baseWidth, outerColor, innerColor, tex, uvScroll, subdivisions);
        }

        // ============================================================
        //  4) 径向辉光 / 冲击环 (§B.8)
        // ============================================================

        /// <summary>
        /// 世界点加性径向泛光 (自管全屏名额仲裁的全屏径向泛光; 内部自行 <see cref="ACMShaders.RequestFullscreenSlot"/>
        /// + 设参数 + 全屏 overlay, **不**二次转调 <see cref="ACMShaders.DrawRadialBloomAt"/> 以免重复占名额)。
        /// 名额被占/配置关闭时自动退化为廉价 <see cref="DrawGlowBurst"/> (SoftGlow 加法叠), 保证总有反馈。
        /// 须在有活动批的阶段调用。
        /// </summary>
        /// <param name="worldCenter">中心世界坐标。</param>
        /// <param name="radiusFrac">半径 (屏幕高度比例, 如 0.15)。</param>
        /// <param name="intensity">强度 0~1。</param>
        /// <param name="color">辉光色。</param>
        /// <param name="rayCount">光芒条数 (0=纯圆晕)。</param>
        public static void DrawRadialBloom(Vector2 worldCenter, float radiusFrac, float intensity, Color color,
            float rayCount = 8f) {
            if (Main.dedServ || intensity <= 0.01f)
                return;
            if (ACMShaders.RequestFullscreenSlot()) {
                Effect fx = ACMShaders.RadialBloom;
                if (fx != null) {
                    Vector2 uv = (worldCenter - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
                    fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                    fx.Parameters["uCenter"]?.SetValue(uv);
                    fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
                    fx.Parameters["uRadius"]?.SetValue(radiusFrac);
                    fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
                    fx.Parameters["uColor"]?.SetValue(color.ToVector4());
                    fx.Parameters["uRayCount"]?.SetValue(rayCount);
                    fx.Parameters["uFalloff"]?.SetValue(2.5f);

                    SpriteBatch sb = Main.spriteBatch;
                    sb.End();
                    ACMShaders.DrawFullscreenOverlay(fx, BlendState.Additive);
                    ACMShaders.RestoreDefaultBatch(sb);
                    return;
                }
            }
            // 退化: 廉价柔光 (半径比例 → 像素 scale)
            DrawGlowBurst(worldCenter, radiusFrac * Main.screenHeight / 32f, color * intensity);
        }

        /// <summary>
        /// 廉价一次性柔光闪 (§B.4 轻量版, SoftGlow 加法)。不占全屏名额, 随处可用 (有活动批阶段)。
        /// </summary>
        /// <param name="worldCenter">中心世界坐标。</param>
        /// <param name="scale">SoftGlow 贴图缩放倍率。</param>
        /// <param name="color">颜色 (内部强制 a=0 走加法)。</param>
        public static void DrawGlowBurst(Vector2 worldCenter, float scale, Color color) {
            if (Main.dedServ || scale <= 0f)
                return;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null)
                return;
            color.A = 0;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(glow, worldCenter - Main.screenPosition, null, color, 0f, glow.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 加性 TriangleStrip 双环冲击波 (参考 <c>Xuanwu.DrawShockwaveRing</c>)。落地/爆炸/能量释放/命中点通用。
        /// 须在有活动批的阶段调用。
        /// </summary>
        /// <param name="worldCenter">环心世界坐标。</param>
        /// <param name="radius">当前半径 (像素)。</param>
        /// <param name="thickness">环带半厚 (像素)。</param>
        /// <param name="alpha">整体不透明 0~1 (随时间衰减)。</param>
        /// <param name="innerColor">内沿色。</param>
        /// <param name="outerColor">外沿色。</param>
        /// <param name="tex">环纹理 (默认 <see cref="ACMAsset.GlaciateWave"/>)。</param>
        public static void DrawShockwaveRing(Vector2 worldCenter, float radius, float thickness, float alpha,
            Color innerColor, Color outerColor, Texture2D tex = null) {
            if (Main.dedServ || alpha <= 0.01f || radius < 1f)
                return;
            tex ??= ACMAsset.GlaciateWave;
            if (tex == null)
                return;

            const int segments = 48;
            float innerR = MathF.Max(radius - thickness, 0f);
            float outerR = radius + thickness;

            var verts = new ColoredVertex[segments * 2 + 2];
            Vector2 center = worldCenter - Main.screenPosition;
            Color inCol = innerColor * alpha; inCol.A = 0;
            Color outCol = outerColor * (alpha * 0.5f); outCol.A = 0;

            for (int i = 0; i <= segments; i++) {
                float angle = MathHelper.TwoPi / segments * i;
                Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle));
                float u = (float)i / segments;
                verts[i * 2] = new ColoredVertex(center + dir * outerR, new Vector3(u, 0f, 0f), outCol);
                verts[i * 2 + 1] = new ColoredVertex(center + dir * innerR, new Vector3(u, 1f, 0f), inCol);
            }

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[0] = tex;
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        // ============================================================
        //  5) 溶解/灼烧 — DissolveBurn.fx 喂武器/minion 贴图 (§B.10)
        // ============================================================

        /// <summary>
        /// 用 <c>DissolveBurn.fx</c> 对一张贴图做噪声 clip + 发光灼烧边 (喂武器/minion 贴图, **不是** screenTarget)。
        /// 用于召唤显形 / 死亡崩解 / 命中灼烧消融 / 部件生长。须在有活动批的阶段调用 (PreDraw 等)。
        /// </summary>
        /// <param name="tex">目标贴图 (s0)。</param>
        /// <param name="worldPos">绘制世界坐标。</param>
        /// <param name="srcRect">源矩形 (null=整图)。</param>
        /// <param name="color">基础着色 (sampleColor)。</param>
        /// <param name="rotation">旋转。</param>
        /// <param name="origin">原点。</param>
        /// <param name="scale">缩放。</param>
        /// <param name="threshold">溶解进度 0~1 (0=完整, 1=全消散)。</param>
        /// <param name="intensity">整体可见度 0~1 (0=透明)。</param>
        /// <param name="edgeColor">灼烧边色 (rgb=色, a=强度)。</param>
        /// <param name="edgeWidth">灼烧边宽 (0.04~0.15)。</param>
        /// <param name="noiseScale">噪声密度 (1~4)。</param>
        /// <param name="direction">方向溶解梯度 (默认 0=均匀)。</param>
        /// <param name="sweepStrength">方向梯度强度。</param>
        /// <param name="effects">翻转。</param>
        public static void ApplyDissolveBurn(Texture2D tex, Vector2 worldPos, Rectangle? srcRect, Color color,
            float rotation, Vector2 origin, float scale, float threshold, float intensity, Color edgeColor,
            float edgeWidth = 0.08f, float noiseScale = 2f, Vector2 direction = default, float sweepStrength = 0f,
            SpriteEffects effects = SpriteEffects.None) {
            if (Main.dedServ || tex == null || intensity <= 0.01f)
                return;
            Effect fx = ACMShaders.DissolveBurn;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uThreshold"]?.SetValue(MathHelper.Clamp(threshold, 0f, 1f));
            fx.Parameters["uEdgeWidth"]?.SetValue(edgeWidth);
            fx.Parameters["uNoiseScale"]?.SetValue(noiseScale);
            fx.Parameters["uEdgeColor"]?.SetValue(edgeColor.ToVector4());
            fx.Parameters["uDirection"]?.SetValue(direction);
            fx.Parameters["uSweepStrength"]?.SetValue(sweepStrength);

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            sb.Draw(tex, worldPos - Main.screenPosition, srcRect, color, rotation, origin, scale, effects, 0f);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        // ============================================================
        //  6) 调色染色 — PaletteLUT.fx 薄封装 (§C.4#2: 强度≤0.15, 占全屏名额)
        // ============================================================

        /// <summary>
        /// <c>PaletteLUT.fx</c> 薄封装: 对全屏做阴影/高光分区染色 (武器线统一配色 / 大招短暂染屏定调)。
        /// **强度自动 clamp ≤ 0.15** 且占本帧唯一全屏名额 (同屏全屏后处理 ≤ 1, §C.4#2); 名额被占则跳过。
        ///
        /// <para><b>唯一合法调用点 / 防误用:</b> 本方法读写 <see cref="Main.screenTarget"/>, 内部先
        /// <see cref="ACMShaders.RequestFullscreenSlot"/> 取得名额、再经
        /// <see cref="ACMShaders.ApplyScreenPostProcess"/> (后者带安全护栏)。请仅在**有活动批且为可做全屏后处理的绘制阶段**
        /// (大招/处决的专用绘制帧, 如 <c>*Finisher</c> / <c>DakkiBook</c> 起爆染屏 / <c>ArrogantSylvanScreenTint</c> 屏幕染色)
        /// 调用; 切勿在普通弹幕每帧 <see cref="ModProjectile.PreDraw"/> 中调用 — 既违反"短暂定调"原则,
        /// 也会被护栏拦截/产生脏帧。基础武器请改用拖尾/边色承载主题色。</para>
        /// </summary>
        /// <param name="sb">当前活动 SpriteBatch。</param>
        /// <param name="shadowTint">阴影染色 (rgb, a=权重)。</param>
        /// <param name="highlightTint">高光染色 (rgb, a=权重)。</param>
        /// <param name="intensity">整体强度 (clamp 到 [0, 0.15])。</param>
        /// <param name="saturation">饱和度 (1=不变)。</param>
        /// <param name="hueShift">色相位移 (弧度)。</param>
        public static void ApplyPaletteTint(SpriteBatch sb, Color shadowTint, Color highlightTint, float intensity,
            float saturation = 1f, float hueShift = 0f) {
            if (Main.dedServ || sb == null || intensity <= 0.005f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;
            Effect fx = ACMShaders.PaletteLUT;
            if (fx == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 0.15f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uSaturation"]?.SetValue(saturation);
            fx.Parameters["uHueShift"]?.SetValue(hueShift);
            fx.Parameters["uShadowTint"]?.SetValue(shadowTint.ToVector4());
            fx.Parameters["uHighlightTint"]?.SetValue(highlightTint.ToVector4());
            fx.Parameters["uSplit"]?.SetValue(0f);

            ACMShaders.ApplyScreenPostProcess(sb, fx, bindNoise: false);
        }
    }
}
