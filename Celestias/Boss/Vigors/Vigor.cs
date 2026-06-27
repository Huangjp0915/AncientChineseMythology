using AncientChineseMythology.Celestias.Boss.Vigors.Items;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.Systems;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.Celestias.Boss.Vigors
{
    /// <summary>
    /// 神威·断罪刃 / Vigor, the Sin-Severing Blade
    /// 巨手刀将 — 后月球领主Boss (约200万HP)
    ///
    /// 核心机制:
    ///   1. 符文封印系统 — 在地面留下延时引爆的符文印记,逼迫玩家持续走位
    ///   2. 格挡反击 — 短暂进入格挡态,被攻击时释放毁灭性反击
    ///   3. 蓄势连斩 — 连续攻击不回巡逻,连击越多伤害递增
    ///   4. 侵略性运动 — 不绕轨道,以冲刺/追击/俯冲为主
    ///
    /// 一阶段(100%-60%): 试炼 — 断罪横扫+符印锁定+冲锋判决+升空劈斩
    /// 二阶段(60%-30%): 裁决 — 连环斩+天降断罪+符文漩涡+格挡反击+刃牢
    /// 三阶段(30%-0%): 天刑 — 四极封印+无间斩舞+天罚执行+裁决风暴
    /// </summary>
    [AutoloadBossHead]
    public class Vigor : ModNPC
    {
        #region 常量

        internal const float Phase2Threshold = 0.60f;
        internal const float Phase3Threshold = 0.30f;
        private const int MaxCombo = 5;
        private const int SealDetonateTime = 150;

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,

            Phase1_Pursuit,
            Phase1_ConvictionSweep,
            Phase1_SealLock,
            Phase1_ChargeVerdict,
            Phase1_RisingStrike,

            PhaseTransition_2,

            Phase2_Pursuit,
            Phase2_ChainSlash,
            Phase2_DescendingJudge,
            Phase2_RunicVortex,
            Phase2_CounterStance,
            Phase2_BladeCage,

            PhaseTransition_3,

            Phase3_Pursuit,
            Phase3_FourPillarSeal,
            Phase3_EndlessDance,
            Phase3_DivineExecution,
            Phase3_JudgmentStorm
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
        private int comboCount;
        private int chargeCount;
        private float glowIntensity = 1f;
        private bool isCounterReady;
        private bool counterTriggered;
        private Vector2 chargeTarget;
        private float comboMultiplier => 1f + comboCount * 0.12f;
        private int counterCooldown;

        // ===== V2 断罪判决演出状态 (纯本地视觉, 客户端确定性驱动) =====
        private int hitstopTimer;          // 处决砸落"全屏定格"近似: Boss 帧冻结
        private int verdictBloomTimer;     // 处决泛光寿命
        private int verdictBloomMax = 1;
        private Vector2 verdictBloomCenter;
        private float verdictBloomPeak;    // 该次泛光峰值强度
        private float chargeRamp;          // 蓄力渐强泛光 0~1
        private int sealRunicTimer;        // 符印封锁区地纹寿命
        private float sealRunicPeak;       // 符印地纹峰值强度
        private Vector2 sealRunicCenter;
        private float sealRunicRadius = 320f;

        private struct JudgmentBeam { public Vector2 Start, End; public int Time, MaxTime; public float Width; }
        private readonly JudgmentBeam[] judgmentBeams = new JudgmentBeam[8];

        #endregion

        #region ModNPC重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 16;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 140;
            NPC.height = 180;
            NPC.damage = 200;
            NPC.defense = 85;
            NPC.lifeMax = 2000000;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
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

        public override void BossLoot(ref int potionType) => potionType = ItemID.SuperHealingPotion;

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<GeneralOrder>()));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<SinSeveringBlade>(),
                ModContent.ItemType<AureateVoidrender>(),
                ModContent.ItemType<VerdictSealHammer>()
            ));
        }

        public override void OnSpawn(IEntitySource source) {
            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            globalTime = 0;
            comboCount = 0;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((int)Phase);
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(comboCount);
            writer.Write(chargeCount);
            writer.Write(isCounterReady);
            writer.Write(counterTriggered);
            writer.WriteVector2(chargeTarget);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            comboCount = reader.ReadInt32();
            chargeCount = reader.ReadInt32();
            isCounterReady = reader.ReadBoolean();
            counterTriggered = reader.ReadBoolean();
            chargeTarget = reader.ReadVector2();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            if (isCounterReady && counterCooldown <= 0) {
                modifiers.FinalDamage *= 0.15f;
                counterTriggered = true;
                counterCooldown = 30;
                NPC.netUpdate = true;
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 6; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, hit.HitDirection * 2f, -1f, 150, default, 1.5f);
            }
            if (NPC.life <= 0) {
                for (int i = 0; i < 50; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, 0, 0, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity *= 5f;
                }
                for (int i = 0; i < 30; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.BlueTorch, 0, 0, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity *= 4f;
                }
            }
        }

        public override void OnKill() {
            DownedBossSystem.downedVigor = true;
            ACMScreenShakeSystem.Add(16f);
        }

        #endregion

        #region AI主循环

        public override void AI() {
            // 断罪判决处决"全屏定格"近似: Boss 帧冻结 (各端确定性, 维持同步), 期间维持泛光/震动
            if (hitstopTimer > 0) {
                hitstopTimer--;
                NPC.velocity = Vector2.Zero;
                PublishVerdictVisuals();
                return;
            }

            globalTime += 1f / 60f;
            if (counterCooldown > 0) counterCooldown--;
            TickVerdictTimers();
            chargeRamp = MathHelper.Lerp(chargeRamp, 0f, 0.08f);

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
                case BossPhase.Intro: AI_Intro(target); break;

                case BossPhase.Phase1_Pursuit: AI_Phase1Pursuit(target); break;
                case BossPhase.Phase1_ConvictionSweep: AI_P1_ConvictionSweep(target); break;
                case BossPhase.Phase1_SealLock: AI_P1_SealLock(target); break;
                case BossPhase.Phase1_ChargeVerdict: AI_P1_ChargeVerdict(target); break;
                case BossPhase.Phase1_RisingStrike: AI_P1_RisingStrike(target); break;

                case BossPhase.PhaseTransition_2: AI_PhaseTransition2(target); break;

                case BossPhase.Phase2_Pursuit: AI_Phase2Pursuit(target); break;
                case BossPhase.Phase2_ChainSlash: AI_P2_ChainSlash(target); break;
                case BossPhase.Phase2_DescendingJudge: AI_P2_DescendingJudge(target); break;
                case BossPhase.Phase2_RunicVortex: AI_P2_RunicVortex(target); break;
                case BossPhase.Phase2_CounterStance: AI_P2_CounterStance(target); break;
                case BossPhase.Phase2_BladeCage: AI_P2_BladeCage(target); break;

                case BossPhase.PhaseTransition_3: AI_PhaseTransition3(target); break;

                case BossPhase.Phase3_Pursuit: AI_Phase3Pursuit(target); break;
                case BossPhase.Phase3_FourPillarSeal: AI_P3_FourPillarSeal(target); break;
                case BossPhase.Phase3_EndlessDance: AI_P3_EndlessDance(target); break;
                case BossPhase.Phase3_DivineExecution: AI_P3_DivineExecution(target); break;
                case BossPhase.Phase3_JudgmentStorm: AI_P3_JudgmentStorm(target); break;
            }

            UpdateVisuals();
            PublishVerdictVisuals();
        }

        private void UpdateVisuals() {
            NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
            NPC.rotation = NPC.velocity.X * 0.025f;

            float baseIntensity = IsPhase3 ? 1.6f : IsPhase2 ? 1.3f : 1f;
            float pulse = baseIntensity + MathF.Sin(globalTime * 3f) * 0.15f;
            if (isCounterReady) pulse = 2.5f + MathF.Sin(globalTime * 12f) * 0.5f;
            glowIntensity = pulse;

            Lighting.AddLight(NPC.Center, new Vector3(0.9f, 0.7f, 0.2f) * glowIntensity);
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
            isCounterReady = false;
            counterTriggered = false;
            NPC.dontTakeDamage = false;
            NPC.netUpdate = true;
        }

        private void EndAttackWithCombo(BossPhase pursuitPhase, Func<BossPhase> getAttack) {
            if (comboCount < MaxCombo && Main.rand.NextBool(3)) {
                comboCount++;
                TransitionTo(getAttack());
            }
            else {
                comboCount = 0;
                TransitionTo(pursuitPhase);
            }
        }

        private BossPhase GetP1Attack() => (BossPhase)(Main.rand.Next(4) switch {
            0 => (int)BossPhase.Phase1_ConvictionSweep,
            1 => (int)BossPhase.Phase1_SealLock,
            2 => (int)BossPhase.Phase1_ChargeVerdict,
            _ => (int)BossPhase.Phase1_RisingStrike
        });

        private BossPhase GetP2Attack() => (BossPhase)(Main.rand.Next(5) switch {
            0 => (int)BossPhase.Phase2_ChainSlash,
            1 => (int)BossPhase.Phase2_DescendingJudge,
            2 => (int)BossPhase.Phase2_RunicVortex,
            3 => (int)BossPhase.Phase2_CounterStance,
            _ => (int)BossPhase.Phase2_BladeCage
        });

        private BossPhase GetP3Attack() => (BossPhase)(Main.rand.Next(4) switch {
            0 => (int)BossPhase.Phase3_FourPillarSeal,
            1 => (int)BossPhase.Phase3_EndlessDance,
            2 => (int)BossPhase.Phase3_DivineExecution,
            _ => (int)BossPhase.Phase3_JudgmentStorm
        });

        private void SpawnRuneSeal(Vector2 position) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), position, Vector2.Zero,
                ModContent.ProjectileType<RunicEnergyOrbs>(), (int)(NPC.damage / 4 * comboMultiplier), 0f, Main.myPlayer,
                ai0: 1f, ai1: SealDetonateTime);
        }

        // ===== V2 断罪判决演出助手 (确定性, 各端独立触发; 服务端无害) =====

        /// <summary>触发处决砸落泛光 + 屏幕震动 (可选 Boss 帧冻结 hitstop)。</summary>
        private void TriggerVerdictSlam(Vector2 center, float peak, int life, float shake, int hitstop = 0) {
            verdictBloomCenter = center;
            verdictBloomPeak = peak;
            verdictBloomTimer = life;
            verdictBloomMax = System.Math.Max(life, 1);
            if (hitstop > 0)
                hitstopTimer = System.Math.Max(hitstopTimer, hitstop);
            ACMScreenShakeSystem.Add(shake);
        }

        /// <summary>登记一道判决光束 (世界坐标, 寿命 life 帧)。</summary>
        private void AddJudgmentBeam(Vector2 start, Vector2 end, int life, float width) {
            for (int i = 0; i < judgmentBeams.Length; i++) {
                if (judgmentBeams[i].Time <= 0) {
                    judgmentBeams[i] = new JudgmentBeam { Start = start, End = end, Time = life, MaxTime = System.Math.Max(life, 1), Width = width };
                    return;
                }
            }
        }

        /// <summary>点亮符印封锁区地纹 (引爆将近=渐亮的可读预警)。</summary>
        private void MarkSealZone(Vector2 center, float radius, float peak, int life) {
            sealRunicCenter = center;
            sealRunicRadius = radius;
            sealRunicPeak = peak;
            sealRunicTimer = System.Math.Max(sealRunicTimer, life);
        }

        private void TickVerdictTimers() {
            if (verdictBloomTimer > 0) verdictBloomTimer--;
            if (sealRunicTimer > 0) sealRunicTimer--;
            for (int i = 0; i < judgmentBeams.Length; i++)
                if (judgmentBeams[i].Time > 0) judgmentBeams[i].Time--;
        }

        private void PublishVerdictVisuals() {
            if (Main.dedServ) return;

            int tier = IsPhase3 ? 2 : IsPhase2 ? 1 : 0;

            float counterTell = 0f;
            if (isCounterReady)
                counterTell = 0.55f + MathF.Sin(globalTime * 12f) * 0.35f;

            float bloom = 0f;
            if (verdictBloomTimer > 0) {
                float t = verdictBloomTimer / (float)verdictBloomMax;   // 1→0
                bloom = verdictBloomPeak * MathF.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi);
            }
            bloom = System.Math.Max(bloom, chargeRamp * 0.5f);
            Vector2 bloomCenter = verdictBloomTimer > 0 ? verdictBloomCenter : NPC.Center;

            float sealRunic = 0f;
            if (sealRunicTimer > 0)
                sealRunic = sealRunicPeak * MathHelper.Clamp(sealRunicTimer / 30f, 0.25f, 1f);

            VigorVerdictSystem.Publish(tier, MathHelper.Clamp(counterTell, 0f, 1f),
                sealRunicCenter, sealRunicRadius, MathHelper.Clamp(sealRunic, 0f, 1f),
                bloomCenter, 0.16f + (1f - bloom) * 0.22f, MathHelper.Clamp(bloom, 0f, 1f),
                (float)Main.GlobalTimeWrappedHourly);
        }

        #endregion

        #region 入场演出

        private void AI_Intro(Player target) {
            if (PhaseTimer == 1) {
                NPC.Center = target.Center + new Vector2(0, -1000);
                NPC.velocity = Vector2.Zero;
                NPC.Opacity = 0f;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.3f }, target.Center);
            }

            NPC.Opacity = MathHelper.Clamp(PhaseTimer / 50f, 0f, 1f);
            float introProgress = MathHelper.Clamp(PhaseTimer / 140f, 0f, 1f);
            float eased = ACMUtils.SineInOut(introProgress);
            Vector2 targetPos = target.Center + new Vector2(0, -300);
            NPC.Center = Vector2.Lerp(target.Center + new Vector2(0, -1000), targetPos, eased);
            NPC.velocity *= 0.9f;

            if (Main.netMode != NetmodeID.Server) {
                int ringCount = 6;
                for (int r = 0; r < ringCount; r++) {
                    float ringSpeed = 2f + r * 1.5f;
                    float ringPhase = globalTime * ringSpeed + r * MathHelper.PiOver4;
                    float ringDist = MathHelper.Lerp(400 - r * 30, 50, eased);
                    Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(ringPhase), MathF.Sin(ringPhase)) * ringDist;
                    int dustType = r % 2 == 0 ? DustID.GoldFlame : DustID.BlueTorch;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 100, default, 2f + r * 0.2f);
                    d.noGravity = true;
                    d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * (3f + r);
                }

                if (PhaseTimer > 30) {
                    for (int i = 0; i < 3; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + new Vector2(Main.rand.NextFloat(-40, 40), 60),
                            0, 0, DustID.GoldFlame, 0, 2f, 80, default, 2.5f);
                        d.noGravity = true;
                    }
                }
            }

            if (PhaseTimer == 80)
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);

            if (PhaseTimer >= 140) {
                NPC.Opacity = 1f;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 1.5f }, NPC.Center);

                ACMScreenShakeSystem.Add(14f);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 40; i++) {
                        float a = MathHelper.TwoPi / 40 * i;
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GoldFlame,
                            MathF.Cos(a) * 14f, MathF.Sin(a) * 14f, 80, default, 3f);
                        d.noGravity = true;
                    }
                }

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int dir = 0; dir < 4; dir++) {
                        float angle = MathHelper.PiOver4 + MathHelper.PiOver2 * dir;
                        Vector2 sealPos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 200f;
                        SpawnRuneSeal(sealPos);
                    }
                    for (int i = 0; i < 8; i++) {
                        float angle = MathHelper.TwoPi / 8 * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 10f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 4, 2f, Main.myPlayer);
                    }
                }

                TransitionTo(BossPhase.Phase1_Pursuit);
            }
        }

        #endregion

        #region 一阶段: 试炼

        private void AI_Phase1Pursuit(Player target) {
            float dist = NPC.Distance(target.Center);
            Vector2 desiredPos = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY) * 280f;
            desiredPos.Y -= 60;

            float approach = dist > 400 ? 0.12f : 0.06f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, (desiredPos - NPC.Center) * approach, 0.08f);

            if (PhaseTimer % 60 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                SpawnRuneSeal(target.Center + Main.rand.NextVector2Circular(80, 30));

            if (PhaseTimer % 40 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 9f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<RunicEnergyOrbs>(), NPC.damage / 5, 0f, Main.myPlayer);
            }

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(50, 70), 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.5f);
                d.noGravity = true;
            }

            if (PhaseTimer > 80) TransitionTo(GetP1Attack());
        }

        private void AI_P1_ConvictionSweep(Player target) {
            if (SubState == 0) {
                float side = NPC.Center.X > target.Center.X ? 1 : -1;
                Vector2 flankPos = target.Center + new Vector2(side * 350, -50);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (flankPos - NPC.Center) * 0.12f, 0.15f);

                if (Main.netMode != NetmodeID.Server) {
                    Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 2f);
                    d.noGravity = true;
                    d.velocity = -NPC.velocity * 0.1f;
                }

                if (AttackTimer > 25) {
                    SubState = 1;
                    AttackTimer = 0;
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f, Volume = 1.1f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.85f;

                if (AttackTimer == 5 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                    int bladeCount = Main.expertMode ? 9 : 7;
                    float totalSpread = MathHelper.ToRadians(60f);

                    for (int i = 0; i < bladeCount; i++) {
                        float t = (float)i / (bladeCount - 1) - 0.5f;
                        Vector2 vel = toPlayer.RotatedBy(t * totalSpread) * (12f + MathF.Abs(t) * 4f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel * comboMultiplier,
                            ModContent.ProjectileType<RunicCleaveWaves>(), (int)(NPC.damage / 4 * comboMultiplier), 1f, Main.myPlayer);
                    }

                    SpawnRuneSeal(target.Center + target.velocity * 30);
                }

                if (Main.netMode != NetmodeID.Server && AttackTimer <= 10) {
                    for (int i = 0; i < 6; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(80, 80), 0, 0, DustID.GoldFlame, 0, 0, 80, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(4, 4);
                    }
                }

                if (AttackTimer > 40)
                    EndAttackWithCombo(BossPhase.Phase1_Pursuit, GetP1Attack);
            }
        }

        private void AI_P1_SealLock(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.08f);

            if (AttackTimer > 10)
                MarkSealZone(target.Center, 250f, 0.7f, 6);

            if (AttackTimer == 15 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi / 6 * i;
                    SpawnRuneSeal(target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 180f);
                }
                SoundEngine.PlaySound(SoundID.Item100 with { Pitch = -0.3f, Volume = 0.9f }, target.Center);
            }

            if (AttackTimer == 50 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.PiOver2 * i;
                    SpawnRuneSeal(target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 110f);
                }
            }

            if (AttackTimer == 80 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 predicted = target.Center + target.velocity * 40;
                for (int i = 0; i < 5; i++)
                    SpawnRuneSeal(predicted + Main.rand.NextVector2Circular(120, 120));
            }

            if (AttackTimer % 25 == 0 && AttackTimer > 20 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 13f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 5, 1f, Main.myPlayer);
            }

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 2; i++) {
                    Dust d = Dust.NewDustDirect(NPC.Center + new Vector2(Main.rand.NextFloat(-30, 30), 50),
                        0, 0, DustID.BlueTorch, 0, 3f, 100, default, 1.8f);
                    d.noGravity = true;
                }
            }

            if (AttackTimer > 110)
                EndAttackWithCombo(BossPhase.Phase1_Pursuit, GetP1Attack);
        }

        private void AI_P1_ChargeVerdict(Player target) {
            if (SubState == 0) {
                Vector2 backPos = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.UnitX) * 500f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, (backPos - NPC.Center) * 0.1f, 0.12f);
                NPC.Center += Main.rand.NextVector2Circular(2, 2);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 5; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(50, 50), 0, 0, DustID.GoldFlame, 0, 0, 80, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 6f;
                    }
                }

                if (AttackTimer > 30) {
                    SubState = 1;
                    AttackTimer = 0;
                    chargeTarget = target.Center;
                    NPC.velocity = (chargeTarget - NPC.Center).SafeNormalize(Vector2.UnitX) * 40f;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f, Volume = 1.1f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                if (AttackTimer % 4 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 perpDir = new Vector2(-NPC.velocity.Y, NPC.velocity.X).SafeNormalize(Vector2.Zero);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + perpDir * 40f, perpDir * 5f,
                        ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 5, 0f, Main.myPlayer);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center - perpDir * 40f, -perpDir * 5f,
                        ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 5, 0f, Main.myPlayer);
                }

                if (AttackTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                    SpawnRuneSeal(NPC.Center + new Vector2(0, 60));

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 5; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GoldFlame, 0, 0, 60, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * Main.rand.NextFloat(0.05f, 0.2f);
                    }
                }

                if (AttackTimer > 20) NPC.velocity *= 0.93f;

                if (AttackTimer > 30) {
                    chargeCount++;
                    if (chargeCount < 3) {
                        SubState = 0;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    else {
                        EndAttackWithCombo(BossPhase.Phase1_Pursuit, GetP1Attack);
                    }
                }
            }
        }

        private void AI_P1_RisingStrike(Player target) {
            if (SubState == 0) {
                NPC.velocity = new Vector2((target.Center.X - NPC.Center.X) * 0.015f, -18f);

                if (AttackTimer == 15 && Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = -3; i <= 3; i++) {
                        float angle = MathHelper.PiOver2 + i * MathHelper.ToRadians(15f);
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 14f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 4, 1f, Main.myPlayer);
                    }
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.5f }, NPC.Center);
                }

                if (AttackTimer > 25) {
                    SubState = 1;
                    AttackTimer = 0;
                    chargeTarget = target.Center;
                    NPC.velocity = new Vector2((chargeTarget.X - NPC.Center.X) * 0.04f, 35f);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                if (NPC.Center.Y >= chargeTarget.Y + 30 || AttackTimer > 35) {
                    SubState = 2;
                    AttackTimer = 0;
                    NPC.velocity = Vector2.Zero;

                    TriggerVerdictSlam(NPC.Center + new Vector2(0, 40), 0.55f, 16, 10f);
                    AddJudgmentBeam(NPC.Center + new Vector2(0, -900), NPC.Center + new Vector2(0, 60), 14, 24f);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f }, NPC.Center);

                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int side = -1; side <= 1; side += 2) {
                            for (int i = 1; i <= 7; i++) {
                                Vector2 vel = new Vector2(side * i * 3f, -2.5f);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + new Vector2(0, 40), vel,
                                    ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 4, 2f, Main.myPlayer);
                            }
                        }
                        for (int i = 0; i < 4; i++) {
                            float angle = MathHelper.PiOver2 * i;
                            SpawnRuneSeal(NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 150f);
                        }
                    }
                }
            }
            else {
                NPC.velocity.Y -= 0.6f;
                if (AttackTimer > 35)
                    EndAttackWithCombo(BossPhase.Phase1_Pursuit, GetP1Attack);
            }
        }

        #endregion

        #region 阶段转换演出

        private void AI_PhaseTransition2(Player target) {
            NPC.velocity *= 0.92f;
            NPC.dontTakeDamage = true;

            if (Main.netMode != NetmodeID.Server) {
                float shrink = MathHelper.Clamp(1f - PhaseTimer / 60f, 0.15f, 1f);
                for (int arm = 0; arm < 2; arm++) {
                    for (int i = 0; i < 6; i++) {
                        float angle = globalTime * (4f + arm * 2f) + arm * MathHelper.Pi + i * MathHelper.PiOver4;
                        float dist = 300 * shrink;
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        int dustType = arm == 0 ? DustID.GoldFlame : DustID.BlueTorch;
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 80, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 8f;
                    }
                }
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustDirect(NPC.Center + new Vector2(Main.rand.NextFloat(-100, 100), 80),
                        0, 0, DustID.GoldFlame, 0, -5f, 60, default, 2f);
                    d.noGravity = true;
                }
            }

            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.3f }, NPC.Center);
                ACMScreenShakeSystem.Add(11f);
                TriggerVerdictSlam(NPC.Center, 0.6f, 18, 0f);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 12; i++) {
                        float angle = MathHelper.TwoPi / 12 * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 12f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 4, 1f, Main.myPlayer);
                    }
                    for (int i = 0; i < 8; i++) {
                        float angle = MathHelper.TwoPi / 8 * i;
                        SpawnRuneSeal(target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 250f);
                    }
                }
            }

            if (PhaseTimer >= 90) {
                NPC.dontTakeDamage = false;
                NPC.defense += 15;
                NPC.damage = (int)(NPC.damage * 1.2f);
                TransitionTo(BossPhase.Phase2_Pursuit);
            }
        }

        private void AI_PhaseTransition3(Player target) {
            NPC.velocity *= 0.90f;
            NPC.dontTakeDamage = true;

            if (Main.netMode != NetmodeID.Server) {
                for (int layer = 0; layer < 3; layer++) {
                    int count = 8 + layer * 4;
                    float speed = 3f + layer * 2.5f;
                    float dist = MathHelper.Lerp(400 - layer * 60, 50, MathHelper.Clamp(PhaseTimer / 80f, 0, 1));
                    for (int i = 0; i < count; i++) {
                        float angle = globalTime * speed + MathHelper.TwoPi / count * i + layer * MathHelper.Pi / 6;
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        int dustType = layer == 1 ? DustID.BlueTorch : DustID.GoldFlame;
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 50, default, 3f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 12f;
                    }
                }
            }

            if (PhaseTimer == 70) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f, Volume = 1.6f }, NPC.Center);
                ACMScreenShakeSystem.Add(12f);
                TriggerVerdictSlam(NPC.Center, 0.75f, 22, 0f);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 16; i++) {
                        float angle = MathHelper.TwoPi / 16 * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 14f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 3, 1f, Main.myPlayer);
                    }
                    for (int i = 0; i < 16; i++) {
                        float angle = MathHelper.TwoPi / 16 * i + MathHelper.ToRadians(11.25f);
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 9f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<RunicEnergyOrbs>(), NPC.damage / 4, 0f, Main.myPlayer);
                    }
                    for (int i = 0; i < 12; i++) {
                        float angle = MathHelper.TwoPi / 12 * i;
                        SpawnRuneSeal(target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 300f);
                    }
                }
            }

            if (PhaseTimer >= 110) {
                NPC.dontTakeDamage = false;
                NPC.defense += 20;
                NPC.damage = (int)(NPC.damage * 1.3f);
                glowIntensity = 1.8f;
                TransitionTo(BossPhase.Phase3_Pursuit);
            }
        }

        #endregion

        #region 二阶段: 裁决

        private void AI_Phase2Pursuit(Player target) {
            float dist = NPC.Distance(target.Center);
            Vector2 desiredPos = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY) * 220f;
            desiredPos.Y -= 40;
            float approach = dist > 350 ? 0.15f : 0.08f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, (desiredPos - NPC.Center) * approach, 0.1f);

            if (PhaseTimer % 35 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                SpawnRuneSeal(target.Center + target.velocity * 20 + Main.rand.NextVector2Circular(60, 30));

            if (PhaseTimer % 22 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 12f;
                vel = vel.RotatedByRandom(MathHelper.ToRadians(10f));
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<RunicEnergyOrbs>(), NPC.damage / 5, 0f, Main.myPlayer);
            }

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(50, 70), 0, 0, DustID.GoldFlame, 0, 0, 100, default, 1.8f);
                d.noGravity = true;
            }

            if (PhaseTimer > 65) TransitionTo(GetP2Attack());
        }

        private void AI_P2_ChainSlash(Player target) {
            if (SubState == 0) {
                NPC.velocity *= 0.85f;
                NPC.Center += Main.rand.NextVector2Circular(3, 3);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 3; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(40, 40), 0, 0, DustID.GoldFlame, 0, 0, 80, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 5f;
                    }
                }

                if (AttackTimer > 15) {
                    SubState = 1;
                    AttackTimer = 0;
                    float offsetAngle = (chargeCount % 2 == 0 ? 1 : -1) * MathHelper.ToRadians(25f);
                    NPC.velocity = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX).RotatedBy(offsetAngle) * 38f;
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f + chargeCount * 0.1f, Volume = 0.9f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                if (AttackTimer % 3 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 perpDir = new Vector2(-NPC.velocity.Y, NPC.velocity.X).SafeNormalize(Vector2.Zero);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + perpDir * 30f, perpDir * 4f,
                        ModContent.ProjectileType<RunicCleaveWaves>(), (int)(NPC.damage / 5 * comboMultiplier), 0f, Main.myPlayer);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center - perpDir * 30f, -perpDir * 4f,
                        ModContent.ProjectileType<RunicCleaveWaves>(), (int)(NPC.damage / 5 * comboMultiplier), 0f, Main.myPlayer);
                }

                if (AttackTimer % 6 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                    SpawnRuneSeal(NPC.Center);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 4; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GoldFlame, 0, 0, 60, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * Main.rand.NextFloat(0.05f, 0.15f);
                    }
                }

                if (AttackTimer > 18) NPC.velocity *= 0.9f;

                if (AttackTimer > 22) {
                    chargeCount++;
                    if (chargeCount < (Main.expertMode ? 5 : 4)) {
                        SubState = 0;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    else {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < 10; i++) {
                                float angle = MathHelper.TwoPi / 10 * i;
                                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 11f;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                    ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 4, 1f, Main.myPlayer);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f }, NPC.Center);
                        EndAttackWithCombo(BossPhase.Phase2_Pursuit, GetP2Attack);
                    }
                }
            }
        }

        private void AI_P2_DescendingJudge(Player target) {
            if (SubState == 0) {
                NPC.velocity = new Vector2((target.Center.X - NPC.Center.X) * 0.01f, -25f);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 3; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + new Vector2(0, 60), 0, 0, DustID.GoldFlame, 0, 3f, 80, default, 2.5f);
                        d.noGravity = true;
                    }
                }

                if (AttackTimer > 25) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.velocity = Vector2.Zero;
                    chargeTarget = target.Center;

                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int ring = 0; ring < 2; ring++) {
                            int count = 8 + ring * 4;
                            float radius = 120f + ring * 100f;
                            for (int i = 0; i < count; i++) {
                                float angle = MathHelper.TwoPi / count * i + ring * MathHelper.ToRadians(15);
                                SpawnRuneSeal(chargeTarget + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item100 with { Pitch = -0.5f, Volume = 1.1f }, chargeTarget);
                    }
                    MarkSealZone(chargeTarget, 280f, 0.85f, 70);
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                MarkSealZone(chargeTarget, 280f, 0.85f, 8);
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 4; i++) {
                        Dust d = Dust.NewDustDirect(chargeTarget + Main.rand.NextVector2Circular(200, 200),
                            0, 0, DustID.BlueTorch, 0, 0, 100, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = new Vector2(0, -2f);
                    }
                }

                if (AttackTimer > 30) {
                    SubState = 2;
                    AttackTimer = 0;
                    NPC.velocity = new Vector2(0, 45f);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 1.2f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 2) {
                if (NPC.Center.Y >= chargeTarget.Y || AttackTimer > 30) {
                    SubState = 3;
                    AttackTimer = 0;
                    NPC.velocity = Vector2.Zero;

                    TriggerVerdictSlam(NPC.Center + new Vector2(0, 40), 0.7f, 18, 12f, hitstop: 3);
                    AddJudgmentBeam(NPC.Center + new Vector2(0, -1000), NPC.Center + new Vector2(0, 60), 16, 30f);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.5f }, NPC.Center);

                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int dir = 0; dir < 4; dir++) {
                            float angle = MathHelper.PiOver2 * dir;
                            for (int i = 0; i < 5; i++) {
                                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (8f + i * 3f);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                    ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 3, 2f, Main.myPlayer);
                            }
                        }
                    }
                }
            }
            else {
                NPC.velocity.Y -= 0.5f;
                if (AttackTimer > 40)
                    EndAttackWithCombo(BossPhase.Phase2_Pursuit, GetP2Attack);
            }
        }

        private void AI_P2_RunicVortex(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -300);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.07f);

            if (AttackTimer == 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int layer = 0; layer < 2; layer++) {
                    int count = 12 + layer * 6;
                    float radius = 500f + layer * 100f;
                    for (int i = 0; i < count; i++) {
                        float angle = MathHelper.TwoPi / count * i + layer * MathHelper.ToRadians(8);
                        Vector2 pos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                        Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * (2.5f + layer * 1.5f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                            ModContent.ProjectileType<RunicEnergyOrbs>(), NPC.damage / 5, 0f, Main.myPlayer);
                    }
                }
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f }, target.Center);
            }

            if (AttackTimer > 15 && AttackTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float spiralAngle = AttackTimer * 0.2f;
                for (int arm = 0; arm < 3; arm++) {
                    float a = spiralAngle + arm * MathHelper.TwoPi / 3f;
                    Vector2 vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 13f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 4, 1f, Main.myPlayer);
                }
            }

            if (AttackTimer > 140)
                EndAttackWithCombo(BossPhase.Phase2_Pursuit, GetP2Attack);
        }

        private void AI_P2_CounterStance(Player target) {
            if (SubState == 0) {
                Vector2 approachPos = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY) * 200f;
                NPC.velocity = Vector2.Lerp(NPC.velocity, (approachPos - NPC.Center) * 0.1f, 0.12f);

                if (AttackTimer > 20) {
                    SubState = 1;
                    AttackTimer = 0;
                    isCounterReady = true;
                    NPC.velocity = Vector2.Zero;
                    SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.3f, Volume = 1f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                NPC.velocity *= 0.9f;

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 4; i++) {
                        float angle = globalTime * 6f + MathHelper.TwoPi / 4 * i;
                        Vector2 shieldPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 70f;
                        Dust d = Dust.NewDustDirect(shieldPos, 0, 0, DustID.GoldFlame, 0, 0, 50, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * 3f;
                    }
                }

                if (counterTriggered) {
                    SubState = 2;
                    AttackTimer = 0;
                    isCounterReady = false;
                    counterTriggered = false;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f, Volume = 1.3f }, NPC.Center);
                    NPC.netUpdate = true;
                }
                else if (AttackTimer > 80) {
                    SubState = 3;
                    AttackTimer = 0;
                    isCounterReady = false;
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 2) {
                if (AttackTimer == 1) {
                    Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    NPC.velocity = toPlayer * 50f;
                    TriggerVerdictSlam(NPC.Center, 0.7f, 16, 12f, hitstop: 3);
                    AddJudgmentBeam(NPC.Center, NPC.Center + toPlayer * 1100f, 14, 26f);
                }

                if (AttackTimer == 12 && Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 16; i++) {
                        float angle = MathHelper.TwoPi / 16 * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 16f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 3, 2f, Main.myPlayer);
                    }
                }

                if (AttackTimer > 8) NPC.velocity *= 0.88f;
                if (AttackTimer > 30)
                    EndAttackWithCombo(BossPhase.Phase2_Pursuit, GetP2Attack);
            }
            else {
                NPC.velocity *= 0.95f;
                if (AttackTimer < 30 && AttackTimer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 14f;
                    vel = vel.RotatedByRandom(MathHelper.ToRadians(20f));
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<RunicEnergyOrbs>(), NPC.damage / 4, 0f, Main.myPlayer);
                    SpawnRuneSeal(target.Center + Main.rand.NextVector2Circular(150, 150));
                }
                if (AttackTimer > 40)
                    EndAttackWithCombo(BossPhase.Phase2_Pursuit, GetP2Attack);
            }
        }

        private void AI_P2_BladeCage(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -350);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.08f);

            if (AttackTimer == 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 1.1f }, NPC.Center);

                for (int i = -4; i <= 4; i++) {
                    float xOffset = i * 80f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(),
                        target.Center + new Vector2(xOffset, -500), new Vector2(0, 4f),
                        ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 5, 0f, Main.myPlayer);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(),
                        target.Center + new Vector2(xOffset, 500), new Vector2(0, -4f),
                        ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
                for (int i = -3; i <= 3; i++) {
                    float yOffset = i * 100f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(),
                        target.Center + new Vector2(-500, yOffset), new Vector2(4f, 0),
                        ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 5, 0f, Main.myPlayer);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(),
                        target.Center + new Vector2(500, yOffset), new Vector2(-4f, 0),
                        ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            if (AttackTimer > 20 && AttackTimer % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 13f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<RunicEnergyOrbs>(), NPC.damage / 4, 0f, Main.myPlayer);
            }

            if (AttackTimer > 120)
                EndAttackWithCombo(BossPhase.Phase2_Pursuit, GetP2Attack);
        }

        #endregion

        #region 三阶段: 天刑

        private void AI_Phase3Pursuit(Player target) {
            float dist = NPC.Distance(target.Center);
            Vector2 desiredPos = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY) * 180f;
            desiredPos.Y -= 30;
            float approach = dist > 300 ? 0.18f : 0.1f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, (desiredPos - NPC.Center) * approach, 0.12f);

            if (PhaseTimer % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                SpawnRuneSeal(target.Center + target.velocity * 15 + Main.rand.NextVector2Circular(80, 30));

            if (PhaseTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 15f;
                vel = vel.RotatedByRandom(MathHelper.ToRadians(8f));
                if (PhaseTimer % 20 == 0) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 4, 1f, Main.myPlayer);
                }
                else {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<RunicEnergyOrbs>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(60, 80), 0, 0, DustID.GoldFlame, 0, 0, 100, default, 2f);
                d.noGravity = true;
            }

            if (PhaseTimer > 55) TransitionTo(GetP3Attack());
        }

        private void AI_P3_FourPillarSeal(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -350);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.08f);

            if (AttackTimer > 8)
                MarkSealZone(target.Center, 440f, 0.8f, 6);

            if (AttackTimer == 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                SoundEngine.PlaySound(SoundID.Item100 with { Pitch = -0.5f, Volume = 1.2f }, target.Center);
                for (int dir = 0; dir < 4; dir++) {
                    float angle = MathHelper.PiOver2 * dir;
                    Vector2 pillarPos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 400f;
                    SpawnRuneSeal(pillarPos);
                }
            }

            if (AttackTimer > 15 && AttackTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int dir = 0; dir < 4; dir++) {
                    float angle = MathHelper.PiOver2 * dir;
                    Vector2 pillarPos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 400f;
                    Vector2 vel = (target.Center - pillarPos).SafeNormalize(Vector2.Zero) * 12f;
                    vel = vel.RotatedByRandom(MathHelper.ToRadians(15f));

                    if (AttackTimer % 20 == 0)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pillarPos, vel,
                            ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 4, 1f, Main.myPlayer);
                    else
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pillarPos, vel,
                            ModContent.ProjectileType<RunicEnergyOrbs>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            if (AttackTimer > 20 && AttackTimer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 16f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 3, 1f, Main.myPlayer);
            }

            if (AttackTimer > 160) TransitionTo(BossPhase.Phase3_Pursuit);
        }

        private void AI_P3_EndlessDance(Player target) {
            if (SubState == 0) {
                NPC.velocity *= 0.8f;
                NPC.Center += Main.rand.NextVector2Circular(4, 4);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 6; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(30, 30), 0, 0, DustID.GoldFlame, 0, 0, 60, default, 3f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * 8f;
                    }
                }

                if (AttackTimer > 10) {
                    SubState = 1;
                    AttackTimer = 0;
                    float dashAngle = MathHelper.TwoPi / 8 * chargeCount + MathHelper.ToRadians(Main.rand.NextFloat(-10, 10));
                    Vector2 dashDir = new Vector2(MathF.Cos(dashAngle), MathF.Sin(dashAngle));
                    Vector2 toPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                    if (Vector2.Dot(dashDir, toPlayer) < 0) dashDir = -dashDir;
                    NPC.velocity = dashDir * 45f;
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.5f + chargeCount * 0.08f, Volume = 0.8f }, NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else {
                if (AttackTimer % 2 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 perpDir = new Vector2(-NPC.velocity.Y, NPC.velocity.X).SafeNormalize(Vector2.Zero);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + perpDir * 25f, perpDir * 3f,
                        ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
                if (AttackTimer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                    SpawnRuneSeal(NPC.Center);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 6; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GoldFlame, 0, 0, 40, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = -NPC.velocity * Main.rand.NextFloat(0.03f, 0.12f);
                    }
                }

                if (AttackTimer > 12) NPC.velocity *= 0.88f;

                if (AttackTimer > 16) {
                    chargeCount++;
                    if (chargeCount < (Main.expertMode ? 8 : 6)) {
                        SubState = 0;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    else {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < 12; i++) {
                                float angle = MathHelper.TwoPi / 12 * i;
                                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 14f;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                    ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 3, 1f, Main.myPlayer);
                            }
                        }
                        ACMScreenShakeSystem.Add(10f);
                        TransitionTo(BossPhase.Phase3_Pursuit);
                    }
                }
            }
        }

        private void AI_P3_DivineExecution(Player target) {
            if (SubState == 0) {
                NPC.velocity *= 0.88f;
                NPC.dontTakeDamage = true;
                NPC.Center += Main.rand.NextVector2Circular(5, 5);

                // 长蓄力: 渐强金色收束泛光 + 渐强震屏 + 向心符印法阵 ("宣判"前奏)
                chargeRamp = MathHelper.Clamp(AttackTimer / 90f, 0f, 1f);
                if (AttackTimer % 12 == 0)
                    ACMScreenShakeSystem.Add(2f + chargeRamp * 6f);
                MarkSealZone(target.Center, 360f, 0.35f + chargeRamp * 0.55f, 6);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 15; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = Main.rand.NextFloat(200, 600);
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 40, default, 3.5f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 14f;
                    }
                }

                if (AttackTimer % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float spiralAngle = AttackTimer * 0.2f;
                    for (int arm = 0; arm < 3; arm++) {
                        float a = spiralAngle + arm * MathHelper.TwoPi / 3f;
                        Vector2 vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 9f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 5, 0f, Main.myPlayer);
                    }
                }

                if (AttackTimer > 90) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.dontTakeDamage = false;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.7f, Volume = 1.6f }, NPC.Center);

                    // 断罪判决"宣判"定格: 主 hitstop(4f) + 金色收束泛光 + 大震 + 天柱判决光束
                    chargeRamp = 0f;
                    TriggerVerdictSlam(NPC.Center, 1f, 26, 12f, hitstop: 4);
                    AddJudgmentBeam(NPC.Center + new Vector2(0, -1100), NPC.Center + new Vector2(0, 80), 24, 42f);
                    MarkSealZone(target.Center, 380f, 1f, 36);
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.92f;

                // 每波刃墙: 判决光束 + 轻收束泛光 + 中震 (各端确定性视觉; 弹幕飞行中不冻结 Boss 避免突兀)
                for (int wave = 0; wave < 6; wave++) {
                    if (AttackTimer == wave * 15 + 5) {
                        float beamAngle = MathHelper.TwoPi / 6 * wave;
                        Vector2 beamDir = beamAngle.ToRotationVector2();
                        AddJudgmentBeam(NPC.Center, NPC.Center + beamDir * 1000f, 12, 22f);
                        TriggerVerdictSlam(NPC.Center, 0.5f, 12, 7f);
                    }
                }

                for (int wave = 0; wave < 6; wave++) {
                    if (AttackTimer == wave * 15 + 5 && Main.netMode != NetmodeID.MultiplayerClient) {
                        float baseAngle = MathHelper.TwoPi / 6 * wave;
                        int wallCount = 12;
                        float wallRadius = 600f;

                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f + wave * 0.15f, Volume = 1f }, NPC.Center);

                        for (int i = 0; i < wallCount; i++) {
                            float spread = MathHelper.ToRadians(50f);
                            float angle = baseAngle + spread * ((float)i / (wallCount - 1) - 0.5f);
                            Vector2 pos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * wallRadius;
                            Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * (10f + wave * 2f);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                                ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 3, 1f, Main.myPlayer);
                        }
                        for (int i = 0; i < 6; i++) {
                            float angle = baseAngle + MathHelper.Pi + Main.rand.NextFloat(-0.5f, 0.5f);
                            Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (8f + Main.rand.NextFloat(4f));
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                ModContent.ProjectileType<RunicEnergyOrbs>(), NPC.damage / 4, 0f, Main.myPlayer);
                        }
                    }
                }

                if (AttackTimer > 110) TransitionTo(BossPhase.Phase3_Pursuit);
            }
        }

        private void AI_P3_JudgmentStorm(Player target) {
            float coilAngle = AttackTimer * 0.12f;
            float radius = 220f + MathF.Sin(coilAngle * 0.3f) * 80f;
            Vector2 orbitPos = target.Center + new Vector2(MathF.Cos(coilAngle), MathF.Sin(coilAngle)) * radius;
            orbitPos.Y -= 60f;
            NPC.velocity = (orbitPos - NPC.Center) * 0.18f;

            if (AttackTimer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 18f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel * comboMultiplier,
                    ModContent.ProjectileType<RunicCleaveWaves>(), NPC.damage / 4, 1f, Main.myPlayer);
            }

            if (AttackTimer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 3; i++) {
                    float angle = coilAngle + MathHelper.TwoPi / 3 * i;
                    Vector2 sealPos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 150f;
                    SpawnRuneSeal(sealPos);
                }
            }

            if (AttackTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float angle = coilAngle + MathHelper.Pi;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 12f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<RunicEnergyOrbs>(), NPC.damage / 4, 0f, Main.myPlayer);
            }

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 5; i++) {
                    Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.GoldFlame, 0, 0, 60, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = -NPC.velocity * Main.rand.NextFloat(0.05f, 0.2f);
                }
            }

            if (AttackTimer > 180) TransitionTo(BossPhase.Phase3_Pursuit);
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            Vector2 origin = frame.Size() / 2f;
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // 断罪判决光束 + 格挡反击护罩环 (硬化 ACMShaders, 缺着色器自动降级)
            DrawCounterRing(spriteBatch);
            DrawJudgmentBeams();

            // 格挡态金色脉冲护盾
            if (isCounterReady) {
                float shieldPulse = MathF.Sin(globalTime * 10f) * 0.3f + 0.7f;
                Color shieldColor = new Color(255, 200, 60) * shieldPulse * 0.6f;
                for (int i = 0; i < 3; i++) {
                    float scale = NPC.scale * (1.1f + i * 0.05f);
                    spriteBatch.Draw(texture, NPC.Center - screenPos, frame, shieldColor * (1f - i * 0.3f),
                        NPC.rotation, origin, scale, effects, 0f);
                }
            }

            // 金蓝双层残影
            int trailLen = NPCID.Sets.TrailCacheLength[Type];
            for (int i = trailLen - 1; i > 0; i--) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;
                Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float t = (float)i / trailLen;
                float alpha = 0.5f * (1f - t) * (IsPhase3 ? 1.3f : 1f);
                Color trailColor = Color.Lerp(new Color(255, 200, 60), new Color(60, 120, 255), t) * alpha;
                float trailScale = NPC.scale * (1f - t * 0.03f);
                spriteBatch.Draw(texture, trailPos, frame, trailColor, NPC.rotation, origin, trailScale, effects, 0f);
            }

            spriteBatch.Draw(texture, NPC.Center - screenPos, frame, drawColor, NPC.rotation, origin, NPC.scale, effects, 0f);

            // 连击发光
            if (comboCount >= 2) {
                float comboPulse = MathF.Sin(globalTime * 6f) * 0.2f + 0.3f;
                Color comboGlow = new Color(255, 180, 50) * comboPulse * (comboCount * 0.15f);
                spriteBatch.Draw(texture, NPC.Center - screenPos, frame, comboGlow, NPC.rotation, origin, NPC.scale * 1.02f, effects, 0f);
            }

            return false;
        }

        // 判决光束 (BeamGrad): 金白芯 + 暖金边, 寿命包络淡出。非致命预警 → 不用红。
        private void DrawJudgmentBeams() {
            if (Main.dedServ)
                return;
            Color core = new(255, 240, 180);
            Color edge = TelegraphColors.Gold;
            for (int i = 0; i < judgmentBeams.Length; i++) {
                JudgmentBeam b = judgmentBeams[i];
                if (b.Time <= 0)
                    continue;
                float life = b.Time / (float)b.MaxTime;          // 1→0
                float intensity = MathF.Sin(MathHelper.Clamp(life, 0f, 1f) * MathHelper.Pi * 0.5f);
                ACMShaders.DrawBeam(b.Start, b.End, b.Width * (0.4f + 0.6f * life),
                    core, edge * 0.5f, intensity, flowSpeed: 2.2f, flowScale: 2.4f, coreSharp: 2.6f, coreGlow: 1.5f);
            }
        }

        // 格挡反击护罩环 (ArenaRunic 法阵): 冷→暖金脉冲, "现在别打"的世界级 tell。
        private void DrawCounterRing(SpriteBatch sb) {
            if (Main.dedServ || !isCounterReady)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            float pulse = 0.55f + MathF.Sin(globalTime * 12f) * 0.35f;
            ACMShaders.WorldDecalParams(NPC.Center, 140f, out Vector2 uv, out float radFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uRadius"]?.SetValue(radFrac);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(pulse, 0f, 1f));
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uColorPrimary"]?.SetValue(new Vector4(TelegraphColors.Gold.ToVector3(), 1f));
            fx.Parameters["uColorSecondary"]?.SetValue(new Vector4(new Color(255, 150, 50).ToVector3(), 1f));
            fx.Parameters["uRuneFreq"]?.SetValue(14f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uShape"]?.SetValue(0f);
            ACMShaders.DrawScreenSpaceDecal(sb, fx, BlendState.Additive);
        }

        #endregion
    }
}
