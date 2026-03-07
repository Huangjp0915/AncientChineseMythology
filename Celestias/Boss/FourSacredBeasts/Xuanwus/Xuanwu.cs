using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.FourSacredBeasts.Xuanwus
{
    /// <summary>
    /// 玄武 - 北方神兽，水/冰/土属性
    /// 龟蛇一体的Boss，最高防御和生命值
    /// 一阶段：镇龟之盾，冰弹幕与水盾防御
    /// 二阶段：灵蛇觉醒，蛇头涌现，毒与冰的双重攻击
    /// 三阶段：玄天武帝，不可阻挡的潮汐和绝对防御
    /// </summary>
    [AutoloadBossHead]
    public class Xuanwu : ModNPC
    {
        #region 常量定义

        public const float Phase2Threshold = 0.60f;
        public const float Phase3Threshold = 0.30f;

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,
            Phase1_Guard,
            Phase1_IceBarrage,
            Phase1_WaterShield,
            Phase1_GravityWell,
            Phase1_ShellSpin,
            PhaseTransition_2,
            Phase2_SnakeStrike,
            Phase2_VenomSpray,
            Phase2_IceStorm,
            Phase2_DualAssault,
            Phase2_FrostWave,
            PhaseTransition_3,
            Phase3_AbsoluteDefense,
            Phase3_TidalCrush,
            Phase3_Blizzard,
            Phase3_NorthStarJudgment,
            Phase3_YinYangBalance,
            Phase3_Drift
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

        private float driftAngle;
        private int strikeCount;
        private float shellRotation;
        private bool absoluteDefenseActive;
        private float glowIntensity = 0.8f;

        #endregion

        #region ModNPC重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 200;
            NPC.height = 200;
            NPC.damage = 200;
            NPC.defense = 120;
            NPC.lifeMax = 5500000;
            NPC.HitSound = SoundID.NPCHit42;
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
            // 掉落占位
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
            writer.Write(strikeCount);
            writer.Write(absoluteDefenseActive);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            strikeCount = reader.ReadInt32();
            absoluteDefenseActive = reader.ReadBoolean();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 4; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Ice, hit.HitDirection * 2f, -1f, 150, default, 1.5f);
            }
            if (NPC.life <= 0) {
                for (int i = 0; i < 40; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Ice, 0, 0, 100, default, 3f);
                    d.noGravity = true;
                    d.velocity *= 5f;
                }
                for (int i = 0; i < 30; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Water, 0, 0, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity *= 4f;
                }
            }
        }

        public override void OnKill() {
            DownedBossSystem.downedXuanwu = true;
            if (Main.netMode != NetmodeID.Server) {
                PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 20f, 10f, 60, 2000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        #endregion

        #region AI主循环

        public override void AI() {
            NPC.defense = 120;
            globalTime += 1f / 60f;

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    NPC.velocity.Y -= 0.5f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            CheckPhaseTransition();
            PhaseTimer++;
            AttackTimer++;

            switch (Phase) {
                case BossPhase.Intro: RunIntro(target); break;
                case BossPhase.Phase1_Guard: RunPhase1Guard(target); break;
                case BossPhase.Phase1_IceBarrage: RunPhase1IceBarrage(target); break;
                case BossPhase.Phase1_WaterShield: RunPhase1WaterShield(target); break;
                case BossPhase.Phase1_GravityWell: RunPhase1GravityWell(target); break;
                case BossPhase.Phase1_ShellSpin: RunPhase1ShellSpin(target); break;
                case BossPhase.PhaseTransition_2: RunPhaseTransition2(target); break;
                case BossPhase.Phase2_SnakeStrike: RunPhase2SnakeStrike(target); break;
                case BossPhase.Phase2_VenomSpray: RunPhase2VenomSpray(target); break;
                case BossPhase.Phase2_IceStorm: RunPhase2IceStorm(target); break;
                case BossPhase.Phase2_DualAssault: RunPhase2DualAssault(target); break;
                case BossPhase.Phase2_FrostWave: RunPhase2FrostWave(target); break;
                case BossPhase.PhaseTransition_3: RunPhaseTransition3(target); break;
                case BossPhase.Phase3_AbsoluteDefense: RunPhase3AbsoluteDefense(target); break;
                case BossPhase.Phase3_TidalCrush: RunPhase3TidalCrush(target); break;
                case BossPhase.Phase3_Blizzard: RunPhase3Blizzard(target); break;
                case BossPhase.Phase3_NorthStarJudgment: RunPhase3NorthStarJudgment(target); break;
                case BossPhase.Phase3_YinYangBalance: RunPhase3YinYangBalance(target); break;
                case BossPhase.Phase3_Drift: RunPhase3Drift(target); break;
            }

            // 旋转攻击时使用shellRotation，普通状态使用轻微倾斜
            bool isSpinning = Phase == BossPhase.Phase1_ShellSpin ||
                              (Phase == BossPhase.Phase2_DualAssault && SubState == 1);
            if (isSpinning)
                NPC.rotation = shellRotation;
            else
                NPC.rotation = NPC.velocity.X * 0.005f;

            NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;

            // 冰水光源
            float iceGlow = absoluteDefenseActive ? 2f : (IsPhase3 ? 1.5f : 1f);
            Lighting.AddLight(NPC.Center, new Vector3(0.3f, 0.5f, 0.9f) * glowIntensity * iceGlow);
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
            strikeCount = 0;
            absoluteDefenseActive = false;
            NPC.dontTakeDamage = false;
            NPC.netUpdate = true;
        }

        private BossPhase GetRandomPhase1Attack() {
            return (BossPhase)(Main.rand.Next(4) switch {
                0 => (int)BossPhase.Phase1_IceBarrage,
                1 => (int)BossPhase.Phase1_WaterShield,
                2 => (int)BossPhase.Phase1_GravityWell,
                _ => (int)BossPhase.Phase1_ShellSpin
            });
        }

        private BossPhase GetRandomPhase2Attack() {
            return (BossPhase)(Main.rand.Next(5) switch {
                0 => (int)BossPhase.Phase2_SnakeStrike,
                1 => (int)BossPhase.Phase2_VenomSpray,
                2 => (int)BossPhase.Phase2_IceStorm,
                3 => (int)BossPhase.Phase2_DualAssault,
                _ => (int)BossPhase.Phase2_FrostWave
            });
        }

        private BossPhase GetRandomPhase3Attack() {
            return (BossPhase)(Main.rand.Next(5) switch {
                0 => (int)BossPhase.Phase3_AbsoluteDefense,
                1 => (int)BossPhase.Phase3_TidalCrush,
                2 => (int)BossPhase.Phase3_Blizzard,
                3 => (int)BossPhase.Phase3_NorthStarJudgment,
                _ => (int)BossPhase.Phase3_YinYangBalance
            });
        }

        private int IceProjectile(Vector2 pos, Vector2 vel, int damage, int timeLeft = 180) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return -1;
            int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<XuanwuIceShard>(), damage, 0f, Main.myPlayer);
            if (proj >= 0 && proj < Main.maxProjectiles) {
                Main.projectile[proj].timeLeft = timeLeft;
            }
            return proj;
        }

        private int WaterProjectile(Vector2 pos, Vector2 vel, int damage) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return -1;
            int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<XuanwuIceShard>(), damage, 0f, Main.myPlayer);
            return proj;
        }

        #endregion

        #region 入场演出

        private void RunIntro(Player target) {
            if (PhaseTimer == 1) {
                // 玄武从大地中缓缓升起
                NPC.Center = target.Center + new Vector2(0, 500);
                NPC.velocity = Vector2.Zero;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f }, target.Center);
            }

            Vector2 targetPos = target.Center + new Vector2(0, -200);
            NPC.Center = Vector2.Lerp(NPC.Center, targetPos, 0.015f);

            // 冰水粒子
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 6; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(200, 200);
                    int dustType = Main.rand.NextBool() ? DustID.Ice : DustID.Water;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, -2f, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity += (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 2f;
                }
            }

            if (PhaseTimer >= 120) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, Vector2.UnitY, 15f, 8f, 40, 2000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);

                    for (int i = 0; i < 20; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Ice, 0, 0, 100, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(10, 10);
                    }
                }
                TransitionTo(BossPhase.Phase1_Guard);
            }
        }

        #endregion

        #region 一阶段：镇龟

        private void RunPhase1Guard(Player target) {
            // 缓慢漂浮，龟甲旋转
            driftAngle += 0.015f;
            float radius = 280f + MathF.Sin(globalTime) * 40f;
            Vector2 orbitPos = target.Center + new Vector2(MathF.Cos(driftAngle) * radius, MathF.Sin(driftAngle) * radius * 0.4f - 200);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (orbitPos - NPC.Center) * 0.04f, 0.06f);

            // 冰雾粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(80, 80), 0, 0, DustID.Ice, 0, -1f, 150, default, 1.2f);
                d.noGravity = true;
            }

            if (PhaseTimer > 140) TransitionTo(GetRandomPhase1Attack());
        }

        private void RunPhase1IceBarrage(Player target) {
            // 冰弹扩散
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime) * 200f, -300);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.03f, 0.06f);

            int interval = Main.expertMode ? 12 : 18;
            if (AttackTimer % interval == 0) {
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                int count = Main.expertMode ? 5 : 3;
                float spread = MathHelper.ToRadians(40f);
                for (int i = 0; i < count; i++) {
                    float angle = -spread / 2 + spread / (count - 1) * i;
                    Vector2 vel = dir.RotatedBy(angle) * 12f;
                    IceProjectile(NPC.Center, vel, NPC.damage / 4);
                }
                SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.6f }, NPC.Center);
            }

            if (AttackTimer > 140) TransitionTo(BossPhase.Phase1_Guard);
        }

        private void RunPhase1WaterShield(Player target) {
            // 水盾：提高防御并释放环绕水弹
            NPC.velocity *= 0.9f;
            NPC.defense += 50;

            if (Main.netMode != NetmodeID.Server) {
                // 水盾可视化
                for (int i = 0; i < 4; i++) {
                    float angle = globalTime * 3f + MathHelper.TwoPi / 4 * i;
                    Vector2 shieldPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 120f;
                    Dust d = Dust.NewDustDirect(shieldPos, 0, 0, DustID.Water, 0, 0, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 3f;
                }
            }

            // 间歇释放水弹向玩家
            if (AttackTimer % 25 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = 8;
                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi / count * i + globalTime * 2f;
                    Vector2 pos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 120f;
                    Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * 8f;
                    WaterProjectile(pos, vel, NPC.damage / 5);
                }
            }

            if (AttackTimer > 150) {
                NPC.defense -= 50;
                TransitionTo(BossPhase.Phase1_Guard);
            }
        }

        private void RunPhase1GravityWell(Player target) {
            // 引力场：制造吸附区域
            NPC.velocity = Vector2.Lerp(NPC.velocity, (target.Center + new Vector2(0, -250) - NPC.Center) * 0.03f, 0.05f);

            // 引力中心在玩家附近
            if (AttackTimer >= 30 && AttackTimer <= 120) {
                // 给玩家施加引力
                float pullStr = 3f;
                Vector2 pullDir = (NPC.Center - target.Center).SafeNormalize(Vector2.Zero);
                target.velocity += pullDir * pullStr * (1f / 60f) * 30f;

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 6; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = Main.rand.NextFloat(200, 400);
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Water, 0, 0, 100, default, 1.5f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 5f;
                    }
                }

                // 同时发射一些冰弹
                if (AttackTimer % 20 == 0) {
                    int count = 6;
                    for (int i = 0; i < count; i++) {
                        float angle = MathHelper.TwoPi / count * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 7f;
                        IceProjectile(NPC.Center, vel, NPC.damage / 5);
                    }
                }
            }

            if (AttackTimer > 150) TransitionTo(BossPhase.Phase1_Guard);
        }

        private void RunPhase1ShellSpin(Player target) {
            // 龟甲旋转弹射
            if (SubState == 0) {
                shellRotation += 0.05f;
                NPC.velocity *= 0.9f;

                if (Main.netMode != NetmodeID.Server) {
                    Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(100, 100), 0, 0, DustID.Ice, 0, 0, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 4f;
                }

                if (AttackTimer > 50) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 22f;
                    SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.5f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else {
                shellRotation += 0.2f;

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 4; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Ice, 0, 0, 80, default, 2f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * Main.rand.NextFloat(0.05f, 0.15f);
                    }
                }

                // 旋转中释放冰碎片
                if (AttackTimer % 6 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float angle = shellRotation * 2f;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f;
                    IceProjectile(NPC.Center, vel, NPC.damage / 5, 120);
                }

                if (AttackTimer > 25) NPC.velocity *= 0.92f;
                if (AttackTimer > 45) {
                    strikeCount++;
                    if (strikeCount < 3) {
                        SubState = 0;
                        AttackTimer = 0;
                    }
                    else TransitionTo(BossPhase.Phase1_Guard);
                }
            }
        }

        #endregion

        #region 阶段转换

        private void RunPhaseTransition2(Player target) {
            NPC.velocity *= 0.9f;
            NPC.dontTakeDamage = true;

            // 蛇觉醒特效 - 水流涌动
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 10; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = 350 - PhaseTimer * 2.5f;
                    if (dist < 40) dist = 40;
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    int dustType = Main.rand.NextBool() ? DustID.Ice : DustID.Water;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 50, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 7f;
                }
                // 蛇形绿色粒子
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(60, 60), 0, 0, DustID.CursedTorch, 0, 0, 80, default, 2f);
                    d.noGravity = true;
                }
            }

            if (PhaseTimer == 80) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.5f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 20f, 10f, 45, 2000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
            }

            if (PhaseTimer >= 100) {
                NPC.dontTakeDamage = false;
                NPC.defense += 10;
                NPC.damage = (int)(NPC.damage * 1.15f);
                glowIntensity = 1.2f;
                TransitionTo(BossPhase.Phase2_SnakeStrike);
            }
        }

        private void RunPhaseTransition3(Player target) {
            NPC.velocity *= 0.85f;
            NPC.dontTakeDamage = true;
            NPC.Center += Main.rand.NextVector2Circular(4, 4);

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 15; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(50, 300);
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    int dustType = Main.rand.Next(3) switch {
                        0 => DustID.Ice,
                        1 => DustID.Water,
                        _ => DustID.CursedTorch
                    };
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 50, default, 3f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 10f;
                }
            }

            if (PhaseTimer == 70) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 2f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 30f, 15f, 60, 3000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }

                // 冰水爆发
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 24; i++) {
                        float angle = MathHelper.TwoPi / 24 * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 13f;
                        IceProjectile(NPC.Center, vel, NPC.damage / 3);
                    }
                }
            }

            if (PhaseTimer >= 130) {
                NPC.dontTakeDamage = false;
                NPC.defense += 20;
                NPC.damage = (int)(NPC.damage * 1.25f);
                glowIntensity = 1.8f;
                TransitionTo(BossPhase.Phase3_Drift);
            }
        }

        #endregion

        #region 二阶段：灵蛇觉醒

        private void RunPhase2SnakeStrike(Player target) {
            // 蛇击：快速蛇头冲刺
            if (SubState == 0) {
                // 蓄力
                NPC.velocity *= 0.85f;
                if (Main.netMode != NetmodeID.Server) {
                    Dust d = Dust.NewDustDirect(NPC.Center + new Vector2(0, -80) + Main.rand.NextVector2Circular(30, 30), 0, 0, DustID.CursedTorch, 0, 0, 100, default, 2f);
                    d.noGravity = true;
                }

                if (AttackTimer > 25) {
                    SubState = 1;
                    AttackTimer = 0;

                    // 顶部蛇头弹射弹幕
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2 snakePos = NPC.Center + new Vector2(0, -80);
                        Vector2 vel = (target.Center - snakePos).SafeNormalize(Vector2.UnitX) * 20f;
                        for (int i = -1; i <= 1; i++) {
                            Vector2 v = vel.RotatedBy(i * MathHelper.ToRadians(8f));
                            int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), snakePos, v,
                                ModContent.ProjectileType<XuanwuVenomFang>(), NPC.damage / 4, 0f, Main.myPlayer);
                            if (proj >= 0 && proj < Main.maxProjectiles) {
                                Main.projectile[proj].timeLeft = 120;
                            }
                        }
                    }
                    SoundEngine.PlaySound(SoundID.Item103 with { Pitch = 0.3f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.9f;
                if (AttackTimer > 30) {
                    strikeCount++;
                    if (strikeCount < 5) {
                        SubState = 0;
                        AttackTimer = 0;
                    }
                    else TransitionTo(GetRandomPhase2Attack());
                }
            }
        }

        private void RunPhase2VenomSpray(Player target) {
            // 毒雾喷射：扇形蛇毒弹幕
            Vector2 hoverPos = target.Center + new Vector2(0, -250);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.03f, 0.05f);

            if (AttackTimer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                int count = Main.expertMode ? 7 : 5;
                float spread = MathHelper.ToRadians(60f);
                for (int i = 0; i < count; i++) {
                    float angle = -spread / 2 + spread / (count - 1) * i;
                    Vector2 vel = dir.RotatedBy(angle) * Main.rand.NextFloat(10f, 14f);
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<XuanwuVenomFang>(), NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) {
                        Main.projectile[proj].timeLeft = 150;
                    }
                }
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f }, NPC.Center);
            }

            if (AttackTimer > 120) TransitionTo(GetRandomPhase2Attack());
        }

        private void RunPhase2IceStorm(Player target) {
            // 冰暴：大量冰弹从天而降
            Vector2 hoverPos = target.Center + new Vector2(0, -500);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.03f, 0.06f);

            int interval = Main.expertMode ? 4 : 6;
            if (AttackTimer % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = Main.expertMode ? 3 : 2;
                for (int i = 0; i < count; i++) {
                    float x = target.Center.X + Main.rand.NextFloat(-500, 500);
                    Vector2 pos = new Vector2(x, target.Center.Y - 600);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(12f, 18f));
                    IceProjectile(pos, vel, NPC.damage / 4);
                }
            }

            if (AttackTimer > 150) TransitionTo(GetRandomPhase2Attack());
        }

        private void RunPhase2DualAssault(Player target) {
            // 龟蛇合击：玄武冲刺同时蛇头四射弹幕
            if (SubState == 0) {
                NPC.velocity *= 0.85f;
                if (AttackTimer > 30) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 18f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else {
                shellRotation += 0.1f;

                // 滑行中蛇头连续射击
                if (AttackTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 snakePos = NPC.Center + new Vector2(0, -80);
                    Vector2 vel = (target.Center - snakePos).SafeNormalize(Vector2.Zero) * 14f;
                    vel = vel.RotatedByRandom(MathHelper.ToRadians(10f));
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), snakePos, vel,
                        ModContent.ProjectileType<XuanwuVenomFang>(), NPC.damage / 4, 0f, Main.myPlayer);
                    if (proj >= 0 && proj < Main.maxProjectiles) {
                        Main.projectile[proj].timeLeft = 120;
                    }
                }

                // 龟甲释放冰碎
                if (AttackTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float angle = shellRotation;
                    IceProjectile(NPC.Center, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f, NPC.damage / 5, 120);
                }

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 3; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Ice, 0, 0, 80, default, 2f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * Main.rand.NextFloat(0.05f, 0.1f);
                    }
                }

                if (AttackTimer > 25) NPC.velocity *= 0.92f;
                if (AttackTimer > 50) {
                    strikeCount++;
                    if (strikeCount < 3) {
                        SubState = 0;
                        AttackTimer = 0;
                    }
                    else TransitionTo(GetRandomPhase2Attack());
                }
            }
        }

        private void RunPhase2FrostWave(Player target) {
            // 寒霜波动：向外扩展的冰环
            NPC.velocity *= 0.9f;

            if (AttackTimer == 30 || AttackTimer == 60) {
                SoundEngine.PlaySound(SoundID.Item28 with { Volume = 1.2f }, NPC.Center);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int count = Main.expertMode ? 24 : 16;
                    float offset = (AttackTimer == 60) ? MathHelper.ToRadians(7.5f) : 0f;
                    for (int i = 0; i < count; i++) {
                        float angle = MathHelper.TwoPi / count * i + offset;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 9f;
                        IceProjectile(NPC.Center, vel, NPC.damage / 4);
                    }
                }

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 15; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Ice, 0, 0, 100, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(8, 8);
                    }
                }
            }

            if (AttackTimer > 100) TransitionTo(GetRandomPhase2Attack());
        }

        #endregion

        #region 三阶段：玄天武帝

        private void RunPhase3Drift(Player target) {
            driftAngle += 0.025f;
            float radius = 300f;
            Vector2 driftPos = target.Center + new Vector2(MathF.Cos(driftAngle) * radius, MathF.Sin(driftAngle * 0.6f) * radius * 0.3f - 250);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (driftPos - NPC.Center) * 0.06f, 0.08f);

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.Ice : DustID.Water;
                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(80, 80), 0, 0, dustType, 0, -1f, 100, default, 2f);
                d.noGravity = true;
            }

            if (PhaseTimer > 100) TransitionTo(GetRandomPhase3Attack());
        }

        private void RunPhase3AbsoluteDefense(Player target) {
            // 绝对防御：短时间无敌并释放反击弹幕
            NPC.velocity *= 0.9f;
            absoluteDefenseActive = true;
            NPC.dontTakeDamage = true;

            if (Main.netMode != NetmodeID.Server) {
                // 玄甲护体可视化
                for (int i = 0; i < 8; i++) {
                    float angle = globalTime * 4f + MathHelper.TwoPi / 8 * i;
                    float dist = 130f + MathF.Sin(globalTime * 6f) * 20f;
                    Vector2 shieldPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                    Dust d = Dust.NewDustDirect(shieldPos, 0, 0, DustID.Ice, 0, 0, 50, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 4f;
                }
            }

            // 防御期间释放反击弹幕
            if (AttackTimer % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = 12;
                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi / count * i + globalTime * 2f;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 10f;
                    IceProjectile(NPC.Center, vel, NPC.damage / 4);
                }
            }

            if (AttackTimer > 120) {
                absoluteDefenseActive = false;
                NPC.dontTakeDamage = false;
                TransitionTo(BossPhase.Phase3_Drift);
            }
        }

        private void RunPhase3TidalCrush(Player target) {
            // 潮汐碾压：巨大水浪横扫
            if (SubState == 0) {
                // 蓄力
                NPC.velocity *= 0.9f;
                NPC.Center += Main.rand.NextVector2Circular(3, 3);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 6; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(150, 150), 0, 0, DustID.Water, 0, 0, 50, default, 3f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 5f;
                    }
                }

                if (AttackTimer > 60) {
                    SubState = 1;
                    AttackTimer = 0;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);

                    // 双向水浪
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int wave = 0; wave < 3; wave++) {
                            for (int i = 0; i < 8; i++) {
                                float speed = 6f + i * 2f + wave * 3f;
                                WaterProjectile(NPC.Center, new Vector2(speed, -2f + wave), NPC.damage / 3);
                                WaterProjectile(NPC.Center, new Vector2(-speed, -2f + wave), NPC.damage / 3);
                            }
                        }
                    }

                    if (Main.netMode != NetmodeID.Server) {
                        PunchCameraModifier modifier = new(NPC.Center, Vector2.UnitX, 18f, 10f, 35, 2000f, FullName);
                        Main.instance.CameraModifiers.Add(modifier);
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.92f;
                if (AttackTimer > 60) TransitionTo(BossPhase.Phase3_Drift);
            }
        }

        private void RunPhase3Blizzard(Player target) {
            // 暴风雪：全屏冰弹
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.03f, 0.06f);

            int interval = Main.expertMode ? 2 : 4;
            if (AttackTimer % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = Main.expertMode ? 4 : 3;
                for (int i = 0; i < count; i++) {
                    float x = target.Center.X + Main.rand.NextFloat(-700, 700);
                    Vector2 pos = new Vector2(x, target.Center.Y - 700);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(10f, 16f));
                    IceProjectile(pos, vel, NPC.damage / 4);
                }
            }

            if (AttackTimer > 200) TransitionTo(BossPhase.Phase3_Drift);
        }

        private void RunPhase3NorthStarJudgment(Player target) {
            // 北辰审判：终极天象攻击
            if (SubState == 0) {
                NPC.velocity *= 0.88f;
                NPC.dontTakeDamage = true;
                NPC.Center += Main.rand.NextVector2Circular(5, 5);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 12; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = Main.rand.NextFloat(80, 400);
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        int dustType = Main.rand.Next(3) switch { 0 => DustID.Ice, 1 => DustID.Water, _ => DustID.CursedTorch };
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 50, default, 3f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 10f;
                    }
                }

                if (AttackTimer > 100) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.dontTakeDamage = false;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -1f, Volume = 2f }, NPC.Center);

                    if (Main.netMode != NetmodeID.Server) {
                        PunchCameraModifier modifier = new(NPC.Center, (Main.rand.NextFloat() * MathHelper.TwoPi).ToRotationVector2(), 30f, 15f, 60, 3000f, FullName);
                        Main.instance.CameraModifiers.Add(modifier);
                    }

                    // 北辰星柱：从玄武向6个方向放射冰柱弹幕
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int dir = 0; dir < 6; dir++) {
                            float angle = MathHelper.TwoPi / 6 * dir;
                            for (int i = 0; i < 10; i++) {
                                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (5f + i * 3f);
                                IceProjectile(NPC.Center, vel, NPC.damage / 3);
                            }
                        }
                        // 额外环形水弹
                        for (int wave = 0; wave < 3; wave++) {
                            int count = 20;
                            for (int i = 0; i < count; i++) {
                                float angle = MathHelper.TwoPi / count * i + wave * MathHelper.ToRadians(9f);
                                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (8f + wave * 4f);
                                WaterProjectile(NPC.Center, vel, NPC.damage / 3);
                            }
                        }
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.9f;
                if (AttackTimer > 80) TransitionTo(BossPhase.Phase3_Drift);
            }
        }

        private void RunPhase3YinYangBalance(Player target) {
            // 阴阳平衡：交替释放冰与蛇毒弹幕
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime * 1.5f) * 200f, -300);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.06f);

            // 交替冰弹和毒弹
            bool isIceTurn = ((int)(AttackTimer / 20)) % 2 == 0;

            int interval = Main.expertMode ? 6 : 10;
            if (AttackTimer % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                int count = 4;
                float spread = MathHelper.ToRadians(30f);

                for (int i = 0; i < count; i++) {
                    float angle = -spread / 2 + spread / (count - 1) * i;
                    Vector2 vel = dir.RotatedBy(angle) * 14f;

                    if (isIceTurn) {
                        IceProjectile(NPC.Center, vel, NPC.damage / 4);
                    } else {
                        int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<XuanwuVenomFang>(), NPC.damage / 4, 0f, Main.myPlayer);
                        if (proj >= 0 && proj < Main.maxProjectiles) {
                            Main.projectile[proj].timeLeft = 150;
                        }
                    }
                }
            }

            // 背景环射
            if (AttackTimer % 30 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = 8;
                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi / count * i + AttackTimer * 0.05f;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 6f;
                    if (isIceTurn)
                        WaterProjectile(NPC.Center, vel, NPC.damage / 5);
                    else
                        IceProjectile(NPC.Center, vel, NPC.damage / 5, 120);
                }
            }

            if (AttackTimer > 200) TransitionTo(BossPhase.Phase3_Drift);
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            Vector2 origin = frame.Size() / 2f;

            bool isSpinning = Phase == BossPhase.Phase1_ShellSpin ||
                              (Phase == BossPhase.Phase2_DualAssault && SubState == 1);

            SpriteEffects effects = SpriteEffects.None;
            float drawRotation = NPC.rotation;

            if (!isSpinning) {
                // 普通状态：纹理朝右，面朝左时水平翻转
                bool facingRight = NPC.spriteDirection == 1;
                effects = facingRight ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                drawRotation = facingRight ? NPC.rotation : -NPC.rotation;
            }

            // 玄武水冰残影
            for (int i = NPCID.Sets.TrailCacheLength[Type] - 1; i > 0; i--) {
                Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float alpha = 0.3f * (1f - (float)i / NPCID.Sets.TrailCacheLength[Type]);
                Color trailColor = new Color(0.3f, 0.5f + alpha, 1f) * alpha;
                spriteBatch.Draw(texture, trailPos, frame, trailColor, drawRotation, origin, NPC.scale, effects, 0f);
            }

            Vector2 drawPos = NPC.Center - screenPos;
            spriteBatch.Draw(texture, drawPos, frame, drawColor, drawRotation, origin, NPC.scale, effects, 0f);
            return false;
        }

        #endregion
    }
}
