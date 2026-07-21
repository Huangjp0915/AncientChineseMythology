using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.AzureDragons
{
    /// <summary>
    /// 苍龙真身·头部 — 统一绘制层。
    /// 全龙 (头+体节+尾) 由头部一次绘制: 龙身流光条带(1 批) → 体节贴图(主批, 带蛇形/鞭波视觉偏移)
    /// → 全部辉光与残影(1 个加性批) → 预警线(DrawBeam)。
    /// 体节自身 PreDraw 跳过 — 每帧批次重启从 ~160 次降到个位数。
    /// </summary>
    public partial class AzureDragonHead
    {
        // 专属条带着色器 (自缓存, 不注册进 ACMShaders)
        private static Asset<Effect> ribbonRef;

        private static Effect RibbonEffect {
            get {
                if (Main.dedServ)
                    return null;
                ribbonRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/AzureDragonRibbon", AssetRequestMode.ImmediateLoad);
                return ribbonRef?.Value;
            }
        }

        private readonly List<NPC> chainCache = [];

        private static readonly Comparison<NPC> ChainSort =
            (a, b) => ((BasicWorm)a.ModNPC).SummonCount.CompareTo(((BasicWorm)b.ModNPC).SummonCount);

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (NPC.IsABestiaryIconDummy) {
                Texture2D icon = TextureAssets.Npc[Type].Value;
                spriteBatch.Draw(icon, NPC.Center - screenPos, null, drawColor, 0f,
                    icon.Size() / 2f, 0.7f, SpriteEffects.None, 0f);
                return false;
            }

            BuildChainCache();

            if (VisualFade > 0.04f) {
                DrawBodyRibbon(spriteBatch, screenPos);
                DrawSegments(spriteBatch, screenPos);
                DrawGlowLayer(spriteBatch, screenPos);
            }
            DrawTelegraphs(spriteBatch, screenPos);

            if (impactFlash > 0.05f)
                ACMShaders.DrawRadialBloomAt(NPC.Center, 0.16f * impactFlash, impactFlash,
                    DragonLightning, rayCount: 0f, falloff: 2.6f);

            return false;
        }

        private void BuildChainCache() {
            chainCache.Clear();
            int bodyType = ModContent.NPCType<AzureDragonBody>();
            int tailType = ModContent.NPCType<AzureDragonTail>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && (n.type == bodyType || n.type == tailType) && n.realLife == NPC.whoAmI)
                    chainCache.Add(n);
            }
            chainCache.Sort(ChainSort);
        }

        #region 龙身流光条带

        private void DrawBodyRibbon(SpriteBatch sb, Vector2 screenPos) {
            if (chainCache.Count < 3)
                return;
            Effect fx = RibbonEffect;
            if (fx == null)
                return;

            // 中心线: 头 + 全部体节 (含视觉偏移, 与贴图一致)
            int count = chainCache.Count + 1;
            Vector2[] pts = new Vector2[count];
            pts[0] = NPC.Center + SegmentVisualOffset(NPC, 0) - screenPos;
            for (int i = 1; i < count; i++) {
                NPC seg = chainCache[i - 1];
                int idx = ((BasicWorm)seg.ModNPC).SummonCount;
                pts[i] = seg.Center + SegmentVisualOffset(seg, idx) - screenPos;
            }

            float fade = VisualFade;
            float glowLevel = 0.45f + 0.4f * auraIntensity + 0.5f * strikeBoost;

            // 共享 uniform
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uFlowSpeed"]?.SetValue(0.55f + strikeBoost * 0.8f);
            fx.Parameters["uScaleFreq"]?.SetValue(5.5f);
            fx.Parameters["uChargePos"]?.SetValue(chargeSweep);
            fx.Parameters["uChargeGlow"]?.SetValue(chargeGlow * (0.7f + 0.5f * MathF.Sin(globalTime * 14f)));
            fx.Parameters["uStrikeBoost"]?.SetValue(strikeBoost);

            SpriteBatch batch = Main.spriteBatch;
            batch.End();
            batch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            Texture2D noise = ACMShaders.NoiseTexture;
            gd.Textures[0] = noise;
            gd.Textures[1] = noise;
            gd.SamplerStates[0] = SamplerState.LinearWrap;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            // 外层: 宽幅青蓝流光
            float baseWidth = 30f * VisualScale;
            var outer = ACMUtils.BuildRibbonStrip(
                pts,
                p => MathHelper.Lerp(baseWidth, 9f, p),
                p => {
                    Color c = Color.Lerp(DragonLightning, DragonDeep, p * 0.85f);
                    c *= (1f - p * 0.5f) * glowLevel * fade;
                    c.A = 0;
                    return c;
                },
                uvScroll: 0f, subdivisions: 2);
            if (outer.Length >= 4) {
                fx.Parameters["uIntensity"]?.SetValue(fade);
                fx.Parameters["uColorCore"]?.SetValue(DragonCyan.ToVector4());
                fx.Parameters["uColorEdge"]?.SetValue(new Vector4(DragonDeep.ToVector3(), 0.35f));
                fx.Parameters["uCoreSharp"]?.SetValue(2.0f);
                fx.CurrentTechnique.Passes[0].Apply();
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, outer, 0, outer.Length - 2);
            }

            // 芯层: 窄幅白热电芯
            var inner = ACMUtils.BuildRibbonStrip(
                pts,
                p => MathHelper.Lerp(baseWidth * 0.38f, 3f, p),
                p => {
                    Color c = Color.Lerp(Color.White, DragonCyan, p);
                    c *= (1f - p * 0.7f) * (0.5f + 0.6f * strikeBoost + 0.5f * chargeGlow) * fade;
                    c.A = 0;
                    return c;
                },
                uvScroll: 0f, subdivisions: 2);
            if (inner.Length >= 4) {
                fx.Parameters["uIntensity"]?.SetValue(fade);
                fx.Parameters["uColorCore"]?.SetValue(Vector4.One);
                fx.Parameters["uColorEdge"]?.SetValue(new Vector4(DragonCyan.ToVector3(), 0.5f));
                fx.Parameters["uCoreSharp"]?.SetValue(3.2f);
                fx.CurrentTechnique.Passes[0].Apply();
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, inner, 0, inner.Length - 2);
            }

            batch.End();
            RestoreMainBatch(batch);
        }

        #endregion

        #region 体节贴图 (主批)

        private void DrawSegments(SpriteBatch sb, Vector2 screenPos) {
            // 入场云层深处的剪影化: 越远越暗越蓝
            float silhouette = MathHelper.Clamp((1f - VisualScale) * 1.3f, 0f, 0.85f);
            Color silhouetteColor = new(25, 50, 90);

            // 尾 → 头绘制, 头压在最上层
            for (int i = chainCache.Count - 1; i >= 0; i--) {
                NPC seg = chainCache[i];
                var worm = (AzureDragon)seg.ModNPC;
                DrawOneSegment(sb, screenPos, seg, TextureAssets.Npc[seg.type].Value,
                    worm.VisualFlip, worm.SummonCount, silhouette, silhouetteColor);
            }
            DrawOneSegment(sb, screenPos, NPC, TextureAssets.Npc[Type].Value,
                VisualFlip, 0, silhouette, silhouetteColor);
        }

        private void DrawOneSegment(SpriteBatch sb, Vector2 screenPos, NPC seg, Texture2D tex,
            bool flip, int summonIndex, float silhouette, Color silhouetteColor) {
            Vector2 pos = seg.Center + SegmentVisualOffset(seg, summonIndex) - screenPos;
            Color light = Lighting.GetColor((int)(seg.Center.X / 16f), (int)(seg.Center.Y / 16f));
            Color col = Color.Lerp(light, silhouetteColor, silhouette) * VisualFade;
            SpriteEffects effects = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;

            sb.Draw(tex, pos, null, col, seg.rotation, tex.Size() / 2f, VisualScale, effects, 0f);
        }

        #endregion

        #region 辉光层 (单个加性批: 残影 + 体节辉光 + 头部光效)

        private void DrawGlowLayer(SpriteBatch sb, Vector2 screenPos) {
            Texture2D glowTex = ACMAsset.SoftGlow;
            if (glowTex == null)
                return;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            float fade = VisualFade;
            Vector2 glowOrigin = glowTex.Size() / 2f;

            // —— 冲刺残影 (速度门控: 只在高速时可见, 常态无噪声) ——
            if (dashGlow > 0.25f) {
                Texture2D headTex = TextureAssets.Npc[Type].Value;
                SpriteEffects fxHead = VisualFlip ? SpriteEffects.FlipVertically : SpriteEffects.None;
                for (int i = 0; i < NPC.oldPos.Length; i += 2) {
                    if (NPC.oldPos[i] == Vector2.Zero)
                        continue;
                    float t = i / (float)NPC.oldPos.Length;
                    Color ghost = Color.Lerp(DragonLightning, DragonDeep, t) * (0.4f * dashGlow * (1f - t) * fade);
                    ghost.A = 0;
                    sb.Draw(headTex, NPC.oldPos[i] + NPC.Size / 2f - screenPos, null, ghost,
                        NPC.oldRot[i], headTex.Size() / 2f, VisualScale * (1f - t * 0.15f), fxHead, 0f);
                }
            }

            // —— 体节呼吸辉光 + 放电预警白闪 ——
            for (int i = 0; i < chainCache.Count; i++) {
                NPC seg = chainCache[i];
                int idx = ((BasicWorm)seg.ModNPC).SummonCount;
                Vector2 pos = seg.Center + SegmentVisualOffset(seg, idx) - screenPos;

                float pulse = 0.6f + 0.4f * MathF.Sin(undulationPhase * 0.6f - idx * 0.3f);
                Color glow = DragonCyan * (0.18f * pulse * (0.6f + 0.6f * auraIntensity) * fade);
                glow.A = 0;
                sb.Draw(glowTex, pos, null, glow, 0f, glowOrigin, (1.1f + 0.25f * pulse) * VisualScale, SpriteEffects.None, 0f);

                // 即将放电的体节: 白闪渐强 (预警三要素之颜色+时间)
                if (dischargeWarnOffset >= 0 && idx % 8 == dischargeWarnOffset && dischargeWarn01 > 0.02f) {
                    float flick = 0.6f + 0.4f * MathF.Sin(globalTime * 26f + idx);
                    Color warn = Color.White * (0.7f * dischargeWarn01 * flick * fade);
                    warn.A = 0;
                    sb.Draw(glowTex, pos, null, warn, 0f, glowOrigin, 0.9f * VisualScale, SpriteEffects.None, 0f);
                }
            }

            // —— 头部主光晕 + 白芯 ——
            Vector2 headPos = NPC.Center + SegmentVisualOffset(NPC, 0) - screenPos;
            float headPulse = 0.7f + 0.3f * MathF.Sin(globalTime * 4f);
            Color headGlow = DragonCyan * (0.5f * headPulse * auraIntensity * fade);
            headGlow.A = 0;
            sb.Draw(glowTex, headPos, null, headGlow, 0f, glowOrigin, (2f + 0.5f * auraIntensity) * VisualScale, SpriteEffects.None, 0f);
            Color headCore = Color.White * (0.3f * headPulse * auraIntensity * fade);
            headCore.A = 0;
            sb.Draw(glowTex, headPos, null, headCore, 0f, glowOrigin, (1f + 0.25f * auraIntensity) * VisualScale, SpriteEffects.None, 0f);

            // —— 高压阶段的星芒 ——
            if (ACMAsset.BlankStar != null && auraIntensity > 0.8f) {
                Color starColor = DragonLightning * (0.3f * headPulse * fade);
                starColor.A = 0;
                sb.Draw(ACMAsset.BlankStar, headPos, null, starColor, globalTime * 1.5f,
                    ACMAsset.BlankStar.Size() / 2f, 1.6f * auraIntensity * VisualScale, SpriteEffects.None, 0f);
            }

            // —— 头部环绕电弧 ——
            if (ACMAsset.ElectricArcSheet != null && auraIntensity > 0.5f) {
                Texture2D arcTex = ACMAsset.ElectricArcSheet;
                int arcHeight = arcTex.Height / 4;
                Rectangle src = new(0, ((int)(globalTime * 8f)) % 4 * arcHeight, arcTex.Width, arcHeight);
                float arcAlpha = 0.35f * auraIntensity * (0.6f + 0.4f * MathF.Sin(globalTime * 6f)) * fade;
                Color arcColor = DragonCyan * arcAlpha;
                arcColor.A = 0;
                Vector2 arcOrigin = new(src.Width / 2f, src.Height / 2f);
                float arcRot = NPC.rotation + MathHelper.PiOver2;
                sb.Draw(arcTex, headPos, src, arcColor, arcRot, arcOrigin, 0.25f * VisualScale, SpriteEffects.None, 0f);
                sb.Draw(arcTex, headPos, src, arcColor * 0.6f, arcRot + MathHelper.Pi, arcOrigin, 0.2f * VisualScale, SpriteEffects.FlipHorizontally, 0f);
            }

            sb.End();
            RestoreMainBatch(sb);
        }

        #endregion

        #region 预警线 (DrawBeam 系)

        private void DrawTelegraphs(SpriteBatch sb, Vector2 screenPos) {
            // —— 穿刺瞄准线: 青 → 末 10f 转致命红 ——
            if ((State is AIState.P1_CoilPierce or AIState.P2_ChainPierce) && (int)SubState == 1) {
                int lockTime = State == AIState.P2_ChainPierce ? 18 : 26;
                if (AttackTimer >= 4) {
                    float p = MathHelper.Clamp(AttackTimer / lockTime, 0f, 1f);
                    float lethal = MathHelper.Clamp((AttackTimer - (lockTime - 10)) / 10f, 0f, 1f);
                    Color warn = Color.Lerp(DragonCyan, TelegraphColors.Lethal, lethal);
                    float width = MathHelper.Lerp(3f, 7f, p);
                    ACMShaders.DrawBeam(
                        NPC.Center + chargeDirection * 70f,
                        NPC.Center + chargeDirection * 1700f,
                        width, warn * 0.85f, warn * 0.25f, 0.3f + 0.55f * p,
                        flowSpeed: 3f, flowScale: 3f, coreSharp: 2.4f);
                }
            }

            // —— 龙息扇形边界 ——
            if (State == AIState.P1_BreathSweep && (int)SubState == 1 && AttackTimer > 4) {
                float p = MathHelper.Clamp(AttackTimer / 32f, 0f, 1f);
                float baseAng = chargeDirection.ToRotation();
                Color warn = DragonCyan * 0.7f;
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 dir = (baseAng + s * 0.7f).ToRotationVector2();
                    ACMShaders.DrawBeam(NPC.Center + dir * 60f, NPC.Center + dir * 1150f,
                        3.5f, warn, warn * 0.3f, 0.35f * p,
                        flowSpeed: 2f, flowScale: 3f, coreSharp: 2f);
                }
            }

            // —— 天眼锁定: 扫描细线 → 锁死转红 ——
            if (State == AIState.P3_SkyDive && (int)SubState == 1) {
                bool locked = AttackTimer >= 46;
                if (locked) {
                    float flick = 0.7f + 0.3f * MathF.Sin(globalTime * 22f);
                    Color red = TelegraphColors.Lethal;
                    ACMShaders.DrawBeam(chargeTarget - chargeDirection * 1600f, chargeTarget + chargeDirection * 450f,
                        7f * flick, red * 0.9f, red * 0.3f, 0.8f,
                        flowSpeed: 4f, flowScale: 2.5f, coreSharp: 2.4f);
                }
                else if (NPC.target >= 0 && NPC.target < Main.maxPlayers) {
                    Player t = Main.player[NPC.target];
                    Vector2 top = new(clientScanX, t.Center.Y - 1500f);
                    Vector2 bottom = new(clientScanX, t.Center.Y + 550f);
                    float p = AttackTimer / 46f;
                    ACMShaders.DrawBeam(top, bottom, 3.5f, DragonCyan * 0.8f, DragonDeep * 0.3f, 0.25f + 0.35f * p,
                        flowSpeed: 2.5f, flowScale: 4f, coreSharp: 2.2f);
                }
            }
        }

        #endregion

        /// <summary>恢复本项目 NPC 绘制惯例批 (AnisotropicClamp + GameViewMatrix)。</summary>
        private static void RestoreMainBatch(SpriteBatch sb) {
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
