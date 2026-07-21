using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.BAWImpermanences
{
    /// <summary>
    /// 白无常幽魂 (V3)。
    /// ai[0]=模式: 0 扇潮(弱追踪) / 1 休眠追(45f 显形后才缓追) / 2 阴阳潮(直线, 安全缝前熄灭) / 3 灯矢(直线快弹)。
    /// ai[2]=所属 NPC (孤使白 → 黑蕊白魂配色)。伤害窗口与显形严格对齐。
    /// </summary>
    public class GhostProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private float pulsePhase;
        private float wobblePhase;
        private float ghostAlpha;
        private float timer;

        private int Mode => (int)Projectile.ai[0];
        private NPC Owner => Projectile.ai[2] >= 0 && Projectile.ai[2] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[2]] : null;

        private const float DormantTime = 45f;
        private bool Dormant => Mode == 1 && timer < DormantTime;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 280;
        }

        public override void AI() {
            timer++;
            pulsePhase += 0.12f;
            wobblePhase += 0.08f;
            Projectile.rotation += MathF.Sin(wobblePhase) * 0.05f + 0.02f;

            switch (Mode) {
                case 0: {
                    // 扇潮: 弱追踪 (0.02 恒可甩) + 波浪漂移
                    ghostAlpha = MathHelper.Lerp(ghostAlpha, 1f, 0.08f);
                    Player t = FindNearest(760f);
                    if (t != null) {
                        Vector2 to = (t.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                        float speed = MathF.Max(6.5f, Projectile.velocity.Length());
                        Projectile.velocity = Vector2.Lerp(Projectile.velocity, to * speed, 0.02f);
                    }
                    ApplyWobble(2f);
                    break;
                }
                case 1: {
                    // 休眠: 原地显形 (无伤害) → 45f 后开始缓追 (7px/f 可甩)
                    if (Dormant) {
                        ghostAlpha = MathHelper.Lerp(ghostAlpha, 0.4f, 0.06f);
                        Projectile.velocity *= 0.85f;
                        if (timer == DormantTime - 6f)
                            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);
                    }
                    else {
                        ghostAlpha = MathHelper.Lerp(ghostAlpha, 1f, 0.1f);
                        Player t = FindNearest(1100f);
                        if (t != null) {
                            Vector2 to = (t.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                            Projectile.velocity = Vector2.Lerp(Projectile.velocity, to * 7f, 0.04f);
                        }
                        ApplyWobble(1.4f);
                    }
                    break;
                }
                case 2: {
                    // 阴阳潮: 直线缓推, 靠近安全缝 150px 即熄灭 (公平走廊)
                    ghostAlpha = MathHelper.Lerp(ghostAlpha, 1f, 0.08f);
                    ApplyWobble(1.6f);
                    if (timer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        NPC black = FindBoss(ModContent.NPCType<BlackImpermanence>());
                        NPC white = FindBoss(ModContent.NPCType<WhiteImpermanence>());
                        if (black != null && white != null) {
                            Vector2 mid = (black.Center + white.Center) * 0.5f;
                            Vector2 whiteNormal = (white.Center - black.Center).SafeNormalize(Vector2.UnitX);
                            if (Vector2.Dot(Projectile.Center - mid, whiteNormal) < 150f)
                                Projectile.Kill();
                        }
                    }
                    break;
                }
                case 3: {
                    // 灯矢: 直线快弹
                    ghostAlpha = MathHelper.Lerp(ghostAlpha, 1f, 0.2f);
                    if (Projectile.timeLeft > 110)
                        Projectile.timeLeft = 110;
                    break;
                }
            }

            // 体表魂点
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f), DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.9f * ghostAlpha;
                d.velocity = -Projectile.velocity * 0.12f;
                d.alpha = 110;
            }

            float lightPulse = 0.35f + MathF.Sin(pulsePhase) * 0.12f;
            Lighting.AddLight(Projectile.Center, new Color(180, 180, 255).ToVector3() * lightPulse * ghostAlpha);
        }

        private void ApplyWobble(float amp) {
            Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            Projectile.position += perp * MathF.Sin(wobblePhase * 1.5f + Projectile.whoAmI * 0.5f) * amp;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Dormant ? false : null; // 休眠显形期无伤害
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<BAWPlayer>().ApplyYinQiCorrosion(120);
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = 0.3f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = BAWHelper.DustTexture;
            if (tex == null)
                return false;

            Vector2 origin = tex.Size() / 2f;
            GetPalette(out Color core, out Color glow);

            // 幽灵拖尾 (双层渐变)
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = glow * (progress * 0.35f * ghostAlpha);
                trailColor.A = 0;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                drawPos.Y += MathF.Sin(wobblePhase + i * 0.3f) * 3f;
                sb.Draw(tex, drawPos, null, trailColor, Projectile.oldRot[i], origin, (0.4f + progress * 0.8f), SpriteEffects.None, 0);
            }

            // 主体幽魂光球
            BAWHelper.DrawGhostOrb(sb, Projectile.Center, core * ghostAlpha, glow, Mode == 3 ? 0.9f : 1.15f, pulsePhase);

            // 幽灵"眼睛"
            float eyeOffset = MathF.Sin(pulsePhase * 2f) * 2f;
            Color eyeColor = (Mode == 1 && Dormant ? Color.Red * 0.7f : Color.White * 0.8f) * ghostAlpha;
            eyeColor.A = 0;
            sb.Draw(tex, Projectile.Center - Main.screenPosition + new Vector2(-4, eyeOffset - 3), null, eyeColor, 0f, origin, 0.28f, SpriteEffects.None, 0);
            sb.Draw(tex, Projectile.Center - Main.screenPosition + new Vector2(4, eyeOffset - 3), null, eyeColor, 0f, origin, 0.28f, SpriteEffects.None, 0);

            return false;
        }

        /// <summary>配色: 常态青白; 孤使白 → 黑蕊白魂。</summary>
        private void GetPalette(out Color core, out Color glow) {
            if (Owner?.ModNPC is BAWImpermanenceBase b && b.Unleashed) {
                core = new Color(60, 45, 90);
                glow = new Color(230, 235, 255);
            }
            else {
                core = new Color(200, 220, 255);
                glow = new Color(120, 180, 255);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.5f, Volume = 0.6f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = Main.rand.NextVector2Circular(5f, 5f);
                d.alpha = 60;
            }
        }

        private Player FindNearest(float maxDist) {
            Player best = null;
            float bestDist = maxDist;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (p != null && p.active && !p.dead) {
                    float dist = p.Distance(Projectile.Center);
                    if (dist < bestDist) { bestDist = dist; best = p; }
                }
            }
            return best;
        }

        private static NPC FindBoss(int type) {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n != null && n.active && n.type == type)
                    return n;
            }
            return null;
        }
    }

    /// <summary>
    /// 引魂灯 (V3)。
    /// ai[0]=模式: 0 灯扑(抛落→60f 充能→环波+径向幽魂) / 1 阵灯(错峰显形→锁线点射) / 2 摄魂帷(锚定结界)。
    /// ai[1]=阵位索引; ai[2]=所属 NPC。灯体本身无接触伤害 (伤害全部来自显形后的弹幕/结界减速)。
    /// </summary>
    public class SpiritCircleProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private float timer;
        private float pulsePhase;
        private float veilRadius;
        private Vector2 lockedAim;
        private bool aimLocked;

        private int Mode => (int)Projectile.ai[0];
        private int Index => (int)Projectile.ai[1];
        private NPC Owner => Projectile.ai[2] >= 0 && Projectile.ai[2] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[2]] : null;

        // mode1 阵灯时序
        private float AppearTime => Index * 6f;
        private float FireTime => 100f + Index * 10f;

        // mode0 灯扑时序
        private const float ChargeTime = 60f;
        private float landTime = -1f;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 400;
            Projectile.netImportant = true;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => false; // 灯体无接触伤害

        public override void AI() {
            timer++;
            pulsePhase += 0.11f;

            switch (Mode) {
                case 0: AI_Pounce(); break;
                case 1: AI_Array(); break;
                case 2: AI_Veil(); break;
            }
        }

        /// <summary>mode0 灯扑: 抛物飞行 → 悬停充能 (收缩环读条) → 爆: 环波 + 5 径向幽魂。</summary>
        private void AI_Pounce() {
            if (landTime < 0f) {
                Projectile.velocity.Y += 0.3f;
                Projectile.velocity *= 0.985f;
                Projectile.rotation = Projectile.velocity.X * 0.04f;
                if (timer >= 40f || Projectile.velocity.Length() < 3f) {
                    landTime = timer;
                    Projectile.velocity = Vector2.Zero;
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.2f }, Projectile.Center);
                }
            }
            else {
                float charge = (timer - landTime) / ChargeTime;
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation = MathHelper.Lerp(Projectile.rotation, 0f, 0.1f);

                // 收缩环读条 (密度∝charge, 末段安静)
                if (!Main.dedServ && charge < 0.75f && timer % 2 == 0) {
                    float r = MathHelper.Lerp(180f, 30f, charge);
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    var d = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * r, DustID.SpectreStaff);
                    d.noGravity = true;
                    d.scale = 1.2f;
                    d.velocity = -ang.ToRotationVector2() * 2.5f;
                }

                if (charge >= 1f) {
                    Burst();
                    Projectile.Kill();
                }
            }

            Lighting.AddLight(Projectile.Center, new Color(255, 240, 200).ToVector3() * 0.5f);
        }

        private void Burst() {
            SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.2f, Volume = 1f }, Projectile.Center);
            ACMScreenShakeSystem.Add(4f);
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // 幽魂环波 (4.5px/f 可跑赢, 灯扑版无缺口但慢)
            var ring = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<GhostWaveProjectile>(), Projectile.damage, 0f, -1, 2f, 0f, 4.5f);
            ring.netUpdate = true;

            // 5 发径向幽魂 (72° 均布 = 恒有缝)
            for (int i = 0; i < 5; i++) {
                float ang = MathHelper.TwoPi / 5f * i + 0.3f;
                var p = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center,
                    ang.ToRotationVector2() * 6f, ModContent.ProjectileType<GhostProjectile>(),
                    (int)(Projectile.damage * 0.9f), 0f, -1, 0f, 0f, Projectile.ai[2]);
                p.netUpdate = true;
            }
        }

        /// <summary>mode1 阵灯: 错峰显形 → 8f 锁线 (矢向锁定不追预判) → 点射 → 熄灭。</summary>
        private void AI_Array() {
            Projectile.velocity = Vector2.Zero;

            // 锁线: 射前 8f 锁定玩家当时位置
            if (!aimLocked && timer >= FireTime - 8f) {
                aimLocked = true;
                Player t = FindNearestPlayer();
                lockedAim = t != null ? t.Center : Projectile.Center + Vector2.UnitY * 100f;
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.4f, Volume = 0.6f }, Projectile.Center);
            }

            if (timer == FireTime) {
                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = 0.2f, Volume = 0.8f }, Projectile.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 dir = (lockedAim - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    var p = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center,
                        dir * 17f, ModContent.ProjectileType<GhostProjectile>(),
                        Projectile.damage, 0f, -1, 3f, 0f, Projectile.ai[2]);
                    p.netUpdate = true;
                }
            }

            if (timer > FireTime + 24f)
                Projectile.Kill();

            float vis = Visibility();
            Lighting.AddLight(Projectile.Center, new Color(255, 240, 200).ToVector3() * 0.45f * vis);
        }

        /// <summary>mode2 摄魂帷: 锚定结界 40f 展开 → 驻留 300f (10% 减速 + 周期魂蚀) → 26f 收拢。</summary>
        private void AI_Veil() {
            Projectile.velocity = Vector2.Zero;
            if (timer == 1f)
                Projectile.timeLeft = 366;

            veilRadius = timer < 40f
                ? MathHelper.Lerp(0f, 420f, timer / 40f)
                : Projectile.timeLeft < 26 ? veilRadius * 0.92f : 420f;

            if (veilRadius > 60f) {
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player p = Main.player[i];
                    if (p == null || !p.active || p.dead || p.Distance(Projectile.Center) > veilRadius)
                        continue;
                    var mp = p.GetModPlayer<BAWPlayer>();
                    mp.ApplyYinQiCorrosion(10); // 10% 减速 (出域即解)
                    if (timer % 60 == 0)
                        UnderworldField.AddSoulErosion(p, 1);
                }
            }

            // 边界游光 (稀疏, 结界主体由 ArenaRunic 绘制)
            if (!Main.dedServ && veilRadius > 60f && Main.rand.NextBool(4)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                var d = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * veilRadius, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 0.9f;
                d.velocity = (ang + MathHelper.PiOver2).ToRotationVector2() * 1.4f;
            }

            Lighting.AddLight(Projectile.Center, new Color(160, 170, 230).ToVector3() * 0.4f);
        }

        private float Visibility() {
            if (Mode != 1)
                return 1f;
            if (timer < AppearTime)
                return 0f;
            return MathHelper.Clamp((timer - AppearTime) / 50f, 0f, 1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;

            if (Mode == 2) {
                DrawVeil(sb);
                return false;
            }

            float vis = Visibility();
            if (vis <= 0.01f)
                return false;

            // 灯焰 (程序魂火): 充能/点射前更旺
            float flameBoost = 0.55f;
            if (Mode == 0 && landTime >= 0f)
                flameBoost = 0.55f + 0.45f * MathHelper.Clamp((timer - landTime) / ChargeTime, 0f, 1f);
            if (Mode == 1 && aimLocked)
                flameBoost = 1f;

            BAWFX.DrawSoulFlame(sb, Projectile.Center - new Vector2(0f, 10f), new Vector2(64f, 92f),
                new Color(255, 244, 210), new Color(255, 210, 140), Projectile.whoAmI * 0.31f, flameBoost * vis, 0f, 1.25f);

            // 灯体 (CPU 光层)
            var tex = BAWHelper.DustTexture;
            if (tex != null) {
                Vector2 origin = tex.Size() / 2f;
                float pulse = 1f + MathF.Sin(pulsePhase) * 0.12f;
                Color shell = new Color(255, 240, 210, 0) * (0.7f * vis);
                sb.Draw(tex, Projectile.Center - Main.screenPosition, null, shell, 0f, origin, 1.1f * pulse, SpriteEffects.None, 0);
                Color coreC = new Color(255, 255, 240, 0) * (0.9f * vis);
                sb.Draw(tex, Projectile.Center - Main.screenPosition, null, coreC, 0f, origin, 0.45f * pulse, SpriteEffects.None, 0);
            }

            // mode1 锁线预警
            if (Mode == 1 && aimLocked && timer < FireTime) {
                float k = Utils.GetLerpValue(FireTime - 8f, FireTime, timer, true);
                Color c = Color.Lerp(BAWFX.YangColor, TelegraphColors.Lethal, k);
                Vector2 dir = (lockedAim - Projectile.Center).SafeNormalize(Vector2.UnitY);
                ACMShaders.DrawBeam(Projectile.Center, Projectile.Center + dir * 1300f, 6f + k * 5f, c, c * 0.4f, 0.3f + k * 0.5f);
            }

            return false;
        }

        /// <summary>摄魂帷: ArenaRunic 屏幕空间结界 (法阵模式, 幽蓝紫)。</summary>
        private void DrawVeil(SpriteBatch sb) {
            if (veilRadius < 8f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(Projectile.Center, veilRadius, out Vector2 uvCenter, out float radiusFrac, out float aspect);
            float intensity = MathHelper.Clamp(veilRadius / 420f, 0f, 1f) * 0.85f;

            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uvCenter);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uIntensity"]?.SetValue(intensity);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.NetherViolet.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(BAWFX.YangColor.ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(11f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);
            ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.Additive);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = 0.2f, Volume = 0.6f }, Projectile.Center);
            for (int i = 0; i < 12; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.2f;
                d.velocity = Main.rand.NextVector2Circular(5f, 5f);
            }
        }

        private Player FindNearestPlayer() {
            Player best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (p != null && p.active && !p.dead) {
                    float dist = p.Distance(Projectile.Center);
                    if (dist < bestDist) { bestDist = dist; best = p; }
                }
            }
            return best;
        }
    }

    /// <summary>
    /// 幽魂环波 (V3): 恒速扩散的环带判定。
    /// ai[0]=变体: 0 阴(幽紫)/1 阳(暖白) 带双对称缺口, 2 灯扑白环无缺口 (更慢可跑赢)。
    /// ai[1]=缺口中心角 (缓慢旋转, 缺口半宽 24°); ai[2]=扩散速度 (0=默认 4.5)。
    /// </summary>
    public class GhostWaveProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private float radius;
        private float pulsePhase;

        private int Variant => (int)Projectile.ai[0];
        private bool HasGap => Variant != 2;
        private float GapHalf => 0.42f; // ~24°
        private float Speed => Projectile.ai[2] > 0.5f ? Projectile.ai[2] : 4.5f;
        private float MaxRadius => Variant == 2 ? 480f : 560f;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 200;
            Projectile.netImportant = true;
        }

        public override void AI() {
            pulsePhase += 0.14f;
            radius += Speed;
            Projectile.velocity = Vector2.Zero;

            // 缺口缓旋 (可读的移动安全窗)
            if (HasGap)
                Projectile.ai[1] += 0.006f;

            if (radius >= MaxRadius)
                Projectile.Kill();

            Lighting.AddLight(Projectile.Center, (Variant == 0 ? BAWFX.YinColor : BAWFX.YangColor).ToVector3() * 0.35f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (radius < 24f)
                return false;

            Vector2 c = Projectile.Center;
            Vector2 p = targetHitbox.Center.ToVector2();
            float dist = Vector2.Distance(c, p);
            if (MathF.Abs(dist - radius) > 15f + targetHitbox.Width * 0.5f)
                return false;

            if (HasGap) {
                float ang = (p - c).ToRotation();
                float d1 = MathF.Abs(MathHelper.WrapAngle(ang - Projectile.ai[1]));
                float d2 = MathF.Abs(MathHelper.WrapAngle(ang - Projectile.ai[1] - MathHelper.Pi));
                if (MathF.Min(d1, d2) < GapHalf)
                    return false; // 缺口内安全
            }
            return true;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.GetModPlayer<BAWPlayer>().ApplyYinQiCorrosion(150);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            var tex = BAWHelper.DustTexture;
            if (tex == null || radius < 4f)
                return false;

            Vector2 origin = tex.Size() / 2f;
            Color body = Variant switch {
                0 => BAWFX.YinColor,
                1 => BAWFX.YangColor,
                _ => new Color(220, 230, 255)
            };
            body.A = 0;
            float fade = MathHelper.Clamp((MaxRadius - radius) / 120f, 0f, 1f);

            const int segs = 44;
            for (int i = 0; i < segs; i++) {
                float ang = MathHelper.TwoPi / segs * i + pulsePhase * 0.05f;
                if (HasGap) {
                    float d1 = MathF.Abs(MathHelper.WrapAngle(ang - Projectile.ai[1]));
                    float d2 = MathF.Abs(MathHelper.WrapAngle(ang - Projectile.ai[1] - MathHelper.Pi));
                    if (MathF.Min(d1, d2) < GapHalf)
                        continue; // 缺口不绘制 = 视觉与判定一致
                }
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * radius;
                float pulse = 0.75f + MathF.Sin(pulsePhase + i * 0.6f) * 0.25f;
                sb.Draw(tex, pos - Main.screenPosition, null, body * (0.5f * fade * pulse), ang, origin, 0.85f, SpriteEffects.None, 0);
                sb.Draw(tex, pos - Main.screenPosition, null, Color.White with { A = 0 } * (0.22f * fade * pulse), ang, origin, 0.4f, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            for (int i = 0; i < 16; i++) {
                float ang = MathHelper.TwoPi / 16f * i;
                var d = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * radius,
                    Variant == 0 ? DustID.Shadowflame : DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.1f;
                d.velocity = ang.ToRotationVector2() * 2f;
            }
        }
    }

    /// <summary>
    /// 汲魂链 (V3): 缓速魂梭追踪 → 命中挂链 (叠魂蚀 + 白使回血)。
    /// **挣断机制**: 与白使拉开 >640px 即断 (播报"挣断!"), 240f 超时自断 —— 反制即玩法。
    /// ai[0]=所属 NPC; ai[1]=被挂玩家 whoAmI+1 (0=未挂)。不再直接 Hurt 扣血 (多人安全)。
    /// </summary>
    public class SoulDrainProjectile : ModProjectile
    {
        public override string Texture => BAWHelper.Path + "BAWDust";

        private float latchTimer;
        private float pulsePhase;
        private float beamAlpha;

        private NPC Owner => Projectile.ai[0] >= 0 && Projectile.ai[0] < Main.npc.Length
            ? Main.npc[(int)Projectile.ai[0]] : null;
        private Player Latched => Projectile.ai[1] >= 1f && Projectile.ai[1] <= Main.maxPlayers
            ? Main.player[(int)Projectile.ai[1] - 1] : null;

        private const float BreakDistance = 640f;
        private const float MaxLatch = 240f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 480;
            Projectile.netImportant = true;
        }

        public override void AI() {
            pulsePhase += 0.1f;
            NPC owner = Owner;
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }

            Player latched = Latched;
            if (latched == null) {
                // 追踪 (0.045 温和转向, 可甩)
                Player t = FindNearest(720f);
                if (t != null) {
                    Vector2 to = (t.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, to * 12f, 0.045f);
                }
                Projectile.rotation = Projectile.velocity.ToRotation();
                beamAlpha = MathHelper.Lerp(beamAlpha, 0f, 0.1f);

                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f), DustID.SpectreStaff);
                    d.noGravity = true;
                    d.scale = 1f;
                    d.velocity = -Projectile.velocity * 0.18f;
                }
                // 空放超时
                if (Projectile.timeLeft < 240)
                    Projectile.Kill();
            }
            else if (latched.active && !latched.dead) {
                latchTimer++;
                beamAlpha = MathHelper.Lerp(beamAlpha, 1f, 0.06f);
                Projectile.Center = latched.Center;
                Projectile.velocity = Vector2.Zero;

                // 汲魂: 叠魂蚀 (身份层 DoT) + 白使回血 —— 不直接扣血 (多人安全)
                if (latchTimer % 45 == 0)
                    UnderworldField.AddSoulErosion(latched, 1);
                if (latchTimer % 30 == 0) {
                    if (Main.netMode != NetmodeID.MultiplayerClient && owner.life < owner.lifeMax) {
                        owner.life = Math.Min(owner.lifeMax, owner.life + 30);
                        owner.HealEffect(30);
                    }
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f, Volume = 0.5f }, Projectile.Center);
                }

                // 汲取流粒子: 玩家 → 白使
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 pos = Vector2.Lerp(latched.Center, owner.Center, Main.rand.NextFloat());
                    var d = Dust.NewDustPerfect(pos, DustID.SpectreStaff);
                    d.noGravity = true;
                    d.scale = 1.1f;
                    d.velocity = (owner.Center - latched.Center).SafeNormalize(Vector2.Zero) * 7f;
                }

                // 挣断 / 超时
                if (latched.Distance(owner.Center) > BreakDistance || latchTimer > MaxLatch) {
                    if (latched.whoAmI == Main.myPlayer && latchTimer <= MaxLatch) {
                        string text = Language.GetTextValue("Mods.AncientChineseMythology.NPCs.WhiteImpermanence.TetherBreak");
                        CombatText.NewText(latched.Hitbox, TelegraphColors.Safe, text, true);
                    }
                    SoundEngine.PlaySound(SoundID.Item20 with { Pitch = 0.6f, Volume = 0.9f }, Projectile.Center);
                    Projectile.Kill();
                }
            }
            else {
                Projectile.Kill();
            }

            Lighting.AddLight(Projectile.Center, new Color(180, 150, 230).ToVector3() * (0.35f + beamAlpha * 0.25f));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Latched != null ? false : null; // 挂链后不再重复判伤
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Projectile.ai[1] < 1f) {
                Projectile.ai[1] = target.whoAmI + 1;
                Projectile.netUpdate = true;
                target.GetModPlayer<BAWPlayer>().ApplyYinQiCorrosion(90);
                SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = -0.2f, Volume = 1.1f }, Projectile.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            NPC owner = Owner;

            // 汲魂链光束 (白使 ↔ 玩家)
            if (owner != null && beamAlpha > 0.05f) {
                Color core = Color.Lerp(new Color(220, 200, 255), Color.White, MathF.Sin(pulsePhase) * 0.5f + 0.5f);
                ACMShaders.DrawBeam(Projectile.Center, owner.Center, 9f * beamAlpha, core, BAWFX.YangColor * 0.5f, beamAlpha * 0.8f);
            }

            // 魂梭本体
            BAWHelper.DrawGhostOrb(sb, Projectile.Center,
                new Color(210, 190, 255), new Color(160, 120, 230), Latched != null ? 1.3f : 1.6f, pulsePhase);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.3f }, Projectile.Center);
            for (int i = 0; i < 14; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.SpectreStaff);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = Main.rand.NextVector2Circular(7f, 7f);
            }
        }

        private Player FindNearest(float maxDist) {
            Player best = null;
            float bestDist = maxDist;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (p != null && p.active && !p.dead) {
                    float dist = p.Distance(Projectile.Center);
                    if (dist < bestDist) { bestDist = dist; best = p; }
                }
            }
            return best;
        }
    }
}
