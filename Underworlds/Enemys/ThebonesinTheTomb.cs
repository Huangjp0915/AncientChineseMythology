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
    /// 墓中骸骨 - 地府飞行骷髅，体术冲刺并发射骨头
    /// </summary>
    public class ThebonesinTheTomb : ModNPC
    {
        #region 常量
        private const float FlySpeed = 5f;
        private const float DashSpeed = 15f;
        private const float DetectionRange = 550f;
        private const float MeleeRange = 150f;
        private const int DashCooldownMax = 150;
        private const int BoneAttackCooldownMax = 80;
        private const int FrameCount = 1;
        private const int FrameDuration = 5;
        #endregion

        #region 状态
        private enum AIState
        {
            Idle,
            Chase,
            Dash,
            BoneAttack,
            Recovery
        }

        private AIState State {
            get => (AIState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        private ref float StateTimer => ref NPC.ai[1];
        private ref float DashCooldown => ref NPC.ai[2];
        private ref float BoneAttackCooldown => ref NPC.ai[3];

        private float pulseTimer = 0f;
        private Vector2 dashDirection = Vector2.Zero;
        private int frameTimer = 0;
        private float boneGlowIntensity = 0f;
        #endregion

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = FrameCount;
            NPCID.Sets.CantTakeLunchMoney[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 38;
            NPC.height = 52;
            NPC.damage = 60;
            NPC.defense = 25;
            NPC.lifeMax = 420;
            NPC.knockBackResist = 0.25f;
            NPC.value = Item.buyPrice(silver: 60);
            NPC.HitSound = SoundID.NPCHit2;
            NPC.DeathSound = SoundID.NPCDeath2;

            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                new FlavorTextBestiaryInfoElement("墓中骸骨，古老坟墓中复活的骷髅战士。它们保留着生前的战斗本能，能够高速冲刺攻击敌人，并投掷锋利的骨刺。")
            ]);
        }

        public override void AI() {
            NPC.TargetClosest(false);
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.velocity *= 0.95f;
                NPC.EncourageDespawn(120);
                return;
            }

            StateTimer++;
            pulseTimer += 0.05f;
            if (DashCooldown > 0) DashCooldown--;
            if (BoneAttackCooldown > 0) BoneAttackCooldown--;

            float glowPulse = 0.3f + MathF.Sin(pulseTimer * 2f) * 0.15f;
            Lighting.AddLight(NPC.Center, new Vector3(0.8f, 0.9f, 1f) * glowPulse);

            float distToTarget = Vector2.Distance(NPC.Center, target.Center);

            if (State != AIState.Dash) {
                NPC.spriteDirection = target.Center.X < NPC.Center.X ? -1 : 1;
            }

            switch (State) {
                case AIState.Idle:
                    RunIdleAI(target, distToTarget);
                    break;
                case AIState.Chase:
                    RunChaseAI(target, distToTarget);
                    break;
                case AIState.Dash:
                    RunDashAI(target, distToTarget);
                    break;
                case AIState.BoneAttack:
                    RunBoneAttackAI(target, distToTarget);
                    break;
                case AIState.Recovery:
                    RunRecoveryAI(target, distToTarget);
                    break;
            }

            float targetGlow = State == AIState.Dash ? 1f : (State == AIState.BoneAttack ? 0.8f : 0.3f);
            boneGlowIntensity = MathHelper.Lerp(boneGlowIntensity, targetGlow, 0.1f);

            if (State == AIState.Dash && Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone, -NPC.velocity.X * 0.2f, -NPC.velocity.Y * 0.2f, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        private void RunIdleAI(Player target, float distance) {
            NPC.velocity *= 0.95f;
            NPC.velocity.Y += MathF.Sin(pulseTimer) * 0.05f;

            if (distance < DetectionRange) {
                State = AIState.Chase;
                StateTimer = 0;
            }
        }

        private void RunChaseAI(Player target, float distance) {
            Vector2 toTarget = target.Center - NPC.Center;
            Vector2 dir = toTarget.SafeNormalize(Vector2.Zero);

            NPC.velocity = Vector2.Lerp(NPC.velocity, dir * FlySpeed, 0.06f);

            if (DashCooldown <= 0 && distance < MeleeRange * 2f && distance > MeleeRange * 0.5f) {
                State = AIState.Dash;
                StateTimer = 0;
                dashDirection = dir;
                SoundEngine.PlaySound(SoundID.DD2_SkeletonHurt with { Pitch = 0.3f }, NPC.Center);
            }
            else if (BoneAttackCooldown <= 0 && distance > MeleeRange * 1.5f && distance < DetectionRange * 0.8f) {
                State = AIState.BoneAttack;
                StateTimer = 0;
            }

            if (distance > DetectionRange * 1.5f) {
                State = AIState.Idle;
                StateTimer = 0;
            }
        }

        private void RunDashAI(Player target, float distance) {
            if (StateTimer < 15) {
                NPC.velocity *= 0.9f;
                dashDirection = (target.Center - NPC.Center).SafeNormalize(dashDirection);

                if (StateTimer % 3 == 0) {
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Bone, 0, 0, 100, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                }
            }
            else if (StateTimer == 15) {
                NPC.velocity = dashDirection * DashSpeed;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Pitch = 0.5f }, NPC.Center);

                for (int i = 0; i < 10; i++) {
                    Vector2 dustVel = -dashDirection.RotatedByRandom(0.3f) * Main.rand.NextFloat(3f, 6f);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Bone, dustVel.X, dustVel.Y, 100, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                }
            }
            else if (StateTimer < 35) {
                NPC.velocity *= 0.97f;
            }
            else {
                State = AIState.Recovery;
                StateTimer = 0;
                DashCooldown = DashCooldownMax;
            }
        }

        private void RunBoneAttackAI(Player target, float distance) {
            NPC.velocity *= 0.92f;

            if (StateTimer < 20) {
                if (StateTimer % 4 == 0) {
                    int dust = Dust.NewDust(NPC.Center + new Vector2(NPC.spriteDirection * 15, -10), 0, 0, DustID.Bone, 0, 0, 100, default, 1.3f);
                    Main.dust[dust].noGravity = true;
                }
            }
            else if (StateTimer == 20) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    SoundEngine.PlaySound(SoundID.Item17, NPC.Center);
                    Vector2 direction = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);

                    for (int i = 0; i < 3; i++) {
                        Vector2 vel = direction.RotatedBy((i - 1) * 0.2f) * 12f;
                        vel += Main.rand.NextVector2Circular(1f, 1f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<BoneSpike>(), NPC.damage / 3, 2f, Main.myPlayer);
                    }
                }

                for (int i = 0; i < 8; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Bone, vel.X, vel.Y, 100, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (StateTimer > 35) {
                State = AIState.Chase;
                StateTimer = 0;
                BoneAttackCooldown = BoneAttackCooldownMax;
            }
        }

        private void RunRecoveryAI(Player target, float distance) {
            NPC.velocity *= 0.9f;

            if (StateTimer > 25) {
                State = AIState.Chase;
                StateTimer = 0;
            }
        }

        public override void FindFrame(int frameHeight) {
            int speed = State == AIState.Dash ? 3 : FrameDuration;
            if (++frameTimer >= speed) {
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

            if (State == AIState.Dash) {
                for (int i = 0; i < 5; i++) {
                    float progress = (float)i / 5f;
                    Vector2 trailPos = drawPos + NPC.velocity * (-i * 0.8f);
                    Color trailColor = drawColor * (1f - progress) * 0.4f;
                    spriteBatch.Draw(texture, trailPos, NPC.frame, trailColor, NPC.rotation, origin, NPC.scale, effects, 0f);
                }
            }

            if (boneGlowIntensity > 0.3f) {
                Color glowColor = new Color(200, 220, 255, 0) * (boneGlowIntensity - 0.3f) * 0.5f;
                spriteBatch.Draw(texture, drawPos, NPC.frame, glowColor, NPC.rotation, origin, NPC.scale * 1.1f, effects, 0f);
            }

            spriteBatch.Draw(texture, drawPos, NPC.frame, drawColor, NPC.rotation, origin, NPC.scale, effects, 0f);

            return false;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 5; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Bone, hit.HitDirection * 2, -1, 100, default, 1.3f);
            }

            if (NPC.life <= 0) {
                for (int i = 0; i < 25; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Bone, vel.X, vel.Y, 100, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.NPCDeath2, NPC.Center);

                for (int i = 0; i < 3; i++) {
                    Gore.NewGore(NPC.GetSource_Death(), NPC.Center, Main.rand.NextVector2Circular(3f, 3f), GoreID.Smoke1);
                }
            }
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<AncientChineseMythology.Items.Materials.Bone>(), 1, 3, 8));
            npcLoot.Add(ItemDropRule.Common(ItemID.Bone, 2, 5, 12));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SoulFragment>(), 2));
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            return 0f;
        }
    }

    /// <summary>
    /// 骨刺投射物
    /// </summary>
    public class BoneSpike : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BoneJavelin;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 150;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Projectile.velocity.Y += 0.15f;
            if (Projectile.velocity.Y > 16f)
                Projectile.velocity.Y = 16f;

            if (Main.rand.NextBool(5)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Bone, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = lightColor * progress * 0.5f;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                sb.Draw(texture, drawPos, null, trailColor, Projectile.oldRot[i], origin, progress * 0.8f, SpriteEffects.None, 0);
            }

            sb.Draw(texture, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3f, 3f);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Bone, vel.X, vel.Y, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
