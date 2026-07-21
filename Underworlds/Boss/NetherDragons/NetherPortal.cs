using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.NetherDragons
{
    /// <summary>
    /// 冥界之门 (Nether Gate) —— 幽冥龙一切强力行为的舞台与预警语言。
    ///
    /// V3: 从"fog 贴图烟圈"重做为 <c>NetherDragonGate</c> 着色器贴花的完整门生命周期,
    /// 门的开裂本身就是 telegraph 三拍: <b>裂缝发光(紫) → 裂纹分叉+濒开(转红) → 轰然破开</b>。
    ///
    /// 状态机 (确定性, 各端独立推进, 生成参数全在 ai[] 内 → 多人零额外同步):
    ///   裂缝生长 [0..crackTime] → 破开 [14f] → 稳定 [holdTime] → 合拢 [18f] → Kill。
    /// 假门(换阶段两假一真)在裂缝转红**之前**自动枯萎 — 保证"红=致命"语义诚实。
    /// 长裂纹门 (crackTime ≥ 24) 在转红窗口额外画一条沿 <see cref="GateDir"/> 的红色戳刺
    /// 预警线 (龙将沿此线轰出)。
    ///
    /// ai[0]=门轴朝向rad(龙出方向);
    /// ai[1]=打包 holdTime*1000+crackTime (crackTime&lt;1000; holdTime=0 → 由 timeLeft 保底);
    /// ai[2]=门半高像素 (0→默认150; **负值 = 假门**)。
    /// </summary>
    internal class NetherPortal : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float GateDirAI => ref Projectile.ai[0];
        private ref float PackedTimeAI => ref Projectile.ai[1];
        private ref float HalfHeightAI => ref Projectile.ai[2];

        /// <summary>打包 ai[1]: holdTime*1000 + crackTime。</summary>
        public static float PackTimes(int crackTime, int holdTime) => holdTime * 1000f + Math.Clamp(crackTime, 6, 999);

        private enum GateState { Crack, Burst, Hold, Close, Wither }

        private GateState state = GateState.Crack;
        private int timer;

        // —— 演出标量 (纯本地视觉) ——
        private float crack;        // 裂缝生长 0~1
        private float open;         // 开裂度 0~1
        private float wither;       // 枯萎消散 0~1
        private float boomFlash;    // 破开白闪脉冲

        private const int BurstDur = 14;
        private const int CloseDur = 18;
        private const int WitherDur = 26;

        /// <summary>门轴朝向 (龙出方向)。</summary>
        public Vector2 GateDir => GateDirAI.ToRotationVector2();

        /// <summary>门是否已完全破开 (龙可通行)。</summary>
        public bool IsOpen => state == GateState.Hold || (state == GateState.Burst && open > 0.85f);

        /// <summary>当前开裂度 0~1 (供头部对齐演出)。</summary>
        public float OpenAmount => open;

        /// <summary>假门 (换阶段两假一真): 只走裂缝, 转红前枯萎。</summary>
        public bool IsFake => HalfHeightAI < 0f;

        /// <summary>
        /// 是否画沿 GateDir 的红色戳刺线 — 由裂纹时长确定性推导 (各端一致):
        /// 长裂纹 (≥24f) = 龙将轰出的攻击门 → 有红线; 短快门 (吞没/回潜) 非攻击 → 无红线。
        /// </summary>
        public bool ShowThrustLine => CrackTime >= 24 && !IsFake;

        private float HalfHeight => MathF.Abs(HalfHeightAI) > 1f ? MathF.Abs(HalfHeightAI) : 150f;
        private int CrackTime => Math.Max(6, (int)(PackedTimeAI % 1000f));
        private int HoldTime => (int)(PackedTimeAI / 1000f);

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 1200;      // 保底寿命; head 会显式关门
            Projectile.alpha = 255;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            timer++;

            switch (state) {
                case GateState.Crack:
                    // 第一拍: 裂缝生长, 末段自动转红 (绘制端按 crack 推 uCrackRed)
                    crack = MathHelper.Clamp(timer / (float)CrackTime, 0f, 1f);
                    if (timer == 1)
                        SoundEngine.PlaySound(SoundID.Item101 with { Volume = 0.55f, Pitch = -0.4f }, Projectile.Center);

                    // 假门在转红之前枯萎 — "红=致命"语义诚实 (红过的门必然有龙)
                    if (IsFake && crack >= 0.55f) {
                        WitherAway();
                        break;
                    }

                    // 濒开 beep: 转红点与末程各一声, 音高上行
                    if (timer == (int)(CrackTime * 0.55f))
                        SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Center);
                    if (timer == (int)(CrackTime * 0.85f))
                        SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.9f, Pitch = 0.3f }, Projectile.Center);

                    if (!Main.dedServ && crack > 0.2f && Main.rand.NextBool(3)) {
                        // 裂缝渗出的幽尘 (沿门长轴)
                        Vector2 axis = GateDir.RotatedBy(MathHelper.PiOver2);
                        Vector2 p = Projectile.Center + axis * Main.rand.NextFloat(-0.9f, 0.9f) * HalfHeight * crack;
                        var d = Dust.NewDustPerfect(p, DustID.PurpleTorch, Vector2.Zero, 120,
                            new Color(120, 90, 200), 1.1f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(0.8f, 0.8f);
                    }

                    if (timer >= CrackTime) {
                        state = GateState.Burst;
                        timer = 0;
                        boomFlash = 1f;
                        SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 1.1f, Pitch = -0.25f }, Projectile.Center);
                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f, Pitch = -0.5f }, Projectile.Center);
                        ACMUtils.AddScreenShake(6f);
                        if (Main.netMode != NetmodeID.Server) {
                            NetherDragonFogSystem.CreateRipple(Projectile.Center, 2.2f);
                            BurstDust(30);
                        }
                    }
                    break;

                case GateState.Burst:
                    // 第二拍: 轰然破开 (BackOut 过冲 → 门"撑开"的弹性)
                    crack = 1f;
                    open = ACMUtils.BackOut(timer / (float)BurstDur);
                    if (timer >= BurstDur) {
                        state = GateState.Hold;
                        timer = 0;
                        open = 1f;
                    }
                    break;

                case GateState.Hold:
                    open = 1f + MathF.Sin(timer * 0.09f) * 0.02f; // 呼吸
                    // 稳定期少量吸入粒子 (门在"吞")
                    if (!Main.dedServ && Main.rand.NextBool(4)) {
                        Vector2 off = Main.rand.NextVector2CircularEdge(1f, 1f) * HalfHeight * Main.rand.NextFloat(0.9f, 1.3f);
                        var d = Dust.NewDustPerfect(Projectile.Center + off, DustID.GreenTorch, Vector2.Zero, 120,
                            new Color(110, 230, 150), 1.2f);
                        d.noGravity = true;
                        d.velocity = -off.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 4f);
                    }
                    // 稳定时长期满或寿命将尽 → 自动合拢 (保底出口)
                    if ((HoldTime > 0 && timer >= HoldTime) || Projectile.timeLeft <= CloseDur + 2)
                        StartClosing();
                    break;

                case GateState.Close:
                    open = MathHelper.Clamp(1f - timer / (float)CloseDur, 0f, 1f);
                    crack = open; // 裂缝随之愈合
                    if (timer >= CloseDur) {
                        if (Main.netMode != NetmodeID.Server)
                            BurstDust(14);
                        SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.35f }, Projectile.Center);
                        Projectile.Kill();
                        return;
                    }
                    break;

                case GateState.Wither:
                    // 假门枯萎: 不破开, 裂缝噪声消散
                    wither = MathHelper.Clamp(timer / (float)WitherDur, 0f, 1f);
                    if (timer >= WitherDur) {
                        Projectile.Kill();
                        return;
                    }
                    break;
            }

            boomFlash = MathHelper.Lerp(boomFlash, 0f, 0.12f);

            // 着色器关闭时的粒子退化: 沿门形椭圆缘描点, 保住"门在哪/开多大"的核心可读信息
            if (!Main.dedServ && !MythologyConfig.FullscreenShadersEnabled &&
                (crack > 0.1f || open > 0.05f) && state != GateState.Wither) {
                float halfW = (0.05f + 0.50f * MathF.Min(open, 1f)) * HalfHeight;
                for (int i = 0; i < 2; i++) {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 local = new(MathF.Cos(a) * halfW, MathF.Sin(a) * HalfHeight * MathF.Max(crack, open));
                    Vector2 world = Projectile.Center + local.RotatedBy(GateDirAI);
                    bool lethal = CrackRed > 0.3f && state == GateState.Crack;
                    var d = Dust.NewDustPerfect(world, lethal ? DustID.RedTorch : DustID.GreenTorch,
                        Vector2.Zero, 100, lethal ? TelegraphColors.Lethal : TelegraphColors.GhostGreen, 1.2f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }
            }

            float glow = MathF.Max(crack * 0.5f, open);
            Lighting.AddLight(Projectile.Center, 0.30f * glow, 0.55f * glow, 0.45f * glow);
        }

        private void BurstDust(int count) {
            Vector2 axis = GateDir.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < count; i++) {
                Vector2 p = Projectile.Center + axis * Main.rand.NextFloat(-1f, 1f) * HalfHeight;
                var d = Dust.NewDustPerfect(p, Main.rand.NextBool() ? DustID.GreenTorch : DustID.PurpleTorch,
                    Vector2.Zero, 100, new Color(110, 230, 150), Main.rand.NextFloat(1.4f, 2.2f));
                d.noGravity = true;
                d.velocity = GateDir.RotatedByRandom(0.7f) * Main.rand.NextFloat(2f, 8f) * (Main.rand.NextBool() ? 1f : -1f);
            }
        }

        /// <summary>开始合拢 (幂等; head 编排收尾/保底超时共用)。</summary>
        public void StartClosing() {
            if (state == GateState.Close || state == GateState.Wither)
                return;
            state = GateState.Close;
            timer = 0;
            // 从未开过的门直接走枯萎, 避免"没开先关"的怪异帧
            if (open <= 0.01f) {
                state = GateState.Wither;
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.4f, Pitch = 0.4f }, Projectile.Center);
            }
        }

        /// <summary>假门枯萎消散 (换阶段两假一真的"假"揭晓)。</summary>
        public void WitherAway() {
            if (state == GateState.Wither)
                return;
            state = GateState.Wither;
            timer = 0;
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.5f, Pitch = 0.35f }, Projectile.Center);
        }

        /// <summary>
        /// 门体贴花绘制 — 由 <see cref="NetherDragonScreenSystem.PostDrawTiles"/> 调用,
        /// 画在**实体之下** (龙从门内钻出时不被门内暗渊遮挡)。自行开合批。
        /// </summary>
        public void DrawGateDecal() {
            if (Main.dedServ)
                return;

            Effect fx = NetherDragonVFX.Gate;
            if (fx == null)
                return;

            float intensity = state == GateState.Wither ? (1f - wither) : 1f;
            if (intensity <= 0.02f)
                return;

            // 屏外剔除 (decal 是全屏 pass, 离屏的门直接跳过)
            Vector2 scr = Projectile.Center - Main.screenPosition;
            float cull = HalfHeight * 2.5f;
            if (scr.X < -cull || scr.X > Main.screenWidth + cull || scr.Y < -cull || scr.Y > Main.screenHeight + cull)
                return;

            ACMShaders.WorldDecalParams(Projectile.Center, HalfHeight, out Vector2 uv, out float radiusFrac, out float aspect);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uIntensity"]?.SetValue(intensity);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uDir"]?.SetValue(GateDirAI);
            fx.Parameters["uCrack"]?.SetValue(crack);
            fx.Parameters["uCrackRed"]?.SetValue(CrackRed);
            fx.Parameters["uOpen"]?.SetValue(MathHelper.Clamp(open + boomFlash * 0.12f, 0f, 1.15f));
            fx.Parameters["uDissolve"]?.SetValue(wither);
            fx.Parameters["uColorRim"]?.SetValue(new Vector4(TelegraphColors.GhostGreen.ToVector3(), 0.85f));
            fx.Parameters["uColorCrack"]?.SetValue(new Vector4(TelegraphColors.NetherViolet.ToVector3(), 1f));
            fx.Parameters["uColorDeep"]?.SetValue(new Vector4(0.16f, 0.09f, 0.30f, 1f));

            ACMShaders.DrawScreenSpaceDecalStandalone(fx, BlendState.AlphaBlend);
        }

        // 裂缝末段自动致命化: 60% 后渐红 (§6.1 红=致命, 破开前必读)
        private float CrackRed => MathHelper.Clamp((crack - 0.60f) / 0.35f, 0f, 1f);

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            // 门体贴花在 PostDrawTiles 绘制 (实体之下); 此处只画应压在实体之上的预警/闪光层。

            // 转红窗口的戳刺预警线: 龙将沿 GateDir 轰出的致命路径 (§6.1 红线)
            if (ShowThrustLine && state == GateState.Crack && CrackRed > 0.02f) {
                ACMShaders.DrawBeam(Projectile.Center, Projectile.Center + GateDir * 900f,
                    2.5f + CrackRed * 3f, TelegraphColors.Lethal, TelegraphColors.Lethal with { A = 0 },
                    0.35f + CrackRed * 0.5f, flowSpeed: 2.4f, flowScale: 3f, coreSharp: 3f);
            }

            // 破开白闪 (加性小泛光, 不占全屏名额的廉价层)
            if (boomFlash > 0.05f) {
                Texture2D soft = ACMAsset.SoftGlow;
                if (soft != null) {
                    Vector2 scr = Projectile.Center - Main.screenPosition;
                    Color c = TelegraphColors.GhostGreen with { A = 0 };
                    Main.spriteBatch.Draw(soft, scr, null, c * (boomFlash * 0.9f), 0f,
                        soft.Size() / 2f, HalfHeight * 2.6f / soft.Width * boomFlash, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
