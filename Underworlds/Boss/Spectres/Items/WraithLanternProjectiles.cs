using AncientChineseMythology.Underworlds.Boss.Spectres;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Boss.Spectres.Items
{
    /// <summary>
    /// 鬼火灯笼体 — 双灯之一，环绕目标或鼠标锚点。
    /// </summary>
    public class WraithLanternGhost : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreSoul";

        public const int Lifetime = 300;
        private const float OrbitRadius = 96f;
        private const float HomeSpeed = 0.12f;

        private ref float SlotIndex => ref Projectile.ai[0];
        private ref float PartnerIndex => ref Projectile.ai[1];
        private ref float LatchNpcIndex => ref Projectile.ai[2];

        private ref float OrbitPhase => ref Projectile.localAI[0];
        private ref float PulsePhase => ref Projectile.localAI[1];

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            PulsePhase += 0.1f;
            OrbitPhase += 0.07f + SlotIndex * 0.01f;

            Vector2 anchor = GetAnchorPosition();
            float phaseOffset = SlotIndex == 0f ? 0f : MathHelper.Pi;
            Vector2 desired = anchor + new Vector2(
                MathF.Cos(OrbitPhase + phaseOffset),
                MathF.Sin(OrbitPhase + phaseOffset) * 0.65f
            ) * OrbitRadius;

            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired - Projectile.Center, HomeSpeed);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Color lightColor = SlotIndex == 0f ? SpectreHelper.SpectreCyan : SpectreHelper.SpectreYellow;
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * 0.45f);

            if (Main.rand.NextBool(3)) {
                int dustType = SlotIndex == 0f ? DustID.IceTorch : DustID.YellowTorch;
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6, 6), dustType);
                d.noGravity = true;
                d.scale = 0.9f;
                d.velocity = -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.6f, 0.6f);
                d.alpha = 120;
            }

            SyncPartnerLatch();
            SyncTetherLifetime();
        }

        private Vector2 GetAnchorPosition() {
            int npcIndex = (int)LatchNpcIndex;
            if (npcIndex >= 0 && npcIndex < Main.maxNPCs) {
                NPC latched = Main.npc[npcIndex];
                if (latched.CanBeChasedBy(Projectile)) {
                    return latched.Center;
                }
            }

            LatchNpcIndex = -1f;
            return Main.MouseWorld;
        }

        private void SyncPartnerLatch() {
            if (!TryGetPartner(out Projectile partner)) return;

            int sharedLatch = (int)LatchNpcIndex;
            if (sharedLatch < 0) {
                sharedLatch = (int)partner.ai[2];
            }

            if (sharedLatch >= 0) {
                LatchNpcIndex = sharedLatch;
                partner.ai[2] = sharedLatch;
            }
        }

        private void SyncTetherLifetime() {
            int tetherType = ModContent.ProjectileType<WraithLanternTether>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile tether = Main.projectile[i];
                if (!tether.active || tether.owner != Projectile.owner || tether.type != tetherType) continue;
                if ((int)tether.ai[0] == Projectile.whoAmI || (int)tether.ai[1] == Projectile.whoAmI) {
                    tether.timeLeft = Math.Min(tether.timeLeft, Projectile.timeLeft);
                }
            }
        }

        private bool TryGetPartner(out Projectile partner) {
            partner = null;
            int index = (int)PartnerIndex;
            if (index < 0 || index >= Main.maxProjectiles) return false;
            partner = Main.projectile[index];
            return partner.active && partner.type == Type && partner.owner == Projectile.owner;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (LatchNpcIndex >= 0f) return;

            LatchNpcIndex = target.whoAmI;
            if (TryGetPartner(out Projectile partner)) {
                partner.ai[2] = target.whoAmI;
            }

            target.AddBuff(BuffID.Frostburn, 180);
            target.AddBuff(BuffID.ShadowFlame, 180);
            SpectreHelper.CreateSpectreBurst(Projectile.Center, 36f, (int)SlotIndex, 8);
            SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.15f, Volume = 0.7f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Color core = SlotIndex == 0f ? SpectreHelper.SpectreCyan : SpectreHelper.SpectreYellow;
            Color glow = SlotIndex == 0f ? SpectreHelper.SpectreDeepCyan : SpectreHelper.SpectreGold;
            SpectreHelper.DrawSpectreCore(Main.spriteBatch, Projectile.Center, core, glow, 0.55f, PulsePhase);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            SpectreHelper.CreateSpectreBurst(Projectile.Center, 28f, (int)SlotIndex, 6);
        }
    }

    /// <summary>
    /// 怨灵锁链 — 连接双鬼火灯笼，链身持续灼烧接触的敌人。
    /// </summary>
    public class WraithLanternTether : ModProjectile
    {
        public override string Texture => SpectreHelper.Path + "SpectreCore";

        private const float ChainWidth = 24f;

        private ref float LanternAIndex => ref Projectile.ai[0];
        private ref float LanternBIndex => ref Projectile.ai[1];

        private float PulsePhase;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = WraithLanternGhost.Lifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 16;
        }

        private bool TryGetLanterns(out Projectile a, out Projectile b) {
            a = b = null;
            int ia = (int)LanternAIndex;
            int ib = (int)LanternBIndex;
            if (ia < 0 || ia >= Main.maxProjectiles || ib < 0 || ib >= Main.maxProjectiles) return false;

            a = Main.projectile[ia];
            b = Main.projectile[ib];
            int ghostType = ModContent.ProjectileType<WraithLanternGhost>();
            return a.active && b.active && a.type == ghostType && b.type == ghostType;
        }

        public override void AI() {
            if (!TryGetLanterns(out Projectile a, out Projectile b)) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = (a.Center + b.Center) * 0.5f;
            Projectile.timeLeft = Math.Min(a.timeLeft, b.timeLeft);
            PulsePhase += 0.14f;

            if (Main.netMode == NetmodeID.Server) return;

            Vector2 dir = b.Center - a.Center;
            float len = dir.Length();
            if (len < 4f) return;

            dir /= len;
            SpectreHelper.CreateSoulChainParticles(a.Center, b.Center, 0.35f);

            Color chainLight = Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow, 0.5f);
            Lighting.AddLight(Projectile.Center, chainLight.ToVector3() * 0.35f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!TryGetLanterns(out Projectile a, out Projectile b)) return false;

            float point = 0f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(), targetHitbox.Size(), a.Center, b.Center, ChainWidth, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn, 90);
            target.AddBuff(BuffID.ShadowFlame, 90);

            if (Main.netMode == NetmodeID.Server) return;

            Vector2 along = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(
                    target.Center + Main.rand.NextVector2Circular(10, 10),
                    Main.rand.NextBool() ? DustID.IceTorch : DustID.YellowTorch,
                    along.RotatedByRandom(0.8f) * Main.rand.NextFloat(1f, 3f)
                );
                d.noGravity = true;
                d.scale = 1.1f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!TryGetLanterns(out Projectile a, out Projectile b)) return false;

            Color chainColor = Color.Lerp(SpectreHelper.SpectreCyan, SpectreHelper.SpectreYellow,
                0.5f + MathF.Sin(PulsePhase) * 0.2f);
            SpectreHelper.DrawSoulChain(Main.spriteBatch, a.Center, b.Center, chainColor, 7f, PulsePhase * 60f);

            var tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float pulse = 1f + MathF.Sin(PulsePhase) * 0.12f;
            Color nodeColor = chainColor;
            nodeColor.A = 180;

            Main.spriteBatch.Draw(tex, a.Center - Main.screenPosition, null, nodeColor * 0.35f,
                PulsePhase, origin, 0.35f * pulse, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(tex, b.Center - Main.screenPosition, null, nodeColor * 0.35f,
                -PulsePhase, origin, 0.35f * pulse, SpriteEffects.None, 0);

            return false;
        }
    }
}
