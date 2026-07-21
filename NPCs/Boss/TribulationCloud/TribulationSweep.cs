using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.TribulationCloud
{
    /// <summary>
    /// 紫霄劫雷·移动安全区扫雷 —— 竖直雷幕横扫战场, 幕上留一道<b>安全缝(法眼)</b>; 玩家须站进缝里随之移动。
    ///
    /// <para><b>三段递进:</b> 基础 = 单幕横扫, 缝静止;
    /// <see cref="StrikeFlags.DriftGap"/> = 缝随横扫平滑漂移 (预告期以翠玉竖线标出漂移路径, 可预读);
    /// <see cref="StrikeFlags.DualSweep"/> = 双幕从两侧向中心合拢 (缝同 Y 联动), 相遇即炸散。</para>
    ///
    /// <para><b>预警契约 (§6.1):</b> 预告期雷幕为青白电弧 (Lightning, 非致命) + 缝隙/漂移路径用翠玉安全色
    /// (Safe) 明确标出; 横扫期幕身转纯红致命 (TelegraphColors.Lethal) 并叠折线电弧。缝内安全, 全手动判定。</para>
    ///
    /// <para>spawn 参数: ai[0]=劫云 whoAmI, ai[1]=<see cref="StrikeFlags"/> 位标志, ai[2]=打包主题色(RGB)。
    /// 站位/扫向/缝位由出生坐标 + 网络 identity 推导, 多人各端一致; 服务端零绘制。</para>
    /// </summary>
    public class TribulationSweep : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private bool Final => ((int)Projectile.ai[1] & StrikeFlags.Final) != 0;
        private bool Drift => ((int)Projectile.ai[1] & StrikeFlags.DriftGap) != 0;
        private bool Dual => ((int)Projectile.ai[1] & StrikeFlags.DualSweep) != 0;

        // —— 时序 ——
        private int Windup => Final ? 80 : 55;
        private int SweepDur => Dual ? 110 : 95;
        private const int FadeAfter = 14;

        // —— 几何 ——
        private const float Range = 760f;       // 横扫半幅
        private const float VHalf = 520f;       // 雷幕竖直半高
        private const float CurtainHalf = 46f;  // 幕身致命半宽
        private const float DriftDist = 150f;   // 缝漂移距离
        private float GapHalf => Final ? 80f : 96f; // 安全缝半高 (法眼)

        private bool _burst;            // 双幕相遇爆散 (防重复)

        private float Time => Projectile.localAI[0];

        // —— 几何推导 (OnSpawn 不在远端客户端执行, 故一律由已同步的出生坐标 + identity 惰性推导, 多人各端一致) ——
        /// <summary>战场中心 = 出生坐标 (AI 不移动本体, 位置天然同步)。</summary>
        private Vector2 ArenaCenter => Projectile.Center;
        /// <summary>扫向: +1 左→右, -1 右→左 (单幕)。</summary>
        private float Dir => Projectile.identity % 2 == 0 ? 1f : -1f;
        /// <summary>缝起始 Y。</summary>
        private float GapBaseY => ArenaCenter.Y + (Projectile.identity % 3 - 1) * 130f;
        /// <summary>缝漂移方向 ±1。</summary>
        private float DriftDir => Projectile.identity / 3 % 2 == 0 ? 1f : -1f;

        private Color ThemeColor {
            get {
                int packed = (int)Projectile.ai[2];
                if (packed <= 0)
                    return new Color(168, 96, 224);
                return new Color((packed >> 16) & 255, (packed >> 8) & 255, packed & 255);
            }
        }

        private TribulationCloudBase Cloud {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active && Main.npc[idx].ModNPC is TribulationCloudBase tcb)
                    return tcb;
                return null;
            }
        }

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.alpha = 255;
            Projectile.light = 0.2f;
            Projectile.netImportant = true;
        }

        private float SweepProg => MathHelper.Clamp((Time - Windup) / Math.Max(1, SweepDur), 0f, 1f);

        /// <summary>当前缝中心 Y (漂移 = 平滑滑向终点, 最大坡度 ~2.4px/f, 可跟随)。</summary>
        private float GapY(float prog) => Drift
            ? MathHelper.Lerp(GapBaseY, GapBaseY + DriftDir * DriftDist, MathHelper.SmoothStep(0f, 1f, prog))
            : GapBaseY;

        /// <summary>取当前活动雷幕 X (单幕 1 条 / 双幕 2 条)。</summary>
        private int GetCurtainXs(Span<float> xs) {
            float prog = SweepProg;
            Vector2 center = ArenaCenter;
            if (Dual) {
                xs[0] = MathHelper.Lerp(center.X - Range, center.X, prog);
                xs[1] = MathHelper.Lerp(center.X + Range, center.X, prog);
                return 2;
            }
            xs[0] = MathHelper.Lerp(center.X - Dir * Range, center.X + Dir * Range, prog);
            return 1;
        }

        public override void AI() {
            Projectile.localAI[0] += 1f;
            int t = (int)Time;
            Projectile.velocity = Vector2.Zero; // 本体钉在战场中心 (世界固定事件)
            Vector2 center = ArenaCenter;

            if (t == 1)
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.7f, Pitch = 0.4f }, center);
            // 预告中点再叩一声 (雷幕越近越急)
            if (t == Windup / 2)
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.55f, Pitch = 0.7f }, center);
            if (t == Windup) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = Dual ? 1.3f : 1f, Pitch = Final ? -0.35f : 0f }, center);
                ACMScreenShakeSystem.Add(Final ? 8f : 5f);
                TribulationScreenSystem.Flash(Dual ? 0.3f : 0.2f);
            }

            bool sweeping = t >= Windup && t < Windup + SweepDur;
            if (sweeping) {
                CurtainVisuals();
                ApplyZoneDamage();
                if (t % 9 == 0)
                    ACMScreenShakeSystem.Add(2f);
                if (t % 34 == 5)
                    SoundEngine.PlaySound(SoundID.DD2_LightningBugZap with { Volume = 0.5f, Pitch = -0.2f }, new Vector2(CurtainNearestToLocal(), center.Y));
            }

            // 双幕相遇爆散 (合拢的收束一击, 纯演出)
            if (Dual && !_burst && t >= Windup + SweepDur) {
                _burst = true;
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f, Pitch = -0.25f }, center);
                ACMScreenShakeSystem.Add(9f);
                TribulationScreenSystem.Flash(0.35f);
                Cloud?.PublishFlash(center.X, 0.9f);
                if (!Main.dedServ) {
                    for (int i = 0; i < 26; i++) {
                        Vector2 vel = new(Main.rand.NextFloat(-9f, 9f), Main.rand.NextFloat(-11f, -2f));
                        Dust d = Dust.NewDustPerfect(new Vector2(center.X, GapY(1f) + Main.rand.NextFloat(-VHalf, VHalf) * 0.6f),
                            DustID.Electric, vel, 40, TelegraphColors.Lightning, 1.6f);
                        d.noGravity = true;
                    }
                }
            }

            if (t >= Windup + SweepDur + FadeAfter)
                Projectile.Kill();
        }

        /// <summary>横扫期视觉: 幕身致命尘 + 缝内翠玉安全微光 + 云内电光联动。</summary>
        private void CurtainVisuals() {
            if (Main.dedServ)
                return;

            float prog = SweepProg;
            float gapY = GapY(prog);
            Span<float> xs = stackalloc float[2];
            int n = GetCurtainXs(xs);

            for (int c = 0; c < n; c++) {
                float x = xs[c];
                Cloud?.PublishFlash(x, 0.3f + 0.1f * c);
                float xDir = Dual ? (c == 0 ? 1f : -1f) : Dir;
                for (int i = 0; i < 3; i++) {
                    float y = ArenaCenter.Y + Main.rand.NextFloat(-VHalf, VHalf);
                    // 缝内不冒致命尘
                    if (Math.Abs(y - gapY) < GapHalf)
                        continue;
                    Dust d = Dust.NewDustPerfect(new Vector2(x, y), DustID.RedTorch,
                        new Vector2(xDir * 2f, 0f), 70, TelegraphColors.Lethal, 1.3f);
                    d.noGravity = true;
                }
                // 缝隙 (法眼) 翠玉安全微光
                if (Main.rand.NextBool(2)) {
                    Dust s = Dust.NewDustPerfect(new Vector2(x, gapY + Main.rand.NextFloat(-GapHalf, GapHalf)),
                        DustID.GreenTorch, Vector2.Zero, 120, TelegraphColors.Safe, 0.9f);
                    s.noGravity = true;
                }
            }
        }

        private float CurtainNearestToLocal() {
            Span<float> xs = stackalloc float[2];
            int n = GetCurtainXs(xs);
            float px = Main.dedServ ? ArenaCenter.X : Main.LocalPlayer.Center.X;
            float best = xs[0];
            for (int i = 1; i < n; i++)
                if (Math.Abs(xs[i] - px) < Math.Abs(best - px))
                    best = xs[i];
            return best;
        }

        private void ApplyZoneDamage() {
            if (Main.dedServ)
                return;
            Player lp = Main.LocalPlayer;
            if (!lp.active || lp.dead || lp.immune)
                return;
            if (Math.Abs(lp.Center.Y - ArenaCenter.Y) > VHalf)
                return;
            if (Math.Abs(lp.Center.Y - GapY(SweepProg)) <= GapHalf)
                return; // 站在缝里 = 安全

            Span<float> xs = stackalloc float[2];
            int n = GetCurtainXs(xs);
            for (int c = 0; c < n; c++) {
                if (Math.Abs(lp.Center.X - xs[c]) > CurtainHalf)
                    continue;
                int hitDir = lp.Center.X > xs[c] ? 1 : -1;
                PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                    Terraria.Localization.NetworkText.FromLiteral($"{lp.name} 被劫云的紫霄雷幕扫成了灰烬。"));
                lp.Hurt(reason, Projectile.damage, hitDir);
                return;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float t = (int)Time;
            bool sweeping = t >= Windup;
            float prog = SweepProg;
            float gapY = GapY(prog);

            float fade = 1f;
            if (t >= Windup + SweepDur)
                fade = MathHelper.Clamp(1f - (t - (Windup + SweepDur)) / FadeAfter, 0f, 1f);

            // 预告期为青白电弧充能 (非致命), 横扫期转纯红致命
            float windP = MathHelper.Clamp(t / Math.Max(1, Windup), 0f, 1f);
            Color body = sweeping ? TelegraphColors.Lethal : Color.Lerp(TelegraphColors.Lightning, TelegraphColors.Lethal, windP * 0.5f);
            Color edge = sweeping ? new Color(255, 120, 120) : TelegraphColors.Lightning;
            float halfW = sweeping ? CurtainHalf * fade : MathHelper.Lerp(4f, 14f, windP);
            float inten = sweeping ? fade : (0.35f + 0.45f * windP);

            Span<float> xs = stackalloc float[2];
            int n = GetCurtainXs(xs);

            for (int c = 0; c < n; c++) {
                float x = xs[c];
                Vector2 top = new(x, ArenaCenter.Y - VHalf);
                Vector2 bot = new(x, ArenaCenter.Y + VHalf);
                Vector2 gapTop = new(x, gapY - GapHalf);
                Vector2 gapBot = new(x, gapY + GapHalf);

                // 幕身分两段, 留出安全缝
                ACMShaders.DrawBeam(top, gapTop, halfW, body, edge, inten, flowSpeed: 2.6f, flowScale: 3.2f, coreGlow: sweeping ? 1.2f : 0.4f);
                ACMShaders.DrawBeam(gapBot, bot, halfW, body, edge, inten, flowSpeed: 2.6f, flowScale: 3.2f, coreGlow: sweeping ? 1.2f : 0.4f);

                // 横扫期在较长的一段上叠折线电弧 (雷幕的"电"感, 每 4f 换形)
                if (sweeping && fade > 0.15f) {
                    bool topLonger = (gapTop.Y - top.Y) >= (bot.Y - gapBot.Y);
                    Vector2 a = topLonger ? top : gapBot;
                    Vector2 b = topLonger ? gapTop : bot;
                    float seed = Projectile.identity * 0.917f + c * 5.3f + (int)(t / 4f) * 1.83f;
                    TribulationFX.DrawBolt(a, b, 130f, Color.Lerp(ThemeColor, TelegraphColors.Lightning, 0.4f),
                        0.8f * fade, seed, 0f, widthScale: 0.5f, branch: 0.4f, flicker: 0.6f);
                }

                // 安全缝 (法眼) 翠玉标记 —— 始终清晰可读
                ACMShaders.DrawBeam(gapTop, gapBot, 8f, TelegraphColors.Safe, TelegraphColors.Safe * 0.3f, (0.5f + 0.3f * windP) * MathF.Max(fade, 0.4f),
                    flowSpeed: 1.2f, flowScale: 1.5f);

                // 预告期: 缝漂移路径预读 (翠玉幽线标出缝将滑向哪里)
                if (!sweeping && Drift) {
                    float endY = GapBaseY + DriftDir * DriftDist;
                    ACMShaders.DrawBeam(new Vector2(x, MathF.Min(GapBaseY, endY) - GapHalf), new Vector2(x, MathF.Max(GapBaseY, endY) + GapHalf),
                        3.5f, TelegraphColors.Safe, TelegraphColors.Safe * 0.2f, 0.25f + 0.15f * windP, flowSpeed: 0.8f, flowScale: 1f);
                }
            }

            return false;
        }
    }
}
