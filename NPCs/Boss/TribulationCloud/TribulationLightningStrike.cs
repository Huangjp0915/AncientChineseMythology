using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.TribulationCloud
{
    /// <summary>
    /// 劫雷·点雷 (黑=固定节奏/双联, 赤=佯攻/双重佯攻) + 渡劫成功金光灌顶 (纯演出) 参数化弹幕。
    ///
    /// <para><b>落雷全链 (充能→先导→死寂→轰落→余烬):</b>
    /// 充能 = 落点法阵渐强 + 电尘向心 + 云内电光脉冲 (粒子在 72% 硬切, 最后一段安静);
    /// 先导 = 细弱暗紫折线电弧探向落点 (12f, 无伤);
    /// 死寂 = 9f 万籁俱寂, 法阵骤缩 40% (爆发前的收缩);
    /// 轰落 = TribulationBolt 主雷柱 (回击沿先导通路) + 全屏白闪 + 大震屏 + 云体反冲;
    /// 余烬 = 40f 雷柱余辉衰减 + 落点电离残光 + 升烟。</para>
    ///
    /// <para><b>预警契约 (§6.1):</b> 红 = 致命预警, 只在<b>真正会落雷</b>的蓄力/落点出现 (TelegraphColors.Lethal);
    /// 赤雷的"假蓄力"用非红主题色微光, 不冒红 —— 避免"狼来了"。伤害窗口 = 轰落后 5f, 与视觉严格对齐。</para>
    ///
    /// <para>spawn 参数: ai[0]=劫云 whoAmI (雷柱起点/云体联动), ai[1]=<see cref="StrikeFlags"/> 位标志,
    /// ai[2]=打包主题色(RGB)。伤害=Projectile.damage; owner=渡劫者。绘制纯本地, 服务端零绘制。</para>
    /// </summary>
    public class TribulationLightningStrike : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        // —— 标志 ——
        private bool Feint => ((int)Projectile.ai[1] & StrikeFlags.Feint) != 0;
        private bool Final => ((int)Projectile.ai[1] & StrikeFlags.Final) != 0;
        private bool Finale => ((int)Projectile.ai[1] & StrikeFlags.SuccessFinale) != 0;
        private bool SecondOfPair => ((int)Projectile.ai[1] & StrikeFlags.SecondOfPair) != 0;
        private bool DoubleFeint => ((int)Projectile.ai[1] & StrikeFlags.DoubleFeint) != 0;

        // —— 时序 (tick) ——
        private int PairDelay => SecondOfPair ? 26 : 0;         // 双联第二记的静默延迟
        private int FakeCount => Feint ? (DoubleFeint ? 2 : 1) : 0;
        private const int FakeDur = 28;                          // 每段假蓄力
        private int Windup => Final ? 90 : (Feint ? 32 : 45);   // 真充能 (红) 时长
        private const int LeaderDur = 12;                        // 先导电弧
        private const int SilenceDur = 9;                        // 死寂
        private const int LethalWindow = 5;                      // 落点致命持续帧 (跑进去也会死, 公平)
        private const int EmberDur = 40;                         // 余烬/雷柱余辉

        private int ChargeStart => PairDelay + FakeCount * FakeDur;
        private int LeaderStart => ChargeStart + Windup;
        private int SilenceStart => LeaderStart + LeaderDur;
        private int StrikeMoment => SilenceStart + SilenceDur;

        private const int FinaleLife = 120;                      // 成功金光时长

        private float Radius => Final ? 132f : 96f;              // 落点致命半径

        private bool _struck;
        private float _strikeSeed;                               // 轰落沿用先导最后一形 (回击走先导通路)
        private float Time => Projectile.localAI[0];

        private Color ThemeColor {
            get {
                int packed = (int)Projectile.ai[2];
                if (packed <= 0)
                    return TelegraphColors.Gold;
                return new Color((packed >> 16) & 255, (packed >> 8) & 255, packed & 255);
            }
        }

        /// <summary>主雷柱辉光色 —— 主题色偏电弧青白 (轰落是兑现, 不再是预警, 可离开红)。</summary>
        private Color BoltGlow => Color.Lerp(ThemeColor, TelegraphColors.Lightning, 0.55f);

        private TribulationCloudBase Cloud {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active && Main.npc[idx].ModNPC is TribulationCloudBase tcb)
                    return tcb;
                return null;
            }
        }

        private Vector2 BoltOrigin {
            get {
                NPC boss = Cloud?.NPC;
                return boss != null ? boss.Center + new Vector2(0f, 130f) : Projectile.Center + new Vector2(0f, -1350f);
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

        public override void AI() {
            Projectile.localAI[0] += 1f;
            int t = (int)Time;

            // 首帧初始化基础种子 (在 AI 而非 OnSpawn: 远端客户端不执行 OnSpawn, identity 已同步)
            if (t == 1)
                _strikeSeed = Projectile.identity * 0.613f + 1.7f;

            if (Finale) {
                FinaleAI();
                return;
            }

            // —— 锚点跳移: 双联第二记起充时 / 每段假蓄力结束时, 真落点追到玩家新站位 ——
            // (owner 位置持续同步, 各端用各自视图跳移; 本地玩家伤害判定本地权威, 观感一致)
            bool anchorHop = SecondOfPair && t == PairDelay;
            for (int k = 1; k <= FakeCount; k++)
                anchorHop |= t == PairDelay + FakeDur * k;
            if (anchorHop) {
                Player owner = Main.player[Projectile.owner];
                if (owner.active && !owner.dead)
                    Projectile.Center = owner.Center;
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.55f, Pitch = 0.35f }, Projectile.Center);
            }

            // 假蓄力起始的低语 (非红, 骗术的声音故意轻)
            if (FakeCount > 0 && t == PairDelay + 1)
                SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with { Volume = 0.45f, Pitch = -0.2f }, Projectile.Center);

            // 真充能开始: 蓄力音 + 从此只冒红 (ChargeStart 为 0 时首帧 t=1, 取 max 保证必响)
            if (t == Math.Max(1, ChargeStart))
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = Final ? 0.85f : 0.55f, Pitch = Final ? 0.1f : 0.4f }, Projectile.Center);

            // 先导起始: 细弱电弧探向落点
            if (t == LeaderStart)
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.8f, Pitch = -0.1f }, Projectile.Center);

            // 蓄力尘 + 云内电光联动 (死寂期一切安静)
            if (t < SilenceStart)
                ChargeVisuals(t);

            // 先导期: 每 3f 换形; 死寂前锁定最终形 (回击将沿此通路)
            if (t >= LeaderStart && t < SilenceStart)
                _strikeSeed = Projectile.identity * 0.613f + 1.7f + (t / 3) * 2.37f;

            // —— 轰落瞬间 ——
            if (!_struck && t >= StrikeMoment) {
                _struck = true;
                Strike();
            }

            // 落点致命窗口: 跑进落点同样致命
            if (_struck && t <= StrikeMoment + LethalWindow)
                ApplyZoneDamage();

            // 余烬期: 电离残光 + 升烟 + 云腹余光渐熄
            if (_struck && !Main.dedServ && t < StrikeMoment + EmberDur) {
                float emberP = 1f - (t - StrikeMoment) / (float)EmberDur;
                Cloud?.PublishFlash(Projectile.Center.X, 0.45f * emberP);
                if (Main.rand.NextFloat() < 0.35f * emberP) {
                    Dust spark = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.5f, 12f),
                        DustID.Electric, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 2f)), 60, BoltGlow, 1.1f * emberP + 0.3f);
                    spark.noGravity = true;
                }
                if (Main.rand.NextFloat() < 0.4f * emberP) {
                    Dust smoke = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.4f, 8f),
                        DustID.Smoke, new Vector2(0f, -Main.rand.NextFloat(1f, 2.6f)), 140, Color.DarkGray, 1.6f);
                    smoke.noGravity = true;
                }
            }

            if (t >= StrikeMoment + EmberDur + 4)
                Projectile.Kill();
        }

        /// <summary>蓄力期视觉: 假段=主题色微尘; 真段=红色致命尘向心 + 云内电光脉冲; 72% 后粒子硬切。</summary>
        private void ChargeVisuals(int t) {
            if (Main.dedServ)
                return;

            bool real = t >= ChargeStart;
            bool leader = t >= LeaderStart;

            // 云内电光: 随充能上升, 先导期定格高位
            float chargeP = real ? MathHelper.Clamp((t - ChargeStart) / (float)Math.Max(1, Windup), 0f, 1f) : 0f;
            Cloud?.PublishFlash(Projectile.Center.X, leader ? 0.55f : chargeP * chargeP * 0.5f + (real ? 0.08f : 0f));

            if (t < PairDelay)
                return; // 双联第二记的静默延迟: 完全无声无光

            if (leader)
                return; // 先导期不再冒尘 (视线让给电弧)

            // 密度: 充能前段渐增, 72% 硬切 (尖叫前的吸气)
            float density = real
                ? (chargeP < 0.72f ? 0.55f * (0.3f + chargeP) * (Final ? 1.3f : 1f) : 0f)
                : 0.3f;
            if (Main.rand.NextFloat() >= density)
                return;

            Color c = real ? TelegraphColors.Lethal : ThemeColor;
            int dustType = real ? DustID.RedTorch : DustID.Electric;
            // 电尘向心汇聚 (converging streaks): 从外圈拉向落点
            Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(Radius * 1.6f, Radius * 0.7f);
            Vector2 vel = (Projectile.Center - from) * 0.06f + new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f));
            Dust d = Dust.NewDustPerfect(from, dustType, vel, 80, c, real ? 1.35f : 0.95f);
            d.noGravity = true;
        }

        private void Strike() {
            // 近雷炸响 (双层) + 全屏白闪 + 大震屏 + 云体反冲
            SoundEngine.PlaySound(SoundID.Thunder with { Volume = Final ? 1.5f : 1.15f, Pitch = Final ? -0.45f : -0.1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = Final ? 1.2f : 0.9f, Pitch = Final ? -0.3f : 0.1f }, Projectile.Center);
            ACMScreenShakeSystem.Add(Final ? 14f : 9f);
            TribulationScreenSystem.Flash(Final ? 0.55f : 0.32f);
            Cloud?.PublishFlash(Projectile.Center.X, 1f);
            Cloud?.PublishRecoil(Final ? 34f : 22f);

            if (Main.dedServ)
                return;

            // 命中爆尘: 电弧飞溅 + 白热碎光 + 烟
            int burst = Final ? 30 : 20;
            for (int i = 0; i < burst; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(11f, 7f) - new Vector2(0f, Main.rand.NextFloat(4f));
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, vel, 40, BoltGlow, 1.7f);
                d.noGravity = true;
            }
            for (int i = 0; i < burst / 2; i++) {
                Dust s = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.5f, 10f),
                    DustID.Smoke, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(2f, 5f)), 120, Color.DimGray, 2f);
                s.noGravity = true;
            }
        }

        private void ApplyZoneDamage() {
            if (Main.dedServ)
                return;
            Player lp = Main.LocalPlayer;
            if (!lp.active || lp.dead || lp.immune)
                return;
            if (Vector2.Distance(lp.Center, Projectile.Center) > Radius)
                return;

            int hitDir = lp.Center.X > Projectile.Center.X ? 1 : -1;
            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                Terraria.Localization.NetworkText.FromLiteral($"{lp.name} 被劫云的天雷劈成了灰烬。"));
            lp.Hurt(reason, Projectile.damage, hitDir);
        }

        // ============================================================
        //  成功金光灌顶 (纯演出)
        // ============================================================

        private void FinaleAI() {
            int t = (int)Time;
            // 天光跟随渡劫者 (洗礼落在人身上)
            Player owner = Main.player[Projectile.owner];
            if (owner.active && !owner.dead)
                Projectile.Center = Vector2.Lerp(Projectile.Center, owner.Center, 0.35f);

            if (t == 1)
                SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with { Volume = 1.2f, Pitch = -0.35f }, Projectile.Center);

            Cloud?.PublishFlash(Projectile.Center.X, 0.5f * MathHelper.Clamp(1f - t / (float)FinaleLife, 0f, 1f));

            if (!Main.dedServ && t % 2 == 0 && t < FinaleLife - 30) {
                Vector2 v = new Vector2(0, -Main.rand.NextFloat(4f, 12f)).RotatedByRandom(0.35f);
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(46f, 22f),
                    DustID.GoldFlame, v, 60, TelegraphColors.Gold, 1.7f);
                d.noGravity = true;
            }
            if (t >= FinaleLife)
                Projectile.Kill();
        }

        // ============================================================
        //  绘制
        // ============================================================

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            if (Finale) {
                DrawFinale();
                return false;
            }

            int t = (int)Time;
            Vector2 origin = BoltOrigin;

            if (!_struck) {
                if (t < PairDelay)
                    return false; // 双联第二记的静默延迟: 不显形

                bool real = t >= ChargeStart;
                bool leader = t >= LeaderStart;
                bool silence = t >= SilenceStart;

                if (silence) {
                    // —— 死寂: 法阵骤缩 40% + 余弦颤闪, 其余全部熄灭 ——
                    float flick = 0.4f + 0.07f * MathF.Cos(t * 1.9f);
                    DrawRunic(Projectile.Center, Radius * flick, TelegraphColors.Lethal, ThemeColor, 0.85f);
                }
                else if (leader) {
                    // —— 先导: 细弱暗紫折线电弧探向落点 (无伤, 最后通牒) ——
                    float lp = MathHelper.Clamp((t - LeaderStart) / (float)LeaderDur, 0f, 1f);
                    Vector2 tip = Vector2.Lerp(origin, Projectile.Center, ACMUtils.QuadOut(MathF.Min(1f, lp * 1.6f)));
                    TribulationFX.DrawBolt(origin, tip, 190f,
                        Color.Lerp(ThemeColor, TelegraphColors.NetherViolet, 0.5f), 0.55f,
                        _strikeSeed, 0f, widthScale: 0.35f, branch: 0f, flicker: 0.85f);
                    DrawRunic(Projectile.Center, Radius, TelegraphColors.Lethal, ThemeColor, 0.95f);
                }
                else if (real) {
                    // —— 真充能: 红色致命法阵渐强 ——
                    float p = MathHelper.Clamp((t - ChargeStart) / (float)Math.Max(1, Windup), 0f, 1f);
                    DrawRunic(Projectile.Center, Radius, TelegraphColors.Lethal, ThemeColor, 0.35f + 0.6f * p);
                }
                else {
                    // —— 假蓄力: 非红主题色微光 (不冒红, 避免狼来了) ——
                    int fakeT = t - PairDelay;
                    float p = MathHelper.Clamp(fakeT % FakeDur / (float)FakeDur, 0f, 1f);
                    DrawRunic(Projectile.Center, Radius * 0.85f, ThemeColor, ThemeColor * 0.5f, 0.25f + 0.3f * p);
                }
            }
            else {
                // —— 轰落 + 余烬: 主雷柱 (回击沿先导通路) + 落点泛光 ——
                float life = MathHelper.Clamp((t - StrikeMoment) / (float)EmberDur, 0f, 1f);
                TribulationFX.DrawBolt(origin, Projectile.Center, Final ? 400f : 320f,
                    BoltGlow, 1f - life * 0.25f, _strikeSeed, life,
                    widthScale: Final ? 1.25f : 1f, branch: 1f, flicker: 0f);

                float bloomFade = MathHelper.Clamp(1f - (t - StrikeMoment) / 12f, 0f, 1f);
                if (bloomFade > 0.01f)
                    ACMShaders.DrawRadialBloomAt(Projectile.Center, Final ? 0.24f : 0.16f, bloomFade,
                        Color.Lerp(BoltGlow, Color.White, 0.4f), rayCount: 12f);

                // 焦土余辉法阵 (慢慢冷却)
                DrawRunic(Projectile.Center, Radius, BoltGlow, ThemeColor, 0.5f * (1f - life));
            }
            return false;
        }

        private void DrawFinale() {
            float t = (int)Time;
            float grow = ACMUtils.QuadOut(MathHelper.Clamp(t / 20f, 0f, 1f));
            float fade = MathHelper.Clamp(1f - (t - (FinaleLife - 45f)) / 45f, 0f, 1f);
            float inten = MathF.Min(grow, fade);
            if (inten <= 0.01f)
                return;

            Vector2 sky = Projectile.Center + new Vector2(0f, -1350f);
            NPC boss = Cloud?.NPC;
            if (boss != null)
                sky = boss.Center;

            // 金光冲天柱 (从云盖裂口灌落) + 内芯
            ACMShaders.DrawBeam(sky, Projectile.Center + new Vector2(0f, 24f), 46f * grow,
                Color.White, TelegraphColors.Gold, inten, flowSpeed: 1.1f, flowScale: 1.5f, coreGlow: 1.6f);
            ACMShaders.DrawBeam(sky, Projectile.Center + new Vector2(0f, 24f), 16f * grow,
                Color.White, TelegraphColors.Holy, inten, flowSpeed: 0.7f, flowScale: 1f, coreGlow: 2f);
            // 金色径向升华泛光
            ACMShaders.DrawRadialBloomAt(Projectile.Center, 0.3f * grow, inten,
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
