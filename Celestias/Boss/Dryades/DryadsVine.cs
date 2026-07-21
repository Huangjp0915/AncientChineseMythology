using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Dryades
{
    /// <summary>
    /// 树精藤蔓/根须多模态弹幕 (V3) — 全程序化条带渲染 (DryadsVineRibbon.fx), 无贴图本体。
    ///
    ///  ai[0] 模式:
    ///   0 = 根须飞梭: 重力弧线投射物, 撞地即灭 (冒出放射用)。
    ///   1 = 根须柱: 30f 光束预告 (绿→末段赤红) → 8f poly(6) 破土速升 → 40f 持续摆动 → 16f 凋落。
    ///       判定 = 根基→顶端线段, 仅竖立期间 (伤害窗与视觉严格对齐)。
    ///   2 = 藤鞭: 分段弹簧角度链鞭物理 (16 节)。破土冒芽 12f → 背挥蓄势 40f (反向高扬,
    ///       末 27f 红导引线) → 抽击 9f (poly(10) ease-out 甩过 ~2.5rad) → 定格 8f →
    ///       余摆回收 42f (弹簧自然震荡) → 缩地 16f。判定仅抽击+定格 17f 且仅鞭梢 8 节。
    ///   3 = 冠层放射鞭 (万藤缠狱): 同 2 的波形, 但根点悬于树冠、可朝任意角放射,
    ///       挥击方向恒为顺时针 (全阵鞭子同向 → "跟着钟摆走"的单一规则)。
    ///
    ///  ai[1] = 模式内计时; ai[2] = (柱)高度 / (鞭)攻击方向角。
    ///  弹簧链为确定性演化 (无随机参与模拟), 各端一致 → 判定可靠。
    /// </summary>
    public class DryadsVine : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        // ===== 模式 =====
        private const int ModeShuttle = 0;
        private const int ModePillar = 1;
        private const int ModeWhip = 2;
        private const int ModeCrownWhip = 3;

        private int Mode => (int)Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        // ===== 根须柱时序 =====
        private const int PillarTelegraph = 30;
        private const int PillarRise = 8;
        private const int PillarHold = 40;
        private const int PillarWither = 16;
        private const int PillarTotal = PillarTelegraph + PillarRise + PillarHold + PillarWither;

        // ===== 藤鞭时序 (§4.3 anticipation/burst/recovery 波形) =====
        private const int WhipSprout = 12;                       // 破土冒芽
        private const int WhipWindupEnd = WhipSprout + 40;       // 背挥蓄势 → 52
        private const int WhipStrikeEnd = WhipWindupEnd + 9;     // 抽击 → 61
        private const int WhipHoldEnd = WhipStrikeEnd + 8;       // 定格 → 69
        private const int WhipRecoverEnd = WhipHoldEnd + 42;     // 余摆 → 111
        private const int WhipTotal = WhipRecoverEnd + 16;       // 缩地 → 127
        private const int WhipGuideStart = WhipWindupEnd - 27;   // 红导引线亮起

        // ===== 藤鞭弹簧角度链 =====
        private const int SegCount = 16;
        private const float SegLen = 34f;
        private float[] segAngle;
        private float[] segAngVel;
        private readonly Vector2[] chainPos = new Vector2[SegCount + 1];
        private float whipGrowth;      // 出土长度 0~1
        private bool strikeCracked;    // 抽击音效只放一次

        // ===== 着色器 (静态缓存一次, Xuanwu 模式) =====
        private static Asset<Effect> ribbonRef;
        private static Effect RibbonEffect {
            get {
                if (Main.dedServ) return null;
                ribbonRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/DryadsVineRibbon", AssetRequestMode.ImmediateLoad);
                return ribbonRef?.Value;
            }
        }

        // 主题色
        private static readonly Color BarkDark = new(46, 66, 26, 235);
        private static readonly Color CoreGreen = new(96, 190, 66, 245);
        private static readonly Color SapGlow = new(180, 255, 120);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override void AI() {
            switch (Mode) {
                case ModePillar: PillarAI(); break;
                case ModeWhip:
                case ModeCrownWhip: WhipAI(); break;
                default: ShuttleAI(); break;
            }
        }

        #region 模式 0 — 根须飞梭 (重力弧)

        private void ShuttleAI() {
            Projectile.tileCollide = Timer > 12f; // 出土瞬间不撞自身地形
            Timer++;
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, 200);

            // 轻重力弧线 (对比直线弹: 可预判的抛物挤位)
            if (Projectile.velocity.Y < 14f)
                Projectile.velocity.Y += 0.12f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.JungleGrass,
                    -Projectile.velocity.X * 0.05f, -Projectile.velocity.Y * 0.05f, 100, default, 1f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.08f, 0.18f, 0.04f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Mode == ModeShuttle) {
                Projectile.Kill();
                return false;
            }
            return false;
        }

        #endregion

        #region 模式 1 — 根须柱 (预告 → 速升 → 凋落)

        private float PillarHeight => Projectile.ai[2] > 0f ? Projectile.ai[2] : 280f;

        /// <summary>竖起进度 0~1 (poly(6) ease-out: 8f 内几乎全部行程 → 一记"破土")。</summary>
        private float PillarRiseEase {
            get {
                float t = MathHelper.Clamp((Timer - PillarTelegraph) / PillarRise, 0f, 1f);
                return 1f - MathF.Pow(1f - t, 6f);
            }
        }

        /// <summary>凋落进度 0~1。</summary>
        private float PillarWitherFrac =>
            MathHelper.Clamp((Timer - (PillarTotal - PillarWither)) / PillarWither, 0f, 1f);

        private void PillarAI() {
            Projectile.velocity = Vector2.Zero;
            Timer++;
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, PillarTotal - (int)Timer + 4);

            int t = (int)Timer;
            if (t < PillarTelegraph) {
                // 预告期: 根点少量绿尘上冒
                if (Main.netMode != NetmodeID.Server && t % 4 == 0) {
                    Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.GreenTorch,
                        Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-1.5f, -0.3f), 80, default, 1.1f);
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.06f, 0.16f, 0.03f);
                return;
            }

            if (t == PillarTelegraph) {
                // 破土帧: 音效 + 土屑喷溅 (§5 impact chain: 单帧事件 + 粒子 ∝ 动能)
                SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.55f, Volume = 0.75f }, Projectile.Center);
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 14; i++) {
                        Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0,
                            Main.rand.NextBool() ? DustID.WoodFurniture : DustID.JungleGrass,
                            Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-7f, -2f), 70, default,
                            Main.rand.NextFloat(1.2f, 2f));
                        d.noGravity = Main.rand.NextBool();
                    }
                }
            }

            // 竖立期顶端漏叶
            if (t < PillarTotal - PillarWither && Main.rand.NextBool(5) && Main.netMode != NetmodeID.Server) {
                Vector2 tip = Projectile.Center - new Vector2(0f, PillarHeight * PillarRiseEase);
                Dust d = Dust.NewDustDirect(tip + Main.rand.NextVector2Circular(12, 12), 0, 0,
                    DustID.GrassBlades, 0, 1f, 90, default, 1.2f);
                d.noGravity = false;
            }

            Lighting.AddLight(Projectile.Center - new Vector2(0, PillarHeight * 0.5f), 0.1f, 0.22f, 0.05f);
        }

        #endregion

        #region 模式 2 — 藤鞭 (弹簧角度链)

        private float AttackAngle => Projectile.ai[2];

        /// <summary>挥打侧向: +1 = 顺时针。地面鞭由攻击角余弦决定 (从上过顶砸落); 冠层鞭恒 +1 (全阵同向)。</summary>
        private float SwingSide {
            get {
                if (Mode == ModeCrownWhip)
                    return 1f;
                float c = MathF.Cos(AttackAngle);
                return MathF.Abs(c) < 0.05f ? 1f : MathF.Sign(c);
            }
        }

        /// <summary>
        /// 连续参数化的攻击角: 朝左 (side=-1) 且 θ>0 时减去 2π, 保证
        /// Upright→Back→Strike→Rest 的插值路径不绕远路/不穿越玩家。
        /// </summary>
        private float ThetaNorm {
            get {
                float th = AttackAngle;
                if (SwingSide < 0f && th > 0f)
                    th -= MathHelper.TwoPi;
                return th;
            }
        }

        private float UprightAngle => Mode == ModeCrownWhip
            ? ThetaNorm - 1.5f                            // 冠层: 收拢在放射向后方
            : -MathHelper.PiOver2 + SwingSide * 0.22f;    // 地面: 近竖直立起
        private float BackAngle => ThetaNorm - SwingSide * (Mode == ModeCrownWhip ? 2.2f : 2.45f);  // 反侧高扬 (counter-motion)
        private float StrikeAngle => ThetaNorm + SwingSide * (Mode == ModeCrownWhip ? 0.30f : 0.35f); // 略微过打
        private float RestAngle => ThetaNorm + SwingSide * (Mode == ModeCrownWhip ? 0.8f : 1.2f);     // 顺势垂落 (follow-through)

        /// <summary>根角驱动曲线 — anticipation(smoothstep) → strike(poly(10) ease-out) → hold → 余摆。</summary>
        private float RootDriveAngle(int t) {
            if (t <= WhipSprout)
                return UprightAngle;
            if (t <= WhipWindupEnd) {
                float k = (t - WhipSprout) / (float)(WhipWindupEnd - WhipSprout);
                k = k * k * (3f - 2f * k); // smoothstep: 缓起缓收的读秒蓄势
                return MathHelper.Lerp(UprightAngle, BackAngle, k);
            }
            if (t <= WhipStrikeEnd) {
                float k = (t - WhipWindupEnd) / (float)(WhipStrikeEnd - WhipWindupEnd);
                k = 1f - MathF.Pow(1f - k, 10f); // poly(10): 几乎全部角行程在前几帧 → 一记"鞭响"
                return MathHelper.Lerp(BackAngle, StrikeAngle, k);
            }
            if (t <= WhipHoldEnd)
                return StrikeAngle;
            // 余摆期: 根角缓落到顺势垂落位, 链条靠弹簧自然震荡
            float r = MathHelper.Clamp((t - WhipHoldEnd) / (float)(WhipRecoverEnd - WhipHoldEnd), 0f, 1f);
            return MathHelper.Lerp(StrikeAngle, RestAngle, r * r * (3f - 2f * r));
        }

        private void EnsureChain() {
            if (segAngle != null)
                return;
            segAngle = new float[SegCount];
            segAngVel = new float[SegCount];
            float a0 = RootDriveAngle((int)Timer);
            for (int i = 0; i < SegCount; i++)
                segAngle[i] = a0;
        }

        private void WhipAI() {
            Projectile.velocity = Vector2.Zero;
            EnsureChain();
            Timer++;
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, WhipTotal - (int)Timer + 4);
            int t = (int)Timer;

            // —— 出土长度 ——
            if (t <= WhipSprout)
                whipGrowth = MathHelper.Lerp(0.15f, 0.55f, t / (float)WhipSprout);
            else if (t <= WhipWindupEnd)
                whipGrowth = MathHelper.Lerp(0.55f, 1f, (t - WhipSprout) / (float)(WhipWindupEnd - WhipSprout));
            else if (t > WhipRecoverEnd)
                whipGrowth = MathHelper.Lerp(1f, 0f, (t - WhipRecoverEnd) / (float)(WhipTotal - WhipRecoverEnd));
            else
                whipGrowth = 1f;

            // —— 弹簧角度链演化 (确定性): 根节直接驱动, 其余追赶前节 ——
            segAngle[0] = RootDriveAngle(t);
            for (int i = 1; i < SegCount; i++) {
                float stiffness = MathHelper.Lerp(0.26f, 0.15f, i / (float)(SegCount - 1)); // 梢部更软
                float delta = MathHelper.WrapAngle(segAngle[i - 1] - segAngle[i]);
                segAngVel[i] += delta * stiffness;
                segAngVel[i] *= 0.86f;
                segAngle[i] += segAngVel[i];
            }
            RebuildChain();

            // —— 关键帧事件 ——
            if (t == WhipSprout && Main.netMode != NetmodeID.Server) {
                if (Mode == ModeCrownWhip) {
                    // 冠层鞭: 枝叶抖开
                    SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.3f, Volume = 0.7f }, Projectile.Center);
                    for (int i = 0; i < 8; i++) {
                        Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.GrassBlades,
                            Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 90, default, 1.4f);
                        d.noGravity = true;
                    }
                }
                else {
                    SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.2f, Volume = 0.6f }, Projectile.Center);
                    for (int i = 0; i < 10; i++) {
                        Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.WoodFurniture,
                            Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, -1f), 110, default, 1.4f);
                        d.noGravity = false;
                    }
                }
            }

            if (t == WhipWindupEnd + 3 && !strikeCracked) {
                // 抽击炸响 (鞭梢过音速的一声脆响) + 梢部叶屑爆
                strikeCracked = true;
                SoundEngine.PlaySound(SoundID.Item153 with { Pitch = 0.25f, Volume = 0.9f }, chainPos[SegCount]);
                ACMUtils.AddScreenShake(3f);
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 16; i++) {
                        Dust d = Dust.NewDustDirect(chainPos[SegCount], 0, 0, DustID.GrassBlades,
                            Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-6f, 6f), 60, default, 1.6f);
                        d.noGravity = true;
                    }
                }
            }

            // 高速段: 鞭梢速度门控的飞叶 (speed-gated dressing)
            if (t > WhipWindupEnd && t <= WhipHoldEnd && Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                Vector2 tip = chainPos[SegCount];
                Dust d = Dust.NewDustDirect(tip + Main.rand.NextVector2Circular(16, 16), 0, 0,
                    DustID.JungleGrass, 0, 0, 80, default, 1.3f);
                d.noGravity = true;
            }

            Lighting.AddLight(chainPos[SegCount / 2], 0.1f, 0.24f, 0.05f);
        }

        /// <summary>由角度链重建世界坐标节点 (根点 = Projectile.Center)。</summary>
        private void RebuildChain() {
            chainPos[0] = Projectile.Center;
            float len = SegLen * MathHelper.Clamp(whipGrowth, 0.01f, 1f);
            for (int i = 0; i < SegCount; i++)
                chainPos[i + 1] = chainPos[i] + segAngle[i].ToRotationVector2() * len;
        }

        /// <summary>抽击伤害窗 (52~69f): 与视觉严格对齐的公平阀。</summary>
        private bool WhipDamageActive => Timer > WhipWindupEnd && Timer <= WhipHoldEnd;

        #endregion

        #region 判定 (伤害窗与视觉对齐)

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Mode == ModeWhip || Mode == ModeCrownWhip) {
                if (!WhipDamageActive || segAngle == null)
                    return false;
                // 仅鞭梢 8 节参与判定 (根段是"杆", 梢段才是"刃")
                float _ = 0f;
                for (int i = SegCount / 2; i < SegCount; i++) {
                    if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                        chainPos[i], chainPos[i + 1], 20f, ref _))
                        return true;
                }
                return false;
            }

            if (Mode == ModePillar) {
                // 竖立期间: 根基→顶端线段
                if (Timer <= PillarTelegraph || Timer > PillarTotal - PillarWither)
                    return false;
                float _ = 0f;
                Vector2 tip = Projectile.Center - new Vector2(0f, PillarHeight * PillarRiseEase);
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    Projectile.Center, tip, 24f, ref _);
            }

            return null; // 飞梭: 默认 AABB
        }

        #endregion

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            Vector2 at = (Mode == ModeWhip || Mode == ModeCrownWhip) && segAngle != null
                ? chainPos[SegCount / 2] : Projectile.Center;
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustDirect(at, 8, 8, DustID.JungleGrass,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, default, 1.2f);
                d.noGravity = true;
            }
        }

        #region 绘制 (DryadsVineRibbon 条带)

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            switch (Mode) {
                case ModePillar: DrawPillar(); break;
                case ModeWhip:
                case ModeCrownWhip: DrawWhip(); break;
                default: DrawShuttle(lightColor); break;
            }
            return false;
        }

        /// <summary>公共条带绘制: NonPremultiplied + LinearWrap + GameViewMatrix, s0 绑共享噪声。</summary>
        private static void DrawRibbon(Vector2[] worldPts, Func<float, float> widthFunc,
            float intensity, float tipGlow, float wither, float barkScale, Color tint) {
            Effect fx = RibbonEffect;
            if (fx == null || worldPts.Length < 2 || intensity <= 0.02f)
                return;

            // 世界 → 屏幕像素 (与 DrawBeam 同顶点契约)
            Vector2[] screenPts = new Vector2[worldPts.Length];
            for (int i = 0; i < worldPts.Length; i++)
                screenPts[i] = worldPts[i] - Main.screenPosition;

            var verts = ACMUtils.BuildRibbonStrip(screenPts, widthFunc, _ => tint, 0f, 2);
            if (verts.Length < 4)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uColorBark"]?.SetValue(BarkDark.ToVector4());
            fx.Parameters["uColorCore"]?.SetValue(CoreGreen.ToVector4());
            fx.Parameters["uColorGlow"]?.SetValue(SapGlow.ToVector4());
            fx.Parameters["uTipGlow"]?.SetValue(tipGlow);
            fx.Parameters["uWither"]?.SetValue(wither);
            fx.Parameters["uFlowSpeed"]?.SetValue(1.1f);
            fx.Parameters["uBarkScale"]?.SetValue(barkScale);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[0] = ACMShaders.NoiseTexture;
            gd.SamplerStates[0] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);

            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);
        }

        private void DrawWhip() {
            if (segAngle == null || whipGrowth <= 0.02f)
                return;
            int t = (int)Timer;

            // 导引线: 蓄势末 27f, 沿最终抽击方向渐显 (红色只在此时出现 — §6.1)
            if (t >= WhipGuideStart && t <= WhipWindupEnd) {
                float k = (t - WhipGuideStart) / (float)(WhipWindupEnd - WhipGuideStart);
                Vector2 dir = StrikeAngle.ToRotationVector2();
                ACMShaders.DrawBeam(Projectile.Center, Projectile.Center + dir * (SegLen * SegCount * 1.02f),
                    5f + k * 4f, TelegraphColors.Lethal, new Color(150, 20, 20), 0.25f + k * 0.55f,
                    flowSpeed: 2.2f, flowScale: 2.5f);
            }

            // 鞭体: 抽击/定格期梢部打满辉光 (速度可读)
            float tipGlow = WhipDamageActive ? 1f : 0.15f;
            float wither = t > WhipRecoverEnd
                ? (t - WhipRecoverEnd) / (float)(WhipTotal - WhipRecoverEnd) : 0f;

            Vector2[] pts = new Vector2[SegCount + 1];
            Array.Copy(chainPos, pts, SegCount + 1);
            DrawRibbon(pts,
                p => MathHelper.Lerp(13f, 5f, p) * MathHelper.Clamp(whipGrowth * 1.6f, 0.3f, 1f),
                MathHelper.Clamp(whipGrowth * 2.2f, 0f, 1f), tipGlow, wither, 5.5f, Color.White);

            // 抽击瞬间鞭梢加性光斑
            if (WhipDamageActive) {
                Texture2D glow = ACMAsset.SoftGlow;
                if (glow != null) {
                    SpriteBatch sb = Main.spriteBatch;
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    sb.Draw(glow, chainPos[SegCount] - Main.screenPosition, null,
                        new Color(180, 255, 120, 0) * 0.9f, 0f, glow.Size() / 2f, 0.9f, SpriteEffects.None, 0f);
                    sb.End();
                    ACMShaders.RestoreDefaultBatch(sb);
                }
            }
        }

        private void DrawPillar() {
            int t = (int)Timer;
            Vector2 basePos = Projectile.Center;

            // 预告: 光束从地面升起, 末 25% 转赤红 (致命)
            if (t < PillarTelegraph) {
                float grow = t / (float)PillarTelegraph;
                Vector2 tip = basePos - new Vector2(0f, MathHelper.Lerp(36f, PillarHeight * 0.9f, grow));
                float lethal = MathHelper.Clamp((grow - 0.75f) / 0.25f, 0f, 1f);
                Color core = Color.Lerp(new Color(160, 255, 120), TelegraphColors.Lethal, lethal);
                Color edge = Color.Lerp(new Color(40, 120, 30), new Color(150, 20, 20), lethal);
                ACMShaders.DrawBeam(basePos, tip, MathHelper.Lerp(7f, 18f, grow), core, edge,
                    0.35f + grow * 0.6f, flowSpeed: 1.7f, flowScale: 2.2f);
                return;
            }

            // 柱体: 带 S 形微弯的条带 (持续期顶端轻摆 → 活物感)
            float rise = PillarRiseEase;
            float wither = PillarWitherFrac;
            float height = PillarHeight * rise * (1f - wither * 0.25f);
            float sway = MathF.Sin((float)Main.GlobalTimeWrappedHourly * 2.6f + Projectile.whoAmI * 1.7f)
                       * 9f * (t > PillarTelegraph + PillarRise ? 1f : 0.2f) * (1f - wither);

            const int PillarPts = 6;
            Vector2[] pts = new Vector2[PillarPts];
            for (int i = 0; i < PillarPts; i++) {
                float p = i / (float)(PillarPts - 1);
                float bend = MathF.Sin(p * MathHelper.Pi * 0.9f) * sway * p;
                pts[i] = basePos + new Vector2(bend, -height * p);
            }

            DrawRibbon(pts, p => MathHelper.Lerp(15f, 6f, p), 1f - wither * 0.5f,
                rise < 1f ? 0.8f : 0.2f, wither, 3.6f, Color.White);
        }

        private void DrawShuttle(Color lightColor) {
            // 短尾条带 (oldPos) + 头部辉光
            int n = 0;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) break;
                n++;
            }
            if (n >= 2) {
                Vector2[] pts = new Vector2[n];
                for (int i = 0; i < n; i++)
                    pts[i] = Projectile.oldPos[n - 1 - i] + Projectile.Size / 2f;
                DrawRibbon(pts, p => MathHelper.Lerp(2.5f, 7f, p), 0.85f, 0.5f, 0f, 4.5f, Color.White);
            }

            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                    new Color(110, 220, 80, 0) * 0.6f, 0f, glow.Size() / 2f, 0.45f, SpriteEffects.None, 0f);
                sb.End();
                ACMShaders.RestoreDefaultBatch(sb);
            }
        }

        #endregion
    }
}
