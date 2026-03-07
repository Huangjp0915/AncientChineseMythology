using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Qinlongs
{
    /// <summary>
    /// 青龙 - 东方神兽，木/风/雷属性
    /// 蛇形飞龙，在空中以蛇形轨迹飞行
    /// 一阶段：风龙巡游，风刃和雷击
    /// 二阶段：暴风骤起，龙卷风与闪电交加
    /// 三阶段：苍龙降世，天罚之雷
    /// </summary>
    [AutoloadBossHead]
    public class Qinlong : ModNPC
    {
        #region 常量定义

        public const float Phase2Threshold = 0.60f;
        public const float Phase3Threshold = 0.30f;

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,
            Phase1_Patrol,
            Phase1_WindBlade,
            Phase1_ThunderStrike,
            Phase1_DragonCoil,
            Phase1_SweepCharge,
            PhaseTransition_2,
            Phase2_TornadoSummon,
            Phase2_ThunderBarrage,
            Phase2_DragonRush,
            Phase2_WindPrison,
            Phase2_StormBreath,
            PhaseTransition_3,
            Phase3_AzureJudgment,
            Phase3_DragonDance,
            Phase3_CelestialStorm,
            Phase3_WindGodsWrath,
            Phase3_FuryPatrol
        }

        #endregion

        #region 状态属性

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float PhaseTimer => ref NPC.ai[1];
        public ref float AttackTimer => ref NPC.ai[2];
        public ref float SubState => ref NPC.ai[3];

        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;
        public bool IsPhase3 => NPC.life < NPC.lifeMax * Phase3Threshold;

        private float globalTime;
        private bool didPhase2Transition;
        private bool didPhase3Transition;

        private Vector2 chargeTarget;
        private int chargeCount;

        private float coilAngle;
        private float glowIntensity = 1f;

        #endregion

        #region ModNPC重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 15;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 160;
            NPC.height = 160;
            NPC.damage = 220;
            NPC.defense = 80;
            NPC.lifeMax = 2000000;
            NPC.HitSound = SoundID.NPCHit56;
            NPC.DeathSound = SoundID.NPCDeath62;
            NPC.value = Item.buyPrice(platinum: 5);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 30f;
            NPC.aiStyle = -1;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.35f);
                NPC.damage = (int)(NPC.damage * 1.25f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.35f);
            }

            Music = MusicID.Boss2;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            // 掉落占位 - 后续添加专属掉落物
        }

        public override void OnSpawn(IEntitySource source) {
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            globalTime = 0;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((int)Phase);
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(chargeCount);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            chargeCount = reader.ReadInt32();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 6; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GreenTorch, hit.HitDirection * 2f, -1f, 150, default, 1.5f);
            }
            if (NPC.life <= 0) {
                for (int i = 0; i < 40; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GreenTorch, 0, 0, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity *= 4f;
                }
            }
        }

        public override void OnKill() {
            DownedBossSystem.downedQinlong = true;
            if (Main.netMode != NetmodeID.Server) {
                PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 20f, 10f, 60, 2000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        #endregion

        #region AI主循环

        public override void AI() {
            globalTime += 1f / 60f;

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    NPC.velocity.Y -= 0.8f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            CheckPhaseTransition();
            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.Phase1_Patrol: RunPhase1Patrol(target); break;
                case BossPhase.Phase1_WindBlade: RunPhase1WindBlade(target); break;
                case BossPhase.Phase1_ThunderStrike: RunPhase1ThunderStrike(target); break;
                case BossPhase.Phase1_DragonCoil: RunPhase1DragonCoil(target); break;
                case BossPhase.Phase1_SweepCharge: RunPhase1SweepCharge(target); break;
                case BossPhase.PhaseTransition_2: RunPhaseTransition2(target); break;
                case BossPhase.Phase2_TornadoSummon: RunPhase2TornadoSummon(target); break;
                case BossPhase.Phase2_ThunderBarrage: RunPhase2ThunderBarrage(target); break;
                case BossPhase.Phase2_DragonRush: RunPhase2DragonRush(target); break;
                case BossPhase.Phase2_WindPrison: RunPhase2WindPrison(target); break;
                case BossPhase.Phase2_StormBreath: RunPhase2StormBreath(target); break;
                case BossPhase.PhaseTransition_3: RunPhaseTransition3(target); break;
                case BossPhase.Phase3_AzureJudgment: RunPhase3AzureJudgment(target); break;
                case BossPhase.Phase3_DragonDance: RunPhase3DragonDance(target); break;
                case BossPhase.Phase3_CelestialStorm: RunPhase3CelestialStorm(target); break;
                case BossPhase.Phase3_WindGodsWrath: RunPhase3WindGodsWrath(target); break;
                case BossPhase.Phase3_FuryPatrol: RunPhase3FuryPatrol(target); break;
            }

            UpdateRotation();
            Lighting.AddLight(NPC.Center, new Vector3(0.2f, 0.9f, 0.4f) * glowIntensity);
        }

        private void UpdateRotation() {
            if (NPC.velocity.LengthSquared() > 1f) {
                float targetRot = NPC.velocity.ToRotation();
                NPC.rotation = MathHelper.Lerp(NPC.rotation, targetRot, 0.1f);
                NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            }
        }

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 && !IsPhase3 &&
                Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.PhaseTransition_2);
                didPhase2Transition = true;
            }
            if (!didPhase3Transition && IsPhase3 &&
                Phase != BossPhase.PhaseTransition_3 && Phase != BossPhase.PhaseTransition_2 && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.PhaseTransition_3);
                didPhase3Transition = true;
            }
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            chargeCount = 0;
            NPC.netUpdate = true;
        }

        private BossPhase GetRandomPhase1Attack() {
            return (BossPhase)(Main.rand.Next(4) switch {
                0 => (int)BossPhase.Phase1_WindBlade,
                1 => (int)BossPhase.Phase1_ThunderStrike,
                2 => (int)BossPhase.Phase1_DragonCoil,
                _ => (int)BossPhase.Phase1_SweepCharge
            });
        }

        private BossPhase GetRandomPhase2Attack() {
            return (BossPhase)(Main.rand.Next(5) switch {
                0 => (int)BossPhase.Phase2_TornadoSummon,
                1 => (int)BossPhase.Phase2_ThunderBarrage,
                2 => (int)BossPhase.Phase2_DragonRush,
                3 => (int)BossPhase.Phase2_WindPrison,
                _ => (int)BossPhase.Phase2_StormBreath
            });
        }

        private BossPhase GetRandomPhase3Attack() {
            return (BossPhase)(Main.rand.Next(4) switch {
                0 => (int)BossPhase.Phase3_AzureJudgment,
                1 => (int)BossPhase.Phase3_DragonDance,
                2 => (int)BossPhase.Phase3_CelestialStorm,
                _ => (int)BossPhase.Phase3_WindGodsWrath
            });
        }

        #endregion

        #region 入场演出

        private void RunIntro(Player target) {
            if (PhaseTimer == 1) {
                NPC.Center = target.Center + new Vector2(0, -800);
                NPC.velocity = Vector2.Zero;
                SoundEngine.PlaySound(SoundID.Roar, target.Center);
            }

            Vector2 targetPos = target.Center + new Vector2(0, -350);
            NPC.Center = Vector2.Lerp(NPC.Center, targetPos, 0.03f);
            NPC.velocity *= 0.95f;

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 3; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(200, 200);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GreenTorch, 0, 0, 150, default, 2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                }
            }

            if (PhaseTimer >= 120) {
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 15f, 8f, 30, 1500f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f }, NPC.Center);
                TransitionTo(BossPhase.Phase1_Patrol);
            }
        }

        #endregion

        #region 一阶段：风龙

        private void RunPhase1Patrol(Player target) {
            float orbitSpeed = 0.025f;
            float orbitRadius = 400f;
            NPC.localAI[1] += orbitSpeed;
            if (NPC.localAI[1] > MathHelper.TwoPi) NPC.localAI[1] -= MathHelper.TwoPi;

            Vector2 targetPos = target.Center + new Vector2(
                MathF.Cos(NPC.localAI[1]) * orbitRadius,
                MathF.Sin(NPC.localAI[1]) * orbitRadius * 0.5f - 200f
            );
            targetPos.Y += MathF.Sin(globalTime * 3f) * 40f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, (targetPos - NPC.Center) * 0.06f, 0.08f);

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(NPC.Center - NPC.velocity * 2f, 0, 0, DustID.GreenTorch, 0, 0, 150, default, 1.5f);
                d.noGravity = true;
                d.velocity = -NPC.velocity * 0.2f;
            }

            // 巡游时持续散落风刃施压
            if (PhaseTimer % 25 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 10f;
                vel = vel.RotatedByRandom(MathHelper.ToRadians(20f));
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 5, 0f, Main.myPlayer);
            }

            if (PhaseTimer > 100) TransitionTo(GetRandomPhase1Attack());
        }

        private void RunPhase1WindBlade(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime * 1.5f) * 150f, -400);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.1f);

            int fireInterval = Main.expertMode ? 6 : 10;
            if (AttackTimer % fireInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                int bladeCount = Main.expertMode ? 7 : 5;
                float spread = MathHelper.ToRadians(10f);

                // 主扇形风刃
                for (int i = -bladeCount / 2; i <= bladeCount / 2; i++) {
                    Vector2 vel = toPlayer.RotatedBy(i * spread) * 16f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 4, 1f, Main.myPlayer);
                }
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f }, NPC.Center);
            }

            // 同步雷击施压
            if (AttackTimer % 35 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 strikePos = target.Center + new Vector2(Main.rand.NextFloat(-250, 250), -500);
                int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), strikePos, new Vector2(0, 20f),
                    ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 5, 0f, Main.myPlayer);
                if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 120;
            }

            if (AttackTimer > 120) TransitionTo(BossPhase.Phase1_Patrol);
        }

        private void RunPhase1ThunderStrike(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -500);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.03f, 0.08f);

            // 双重雷击：纵向天雷 + 横向闪电墙
            if (AttackTimer % 25 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int thunderCount = Main.expertMode ? 7 : 5;
                for (int i = 0; i < thunderCount; i++) {
                    Vector2 strikePos = target.Center + new Vector2(Main.rand.NextFloat(-350, 350), -600);
                    Vector2 vel = new Vector2(0, 20f);
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), strikePos, vel,
                        ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 150;
                }
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.8f }, target.Center);
            }

            // 横向闪电墙
            if (AttackTimer % 40 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float side = Main.rand.NextBool() ? -1f : 1f;
                for (int i = 0; i < 5; i++) {
                    Vector2 wallPos = target.Center + new Vector2(side * 600, -200 + i * 100);
                    Vector2 vel = new Vector2(-side * 14f, 0);
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), wallPos, vel,
                        ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 120;
                }
            }

            // 风刃背景压力
            if (AttackTimer % 18 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 spawnPos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 500f;
                Vector2 vel = (target.Center - spawnPos).SafeNormalize(Vector2.Zero) * 10f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                    ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 5, 0f, Main.myPlayer);
            }

            if (Main.netMode != NetmodeID.Server && AttackTimer % 10 == 0) {
                for (int i = 0; i < 5; i++) {
                    Vector2 dustPos = target.Center + new Vector2(Main.rand.NextFloat(-300, 300), Main.rand.NextFloat(-50, 50));
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Electric, 0, -2f, 200, default, 1.2f);
                    d.noGravity = true;
                }
            }

            if (AttackTimer > 160) TransitionTo(BossPhase.Phase1_Patrol);
        }

        private void RunPhase1DragonCoil(Player target) {
            coilAngle += 0.08f;
            float radius = 300f - PhaseTimer * 0.8f;
            if (radius < 120f) radius = 120f;

            Vector2 orbitPos = target.Center + new Vector2(MathF.Cos(coilAngle), MathF.Sin(coilAngle)) * radius;
            NPC.velocity = (orbitPos - NPC.Center) * 0.14f;

            // 高频风刃尾迹
            if (AttackTimer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                    ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 5, 0f, Main.myPlayer);
                if (proj >= 0 && proj < Main.maxProjectiles) {
                    Main.projectile[proj].timeLeft = 150;
                }
            }

            // 收缩时向玩家射出雷弹
            if (AttackTimer % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 12f;
                int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 4, 0f, Main.myPlayer);
                if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 150;
            }

            // 螺旋收缩到最紧时爆发环形弹
            if (radius <= 120f && AttackTimer % 30 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = 12;
                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi / count * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GreenTorch, 0, 0, 100, default, 1.8f);
                d.noGravity = true;
                d.velocity = NPC.velocity * 0.1f;
            }

            if (PhaseTimer > 180) {
                coilAngle = 0;
                TransitionTo(BossPhase.Phase1_Patrol);
            }
        }

        private void RunPhase1SweepCharge(Player target) {
            if (SubState == 0) {
                NPC.velocity *= 0.9f;
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 2; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(50, 50), 0, 0, DustID.GreenTorch, 0, 0, 100, default, 2f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 4f;
                    }
                }

                if (AttackTimer > 30) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 32f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else {
                // 冲刺中释放风刃尾迹
                if (AttackTimer % 3 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 perpDir = new Vector2(-NPC.velocity.Y, NPC.velocity.X).SafeNormalize(Vector2.Zero);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + perpDir * 40f, perpDir * 6f,
                        ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 5, 0f, Main.myPlayer);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center - perpDir * 40f, -perpDir * 6f,
                        ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 5, 0f, Main.myPlayer);
                }

                if (AttackTimer > 30) NPC.velocity *= 0.92f;

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 3; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GreenTorch, 0, 0, 100, default, 2f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * Main.rand.NextFloat(0.1f, 0.3f);
                    }
                }

                chargeCount++;
                if (AttackTimer > 45) {
                    // 每次冲刺结束释放雷击爆发
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        int burstCount = 8;
                        for (int i = 0; i < burstCount; i++) {
                            float angle = MathHelper.TwoPi / burstCount * i;
                            Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 10f;
                            int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 5, 0f, Main.myPlayer);
                            if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 100;
                        }
                    }

                    if (chargeCount < 4) {
                        SubState = 0;
                        AttackTimer = 0;
                    }
                    else {
                        TransitionTo(BossPhase.Phase1_Patrol);
                    }
                }
            }
        }

        #endregion

        #region 阶段转换演出

        private void RunPhaseTransition2(Player target) {
            NPC.velocity *= 0.95f;
            NPC.dontTakeDamage = true;

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi / 8 * i + globalTime * 3f;
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (200 - PhaseTimer);
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GreenTorch, 0, 0, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                }
            }

            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 18f, 10f, 40, 2000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
            }

            if (PhaseTimer >= 90) {
                NPC.dontTakeDamage = false;
                NPC.defense += 15;
                NPC.damage = (int)(NPC.damage * 1.2f);
                TransitionTo(BossPhase.Phase2_TornadoSummon);
            }
        }

        private void RunPhaseTransition3(Player target) {
            NPC.velocity *= 0.93f;
            NPC.dontTakeDamage = true;

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 12; i++) {
                    float angle = MathHelper.TwoPi / 12 * i + globalTime * 5f;
                    float dist = 300 - PhaseTimer * 2;
                    if (dist < 50) dist = 50;
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Electric, 0, 0, 100, default, 3f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 8f;
                }
            }

            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1.5f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 1.5f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 25f, 12f, 60, 3000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 16; i++) {
                        float angle = MathHelper.TwoPi / 16 * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 12f;
                        int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 3, 0f, Main.myPlayer);
                        if (proj >= 0 && proj < Main.maxProjectiles) {
                            Main.projectile[proj].timeLeft = 120;
                        }
                    }
                }
            }

            if (PhaseTimer >= 120) {
                NPC.dontTakeDamage = false;
                NPC.defense += 20;
                NPC.damage = (int)(NPC.damage * 1.3f);
                glowIntensity = 1.8f;
                TransitionTo(BossPhase.Phase3_FuryPatrol);
            }
        }

        #endregion

        #region 二阶段：暴风龙

        private void RunPhase2TornadoSummon(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -450);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.03f, 0.08f);

            // 龙卷风柱：密集风刃柱从四方向逼近
            if (AttackTimer % 40 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int tornadoCount = Main.expertMode ? 5 : 4;
                for (int i = 0; i < tornadoCount; i++) {
                    Vector2 spawnPos = target.Center + new Vector2(Main.rand.NextFloat(-500, 500), 400);
                    for (int j = 0; j < 3; j++) {
                        Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), -5f - j * 2f);
                        int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos + new Vector2(j * 20, 0), vel,
                            ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 4, 0f, Main.myPlayer);
                        if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 250;
                    }
                }
                SoundEngine.PlaySound(SoundID.Item122, NPC.Center);
            }

            // 同步雷击
            if (AttackTimer % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 strikePos = target.Center + new Vector2(Main.rand.NextFloat(-300, 300), -600);
                int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), strikePos, new Vector2(0, 22f),
                    ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 4, 0f, Main.myPlayer);
                if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 120;
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.4f }, target.Center);
            }

            if (AttackTimer > 160) TransitionTo(GetRandomPhase2Attack());
        }

        private void RunPhase2ThunderBarrage(Player target) {
            NPC.localAI[1] += 0.05f;
            float radius = 320f;
            Vector2 orbitPos = target.Center + new Vector2(MathF.Cos(NPC.localAI[1]), MathF.Sin(NPC.localAI[1])) * radius;
            NPC.velocity = (orbitPos - NPC.Center) * 0.12f;

            // 高频追踪雷弹
            int interval = Main.expertMode ? 5 : 8;
            if (AttackTimer % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 18f;
                toPlayer = toPlayer.RotatedByRandom(MathHelper.ToRadians(10));
                int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, toPlayer,
                    ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 4, 0f, Main.myPlayer);
                if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 180;
            }

            // 天降雷柱（多列）
            if (AttackTimer % 18 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 3; i++) {
                    Vector2 strikePos = target.Center + new Vector2(Main.rand.NextFloat(-350, 350), -700);
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), strikePos, new Vector2(0, 24f),
                        ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 3, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 120;
                }
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.5f, Pitch = Main.rand.NextFloat(-0.3f, 0.3f) }, target.Center);
            }

            // 旋转风刃环路
            if (AttackTimer % 30 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int wCount = 6;
                float baseAngle = globalTime * 3f;
                for (int i = 0; i < wCount; i++) {
                    float angle = baseAngle + MathHelper.TwoPi / wCount * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            if (AttackTimer > 180) TransitionTo(GetRandomPhase2Attack());
        }

        private void RunPhase2DragonRush(Player target) {
            if (SubState == 0) {
                NPC.velocity *= 0.85f;
                NPC.Center += Main.rand.NextVector2Circular(2, 2);

                // 蓄力期间释放旋转风刃压缩空间
                if (AttackTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float angle = AttackTimer * 0.3f;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 7f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 5, 0f, Main.myPlayer);
                }

                if (AttackTimer > 25) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 38f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else {
                // 冲刺中双侧释放风刃+雷弹
                if (AttackTimer % 4 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 perpDir = new Vector2(-NPC.velocity.Y, NPC.velocity.X).SafeNormalize(Vector2.Zero);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perpDir * 7f,
                        ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 5, 0f, Main.myPlayer);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, -perpDir * 7f,
                        ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 5, 0f, Main.myPlayer);
                }

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 5; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(30, 30), 0, 0, DustID.GreenTorch, 0, 0, 100, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * Main.rand.NextFloat(0.1f, 0.2f);
                    }
                }
                if (AttackTimer > 22) NPC.velocity *= 0.9f;
                if (AttackTimer > 35) {
                    // 每次冲刺结束雷弹爆发
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int i = 0; i < 6; i++) {
                            float angle = MathHelper.TwoPi / 6 * i;
                            Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 10f;
                            int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 4, 0f, Main.myPlayer);
                            if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 120;
                        }
                    }

                    chargeCount++;
                    if (chargeCount < 6) {
                        SubState = 0;
                        AttackTimer = 0;
                    }
                    else TransitionTo(GetRandomPhase2Attack());
                }
            }
        }

        private void RunPhase2WindPrison(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -350);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.1f);

            // 三层收缩风墙
            if (AttackTimer == 20 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int ring = 0; ring < 3; ring++) {
                    int wallCount = 20 + ring * 4;
                    float ringRadius = 400f + ring * 80f;
                    for (int i = 0; i < wallCount; i++) {
                        float angle = MathHelper.TwoPi / wallCount * i + ring * MathHelper.ToRadians(9f);
                        Vector2 pos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ringRadius;
                        Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * (3f + ring * 1.5f);
                        int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                            ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 5, 0f, Main.myPlayer);
                        if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 250;
                    }
                }
                SoundEngine.PlaySound(SoundID.Item122, target.Center);
            }

            // 同步追踪雷弹+风刃交替射击
            if (AttackTimer > 40 && AttackTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 18f;
                if (AttackTimer % 16 == 0) {
                    // 雷弹
                    for (int i = -1; i <= 1; i++) {
                        Vector2 vel = toPlayer.RotatedBy(i * MathHelper.ToRadians(10f));
                        int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 4, 0f, Main.myPlayer);
                        if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 150;
                    }
                }
                else {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, toPlayer,
                        ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 4, 0f, Main.myPlayer);
                }
            }

            if (AttackTimer > 150) TransitionTo(GetRandomPhase2Attack());
        }

        private void RunPhase2StormBreath(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(NPC.Center.X > target.Center.X ? 400 : -400, -100);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.08f);

            if (AttackTimer > 20 && AttackTimer < 130) {
                int breathInterval = Main.expertMode ? 2 : 3;
                if (AttackTimer % breathInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    int spreadCount = 7;
                    float totalSpread = MathHelper.ToRadians(50f);
                    for (int i = 0; i < spreadCount; i++) {
                        float angle = -totalSpread / 2 + totalSpread / (spreadCount - 1) * i;
                        angle += Main.rand.NextFloat(-0.05f, 0.05f);
                        Vector2 vel = toPlayer.RotatedBy(angle) * Main.rand.NextFloat(14f, 22f);
                        int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + toPlayer * 40f, vel,
                            ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 4, 0f, Main.myPlayer);
                        if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 100;
                    }
                }

                // 吐息过程中同步释放雷弹螺旋
                if (AttackTimer % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float spiralAngle = AttackTimer * 0.2f;
                    for (int i = 0; i < 2; i++) {
                        float a = spiralAngle + i * MathHelper.Pi;
                        Vector2 vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 10f;
                        int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 4, 0f, Main.myPlayer);
                        if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 150;
                    }
                }
            }

            if (AttackTimer > 150) TransitionTo(GetRandomPhase2Attack());
        }

        #endregion

        #region 三阶段：苍龙降世

        private void RunPhase3FuryPatrol(Player target) {
            float orbitSpeed = 0.05f;
            float orbitRadius = 320f;
            NPC.localAI[1] += orbitSpeed;
            Vector2 targetPos = target.Center + new Vector2(
                MathF.Cos(NPC.localAI[1]) * orbitRadius,
                MathF.Sin(NPC.localAI[1]) * orbitRadius * 0.4f - 250f
            );
            targetPos.Y += MathF.Sin(globalTime * 4f) * 30f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, (targetPos - NPC.Center) * 0.08f, 0.1f);

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(80, 80), 0, 0, DustID.Electric, 0, 0, 100, default, 2f);
                d.noGravity = true;
            }

            // 三阶段巡逻持续施压：风刃+雷弹交替
            if (PhaseTimer % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 14f;
                vel = vel.RotatedByRandom(MathHelper.ToRadians(15f));
                if (PhaseTimer % 24 == 0) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 4, 0f, Main.myPlayer);
                }
                else {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            if (PhaseTimer > 70) TransitionTo(GetRandomPhase3Attack());
        }

        private void RunPhase3AzureJudgment(Player target) {
            NPC.velocity *= 0.95f;
            NPC.Center += Main.rand.NextVector2Circular(3, 3);

            if (AttackTimer == 30) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1.5f }, target.Center);
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(target.Center, Vector2.UnitY, 18f, 10f, 35, 2000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
            }

            // 多波次天雷交叉网
            if (AttackTimer == 40 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int wave = 0; wave < 4; wave++) {
                    for (int i = 0; i < 10; i++) {
                        Vector2 strikePos = target.Center + new Vector2(-360 + i * 80, -800 - wave * 120);
                        int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), strikePos, new Vector2(0, 28f),
                            ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 3, 0f, Main.myPlayer);
                        if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 150;
                    }
                }
            }

            // 延迟横向闪电墙
            if (AttackTimer == 60 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int side = -1; side <= 1; side += 2) {
                    for (int i = 0; i < 8; i++) {
                        Vector2 wallPos = target.Center + new Vector2(side * 700, -350 + i * 100);
                        Vector2 vel = new Vector2(-side * 16f, 0);
                        int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), wallPos, vel,
                            ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 3, 0f, Main.myPlayer);
                        if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 150;
                    }
                }
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1f }, target.Center);
            }

            // 同步风刃螺旋
            if (AttackTimer >= 30 && AttackTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int wCount = 4;
                float baseAngle = AttackTimer * 0.15f;
                for (int i = 0; i < wCount; i++) {
                    float angle = baseAngle + MathHelper.TwoPi / wCount * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 10f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 4, 0f, Main.myPlayer);
                }
            }

            if (Main.netMode != NetmodeID.Server && AttackTimer >= 30 && AttackTimer <= 60) {
                for (int i = 0; i < 10; i++) {
                    Vector2 dustPos = target.Center + new Vector2(Main.rand.NextFloat(-300, 300), Main.rand.NextFloat(-600, 0));
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Electric, 0, -3f, 100, default, 2f);
                    d.noGravity = true;
                }
            }

            if (AttackTimer > 100) TransitionTo(BossPhase.Phase3_FuryPatrol);
        }

        private void RunPhase3DragonDance(Player target) {
            coilAngle += 0.1f;
            float radius = 220f + MathF.Sin(coilAngle * 0.5f) * 120f;
            Vector2 orbitPos = target.Center + new Vector2(MathF.Cos(coilAngle), MathF.Sin(coilAngle)) * radius;
            orbitPos.Y -= 100f;
            NPC.velocity = (orbitPos - NPC.Center) * 0.18f;

            // 高频风刃射击
            if (AttackTimer % 4 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 16f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, toPlayer,
                    ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 4, 0f, Main.myPlayer);
            }

            // 同步旋转雷弹臂
            if (AttackTimer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int armCount = 3;
                for (int i = 0; i < armCount; i++) {
                    float angle = coilAngle + MathHelper.TwoPi / armCount * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 12f;
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 150;
                }
            }

            // 尾迹风刃地雷
            if (AttackTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                    ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 5, 0f, Main.myPlayer);
                if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 200;
            }

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GreenTorch, 0, 0, 80, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = -NPC.velocity * Main.rand.NextFloat(0.1f, 0.3f);
                }
            }

            if (AttackTimer > 180) {
                coilAngle = 0;
                TransitionTo(BossPhase.Phase3_FuryPatrol);
            }
        }

        private void RunPhase3CelestialStorm(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.03f, 0.06f);

            // 外圈收缩风刃轮
            if (AttackTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int burstCount = 10;
                float baseAngle = MathHelper.TwoPi / burstCount * (AttackTimer / 8);
                for (int i = 0; i < burstCount; i++) {
                    float angle = baseAngle + MathHelper.TwoPi / burstCount * i;
                    Vector2 pos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 600f;
                    Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * 14f;
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                        ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 120;
                }
            }

            // 天降多列雷弹
            if (AttackTimer % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 5; i++) {
                    Vector2 strikePos = target.Center + new Vector2(Main.rand.NextFloat(-500, 500), -700);
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), strikePos, new Vector2(Main.rand.NextFloat(-3f, 3f), 22f),
                        ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 3, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) Main.projectile[proj].timeLeft = 150;
                }
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.6f }, target.Center);
            }

            // Boss自身旋转双臂螺旋弹
            if (AttackTimer % 6 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float spiralAngle = AttackTimer * 0.12f;
                for (int arm = 0; arm < 2; arm++) {
                    float a = spiralAngle + arm * MathHelper.Pi;
                    Vector2 vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 9f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 4, 0f, Main.myPlayer);
                }
            }

            if (AttackTimer > 200) TransitionTo(BossPhase.Phase3_FuryPatrol);
        }

        private void RunPhase3WindGodsWrath(Player target) {
            if (SubState == 0) {
                NPC.velocity *= 0.9f;
                NPC.dontTakeDamage = true;
                NPC.Center += Main.rand.NextVector2Circular(4, 4);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 15; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = Main.rand.NextFloat(100, 500);
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GreenTorch, 0, 0, 50, default, 3f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 10f;
                    }
                }

                // 蓄力阶段就释放旋转风刃压制
                if (AttackTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float spiralAngle = AttackTimer * 0.15f;
                    for (int arm = 0; arm < 3; arm++) {
                        float a = spiralAngle + arm * MathHelper.TwoPi / 3f;
                        Vector2 vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 8f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 5, 0f, Main.myPlayer);
                    }
                }

                if (AttackTimer > 80) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.dontTakeDamage = false;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);

                    if (Main.netMode != NetmodeID.Server) {
                        PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 30f, 15f, 60, 3000f, FullName);
                        Main.instance.CameraModifiers.Add(modifier);
                    }

                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        // 5波交错风刃环
                        for (int wave = 0; wave < 5; wave++) {
                            int bladeCount = 20 + wave * 4;
                            for (int i = 0; i < bladeCount; i++) {
                                float angle = MathHelper.TwoPi / bladeCount * i + wave * MathHelper.ToRadians(9f);
                                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (8f + wave * 3f);
                                int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                    ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 3, 0f, Main.myPlayer);
                                if (proj >= 0 && proj < Main.maxProjectiles)
                                    Main.projectile[proj].timeLeft = 220;
                            }
                        }

                        // 双侧雷柱夹击
                        for (int side = -1; side <= 1; side += 2) {
                            for (int i = 0; i < 8; i++) {
                                Vector2 strikePos = target.Center + new Vector2(side * 800, -300 + i * 80);
                                int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), strikePos, new Vector2(-side * 24f, 0),
                                    ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 3, 0f, Main.myPlayer);
                                if (proj >= 0 && proj < Main.maxProjectiles)
                                    Main.projectile[proj].timeLeft = 180;
                            }
                        }

                        // 天降雷幕
                        for (int i = 0; i < 16; i++) {
                            Vector2 strikePos = target.Center + new Vector2(-600 + i * 80, -800);
                            int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), strikePos, new Vector2(Main.rand.NextFloat(-2f, 2f), 28f),
                                ModContent.ProjectileType<QinglongThunderBolt>(), NPC.damage / 3, 0f, Main.myPlayer);
                            if (proj >= 0 && proj < Main.maxProjectiles)
                                Main.projectile[proj].timeLeft = 180;
                        }
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.92f;

                // 爆发后持续施压：追踪风刃
                if (AttackTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 toTarget = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                    for (int i = -1; i <= 1; i++) {
                        Vector2 vel = toTarget.RotatedBy(i * MathHelper.ToRadians(20)) * 16f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<QinglongWindBlade>(), NPC.damage / 4, 0f, Main.myPlayer);
                    }
                }

                if (AttackTimer > 50) TransitionTo(BossPhase.Phase3_FuryPatrol);
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            Vector2 origin = frame.Size() / 2f;

            // 纹理正向朝右，朝左飞行时使用垂直翻转来修正旋转导致的上下颠倒
            bool facingLeft = MathF.Abs(NPC.rotation) > MathHelper.PiOver2;
            SpriteEffects effects = facingLeft ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // 龙形残影
            for (int i = NPCID.Sets.TrailCacheLength[Type] - 1; i > 0; i--) {
                Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float trailRot = NPC.oldRot[i];
                bool trailLeft = MathF.Abs(trailRot) > MathHelper.PiOver2;
                SpriteEffects trailFx = effects;
                float alpha = 0.5f * (1f - (float)i / NPCID.Sets.TrailCacheLength[Type]);
                Color trailColor = drawColor * alpha;
                trailColor.G = (byte)Math.Min(trailColor.G * 1.3f, 255);
                spriteBatch.Draw(texture, trailPos, frame, trailColor, NPC.rotation, origin,
                    NPC.scale * (1f - i * 0.015f), trailFx, 0f);
            }

            Vector2 drawPos = NPC.Center - screenPos;
            spriteBatch.Draw(texture, drawPos, frame, drawColor, NPC.rotation, origin, NPC.scale, effects, 0f);
            return false;
        }

        #endregion
    }
}
