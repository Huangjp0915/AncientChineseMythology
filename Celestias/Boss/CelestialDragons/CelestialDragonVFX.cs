using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.CelestialDragons
{
    /// <summary>
    /// 天御金龙专属着色器缓存与绘制助手 (V3)。
    /// 4 个专属着色器按 Xuanwu 写法用 ModContent.Request 静态缓存, 不注册进 ACMShaders:
    ///   ● CelestialDragonRibbon — 龙身金辉流光顶点条带 (沿全部体节铺设, 充能波/死亡白热参数)
    ///   ● CelestialDragonCloudSea — 云海层屏幕空间 decal (由 CelestialScreenSystem 绘制)
    ///   ● CelestialDragonPearl — 龙珠程序化球体 (蓄光/白热/塌缩)
    ///   ● CelestialDragonPillar — 天光柱垂直光柱 (预警细线→轰落全宽)
    /// 顶点契约与 ACMShaders.DrawBeam 相同: 屏幕像素坐标 + GameViewMatrix;
    /// 条带/龙珠/光柱须在已有活动批的阶段调用 (PreDraw/PostDraw), 内部 End→绘制→恢复默认批。
    /// </summary>
    internal static class CelestialDragonVFX
    {
        private const string Path = "AncientChineseMythology/Effects/";

        private static Asset<Effect> _ribbon;
        private static Asset<Effect> _cloudSea;
        private static Asset<Effect> _pearl;
        private static Asset<Effect> _pillar;

        public static Effect Ribbon => Get(ref _ribbon, "CelestialDragonRibbon");
        public static Effect CloudSea => Get(ref _cloudSea, "CelestialDragonCloudSea");
        public static Effect Pearl => Get(ref _pearl, "CelestialDragonPearl");
        public static Effect Pillar => Get(ref _pillar, "CelestialDragonPillar");

        private static Effect Get(ref Asset<Effect> slot, string name) {
            if (Main.dedServ)
                return null;
            slot ??= ModContent.Request<Effect>(Path + name, AssetRequestMode.ImmediateLoad);
            return slot?.Value;
        }

        // ===== 龙身体节链收集 (零分配复用缓冲) =====
        private static readonly Vector2[] chainSlots = new Vector2[64];
        private static readonly bool[] chainUsed = new bool[64];
        private static readonly List<Vector2> chainList = new(64);

        /// <summary>
        /// 收集头部 + 全部体节的中心点, 按链序 (头→尾) 排列。体节按 <see cref="CelestialDragons.SegmentIndex"/>
        /// 定位 (沿同步的 FatherWorm 链推导, 多人客户端亦正确);
        /// 每帧仅头部绘制时调用一次; 返回内部复用列表, 勿持有引用跨帧。
        /// </summary>
        public static List<Vector2> CollectChain(NPC head) {
            chainList.Clear();
            System.Array.Clear(chainUsed, 0, chainUsed.Length);

            int maxIndex = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.realLife != head.whoAmI || npc.whoAmI == head.whoAmI)
                    continue;
                if (npc.ModNPC is not CelestialDragons seg)
                    continue;
                int idx = seg.SegmentIndex;
                if (idx <= 0 || idx >= chainSlots.Length)
                    continue;
                chainSlots[idx] = npc.Center;
                chainUsed[idx] = true;
                if (idx > maxIndex)
                    maxIndex = idx;
            }

            chainList.Add(head.Center);
            for (int i = 1; i <= maxIndex; i++) {
                if (chainUsed[i])
                    chainList.Add(chainSlots[i]);
            }
            return chainList;
        }

        // 条带中心线缓冲 (屏幕空间); 节数稳定后长度不变 → 稳态零分配
        private static Vector2[] chainArray = System.Array.Empty<Vector2>();

        /// <summary>
        /// 沿龙身铺设金辉流光条带 (双层: 宽金辉 + 窄白芯)。须在活动批阶段调用。
        /// </summary>
        /// <param name="head">龙头 NPC。</param>
        /// <param name="chargeWave">充能波位置 0~1 (头→尾); &lt;0 = 无波。</param>
        /// <param name="breakHeat">死亡白热化 0~1。</param>
        /// <param name="intensity">总强度 0~1。</param>
        public static void DrawBodyRibbon(NPC head, float chargeWave, float breakHeat, float intensity) {
            if (Main.dedServ || intensity <= 0.02f)
                return;
            Effect fx = Ribbon;
            if (fx == null)
                return;

            List<Vector2> chain = CollectChain(head);
            if (chain.Count < 3)
                return;

            if (chainArray.Length != chain.Count)
                chainArray = new Vector2[chain.Count];
            for (int i = 0; i < chain.Count; i++)
                chainArray[i] = chain[i] - Main.screenPosition;

            float time = (float)Main.gameTimeCache.TotalGameTime.TotalSeconds;

            var verts = ACMUtils.BuildRibbonStrip(
                chainArray,
                p => MathHelper.Lerp(46f, 8f, p * p),
                p => Color.White,
                uvScroll: 0f,
                subdivisions: 2
            );
            if (verts.Length < 4)
                return;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Texture2D noise = ACMShaders.NoiseTexture;
            gd.Textures[0] = noise;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            fx.Parameters["uTime"]?.SetValue(time);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uFlowSpeed"]?.SetValue(0.9f);
            fx.Parameters["uChargeWave"]?.SetValue(chargeWave);
            fx.Parameters["uChargeWidth"]?.SetValue(0.12f);
            fx.Parameters["uBreak"]?.SetValue(MathHelper.Clamp(breakHeat, 0f, 1f));
            fx.Parameters["uColorCore"]?.SetValue(new Vector4(1f, 0.86f, 0.45f, 0.85f));
            fx.Parameters["uColorEdge"]?.SetValue(new Vector4(0.85f, 0.5f, 0.12f, 0.35f));
            fx.Parameters["uColorCharge"]?.SetValue(new Vector4(1f, 0.98f, 0.88f, 1f));
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 绘制龙珠 (程序化球体)。须在活动批阶段调用。
        /// </summary>
        /// <param name="worldCenter">珠心世界坐标。</param>
        /// <param name="radiusPx">半径 (世界像素)。</param>
        /// <param name="charge">充能 0~1。</param>
        /// <param name="intensity">强度 0~1 (含塌缩颤闪)。</param>
        public static void DrawPearl(Vector2 worldCenter, float radiusPx, float charge, float intensity) {
            if (Main.dedServ || intensity <= 0.02f || radiusPx < 2f)
                return;
            Effect fx = Pearl;
            Texture2D quad = ACMAsset.SoftGlow;
            if (fx == null || quad == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.gameTimeCache.TotalGameTime.TotalSeconds);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uCharge"]?.SetValue(MathHelper.Clamp(charge, 0f, 1f));
            fx.Parameters["uColorCore"]?.SetValue(new Vector4(1f, 0.95f, 0.72f, 1f));
            fx.Parameters["uColorRim"]?.SetValue(new Vector4(1f, 0.72f, 0.28f, 0.8f));

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = ACMShaders.NoiseTexture;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            float scale = radiusPx * 2f / quad.Width;
            sb.Draw(quad, worldCenter - Main.screenPosition, null, Color.White, 0f,
                quad.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        /// <summary>
        /// 绘制天光柱 (自底部锚点向上 heightPx)。须在活动批阶段调用。
        /// </summary>
        /// <param name="worldBottom">柱底世界坐标 (落点)。</param>
        /// <param name="heightPx">柱高 (世界像素)。</param>
        /// <param name="quadWidthPx">绘制带总宽 (世界像素, 含辉光边)。</param>
        /// <param name="grow">推进 0~1 (光柱自天顶向下轰落)。</param>
        /// <param name="coreFrac">核心宽度占比 0~1 (预警细线≈0.05, 轰落≈0.45)。</param>
        /// <param name="intensity">强度 0~1。</param>
        /// <param name="core">柱心色。</param>
        /// <param name="edge">边缘色。</param>
        public static void DrawPillar(Vector2 worldBottom, float heightPx, float quadWidthPx,
            float grow, float coreFrac, float intensity, Color core, Color edge) {
            if (Main.dedServ || intensity <= 0.02f || heightPx < 8f)
                return;
            Effect fx = Pillar;
            Texture2D quad = ACMAsset.SoftGlow;
            if (fx == null || quad == null)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.gameTimeCache.TotalGameTime.TotalSeconds);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uGrow"]?.SetValue(MathHelper.Clamp(grow, 0f, 1f));
            fx.Parameters["uWidth"]?.SetValue(MathHelper.Clamp(coreFrac, 0.01f, 1f));
            fx.Parameters["uFlowSpeed"]?.SetValue(2.2f);
            fx.Parameters["uColorCore"]?.SetValue(core.ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(edge.ToVector4());

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = ACMShaders.NoiseTexture;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            Vector2 scale = new(quadWidthPx / quad.Width, heightPx / quad.Height);
            // 原点取纹理底部中心 → 柱体自 worldBottom 向上延伸
            sb.Draw(quad, worldBottom - Main.screenPosition, null, Color.White, 0f,
                new Vector2(quad.Width / 2f, quad.Height), scale, SpriteEffects.None, 0f);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }
    }
}
