using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 幽冥龙专属着色器缓存与绘制助手 (V3)。
    /// 3 个专属着色器按 Xuanwu 写法用 ModContent.Request 静态缓存, 不注册进 ACMShaders:
    ///   ● NetherDragonGate — 冥界之门屏幕空间贴花 (裂缝→破开→合拢/枯萎, 由 NetherPortal 驱动)
    ///   ● NetherDragonRibbon — 龙身冥焰披风顶点条带 (沿全部体节铺设, 鞭波/暴怒/死亡熄灭波参数)
    ///   ● NetherDragonCone — 锥形/扇形危险区预警贴花 (吐息锥/魂束扫射扇共用, 紫→红收口)
    /// 顶点契约与 ACMShaders.DrawBeam 相同: 屏幕像素坐标 + GameViewMatrix;
    /// 条带须在已有活动批的阶段调用 (PreDraw/PostDraw), 内部 End→绘制→恢复默认批。
    /// </summary>
    internal static class NetherDragonVFX
    {
        private const string Path = "AncientChineseMythology/Effects/";

        private static Asset<Effect> _gate;
        private static Asset<Effect> _ribbon;
        private static Asset<Effect> _cone;

        public static Effect Gate => Get(ref _gate, "NetherDragonGate");
        public static Effect Ribbon => Get(ref _ribbon, "NetherDragonRibbon");
        public static Effect Cone => Get(ref _cone, "NetherDragonCone");

        private static Effect Get(ref Asset<Effect> slot, string name) {
            if (Main.dedServ)
                return null;
            slot ??= ModContent.Request<Effect>(Path + name, AssetRequestMode.ImmediateLoad);
            return slot?.Value;
        }

        // ===== 龙身体节链收集 (零分配复用缓冲) =====
        private static readonly Vector2[] chainSlots = new Vector2[48];
        private static readonly bool[] chainUsed = new bool[48];
        private static readonly List<Vector2> chainList = new(48);
        private static Vector2[] chainArray = System.Array.Empty<Vector2>();

        /// <summary>
        /// 收集头部 + 全部体节中心点 (含鞭波弹簧偏移), 按链序 (头→尾) 排列。
        /// 体节按 BasicWorm.SummonCount 定位。每帧仅头部绘制时调用一次;
        /// 返回内部复用列表, 勿持有引用跨帧。
        /// </summary>
        public static List<Vector2> CollectChain(NPC head) {
            chainList.Clear();
            System.Array.Clear(chainUsed, 0, chainUsed.Length);

            int maxIndex = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.realLife != head.whoAmI || npc.whoAmI == head.whoAmI)
                    continue;
                if (npc.ModNPC is not NetherDragon seg)
                    continue;
                int idx = seg.SummonCount;
                if (idx <= 0 || idx >= chainSlots.Length)
                    continue;
                chainSlots[idx] = seg.VisualCenter;
                chainUsed[idx] = true;
                if (idx > maxIndex)
                    maxIndex = idx;
            }

            chainList.Add(head.ModNPC is NetherDragon headSeg ? headSeg.VisualCenter : head.Center);
            for (int i = 1; i <= maxIndex; i++) {
                if (chainUsed[i])
                    chainList.Add(chainSlots[i]);
            }
            return chainList;
        }

        /// <summary>
        /// 沿龙身铺设冥焰披风条带 (加性)。须在活动批阶段调用。
        /// </summary>
        /// <param name="head">龙头 NPC。</param>
        /// <param name="wave">鞭波位置 0~1 (头→尾); &lt;0 = 无波。</param>
        /// <param name="enrage">暴怒泛红 0~1。</param>
        /// <param name="breakHeat">死亡熄灭波前 0~1 (尾→头推进)。</param>
        /// <param name="intensity">总强度 0~1。</param>
        public static void DrawBodyRibbon(NPC head, float wave, float enrage, float breakHeat, float intensity) {
            if (Main.dedServ || intensity <= 0.02f)
                return;
            if (MythologyConfig.Trail == TrailQualityLevel.Off)
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

            int subdiv = MythologyConfig.Trail == TrailQualityLevel.High ? 2 : 1;
            var verts = ACMUtils.BuildRibbonStrip(
                chainArray,
                p => MathHelper.Lerp(34f, 7f, p),
                p => Color.White,
                uvScroll: 0f,
                subdivisions: subdiv
            );
            if (verts.Length < 4)
                return;

            float time = (float)Main.gameTimeCache.TotalGameTime.TotalSeconds;

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
            fx.Parameters["uFlowSpeed"]?.SetValue(0.75f);
            fx.Parameters["uWave"]?.SetValue(wave);
            fx.Parameters["uEnrage"]?.SetValue(MathHelper.Clamp(enrage, 0f, 1f));
            fx.Parameters["uBreak"]?.SetValue(MathHelper.Clamp(breakHeat, 0f, 1f));
            fx.Parameters["uColorHead"]?.SetValue(new Vector4(0.43f, 0.90f, 0.59f, 0.80f)); // 鬼绿
            fx.Parameters["uColorTail"]?.SetValue(new Vector4(0.36f, 0.24f, 0.62f, 0.30f)); // 幽紫
            fx.Parameters["uColorWave"]?.SetValue(new Vector4(0.86f, 1f, 0.90f, 1f));
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }
    }
}
