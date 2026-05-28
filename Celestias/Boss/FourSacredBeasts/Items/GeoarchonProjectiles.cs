using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Items
{
    /// <summary>
    /// 坤空地标 — 在光标处刻印地脉裂穴阵，预兆后召唤七柱地能。
    /// </summary>
    public class GeoarchonMarker : ModProjectile
    {
        private const int PillarCount = 7;
        private const float RingRadius = 88f;
        private const int WarningFrames = 28;

        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = WarningFrames + 8;
        }

        public override void AI() {
            Projectile.localAI[0]++;

            float warnT = Projectile.localAI[0] / WarningFrames;
            float pulse = 0.45f + (float)System.Math.Sin(Projectile.localAI[0] * 0.35f) * 0.25f;

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < PillarCount; i++) {
                    Vector2 pos = GetPillarPosition(i);
                    pos.Y = FindGroundY(pos.X, pos.Y - 400f);

                    if (Main.rand.NextBool(3)) {
                        int dust = Dust.NewDust(pos + new Vector2(Main.rand.NextFloat(-18f, 18f), -4f), 0, 0, DustID.Stone, 0, -1.2f, 90, default, 1.1f + warnT);
                        Main.dust[dust].noGravity = true;
                    }

                    if (Main.rand.NextBool(5)) {
                        int dust = Dust.NewDust(pos + new Vector2(Main.rand.NextFloat(-10f, 10f), -2f), 0, 0, DustID.AmberBolt, 0, -0.8f, 70, default, 1.4f * pulse);
                        Main.dust[dust].noGravity = true;
                    }
                }

                Lighting.AddLight(Projectile.Center, new Vector3(0.55f, 0.42f, 0.18f) * warnT * pulse);
            }

            if (Projectile.localAI[0] >= WarningFrames) {
                SpawnPillars();
                Projectile.Kill();
            }
        }

        private void SpawnPillars() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int pillarType = ModContent.ProjectileType<GeoarchonPillar>();
            float knockback = Projectile.knockBack;
            int damage = Projectile.damage;

            for (int i = 0; i < PillarCount; i++) {
                Vector2 pos = GetPillarPosition(i);
                pos.Y = FindGroundY(pos.X, pos.Y - 400f);

                float heightScale = i == 0 ? 1.15f : 0.92f;
                int stagger = i * 3;

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    pos,
                    Vector2.Zero,
                    pillarType,
                    damage,
                    knockback,
                    Projectile.owner,
                    stagger,
                    heightScale
                );
            }

            if (Projectile.owner == Main.myPlayer) {
                Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(7, 18);
            }

            SoundEngine.PlaySound(SoundID.Item70 with { Pitch = -0.35f, Volume = 0.95f }, Projectile.Center);
        }

        private Vector2 GetPillarPosition(int index) {
            if (index == 0)
                return Projectile.Center;

            float angle = MathHelper.TwoPi * (index - 1) / (PillarCount - 1);
            return Projectile.Center + angle.ToRotationVector2() * RingRadius;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.netMode == NetmodeID.Server || ACMAsset.SoftGlow == null)
                return false;

            float warnT = Projectile.localAI[0] / WarningFrames;
            Texture2D glow = ACMAsset.SoftGlow;
            Vector2 origin = glow.Size() / 2f;

            for (int i = 0; i < PillarCount; i++) {
                Vector2 pos = GetPillarPosition(i);
                pos.Y = FindGroundY(pos.X, pos.Y - 400f);
                Vector2 drawPos = pos - Main.screenPosition;

                float pulse = 0.55f + (float)System.Math.Sin(Projectile.localAI[0] * 0.35f + i) * 0.2f;
                Color color = Color.Lerp(new Color(120, 90, 40, 0), new Color(220, 180, 80, 0), warnT) * pulse * 0.55f;
                float scale = (i == 0 ? 0.55f : 0.42f) * (0.7f + warnT * 0.35f);
                Main.spriteBatch.Draw(glow, drawPos, null, color, 0f, origin, scale, SpriteEffects.None, 0f);
            }

            return false;
        }

        private static float FindGroundY(float worldX, float searchStartY) {
            int tileX = (int)(worldX / 16f);
            int startTileY = (int)(searchStartY / 16f);
            for (int tileY = startTileY; tileY < startTileY + 80; tileY++) {
                if (tileX >= 0 && tileX < Main.maxTilesX && tileY >= 0 && tileY < Main.maxTilesY &&
                    WorldGen.SolidTile(tileX, tileY)) {
                    return tileY * 16f;
                }
            }

            return searchStartY + 480f;
        }
    }

    /// <summary>
    /// 地能裂穴能量柱 — 自地面向上刺出的地脉岩柱。
    /// ai[0] = 启动延迟帧；ai[1] = 高度系数。
    /// </summary>
    public class GeoarchonPillar : ModProjectile
    {
        private const float BaseHeight = 240f;
        private const int WarningDuration = 18;
        private const int GrowDuration = 11;
        private const int HoldDuration = 42;
        private const int ShatterDuration = 16;

        private int lifetime;
        private float growthProgress;
        private float shatterProgress;
        private float warningFlash;
        private float pillarHeight;
        private float pillarWidth = 1f;

        public override string Texture => "AncientChineseMythology/Textures/Projectiles/BlankProjectile";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 240;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
        }

        public override void AI() {
            lifetime++;

            int startDelay = (int)Projectile.ai[0];
            if (lifetime <= startDelay)
                return;

            int activeLife = lifetime - startDelay;

            if (activeLife == 1) {
                pillarHeight = BaseHeight * MathHelper.Clamp(Projectile.ai[1], 0.75f, 1.25f);
                Projectile.width = (int)(44 * pillarWidth);
                Projectile.height = (int)pillarHeight;
                Projectile.timeLeft = WarningDuration + GrowDuration + HoldDuration + ShatterDuration + 6;

                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.DD2_OgreGroundPound with { Pitch = 0.15f, Volume = 0.45f }, Projectile.Center);
                }
            }

            int phase = GetPhase(activeLife);

            if (phase == 0) {
                Projectile.damage = 0;
                float warnT = activeLife / (float)WarningDuration;
                float flashFreq = 5f + warnT * 18f;
                warningFlash = ((float)System.Math.Sin(activeLife * flashFreq * 0.1f) * 0.5f + 0.5f) * warnT;
                growthProgress = warnT * 0.06f;

                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                    SpawnWarningDust(warnT);
                }
            }
            else if (phase == 1) {
                int growFrame = activeLife - WarningDuration;
                float growT = growFrame / (float)GrowDuration;
                growthProgress = ACMUtils.BackOut(Math.Min(growT, 1f));
                shatterProgress = 0f;

                if (growFrame == 1) {
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Item70 with { Pitch = -0.15f, Volume = 0.85f }, Projectile.Center);
                        if (Projectile.owner == Main.myPlayer) {
                            Main.player[Projectile.owner].GetModPlayer<ScreenShakePlayer>().ShakeScreen(4, 8);
                        }
                    }

                    SpawnEruptionDust(0.35f);
                }

                if (Main.netMode != NetmodeID.Server && growFrame <= GrowDuration) {
                    SpawnEruptionDust(growT);
                }
            }
            else if (phase == 2) {
                growthProgress = 1f;
                shatterProgress = 0f;

                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(5)) {
                    SpawnIdleDust();
                }
            }
            else {
                int shatterFrame = activeLife - WarningDuration - GrowDuration - HoldDuration;
                float shatterT = shatterFrame / (float)ShatterDuration;
                growthProgress = 1f;
                shatterProgress = ACMUtils.QuadIn(System.Math.Min(shatterT, 1f));
                Projectile.damage = 0;

                if (shatterFrame == 1 && Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.2f, Volume = 0.65f }, Projectile.Center);
                }

                if (Main.netMode != NetmodeID.Server && shatterFrame <= ShatterDuration) {
                    SpawnShatterDust(shatterT);
                }

                if (shatterT >= 1f) {
                    Projectile.Kill();
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                Lighting.AddLight(Projectile.Center + new Vector2(0, -pillarHeight * 0.35f * growthProgress),
                    new Vector3(0.65f, 0.48f, 0.18f) * growthProgress * 0.9f);
            }
        }

        private int GetPhase(int activeLife) {
            if (activeLife <= WarningDuration) return 0;
            if (activeLife <= WarningDuration + GrowDuration) return 1;
            if (activeLife <= WarningDuration + GrowDuration + HoldDuration) return 2;
            return 3;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            int startDelay = (int)Projectile.ai[0];
            int activeLife = lifetime - startDelay;
            int phase = GetPhase(activeLife);
            if (phase != 1 && phase != 2)
                return false;

            Vector2 basePos = Projectile.Center;
            Vector2 tipPos = basePos - Vector2.UnitY * pillarHeight * growthProgress;
            float lineWidth = 22f * pillarWidth;
            float dummy = 0f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.Location.ToVector2(), targetHitbox.Size(), basePos, tipPos, lineWidth, ref dummy);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 90);

            if (Main.netMode == NetmodeID.Server)
                return;

            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                int dustType = Main.rand.NextBool() ? DustID.Stone : DustID.AmberBolt;
                Dust d = Dust.NewDustDirect(target.Center, 0, 0, dustType, vel.X, vel.Y, 60, default, 1.4f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (growthProgress < 0.01f && warningFlash < 0.01f)
                return false;

            int startDelay = (int)Projectile.ai[0];
            int activeLife = System.Math.Max(0, lifetime - startDelay);
            int phase = GetPhase(activeLife);

            if (phase == 0) {
                DrawWarningIndicator();
                return false;
            }

            DrawEarthPillar();
            return false;
        }

        private void DrawWarningIndicator() {
            if (ACMAsset.SoftGlow == null)
                return;

            Texture2D glow = ACMAsset.SoftGlow;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = glow.Size() / 2f;
            Color warnColor = Color.Lerp(new Color(80, 55, 25, 0), new Color(210, 165, 60, 0), warningFlash);
            warnColor *= warningFlash * 0.65f;
            Main.spriteBatch.Draw(glow, drawPos, null, warnColor, 0f, origin, 0.55f * pillarWidth, SpriteEffects.None, 0f);
        }

        private void DrawEarthPillar() {
            Texture2D tex = ACMAsset.GlaciateWave ?? ACMAsset.SoftGlow;
            if (tex == null)
                return;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            float activeHeight = pillarHeight * growthProgress * (1f - shatterProgress * 0.35f);
            float alpha = MathHelper.Clamp(1f - shatterProgress, 0f, 1f);

            Color outer = new Color(90, 65, 30) * alpha * 0.45f;
            outer.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, outer, MathHelper.PiOver2, origin,
                new Vector2(activeHeight / tex.Width, 0.28f * pillarWidth), SpriteEffects.None, 0f);

            Color mid = new Color(150, 110, 45) * alpha * 0.65f;
            mid.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, mid, MathHelper.PiOver2, origin,
                new Vector2(activeHeight / tex.Width, 0.18f * pillarWidth), SpriteEffects.None, 0f);

            Color core = new Color(230, 190, 90) * alpha * 0.75f;
            core.A = 0;
            Main.spriteBatch.Draw(tex, drawPos, null, core, MathHelper.PiOver2, origin,
                new Vector2(activeHeight / tex.Width, 0.1f * pillarWidth), SpriteEffects.None, 0f);
        }

        private void SpawnWarningDust(float warnT) {
            for (int i = 0; i < 2; i++) {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(-4f, 2f));
                int dust = Dust.NewDust(pos, 0, 0, DustID.Stone, 0, -0.6f, 80, default, 0.9f + warnT);
                Main.dust[dust].noGravity = true;
            }
        }

        private void SpawnEruptionDust(float intensity) {
            for (int i = 0; i < 4; i++) {
                float height = Main.rand.NextFloat(0.1f, 1f) * pillarHeight * growthProgress;
                Vector2 pos = Projectile.Center - Vector2.UnitY * height + Main.rand.NextVector2Circular(18f, 8f);
                int dustType = Main.rand.NextBool() ? DustID.Stone : DustID.AmberBolt;
                Dust d = Dust.NewDustDirect(pos, 0, 0, dustType, 0, 0, 50, default, 1.2f + intensity);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(3.5f, 3.5f) - Vector2.UnitY * Main.rand.NextFloat(1f, 3f);
            }
        }

        private void SpawnIdleDust() {
            float height = Main.rand.NextFloat(0.2f, 0.95f) * pillarHeight;
            Vector2 pos = Projectile.Center - Vector2.UnitY * height;
            int dust = Dust.NewDust(pos, 0, 0, DustID.AmberBolt, 0, -0.4f, 60, default, 0.9f);
            Main.dust[dust].noGravity = true;
        }

        private void SpawnShatterDust(float shatterT) {
            for (int i = 0; i < 5; i++) {
                float height = Main.rand.NextFloat(0f, 1f) * pillarHeight;
                Vector2 pos = Projectile.Center - Vector2.UnitY * height;
                int dustType = Main.rand.NextBool() ? DustID.Stone : DustID.Torch;
                Dust d = Dust.NewDustDirect(pos, 0, 0, dustType, 0, 0, 40, default, 1.3f);
                d.noGravity = Main.rand.NextBool(3);
                d.velocity = Main.rand.NextVector2Circular(5f, 5f);
                d.scale *= 1f - shatterT * 0.4f;
            }
        }
    }
}
