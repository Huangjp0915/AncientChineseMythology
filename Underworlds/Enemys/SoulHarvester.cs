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
    /// 摄魂使者 - 死神风格的地府敌怪，飞行类，发射死神镰刀
    /// </summary>
    public class SoulHarvester : ModNPC
    {
        #region 常量
        private const float FlySpeed = 3.5f;
        private const float DetectionRange = 650f;
        private const float AttackRange = 450f;
        private const int SickleAttackCooldownMax = 100;
        private const int SweepAttackCooldownMax = 180;
        private const int FrameCount = 1;
        private const int FrameDuration = 7;
        #endregion

        #region 状态
        private enum AIState
        {
            Lurk,
            Approach,
            SickleThrow,
            SweepAttack,
            Fade
        }

        private AIState State {
            get => (AIState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        private ref float StateTimer => ref NPC.ai[1];
        private ref float SickleCooldown => ref NPC.ai[2];
        private ref float SweepCooldown => ref NPC.ai[3];

        private float pulseTimer = 0f;
        private float deathAura = 0f;
        private float fadeAlpha = 1f;
        private int frameTimer = 0;
        #endregion

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = FrameCount;
            NPCID.Sets.CantTakeLunchMoney[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 44;
            NPC.height = 60;
            NPC.damage = 70;
            NPC.defense = 30;
            NPC.lifeMax = 550;
            NPC.knockBackResist = 0.15f;
            NPC.value = Item.buyPrice(gold: 1);
            NPC.HitSound = SoundID.NPCHit36;
            NPC.DeathSound = SoundID.NPCDeath39;

            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;

            NPC.alpha = 30;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                new FlavorTextBestiaryInfoElement("摄魂使者，地府中收割亡魂的死神。手持幽冥镰刀，能从远处投掷致命的镰刃，也能进行近距离的死亡横扫。据说看到它出现就意味着死期将至。")
            ]);
        }

        public override void AI() {
            NPC.TargetClosest(false);
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                fadeAlpha -= 0.02f;
                if (fadeAlpha <= 0) {
                    NPC.active = false;
                }
                NPC.alpha = (int)(255 * (1f - fadeAlpha));
                return;
            }

            StateTimer++;
            pulseTimer += 0.04f;
            if (SickleCooldown > 0) SickleCooldown--;
            if (SweepCooldown > 0) SweepCooldown--;

            float glowPulse = 0.4f + MathF.Sin(pulseTimer * 2f) * 0.2f;
            Lighting.AddLight(NPC.Center, new Vector3(0.6f, 0.3f, 0.8f) * (glowPulse + deathAura * 0.3f));

            float distToTarget = Vector2.Distance(NPC.Center, target.Center);

            NPC.spriteDirection = target.Center.X < NPC.Center.X ? -1 : 1;

            switch (State) {
                case AIState.Lurk:
                    RunLurkAI(target, distToTarget);
                    break;
                case AIState.Approach:
                    RunApproachAI(target, distToTarget);
                    break;
                case AIState.SickleThrow:
                    RunSickleThrowAI(target, distToTarget);
                    break;
                case AIState.SweepAttack:
                    RunSweepAttackAI(target, distToTarget);
                    break;
                case AIState.Fade:
                    RunFadeAI(target, distToTarget);
                    break;
            }

            float targetAura = State == AIState.SweepAttack ? 1f : (State == AIState.SickleThrow ? 0.7f : 0.3f);
            deathAura = MathHelper.Lerp(deathAura, targetAura, 0.08f);

            if (Main.rand.NextBool(12)) {
                int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Shadowflame, 0, -1, 150, default, 1.3f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0.4f;
            }

            NPC.alpha = (int)(255 * (1f - fadeAlpha));
        }

        private void RunLurkAI(Player target, float distance) {
            NPC.velocity.Y = MathF.Sin(pulseTimer * 0.8f) * 0.8f;
            NPC.velocity.X *= 0.95f;

            fadeAlpha = MathHelper.Lerp(fadeAlpha, 0.5f, 0.02f);

            if (distance < DetectionRange) {
                State = AIState.Approach;
                StateTimer = 0;
                fadeAlpha = 1f;
                SoundEngine.PlaySound(SoundID.NPCHit36 with { Pitch = -0.3f, Volume = 0.7f }, NPC.Center);
            }
        }

        private void RunApproachAI(Player target, float distance) {
            fadeAlpha = MathHelper.Lerp(fadeAlpha, 1f, 0.05f);

            Vector2 targetPos = target.Center + new Vector2(target.Center.X < NPC.Center.X ? 200 : -200, -100);
            Vector2 toTarget = targetPos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget.SafeNormalize(Vector2.Zero) * FlySpeed, 0.04f);

            if (SweepCooldown <= 0 && distance < 200f) {
                State = AIState.SweepAttack;
                StateTimer = 0;
            }
            else if (SickleCooldown <= 0 && distance < AttackRange && distance > 150f) {
                State = AIState.SickleThrow;
                StateTimer = 0;
            }

            if (StateTimer % 10 == 0) {
                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Shadowflame, 0, 0, 100, default, 1f);
                Main.dust[dust].noGravity = true;
            }

            if (distance > DetectionRange * 1.5f) {
                State = AIState.Fade;
                StateTimer = 0;
            }
        }

        private void RunSickleThrowAI(Player target, float distance) {
            NPC.velocity *= 0.9f;

            if (StateTimer < 35) {
                if (StateTimer % 3 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * (35 - StateTimer);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.Shadowflame, 0, 0, 100, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                }
            }
            else if (StateTimer == 35) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
                    Vector2 direction = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 vel = direction * 14f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<SoulSickle>(), NPC.damage / 2, 3f, Main.myPlayer, NPC.whoAmI);
                }

                for (int i = 0; i < 15; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Shadowflame, vel.X, vel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (StateTimer > 55) {
                State = AIState.Approach;
                StateTimer = 0;
                SickleCooldown = SickleAttackCooldownMax;
            }
        }

        private void RunSweepAttackAI(Player target, float distance) {
            if (StateTimer < 20) {
                NPC.velocity *= 0.85f;

                if (StateTimer % 2 == 0) {
                    for (int i = 0; i < 3; i++) {
                        float angle = pulseTimer + i * MathHelper.TwoPi / 3f;
                        Vector2 dustPos = NPC.Center + angle.ToRotationVector2() * (40 + StateTimer);
                        int dust = Dust.NewDust(dustPos, 0, 0, DustID.Shadowflame, 0, 0, 100, default, 1.5f);
                        Main.dust[dust].noGravity = true;
                    }
                }
            }
            else if (StateTimer == 20) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f }, NPC.Center);

                    for (int i = 0; i < 8; i++) {
                        float angle = MathHelper.TwoPi * i / 8f;
                        Vector2 vel = angle.ToRotationVector2() * 10f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<SoulSickle>(), NPC.damage / 3, 2f, Main.myPlayer, NPC.whoAmI);
                    }
                }

                for (int i = 0; i < 25; i++) {
                    float angle = MathHelper.TwoPi * i / 25f;
                    Vector2 vel = angle.ToRotationVector2() * 8f;
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Shadowflame, vel.X, vel.Y, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (StateTimer > 45) {
                State = AIState.Fade;
                StateTimer = 0;
                SweepCooldown = SweepAttackCooldownMax;
            }
        }

        private void RunFadeAI(Player target, float distance) {
            fadeAlpha = MathHelper.Lerp(fadeAlpha, 0.3f, 0.03f);
            NPC.velocity *= 0.95f;

            Vector2 awayDir = (NPC.Center - target.Center).SafeNormalize(Vector2.Zero);
            NPC.velocity += awayDir * 0.2f;

            if (StateTimer > 60) {
                if (distance < DetectionRange) {
                    State = AIState.Approach;
                }
                else {
                    State = AIState.Lurk;
                }
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
            Vector2 drawPos = NPC.Center - screenPos;
            Vector2 origin = new Vector2(NPC.frame.Width / 2f, NPC.frame.Height / 2f);
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Color auraColor = new Color(100, 50, 150, 0) * deathAura * 0.4f;
            for (int i = 0; i < 3; i++) {
                float angle = pulseTimer + i * MathHelper.TwoPi / 3f;
                Vector2 offset = angle.ToRotationVector2() * (5f + deathAura * 5f);
                spriteBatch.Draw(texture, drawPos + offset, NPC.frame, auraColor, NPC.rotation, origin, NPC.scale * 1.05f, effects, 0f);
            }

            Color mainColor = drawColor * fadeAlpha;
            spriteBatch.Draw(texture, drawPos, NPC.frame, mainColor, NPC.rotation, origin, NPC.scale, effects, 0f);

            if (deathAura > 0.5f) {
                Color eyeGlow = new Color(200, 100, 255, 0) * (deathAura - 0.5f);
                Vector2 eyeOffset = new Vector2(NPC.spriteDirection * 8, -12);
                spriteBatch.Draw(TextureAssets.Extra[ExtrasID.SharpTears].Value, drawPos + eyeOffset, null, eyeGlow, 0f,
                    TextureAssets.Extra[ExtrasID.SharpTears].Value.Size() / 2f, 0.3f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 6; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Shadowflame, hit.HitDirection * 2, -1, 100, default, 1.5f);
            }

            if (NPC.life <= 0) {
                for (int i = 0; i < 30; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(7f, 7f);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.Shadowflame, vel.X, vel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.NPCDeath39, NPC.Center);

                for (int i = 0; i < 4; i++) {
                    Gore.NewGore(NPC.GetSource_Death(), NPC.Center, Main.rand.NextVector2Circular(4f, 4f), GoreID.Smoke2);
                }
            }
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ItemID.SoulofNight, 3, 2, 4));
            npcLoot.Add(ItemDropRule.Common(ItemID.DeathSickle, 50));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<AncientChineseMythology.Items.Materials.DiHuo>(), 4, 1, 3));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SoulFragment>(), 2));
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            return 0f;
        }
    }

    /// <summary>
    /// 灵魂镰刀投射物
    /// </summary>
    public class SoulSickle : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DeathSickle;

        private float pulsePhase = 0f;
        private float spinSpeed = 0.25f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.alpha = 50;
        }

        public override void AI() {
            pulsePhase += 0.1f;
            Projectile.rotation += spinSpeed;
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.15f, 0.01f);

            if (Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0, 0, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.1f;
            }

            float lightPulse = 0.5f + MathF.Sin(pulsePhase) * 0.2f;
            Lighting.AddLight(Projectile.Center, new Color(150, 80, 200).ToVector3() * lightPulse);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (float)i / Projectile.oldPos.Length;
                Color trailColor = new Color(150, 80, 200) * progress * 0.4f;
                trailColor.A = 0;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                float trailScale = progress * 1.2f;
                sb.Draw(texture, drawPos, null, trailColor, Projectile.oldRot[i], origin, trailScale, SpriteEffects.None, 0);
            }

            Color glowColor = new Color(200, 120, 255, 0) * 0.5f;
            sb.Draw(texture, Projectile.Center - Main.screenPosition, null, glowColor, Projectile.rotation, origin, 1.2f, SpriteEffects.None, 0);
            sb.Draw(texture, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.ShadowFlame, 180);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.3f }, Projectile.Center);
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.Shadowflame, vel.X, vel.Y, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}
