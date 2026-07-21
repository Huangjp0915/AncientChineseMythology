using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    /// <summary>
    /// 敖闰专属着色器缓存 — 遵循并行纪律: Aoyuan 前缀、自缓存、不注册进 ACMShaders。
    /// 参考 Xuanwu 写法: static Asset 惰性 ImmediateLoad, 服务器返回 null。
    /// </summary>
    public static class AoyuanShaders
    {
        private static Asset<Effect> _crystalline;
        private static Asset<Effect> _frostGround;
        private static Asset<Effect> _mirror;

        /// <summary>冰晶棱镜全屏后处理 (s0=screenTarget, s1=噪声)</summary>
        public static Effect Crystalline => Get(ref _crystalline, "AoyuanCrystalline");
        /// <summary>寒潮冻土/冻结陷阱地纹 decal (s0=噪声)</summary>
        public static Effect FrostGround => Get(ref _frostGround, "AoyuanFrostGround");
        /// <summary>西海冰镜面板 decal (s0=噪声)</summary>
        public static Effect Mirror => Get(ref _mirror, "AoyuanMirror");

        private static Effect Get(ref Asset<Effect> slot, string name) {
            if (Main.dedServ)
                return null;
            slot ??= ModContent.Request<Effect>("AncientChineseMythology/Effects/" + name, AssetRequestMode.ImmediateLoad);
            return slot?.Value;
        }
    }

    internal partial class Aoyuan
    {
        #region 头部绘制

        /// <summary>
        /// 头部绘制 - 纹理Aoyuan.png: 112×438, 3帧, 每帧112×146
        /// 层序: 突刺预警线(最底) → 速度门控残影 → 本体 → 出剑爆闪/棱光 → 绝对零度预警环
        /// </summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 隐没状态（入场未现身/入镜）完全不绘制
            if (BodyHidden)
                return false;

            // —— 突刺预警线（伤害路径 → 全局契约: 致命预警唯一用红）——
            DrawThrustTelegraph();

            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            int frameHeight = texture.Height / HeadFrameCount;
            Rectangle sourceRect = fireAttack
                ? new Rectangle(0, frameHeight * attackFrame, texture.Width, frameHeight)
                : NPC.frame;
            Vector2 origin = fireAttack
                ? new Vector2(texture.Width / 2f, frameHeight / 2f)
                : NPC.frame.Size() / 2f;

            // —— 速度门控残影: 只在突刺帧显现（速度即速度感）——
            float speed = NPC.velocity.Length();
            if (speed > 40f) {
                float ghostAlpha = MathHelper.Clamp((speed - 40f) / 55f, 0f, 1f);
                for (int i = NPC.oldPos.Length - 1; i > 0; i -= 2) {
                    if (NPC.oldPos[i] == Vector2.Zero) continue;
                    float p = 1f - i / (float)NPC.oldPos.Length;
                    Color gc = AoyuanHelper.FrostCyan * (0.38f * p * ghostAlpha);
                    gc.A = 0;
                    Vector2 gp = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                    spriteBatch.Draw(texture, gp, sourceRect, gc, NPC.rotation, origin, NPC.scale, effects, 0f);
                }
            }

            // —— 死亡晶化: 头部最后白化 ——
            Color bodyColor = drawColor;
            if (CurrentState == AoyuanState.DeathAnim && CrystallizedSegments >= 17) {
                bodyColor = Color.Lerp(drawColor, AoyuanHelper.IceCrystalWhite, 0.8f);
            }

            spriteBatch.Draw(texture, NPC.Center - screenPos, sourceRect, bodyColor,
                NPC.rotation, origin, NPC.scale, effects, 0f);

            // —— P2 棱光冰甲: 头部冷辉 ——
            if (IsPhase2 && ACMAsset.Sparkle != null && CurrentState != AoyuanState.DeathAnim) {
                float pulse = 0.5f + MathF.Sin(globalTime * 3f) * 0.25f;
                Color glint = AoyuanHelper.IceCrystalWhite * (0.30f * pulse);
                glint.A = 0;
                spriteBatch.Draw(ACMAsset.Sparkle, NPC.Center - screenPos, null, glint,
                    globalTime * 0.7f, ACMAsset.Sparkle.Size() / 2f, 0.55f, SpriteEffects.None, 0f);
            }

            // —— 出剑爆闪: SlashBurst 沿突刺方向 ——
            if (slashFlash > 0.05f && ACMAsset.SlashBurst != null) {
                Texture2D burst = ACMAsset.SlashBurst;
                Color bc = AoyuanHelper.IceCrystalWhite * (slashFlash * 0.85f);
                bc.A = 0;
                float rot = internalAI[1] + MathHelper.PiOver2;
                spriteBatch.Draw(burst, NPC.Center - screenPos, null, bc, rot,
                    new Vector2(burst.Width / 2f, burst.Height * 0.85f),
                    new Vector2(0.45f, 0.9f) * (0.6f + (1f - slashFlash) * 0.7f), SpriteEffects.None, 0f);
            }

            // —— 绝对零度: 致命预警环（生长 charge³ → 预塌缩收缩）——
            DrawAZTelegraphRing(spriteBatch, screenPos);

            return false;
        }

        /// <summary>
        /// 突刺预警线: 蓄势期 Frost 渐亮跟踪 → 锁定帧转白定格。
        /// 走共享 BeamGrad 顶点条带（活动批内安全, DrawBeam 自恢复默认批）。
        /// </summary>
        private void DrawThrustTelegraph() {
            if (telegraphAlpha <= 0.03f)
                return;

            Vector2 dir = internalAI[1].ToRotationVector2();
            Vector2 start = NPC.Center;
            Vector2 end = start + dir * 1600f;

            // 锁定后转白且更亮 — 危险语义: 伤害路径用 Lethal 红芯 + 冰蓝缘
            Color core = telegraphLock > 0.05f
                ? Color.Lerp(TelegraphColors.Lethal, TelegraphColors.IceWhite, telegraphLock * 0.6f)
                : TelegraphColors.Lethal;
            Color edge = TelegraphColors.Frost;

            float width = 7f + telegraphLock * 6f;
            float intensity = telegraphAlpha * (0.55f + telegraphLock * 0.45f);
            ACMShaders.DrawBeam(start, end, width, core with { A = 140 }, edge with { A = 0 }, intensity,
                flowSpeed: 2.2f, flowScale: 1.6f, coreSharp: 2.6f);
        }

        /// <summary>绝对零度预警环: 半径 ∝ charge³ 生长, 预塌缩期收缩至 40%（收束即将爆发）</summary>
        private void DrawAZTelegraphRing(SpriteBatch sb, Vector2 screenPos) {
            float charge = AZChargeProgress;
            if (charge <= 0.01f || WeakPointsExposed == false && !AZCollapsing)
                return;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null)
                return;

            float radius = MathHelper.Lerp(140f, 640f, charge * charge * charge);
            if (AZCollapsing) {
                float ct = MathHelper.Clamp((StateTimer - AZChargeEnd) / 12f, 0f, 1f);
                radius *= MathHelper.Lerp(1f, 0.4f, AoyuanHelper.PolyOut(ct, 4));
            }

            Vector2 origin = glow.Size() / 2f;
            int dots = 44;
            Color c = TelegraphColors.Lethal * (0.28f + charge * 0.45f);
            c.A = 0;
            for (int i = 0; i < dots; i++) {
                float ang = MathHelper.TwoPi * i / dots + globalTime * 0.4f;
                Vector2 pos = NPC.Center + ang.ToRotationVector2() * radius - screenPos;
                sb.Draw(glow, pos, null, c, 0f, origin, 0.38f, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 全屏后处理 — AoyuanCrystalline 冰晶棱镜

        /// <summary>
        /// 签名时刻的棱镜后处理（绝对零度/破境/死亡/出剑脉冲）。喂 Main.screenTarget 的昂贵操作,
        /// 严格走 <see cref="ACMShaders.RequestFullscreenSlot"/> 单名额契约; 全部标量 <0.01 直接早退。
        /// 氛围底色/冻爆泛光/法阵地纹由 <see cref="AoyuanFrostScreenSystem"/> 单独承担。
        /// </summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ)
                return;
            if (crystalFx <= 0.01f && stillFx <= 0.01f && flashFx <= 0.01f && frostEdge <= 0.01f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = AoyuanShaders.Crystalline;
            if (fx == null)
                return;

            Vector2 centerUV = (NPC.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            fx.Parameters["uTime"]?.SetValue(globalTime);
            fx.Parameters["uCenter"]?.SetValue(centerUV);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(crystalFx, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uStill"]?.SetValue(MathHelper.Clamp(stillFx, 0f, 1f));
            fx.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flashFx, 0f, 1f));
            fx.Parameters["uFrost"]?.SetValue(MathHelper.Clamp(frostEdge, 0f, 1f));

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        #endregion
    }
}
