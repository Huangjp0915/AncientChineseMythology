using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Aokins
{
    #region 龙焰火球

    /// <summary>
    /// 敖钦火球 - 带追踪的火焰弹幕
    /// </summary>
    public class AokinFireball : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float firePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
            Projectile.alpha = 0;
        }

        public override void AI() {
            firePhase += 0.12f;

            if (Projectile.timeLeft > 220) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float targetAngle = toTarget.ToRotation();
                    float currentAngle = Projectile.velocity.ToRotation();
                    float newAngle = MathHelper.Lerp(currentAngle, targetAngle, 0.015f);
                    Projectile.velocity = newAngle.ToRotationVector2() * Projectile.velocity.Length();
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, 0, 0, 180, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.5f, 0.5f);
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.DragonFlameRed.ToVector3() * 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(firePhase * 2f) * 0.2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AokinHelper.MoltenOrange, AokinHelper.DragonFlameRed, 1f - progress);
                trailColor *= progress * 0.4f;
                trailColor.A = 0;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, 0.5f * progress * pulse, SpriteEffects.None, 0f);
            }

            Color outerColor = AokinHelper.DragonFlameRed * 0.35f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, 0f, origin, 0.9f * pulse, SpriteEffects.None, 0f);

            Color midColor = AokinHelper.MoltenOrange * 0.5f * pulse;
            midColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, midColor, 0f, origin, 0.55f * pulse, SpriteEffects.None, 0f);

            Color coreColor = AokinHelper.BlazingGold * 0.8f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, 0f, origin, 0.3f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 150, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region 陨石（保留：祭祀法杖等可能复用）

    /// <summary>
    /// 敖钦陨石 - 从空中下落的火焰陨石（V2 主战已由劫火印记火柱取代刷屏，此弹保留兼容）
    /// </summary>
    public class AokinMeteor : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float firePhase;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            firePhase += 0.1f;
            Projectile.rotation += 0.1f;

            if (Projectile.velocity.Y < 18f)
                Projectile.velocity.Y += 0.3f;

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 2; i++) {
                    Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(10, 10);
                    int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, -2, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
                }
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.DragonFlameRed.ToVector3() * 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float pulse = 1f + MathF.Sin(firePhase * 3f) * 0.15f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AokinHelper.BlazingGold, AokinHelper.DragonFlameRed, 1f - progress);
                trailColor *= progress * 0.5f;
                trailColor.A = 0;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, (0.6f + progress * 0.4f) * pulse, SpriteEffects.None, 0f);
            }

            Color outerColor = AokinHelper.DragonFlameRed * 0.4f * pulse;
            outerColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outerColor, 0f, origin, 1.4f * pulse, SpriteEffects.None, 0f);

            Color midColor = AokinHelper.MoltenOrange * 0.6f * pulse;
            midColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, midColor, 0f, origin, 0.9f * pulse, SpriteEffects.None, 0f);

            Color coreColor = AokinHelper.BlazingGold * 0.9f;
            coreColor.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, coreColor, 0f, origin, 0.5f * pulse, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, default, 2.5f);
                Main.dust[dust].noGravity = true;
            }
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3, 6);
                var d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch);
                d.noGravity = false;
                d.scale = 1.5f;
                d.velocity = vel;
            }
        }
    }

    #endregion

    #region 火焰旋涡

    /// <summary>
    /// 敖钦火焰旋涡 - 停留在原地的旋转火焰区域
    /// </summary>
    public class AokinFlameVortex : ModProjectile
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

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 4; i++) {
                    float angle = vortexAngle + MathHelper.TwoPi * i / 4;
                    float radius = 60f + MathF.Sin(vortexAngle * 2f + i) * 20f;
                    Vector2 dustPos = Projectile.Center + angle.ToRotationVector2() * radius;
                    int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (angle + MathHelper.PiOver2).ToRotationVector2() * 5f;
                }
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.MoltenOrange.ToVector3() * 0.6f * vortexAlpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            float distance = Vector2.Distance(Projectile.Center, targetCenter);
            return distance < 70f;
        }

        public override bool PreDraw(ref Color lightColor) {
            AokinHelper.DrawFlameAura(Main.spriteBatch, Projectile.Center, 70f, vortexAngle, vortexAlpha);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20;
                Vector2 vel = angle.ToRotationVector2() * 5f;
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Torch, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }

    #endregion

    #region 劫火印记 — 预告式顺序火柱

    /// <summary>
    /// 劫火印记火柱：ai0=预告 tick, ai1>0.5=二阶段(更高更宽)。
    /// 三段(参考 §0.4)：地面焦黑预警圈 → 龟裂发光柱影 → 向上喷发火柱(致命)。
    /// 预告非致命用 Flame/Gold；喷发致命才用红。柱沿竖直 DrawBeam 绘制(顶点带, 廉价)。
    /// </summary>
    public class AokinEmberPillar : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private int Telegraph => (int)Projectile.ai[0];
        private bool Bigger => Projectile.ai[1] > 0.5f;
        private ref float Elapsed => ref Projectile.localAI[0];

        private const int EruptDuration = 38;
        private const int FadeDuration = 16;

        private float Height => Bigger ? 540f : 440f;
        private float HalfWidth => Bigger ? 30f : 24f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 600;
        }

        public override void AI() {
            Elapsed++;
            int eruptStart = Telegraph;
            int eruptEnd = Telegraph + EruptDuration;

            if (Elapsed == eruptStart) {
                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = 0.1f, Volume = 0.9f }, Projectile.Center);
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 24; i++) {
                        Vector2 vel = new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(6f, 16f));
                        int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                        var d = Dust.NewDustPerfect(Projectile.Center, dustType, vel, 0, default, Main.rand.NextFloat(2f, 3.5f));
                        d.noGravity = true;
                    }
                }
                ACMUtils.AddScreenShake(3.5f);
            }

            if (Elapsed >= eruptStart && Elapsed < eruptEnd) {
                // 喷发期柱内火焰
                if (!VaultUtils.isServer && Elapsed % 2 == 0) {
                    float h = Main.rand.NextFloat(0f, Height);
                    Vector2 pos = Projectile.Center - new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), h);
                    var d = Dust.NewDustPerfect(pos, DustID.SolarFlare, new Vector2(0, -Main.rand.NextFloat(2f, 6f)), 0, default, 2.4f);
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center - new Vector2(0, Height * 0.4f), AokinHelper.MoltenOrange.ToVector3());
            }

            if (Elapsed >= eruptEnd + FadeDuration)
                Projectile.Kill();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Elapsed < Telegraph || Elapsed >= Telegraph + EruptDuration)
                return false;
            // 竖直柱判定：marker.X 左右 HalfWidth, 自 marker.Y 向上 Height
            float left = Projectile.Center.X - HalfWidth;
            float right = Projectile.Center.X + HalfWidth;
            float bottom = Projectile.Center.Y + 24f;
            float top = Projectile.Center.Y - Height;
            Rectangle col = new Rectangle((int)left, (int)top, (int)(right - left), (int)(bottom - top));
            return col.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) return false;

            int eruptStart = Telegraph;
            int eruptEnd = Telegraph + EruptDuration;
            Vector2 bottom = Projectile.Center;
            Vector2 top = Projectile.Center - new Vector2(0, Height);

            if (Elapsed < eruptStart) {
                // 预告（非致命）：地面圈 + 渐强细柱影, Flame/Gold
                float t = Elapsed / Math.Max(1f, eruptStart);
                float warnIntensity = 0.25f + t * 0.45f;
                ACMShaders.DrawBeam(bottom, top, HalfWidth * (0.4f + t * 0.5f),
                    TelegraphColors.Flame, TelegraphColors.Gold, warnIntensity,
                    flowSpeed: 2.2f, flowScale: 2.4f, coreSharp: 2.6f);

                Texture2D glow = ACMAsset.SoftGlow;
                if (glow != null) {
                    Color g = TelegraphColors.Gold * (0.4f + t * 0.4f);
                    g.A = 0;
                    Main.spriteBatch.Draw(glow, bottom - Main.screenPosition, null, g, 0f,
                        glow.Size() / 2f, (0.7f + t * 0.5f), SpriteEffects.None, 0f);
                }
            }
            else if (Elapsed < eruptEnd) {
                // 喷发（致命）：白金芯 + 致命红边
                float life = (Elapsed - eruptStart) / (float)EruptDuration;
                float intensity = 1f - life * 0.3f;
                ACMShaders.DrawBeam(bottom, top, HalfWidth * (1f + MathF.Sin(Elapsed * 0.6f) * 0.1f),
                    Color.White, TelegraphColors.Lethal, intensity,
                    flowSpeed: 3.5f, flowScale: 1.8f, coreSharp: 1.8f, coreGlow: 1.2f);
            }
            else {
                // 余烬消退
                float fade = 1f - (Elapsed - eruptEnd) / (float)FadeDuration;
                ACMShaders.DrawBeam(bottom, top, HalfWidth * 0.6f * fade,
                    AokinHelper.MoltenOrange, AokinHelper.DragonFlameRed, fade * 0.5f);
            }
            return false;
        }
    }

    #endregion

    #region 焚海劫 — 熔潮裂隙柱（P3 场地机制）

    /// <summary>
    /// 熔潮裂隙柱：焚海劫 P3 的地面熔岩柱阵成员（缺口处不生成=安全平台）。
    /// 比劫火印记更高更持久；预告非致命(Flame/Gold), 喷发致命(红)。
    /// </summary>
    public class AokinLavaFissure : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private int Telegraph => (int)Projectile.ai[0];
        private ref float Elapsed => ref Projectile.localAI[0];

        private const int EruptDuration = 70;
        private const int FadeDuration = 24;
        private const float Height = 720f;
        private const float HalfWidth = 38f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults() {
            Projectile.width = 64;
            Projectile.height = 64;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 900;
        }

        public override void AI() {
            Elapsed++;
            int eruptStart = Telegraph;
            int eruptEnd = Telegraph + EruptDuration;

            if (Elapsed == eruptStart) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f, Volume = 1f }, Projectile.Center);
                ACMUtils.AddScreenShake(4.5f);
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 30; i++) {
                        Vector2 vel = new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(8f, 20f));
                        int dustType = Main.rand.NextBool() ? DustID.SolarFlare : DustID.Lava;
                        var d = Dust.NewDustPerfect(Projectile.Center, dustType, vel, 0, default, Main.rand.NextFloat(2.5f, 4f));
                        d.noGravity = true;
                    }
                }
            }

            if (Elapsed >= eruptStart && Elapsed < eruptEnd) {
                if (!VaultUtils.isServer && Elapsed % 2 == 0) {
                    float h = Main.rand.NextFloat(0f, Height);
                    Vector2 pos = Projectile.Center - new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), h);
                    var d = Dust.NewDustPerfect(pos, DustID.SolarFlare, new Vector2(0, -Main.rand.NextFloat(2f, 7f)), 0, default, 2.8f);
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center - new Vector2(0, Height * 0.4f), AokinHelper.DragonFlameRed.ToVector3() * 1.2f);
            }

            if (Elapsed >= eruptEnd + FadeDuration)
                Projectile.Kill();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Elapsed < Telegraph || Elapsed >= Telegraph + EruptDuration)
                return false;
            float left = Projectile.Center.X - HalfWidth;
            float top = Projectile.Center.Y - Height;
            Rectangle col = new Rectangle((int)left, (int)top, (int)(HalfWidth * 2), (int)(Height + 28));
            return col.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) return false;

            int eruptStart = Telegraph;
            int eruptEnd = Telegraph + EruptDuration;
            Vector2 bottom = Projectile.Center;
            Vector2 top = Projectile.Center - new Vector2(0, Height);

            if (Elapsed < eruptStart) {
                float t = Elapsed / Math.Max(1f, eruptStart);
                ACMShaders.DrawBeam(bottom, top, HalfWidth * (0.35f + t * 0.5f),
                    TelegraphColors.Flame, TelegraphColors.Gold, 0.3f + t * 0.5f,
                    flowSpeed: 2.4f, flowScale: 2.6f, coreSharp: 2.4f);
            }
            else if (Elapsed < eruptEnd) {
                float life = (Elapsed - eruptStart) / (float)EruptDuration;
                ACMShaders.DrawBeam(bottom, top, HalfWidth * (1f + MathF.Sin(Elapsed * 0.4f) * 0.08f),
                    AokinHelper.BlazingGold, TelegraphColors.Lethal, 1f - life * 0.25f,
                    flowSpeed: 3.2f, flowScale: 2.0f, coreSharp: 1.7f, coreGlow: 1.1f);
            }
            else {
                float fade = 1f - (Elapsed - eruptEnd) / (float)FadeDuration;
                ACMShaders.DrawBeam(bottom, top, HalfWidth * 0.6f * fade,
                    AokinHelper.MoltenOrange, AokinHelper.DragonFlameRed, fade * 0.5f);
            }
            return false;
        }
    }

    #endregion

    #region 炼狱茧火环 — 满温泄压（带缺口, 有反制）

    /// <summary>
    /// 炼狱茧泄压火环：ai0=缺口角度(server 同步), ai1=环序(延迟错开)。
    /// 自释放点向外扩张的环带, 留一道缺口(安全, Gold/翠玉标示)——玩家朝缺口冲出即可躲过。
    /// </summary>
    public class AokinInfernoRing : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float GapAngle => Projectile.ai[0];
        private int RingIndex => (int)Projectile.ai[1];
        private ref float Elapsed => ref Projectile.localAI[0];

        private const float MaxRadius = 1150f;
        private const float GrowTime = 78f;
        private const float Band = 48f;
        private const float GapHalf = 0.55f;

        private int Delay => RingIndex * 16;
        private float Radius {
            get {
                float e = Elapsed - Delay;
                if (e <= 0) return 40f;
                return MathHelper.Lerp(60f, MaxRadius, MathHelper.Clamp(e / GrowTime, 0f, 1f));
            }
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            Elapsed++;
            if (Elapsed - Delay > GrowTime + 6)
                Projectile.Kill();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Elapsed < Delay) return false;
            Vector2 d = targetHitbox.Center.ToVector2() - Projectile.Center;
            float dist = d.Length();
            if (Math.Abs(dist - Radius) > Band) return false;
            float diff = MathHelper.WrapAngle(d.ToRotation() - GapAngle);
            if (Math.Abs(diff) < GapHalf) return false; // 缺口=安全
            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) return false;
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow == null) return false;

            float radius = Radius;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 origin = glow.Size() / 2f;

            int segs = 64;
            for (int i = 0; i < segs; i++) {
                float ang = MathHelper.TwoPi * i / segs;
                float diff = MathHelper.WrapAngle(ang - GapAngle);
                bool inGap = Math.Abs(diff) < GapHalf;
                Vector2 pos = center + ang.ToRotationVector2() * radius;

                if (inGap) {
                    // 缺口边缘：安全提示(翠玉/金, 不与红冲突)
                    Color safe = TelegraphColors.Safe * 0.5f;
                    safe.A = 0;
                    Main.spriteBatch.Draw(glow, pos, null, safe, 0f, origin, 0.35f, SpriteEffects.None, 0f);
                    continue;
                }
                // 环带致命：橙红
                Color c = Color.Lerp(AokinHelper.MoltenOrange, TelegraphColors.Lethal, 0.5f) * 0.85f;
                c.A = 0;
                Main.spriteBatch.Draw(glow, pos, null, c, 0f, origin, 0.6f, SpriteEffects.None, 0f);
                Color core = AokinHelper.BlazingGold * 0.5f;
                core.A = 0;
                Main.spriteBatch.Draw(glow, pos, null, core, 0f, origin, 0.32f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    #endregion

    #region 火焰封路龙卷

    /// <summary>
    /// 敖钦火焰封路龙卷 - 战场边界。跟随玩家两侧，半宽读取 Boss <see cref="Aokin.ArenaHalfWidth"/>，
    /// 随阶段向内收缩；相变跨档时一次性"点燃"加亮（bloom + 额外粒子）。
    /// </summary>
    public class AokinBarrierTornado : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float OwnerIndex => ref Projectile.ai[0];
        private ref float Side => ref Projectile.ai[1]; // -1左, 1右

        private float tornadoRotation;
        private float tornadoAlpha;
        private float tornadoHeight;
        private float igniteFlash;
        private int lastPhase = 1;
        private const float MaxHeight = 1200f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 100;
            Projectile.height = 100;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 99999;
        }

        public override void AI() {
            NPC owner = Main.npc[(int)OwnerIndex];
            if (!owner.active || owner.type != ModContent.NPCType<Aokin>()) {
                tornadoAlpha -= 0.02f;
                if (tornadoAlpha <= 0f)
                    Projectile.Kill();
                return;
            }

            float halfWidth = 800f;
            int phase = 1;
            if (owner.ModNPC is Aokin aokin) {
                halfWidth = aokin.ArenaHalfWidth;
                phase = aokin.PhaseRegion;
            }

            // 相变点燃
            if (phase != lastPhase) {
                igniteFlash = 1f;
                lastPhase = phase;
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 24; i++) {
                        Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-60, 60), Main.rand.NextFloat(-MaxHeight / 2, MaxHeight / 2));
                        var d = Dust.NewDustPerfect(pos, DustID.SolarFlare, new Vector2(0, -Main.rand.NextFloat(2, 8)), 0, default, 3f);
                        d.noGravity = true;
                    }
                }
            }
            if (igniteFlash > 0f)
                igniteFlash = Math.Max(0f, igniteFlash - 0.02f);

            Player target = Main.player[owner.target];
            if (target.active && !target.dead) {
                float targetX = target.Center.X + Side * halfWidth;
                Projectile.Center = new Vector2(
                    MathHelper.Lerp(Projectile.Center.X, targetX, 0.03f),
                    target.Center.Y);
            }

            tornadoAlpha = MathHelper.Lerp(tornadoAlpha, 1f, 0.02f);
            tornadoHeight = MathHelper.Lerp(tornadoHeight, MaxHeight, 0.03f);
            tornadoRotation += 0.15f + igniteFlash * 0.1f;

            if (Main.netMode != NetmodeID.Server) {
                int count = 8 + (int)(igniteFlash * 8);
                for (int i = 0; i < count; i++) {
                    float heightOffset = Main.rand.NextFloat(-tornadoHeight / 2, tornadoHeight / 2);
                    float angle = tornadoRotation + Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = 40f + MathF.Abs(heightOffset / tornadoHeight) * 60f;
                    Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(angle) * radius, heightOffset);
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Torch,
                        1 => DustID.SolarFlare,
                        _ => DustID.Smoke
                    };
                    int dust = Dust.NewDust(dustPos, 0, 0, dustType, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = new Vector2(MathF.Cos(angle + MathHelper.PiOver2) * 6f, Main.rand.NextFloat(-2, 2));
                }
            }

            foreach (Player player in Main.player) {
                if (!player.active || player.dead) continue;
                float distance = MathF.Abs(player.Center.X - Projectile.Center.X);
                if (distance < 120f) {
                    float pushDirection = player.Center.X > Projectile.Center.X ? 1 : -1;
                    player.velocity.X += pushDirection * 1.5f;
                }
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.DragonFlameRed.ToVector3() * tornadoAlpha * (0.8f + igniteFlash));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float targetX = targetHitbox.Center.X;
            float distance = MathF.Abs(targetX - Projectile.Center.X);
            float targetY = targetHitbox.Center.Y;
            float heightDiff = MathF.Abs(targetY - Projectile.Center.Y);
            return distance < 60f && heightDiff < tornadoHeight / 2;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            Main.instance.LoadProjectile(ProjectileID.SandnadoHostile);
            Texture2D tornadoTex = TextureAssets.Projectile[ProjectileID.SandnadoHostile].Value;
            Vector2 origin = new Vector2(tornadoTex.Width / 2f, tornadoTex.Height / 2f);

            float ignite = 1f + igniteFlash * 0.8f;

            int segments = 162;
            for (int seg = 0; seg < segments; seg++) {
                float heightPercent = (float)seg / segments;
                float yOffset = (heightPercent - 0.5f) * tornadoHeight;
                float segRadius = 2.6f + MathF.Abs(heightPercent - 0.5f) * 0.8f - seg * 0.01f;
                float segRot = tornadoRotation + seg * 0.3f;
                Vector2 segPos = screenPos + new Vector2(0, yOffset);

                Color outerColor = AokinHelper.DragonFlameRed * tornadoAlpha * 0.4f * ignite;
                outerColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, outerColor, segRot, origin, segRadius * 1.3f, SpriteEffects.None, 0f);

                Color midColor = AokinHelper.MoltenOrange * tornadoAlpha * 0.6f * ignite;
                midColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, midColor, segRot * 1.2f, origin, segRadius, SpriteEffects.None, 0f);

                Color innerColor = AokinHelper.BlazingGold * tornadoAlpha * 0.3f * ignite;
                innerColor.A = 0;
                sb.Draw(tornadoTex, segPos, null, innerColor, segRot * 1.5f, origin, segRadius * 0.7f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    #endregion
}
