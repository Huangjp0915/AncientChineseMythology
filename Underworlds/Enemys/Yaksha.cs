using AncientChineseMythology.Underworlds.Items;
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
    /// 夜叉 - 地府飞行敌怪，发射地狱火球
    /// </summary>
    public class Yaksha : ModNPC
    {
        #region 常量
        private const float FlySpeed = 4f;
        private const float DetectionRange = 500f;
        private const float AttackRange = 350f;
        private const int AttackCooldownMax = 120;
        private const int FrameCount = 1;
        private const int FrameDuration = 6;
        #endregion

        #region 状态
        private enum AIState
        {
            Idle,
            Chase,
            Attack,
            Retreat
        }

        private AIState State {
            get => (AIState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        private ref float StateTimer => ref NPC.ai[1];
        private ref float AttackCooldown => ref NPC.ai[2];
        private ref float HoverOffset => ref NPC.ai[3];

        private float pulseTimer = 0f;
        private float glowIntensity = 0f;
        private int frameTimer = 0;
        #endregion

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = FrameCount;
            NPCID.Sets.CantTakeLunchMoney[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 40;
            NPC.height = 50;
            NPC.damage = 55;
            NPC.defense = 20;
            NPC.lifeMax = 350;
            NPC.knockBackResist = 0.3f;
            NPC.value = Item.buyPrice(silver: 50);
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath6;

            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;

            NPC.lavaImmune = true;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                new FlavorTextBestiaryInfoElement("夜叉，地府的恶鬼守卫。身形敏捷，能吐出炙热的地狱火焰，焚烧一切胆敢入侵地府的生灵。")
            ]);
        }

        public override void AI() {
            NPC.TargetClosest(false);
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.velocity.Y -= 0.1f;
                NPC.EncourageDespawn(120);
                return;
            }

            StateTimer++;
            pulseTimer += 0.05f;
            if (AttackCooldown > 0) AttackCooldown--;

            HoverOffset = MathF.Sin(pulseTimer * 2f) * 15f;

            float glowPulse = 0.5f + MathF.Sin(pulseTimer * 3f) * 0.3f;
            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.4f, 0.1f) * (0.5f + glowIntensity * 0.5f) * glowPulse);

            float distToTarget = Vector2.Distance(NPC.Center, target.Center);

            NPC.spriteDirection = target.Center.X < NPC.Center.X ? -1 : 1;

            switch (State) {
                case AIState.Idle:
                    RunIdleAI(target, distToTarget);
                    break;
                case AIState.Chase:
                    RunChaseAI(target, distToTarget);
                    break;
                case AIState.Attack:
                    RunAttackAI(target, distToTarget);
                    break;
                case AIState.Retreat:
                    RunRetreatAI(target, distToTarget);
                    break;
            }

            float targetGlow = State == AIState.Attack ? 1f : (State == AIState.Chase ? 0.5f : 0.2f);
            glowIntensity = MathHelper.Lerp(glowIntensity, targetGlow, 0.1f);

            if (Main.rand.NextBool(10)) {
                int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Torch, 0, -2, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        private void RunIdleAI(Player target, float distance) {
            NPC.velocity.Y = MathF.Sin(pulseTimer) * 0.5f;
            NPC.velocity.X *= 0.95f;

            if (distance < DetectionRange) {
                State = AIState.Chase;
                StateTimer = 0;
            }
        }

        private void RunChaseAI(Player target, float distance) {
            Vector2 toTarget = target.Center - NPC.Center;
            Vector2 dir = toTarget.SafeNormalize(Vector2.Zero);

            if (distance > AttackRange) {
                NPC.velocity = Vector2.Lerp(NPC.velocity, dir * FlySpeed, 0.08f);
            }
            else {
                NPC.velocity *= 0.95f;
                if (AttackCooldown <= 0) {
                    State = AIState.Attack;
                    StateTimer = 0;
                }
            }

            if (distance > DetectionRange * 1.5f) {
                State = AIState.Idle;
                StateTimer = 0;
            }
        }

        private void RunAttackAI(Player target, float distance) {
            NPC.velocity *= 0.9f;

            if (StateTimer < 30) {
                if (StateTimer % 3 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * (30 - StateTimer);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Torch, 0, 0, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                }
            }
            else if (StateTimer == 30) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    SoundEngine.PlaySound(SoundID.Item20, NPC.Center);
                    Vector2 direction = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);

                    for (int i = 0; i < 3; i++) {
                        Vector2 vel = direction.RotatedBy((i - 1) * 0.15f) * 10f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<YakshaFireball>(), NPC.damage / 2, 1f, Main.myPlayer);
                    }
                }

                for (int i = 0; i < 15; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Torch, vel.X, vel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (StateTimer > 50) {
                State = AIState.Retreat;
                StateTimer = 0;
                AttackCooldown = AttackCooldownMax;
            }
        }

        private void RunRetreatAI(Player target, float distance) {
            Vector2 awayFromTarget = (NPC.Center - target.Center).SafeNormalize(Vector2.Zero);
            NPC.velocity = Vector2.Lerp(NPC.velocity, awayFromTarget * FlySpeed * 1.5f, 0.1f);

            if (StateTimer > 40) {
                State = AIState.Chase;
                StateTimer = 0;
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
            Vector2 drawPos = NPC.Center - screenPos + new Vector2(0, HoverOffset);
            Vector2 origin = new Vector2(NPC.frame.Width / 2f, NPC.frame.Height / 2f);
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            if (glowIntensity > 0.2f) {
                Color glowColor = new Color(255, 100, 50, 0) * glowIntensity * 0.4f;
                spriteBatch.Draw(texture, drawPos, NPC.frame, glowColor, NPC.rotation, origin, NPC.scale * 1.1f, effects, 0f);
            }

            spriteBatch.Draw(texture, drawPos, NPC.frame, drawColor, NPC.rotation, origin, NPC.scale, effects, 0f);

            return false;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 5; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Torch, hit.HitDirection * 2, -1, 100, default, 1.5f);
            }

            if (NPC.life <= 0) {
                for (int i = 0; i < 20; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                    Dust.NewDust(NPC.Center, 0, 0, DustID.Torch, vel.X, vel.Y, 100, default, 2f);
                }
                SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
            }
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ItemID.Hellstone, 4, 2, 5));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<AncientChineseMythology.Items.Materials.DiHuo>(), 3, 1, 2));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SoulFragment>(), 2));
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            return 0f;
        }
    }

    /// <summary>
    /// 夜叉地狱火球投射物
    /// </summary>
    public class YakshaFireball : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Fireball;

        private float pulsePhase = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.alpha = 100;
        }

        public override void AI() {
            pulsePhase += 0.15f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0, 0, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.2f;
            }

            float lightPulse = 0.6f + MathF.Sin(pulsePhase) * 0.2f;
            Lighting.AddLight(Projectile.Center, new Color(255, 100, 50).ToVector3() * lightPulse);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(255, 100, 50) * progress * 0.5f;
                trailColor.A = 0;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                sb.Draw(texture, drawPos, null, trailColor, Projectile.oldRot[i], origin, progress * 1.2f, SpriteEffects.None, 0);
            }

            sb.Draw(texture, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire, 180);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f }, Projectile.Center);
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Torch, vel.X, vel.Y, 100, default, 2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
