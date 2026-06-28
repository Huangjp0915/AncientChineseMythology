using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.BAWImpermanences
{
    /// <summary>
    /// 黑白无常 V2 演出层 —— 共享着色器演出助手 (纯视觉, 服务端零绘制)。
    ///
    /// 作为 V2 两支签名着色器的**首发验证用例**:
    ///  1. <see cref="ACMShaders.DissolveBurn"/> 魂魄消融 —— 双使出场/复活时由"魂"溶凝为实体 (<see cref="DrawDissolveSprite"/>)。
    ///  2. <see cref="ACMShaders.PaletteLUT"/> 阴阳分屏 (yin-yang-split) —— 双半血协同《阴阳勾魂》时沿两使连线把屏幕一分为二
    ///     (左阴右阳, <see cref="DrawYinYangSplit"/>)。强度由地府身份层怨念账 <see cref="UnderworldField"/> 单点驱动, 走唯一全屏名额。
    ///
    /// 不改任何 AI / 协同 / 复活骨架 —— 仅在既有绘制点与既有状态上叠加表现。
    /// </summary>
    public static class BAWFX
    {
        // —— 阴阳配色 (与既有黑/白发光取色一致) ——
        /// <summary>阴侧 (黑无常): 幽蓝紫。</summary>
        public static readonly Color YinColor = TelegraphColors.NetherViolet;
        /// <summary>阳侧 (白无常): 暖白幽光。</summary>
        public static readonly Color YangColor = new(255, 244, 220);

        /// <summary>黑无常溶解灼烧边: 紫魂。</summary>
        public static readonly Color BlackDissolveEdge = new(150, 110, 230);
        /// <summary>白无常溶解灼烧边: 青白魂。</summary>
        public static readonly Color WhiteDissolveEdge = new(205, 222, 255);

        // ===================================================================
        //  1) 魂魄消融 DissolveBurn (Boss 贴图单 pass)
        // ===================================================================

        /// <summary>
        /// 用 DissolveBurn 着色器绘制一张 Boss 贴图 (出场/复活的"魂→实体"重凝)。
        /// <paramref name="threshold"/> 0=完整实体, 1=完全消散为魂; 出场/复活时由 1→0 推进。
        /// 着色器缺失 / 服务端时返回 false, 调用方应回退到普通 <c>sb.Draw</c>。
        /// </summary>
        public static bool DrawDissolveSprite(SpriteBatch sb, Texture2D tex, Vector2 drawPos, Rectangle? frame,
            Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects,
            float threshold, Color edgeColor) {
            if (Main.dedServ || sb == null || tex == null)
                return false;

            Effect fx = ACMShaders.DissolveBurn;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null)
                return false;

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(1f);
            fx.Parameters["uThreshold"]?.SetValue(MathHelper.Clamp(threshold, 0f, 1f));
            fx.Parameters["uEdgeWidth"]?.SetValue(0.09f);
            fx.Parameters["uNoiseScale"]?.SetValue(2.5f);
            fx.Parameters["uEdgeColor"]?.SetValue(edgeColor.ToVector4());
            fx.Parameters["uDirection"]?.SetValue(new Vector2(0f, -1f)); // 自下而上消融
            fx.Parameters["uSweepStrength"]?.SetValue(0.25f);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(tex, drawPos, frame, color, rotation, origin, scale, effects, 0f);
            sb.End();

            ACMShaders.RestoreDefaultBatch(sb);
            return true;
        }

        // ===================================================================
        //  2) 阴阳分屏 yin-yang-split (PaletteLUT 全屏后处理)
        // ===================================================================

        private static float _intensity;       // 平滑后的当前分屏强度
        private static ulong _intensityFrame;   // 强度推进的帧守卫 (双使同帧两次 PostDraw 只推进一次)

        /// <summary>
        /// 协同《阴阳勾魂》时绘制阴阳分屏。两使的任一 <c>PostDraw</c> 调用即可 —— 内部用
        /// <see cref="ACMShaders.RequestFullscreenSlot"/> 保证每帧仅一层全屏后处理, 强度按怨念账渐入/渐出。
        /// </summary>
        public static void DrawYinYangSplit(SpriteBatch sb) {
            if (Main.dedServ || sb == null || Main.gameMenu)
                return;

            NPC black = FindTwin(ModContent.NPCType<BlackImpermanence>());
            NPC white = FindTwin(ModContent.NPCType<WhiteImpermanence>());

            // 同帧只推进一次强度 (避免双使各推一次导致速度翻倍)
            if (_intensityFrame != Main.GameUpdateCount) {
                _intensityFrame = Main.GameUpdateCount;
                _intensity = MathHelper.Lerp(_intensity, TargetIntensity(black, white), 0.05f);
            }

            if (_intensity < 0.01f || black == null || white == null)
                return;

            if (!ACMShaders.RequestFullscreenSlot())
                return; // 本帧名额已被占用 / 全屏 shader 被配置关闭

            Effect fx = ACMShaders.PaletteLUT;
            if (fx == null)
                return;

            // 分屏中线沿两使连线的垂线 (左阴=黑使侧, 右阳=白使侧)
            Vector2 mid = (black.Center + white.Center) * 0.5f;
            Vector2 dir = white.Center - black.Center;
            if (dir.LengthSquared() < 1f)
                dir = Vector2.UnitX;
            dir.Normalize();

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            Vector2 midUV = (mid - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            // aspect 空间方向 ∝ 屏幕方向, 故 uSplitDir 直接用归一化屏幕方向
            float proj = midUV.X * aspect * dir.X + midUV.Y * dir.Y;
            float splitPos = proj / ((1f + aspect) * 0.5f);

            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(_intensity);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uSaturation"]?.SetValue(1f);
            fx.Parameters["uHueShift"]?.SetValue(0f);
            fx.Parameters["uShadowTint"]?.SetValue(new Vector4(YinColor.ToVector3(), 1f));
            fx.Parameters["uHighlightTint"]?.SetValue(new Vector4(YangColor.ToVector3(), 1f));
            fx.Parameters["uSplit"]?.SetValue(1f);
            fx.Parameters["uSplitDir"]?.SetValue(dir);
            fx.Parameters["uSplitPos"]?.SetValue(splitPos);

            ACMShaders.ApplyScreenPostProcess(sb, fx, bindNoise: false);
        }

        /// <summary>协同激活且双半血时的目标强度: 底 0.4, 随怨念账 (地府身份层) 上探至 0.7。</summary>
        private static float TargetIntensity(NPC black, NPC white) {
            if (black == null || white == null)
                return 0f;
            bool synergy = InSynergy(black) || InSynergy(white);
            if (!synergy)
                return 0f;

            float grudge = System.Math.Max(
                UnderworldField.GetGrudgeNormalized(black),
                UnderworldField.GetGrudgeNormalized(white));
            return MathHelper.Clamp(0.4f + grudge * 0.3f, 0f, 0.7f);
        }

        private static bool InSynergy(NPC npc) {
            if (npc == null || !npc.active)
                return false;
            if (npc.ModNPC is BlackImpermanence b)
                return b.InSynergyAttack;
            if (npc.ModNPC is WhiteImpermanence w)
                return w.InSynergyAttack;
            return false;
        }

        private static NPC FindTwin(int type) {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n != null && n.active && n.type == type)
                    return n;
            }
            return null;
        }
    }
}
