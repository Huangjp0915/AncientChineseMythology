using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aoyuans
{
    #region 冰弹（压制用冰晶飞棱）

    /// <summary>
    /// 敖闰冰弹 - 微追踪冰晶飞棱（困龙局放牧压制用）
    /// </summary>
    public class AoyuanIceball : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float icePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
            Projectile.alpha = 0;
        }

        public override void AI() {
            icePhase += 0.12f;

            // 前 80f 微追踪, 之后直线（可甩掉）
            if (Projectile.timeLeft > 160) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    float targetAngle = (target.Center - Projectile.Center).ToRotation();
                    float newAngle = AoyuanHelper.LerpAngle(Projectile.velocity.ToRotation(), targetAngle, 0.02f);
                    Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 180, default, 1.3f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.12f;
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float pulse = 1f + MathF.Sin(icePhase * 2f) * 0.15f;

            // 拖尾冰晶
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i -= 2) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoyuanHelper.DeepSeaBlue, AoyuanHelper.FrostCyan, progress) * (progress * 0.45f);
                AoyuanHelper.DrawCrystalShard(Main.spriteBatch, Projectile.oldPos[i] + Projectile.Size / 2f,
                    Projectile.rotation, 0.9f * progress * pulse, trailColor, 0f);
            }

            // 本体: 冰晶菱形
            AoyuanHelper.DrawCrystalShard(Main.spriteBatch, Projectile.Center, Projectile.rotation,
                1.25f * pulse, Color.Lerp(AoyuanHelper.FrostCyan, AoyuanHelper.IceCrystalWhite, 0.4f), 0.7f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 150, default, 1.4f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region 冰柱（通用下落冰晶）

    /// <summary>
    /// 敖闰冰柱 - 从空中下落的冰晶
    /// </summary>
    public class AoyuanIcicle : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float icePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            icePhase += 0.1f;

            if (Projectile.velocity.Y < 18f)
                Projectile.velocity.Y += 0.3f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(8, 8);
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, -1, 120, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float pulse = 1f + MathF.Sin(icePhase * 3f) * 0.12f;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i -= 2) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AoyuanHelper.DeepSeaBlue, AoyuanHelper.IceCrystalWhite, progress) * (progress * 0.4f);
                AoyuanHelper.DrawCrystalShard(Main.spriteBatch, Projectile.oldPos[i] + Projectile.Size / 2f,
                    Projectile.rotation, progress * pulse, trailColor, 0f);
            }

            AoyuanHelper.DrawCrystalShard(Main.spriteBatch, Projectile.Center, Projectile.rotation,
                1.35f * pulse, Color.Lerp(AoyuanHelper.FrostCyan, AoyuanHelper.IceCrystalWhite, 0.5f), 0.6f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region 冰霜旋涡（旧版遗留, 保留类型）

    /// <summary>
    /// 敖闰冰霜旋涡 - 停留在原地的旋转冰霜区域（旧版遗留, 类型保留以兼容）
    /// </summary>
    public class AoyuanFrostVortex : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float vortexAngle;
        private float vortexAlpha;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            vortexAngle += 0.15f;
            vortexAlpha = MathHelper.Lerp(vortexAlpha, 1f, 0.05f);
            Projectile.velocity *= 0.95f;

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                float angle = vortexAngle + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * 60f;
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.IceTorch, 0, 0, 150, default, 1.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 4f;
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 0.6f * vortexAlpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2()) < 70f;
        }

        public override bool PreDraw(ref Color lightColor) {
            AoyuanHelper.DrawFrostAura(Main.spriteBatch, Projectile.Center, 70f, vortexAngle, vortexAlpha);
            return false;
        }
    }

    #endregion

    #region 折光冰束（冰镜发射）

    /// <summary>
    /// 敖闰折光冰束 - 自冰镜射出的定向冰晶光束
    /// 固定原点: 26f 致命预警线锁定 → 30f 成束 → 8f 消散
    /// ai[1]: 出射角
    /// </summary>
    public class AoyuanFrostBeam : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float BeamLength = 1650f;
        private const float BeamWidth = 30f;
        private const int WarnTime = 26;
        private const int ActiveTime = 30;
        private const int FadeTime = 8;
        private const int TotalTime = WarnTime + ActiveTime + FadeTime;

        private int Timer => TotalTime - Projectile.timeLeft;
        private bool BeamActive => Timer >= WarnTime && Timer < WarnTime + ActiveTime;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalTime;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = Projectile.ai[1];

            int t = Timer;
            if (t == WarnTime) {
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.35f, Volume = 1f }, Projectile.Center);
                    ACMUtils.AddScreenShake(2.5f);
                }
            }

            if (BeamActive && Main.netMode != NetmodeID.Server) {
                Vector2 dir = Projectile.ai[1].ToRotationVector2();
                for (int i = 0; i < 3; i++) {
                    float dist = Main.rand.NextFloat(0, BeamLength);
                    Vector2 dustPos = Projectile.Center + dir * dist
                        + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-BeamWidth * 0.5f, BeamWidth * 0.5f);
                    int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff;
                    int d = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 120, default, 1.8f);
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity = dir.RotatedBy(MathHelper.PiOver2 * (Main.rand.NextBool() ? 1 : -1)) * 1.5f;
                }
                Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 1.5f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!BeamActive)
                return false;
            Vector2 dir = Projectile.ai[1].ToRotationVector2();
            float point = 0f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + dir * BeamLength, BeamWidth * 0.7f, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            int t = Timer;
            Vector2 dir = Projectile.ai[1].ToRotationVector2();
            Vector2 start = Projectile.Center;
            Vector2 end = start + dir * BeamLength;

            if (t < WarnTime) {
                // 致命预警线: 渐亮, 末 8f 转白
                float p = t / (float)WarnTime;
                Color core = p > 0.7f ? Color.Lerp(TelegraphColors.Lethal, TelegraphColors.IceWhite, (p - 0.7f) / 0.3f) : TelegraphColors.Lethal;
                ACMShaders.DrawBeam(start, end, 5f + p * 4f, core with { A = 130 }, TelegraphColors.Frost with { A = 0 },
                    0.35f + p * 0.5f, flowSpeed: 2.5f, coreSharp: 3f);
            }
            else {
                float fade = t < WarnTime + ActiveTime
                    ? MathF.Min((t - WarnTime) / 5f, 1f)
                    : 1f - (t - WarnTime - ActiveTime) / (float)FadeTime;
                ACMShaders.DrawBeam(start, end, BeamWidth * (0.6f + fade * 0.5f),
                    AoyuanHelper.IceCrystalWhite with { A = 200 }, AoyuanHelper.FrostCyan with { A = 0 },
                    MathHelper.Clamp(fade, 0f, 1f), flowSpeed: 3.2f, flowScale: 2.6f, coreSharp: 2.2f);

                // 束身冰晶
                if (fade > 0.4f) {
                    for (int i = 0; i < 5; i++) {
                        float dd = BeamLength * (i + 0.5f) / 5f;
                        AoyuanHelper.DrawCrystalShard(Main.spriteBatch, start + dir * dd, Projectile.ai[1],
                            1.1f * fade, AoyuanHelper.IceCrystalWhite * (0.5f * fade), 0.3f);
                    }
                }
            }

            return false;
        }
    }

    #endregion

    #region 冰封航迹（突刺剑痕）

    /// <summary>
    /// 敖闰冰封航迹 - 刹那突刺沿途留下的剑痕段。
    /// 落下时无害的寒雾 → ai[0] 帧后凝晶成伤害冰棱（25f 逃逸窗, 诚实预警）→ ai[1] 帧后碎裂消散
    /// rotation: 突刺方向（生成侧设置）
    /// </summary>
    public class AoyuanPermafrostTrail : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private int CrystallizeDelay => (int)Projectile.ai[0];
        private int LifeAfter => (int)Projectile.ai[1];
        private int Age => CrystallizeDelay + LifeAfter + 14 - Projectile.timeLeft;
        private bool Crystallized => Age >= CrystallizeDelay;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 44;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 200;
        }

        public override void AI() {
            // 首帧各端按同步的 ai0/ai1 对齐寿命（OnSpawn 只在生成端跑, 客户端也要一致的凝晶时序）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = CrystallizeDelay + LifeAfter + 14;
            }

            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = Projectile.ai[2]; // 剑痕朝向（ai 同步, 多人安全）

            // 凝晶瞬间: 冰裂脆响 + 碎晶迸出
            if (Age == CrystallizeDelay && Main.netMode != NetmodeID.Server) {
                if (Main.rand.NextBool(3))
                    SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.5f, Volume = 0.35f }, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(16, 16), DustID.FrostStaff);
                    d.noGravity = true;
                    d.scale = 1.5f;
                    d.velocity = Main.rand.NextVector2Circular(2.5f, 2.5f);
                }
            }

            if (!Crystallized && Main.netMode != NetmodeID.Server && Main.rand.NextBool(4)) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(18, 18), DustID.Cloud);
                d.noGravity = true;
                d.scale = 1.1f;
                d.alpha = 170;
                d.velocity = new Vector2(0, -0.4f);
            }

            float fade = Math.Min(Projectile.timeLeft / 20f, 1f);
            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * (Crystallized ? 0.5f : 0.15f) * fade);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // 凝晶前无害（逃逸窗）; 凝晶后细线判定
            if (!Crystallized || Projectile.timeLeft < 14)
                return false;
            return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2()) < 30f;
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeOut = Math.Min(Projectile.timeLeft / 20f, 1f);

            if (!Crystallized) {
                // 未凝晶: 暗淡寒雾线（预告将要成形的位置）
                Texture2D glow = ACMAsset.SoftGlow;
                if (glow != null) {
                    float p = Age / (float)Math.Max(CrystallizeDelay, 1);
                    Color mist = TelegraphColors.DeepFrost * (0.20f + p * 0.25f);
                    mist.A = 0;
                    Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null, mist,
                        Projectile.rotation, glow.Size() / 2f, new Vector2(0.9f, 0.5f), SpriteEffects.None, 0f);
                }
                return false;
            }

            // 凝晶后: 垂直于剑痕的冰棱簇
            float grow = AoyuanHelper.PolyOut(Math.Min((Age - CrystallizeDelay) / 8f, 1f), 5);
            float perp = Projectile.rotation + MathHelper.PiOver2;
            int seed = Projectile.whoAmI;
            for (int i = 0; i < 3; i++) {
                float ofs = (i - 1) * 14f;
                float jag = MathF.Sin(seed * 2.3f + i * 1.9f) * 0.35f;
                Color c = Color.Lerp(AoyuanHelper.FrostCyan, AoyuanHelper.IceCrystalWhite, i * 0.35f) * (0.75f * fadeOut);
                AoyuanHelper.DrawCrystalShard(Main.spriteBatch,
                    Projectile.Center + Projectile.rotation.ToRotationVector2() * ofs,
                    perp + jag, (1.15f - i * 0.22f) * grow, c, 0.45f * fadeOut);
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 6; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14, 14),
                    Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff);
                d.noGravity = true;
                d.scale = 1.4f;
                d.velocity = Main.rand.NextVector2Circular(3f, 3f);
            }
        }
    }

    #endregion

    #region 冰晶棋局预告（旧版遗留, 保留类型）

    /// <summary>
    /// 敖闰冰晶棋局预告 - 旧版遗留, 类型保留以兼容本地化与外部引用
    /// </summary>
    public class AoyuanPillarTelegraph : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 30;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    #endregion

    #region 暴雪帷幕（旧版遗留, 保留类型）

    /// <summary>
    /// 敖闰暴雪帷幕 - 旧版遗留, 类型保留以兼容本地化与外部引用
    /// </summary>
    public class AoyuanBlizzardWall : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 30;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    #endregion

    #region 绝对零度 - 放射冻结环

    /// <summary>
    /// 敖闰绝对零度放射环
    /// ai[0]: 0=致命快环(伤害+冻结, 红) 1=寒潮环(仅叠冰冻, 冰蓝, 无伤害)
    /// ai[1]: 1=慢速余波环
    /// </summary>
    public class AoyuanAbsoluteZeroBurst : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const float MaxRadius = 1500f;
        private const float Band = 90f;

        private float burstPhase;
        private bool appliedLocal;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 70;
        }

        private bool Chill => Projectile.ai[0] == 1f;
        private int Lifetime => Projectile.ai[1] > 0.5f ? 110 : 70;
        private float Radius => MaxRadius * (1f - Projectile.timeLeft / (float)Lifetime);

        public override void AI() {
            // 首帧各端按同步的 ai0/ai1 对齐寿命与敌意（OnSpawn 只在生成端跑, 环半径读数必须各端一致）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = Lifetime;
                if (Chill)
                    Projectile.hostile = false;
            }

            burstPhase += 0.2f;
            Projectile.velocity = Vector2.Zero;

            float radius = Radius;

            // 本地玩家被环波扫到 → 冻结 / 叠冰冻
            if (!VaultUtils.isServer && !appliedLocal) {
                Player lp = Main.LocalPlayer;
                if (lp.active && !lp.dead && Math.Abs(Vector2.Distance(lp.Center, Projectile.Center) - radius) < Band) {
                    appliedLocal = true;
                    var fp = lp.GetModPlayer<AoyuanFrostPlayer>();
                    if (Chill)
                        fp.AddChill();
                    else
                        fp.frozenTimer = Math.Max(fp.frozenTimer, 60);
                }
            }

            if (!VaultUtils.isServer) {
                int count = Math.Clamp((int)(radius / 60f), 6, 22);
                for (int i = 0; i < count; i++) {
                    if (!Main.rand.NextBool(3)) continue;
                    float ang = MathHelper.TwoPi * i / count + burstPhase * 0.2f;
                    Vector2 pos = Projectile.Center + ang.ToRotationVector2() * radius;
                    var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff);
                    d.noGravity = true;
                    d.scale = 1.8f;
                    d.velocity = ang.ToRotationVector2() * 4f;
                }
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 1.2f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Chill)
                return false;
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            return Math.Abs(dist - Radius) < Band;
        }

        public override bool PreDraw(ref Color lightColor) {
            float radius = Radius;
            float fade = Math.Min(Projectile.timeLeft / 25f, 1f);

            // 环带: 冰晶菱形链（致命=红 / 寒潮=冰蓝, 全局契约: 红只属于真伤害源）
            Color c = (Chill ? TelegraphColors.Frost : TelegraphColors.Lethal) * (0.65f * fade);
            int ringCount = Math.Clamp((int)(radius / 44f), 10, 40);
            for (int i = 0; i < ringCount; i++) {
                float ang = MathHelper.TwoPi * i / ringCount + burstPhase * 0.1f;
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * radius;
                AoyuanHelper.DrawCrystalShard(Main.spriteBatch, pos, ang + MathHelper.PiOver2, 1.2f * fade, c, 0.3f * fade);
            }

            // 内缘白霜
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Color core = AoyuanHelper.IceCrystalWhite * (0.3f * fade);
                core.A = 0;
                Vector2 origin = glow.Size() / 2f;
                for (int i = 0; i < ringCount; i += 2) {
                    float ang = MathHelper.TwoPi * i / ringCount - burstPhase * 0.07f;
                    Vector2 pos = Projectile.Center + ang.ToRotationVector2() * (radius - 20f) - Main.screenPosition;
                    Main.spriteBatch.Draw(glow, pos, null, core, 0f, origin, 0.5f, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }

    #endregion

    #region 西海冰镜

    /// <summary>
    /// 敖闰西海冰镜 - 折光阵/镜界瞬狱的镜面载体（本体无伤害）
    /// ai[0]: 模式 0=折光阵(自动发射) 1=镜界(受头部指挥) 2=入场演出(只生长后碎裂)
    /// ai[1]: 镜界模式的出射角（头部/齐射指令写入）
    /// ai[2]: 镜界指令 0=无 1=出口白亮 2=齐射充能 3=立即碎裂
    /// 绘制: 帧守卫合批 — 每帧第一面镜用 AoyuanMirror.fx 一次画出全部镜面
    /// </summary>
    public class AoyuanIceMirror : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int GrowTime = 22;
        private const int ArrayChargeTime = 45;
        private const int LanceWarn = 26;

        private static ulong lastMirrorDrawFrame;

        private int Mode => (int)Projectile.ai[0];
        private ref float AimAngle => ref Projectile.ai[1];
        private ref float Command => ref Projectile.ai[2];

        // localAI[0]: 年龄计时  localAI[1]: 白亮/齐射充能计时
        private ref float Age => ref Projectile.localAI[0];
        private ref float CmdTimer => ref Projectile.localAI[1];

        private bool lanceFired;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 120;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 600;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            switch (Mode) {
                case 0:
                    // 折光阵: ai[1] 携带发射时刻（生成侧填写）, 到点自动射折光束
                    Projectile.timeLeft = (int)AimAngle + 44;
                    break;
                case 2:
                    Projectile.timeLeft = Aoyuan.IntroRevealTime - Aoyuan.IntroMirrorTime + 2;
                    break;
                default:
                    Projectile.timeLeft = 520;
                    break;
            }
        }

        /// <summary>镜面生长/蓄光可视标量（0~1 生长+蓄光, >1 出口白亮）</summary>
        public float VisualCharge {
            get {
                float grow = MathHelper.Clamp(Age / GrowTime, 0f, 1f);
                switch (Mode) {
                    case 0: {
                        // 折光阵: 生长后蓄光至发射时刻
                        float fireTick = Projectile.ai[1];
                        float charge = fireTick > GrowTime
                            ? MathHelper.Clamp((Age - GrowTime) / Math.Max(fireTick - GrowTime, 1f), 0f, 1f)
                            : 0f;
                        return grow * (0.6f + charge * 0.4f);
                    }
                    case 1:
                        if (Command == 1f) // 出口白亮
                            return 1f + MathHelper.Clamp(CmdTimer / 30f, 0f, 1f);
                        if (Command == 2f) // 齐射充能
                            return grow * (0.6f + MathHelper.Clamp(CmdTimer / ArrayChargeTime, 0f, 1f) * 0.4f);
                        return grow * 0.6f;
                    default:
                        return grow * 0.8f;
                }
            }
        }

        /// <summary>镜面朝向: 镜界受指挥时用锁定角, 否则缓慢面向玩家（纯视觉）</summary>
        public float Facing {
            get {
                if (Mode == 1 && Command is 1f or 2f)
                    return AimAngle;
                Player p = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                return (p.Center - Projectile.Center).ToRotation();
            }
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Age++;

            switch (Mode) {
                case 0: {
                    float fireTick = Projectile.ai[1];
                    // 发射前 26f 生成折光束（束自带预警线）
                    if (!lanceFired && Age >= fireTick - LanceWarn && Main.netMode != NetmodeID.MultiplayerClient) {
                        lanceFired = true;
                        Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                        AoyuanAttacks.SpawnMirrorLance(Projectile, (target.Center - Projectile.Center).ToRotation());
                    }
                    // 发射后碎裂
                    if (Age >= fireTick + 8 && Main.netMode != NetmodeID.MultiplayerClient)
                        Projectile.Kill();
                    break;
                }
                case 1: {
                    if (Command == 1f || Command == 2f)
                        CmdTimer++;
                    else
                        CmdTimer = 0;

                    // 齐射充能完成 → 射束 + 碎裂
                    if (Command == 2f && CmdTimer >= ArrayChargeTime && Main.netMode != NetmodeID.MultiplayerClient) {
                        AoyuanAttacks.SpawnMirrorLance(Projectile, AimAngle);
                        Command = 0f;
                        Projectile.netUpdate = true;
                        Projectile.timeLeft = Math.Min(Projectile.timeLeft, LanceWarn + 14);
                    }
                    // 入口碎裂指令
                    if (Command == 3f && Main.netMode != NetmodeID.MultiplayerClient)
                        Projectile.Kill();
                    break;
                }
                case 2:
                    // 入场演出镜: 到时自然 Kill
                    break;
            }

            // 成形瞬间脆响
            if ((int)Age == GrowTime && !Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.3f, Volume = 0.6f }, Projectile.Center);

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 0.7f * MathHelper.Clamp(Age / GrowTime, 0f, 1f));
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            // —— 帧守卫合批: 每帧第一面镜统一绘制全部镜面面板 ——
            if (lastMirrorDrawFrame != Main.GameUpdateCount) {
                lastMirrorDrawFrame = Main.GameUpdateCount;
                DrawAllMirrorPanes();
            }

            // —— 出口白亮的出射预警线（镜界瞬狱: 头部即将从此爆出）——
            if (Mode == 1 && Command == 1f && CmdTimer > 6f) {
                float p = MathHelper.Clamp((CmdTimer - 6f) / 24f, 0f, 1f);
                Vector2 dir = AimAngle.ToRotationVector2();
                ACMShaders.DrawBeam(Projectile.Center, Projectile.Center + dir * 1500f, 5f + p * 5f,
                    TelegraphColors.Lethal with { A = 130 }, TelegraphColors.Frost with { A = 0 },
                    0.3f + p * 0.55f, flowSpeed: 2.5f, coreSharp: 3f);
            }

            // —— 每镜 CPU 高光: 边缘 Sparkle 尖芒 ——
            if (ACMAsset.Sparkle != null && VisualCharge > 0.2f) {
                float tw = 0.5f + MathF.Sin(Main.GlobalTimeWrappedHourly * 4f + Projectile.whoAmI * 1.3f) * 0.3f;
                Color sc = AoyuanHelper.IceCrystalWhite * (0.4f * tw * MathHelper.Clamp(VisualCharge, 0f, 1f));
                sc.A = 0;
                Main.spriteBatch.Draw(ACMAsset.Sparkle, Projectile.Center - Main.screenPosition, null, sc,
                    Facing, ACMAsset.Sparkle.Size() / 2f, 0.5f, SpriteEffects.None, 0f);
            }

            return false;
        }

        /// <summary>用 AoyuanMirror.fx 一次 pass 画出屏内全部冰镜（≤6）</summary>
        private static void DrawAllMirrorPanes() {
            Effect fx = AoyuanShaders.Mirror;
            if (fx == null)
                return;

            Vector4[] mirrors = new Vector4[6];
            for (int i = 0; i < 6; i++)
                mirrors[i] = new Vector4(0f, 0f, 0f, -1f);

            int count = 0;
            float sizeFrac = 0f;
            int mirrorType = ModContent.ProjectileType<AoyuanIceMirror>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type != mirrorType || count >= 6)
                    continue;
                ACMShaders.WorldDecalParams(p.Center, 92f, out Vector2 uv, out float radiusFrac, out _);
                if (uv.X < -0.2f || uv.X > 1.2f || uv.Y < -0.2f || uv.Y > 1.2f)
                    continue;
                var mirror = (AoyuanIceMirror)p.ModProjectile;
                mirrors[count] = new Vector4(uv.X, uv.Y, mirror.Facing, mirror.VisualCharge);
                sizeFrac = radiusFrac;
                count++;
            }
            if (count == 0)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uIntensity"]?.SetValue(1f);
            fx.Parameters["uMirrors"]?.SetValue(mirrors);
            fx.Parameters["uMirrorCount"]?.SetValue((float)count);
            fx.Parameters["uSize"]?.SetValue(sizeFrac);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(AoyuanHelper.DeepSeaBlue.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(AoyuanHelper.IceCrystalWhite.ToVector3(), 1f));

            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.AlphaBlend);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ)
                return;
            AoyuanHelper.PlayShatter(Projectile.Center, 0.15f, 0.9f);
            AoyuanHelper.CreateMirrorShards(Projectile.Center, 1.2f, 26);
        }
    }

    #endregion

    #region 寒潮冻土场

    /// <summary>
    /// 敖闰寒潮冻土 - 触地后沿地面蔓延的霜面（AoyuanFrostGround mode0 绘制）
    /// 站上叠加冰冻层(CC, 不伤血); 服务器侧派生冰脊波与冰晶尖刺
    /// ai[0]: 1=二阶段强化（尖刺更多）
    /// </summary>
    public class AoyuanColdField : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int ExpandTime = 70;
        private const int HoldTime = 170;
        private const int FadeTime = 40;
        private const float MaxRadius = 900f;

        private int Age => ExpandTime + HoldTime + FadeTime - Projectile.timeLeft;
        private float Radius => MaxRadius * AoyuanHelper.QuadOut(Math.Min(Age / (float)ExpandTime, 1f));
        private float Fade => Math.Min(Projectile.timeLeft / (float)FadeTime, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = ExpandTime + HoldTime + FadeTime;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;

            // 冰脊波（左右各一道, 可跳过）
            if (Age == 4 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int dir = -1; dir <= 1; dir += 2) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        Projectile.Center + new Vector2(dir * 60f, -20f),
                        new Vector2(dir * 12f, 0f),
                        ModContent.ProjectileType<AoyuanFrostRidge>(),
                        Projectile.damage, 1f, Main.myPlayer);
                }
            }

            // 冰晶尖刺组: 3 波, 落点带 30f 预警
            bool phase2 = Projectile.ai[0] == 1f;
            if ((Age == 70 || Age == 105 || (phase2 && Age == 140)) && Main.netMode != NetmodeID.MultiplayerClient) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                int spikes = phase2 ? 4 : 3;
                for (int i = 0; i < spikes; i++) {
                    float ox = target.Center.X + Main.rand.NextFloat(-260f, 260f);
                    ox = MathHelper.Clamp(ox, Projectile.Center.X - Radius, Projectile.Center.X + Radius);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        new Vector2(ox, Projectile.Center.Y - 30f), Vector2.Zero,
                        ModContent.ProjectileType<AoyuanIceSpike>(),
                        (int)(Projectile.damage * 1.15f), 1f, Main.myPlayer);
                }
                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item30 with { Pitch = -0.2f, Volume = 0.8f }, target.Center);
            }

            // 本地玩家站在霜面上叠冰冻（低空带内, CC 不伤血）
            if (!VaultUtils.isServer) {
                Player lp = Main.LocalPlayer;
                if (lp.active && !lp.dead
                    && Math.Abs(lp.Center.X - Projectile.Center.X) < Radius
                    && lp.Center.Y > Projectile.Center.Y - 150f && lp.Center.Y < Projectile.Center.Y + 120f) {
                    lp.GetModPlayer<AoyuanFrostPlayer>().AddChill();
                }

                // 霜面细雪
                if (Main.rand.NextBool(3)) {
                    float ox = Main.rand.NextFloat(-Radius, Radius);
                    var d = Dust.NewDustPerfect(Projectile.Center + new Vector2(ox, Main.rand.NextFloat(-24f, 6f)), DustID.IceTorch);
                    d.noGravity = true;
                    d.scale = 1f + Main.rand.NextFloat(0.5f);
                    d.velocity = new Vector2(0, -Main.rand.NextFloat(0.4f, 1.1f));
                    d.alpha = 130;
                }
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.DeepSeaBlue.ToVector3() * 0.4f * Fade);
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect fx = AoyuanShaders.FrostGround;
            if (fx == null)
                return false;

            ACMShaders.WorldDecalParams(Projectile.Center, MaxRadius, out Vector2 uv, out float radiusFrac, out float aspect);

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uProgress"]?.SetValue(Radius / MaxRadius);
            fx.Parameters["uIntensity"]?.SetValue(Fade);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.Frost.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(TelegraphColors.IceWhite.ToVector3(), 1f));

            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.AlphaBlend);
            return false;
        }
    }

    #endregion

    #region 冰脊波

    /// <summary>
    /// 敖闰冰脊波 - 沿地表推进的低矮冰浪（高 90px, 可跳过）
    /// </summary>
    public class AoyuanFrostRidge : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 90;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 78;
        }

        public override void AI() {
            Projectile.velocity.Y = 0f;
            Projectile.rotation = 0f;

            if (Main.netMode != NetmodeID.Server) {
                if (Main.rand.NextBool(2)) {
                    var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20, 40), DustID.FrostStaff);
                    d.noGravity = true;
                    d.scale = 1.6f;
                    d.velocity = new Vector2(Projectile.velocity.X * 0.25f, -Main.rand.NextFloat(1f, 3f));
                }
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = Math.Min(Projectile.timeLeft / 15f, 1f) * Math.Min((78 - Projectile.timeLeft) / 8f, 1f);
            int dir = Projectile.velocity.X >= 0 ? 1 : -1;

            // 前倾的冰浪剑气
            Texture2D wave = ACMAsset.GlaciateWave;
            if (wave != null) {
                Vector2 origin = new Vector2(wave.Width / 2f, wave.Height);
                float lean = dir * -0.35f;
                Color c1 = AoyuanHelper.FrostCyan * (0.7f * fade); c1.A = 0;
                Color c2 = AoyuanHelper.IceCrystalWhite * (0.5f * fade); c2.A = 0;
                Vector2 basePos = Projectile.Center + new Vector2(0, 45f) - Main.screenPosition;
                Main.spriteBatch.Draw(wave, basePos, null, c1, -MathHelper.PiOver2 + lean, origin,
                    new Vector2(0.22f, 0.24f), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(wave, basePos, null, c2, -MathHelper.PiOver2 + lean * 1.3f, origin,
                    new Vector2(0.12f, 0.20f), SpriteEffects.None, 0f);
            }

            // 浪尖冰晶
            AoyuanHelper.DrawCrystalShard(Main.spriteBatch, Projectile.Center + new Vector2(0, -28f),
                -MathHelper.PiOver2 + dir * 0.3f, 1.2f * fade, AoyuanHelper.IceCrystalWhite * 0.7f, 0.5f * fade);

            return false;
        }
    }

    #endregion

    #region 冰晶尖刺

    /// <summary>
    /// 敖闰冰晶尖刺 - 霜面上先预警后喷发的冰柱
    /// 30f 落点预警(致命红标) → 12f 喷发判定 → 碎裂消散
    /// </summary>
    public class AoyuanIceSpike : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private const int WarnTime = 30;
        private const int EruptTime = 12;
        private const int LingerTime = 22;
        private const float SpikeHeight = 170f;

        private int Age => WarnTime + EruptTime + LingerTime - Projectile.timeLeft;
        private bool Erupting => Age >= WarnTime && Age < WarnTime + EruptTime;

        public override void SetDefaults() {
            Projectile.width = 56;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = WarnTime + EruptTime + LingerTime;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;

            if (Age == WarnTime && Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.15f, Volume = 0.9f }, Projectile.Center);
                for (int i = 0; i < 14; i++) {
                    var d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-24f, 24f), 10f),
                        Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff);
                    d.noGravity = true;
                    d.scale = 1.8f;
                    d.velocity = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(4f, 9f));
                }
            }

            // 预警期升腾寒气
            if (Age < WarnTime && Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                var d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), 12f), DustID.FrostStaff);
                d.noGravity = true;
                d.scale = 1.1f;
                d.velocity = new Vector2(0, -Main.rand.NextFloat(1f, 2.5f) * (Age / (float)WarnTime + 0.4f));
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * (Erupting ? 1f : 0.3f));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Erupting)
                return false;
            Rectangle spikeBox = new Rectangle((int)(Projectile.Center.X - 26f), (int)(Projectile.Center.Y - SpikeHeight),
                52, (int)SpikeHeight + 18);
            return spikeBox.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = ACMAsset.SoftGlow;
            int age = Age;

            if (age < WarnTime) {
                // 落点预警: 致命红标 + 渐急脉冲
                if (glow != null) {
                    float p = age / (float)WarnTime;
                    float pulse = 1f + MathF.Sin(age * (0.25f + p * 0.5f)) * 0.3f;
                    Color c = Color.Lerp(TelegraphColors.Lethal, TelegraphColors.IceWhite, p * 0.4f) * (0.35f + p * 0.4f);
                    c.A = 0;
                    Main.spriteBatch.Draw(glow, Projectile.Center + new Vector2(0, 8f) - Main.screenPosition, null, c,
                        0f, glow.Size() / 2f, new Vector2(1f * pulse, 0.35f), SpriteEffects.None, 0f);
                }
                return false;
            }

            // 喷发/存续: 冰柱本体
            float grow = AoyuanHelper.PolyOut(Math.Min((age - WarnTime) / (float)EruptTime, 1f), 8);
            float fade = Math.Min(Projectile.timeLeft / 12f, 1f);
            Texture2D wave = ACMAsset.GlaciateWave;
            if (wave != null) {
                Vector2 origin = new Vector2(wave.Width / 2f, wave.Height);
                Vector2 basePos = Projectile.Center + new Vector2(0, 20f) - Main.screenPosition;
                Color c1 = AoyuanHelper.FrostCyan * (0.85f * fade); c1.A = 0;
                Color c2 = AoyuanHelper.IceCrystalWhite * (0.6f * fade); c2.A = 0;
                Main.spriteBatch.Draw(wave, basePos, null, c1, -MathHelper.PiOver2, origin,
                    new Vector2(0.16f, 0.42f * grow), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(wave, basePos, null, c2, -MathHelper.PiOver2 + 0.12f, origin,
                    new Vector2(0.09f, 0.36f * grow), SpriteEffects.None, 0f);
            }
            AoyuanHelper.DrawCrystalShard(Main.spriteBatch, Projectile.Center - new Vector2(0, SpikeHeight * 0.8f * grow),
                -MathHelper.PiOver2, 1.3f * grow * fade, AoyuanHelper.IceCrystalWhite * 0.8f, 0.6f * fade);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 10; i++) {
                var d = Dust.NewDustPerfect(Projectile.Center - new Vector2(0, Main.rand.NextFloat(SpikeHeight)),
                    Main.rand.NextBool() ? DustID.IceTorch : DustID.FrostStaff);
                d.noGravity = true;
                d.scale = 1.5f;
                d.velocity = Main.rand.NextVector2Circular(3f, 3f);
            }
        }
    }

    #endregion

    #region 冰封陷阱

    /// <summary>
    /// 敖闰冰封困龙局陷阱 - 纯控制冻结区（不伤血 → 冰蓝预警, 诚实倒计时）
    /// 外环标界 + 内缩倒计时环(AoyuanFrostGround mode1, 帧守卫合批 ≤4)
    /// ai[0]: 引爆倒计时总帧数
    /// </summary>
    public class AoyuanFreezeTrap : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public const float TrapRadius = 170f;
        private const int LingerTime = 40;

        private static ulong lastTrapDrawFrame;

        private int Fuse => (int)Projectile.ai[0];
        private int Age => Fuse + LingerTime - Projectile.timeLeft;
        private bool Detonated => Age >= Fuse;

        /// <summary>倒计时进度 0~1（绘制/引爆共用）</summary>
        public float Countdown => MathHelper.Clamp(Age / (float)Math.Max(Fuse, 1), 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 130;
        }

        public override void AI() {
            // 首帧各端按同步的 ai0 对齐寿命（OnSpawn 只在生成端跑; P2 错拍引爆的本地冻结判定依赖一致的倒计时）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = Fuse + LingerTime;
            }

            Projectile.velocity = Vector2.Zero;

            // 引爆帧: 区内本地玩家冻结（CC, 无伤害 → 全程冰蓝, 不用红）
            if (Age == Fuse) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item30 with { Pitch = 0.2f, Volume = 1.1f }, Projectile.Center);
                    AoyuanHelper.PlayShatter(Projectile.Center, -0.2f, 0.7f);
                    AoyuanHelper.CreateIceBurst(Projectile.Center, TrapRadius, 3, 16);

                    Player lp = Main.LocalPlayer;
                    if (lp.active && !lp.dead && Vector2.Distance(lp.Center, Projectile.Center) < TrapRadius) {
                        var fp = lp.GetModPlayer<AoyuanFrostPlayer>();
                        fp.frozenTimer = Math.Max(fp.frozenTimer, 50);
                    }
                }
            }

            // 倒计时末段渐密的冰晶聚拢
            if (!VaultUtils.isServer && !Detonated && Countdown > 0.4f && Main.rand.NextBool(4)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * TrapRadius * Main.rand.NextFloat(0.8f, 1f);
                var d = Dust.NewDustPerfect(pos, DustID.FrostStaff);
                d.noGravity = true;
                d.scale = 1.3f;
                d.velocity = (Projectile.Center - pos) * 0.03f;
            }

            Lighting.AddLight(Projectile.Center, AoyuanHelper.FrostCyan.ToVector3() * (0.25f + Countdown * 0.5f));
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ)
                return false;

            // 帧守卫合批: 每帧第一个陷阱统一画全部陷阱环
            if (lastTrapDrawFrame != Main.GameUpdateCount) {
                lastTrapDrawFrame = Main.GameUpdateCount;
                DrawAllTraps();
            }

            // 引爆后的冰晶簇（CPU 个体绘制）
            if (Detonated) {
                float fade = Math.Min(Projectile.timeLeft / (float)LingerTime, 1f);
                for (int i = 0; i < 5; i++) {
                    float ang = -MathHelper.PiOver2 + (i - 2) * 0.42f;
                    AoyuanHelper.DrawCrystalShard(Main.spriteBatch,
                        Projectile.Center + (ang + MathHelper.PiOver2).ToRotationVector2() * (i - 2) * 34f + new Vector2(0, 10f),
                        ang, (1.4f - Math.Abs(i - 2) * 0.25f) * fade,
                        AoyuanHelper.IceCrystalWhite * (0.75f * fade), 0.5f * fade);
                }
            }

            return false;
        }

        /// <summary>用 AoyuanFrostGround.fx(mode1) 一次 pass 画出全部陷阱（≤4）</summary>
        private static void DrawAllTraps() {
            Effect fx = AoyuanShaders.FrostGround;
            if (fx == null)
                return;

            Vector4[] traps = new Vector4[4];
            for (int i = 0; i < 4; i++)
                traps[i] = new Vector4(0f, 0f, 0f, -1f);

            int count = 0;
            int trapType = ModContent.ProjectileType<AoyuanFreezeTrap>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type != trapType || count >= 4)
                    continue;
                var trap = (AoyuanFreezeTrap)p.ModProjectile;
                if (trap.Detonated)
                    continue;
                ACMShaders.WorldDecalParams(p.Center, TrapRadius, out Vector2 uv, out float radiusFrac, out _);
                traps[count] = new Vector4(uv.X, uv.Y, radiusFrac, trap.Countdown);
                count++;
            }
            if (count == 0)
                return;

            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uIntensity"]?.SetValue(0.9f);
            fx.Parameters["uAspect"]?.SetValue((float)Main.screenWidth / Main.screenHeight);
            fx.Parameters["uMode"]?.SetValue(1f);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.Frost.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(TelegraphColors.IceWhite.ToVector3(), 1f));
            fx.Parameters["uTraps"]?.SetValue(traps);
            fx.Parameters["uTrapCount"]?.SetValue((float)count);

            ACMShaders.DrawScreenSpaceDecal(Main.spriteBatch, fx, BlendState.AlphaBlend);
        }
    }

    #endregion
}
