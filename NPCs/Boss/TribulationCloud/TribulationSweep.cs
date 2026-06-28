using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.TribulationCloud
{
    /// <summary>
    /// 紫霄劫雷·移动安全区扫雷 —— 一道竖直雷幕横扫战场, 幕上留一道<b>安全缝(法眼)</b>; 玩家须站进缝里随之移动。
    ///
    /// <para><b>预警契约 (§6.1):</b> 预告期雷幕为青白电弧 (Lightning, 充能, 非致命) + 缝隙用翠玉安全色 (Safe) 明确标出;
    /// 横扫期雷幕转纯红致命 (TelegraphColors.Lethal)。缝隙之外的幕身致命, 缝内安全。本体无接触伤害, 全手动判定。</para>
    ///
    /// <para>spawn 参数: ai[0]=劫云 whoAmI, ai[1]=<see cref="StrikeFlags"/> 位标志, ai[2]=打包主题色(RGB)。
    /// 纯本地视觉, 服务端零绘制; 站位/扫向由出生坐标 + 网络 identity 推导, 多人一致。</para>
    /// </summary>
    public class TribulationSweep : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        private bool Final => ((int)Projectile.ai[1] & StrikeFlags.Final) != 0;

        // —— 时序 ——
        private int Windup => Final ? 70 : 50;
        private int SweepDur => Final ? 120 : 90;
        private const int FadeAfter = 14;

        // —— 几何 ——
        private const float Range = 760f;       // 横扫半幅
        private const float VHalf = 520f;       // 雷幕竖直半高
        private const float CurtainHalf = 46f;  // 幕身致命半宽
        private float GapHalf => Final ? 80f : 96f; // 安全缝半高 (法眼)

        private Vector2 _arenaCenter;   // 出生时记录的战场中心 (世界固定事件)
        private float _gapY;            // 安全缝中心 Y
        private float _dir;             // +1 左→右, -1 右→左

        private float Time => Projectile.localAI[0];

        private Color ThemeColor {
            get {
                int packed = (int)Projectile.ai[2];
                if (packed <= 0)
                    return new Color(168, 96, 224);
                return new Color((packed >> 16) & 255, (packed >> 8) & 255, packed & 255);
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

        public override void OnSpawn(IEntitySource source) {
            _arenaCenter = Projectile.Center;
            // 由网络 identity 推导扫向与缝位 (多人各端一致, 出生位置已同步)
            _dir = Projectile.identity % 2 == 0 ? 1f : -1f;
            _gapY = _arenaCenter.Y + ((Projectile.identity % 3) - 1) * 130f;
        }

        private float SweepX() {
            float prog = MathHelper.Clamp((Time - Windup) / System.Math.Max(1, SweepDur), 0f, 1f);
            float startX = _arenaCenter.X - _dir * Range;
            float endX = _arenaCenter.X + _dir * Range;
            return MathHelper.Lerp(startX, endX, prog);
        }

        public override void AI() {
            Projectile.localAI[0] += 1f;
            Projectile.Center = new Vector2(SweepX(), _arenaCenter.Y);

            if ((int)Time == 1)
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.6f, Pitch = 0.5f }, _arenaCenter);
            if ((int)Time == Windup) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = Final ? -0.3f : 0.1f }, _arenaCenter);
                ACMScreenShakeSystem.Add(Final ? 7f : 4f);
            }

            bool sweeping = (int)Time >= Windup && (int)Time < Windup + SweepDur;
            if (sweeping) {
                CurtainDust();
                ApplyZoneDamage();
                if ((int)Time % 9 == 0)
                    ACMScreenShakeSystem.Add(2f);
            }

            if ((int)Time >= Windup + SweepDur + FadeAfter)
                Projectile.Kill();
        }

        private void CurtainDust() {
            if (Main.dedServ)
                return;
            float x = SweepX();
            for (int i = 0; i < 3; i++) {
                float y = _arenaCenter.Y + Main.rand.NextFloat(-VHalf, VHalf);
                // 缝内不冒致命尘
                if (System.Math.Abs(y - _gapY) < GapHalf)
                    continue;
                Dust d = Dust.NewDustPerfect(new Vector2(x, y), DustID.RedTorch,
                    new Vector2(_dir * 2f, 0f), 70, TelegraphColors.Lethal, 1.3f);
                d.noGravity = true;
            }
            // 缝隙 (法眼) 翠玉安全微光
            if (Main.rand.NextBool(2)) {
                Dust s = Dust.NewDustPerfect(new Vector2(x, _gapY + Main.rand.NextFloat(-GapHalf, GapHalf)),
                    DustID.GreenTorch, Vector2.Zero, 120, TelegraphColors.Safe, 0.9f);
                s.noGravity = true;
            }
        }

        private void ApplyZoneDamage() {
            if (Main.dedServ)
                return;
            Player lp = Main.LocalPlayer;
            if (!lp.active || lp.dead || lp.immune)
                return;

            float x = SweepX();
            if (System.Math.Abs(lp.Center.X - x) > CurtainHalf)
                return;
            if (System.Math.Abs(lp.Center.Y - _arenaCenter.Y) > VHalf)
                return;
            if (System.Math.Abs(lp.Center.Y - _gapY) <= GapHalf)
                return; // 站在缝里 = 安全

            int hitDir = lp.Center.X > x ? 1 : -1;
            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason($"{lp.name} 被劫云的紫霄雷幕扫成了灰烬。");
            lp.Hurt(reason, Projectile.damage, hitDir);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            float t = (int)Time;
            float x = SweepX();
            bool sweeping = t >= Windup;

            float fade = 1f;
            if (t >= Windup + SweepDur)
                fade = MathHelper.Clamp(1f - (t - (Windup + SweepDur)) / FadeAfter, 0f, 1f);

            // 预告期为青白电弧充能 (非致命), 横扫期转纯红致命
            float windP = MathHelper.Clamp(t / System.Math.Max(1, Windup), 0f, 1f);
            Color body = sweeping ? TelegraphColors.Lethal : Color.Lerp(TelegraphColors.Lightning, TelegraphColors.Lethal, windP * 0.5f);
            Color edge = sweeping ? new Color(255, 120, 120) : TelegraphColors.Lightning;
            float halfW = sweeping ? CurtainHalf * fade : MathHelper.Lerp(4f, 14f, windP);
            float inten = sweeping ? fade : (0.35f + 0.45f * windP);

            Vector2 top = new Vector2(x, _arenaCenter.Y - VHalf);
            Vector2 bot = new Vector2(x, _arenaCenter.Y + VHalf);
            Vector2 gapTop = new Vector2(x, _gapY - GapHalf);
            Vector2 gapBot = new Vector2(x, _gapY + GapHalf);

            // 幕身分两段, 留出安全缝
            ACMShaders.DrawBeam(top, gapTop, halfW, body, edge, inten, flowSpeed: 2.6f, flowScale: 3.2f, coreGlow: sweeping ? 1.2f : 0.4f);
            ACMShaders.DrawBeam(gapBot, bot, halfW, body, edge, inten, flowSpeed: 2.6f, flowScale: 3.2f, coreGlow: sweeping ? 1.2f : 0.4f);

            // 安全缝 (法眼) 翠玉标记 —— 始终清晰可读
            ACMShaders.DrawBeam(gapTop, gapBot, 8f, TelegraphColors.Safe, TelegraphColors.Safe * 0.3f, 0.5f + 0.3f * windP,
                flowSpeed: 1.2f, flowScale: 1.5f);

            return false;
        }
    }
}
