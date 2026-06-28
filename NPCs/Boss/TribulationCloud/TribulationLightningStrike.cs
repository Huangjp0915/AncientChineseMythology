using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.TribulationCloud
{
    /// <summary>
    /// 劫雷·点雷 (黑=固定节奏 / 赤=佯攻) + 渡劫成功金光 (纯演出) 三合一参数化弹幕。
    ///
    /// <para><b>预警契约 (§6.1):</b> 红 = 致命预警, 只在<b>真正会落雷</b>的蓄力/落点出现 (TelegraphColors.Lethal);
    /// 赤雷的"假蓄力"用非红尘烟/微光, 不冒红 —— 避免"狼来了"。落点法阵 (ArenaRunic) 红色渐强 → DrawBeam 雷柱命中
    /// → RadialBloom 泛光 + ACMScreenShakeSystem 震屏。本体无接触伤害, 仅在落雷瞬间对站在落点内的玩家造成致命伤。</para>
    ///
    /// <para>spawn 参数: ai[0]=劫云 whoAmI (雷柱起点), ai[1]=<see cref="StrikeFlags"/> 位标志, ai[2]=打包主题色(RGB)。
    /// 伤害=Projectile.damage; owner=渡劫者。纯本地视觉, 服务端零绘制。</para>
    /// </summary>
    public class TribulationLightningStrike : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        // —— 时序 (tick) ——
        private int FakeDur => Feint ? 30 : 0;                 // 赤雷假蓄力 0.5s
        private int Windup => Final ? 70 : (Feint ? 35 : 45);  // 真预警时长
        private int StrikeMoment => FakeDur + Windup;          // 落雷瞬间
        private const int LethalWindow = 5;                    // 落点致命持续帧 (跑进去也会死, 公平)
        private const int FadeAfter = 16;                      // 落雷后余辉

        private const int FinaleLife = 60;                     // 成功金光时长

        private float Radius => Final ? 132f : 96f;            // 落点致命半径

        private bool Feint => ((int)Projectile.ai[1] & StrikeFlags.Feint) != 0;
        private bool Final => ((int)Projectile.ai[1] & StrikeFlags.Final) != 0;
        private bool Finale => ((int)Projectile.ai[1] & StrikeFlags.SuccessFinale) != 0;

        private Vector2 _target;     // 真实落点
        private bool _struck;        // 已落雷 (防重复音/泛光)
        private float Time => Projectile.localAI[0];

        private Color ThemeColor {
            get {
                int packed = (int)Projectile.ai[2];
                if (packed <= 0)
                    return TelegraphColors.Gold;
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
            Projectile.hostile = false;       // 伤害全手动 (定点 + 落雷瞬间), 避免自动接触伤害
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.alpha = 255;
            Projectile.light = 0.2f;
            Projectile.netImportant = true;
        }

        public override void OnSpawn(IEntitySource source) {
            _target = Projectile.Center;
        }

        public override void AI() {
            Projectile.localAI[0] += 1f;

            if (Finale) {
                FinaleAI();
                return;
            }

            // 假蓄力结束 → 真雷"追到"玩家新站位 (赤雷反制: 别被假动作骗走)
            if (Feint && (int)Time == FakeDur) {
                Player owner = Main.player[Projectile.owner];
                if (owner.active && !owner.dead)
                    _target = owner.Center;
                Projectile.Center = _target;
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.55f, Pitch = 0.3f }, _target);
            }

            // 真预警开始的蓄力音 (黑雷无假段, 直接此时)
            if ((int)Time == FakeDur) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 0.5f, Pitch = 0.4f }, _target);
            }

            // 蓄力期上升尘烟
            ChargeDust();

            // —— 落雷瞬间 ——
            if (!_struck && (int)Time >= StrikeMoment) {
                _struck = true;
                Strike();
            }

            // 落点致命窗口: 跑进落点同样致命
            if (_struck && (int)Time <= StrikeMoment + LethalWindow)
                ApplyZoneDamage();

            if ((int)Time >= StrikeMoment + FadeAfter)
                Projectile.Kill();
        }

        private void FinaleAI() {
            // 纯演出: 金光柱 + 泛光 (无伤)。结算时刻一次性升华
            if (!Main.dedServ && (int)Time % 3 == 0) {
                Vector2 v = new Vector2(0, -Main.rand.NextFloat(6f, 14f)).RotatedByRandom(0.3f);
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(40f, 20f),
                    DustID.GoldFlame, v, 60, TelegraphColors.Gold, 1.6f);
                d.noGravity = true;
            }
            if ((int)Time >= FinaleLife)
                Projectile.Kill();
        }

        private void ChargeDust() {
            if (Main.dedServ)
                return;
            float prog = Final ? 1.2f : 1f;
            bool real = (int)Time >= FakeDur;
            // 假蓄力期 = 非红尘烟 (主题色); 真预警期 = 红色致命尘烟
            Color c = real ? TelegraphColors.Lethal : ThemeColor;
            int dustType = real ? DustID.RedTorch : DustID.Electric;
            if (Main.rand.NextFloat() < 0.5f * prog) {
                Vector2 around = _target + Main.rand.NextVector2Circular(Radius * 0.8f, Radius * 0.35f);
                Dust d = Dust.NewDustPerfect(around, dustType,
                    new Vector2(0, -Main.rand.NextFloat(3f, 8f)), 80, c, real ? 1.4f : 1.0f);
                d.noGravity = true;
            }
        }

        private void Strike() {
            // 命中音 + 震屏 (终雷更重)
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = Final ? 1.3f : 1f, Pitch = Final ? -0.4f : 0f }, _target);
            SoundEngine.PlaySound(SoundID.DD2_KoboldExplosion with { Volume = 0.7f }, _target);
            ACMScreenShakeSystem.Add(Final ? 9f : 5f);

            if (Main.dedServ)
                return;
            // 命中爆尘
            for (int i = 0; i < (Final ? 26 : 16); i++) {
                Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                Dust d = Dust.NewDustPerfect(_target, DustID.RedTorch, vel, 60, TelegraphColors.Lethal, 1.6f);
                d.noGravity = true;
            }
        }

        private void ApplyZoneDamage() {
            if (Main.dedServ)
                return;
            Player lp = Main.LocalPlayer;
            if (!lp.active || lp.dead || lp.immune)
                return;
            if (Vector2.Distance(lp.Center, _target) > Radius)
                return;

            int hitDir = lp.Center.X > _target.X ? 1 : -1;
            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason($"{lp.name} 被劫云的天雷劈成了灰烬。");
            lp.Hurt(reason, Projectile.damage, hitDir);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            if (Finale) {
                DrawFinale();
                return false;
            }

            NPC boss = Projectile.ai[0] >= 0 && Projectile.ai[0] < Main.maxNPCs ? Main.npc[(int)Projectile.ai[0]] : null;
            Vector2 origin = boss != null && boss.active ? boss.Center : _target + new Vector2(0f, -1400f);

            bool real = (int)Time >= FakeDur;
            float t = (int)Time;

            if (!_struck) {
                // —— 落点法阵 (ArenaRunic) ——
                if (real) {
                    // 真预警: 红色致命法阵, 渐强
                    float p = MathHelper.Clamp((t - FakeDur) / System.Math.Max(1, Windup), 0f, 1f);
                    DrawRunic(_target, Radius, TelegraphColors.Lethal, ThemeColor, 0.35f + 0.6f * p);
                    // 临落雷的预导电弧 (细, 红)
                    if (p > 0.6f) {
                        float flick = 0.4f + 0.6f * (float)System.Math.Abs(System.Math.Sin(t * 0.9f));
                        ACMShaders.DrawBeam(origin, _target, 5f * p,
                            TelegraphColors.Lethal, TelegraphColors.Lethal * 0.4f, flick * p,
                            flowSpeed: 2.2f, flowScale: 3f);
                    }
                }
                else {
                    // 假蓄力: 非红主题色微光 (不冒红, 避免狼来了)
                    float p = MathHelper.Clamp(t / System.Math.Max(1, FakeDur), 0f, 1f);
                    DrawRunic(_target, Radius * 0.85f, ThemeColor, ThemeColor * 0.5f, 0.25f + 0.25f * p);
                }
            }
            else {
                // —— 落雷瞬间: 粗雷柱 + 泛光 ——
                float fade = MathHelper.Clamp(1f - (t - StrikeMoment) / 10f, 0f, 1f);
                ACMShaders.DrawBeam(origin, _target, (Final ? 26f : 18f) * fade,
                    Color.White, TelegraphColors.Lethal, fade,
                    flowSpeed: 3.5f, flowScale: 2.2f, coreGlow: 1.4f);
                ACMShaders.DrawRadialBloomAt(_target, Final ? 0.22f : 0.15f, fade,
                    TelegraphColors.Lethal, rayCount: 12f);
            }
            return false;
        }

        private void DrawFinale() {
            float t = (int)Time;
            float grow = MathHelper.Clamp(t / 14f, 0f, 1f);
            float fade = MathHelper.Clamp(1f - (t - 20f) / 40f, 0f, 1f);
            float inten = System.Math.Min(grow, fade);
            if (inten <= 0.01f)
                return;

            Vector2 sky = Projectile.Center + new Vector2(0f, -1400f);
            // 金光冲天柱
            ACMShaders.DrawBeam(Projectile.Center, sky, 30f * grow,
                Color.White, TelegraphColors.Gold, inten,
                flowSpeed: 1.2f, flowScale: 1.6f, coreGlow: 1.6f);
            // 金色径向升华泛光
            ACMShaders.DrawRadialBloomAt(Projectile.Center, 0.28f * grow, inten,
                TelegraphColors.Gold, rayCount: 14f, falloff: 2.2f);
        }

        private static void DrawRunic(Vector2 worldCenter, float worldRadius, Color primary, Color secondary, float intensity) {
            if (intensity <= 0.01f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(worldCenter, worldRadius, out Vector2 uv, out float rFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(rFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(intensity, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(primary.ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(secondary.ToVector4());
            fx.Parameters["uRuneFreq"]?.SetValue(9f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);

            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.Additive);
        }
    }
}
