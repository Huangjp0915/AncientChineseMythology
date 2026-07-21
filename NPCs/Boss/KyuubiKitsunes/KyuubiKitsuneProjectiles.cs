using AncientChineseMythology.Helpers;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.KyuubiKitsunes
{
    /// <summary>
    /// 狐火弹 — 九尾狐全部远程攻击的基本单元。
    /// 设计要点 (可读性): 先缓慢漂浮蓄势(金色, 预告), 后点火加速(变色, 致命)。慢起 → 渐快 → 收口。
    /// 纯服务器生成 + 同步; 绘制纯本地。
    /// ai[0]=自计时; ai[1]=追踪强度 0~1 (0=直线) / 风车模式下为逐帧旋转弧度;
    /// ai[2]=样式: 0=狐火 1=天灯灯笼(长悬浮+着色器火芯) 2=紫色妖火(快直线) 3=曼陀罗风车(切向弧线)。
    /// </summary>
    public class KyuubiFoxFire : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/KyuubiKitsunes/MissesTop";

        private const float MaxSpeed = 13f;

        private ref float Timer => ref Projectile.ai[0];
        private float HomeStrength => Projectile.ai[1];
        private int Style => (int)Projectile.ai[2];

        /// <summary>样式对应漂浮蓄势时长 tick (慢=可读)。</summary>
        private int DriftTime => Style switch { 1 => 46, 2 => 20, 3 => 0, _ => 32 };

        // 灯笼火芯着色器 (静态缓存, 参考 Xuanwu 写法)
        private static Asset<Effect> flameRef;
        internal static Effect FlameEffect {
            get {
                if (Main.dedServ)
                    return null;
                flameRef ??= ModContent.Request<Effect>(
                    "AncientChineseMythology/Effects/KyuubiFoxFlame", AssetRequestMode.ImmediateLoad);
                return flameRef?.Value;
            }
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 360;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Timer++;

            switch (Style) {
                case 3: // 曼陀罗风车: 切向弧线匀速外旋, 无追踪 (几何可读)
                    Projectile.velocity = Projectile.velocity.RotatedBy(HomeStrength);
                    if (Projectile.timeLeft > 200)
                        Projectile.timeLeft = 200;
                    break;

                default:
                    if (Timer < DriftTime) {
                        // 漂浮蓄势: 缓慢减速到将停, 金色脉动 (慢=可读; 灯笼期可被提前打掉方位)
                        Projectile.velocity *= 0.95f;
                    }
                    else if (HomeStrength > 0f) {
                        Player target = FindTarget();
                        if (target != null) {
                            Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                            float t = MathHelper.Clamp((Timer - DriftTime) / 45f, 0f, 1f);
                            float spd = MathHelper.Lerp(2.5f, MaxSpeed, t);
                            Vector2 cur = Projectile.velocity.SafeNormalize(desired);
                            Vector2 dir = Vector2.Lerp(cur, desired, 0.05f + 0.05f * HomeStrength).SafeNormalize(desired);
                            Projectile.velocity = dir * spd;
                        }
                    }
                    else {
                        // 直线狐火: 漂浮后重新点火沿原方向 (紫色妖火更快)
                        float top = Style == 2 ? 16f : MaxSpeed;
                        float t = MathHelper.Clamp((Timer - DriftTime) / 30f, 0f, 1f);
                        Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                        Projectile.velocity = dir * MathHelper.Lerp(2.5f, top, t);
                    }
                    break;
            }

            if (Projectile.velocity != Vector2.Zero)
                Projectile.rotation = Projectile.velocity.ToRotation();

            // 光照: 漂浮期偏金, 点火期按样式变色
            float hot = Style == 3 ? 1f : MathHelper.Clamp((Timer - DriftTime) / 30f, 0f, 1f);
            Vector3 light = Style == 2
                ? new Vector3(0.8f, 0.35f, 0.9f)
                : new Vector3(1f, 0.6f - hot * 0.25f, 0.25f);
            Lighting.AddLight(Projectile.Center, light * 0.6f);

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                int dustType = Style == 2 ? DustID.PinkFairy : DustID.GoldFlame;
                Dust d = Dust.NewDustPerfect(Projectile.Center, dustType,
                    -Projectile.velocity * 0.1f, 120, default, Style == 1 ? 1.5f : 1.2f);
                d.noGravity = true;
            }
        }

        private Player FindTarget() {
            Player best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead)
                    continue;
                float sq = Vector2.DistanceSquared(p.Center, Projectile.Center);
                if (sq < bestSq) {
                    bestSq = sq;
                    best = p;
                }
            }
            return best;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            int dustType = Style == 2 ? DustID.PinkFairy : DustID.GoldFlame;
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, dustType,
                    Main.rand.NextVector2Circular(4f, 4f), 100, default, 1.6f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            // 配色: 金(惰性) → 点火变色 (样式各异, 固定色语言)
            float hot = Style == 3 ? 1f : MathHelper.Clamp((Timer - DriftTime) / 30f, 0f, 1f);
            Color outer, inner;
            switch (Style) {
                case 2: // 紫色妖火 (直线)
                    outer = Color.Lerp(new Color(150, 60, 190), new Color(190, 40, 150), hot);
                    inner = Color.Lerp(new Color(235, 170, 255), new Color(255, 150, 220), hot);
                    break;
                case 3: // 风车: 恒金橙
                    outer = new Color(220, 120, 30);
                    inner = new Color(255, 210, 120);
                    break;
                default:
                    outer = Color.Lerp(new Color(220, 150, 40), new Color(210, 70, 20), hot);
                    inner = Color.Lerp(new Color(255, 225, 140), new Color(255, 150, 70), hot);
                    break;
            }
            outer.A = 150;
            inner.A = 200;

            WeaponVFX.DrawProjectileTrail(Projectile, Style == 1 ? 20f : 16f, outer, inner,
                uvScroll: -Main.GlobalTimeWrappedHourly * 2.2f);

            // 灯笼样式: 专属狐火着色器火芯 (漂浮期最抢眼的读法元素)
            if (Style == 1)
                DrawLanternFlame(inner);
            else
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.55f + 0.12f * MathF.Sin(Timer * 0.3f), inner);

            return false;
        }

        /// <summary>灯笼火芯: KyuubiFoxFlame 着色器画一枚立焰 (单四边形, 自开合批)。</summary>
        private void DrawLanternFlame(Color inner) {
            Effect fx = FlameEffect;
            Texture2D noise = ACMShaders.NoiseTexture;
            if (fx == null || noise == null) {
                WeaponVFX.DrawGlowBurst(Projectile.Center, 0.8f, inner);
                return;
            }

            float hot = MathHelper.Clamp((Timer - DriftTime) / 30f, 0f, 1f);
            Color edge = HomeStrength > 0f
                ? Color.Lerp(new Color(255, 190, 60), new Color(235, 70, 45), hot)
                : Color.Lerp(new Color(200, 90, 220), new Color(190, 40, 150), hot);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(0.95f);
            fx.Parameters["uColorCore"]?.SetValue(new Color(255, 250, 225).ToVector4());
            fx.Parameters["uColorEdge"]?.SetValue(edge.ToVector4());
            fx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.73f % 10f);
            fx.Parameters["uTall"]?.SetValue(1.1f);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            // 火焰基点在 uv(0.5, 0.88); 灯笼恒朝上
            Vector2 origin = new(noise.Width * 0.5f, noise.Height * 0.88f);
            float bob = 1f + 0.06f * MathF.Sin(Timer * 0.15f + Projectile.whoAmI);
            sb.Draw(noise, Projectile.Center + new Vector2(0f, 26f) - Main.screenPosition, null, Color.White,
                0f, origin, new Vector2(0.30f, 0.36f) * bob, SpriteEffects.None, 0f);
            sb.End();
            ACMShaders.RestoreDefaultBatch(sb);

            // 灯笼外晕
            WeaponVFX.DrawGlowBurst(Projectile.Center, 0.9f + 0.1f * MathF.Sin(Timer * 0.2f), edge * 0.7f);
        }
    }

    /// <summary>
    /// 天狐贯刺 — 九尾贯刺攻击的**权威伤害弹幕** (V3 核心修复: 尾巴演出与伤害判定合一)。
    /// 一条从固定原点出发的直线: 红线预告 → 一瞬亮起造成伤害 → 淡出。
    /// 尾巴的 LongRangeStab 视觉与本弹幕由本体用同一份同步参数对齐。
    /// ai[0]=方向角(rad); ai[1]=预告时长 tick; ai[2]=伤害窗时长 tick。
    /// </summary>
    public class KyuubiTailLance : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/KyuubiKitsunes/MissesTop";

        /// <summary>贯刺全长 (对齐尾巴满延展长度)。</summary>
        public const float LanceLength = 1150f;
        private const float HitHalfWidth = 20f;
        private const int FadeTime = 14;

        private float Angle => Projectile.ai[0];
        private int TelegraphTime => (int)Projectile.ai[1];
        private int StrikeTime => Math.Max(6, (int)Projectile.ai[2]);

        private int timer; // 本地自计时 (生成同步后各端确定性推进)
        private bool struck;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            timer++;
            Projectile.velocity = Vector2.Zero; // 原点冻结: 扇面在施放瞬间锁定

            if (!struck && timer >= TelegraphTime) {
                struck = true;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.35f, Volume = 0.8f }, Projectile.Center);
                    Vector2 dir = Angle.ToRotationVector2();
                    for (int i = 0; i < 10; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(LanceLength),
                            DustID.GoldFlame, dir * Main.rand.NextFloat(2f, 7f), 100, default, 1.7f);
                        d.noGravity = true;
                    }
                }
            }

            if (timer >= TelegraphTime + StrikeTime + FadeTime)
                Projectile.Kill();

            Vector2 tip = Projectile.Center + Angle.ToRotationVector2() * LanceLength;
            Lighting.AddLight(tip, new Vector3(1f, 0.5f, 0.2f) * 0.4f);
        }

        public override bool? CanDamage() {
            // 伤害窗与视觉亮起严格对齐
            return timer >= TelegraphTime && timer < TelegraphTime + StrikeTime ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 end = Projectile.Center + Angle.ToRotationVector2() * LanceLength;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, end, HitHalfWidth * 2f, ref _);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Vector2 dir = Angle.ToRotationVector2();
            Vector2 end = Projectile.Center + dir * LanceLength;

            if (timer < TelegraphTime) {
                // 预告: 细红线渐显 + 末梢脉冲 (契约红 = 致命且唯一)
                float t = timer / (float)TelegraphTime;
                float pulse = 0.5f + 0.5f * MathF.Sin(timer * 0.55f);
                ACMShaders.DrawBeam(Projectile.Center, end, 2.2f + t * 2.2f,
                    TelegraphColors.Lethal, TelegraphColors.Lethal * 0.3f,
                    0.28f + t * 0.45f + pulse * 0.12f, flowSpeed: 3.2f, coreSharp: 3f);
            }
            else if (timer < TelegraphTime + StrikeTime) {
                // 亮起: 狐火金白粗光束 (伤害窗)
                float t = (timer - TelegraphTime) / (float)StrikeTime;
                float w = MathHelper.Lerp(16f, 11f, t);
                ACMShaders.DrawBeam(Projectile.Center, end, w,
                    new Color(255, 240, 200), new Color(230, 110, 30), 1f - t * 0.25f,
                    flowSpeed: 2.2f, flowScale: 2.6f, coreSharp: 1.8f);
                WeaponVFX.DrawGlowBurst(Projectile.Center, 1.1f - t * 0.4f, new Color(255, 210, 120));
            }
            else {
                // 淡出收口
                float t = (timer - TelegraphTime - StrikeTime) / (float)FadeTime;
                ACMShaders.DrawBeam(Projectile.Center, end, MathHelper.Lerp(10f, 2f, t),
                    new Color(255, 200, 140), new Color(180, 70, 20), (1f - t) * 0.6f,
                    flowSpeed: 2.2f, coreSharp: 2.2f);
            }
            return false;
        }
    }

    /// <summary>
    /// 狐火明珠 — 魅影环舞冲刺沿途布撒的定时妖火。
    /// 缓停悬浮(金, 惰性) → 末 10f 高频红闪(预警) → 点燃后锁定方向直线射出(不追踪)。
    /// ai[0]=引信 tick; ai[1]=自计时。
    /// </summary>
    public class KyuubiFoxfirePearl : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/KyuubiKitsunes/MissesTop";

        private int Fuse => Math.Max(20, (int)Projectile.ai[0]);
        private ref float Timer => ref Projectile.ai[1];
        private bool Armed => Timer >= Fuse;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 320;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Timer++;

            if (!Armed) {
                // 缓停悬浮 + 轻微上浮 (惰性期, 可绕开)
                Projectile.velocity *= 0.9f;
                Projectile.velocity.Y -= 0.01f;
            }
            else if (Timer == Fuse) {
                // 点燃: 服务器锁定朝最近玩家的直线方向并同步 (锁定后不再追踪 — 公平阀门)
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Player target = null;
                    float bestSq = float.MaxValue;
                    for (int i = 0; i < Main.maxPlayers; i++) {
                        Player p = Main.player[i];
                        if (!p.active || p.dead)
                            continue;
                        float sq = Vector2.DistanceSquared(p.Center, Projectile.Center);
                        if (sq < bestSq) { bestSq = sq; target = p; }
                    }
                    Vector2 dir = target != null
                        ? (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY)
                        : Vector2.UnitY;
                    Projectile.velocity = dir * 2f;
                    Projectile.netUpdate = true;
                }
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item20 with { Pitch = 0.4f, Volume = 0.7f }, Projectile.Center);
            }
            else {
                // 点燃后直线加速 2→11
                float t = MathHelper.Clamp((Timer - Fuse) / 30f, 0f, 1f);
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Projectile.velocity = dir * MathHelper.Lerp(2f, 11f, t);
            }

            bool blinking = !Armed && Timer > Fuse - 10;
            Vector3 light = blinking && (int)Timer % 4 < 2
                ? new Vector3(1f, 0.15f, 0.1f)
                : new Vector3(0.9f, 0.6f, 0.2f);
            Lighting.AddLight(Projectile.Center, light * 0.5f);
        }

        public override bool? CanDamage() => Armed ? null : false; // 惰性期无伤害

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame,
                    Main.rand.NextVector2Circular(3f, 3f), 100, default, 1.4f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            bool blinking = !Armed && Timer > Fuse - 10;
            bool blinkOn = blinking && (int)Timer % 4 < 2;

            Color core = Armed ? new Color(255, 160, 70) : new Color(255, 215, 130);
            if (blinkOn)
                core = TelegraphColors.Lethal; // 点燃前高频红闪 = 契约红预警

            if (Armed) {
                Color outer = new(210, 80, 20, 150);
                Color inner = new(255, 190, 100, 200);
                WeaponVFX.DrawProjectileTrail(Projectile, 13f, outer, inner,
                    uvScroll: -Main.GlobalTimeWrappedHourly * 2.4f);
            }

            // 珠体: BlankStar 星芒 + 柔光 (加性)
            Texture2D star = ACMAsset.BlankStar;
            if (star != null) {
                SpriteBatch sb = Main.spriteBatch;
                float pulse = 0.8f + 0.2f * MathF.Sin(Timer * 0.25f + Projectile.whoAmI);
                Color c = core;
                c.A = 0;
                sb.Draw(star, Projectile.Center - Main.screenPosition, null, c * 0.9f,
                    Timer * 0.02f, star.Size() * 0.5f, 0.28f * pulse, SpriteEffects.None, 0f);
            }
            WeaponVFX.DrawGlowBurst(Projectile.Center, blinkOn ? 0.7f : 0.45f, core);
            return false;
        }
    }

    /// <summary>
    /// 金风狐刃 — 金风横扫掠过时鞭尾甩出的弯月风刃。
    /// 出手 9px/f 缓弧渐加速至 15 (增速可读); 不追踪。
    /// ai[0]=逐帧弧线弯曲 (rad/f, 正负交替形成剪刀口)。
    /// </summary>
    public class KyuubiWindCrescent : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/KyuubiKitsunes/MissesTop";

        private float Curve => Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 210;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Timer++;
            // 缓弧 + 复利加速 (9 → 15)
            Projectile.velocity = Projectile.velocity.RotatedBy(Curve);
            if (Projectile.velocity.Length() < 15f)
                Projectile.velocity *= 1.018f;

            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.75f, 0.3f) * 0.45f);

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame,
                    -Projectile.velocity * 0.08f, 130, default, 1.1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            Color outer = new(200, 120, 30, 140);
            Color inner = new(255, 230, 150, 190);
            WeaponVFX.DrawProjectileTrail(Projectile, 14f, outer, inner,
                uvScroll: -Main.GlobalTimeWrappedHourly * 2.6f);

            // 弯月刃体: GlaciateWave 剑气灰度图, 沿速度方向
            Texture2D wave = ACMAsset.GlaciateWave;
            if (wave != null) {
                SpriteBatch sb = Main.spriteBatch;
                Color c = new(255, 215, 120, 0);
                float pulse = 1f + 0.08f * MathF.Sin(Timer * 0.4f);
                sb.Draw(wave, Projectile.Center - Main.screenPosition, null, c * 0.85f,
                    Projectile.rotation, wave.Size() * 0.5f, new Vector2(0.16f, 0.10f) * pulse, SpriteEffects.None, 0f);
                sb.Draw(wave, Projectile.Center - Main.screenPosition, null, new Color(255, 250, 220, 0) * 0.6f,
                    Projectile.rotation, wave.Size() * 0.5f, new Vector2(0.11f, 0.06f) * pulse, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 狐火曼陀罗的一条边墙 (P2 招牌 set-piece)。九条边围成绕玩家旋转的九边形, 缺口边为安全缝。
    /// 全部状态(中心/半径/旋转/缺口/伤害窗口)由本体 <see cref="KyuubiKitsune"/> 权威驱动, 边墙仅读取并自绘/判定。
    /// ai[0]=本体 whoAmI; ai[1]=边索引 0~8。
    /// </summary>
    public class KyuubiMandalaEdge : ModProjectile
    {
        public override string Texture => "AncientChineseMythology/NPCs/Boss/KyuubiKitsunes/MissesBody";

        private int EdgeIndex => (int)Projectile.ai[1];
        private Vector2 v1, v2;
        private bool isGap;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 5;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
        }

        private KyuubiKitsune Boss {
            get {
                int who = (int)Projectile.ai[0];
                if (who < 0 || who >= Main.maxNPCs)
                    return null;
                NPC n = Main.npc[who];
                if (!n.active || n.ModNPC is not KyuubiKitsune k || !k.InMandala)
                    return null;
                return k;
            }
        }

        public override void AI() {
            KyuubiKitsune boss = Boss;
            if (boss == null) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 5; // 由本体续命

            Vector2 center = boss.MandalaCenter;
            float radius = boss.MandalaRadius;
            float rot = boss.MandalaRotation;
            int gap = boss.MandalaGapIndex;

            float a1 = rot + MathHelper.TwoPi * EdgeIndex / 9f;
            float a2 = rot + MathHelper.TwoPi * (EdgeIndex + 1) / 9f;
            v1 = center + a1.ToRotationVector2() * radius;
            v2 = center + a2.ToRotationVector2() * radius;
            Projectile.Center = (v1 + v2) * 0.5f;

            isGap = EdgeIndex == gap;
        }

        public override bool? CanDamage() {
            KyuubiKitsune boss = Boss;
            if (boss == null || !boss.MandalaDamaging || isGap)
                return false;
            return null;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (isGap)
                return false;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(), targetHitbox.Size(), v1, v2);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;
            KyuubiKitsune boss = Boss;
            if (boss == null)
                return false;

            float alpha = boss.MandalaEdgeAlpha;
            if (alpha <= 0.01f)
                return false;

            if (!boss.MandalaDamaging) {
                // 预告窗口: 红色细线 (致命预警语言), 缺口边用安全翠玉
                Color tc = isGap ? TelegraphColors.Safe : TelegraphColors.Lethal;
                ACMShaders.DrawBeam(v1, v2, 3f + 2f * alpha, tc, tc * 0.4f, alpha * 0.9f,
                    flowSpeed: 2.4f, coreSharp: 3f);
            }
            else if (isGap) {
                // 安全缝: 翠玉指示 + 缝口柔光路标, 不挡视线
                ACMShaders.DrawBeam(v1, v2, 4f, TelegraphColors.Safe, TelegraphColors.Safe * 0.3f, alpha * 0.4f);
                WeaponVFX.DrawGlowBurst((v1 + v2) * 0.5f, 0.55f + 0.12f * MathF.Sin((float)Main.timeForVisualEffects * 0.12f),
                    TelegraphColors.Safe * (alpha * 0.5f));
            }
            else {
                // 实墙: 狐火金橙(致命) 流动光束 + 顶点狐火结点
                Color core = new(255, 200, 110);
                Color edge = new(210, 70, 20);
                ACMShaders.DrawBeam(v1, v2, 11f, core, edge, alpha,
                    flowSpeed: 1.8f, flowScale: 2.4f, coreSharp: 2.0f);

                // 顶点结点柔光 (每边只画自己的 v1, 九边合起来正好九个结点)
                Texture2D glow = ACMAsset.SoftGlow;
                if (glow != null) {
                    SpriteBatch sb = Main.spriteBatch;
                    float pulse = 0.9f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.1f + EdgeIndex);
                    Color c = new(255, 190, 90, 0);
                    sb.Draw(glow, v1 - Main.screenPosition, null, c * (alpha * 0.9f), 0f,
                        glow.Size() * 0.5f, 0.9f * pulse, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
