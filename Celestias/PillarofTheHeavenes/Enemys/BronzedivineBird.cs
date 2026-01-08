using AncientChineseMythology.Celestias.PillarofTheHeavenes.Items;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.PillarofTheHeavenes.Enemys
{
    /// <summary>
    /// 铜羽神鸟 - 天柱区域的飞行型鸟类敌怪
    /// 快速的飞行攻击，会俯冲撞击玩家并散落神圣羽毛
    /// </summary>
    public class BronzedivineBird : ModNPC
    {
        #region 常量
        private const float FlySpeed = 8f;
        private const float DiveSpeed = 18f;
        private const float DetectionRange = 700f;
        #endregion

        #region 状态
        private enum AIState
        {
            Glide,
            Circle,
            Dive,
            Recover,
            FeatherBarrage
        }

        private AIState State {
            get => (AIState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        private ref float StateTimer => ref NPC.ai[1];
        private ref float CircleAngle => ref NPC.ai[2];
        private ref float AttackCooldown => ref NPC.ai[3];

        private float animationCounter = 0f;
        private Vector2 diveTarget = Vector2.Zero;
        private float wingGlowIntensity = 0f;
        #endregion

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.width = 60;
            NPC.height = 40;
            NPC.damage = 75;
            NPC.defense = 30;
            NPC.lifeMax = 22000;
            NPC.knockBackResist = 0.3f;
            NPC.value = Item.buyPrice(gold: 1, silver: 80);
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath4;

            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;

            NPC.lavaImmune = true;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                new FlavorTextBestiaryInfoElement("铜羽神鸟，天庭的信使与守卫。铜色羽翼闪耀神光，俯冲时如金色闪电。")
            ]);
        }

        public override void AI() {
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.velocity.Y -= 0.1f;
                NPC.EncourageDespawn(60);
                return;
            }

            // 更新计时器
            StateTimer++;
            animationCounter++;
            if (AttackCooldown > 0) AttackCooldown--;

            // 根据速度调整旋转
            if (NPC.velocity.Length() > 1f) {
                NPC.rotation = NPC.velocity.ToRotation();
                if (NPC.spriteDirection == -1) {
                    NPC.rotation += MathHelper.Pi;
                }
            }

            // 更新朝向
            if (State != AIState.Dive) {
                NPC.spriteDirection = NPC.velocity.X > 0 ? 1 : -1;
            }

            // 添加光照
            float glowPulse = 0.5f + MathF.Sin(animationCounter * 0.08f) * 0.2f;
            Lighting.AddLight(NPC.Center, new Vector3(0.9f, 0.8f, 0.5f) * 0.5f * glowPulse);

            // 翅膀光效粒子
            if (animationCounter % 4 == 0 && State != AIState.Recover) {
                Vector2 wingOffset = new Vector2(-NPC.spriteDirection * 15, 5);
                int dust = Dust.NewDust(NPC.Center + wingOffset, 0, 0, DustID.GoldFlame,
                    -NPC.velocity.X * 0.2f, -NPC.velocity.Y * 0.2f, 100, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }

            float distToTarget = Vector2.Distance(NPC.Center, target.Center);

            switch (State) {
                case AIState.Glide:
                    RunGlideAI(target, distToTarget);
                    break;
                case AIState.Circle:
                    RunCircleAI(target, distToTarget);
                    break;
                case AIState.Dive:
                    RunDiveAI(target);
                    break;
                case AIState.Recover:
                    RunRecoverAI(target);
                    break;
                case AIState.FeatherBarrage:
                    RunFeatherBarrageAI(target);
                    break;
            }

            // 翅膀光芒强度
            float targetGlow = State == AIState.Dive ? 1f : (State == AIState.FeatherBarrage ? 0.8f : 0.4f);
            wingGlowIntensity = MathHelper.Lerp(wingGlowIntensity, targetGlow, 0.1f);
        }

        private void RunGlideAI(Player target, float distance) {
            // 悠闲滑翔，靠近玩家
            Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
            Vector2 glideDir = toTarget + new Vector2(MathF.Sin(animationCounter * 0.02f) * 0.5f, MathF.Cos(animationCounter * 0.03f) * 0.3f);
            glideDir.Normalize();

            NPC.velocity = Vector2.Lerp(NPC.velocity, glideDir * FlySpeed * 0.6f, 0.03f);

            // 进入环绕状态
            if (distance < DetectionRange * 0.7f) {
                State = AIState.Circle;
                StateTimer = 0;
                CircleAngle = (NPC.Center - target.Center).ToRotation();
            }
        }

        private void RunCircleAI(Player target, float distance) {
            // 环绕玩家飞行
            float circleRadius = 350f + MathF.Sin(StateTimer * 0.02f) * 50f;
            CircleAngle += 0.025f * NPC.spriteDirection;

            Vector2 circlePos = target.Center + CircleAngle.ToRotationVector2() * circleRadius;
            circlePos.Y -= 100f; // 在玩家上方

            Vector2 toCircle = circlePos - NPC.Center;
            NPC.velocity = Vector2.Lerp(NPC.velocity, toCircle * 0.08f, 0.1f);

            // 选择攻击
            if (AttackCooldown <= 0 && StateTimer > 60) {
                if (Main.rand.NextBool(80)) {
                    if (Main.rand.NextBool(2)) {
                        // 俯冲攻击
                        State = AIState.Dive;
                        StateTimer = 0;
                        diveTarget = target.Center + target.velocity * 20f;
                        NPC.spriteDirection = diveTarget.X > NPC.Center.X ? 1 : -1;
                    }
                    else {
                        // 羽毛弹幕
                        State = AIState.FeatherBarrage;
                        StateTimer = 0;
                    }
                }
            }
        }

        private void RunDiveAI(Player target) {
            if (StateTimer < 25) {
                // 蓄力阶段 - 升高
                NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0, -6f), 0.1f);

                // 蓄力粒子
                if (StateTimer % 3 == 0) {
                    for (int i = 0; i < 2; i++) {
                        Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(40, 40);
                        int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.8f);
                        Main.dust[dust].noGravity = true;
                        Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                    }
                }
            }
            else if (StateTimer == 25) {
                // 开始俯冲
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.5f }, NPC.Center);
                diveTarget = target.Center + target.velocity * 15f;
                Vector2 diveDir = (diveTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                NPC.velocity = diveDir * DiveSpeed;
                NPC.spriteDirection = diveDir.X > 0 ? 1 : -1;
            }
            else {
                // 俯冲中
                NPC.velocity *= 0.99f;

                // 俯冲尾迹
                if (StateTimer % 2 == 0) {
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, -NPC.velocity.X * 0.3f, -NPC.velocity.Y * 0.3f, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }

                // 接近目标或超时
                float distToTarget = Vector2.Distance(NPC.Center, diveTarget);
                if (distToTarget < 50f || StateTimer > 70) {
                    State = AIState.Recover;
                    StateTimer = 0;
                    AttackCooldown = 80;
                }
            }
        }

        private void RunRecoverAI(Player target) {
            // 恢复阶段 - 缓慢上升
            Vector2 recoverDir = new Vector2(NPC.spriteDirection * 0.5f, -1f).SafeNormalize(Vector2.UnitY);
            NPC.velocity = Vector2.Lerp(NPC.velocity, recoverDir * FlySpeed * 0.7f, 0.05f);

            if (StateTimer > 40) {
                State = AIState.Circle;
                StateTimer = 0;
                CircleAngle = (NPC.Center - target.Center).ToRotation();
            }
        }

        private void RunFeatherBarrageAI(Player target) {
            // 悬停并发射羽毛
            NPC.velocity *= 0.9f;

            if (StateTimer == 20 || StateTimer == 35 || StateTimer == 50) {
                // 发射羽毛弹幕
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    SoundEngine.PlaySound(SoundID.Item7, NPC.Center);

                    int featherCount = 5;
                    for (int i = 0; i < featherCount; i++) {
                        float angle = (target.Center - NPC.Center).ToRotation();
                        angle += (i - featherCount / 2) * 0.12f;
                        Vector2 vel = angle.ToRotationVector2() * 10f;

                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ProjectileID.HarpyFeather, NPC.damage / 3, 1f, Main.myPlayer);
                    }
                }

                // 羽毛散落效果
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                    vel.Y += 2f;
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (StateTimer > 70) {
                State = AIState.Circle;
                StateTimer = 0;
                AttackCooldown = 100;
                CircleAngle = (NPC.Center - target.Center).ToRotation();
            }
        }

        public override void FindFrame(int frameHeight) {
            // 单帧图片
            NPC.frame.Y = 0;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 origin = NPC.frame.Size() / 2;
            SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            // 绘制残影
            if (State == AIState.Dive) {
                for (int i = 0; i < NPC.oldPos.Length; i++) {
                    float alpha = 1f - (i / (float)NPC.oldPos.Length);
                    Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2 - screenPos;
                    Color trailColor = new Color(255, 220, 150) * alpha * 0.5f;
                    trailColor.A = 0;
                    spriteBatch.Draw(texture, drawPos, NPC.frame, trailColor, NPC.oldRot[i], origin, NPC.scale, effects, 0f);
                }
            }

            // 绘制主体
            Vector2 mainDrawPos = NPC.Center - screenPos;
            spriteBatch.Draw(texture, mainDrawPos, NPC.frame, drawColor, NPC.rotation, origin, NPC.scale, effects, 0f);

            // 绘制翅膀光效
            if (wingGlowIntensity > 0.3f) {
                Color glowColor = new Color(255, 230, 150, 0) * (wingGlowIntensity - 0.3f) * 0.7f;
                spriteBatch.Draw(texture, mainDrawPos, NPC.frame, glowColor, NPC.rotation, origin, NPC.scale * 1.05f, effects, 0f);
            }

            return false;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            // 羽毛飞溅
            for (int i = 0; i < 6; i++) {
                Vector2 vel = new Vector2(hit.HitDirection * Main.rand.NextFloat(2f, 4f), Main.rand.NextFloat(-3f, 0f));
                int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 1.3f);
                Main.dust[dust].noGravity = true;
            }

            if (NPC.life <= 0) {
                // 大量羽毛散落
                for (int i = 0; i < 40; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                    Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                }
                SoundEngine.PlaySound(SoundID.NPCDeath4, NPC.Center);
            }
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ItemID.Feather, 2, 2, 5));
            npcLoot.Add(ItemDropRule.Common(ItemID.GoldBar, 4, 1, 3));
            npcLoot.Add(ItemDropRule.Common(ItemID.SoulofFlight, 5, 1, 2));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<HeavenFragment>(), 2));
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            return 0f;
        }
    }
}
