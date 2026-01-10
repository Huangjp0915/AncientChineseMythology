using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Underworlds.Enemys
{
    /// <summary>
    /// 死者 - 地府幽魂敌怪，飞行类，发射幽灵球
    /// </summary>
    public class TheDeceasedPerson : ModNPC
    {
        #region 常量
        private const float FlySpeed = 3f;
        private const float DetectionRange = 600f;
        private const float AttackRange = 400f;
        private const int AttackCooldownMax = 90;
        private const int FrameCount = 1;
        private const int FrameDuration = 8;
        #endregion

        #region 状态
        private enum AIState
        {
            Wander,
            Alert,
            Attack
        }

        private AIState State {
            get => (AIState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        private ref float StateTimer => ref NPC.ai[1];
        private ref float AttackCooldown => ref NPC.ai[2];
        private ref float WanderAngle => ref NPC.ai[3];

        private float pulseTimer = 0f;
        private float ghostAlpha = 0.7f;
        private int frameTimer = 0;
        #endregion

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = FrameCount;
            NPCID.Sets.CantTakeLunchMoney[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 36;
            NPC.height = 48;
            NPC.damage = 45;
            NPC.defense = 12;
            NPC.lifeMax = 280;
            NPC.knockBackResist = 0.4f;
            NPC.value = Item.buyPrice(silver: 35);
            NPC.HitSound = SoundID.NPCHit54;
            NPC.DeathSound = SoundID.NPCDeath52;

            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;

            NPC.alpha = 50;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                new FlavorTextBestiaryInfoElement("死者的亡魂，游荡于地府之中。它们已失去生前的记忆，只剩下对生者的怨恨，会发射幽冷的灵魂能量攻击入侵者。")
            ]);
        }

        public override void AI() {
            NPC.TargetClosest(false);
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.velocity.Y -= 0.05f;
                NPC.EncourageDespawn(150);
                return;
            }

            StateTimer++;
            pulseTimer += 0.04f;
            if (AttackCooldown > 0) AttackCooldown--;

            ghostAlpha = 0.6f + MathF.Sin(pulseTimer * 2f) * 0.2f;
            NPC.alpha = (int)(255 * (1f - ghostAlpha));

            float glowPulse = 0.3f + MathF.Sin(pulseTimer * 3f) * 0.2f;
            Lighting.AddLight(NPC.Center, new Vector3(0.5f, 0.7f, 1f) * glowPulse);

            float distToTarget = Vector2.Distance(NPC.Center, target.Center);

            NPC.spriteDirection = target.Center.X < NPC.Center.X ? -1 : 1;

            switch (State) {
                case AIState.Wander:
                    RunWanderAI(target, distToTarget);
                    break;
                case AIState.Alert:
                    RunAlertAI(target, distToTarget);
                    break;
                case AIState.Attack:
                    RunAttackAI(target, distToTarget);
                    break;
            }

            if (Main.rand.NextBool(15)) {
                int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.SpectreStaff, 0, -1, 150, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0.3f;
            }
        }

        private void RunWanderAI(Player target, float distance) {
            WanderAngle += 0.02f;
            NPC.velocity.X = MathF.Cos(WanderAngle) * 1.5f;
            NPC.velocity.Y = MathF.Sin(WanderAngle * 0.7f) * 1f;

            if (distance < DetectionRange) {
                State = AIState.Alert;
                StateTimer = 0;
                SoundEngine.PlaySound(SoundID.NPCHit54 with { Pitch = 0.5f, Volume = 0.5f }, NPC.Center);
            }
        }

        private void RunAlertAI(Player target, float distance) {
            Vector2 toTarget = target.Center - NPC.Center;
            Vector2 dir = toTarget.SafeNormalize(Vector2.Zero);

            Vector2 hoverPos = target.Center + new Vector2(0, -150);
            Vector2 toHover = hoverPos - NPC.Center;

            if (distance > AttackRange * 0.5f) {
                NPC.velocity = Vector2.Lerp(NPC.velocity, toHover.SafeNormalize(Vector2.Zero) * FlySpeed, 0.05f);
            }
            else {
                NPC.velocity *= 0.95f;
            }

            if (AttackCooldown <= 0 && distance <= AttackRange) {
                State = AIState.Attack;
                StateTimer = 0;
            }

            if (StateTimer % 8 == 0) {
                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.SpectreStaff, 0, 0, 100, default, 1.3f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(2f, 2f);
            }

            if (distance > DetectionRange * 1.5f) {
                State = AIState.Wander;
                StateTimer = 0;
            }
        }

        private void RunAttackAI(Player target, float distance) {
            NPC.velocity *= 0.92f;

            if (StateTimer < 25) {
                if (StateTimer % 2 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * (25 - StateTimer);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.SpectreStaff, 0, 0, 100, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 2f;
                }
            }
            else if (StateTimer == 25) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                    Vector2 direction = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 vel = direction * 8f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<GhostOrbProjectile>(), NPC.damage / 2, 1f, Main.myPlayer);
                }

                for (int i = 0; i < 12; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.SpectreStaff, vel.X, vel.Y, 100, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (StateTimer > 40) {
                State = AIState.Alert;
                StateTimer = 0;
                AttackCooldown = AttackCooldownMax;
            }
        }

        public override void FindFrame(int frameHeight) {
            if (++frameTimer >= FrameDuration) {
                frameTimer = 0;
                NPC.frame.Y += frameHeight;
                if (NPC.frame.Y >= frameHeight * FrameCount)
                    NPC.frame.Y = 0;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;
            Vector2 origin = new Vector2(NPC.frame.Width / 2f, NPC.frame.Height / 2f);
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Color glowColor = new Color(100, 150, 255, 0) * ghostAlpha * 0.3f;
            spriteBatch.Draw(texture, drawPos, NPC.frame, glowColor, NPC.rotation, origin, NPC.scale * 1.15f, effects, 0f);

            Color mainColor = drawColor * ghostAlpha;
            spriteBatch.Draw(texture, drawPos, NPC.frame, mainColor, NPC.rotation, origin, NPC.scale, effects, 0f);

            return false;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 5; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.SpectreStaff, hit.HitDirection * 2, -1, 100, default, 1.3f);
            }

            if (NPC.life <= 0) {
                for (int i = 0; i < 20; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.SpectreStaff, vel.X, vel.Y, 100, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.NPCDeath52, NPC.Center);
            }
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ItemID.Ectoplasm, 8, 1, 2));
            npcLoot.Add(ItemDropRule.Common(ItemID.SoulofNight, 5, 1, 2));
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            return 0f;
        }
    }

    /// <summary>
    /// 幽灵球投射物
    /// </summary>
    public class GhostOrbProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LostSoulFriendly;

        private float pulsePhase = 0f;
        private float homingStrength = 0.02f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.alpha = 80;
        }

        public override void AI() {
            pulsePhase += 0.1f;
            Projectile.rotation += 0.1f;

            Player target = null;
            float closestDist = 600f;
            foreach (var p in Main.player) {
                if (p != null && p.active && !p.dead) {
                    float dist = p.Distance(Projectile.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        target = p;
                    }
                }
            }

            if (target != null) {
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 10f, homingStrength);
            }

            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.SpectreStaff, 0, 0, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.15f;
            }

            float lightPulse = 0.4f + MathF.Sin(pulsePhase) * 0.15f;
            Lighting.AddLight(Projectile.Center, new Color(100, 150, 255).ToVector3() * lightPulse);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(100, 150, 255) * progress * 0.4f;
                trailColor.A = 0;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                sb.Draw(texture, drawPos, null, trailColor, Projectile.oldRot[i], origin, progress, SpriteEffects.None, 0);
            }

            Color glowColor = new Color(150, 200, 255, 0) * 0.5f;
            sb.Draw(texture, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, origin, 1.3f, SpriteEffects.None, 0);
            sb.Draw(texture, Projectile.Center - Main.screenPosition, null, Color.White * 0.9f, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Chilled, 120);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.6f }, Projectile.Center);
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.SpectreStaff, vel.X, vel.Y, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
