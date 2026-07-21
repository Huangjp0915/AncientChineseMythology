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
    /// 敖钦火球 — ai0: 0=直线弹(默认, 走位可解) 1=弱追踪。
    /// V3 主战齐射一律直线弹; 追踪档保留给需要的场合。
    /// </summary>
    public class AokinFireball : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float firePhase;
        private bool Homing => Projectile.ai[0] > 0.5f;

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

            if (Homing && Projectile.timeLeft > 220) {
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

    #region 熔金龙息 — 弧线熔滴

    /// <summary>
    /// 熔金龙息熔滴：弧线下坠的熔金液滴, 落地/超时结成熔池（存留区域封锁, 有上限）。
    /// </summary>
    public class AokinMoltenGlob : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private float wobble;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            wobble += 0.2f;
            // ai0>0 = 自定义重力（熔金雨抛物线反解时与其保持一致）, 否则默认 0.24
            float gravity = Projectile.ai[0] > 0.01f ? Projectile.ai[0] : 0.24f;
            if (Projectile.velocity.Y < 18f)
                Projectile.velocity.Y += gravity;

            // ai1>0 = 预定落点线（熔金雨按玩家脚底 Y 反解抛物线, 无地形时也能按线成池）
            float landingY = Projectile.ai[1];
            if (landingY > 0f && Projectile.velocity.Y > 0f && Projectile.Center.Y >= landingY) {
                Projectile.Kill();
                return;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                var d = Dust.NewDustPerfect(Projectile.Center, dustType, -Projectile.velocity * 0.08f, 130, default, 1.6f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.MoltenOrange.ToVector3() * 0.55f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.Kill();
            return false;
        }

        public override void OnKill(int timeLeft) {
            // 落点结熔池（服务器生成; 全场上限 6 池, 超限先熄最旧）
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int poolType = ModContent.ProjectileType<AokinFlamePool>();
                int count = 0;
                Terraria.Projectile oldest = null;
                foreach (Terraria.Projectile p in Main.ActiveProjectiles) {
                    if (p.type != poolType) continue;
                    count++;
                    if (oldest == null || p.timeLeft < oldest.timeLeft)
                        oldest = p;
                }
                if (count >= 6 && oldest != null)
                    oldest.Kill();

                Terraria.Projectile.NewProjectile(Projectile.GetSource_Death(),
                    Projectile.Center, Vector2.Zero, poolType,
                    (int)(Projectile.damage * 0.8f), 0.5f);
            }

            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.3f, Volume = 0.5f }, Projectile.Center);
            for (int i = 0; i < 12; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(1f, 6f));
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.Lava;
                var d = Dust.NewDustPerfect(Projectile.Center, dustType, vel, 100, default, 1.8f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 沿速度拉伸的熔滴形
            float speed = Projectile.velocity.Length();
            Vector2 stretch = new Vector2(0.55f + speed * 0.014f, 0.4f);
            float pulse = 1f + MathF.Sin(wobble) * 0.12f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = AokinHelper.MoltenOrange * (progress * 0.35f);
                trailColor.A = 0;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, Projectile.rotation, origin, stretch * (0.7f * progress), SpriteEffects.None, 0f);
            }

            Color outer = AokinHelper.DragonFlameRed * 0.5f * pulse;
            outer.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outer, Projectile.rotation, origin, stretch * pulse, SpriteEffects.None, 0f);

            Color core = AokinHelper.BlazingGold * 0.85f;
            core.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, core, Projectile.rotation, origin, stretch * 0.5f, SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>
    /// 熔池：熔滴落点的存留灼烧区（~4s）。低矮宽判定, 站进去才疼——区域封锁而非弹幕压力。
    /// </summary>
    public class AokinFlamePool : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float Elapsed => ref Projectile.localAI[0];
        private float seedOffset;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;
        }

        public override void SetDefaults() {
            Projectile.width = 110;
            Projectile.height = 36;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            Elapsed++;
            if (seedOffset == 0f)
                seedOffset = Projectile.whoAmI * 0.77f + 1f;
            Projectile.velocity = Vector2.Zero;

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(48f, 12f);
                var d = Dust.NewDustPerfect(pos, Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare,
                    new Vector2(0, -Main.rand.NextFloat(1f, 3f)), 130, default, 1.5f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.MoltenOrange.ToVector3() * 0.5f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dx = MathF.Abs(targetHitbox.Center.X - Projectile.Center.X);
            float dy = MathF.Abs(targetHitbox.Center.Y - Projectile.Center.Y);
            return dx < 58f && dy < 34f;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) return false;

            float appear = MathHelper.Clamp(Elapsed / 12f, 0f, 1f);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 40f, 0f, 1f);
            float flicker = 0.85f + MathF.Sin(Elapsed * 0.2f + seedOffset * 5f) * 0.15f;

            // 低矮熔浪(专属柱着色器, 熔火档)
            AokinHelper.DrawFirePillar(Projectile.Center + new Vector2(0, 18f), 74f, 52f,
                appear, fade, 0.8f * flicker * fade, seedOffset, mode: 0);

            // 基座熔光
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Color g = AokinHelper.MoltenOrange * (0.45f * fade * appear);
                g.A = 0;
                Main.spriteBatch.Draw(glow, Projectile.Center + new Vector2(0, 12f) - Main.screenPosition, null, g, 0f,
                    glow.Size() / 2f, new Vector2(1.25f, 0.4f) * flicker, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    #endregion

    #region 陨石（保留：祭祀法杖等可能复用）

    /// <summary>
    /// 敖钦陨石 - 从空中下落的火焰陨石（主战已弃用，保留兼容）
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

    #region 火焰旋涡（保留兼容, 主战已由炎龙卷舞取代）

    /// <summary>
    /// 敖钦火焰旋涡 - 停留在原地的旋转火焰区域（主战已弃用，保留兼容）
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
    /// 三段: 地面金圈+柱影预告(非致命 Flame/Gold) → 熔火柱喷发(致命) → 余烬消退。
    /// V3: 柱体改用 AokinFirePillar 专属着色器（熔岩质感取代"光束感"）。
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
        private float Seed => Projectile.whoAmI * 0.61f + 0.5f;

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
            Vector2 bottom = Projectile.Center + new Vector2(0, 24f);

            if (Elapsed < eruptStart) {
                // 预告（非致命 Flame/Gold）：地面金圈 + 幽影柱渐显
                float t = Elapsed / Math.Max(1f, eruptStart);

                AokinHelper.DrawFirePillar(bottom, Height, HalfWidth * 1.2f,
                    1f, 1f, 0.10f + t * 0.22f, Seed, mode: 0);

                Texture2D glow = ACMAsset.SoftGlow;
                if (glow != null) {
                    float pulse = 0.8f + MathF.Sin(Elapsed * 0.35f) * 0.2f;
                    Color g = TelegraphColors.Gold * ((0.35f + t * 0.45f) * pulse);
                    g.A = 0;
                    Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null, g, 0f,
                        glow.Size() / 2f, new Vector2(0.75f + t * 0.4f, 0.28f), SpriteEffects.None, 0f);
                }
            }
            else if (Elapsed < eruptEnd) {
                // 喷发（致命）：熔火柱急速生长
                float grow = ACMUtils.BackOut(MathHelper.Clamp((Elapsed - eruptStart) / 8f, 0f, 1f));
                float life = (Elapsed - eruptStart) / (float)EruptDuration;
                AokinHelper.DrawFirePillar(bottom, Height, HalfWidth * 1.6f,
                    grow, 1f, 1f - life * 0.2f, Seed, mode: 0);
            }
            else {
                // 余烬消退
                float fade = 1f - (Elapsed - eruptEnd) / (float)FadeDuration;
                AokinHelper.DrawFirePillar(bottom, Height, HalfWidth * 1.4f,
                    1f, fade, fade * 0.7f, Seed, mode: 0);
            }
            return false;
        }
    }

    #endregion

    #region 沸海间歇泉 — 追足蒸汽柱

    /// <summary>
    /// 沸海间歇泉：ai0=预告 tick, ai1=高度系数。42f 水泡聚集(白金圈+嘶鸣, 非致命) →
    /// 30f 蒸汽柱喷发(致命) → 消散。用蒸汽白与熔火柱明确区分。
    /// </summary>
    public class AokinScaldGeyser : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private int Telegraph => (int)Projectile.ai[0];
        private float HeightScale => Projectile.ai[1] > 0.1f ? Projectile.ai[1] : 1f;
        private ref float Elapsed => ref Projectile.localAI[0];

        private const int EruptDuration = 30;
        private const int FadeDuration = 14;
        private float Height => 560f * HeightScale;
        private const float HalfWidth = 34f;
        private float Seed => Projectile.whoAmI * 0.43f + 2f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 56;
            Projectile.height = 56;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 400;
        }

        public override void AI() {
            Elapsed++;
            int eruptStart = Telegraph;
            int eruptEnd = Telegraph + EruptDuration;

            if (Elapsed == 2)
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);

            // 预告期：水泡上冒 + 嘶鸣渐强
            if (Elapsed < eruptStart && !VaultUtils.isServer && Elapsed % 4 == 0) {
                float t = Elapsed / Math.Max(1f, eruptStart);
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), Main.rand.NextFloat(-6f, 10f));
                var d = Dust.NewDustPerfect(pos, DustID.Smoke, new Vector2(0, -Main.rand.NextFloat(1f, 3f + t * 3f)), 150, default, 1.3f + t);
                d.noGravity = true;
            }

            if (Elapsed == eruptStart) {
                SoundEngine.PlaySound(SoundID.Item95 with { Pitch = -0.15f, Volume = 1.1f }, Projectile.Center);
                ACMUtils.AddScreenShake(3f);
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 26; i++) {
                        Vector2 vel = new Vector2(Main.rand.NextFloat(-3.5f, 3.5f), -Main.rand.NextFloat(8f, 20f));
                        var d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, vel, 110, default, Main.rand.NextFloat(2f, 3.4f));
                        d.noGravity = true;
                    }
                }
            }

            if (Elapsed >= eruptStart && Elapsed < eruptEnd) {
                if (!VaultUtils.isServer && Elapsed % 2 == 0) {
                    float h = Main.rand.NextFloat(0f, Height);
                    Vector2 pos = Projectile.Center - new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), h);
                    var d = Dust.NewDustPerfect(pos, DustID.Smoke, new Vector2(0, -Main.rand.NextFloat(3f, 8f)), 130, default, 2.2f);
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center - new Vector2(0, Height * 0.3f), AokinHelper.SteamWhite.ToVector3() * 0.4f);
            }

            if (Elapsed >= eruptEnd + FadeDuration)
                Projectile.Kill();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Elapsed < Telegraph || Elapsed >= Telegraph + EruptDuration)
                return false;
            float left = Projectile.Center.X - HalfWidth;
            float top = Projectile.Center.Y - Height;
            Rectangle col = new Rectangle((int)left, (int)top, (int)(HalfWidth * 2), (int)(Height + 26));
            return col.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) return false;

            int eruptStart = Telegraph;
            int eruptEnd = Telegraph + EruptDuration;
            Vector2 bottom = Projectile.Center + new Vector2(0, 26f);

            if (Elapsed < eruptStart) {
                // 预告：白金水泡圈脉动（非致命色）
                float t = Elapsed / Math.Max(1f, eruptStart);
                Texture2D glow = ACMAsset.SoftGlow;
                if (glow != null) {
                    float pulse = 0.75f + MathF.Sin(Elapsed * (0.25f + t * 0.35f)) * 0.25f;
                    Color g = Color.Lerp(TelegraphColors.Gold, AokinHelper.SteamWhite, 0.6f) * ((0.35f + t * 0.5f) * pulse);
                    g.A = 0;
                    Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null, g, 0f,
                        glow.Size() / 2f, new Vector2(0.65f + t * 0.5f, 0.24f + t * 0.12f), SpriteEffects.None, 0f);
                }
                // 幽影汽柱
                AokinHelper.DrawFirePillar(bottom, Height, HalfWidth,
                    1f, 1f, 0.08f + t * 0.16f, Seed, mode: 1);
            }
            else if (Elapsed < eruptEnd) {
                float grow = ACMUtils.BackOut(MathHelper.Clamp((Elapsed - eruptStart) / 7f, 0f, 1f));
                float life = (Elapsed - eruptStart) / (float)EruptDuration;
                AokinHelper.DrawFirePillar(bottom, Height, HalfWidth * 1.5f,
                    grow, 1f, 0.95f - life * 0.15f, Seed, mode: 1);
            }
            else {
                float fade = 1f - (Elapsed - eruptEnd) / (float)FadeDuration;
                AokinHelper.DrawFirePillar(bottom, Height, HalfWidth * 1.3f,
                    1f, fade, fade * 0.6f, Seed, mode: 1);
            }
            return false;
        }
    }

    #endregion

    #region 焚海劫 — 熔潮裂隙柱（P3 场地机制）

    /// <summary>
    /// 熔潮裂隙柱：焚海劫的地面熔岩柱阵成员（缺口处不生成=安全平台）。
    /// 比劫火印记更高更持久；V3 改用 AokinFirePillar 着色器绘制。
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
        private float Seed => Projectile.whoAmI * 0.37f + 4f;

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
            Vector2 bottom = Projectile.Center + new Vector2(0, 28f);

            if (Elapsed < eruptStart) {
                float t = Elapsed / Math.Max(1f, eruptStart);
                AokinHelper.DrawFirePillar(bottom, Height, HalfWidth * 1.15f,
                    1f, 1f, 0.10f + t * 0.24f, Seed, mode: 0);

                Texture2D glow = ACMAsset.SoftGlow;
                if (glow != null) {
                    float pulse = 0.8f + MathF.Sin(Elapsed * 0.3f) * 0.2f;
                    Color g = TelegraphColors.Flame * ((0.3f + t * 0.5f) * pulse);
                    g.A = 0;
                    Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null, g, 0f,
                        glow.Size() / 2f, new Vector2(0.9f + t * 0.4f, 0.3f), SpriteEffects.None, 0f);
                }
            }
            else if (Elapsed < eruptEnd) {
                float grow = ACMUtils.BackOut(MathHelper.Clamp((Elapsed - eruptStart) / 9f, 0f, 1f));
                float life = (Elapsed - eruptStart) / (float)EruptDuration;
                AokinHelper.DrawFirePillar(bottom, Height, HalfWidth * 1.6f,
                    grow, 1f, 1f - life * 0.15f, Seed, mode: 0);
            }
            else {
                float fade = 1f - (Elapsed - eruptEnd) / (float)FadeDuration;
                AokinHelper.DrawFirePillar(bottom, Height, HalfWidth * 1.4f,
                    1f, fade, fade * 0.65f, Seed, mode: 0);
            }
            return false;
        }
    }

    #endregion

    #region 炼狱茧火环 — 满温泄压（带缺口, 有反制）

    /// <summary>
    /// 炼狱茧泄压火环：ai0=缺口角度(server 同步), ai1=环序(延迟错开)。
    /// 自释放点向外扩张的环带, 留一道缺口(安全, Safe 色金门柱标示)——玩家朝缺口冲出即可躲过。
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

            float radius = Radius;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float lifeFade = 1f - MathHelper.Clamp((Elapsed - Delay - GrowTime + 8f) / 12f, 0f, 1f);

            // 缺口方向安全射线：自环心指向出口, 提前告知逃生方向
            AokinHelper.DrawTelegraphLine(Main.spriteBatch,
                Projectile.Center + GapAngle.ToRotationVector2() * radius * 0.2f,
                Projectile.Center + GapAngle.ToRotationVector2() * (radius + 300f),
                TelegraphColors.Safe, 0.55f * lifeFade, 0.14f);

            bool shaderDrawn = AokinHelper.ShockRingEffect != null;
            if (shaderDrawn) {
                // AokinShockRing 着色器环带（火焰噪声边 + 白热前缘 + 翠玉缺口）
                AokinHelper.DrawShockRing(Projectile.Center, radius, Band * 1.05f,
                    GapAngle, GapHalf, 0.95f * lifeFade, steamMode: false,
                    time: (float)Main.GlobalTimeWrappedHourly + RingIndex * 1.7f);
            }
            else if (glow != null) {
                // 回退: SoftGlow 点拼环
                Vector2 origin = glow.Size() / 2f;
                int segs = 72;
                for (int i = 0; i < segs; i++) {
                    float ang = MathHelper.TwoPi * i / segs;
                    float diff = MathHelper.WrapAngle(ang - GapAngle);
                    bool inGap = Math.Abs(diff) < GapHalf;
                    Vector2 pos = center + ang.ToRotationVector2() * radius;

                    if (inGap) {
                        Color safe = TelegraphColors.Safe * (0.45f * lifeFade);
                        safe.A = 0;
                        Main.spriteBatch.Draw(glow, pos, null, safe, 0f, origin, 0.32f, SpriteEffects.None, 0f);
                        continue;
                    }
                    // 环带致命：橙红双层 + 金芯
                    float flick = 0.85f + MathF.Sin(Elapsed * 0.4f + ang * 5f) * 0.15f;
                    Color c = Color.Lerp(AokinHelper.MoltenOrange, TelegraphColors.Lethal, 0.5f) * (0.85f * lifeFade * flick);
                    c.A = 0;
                    Main.spriteBatch.Draw(glow, pos, null, c, 0f, origin, 0.62f, SpriteEffects.None, 0f);
                    Color core = AokinHelper.BlazingGold * (0.5f * lifeFade);
                    core.A = 0;
                    Main.spriteBatch.Draw(glow, pos, null, core, 0f, origin, 0.33f, SpriteEffects.None, 0f);
                }
            }

            // 缺口金门柱：明确告知"往这跑"
            if (glow != null) {
                Vector2 origin = glow.Size() / 2f;
                float postPulse = 0.7f + MathF.Sin(Elapsed * 0.35f) * 0.3f;
                for (int s = -1; s <= 1; s += 2) {
                    float edgeAngle = GapAngle + s * GapHalf;
                    Vector2 post = center + edgeAngle.ToRotationVector2() * radius;
                    Color pc = TelegraphColors.Gold * (0.8f * postPulse * lifeFade);
                    pc.A = 0;
                    Main.spriteBatch.Draw(glow, post, null, pc, edgeAngle + MathHelper.PiOver2,
                        origin, new Vector2(0.3f, 1.0f), SpriteEffects.None, 0f);
                }
            }

            return false;
        }
    }

    #endregion

    #region 炎龙卷舞 — 漂移火龙卷

    /// <summary>
    /// 炎龙卷舞龙卷：ai0=初始方位角, ai1=Boss whoAmI。
    /// 绕 Boss 目标玩家缓慢漂移(≤0.9px/f 级), 轻微牵引(可对抗); 本体即预警(大而慢)。
    /// AokinFireTornado 着色器一次 quad 绘制。
    /// </summary>
    public class AokinFireTornadoProj : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float OrbitAngle => ref Projectile.ai[0];
        private ref float OwnerIndex => ref Projectile.ai[1];
        private ref float Elapsed => ref Projectile.localAI[0];

        private float orbitRadius = 260f;
        private float alpha;
        private const float TornadoHeight = 430f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;
        }

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 320;
        }

        public override void AI() {
            Elapsed++;
            alpha = MathHelper.Lerp(alpha, Projectile.timeLeft < 40 ? 0f : 1f, 0.06f);

            NPC owner = OwnerIndex >= 0 && OwnerIndex < Main.maxNPCs ? Main.npc[(int)OwnerIndex] : null;
            if (owner == null || !owner.active || owner.type != ModContent.NPCType<Aokin>()) {
                Projectile.velocity *= 0.95f;
                if (Projectile.timeLeft > 40)
                    Projectile.timeLeft = 40;
            }
            else {
                Player target = Main.player[owner.target];
                if (target.active && !target.dead) {
                    // 缓慢公转 + 半径微收（大而慢=自带预警）
                    OrbitAngle += 0.0045f;
                    orbitRadius = MathHelper.Lerp(orbitRadius, 208f, 0.0016f);
                    Vector2 desired = target.Center + OrbitAngle.ToRotationVector2() * orbitRadius;
                    Vector2 to = desired - Projectile.Center;
                    float maxStep = 0.9f;
                    Projectile.velocity = to.LengthSquared() > maxStep * maxStep
                        ? to.SafeNormalize(Vector2.Zero) * maxStep
                        : to;
                }
            }

            // 轻牵引（可对抗的小扰动）
            if (Main.netMode != NetmodeID.Server) {
                Player lp = Main.LocalPlayer;
                if (lp.active && !lp.dead) {
                    float dist = Vector2.Distance(lp.Center, Projectile.Center);
                    if (dist < 230f && dist > 40f) {
                        Vector2 pull = (Projectile.Center - lp.Center).SafeNormalize(Vector2.Zero);
                        lp.velocity += pull * 0.1f * alpha * (1f - dist / 230f);
                    }
                }
            }

            // 卷动粒子
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                float h = Main.rand.NextFloat(-TornadoHeight / 2f, TornadoHeight / 2f);
                float ang = Elapsed * 0.2f + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dustPos = Projectile.Center + new Vector2(MathF.Cos(ang) * (30f + MathF.Abs(h) * 0.12f), h);
                int dustType = Main.rand.NextBool(3) ? DustID.Smoke : DustID.Torch;
                var d = Dust.NewDustPerfect(dustPos, dustType, new Vector2(MathF.Cos(ang + MathHelper.PiOver2) * 4f, -1.5f), 130, default, 1.8f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.MoltenOrange.ToVector3() * 0.7f * alpha);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (alpha < 0.5f) return false;
            float dx = MathF.Abs(targetHitbox.Center.X - Projectile.Center.X);
            float dy = MathF.Abs(targetHitbox.Center.Y - Projectile.Center.Y);
            return dx < 48f && dy < TornadoHeight / 2f;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) return false;

            AokinHelper.DrawFireTornado(Projectile.Center, TornadoHeight, 78f,
                alpha * 0.95f, 0f, Projectile.whoAmI * 0.53f, spin: 1.2f);

            // 底部熔光
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Color g = AokinHelper.MoltenOrange * (0.4f * alpha);
                g.A = 0;
                Main.spriteBatch.Draw(glow, Projectile.Center + new Vector2(0, TornadoHeight * 0.45f) - Main.screenPosition,
                    null, g, 0f, glow.Size() / 2f, new Vector2(1.1f, 0.35f), SpriteEffects.None, 0f);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.Smoke;
                var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(40f, 120f), dustType, vel, 120, default, 2f);
                d.noGravity = true;
            }
        }
    }

    #endregion

    #region 龙焰洪流 — 横贯火河（P3 终极）

    /// <summary>
    /// 龙焰洪流：ai0=推进方向(±1), ai1=Boss whoAmI。
    /// 自龙口横贯推进的火河(前锋 14px/f, 河高 190), 上下即安全区; 判定与可视边界严格一致。
    /// 绘制: AokinFirePillar 横置(growth=推进) + BeamGrad 白热芯 + 前锋余烬。
    /// </summary>
    public class AokinFlameFlood : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float Direction => ref Projectile.ai[0];
        private ref float Elapsed => ref Projectile.localAI[0];

        private const int ActiveTime = 130;
        private const int FadeTime = 40;
        private const float FrontSpeed = 14f;
        private const float MaxLength = 2600f;
        private const float HalfHeight = 95f;

        private float FrontDist => MathF.Min(Elapsed * FrontSpeed, MaxLength);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2800;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = ActiveTime + FadeTime;
        }

        public override void AI() {
            Elapsed++;
            Projectile.velocity = Vector2.Zero;

            bool active = Elapsed <= ActiveTime;

            // 前锋余烬 + 河体上飘火星
            if (!VaultUtils.isServer && active) {
                Vector2 front = Projectile.Center + new Vector2(Direction * FrontDist, 0f);
                for (int i = 0; i < 3; i++) {
                    Vector2 vel = new Vector2(Direction * Main.rand.NextFloat(4f, 10f), Main.rand.NextFloat(-4f, 4f));
                    int dustType = Main.rand.NextBool() ? DustID.Torch : DustID.SolarFlare;
                    var d = Dust.NewDustPerfect(front + Main.rand.NextVector2Circular(24f, HalfHeight * 0.8f), dustType, vel, 60, default, Main.rand.NextFloat(2.2f, 3.4f));
                    d.noGravity = true;
                }
                if (Main.rand.NextBool(2)) {
                    float x = Main.rand.NextFloat(0f, FrontDist);
                    Vector2 pos = Projectile.Center + new Vector2(Direction * x, Main.rand.NextFloat(-HalfHeight, HalfHeight));
                    var e = Dust.NewDustPerfect(pos, DustID.SolarFlare, new Vector2(Direction * 2f, -Main.rand.NextFloat(2f, 5f)), 90, default, 2f);
                    e.noGravity = true;
                }

                Lighting.AddLight(front, AokinHelper.BlazingGold.ToVector3() * 1.2f);
                for (float x = 0; x < FrontDist; x += 260f)
                    Lighting.AddLight(Projectile.Center + new Vector2(Direction * x, 0f), AokinHelper.MoltenOrange.ToVector3() * 0.8f);
            }

            // 持续低鸣
            if (active && Elapsed % 28 == 0)
                SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.5f, Volume = 0.7f }, Projectile.Center + new Vector2(Direction * FrontDist * 0.5f, 0f));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Elapsed > ActiveTime)
                return false;
            // 判定与可视河道严格一致：源点向 Direction 推进 FrontDist, 高 ±HalfHeight
            float length = FrontDist;
            float left = Direction > 0 ? Projectile.Center.X : Projectile.Center.X - length;
            Rectangle band = new Rectangle((int)left, (int)(Projectile.Center.Y - HalfHeight), (int)length, (int)(HalfHeight * 2f));
            return band.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) return false;

            float fade = Elapsed > ActiveTime ? 1f - (Elapsed - ActiveTime) / (float)FadeTime : 1f;
            if (fade <= 0.01f) return false;

            float length = FrontDist;
            Vector2 origin = Projectile.Center;
            Vector2 front = origin + new Vector2(Direction * length, 0f);

            // 河体：横置熔火柱（柱长=已推进距离, 基座=龙口端, 已推进部分全显）
            AokinHelper.DrawFirePillar(origin, length, HalfHeight,
                1f, fade, 0.95f * fade, 7.7f, mode: 0,
                rotation: Direction > 0 ? MathHelper.PiOver2 : -MathHelper.PiOver2);

            // 白热芯（BeamGrad 顶点带）
            ACMShaders.DrawBeam(origin, front, HalfHeight * 0.4f,
                Color.White, AokinHelper.BlazingGold, 0.85f * fade,
                flowSpeed: 4f, flowScale: 1.6f, coreSharp: 2f, coreGlow: 1.1f);

            // 前锋亮球
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null && Elapsed <= ActiveTime) {
                float pulse = 1f + MathF.Sin(Elapsed * 0.5f) * 0.15f;
                Color g = AokinHelper.BlazingGold * (0.85f * fade);
                g.A = 0;
                Main.spriteBatch.Draw(glow, front - Main.screenPosition, null, g, 0f,
                    glow.Size() / 2f, new Vector2(1.0f, 1.5f) * pulse, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    #endregion

    #region 赤炎龙息 — 锥形火舌（持械锥）

    /// <summary>
    /// 赤炎龙息锥：ai0=Boss whoAmI, ai1>0.5=强化（狂暴/P3, 更长更宽）。
    /// 挂在龙口, 方向实时取 Boss 朝向（Boss 侧钳制转速 → 可绕背）; AokinBreathCone 条带绘制,
    /// 判定=口部沿向线段的锥形距离场, 与可视火舌严格一致。
    /// </summary>
    public class AokinBreathFlame : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float OwnerIndex => ref Projectile.ai[0];
        private bool Empowered => Projectile.ai[1] > 0.5f;
        private ref float Elapsed => ref Projectile.localAI[0];

        private const int Duration = 42;
        private float MaxLength => Empowered ? 640f : 540f;
        private float EndHalfWidth => Empowered ? 108f : 88f;

        /// <summary>火舌当前长度（前 12f BackOut 展开）。</summary>
        private float CurLength => MaxLength * ACMUtils.BackOut(MathHelper.Clamp(Elapsed / 12f, 0f, 1f));
        /// <summary>收尾淡出。</summary>
        private float Fade => 1f - MathHelper.Clamp((Elapsed - (Duration - 8f)) / 8f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Duration;
        }

        public override void AI() {
            Elapsed++;

            NPC owner = OwnerIndex >= 0 && OwnerIndex < Main.maxNPCs ? Main.npc[(int)OwnerIndex] : null;
            if (owner == null || !owner.active || owner.type != ModContent.NPCType<Aokin>()) {
                Projectile.Kill();
                return;
            }

            // 挂口: 位置与方向每帧取 Boss（多人下 rotation 走 NPC 常规同步）
            Vector2 dir = owner.rotation.ToRotationVector2();
            Projectile.Center = owner.Center + dir * 55f;
            Projectile.rotation = owner.rotation;
            Projectile.velocity = Vector2.Zero;

            // 沿舌身火星（速度快死得快）
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    float along = Main.rand.NextFloat(0.15f, 1f) * CurLength;
                    float halfW = MathHelper.Lerp(14f, EndHalfWidth, along / MaxLength);
                    Vector2 pos = Projectile.Center + dir * along
                        + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-halfW, halfW) * 0.8f;
                    int dustType = Main.rand.NextBool(3) ? DustID.Torch : DustID.SolarFlare;
                    var d = Dust.NewDustPerfect(pos, dustType, dir * Main.rand.NextFloat(4f, 11f), 90, default, 1.7f);
                    d.noGravity = true;
                }
            }

            for (float along = 80f; along < CurLength; along += 160f)
                Lighting.AddLight(Projectile.Center + dir * along, AokinHelper.MoltenOrange.ToVector3() * 0.8f * Fade);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Fade < 0.35f)
                return false; // 收尾淡出期不再判定

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 start = Projectile.Center;
            Vector2 end = start + dir * CurLength;
            Vector2 p = targetHitbox.Center.ToVector2();

            // 点到线段最近点 + 锥形宽度场
            Vector2 seg = end - start;
            float t = MathHelper.Clamp(Vector2.Dot(p - start, seg) / MathF.Max(seg.LengthSquared(), 0.001f), 0f, 1f);
            Vector2 closest = start + seg * t;
            float halfW = MathHelper.Lerp(14f, EndHalfWidth, t) * 0.85f; // 判定略窄于可视, 宁松勿冤
            return Vector2.DistanceSquared(p, closest) < halfW * halfW;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) return false;

            float intensity = MathHelper.Clamp(Elapsed / 6f, 0f, 1f) * Fade;
            AokinHelper.DrawBreathCone(Projectile.Center, Projectile.rotation.ToRotationVector2(),
                CurLength, EndHalfWidth, intensity, (float)Main.GlobalTimeWrappedHourly + Projectile.whoAmI * 0.37f);

            // 口部亮球
            Texture2D glow = ACMAsset.SoftGlow;
            if (glow != null) {
                Color g = AokinHelper.BlazingGold * (0.8f * intensity);
                g.A = 0;
                Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null, g, 0f,
                    glow.Size() / 2f, 0.75f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    #endregion

    #region 蒸汽龙炮 — 大蒸汽熔球与爆散碎弹

    /// <summary>
    /// 蒸汽熔球：ai0=爆散碎弹伤害。前 26f 轻微追踪后锁直线; 超时/贴近玩家爆散 8 向蒸汽碎弹。
    /// </summary>
    public class AokinSteamOrb : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float ShardDamage => ref Projectile.ai[0];
        private ref float Elapsed => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 96;
        }

        public override void AI() {
            Elapsed++;

            // 前 26f 轻微修正航向, 之后锁直线（可预读）
            if (Elapsed < 26f) {
                Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (target.active && !target.dead) {
                    Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * Projectile.velocity.Length();
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.045f);
                }
            }
            Projectile.velocity *= 0.995f;
            Projectile.rotation += 0.09f;

            // 近身自爆（服务器判定）
            if (Main.netMode != NetmodeID.MultiplayerClient && Elapsed > 20f) {
                Player near = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                if (near.active && !near.dead && Vector2.Distance(near.Center, Projectile.Center) < 90f)
                    Projectile.Kill();
            }

            if (!VaultUtils.isServer) {
                if (Main.rand.NextBool(2)) {
                    var d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                        DustID.Smoke, -Projectile.velocity * 0.1f + new Vector2(0, -1.5f), 130, AokinHelper.SteamWhite, 2f);
                    d.noGravity = true;
                }
                if (Main.rand.NextBool(3)) {
                    var e = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare, -Projectile.velocity * 0.15f, 80, default, 1.6f);
                    e.noGravity = true;
                }
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.MoltenOrange.ToVector3() * 0.8f);
        }

        public override void OnKill(int timeLeft) {
            // 爆散 8 向蒸汽碎弹（server）
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int damage = (int)MathF.Max(ShardDamage, 10f);
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi * i / 8f + 0.2f;
                    Terraria.Projectile.NewProjectile(Projectile.GetSource_Death(),
                        Projectile.Center, angle.ToRotationVector2() * 7.5f,
                        ModContent.ProjectileType<AokinSteamShard>(), damage, 1f);
                }
            }

            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.1f, Volume = 1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Splash with { Pitch = 0.4f, Volume = 0.8f }, Projectile.Center);
            AokinHelper.CreateSteamBurst(Projectile.Center, 130f, 30);
            AokinHelper.CreateDragonFireBurst(Projectile.Center, 120f, 3, 12);
            ACMUtils.AddScreenShake(5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(Elapsed * 0.35f) * 0.12f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(AokinHelper.SteamWhite, AokinHelper.MoltenOrange, 1f - progress) * (progress * 0.4f);
                trailColor.A = 0;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.spriteBatch.Draw(tex, pos, null, trailColor, 0f, origin, 1.0f * progress * pulse, SpriteEffects.None, 0f);
            }

            // 蒸汽雾壳 + 熔芯
            Color shell = AokinHelper.SteamWhite * 0.5f * pulse;
            shell.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, shell, 0f, origin, 1.5f * pulse, SpriteEffects.None, 0f);

            Color mid = AokinHelper.MoltenOrange * 0.7f * pulse;
            mid.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mid, 0f, origin, 0.95f * pulse, SpriteEffects.None, 0f);

            Color core = AokinHelper.BlazingGold * 0.95f;
            core.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, core, 0f, origin, 0.5f * pulse, SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>蒸汽碎弹：直线短命小弹（爆散余波, 弹速低可读）。</summary>
    public class AokinSteamShard : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 110;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.velocity *= 0.995f;

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Smoke : DustID.Torch;
                var d = Dust.NewDustPerfect(Projectile.Center, dustType, -Projectile.velocity * 0.1f, 130,
                    dustType == DustID.Smoke ? AokinHelper.SteamWhite : default, 1.3f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, AokinHelper.MoltenOrange.ToVector3() * 0.35f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ACMAsset.SoftGlow ?? TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color c = AokinHelper.SteamWhite * (progress * 0.3f);
                c.A = 0;
                Main.spriteBatch.Draw(tex, Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition,
                    null, c, 0f, origin, 0.35f * progress, SpriteEffects.None, 0f);
            }

            Color shell = AokinHelper.SteamWhite * 0.55f;
            shell.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, shell, 0f, origin, 0.42f, SpriteEffects.None, 0f);
            Color core = AokinHelper.MoltenOrange * 0.75f;
            core.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, core, 0f, origin, 0.24f, SpriteEffects.None, 0f);
            return false;
        }
    }

    #endregion

    #region 无伤冲击波 — 相变蒸腾 / 逆鳞爆气（演出 + 轻推离）

    /// <summary>
    /// 无伤冲击波：ai0>0.5=蒸汽白主题（P2 相变）, 否则逆鳞红主题。
    /// 零伤害, 仅对本地玩家施加一次轻推离; AokinShockRing 蒸汽档绘制。
    /// </summary>
    public class AokinShockwave : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private bool SteamTheme => Projectile.ai[0] > 0.5f;
        private ref float Elapsed => ref Projectile.localAI[0];
        private bool pushedLocal;

        private const float GrowTime = 34f;
        private const float MaxRadius = 950f;

        private float Radius => MathHelper.Lerp(50f, MaxRadius, ACMUtils.QuadOut(MathHelper.Clamp(Elapsed / GrowTime, 0f, 1f)));

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false; // 纯演出, 无伤
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = (int)GrowTime + 10;
        }

        public override void AI() {
            Elapsed++;
            Projectile.velocity = Vector2.Zero;

            // 环带扫过本地玩家时施加一次轻推离（喘息礼物的触感, 不造成伤害）
            if (!Main.dedServ && !pushedLocal) {
                Player lp = Main.LocalPlayer;
                if (lp.active && !lp.dead) {
                    float dist = Vector2.Distance(lp.Center, Projectile.Center);
                    if (MathF.Abs(dist - Radius) < 70f) {
                        Vector2 pushDir = (lp.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                        lp.velocity += pushDir * 5.5f;
                        pushedLocal = true;
                    }
                }
            }

            if (!VaultUtils.isServer && Elapsed % 2 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * Radius;
                int dustType = SteamTheme ? DustID.Smoke : DustID.Torch;
                var d = Dust.NewDustPerfect(pos, dustType, ang.ToRotationVector2() * 3f, 130,
                    SteamTheme ? AokinHelper.SteamWhite : default, 1.8f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) return false;
            float fade = 1f - MathHelper.Clamp((Elapsed - GrowTime) / 10f, 0f, 1f);
            float intensity = MathHelper.Clamp(Elapsed / 5f, 0f, 1f) * fade * (1f - Elapsed / (GrowTime + 12f) * 0.4f);
            AokinHelper.DrawShockRing(Projectile.Center, Radius, 60f, 0f, 0f,
                intensity, steamMode: SteamTheme, time: (float)Main.GlobalTimeWrappedHourly);
            return false;
        }
    }

    #endregion

    #region 火焰封路龙卷

    /// <summary>
    /// 敖钦火焰封路龙卷 - 战场边界。跟随玩家两侧，半宽读取 Boss <see cref="Aokin.ArenaHalfWidth"/>，
    /// 随阶段向内收缩；相变跨档时一次性"点燃"加亮。V3: AokinFireTornado 着色器一次绘制取代 162 段贴图叠绘。
    /// </summary>
    public class AokinBarrierTornado : ModProjectile
    {
        public override string Texture => "InnoVault/Assets/placeholder";

        private ref float OwnerIndex => ref Projectile.ai[0];
        private ref float Side => ref Projectile.ai[1]; // -1左, 1右

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
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.1f, Volume = 0.9f }, Projectile.Center);
                    for (int i = 0; i < 24; i++) {
                        Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-60, 60), Main.rand.NextFloat(-MaxHeight / 2, MaxHeight / 2));
                        var d = Dust.NewDustPerfect(pos, DustID.SolarFlare, new Vector2(0, -Main.rand.NextFloat(2, 8)), 0, default, 3f);
                        d.noGravity = true;
                    }
                }
            }
            if (igniteFlash > 0f)
                igniteFlash = Math.Max(0f, igniteFlash - 0.015f);

            Player target = Main.player[owner.target];
            if (target.active && !target.dead) {
                float targetX = target.Center.X + Side * halfWidth;
                Projectile.Center = new Vector2(
                    MathHelper.Lerp(Projectile.Center.X, targetX, 0.03f),
                    target.Center.Y);
            }

            tornadoAlpha = MathHelper.Lerp(tornadoAlpha, 1f, 0.02f);
            tornadoHeight = MathHelper.Lerp(tornadoHeight, MaxHeight, 0.03f);

            // 卷动粒子（着色器接管主体后可收敛数量）
            if (Main.netMode != NetmodeID.Server) {
                int count = 4 + (int)(igniteFlash * 6);
                for (int i = 0; i < count; i++) {
                    float heightOffset = Main.rand.NextFloat(-tornadoHeight / 2, tornadoHeight / 2);
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
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
            if (Main.dedServ || tornadoAlpha < 0.02f) return false;

            // 双层龙卷：宽外焰 + 窄亮芯（各 1 次 quad）
            AokinHelper.DrawFireTornado(Projectile.Center, tornadoHeight, 120f,
                tornadoAlpha * 0.85f, igniteFlash, Side * 3.1f + 1f, spin: 1f);
            AokinHelper.DrawFireTornado(Projectile.Center, tornadoHeight * 0.96f, 70f,
                tornadoAlpha * 0.9f, igniteFlash, Side * 5.7f + 9f, spin: 1.6f);

            return false;
        }
    }

    #endregion
}
