using Microsoft.Xna.Framework;
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
    /// 翔龙 - 天柱区域的飞行型龙形敌怪
    /// 优雅地在空中盘旋，发射神圣光弹攻击玩家
    /// </summary>
    public class Xianglong : ModNPC
    {
        #region 常量
        private const float FlySpeed = 6f;
        private const float AttackRange = 450f;
        private const float OrbitDistance = 300f;
        #endregion

        #region 状态
        private enum AIState
        {
            Idle,
            Orbit,
            Attack,
            Dash
        }

        private AIState State {
            get => (AIState)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        private ref float StateTimer => ref NPC.ai[1];
        private ref float OrbitAngle => ref NPC.ai[2];
        private ref float AttackCooldown => ref NPC.ai[3];

        private float animationCounter = 0f;
        private int trailCounter = 0;
        #endregion

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            NPC.width = 80;
            NPC.height = 50;
            NPC.damage = 85;
            NPC.defense = 40;
            NPC.lifeMax = 22800;
            NPC.knockBackResist = 0.2f;
            NPC.value = Item.buyPrice(gold: 2);
            NPC.HitSound = SoundID.NPCHit7;
            NPC.DeathSound = SoundID.NPCDeath8;

            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;

            NPC.lavaImmune = true;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                new FlavorTextBestiaryInfoElement("翔龙，天庭神兽之一，守护神圣天柱。其身披金鳞，口吐神光，凡人难以近身。")
            ]);
        }

        public override void AI() {
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.velocity.Y -= 0.2f;
                NPC.EncourageDespawn(60);
                return;
            }

            // 更新朝向
            NPC.spriteDirection = target.Center.X < NPC.Center.X ? 1 : -1;

            // 更新计时器
            StateTimer++;
            if (AttackCooldown > 0) AttackCooldown--;
            animationCounter++;
            trailCounter++;

            // 添加光照
            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.9f, 0.6f) * 0.5f);

            // 生成神圣粒子
            if (trailCounter % 5 == 0) {
                int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, 
                    NPC.velocity.X * 0.2f, NPC.velocity.Y * 0.2f, 100, default, 1.5f);
                Main.dust[dust].noGravity = true;
            }

            float distToTarget = Vector2.Distance(NPC.Center, target.Center);

            switch (State) {
                case AIState.Idle:
                    RunIdleAI(target, distToTarget);
                    break;
                case AIState.Orbit:
                    RunOrbitAI(target, distToTarget);
                    break;
                case AIState.Attack:
                    RunAttackAI(target);
                    break;
                case AIState.Dash:
                    RunDashAI(target);
                    break;
            }
        }

        private void RunIdleAI(Player target, float distance) {
            // 缓慢靠近玩家
            Vector2 toTarget = target.Center - NPC.Center;
            if (toTarget.Length() > 0) toTarget.Normalize();
            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * FlySpeed * 0.5f, 0.05f);

            // 进入轨道状态
            if (distance < AttackRange * 1.5f) {
                State = AIState.Orbit;
                StateTimer = 0;
                OrbitAngle = (NPC.Center - target.Center).ToRotation();
            }
        }

        private void RunOrbitAI(Player target, float distance) {
            // 环绕玩家飞行
            OrbitAngle += 0.02f * NPC.spriteDirection;
            Vector2 orbitPos = target.Center + OrbitAngle.ToRotationVector2() * OrbitDistance;
            Vector2 toOrbit = orbitPos - NPC.Center;

            NPC.velocity = Vector2.Lerp(NPC.velocity, toOrbit * 0.1f, 0.08f);

            // 攻击检测
            if (AttackCooldown <= 0 && StateTimer > 60) {
                if (Main.rand.NextBool(60)) {
                    State = Main.rand.NextBool(3) ? AIState.Dash : AIState.Attack;
                    StateTimer = 0;
                }
            }
        }

        private void RunAttackAI(Player target) {
            NPC.velocity *= 0.95f;

            if (StateTimer == 20) {
                // 发射三发神圣光弹
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    SoundEngine.PlaySound(SoundID.Item12, NPC.Center);
                    for (int i = -1; i <= 1; i++) {
                        Vector2 direction = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                        direction = direction.RotatedBy(i * 0.15f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, direction * 12f,
                            ProjectileID.CultistBossLightningOrb, NPC.damage / 2, 2f, Main.myPlayer);
                    }
                }

                // 神圣光效
                for (int i = 0; i < 15; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (StateTimer > 50) {
                State = AIState.Orbit;
                StateTimer = 0;
                AttackCooldown = 90;
            }
        }

        private void RunDashAI(Player target) {
            if (StateTimer < 30) {
                // 蓄力阶段
                NPC.velocity *= 0.9f;

                // 蓄力粒子
                if (StateTimer % 3 == 0) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(60, 60);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                }
            }
            else if (StateTimer == 30) {
                // 冲刺
                SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
                Vector2 dashDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                NPC.velocity = dashDir * 20f;
            }
            else {
                NPC.velocity *= 0.97f;
            }

            if (StateTimer > 80) {
                State = AIState.Orbit;
                StateTimer = 0;
                AttackCooldown = 120;
            }
        }

        public override void FindFrame(int frameHeight) {
            // 单帧图片
            NPC.frame.Y = 0;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;

            // 绘制残影
            for (int i = 0; i < NPC.oldPos.Length; i++) {
                float alpha = 1f - (i / (float)NPC.oldPos.Length);
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2 - screenPos;
                Color trailColor = drawColor * alpha * 0.4f;
                SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                spriteBatch.Draw(texture, drawPos, NPC.frame, trailColor, NPC.rotation, 
                    NPC.frame.Size() / 2, NPC.scale, effects, 0f);
            }

            return true;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 5; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, hit.HitDirection * 2, -1, 100, default, 1.5f);
            }

            if (NPC.life <= 0) {
                for (int i = 0; i < 30; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(8f, 8f);
                    Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, vel.X, vel.Y, 100, default, 2f);
                }
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f }, NPC.Center);
            }
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ItemID.GoldBar, 3, 2, 5));
            npcLoot.Add(ItemDropRule.Common(ItemID.SoulofLight, 2, 1, 3));
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo) {
            // 由 HeavenPillarSpawnSystem 控制生成
            return 0f;
        }
    }
}
