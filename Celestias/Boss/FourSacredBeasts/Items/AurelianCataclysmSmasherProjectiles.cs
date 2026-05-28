using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Items
{
    /// <summary>万劫狂金锤 — 圣骑士之锤式回旋，触地或命中时裂地释放金纹冲击波。</summary>
    public class AurelianCataclysmSmasherProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.PaladinsHammer;

        private ref float FlightMode => ref Projectile.ai[0];
        private ref float FlightTimer => ref Projectile.ai[1];
        private ref float HasReleasedShockwave => ref Projectile.localAI[0];

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 52;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            Projectile.rotation += 0.38f;
            FlightTimer++;

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(10, 10),
                    0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.4f);
                d.noGravity = true;
                d.velocity = -Projectile.velocity * 0.15f;
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.88f, 0.45f) * 0.65f);

            if (FlightMode < 1f) {
                Projectile.velocity *= 0.985f;

                if (Projectile.velocity.Y > 0f && Projectile.velocity.Length() < 10f) {
                    FlightMode = 1f;
                    FlightTimer = 0f;
                    Projectile.netUpdate = true;
                }
                else if (FlightTimer >= 36f) {
                    FlightMode = 1f;
                    FlightTimer = 0f;
                    Projectile.netUpdate = true;
                }
            }
            else {
                Vector2 toOwner = Owner.Center - Projectile.Center;
                if (toOwner.Length() > 4200f) {
                    Projectile.Kill();
                    return;
                }

                float returnSpeed = 24f + FlightTimer * 0.06f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toOwner.SafeNormalize(Vector2.Zero) * returnSpeed, 0.12f);

                if (Main.myPlayer == Projectile.owner) {
                    Rectangle projHitbox = Projectile.Hitbox;
                    Rectangle playerHitbox = Owner.Hitbox;
                    if (projHitbox.Intersects(playerHitbox)) {
                        Projectile.Kill();
                    }
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            ReleaseGroundShockwaves();
            Projectile.velocity *= 0.35f;
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y, -2f);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ReleaseGroundShockwaves();

            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6, 6);
                int dustType = Main.rand.NextBool() ? DustID.GoldFlame : DustID.Silver;
                Dust d = Dust.NewDustDirect(target.Center, 0, 0, dustType, vel.X, vel.Y, 90, default, 2f);
                d.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.75f, Pitch = 0.1f }, Projectile.Center);
        }

        private void ReleaseGroundShockwaves() {
            if (HasReleasedShockwave >= 1f || Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }

            HasReleasedShockwave = 1f;
            Vector2 groundPos = FindGroundPosition(Projectile.Center);
            float direction = Math.Sign(Projectile.velocity.X);
            if (direction == 0) {
                direction = Owner.direction;
            }

            int waveDamage = (int)(Projectile.damage * 0.75f);
            float knockback = Projectile.knockBack;

            for (int wave = -2; wave <= 2; wave++) {
                Vector2 spawnPos = groundPos + new Vector2(wave * 44f, 0f);
                float scale = 0.85f + MathF.Abs(wave) * 0.12f;
                Vector2 waveVel = new Vector2(direction * (10f - MathF.Abs(wave) * 1.5f), 0f);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    spawnPos,
                    waveVel,
                    ModContent.ProjectileType<AurelianShockwave>(),
                    waveDamage,
                    knockback,
                    Projectile.owner,
                    0f,
                    scale);
            }

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.95f, Pitch = -0.05f }, groundPos);

            for (int i = 0; i < 24; i++) {
                float angle = MathHelper.TwoPi * i / 24f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 9f);
                int dustType = Main.rand.NextBool(3) ? DustID.GoldFlame : DustID.Silver;
                Dust d = Dust.NewDustDirect(groundPos, 0, 0, dustType, vel.X, vel.Y, 100, default, Main.rand.NextFloat(1.4f, 2.4f));
                d.noGravity = true;
            }

            if (Projectile.owner == Main.myPlayer) {
                Owner.GetModPlayer<ScreenShakePlayer>().ShakeScreen(8, 14);
            }
        }

        private static Vector2 FindGroundPosition(Vector2 position) {
            int tileX = (int)(position.X / 16f);
            int startY = (int)(position.Y / 16f);

            for (int y = startY; y < Main.maxTilesY - 10; y++) {
                Tile tile = Main.tile[tileX, y];
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return new Vector2(position.X, y * 16f - 4f);
                }
            }

            return position;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle frame = texture.GetRectangle();
            Vector2 origin = frame.Size() / 2f;
            float trailAlpha = 0.65f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = Color.Lerp(new Color(255, 220, 120), new Color(200, 200, 220), (float)i / Projectile.oldPos.Length);
                trailColor *= trailAlpha;
                trailAlpha *= 0.82f;

                Main.spriteBatch.Draw(texture, drawPos, frame, trailColor,
                    Projectile.oldRot[i] + MathHelper.PiOver2, origin, Projectile.scale, SpriteEffects.None, 0f);
            }

            Color glow = new Color(255, 230, 140) * 0.45f;
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, frame, glow,
                Projectile.rotation + MathHelper.PiOver2, origin, Projectile.scale * 1.15f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, frame, lightColor,
                Projectile.rotation + MathHelper.PiOver2, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>金裂地冲击波 — 贴地扩散的金纹环形震波。</summary>
    public class AurelianShockwave : ModProjectile
    {
        private ref float WaveAge => ref Projectile.ai[0];
        private ref float WaveScale => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 32;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI() {
            WaveAge++;
            Projectile.velocity.X *= 0.94f;
            Projectile.velocity.Y = 0f;

            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(18, 6),
                    0, 0, Main.rand.NextBool() ? DustID.GoldFlame : DustID.Silver,
                    0, 0, 100, default, Main.rand.NextFloat(1.2f, 1.8f));
                d.noGravity = true;
                d.velocity = Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(1.5f, 1.5f);
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.9f, 0.5f) * 0.45f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4, 4);
                Dust d = Dust.NewDustDirect(target.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 80, default, 1.6f);
                d.noGravity = true;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = 18f + WaveAge * 5f * WaveScale;
            float distX = MathF.Abs(targetHitbox.Center.X - Projectile.Center.X);
            float distY = MathF.Abs(targetHitbox.Center.Y - Projectile.Center.Y);
            return distX < radius && distY < radius * 0.45f + 12f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[ProjectileID.DD2PhoenixBowShot].Value;
            float progress = WaveAge / 32f;
            float scale = (WaveAge / 10f) * Math.Max(WaveScale, 0.5f);
            Color drawColor = Color.Lerp(new Color(255, 220, 90), new Color(210, 210, 230), progress);
            drawColor *= 1f - progress;
            drawColor.A = 0;

            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null,
                drawColor, 0f, texture.Size() / 2f, scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
