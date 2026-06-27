using AncientChineseMythology.Celestias.Boss.Arguses.Items;
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

namespace AncientChineseMythology.Celestias.Boss.Arguses
{
    /// <summary>
    /// 天目·追魂弧 / Argus, the Soul-Piercing Arc
    /// 独眼弓将 — 后月球领主Boss (约200万HP)
    ///
    /// 核心机制:
    ///   1. 预判射击系统 — 根据玩家速度预测落点射箭,奖励不规则走位
    ///   2. 瞬移换位 — 不悬停硬射,而是快速闪现到多个位置连续射击
    ///   3. 凝视锁定 — 可见的瞄准粒子线→蓄力→致命精准射击(可读的电报)
    ///   4. 极远距离 — 始终保持超远距离,被接近时会闪退
    ///
    /// 一阶段(100%-60%): 审视 — 预判齐射+星系锁链+天弓三连+视界压制
    /// 二阶段(60%-30%): 追猎 — 瞬移射击+星界牢笼+光刃布雷+星落箭雨
    /// 三阶段(30%-0%): 天目审判 — 追魂万矢+全视之域+凝视扫射+最终审判
    /// </summary>
    [AutoloadBossHead]
    public class Argus : ModNPC
    {
        #region 常量

        internal const float Phase2Threshold = 0.60f;
        internal const float Phase3Threshold = 0.30f;
        private const float MinKeepDistance = 450f;  // 最小保持距离
        private const float PreferredDistance = 600f; // 偏好距离
        private const float FlashStepSpeed = 55f;    // 瞬移速度

        #endregion

        #region 状态枚举

        public enum BossPhase
        {
            Intro,

            // 一阶段 — 审视
            Phase1_Reposition,       // 高频换位(非轨道巡逻)
            Phase1_PredictiveVolley, // 预判齐射 — 射向玩家预测位置
            Phase1_GalaxyChains,    // 星系锁链 — 在两点间生成球链屏障
            Phase1_TripleShot,      // 天弓三连 — 三发预判层级不同的箭
            Phase1_VisionSuppress,  // 视界压制 — 慢速球铺路+间隙精准射击

            PhaseTransition_2,

            // 二阶段 — 追猎
            Phase2_Reposition,       // 加强换位
            Phase2_FlashStepVolley, // 瞬移射击 — 快速传送多点连射
            Phase2_AstralPrison,    // 星界牢笼 — 收缩球包围+外部射击
            Phase2_WingbladeMines,  // 光刃布雷 — 在玩家路径上布置触发光刃
            Phase2_StarFallRain,    // 星落箭雨 — 高空倾泻+交替预判
            Phase2_SniperDuel,      // 狙击对决 — 极远距离+凝视锁定+致命一击

            PhaseTransition_3,

            // 三阶段 — 天目审判
            Phase3_Reposition,       // 最高压换位
            Phase3_SoulMyriad,      // 追魂万矢 — 全方位预判箭阵
            Phase3_AllSeeingDomain, // 全视之域 — 眼形球阵+中心穿射
            Phase3_GazeSweep,       // 凝视扫射 — 旋转式箭阵横扫
            Phase3_FinalJudgment    // 最终审判 — 收缩环+预判射击的终极大招
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
        private int flashStepCount;     // 瞬移计数
        private float glowIntensity = 1f;
        private Vector2 flashTarget;    // 瞬移目标位
        private float gazeAngle;        // 凝视扫射角度
        private int gazeLockTimer;      // 凝视蓄力计时

        // === V2 视觉提升 (纯本地视觉, 各客户端自行运算 AI, 不入网络同步) ===
        private float gazeBeamFlash;    // 狙击/凝视命中瞬间的沿线闪白衰减
        private float bloomBurst;       // 离散加性径向泛光脉冲 0~1
        private float bloomBurstRadius; // 当前泛光半径(屏高比例)
        private Color bloomBurstColor = new(190, 110, 255);
        private float domainPower;      // 「全视之域」签名进度 0~1 (驱动巨眼锁定/折射/虹环)
        private float ambientVoid;      // 虚空「被注视」屏幕染色强度

        /// <summary>供 <see cref="ArgusSky"/> 读取的天目巨眼锁定信号 (0~1)。本地视觉, 各端自算。</summary>
        public static float DomainSignal;

        #endregion

        #region ModNPC重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 14;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 120;
            NPC.height = 160;
            NPC.damage = 180;
            NPC.defense = 70;
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

        public override void BossLoot(ref int potionType) => potionType = ItemID.SuperHealingPotion;

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<GeneralOrder>()));
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<SoulPiercingArc>(),
                ModContent.ItemType<LuminanceStellarCannon>(),
                ModContent.ItemType<LuminousIrisAnnihilator>()
            ));
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
            writer.Write(flashStepCount);
            writer.Write(gazeAngle);
            writer.Write(gazeLockTimer);
            writer.WriteVector2(flashTarget);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            flashStepCount = reader.ReadInt32();
            gazeAngle = reader.ReadSingle();
            gazeLockTimer = reader.ReadInt32();
            flashTarget = reader.ReadVector2();
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.8f;
            return null;
        }

        public override bool CheckActive() => false;

        public override void HitEffect(NPC.HitInfo hit) {
            for (int i = 0; i < 6; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, hit.HitDirection * 2f, -1f, 150, default, 1.5f);
            }
            if (NPC.life <= 0) {
                for (int i = 0; i < 50; i++) {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, 0, 0, 100, default, 2.5f);
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
            DownedBossSystem.downedArgus = true;
            DomainSignal = 0f;
            if (Main.netMode != NetmodeID.Server)
                ACMScreenShakeSystem.Add(16f);
        }

        #endregion

        #region AI工具函数

        /// <summary>计算玩家预测位置(预判射击核心)</summary>
        private Vector2 PredictPosition(Player target, float predictFrames) {
            return target.Center + target.velocity * predictFrames;
        }

        /// <summary>向预测位置射箭</summary>
        private void FirePredictiveArrow(Player target, float predictFrames, float speed, float damageScale = 0.25f) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Vector2 predicted = PredictPosition(target, predictFrames);
            Vector2 vel = (predicted - NPC.Center).SafeNormalize(Vector2.UnitY) * speed;
            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                ModContent.ProjectileType<StarSightArrows>(), (int)(NPC.damage * damageScale), 1f, Main.myPlayer);
        }

        /// <summary>绘制凝视线(瞄准粒子)——给玩家可读的电报</summary>
        private void DrawGazeLine(Player target) {
            if (Main.netMode == NetmodeID.Server) return;
            Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
            float dist = NPC.Distance(target.Center);
            int particleCount = (int)(dist / 30f);
            for (int i = 0; i < particleCount; i++) {
                float t = (float)i / particleCount;
                Vector2 pos = NPC.Center + dir * (dist * t);
                // 闪烁的紫色瞄准线
                if (Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustDirect(pos, 0, 0, DustID.PurpleTorch, 0, 0, 150, default, 0.8f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                    d.fadeIn = 0.5f;
                }
            }
        }

        /// <summary>选择远距离重新定位的位置</summary>
        private Vector2 GetRepositionTarget(Player target) {
            // 随机选择一个远离当前位置、保持偏好距离的新位点
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            // 确保不选择在玩家正下方的位置
            float y = MathF.Sin(angle) * PreferredDistance;
            if (y > -100) y = -100 - Main.rand.NextFloat(200);
            return target.Center + new Vector2(MathF.Cos(angle) * PreferredDistance, y);
        }

        /// <summary>如果被逼近,执行闪退</summary>
        private bool CheckFlashRetreat(Player target) {
            float dist = NPC.Distance(target.Center);
            if (dist < MinKeepDistance * 0.7f) {
                // 紧急闪退——瞬间拉开距离
                Vector2 retreatDir = (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY);
                NPC.Center = target.Center + retreatDir * PreferredDistance;
                NPC.velocity = retreatDir * 8f;
                NPC.netUpdate = true;

                if (Main.netMode != NetmodeID.Server) {
                    // 闪退残影粒子
                    for (int i = 0; i < 15; i++) {
                        int dustType = i % 2 == 0 ? DustID.PurpleTorch : DustID.BlueTorch;
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, dustType, 0, 0, 80, default, 2f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(6, 6);
                    }
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.8f }, NPC.Center);
                }
                return true;
            }
            return false;
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
                case BossPhase.Intro: AI_Intro(target); break;

                case BossPhase.Phase1_Reposition: AI_Phase1Reposition(target); break;
                case BossPhase.Phase1_PredictiveVolley: AI_P1_PredictiveVolley(target); break;
                case BossPhase.Phase1_GalaxyChains: AI_P1_GalaxyChains(target); break;
                case BossPhase.Phase1_TripleShot: AI_P1_TripleShot(target); break;
                case BossPhase.Phase1_VisionSuppress: AI_P1_VisionSuppress(target); break;

                case BossPhase.PhaseTransition_2: AI_PhaseTransition2(target); break;

                case BossPhase.Phase2_Reposition: AI_Phase2Reposition(target); break;
                case BossPhase.Phase2_FlashStepVolley: AI_P2_FlashStepVolley(target); break;
                case BossPhase.Phase2_AstralPrison: AI_P2_AstralPrison(target); break;
                case BossPhase.Phase2_WingbladeMines: AI_P2_WingbladeMines(target); break;
                case BossPhase.Phase2_StarFallRain: AI_P2_StarFallRain(target); break;
                case BossPhase.Phase2_SniperDuel: AI_P2_SniperDuel(target); break;

                case BossPhase.PhaseTransition_3: AI_PhaseTransition3(target); break;

                case BossPhase.Phase3_Reposition: AI_Phase3Reposition(target); break;
                case BossPhase.Phase3_SoulMyriad: AI_P3_SoulMyriad(target); break;
                case BossPhase.Phase3_AllSeeingDomain: AI_P3_AllSeeingDomain(target); break;
                case BossPhase.Phase3_GazeSweep: AI_P3_GazeSweep(target); break;
                case BossPhase.Phase3_FinalJudgment: AI_P3_FinalJudgment(target); break;
            }

            UpdateVisuals();
        }

        private void UpdateVisuals() {
            // 弓将面向玩家(始终瞄准)
            Player target = Main.player[NPC.target];
            if (target.active) {
                NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;
            }
            NPC.rotation = NPC.velocity.X * 0.015f;

            float baseIntensity = IsPhase3 ? 1.6f : IsPhase2 ? 1.3f : 1f;
            glowIntensity = baseIntensity + MathF.Sin(globalTime * 4f) * 0.15f;

            // 独眼发光
            Lighting.AddLight(NPC.Center, new Vector3(0.5f, 0.3f, 0.9f) * glowIntensity);

            // —— V2 视觉通道演进 (各端自算; 衰减/插值纯本地) ——
            gazeBeamFlash *= 0.86f;
            bloomBurst *= 0.90f;

            // 「全视之域」签名进度: 仅 Phase3_AllSeeingDomain 拉满, 其余回落
            float domainTarget = Phase == BossPhase.Phase3_AllSeeingDomain ? 1f : 0f;
            domainPower = MathHelper.Lerp(domainPower, domainTarget, domainTarget > 0f ? 0.06f : 0.10f);
            DomainSignal = domainPower;

            // 虚空「被注视」染色: P2 起轻染, P3 加重, 域内拉满
            float voidTarget = (IsPhase3 ? 0.5f : IsPhase2 ? 0.22f : 0f) + domainPower * 0.5f;
            ambientVoid = MathHelper.Lerp(ambientVoid, MathHelper.Clamp(voidTarget, 0f, 1f), 0.05f);

            // 发布给屏幕氛围系统 (域虹环以玩家为心)
            Vector2 domainCenter = target.active ? target.Center : NPC.Center;
            ArgusScreenSystem.Publish(ambientVoid, domainPower, domainCenter, (float)Main.GlobalTimeWrappedHourly);
        }

        /// <summary>触发一次离散加性径向泛光脉冲 (下一帧 PreDraw 经 DrawRadialBloomAt 绘制)。</summary>
        private void TriggerBloom(float radius, Color color) {
            bloomBurst = 1f;
            bloomBurstRadius = radius;
            bloomBurstColor = color;
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
            flashStepCount = 0;
            gazeLockTimer = 0;
            NPC.dontTakeDamage = false;
            NPC.netUpdate = true;
        }

        private BossPhase GetP1Attack() => (BossPhase)(Main.rand.Next(4) switch {
            0 => (int)BossPhase.Phase1_PredictiveVolley,
            1 => (int)BossPhase.Phase1_GalaxyChains,
            2 => (int)BossPhase.Phase1_TripleShot,
            _ => (int)BossPhase.Phase1_VisionSuppress
        });

        private BossPhase GetP2Attack() => (BossPhase)(Main.rand.Next(5) switch {
            0 => (int)BossPhase.Phase2_FlashStepVolley,
            1 => (int)BossPhase.Phase2_AstralPrison,
            2 => (int)BossPhase.Phase2_WingbladeMines,
            3 => (int)BossPhase.Phase2_StarFallRain,
            _ => (int)BossPhase.Phase2_SniperDuel
        });

        private BossPhase GetP3Attack() => (BossPhase)(Main.rand.Next(4) switch {
            0 => (int)BossPhase.Phase3_SoulMyriad,
            1 => (int)BossPhase.Phase3_AllSeeingDomain,
            2 => (int)BossPhase.Phase3_GazeSweep,
            _ => (int)BossPhase.Phase3_FinalJudgment
        });

        /// <summary>保持远距离悬停——弓将的独特行为</summary>
        private void MaintainDistance(Player target, float preferDist) {
            float dist = NPC.Distance(target.Center);
            Vector2 dir = (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY);

            if (dist < preferDist * 0.75f) {
                // 太近了——后退
                NPC.velocity = Vector2.Lerp(NPC.velocity, dir * 12f, 0.15f);
            }
            else if (dist > preferDist * 1.3f) {
                // 太远了——靠近
                NPC.velocity = Vector2.Lerp(NPC.velocity, -dir * 8f, 0.1f);
            }
            else {
                // 适当距离——横向移动(难以预测)
                Vector2 lateral = new Vector2(-dir.Y, dir.X) * MathF.Sin(globalTime * 2f) * 6f;
                lateral.Y -= 2f; // 偏好在上方
                NPC.velocity = Vector2.Lerp(NPC.velocity, lateral, 0.08f);
            }
        }

        #endregion

        #region 入场演出

        private void AI_Intro(Player target) {
            if (PhaseTimer == 1) {
                NPC.Center = target.Center + new Vector2(0, -1200);
                NPC.velocity = Vector2.Zero;
                NPC.Opacity = 0f;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 1.2f }, target.Center);
            }

            NPC.Opacity = MathHelper.Clamp(PhaseTimer / 60f, 0f, 1f);
            float introProgress = MathHelper.Clamp(PhaseTimer / 130f, 0f, 1f);
            float eased = ACMUtils.SineInOut(introProgress);
            Vector2 targetPos = target.Center + new Vector2(0, -500);
            NPC.Center = Vector2.Lerp(target.Center + new Vector2(0, -1200), targetPos, eased);
            NPC.velocity *= 0.9f;

            if (Main.netMode != NetmodeID.Server) {
                // 星系粒子双螺旋——独眼从虚空中浮现
                for (int arm = 0; arm < 2; arm++) {
                    for (int i = 0; i < 4; i++) {
                        float angle = globalTime * (5f + arm * 3f) + arm * MathHelper.Pi + i * MathHelper.PiOver2;
                        float dist = MathHelper.Lerp(350, 40, eased);
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        int dustType = arm == 0 ? DustID.PurpleTorch : DustID.BlueTorch;
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 100, default, 2.2f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * (4f + arm * 2f);
                    }
                }

                // 独眼逐渐亮起——中心紫色光点
                if (PhaseTimer > 40) {
                    float eyeGlow = MathHelper.Clamp((PhaseTimer - 40) / 50f, 0, 1);
                    for (int i = 0; i < (int)(eyeGlow * 4); i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(10, 10),
                            0, 0, DustID.PurpleTorch, 0, 0, 50, default, 1.5f + eyeGlow);
                        d.noGravity = true;
                        d.velocity = Vector2.Zero;
                    }
                }
            }

            // "天目"睁开——脉冲闪光
            if (PhaseTimer == 95) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.3f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    ACMScreenShakeSystem.Add(6f);
                    TriggerBloom(0.16f, new Color(190, 110, 255));
                    for (int i = 0; i < 25; i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.PurpleTorch, 0, 0, 60, default, 3.5f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(10, 10);
                    }
                }
            }

            if (PhaseTimer >= 130) {
                NPC.Opacity = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    ACMScreenShakeSystem.Add(14f);
                    TriggerBloom(0.24f, new Color(190, 110, 255));
                }
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);

                // 入场: 12方向预判箭阵——展示预判射击主题
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 12; i++) {
                        float angle = MathHelper.TwoPi / 12 * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 10f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<StarSightArrows>(), NPC.damage / 5, 1f, Main.myPlayer);
                    }
                    // 6颗星系球施压
                    for (int i = 0; i < 6; i++) {
                        float angle = MathHelper.TwoPi / 6 * i + MathHelper.ToRadians(15);
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 6f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<SpinningGalacticOrbs>(), NPC.damage / 5, 0f, Main.myPlayer);
                    }
                }

                TransitionTo(BossPhase.Phase1_Reposition);
            }
        }

        #endregion

        #region 一阶段: 审视

        /// <summary>高频换位——弓将的"巡逻"是不断变换位置而非固定轨道</summary>
        private void AI_Phase1Reposition(Player target) {
            MaintainDistance(target, PreferredDistance);

            // 检测是否被逼近
            CheckFlashRetreat(target);

            // 换位中持续发射预判箭施压
            if (PhaseTimer % 25 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                FirePredictiveArrow(target, 20f, 14f, 0.2f);
            }

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4)) {
                int dustType = Main.rand.NextBool() ? DustID.PurpleTorch : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(40, 40), 0, 0, dustType, 0, 0, 100, default, 1.3f);
                d.noGravity = true;
            }

            if (PhaseTimer > 75) TransitionTo(GetP1Attack());
        }

        /// <summary>预判齐射——同时向当前位置和多个预测位置射箭</summary>
        private void AI_P1_PredictiveVolley(Player target) {
            MaintainDistance(target, PreferredDistance);
            CheckFlashRetreat(target);

            int fireInterval = Main.expertMode ? 10 : 14;
            if (AttackTimer % fireInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                // 三级预判: 当前位、0.5秒后、1秒后
                float[] predictTimes = { 0f, 15f, 30f };
                foreach (float pt in predictTimes) {
                    Vector2 predicted = PredictPosition(target, pt);
                    Vector2 vel = (predicted - NPC.Center).SafeNormalize(Vector2.UnitY) * 18f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<StarSightArrows>(), NPC.damage / 4, 1f, Main.myPlayer);
                }
                SoundEngine.PlaySound(SoundID.Item5 with { Pitch = 0.5f, Volume = 0.8f }, NPC.Center);
            }

            // 间隔释放旋转星系球扰乱走位节奏
            if (AttackTimer % 30 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 3; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 5f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<SpinningGalacticOrbs>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            if (AttackTimer > 120) TransitionTo(BossPhase.Phase1_Reposition);
        }

        /// <summary>星系锁链——在玩家逃跑路线上形成球链屏障</summary>
        private void AI_P1_GalaxyChains(Player target) {
            MaintainDistance(target, PreferredDistance);

            if (AttackTimer == 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.2f, Volume = 0.9f }, target.Center);

                // 在玩家移动方向的两侧建立球链墙
                Vector2 moveDir = target.velocity.SafeNormalize(Vector2.UnitX);
                Vector2 perpDir = new Vector2(-moveDir.Y, moveDir.X);

                for (int side = -1; side <= 1; side += 2) {
                    for (int i = 0; i < 10; i++) {
                        Vector2 pos = target.Center + moveDir * (i * 50 - 250) + perpDir * side * 200;
                        Vector2 vel = -perpDir * side * 1.5f; // 缓慢向内收缩
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                            ModContent.ProjectileType<SpinningGalacticOrbs>(), NPC.damage / 5, 0f, Main.myPlayer);
                    }
                }
            }

            // 在墙壁形成后通过中间缝隙射出精准箭
            if (AttackTimer > 25 && AttackTimer % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                FirePredictiveArrow(target, 15f, 20f, 0.25f);
                SoundEngine.PlaySound(SoundID.Item5 with { Pitch = 0.6f, Volume = 0.6f }, NPC.Center);
            }

            if (AttackTimer > 130) TransitionTo(BossPhase.Phase1_Reposition);
        }

        /// <summary>天弓三连——快速三发，每发预判级别不同(当前/近/远)</summary>
        private void AI_P1_TripleShot(Player target) {
            // 保持极远距离
            Vector2 hoverPos = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY) * 650f;
            hoverPos.Y = Math.Min(hoverPos.Y, target.Center.Y - 200);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.06f, 0.1f);

            // 三连射 (快速连发)
            int[] shotFrames = { 15, 22, 29 };
            float[] predicts = { 5f, 20f, 40f };
            float[] speeds = { 22f, 20f, 18f };

            for (int s = 0; s < 3; s++) {
                if (AttackTimer == shotFrames[s] && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 predicted = PredictPosition(target, predicts[s]);
                    Vector2 vel = (predicted - NPC.Center).SafeNormalize(Vector2.UnitY) * speeds[s];
                    // 每发带一点扇形扩散
                    for (int i = -1; i <= 1; i++) {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                            vel.RotatedBy(i * MathHelper.ToRadians(5f)),
                            ModContent.ProjectileType<StarSightArrows>(), NPC.damage / 4, 1f, Main.myPlayer);
                    }
                    SoundEngine.PlaySound(SoundID.Item5 with { Pitch = 0.3f + s * 0.3f, Volume = 0.9f }, NPC.Center);

                    if (Main.netMode != NetmodeID.Server)
                        ACMScreenShakeSystem.Add(MathHelper.Clamp(3f + s * 2f, 0f, 6f));
                }
            }

            if (AttackTimer > 60) TransitionTo(BossPhase.Phase1_Reposition);
        }

        /// <summary>视界压制——大量慢速星系球铺满区域+通过缝隙精准射击</summary>
        private void AI_P1_VisionSuppress(Player target) {
            MaintainDistance(target, PreferredDistance);

            // 先铺设大量慢速星系球覆盖区域
            if (AttackTimer < 40 && AttackTimer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = 6;
                float baseAngle = AttackTimer * 0.15f;
                for (int i = 0; i < count; i++) {
                    float angle = baseAngle + MathHelper.TwoPi / count * i;
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 4f; // 慢速
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<SpinningGalacticOrbs>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            // 球铺好后开始精准射击——玩家需要在球之间的空隙中闪避箭矢
            if (AttackTimer > 45 && AttackTimer % 6 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                FirePredictiveArrow(target, 12f, 22f, 0.25f);
                SoundEngine.PlaySound(SoundID.Item5 with { Pitch = 0.7f, Volume = 0.5f }, NPC.Center);
            }

            if (AttackTimer > 130) TransitionTo(BossPhase.Phase1_Reposition);
        }

        #endregion

        #region 阶段转换演出

        private void AI_PhaseTransition2(Player target) {
            NPC.velocity *= 0.93f;
            NPC.dontTakeDamage = true;

            if (Main.netMode != NetmodeID.Server) {
                // 独眼睁大——紫蓝粒子高速旋涡
                float shrink = MathHelper.Clamp(1f - PhaseTimer / 60f, 0.1f, 1f);
                for (int arm = 0; arm < 3; arm++) {
                    for (int i = 0; i < 5; i++) {
                        float angle = globalTime * (5f + arm * 2f) + arm * MathHelper.TwoPi / 3 + i * MathHelper.TwoPi / 5;
                        float dist = 350 * shrink;
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        int dustType = arm == 1 ? DustID.BlueTorch : DustID.PurpleTorch;
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 80, default, 2.5f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 8f;
                    }
                }

                // 独眼中心脉冲
                if (PhaseTimer % 5 == 0) {
                    Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.PurpleTorch, 0, 0, 30, default, 3f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }
            }

            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    ACMScreenShakeSystem.Add(10f);
                    TriggerBloom(0.26f, new Color(180, 100, 255));
                }

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    // 爆发: 16方向箭 + 12方向星系球 + 8方向光刃
                    for (int i = 0; i < 16; i++) {
                        float angle = MathHelper.TwoPi / 16 * i;
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 12f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<StarSightArrows>(), NPC.damage / 4, 1f, Main.myPlayer);
                    }
                    for (int i = 0; i < 12; i++) {
                        float angle = MathHelper.TwoPi / 12 * i + MathHelper.ToRadians(15);
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 7f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<SpinningGalacticOrbs>(), NPC.damage / 5, 0f, Main.myPlayer);
                    }
                    for (int i = 0; i < 8; i++) {
                        float angle = MathHelper.TwoPi / 8 * i + MathHelper.ToRadians(22.5f);
                        Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 9f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<AetherealWingblades>(), NPC.damage / 4, 1f, Main.myPlayer);
                    }
                }
            }

            if (PhaseTimer >= 90) {
                NPC.dontTakeDamage = false;
                NPC.defense += 12;
                NPC.damage = (int)(NPC.damage * 1.2f);
                TransitionTo(BossPhase.Phase2_Reposition);
            }
        }

        private void AI_PhaseTransition3(Player target) {
            NPC.velocity *= 0.90f;
            NPC.dontTakeDamage = true;

            if (Main.netMode != NetmodeID.Server) {
                // "天目"完全睁开——四层旋涡聚焦独眼
                for (int layer = 0; layer < 4; layer++) {
                    int count = 6 + layer * 3;
                    float speed = 4f + layer * 2f;
                    float dist = MathHelper.Lerp(500 - layer * 80, 30, MathHelper.Clamp(PhaseTimer / 80f, 0, 1));
                    for (int i = 0; i < count; i++) {
                        float angle = globalTime * speed + MathHelper.TwoPi / count * i + layer * MathHelper.Pi / 8;
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        int dustType = layer % 2 == 0 ? DustID.PurpleTorch : DustID.BlueTorch;
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 40, default, 3f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 14f;
                    }
                }
            }

            if (PhaseTimer == 70) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 1.6f }, NPC.Center);
                if (Main.netMode != NetmodeID.Server) {
                    ACMScreenShakeSystem.Add(12f);
                    TriggerBloom(0.32f, new Color(190, 110, 255));
                }

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    // 超大规模爆发
                    for (int wave = 0; wave < 3; wave++) {
                        int count = 12 + wave * 6;
                        for (int i = 0; i < count; i++) {
                            float angle = MathHelper.TwoPi / count * i + wave * MathHelper.ToRadians(10);
                            Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (8f + wave * 4f);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                ModContent.ProjectileType<StarSightArrows>(), NPC.damage / 3, 1f, Main.myPlayer);
                        }
                    }
                }
            }

            if (PhaseTimer >= 110) {
                NPC.dontTakeDamage = false;
                NPC.defense += 18;
                NPC.damage = (int)(NPC.damage * 1.3f);
                glowIntensity = 1.8f;
                TransitionTo(BossPhase.Phase3_Reposition);
            }
        }

        #endregion

        #region 二阶段: 追猎

        private void AI_Phase2Reposition(Player target) {
            MaintainDistance(target, PreferredDistance);
            CheckFlashRetreat(target);

            // 换位时更高频预判射击
            if (PhaseTimer % 18 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                FirePredictiveArrow(target, 18f, 16f, 0.2f);

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                int dustType = Main.rand.NextBool() ? DustID.PurpleTorch : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(40, 40), 0, 0, dustType, 0, 0, 100, default, 1.5f);
                d.noGravity = true;
            }

            if (PhaseTimer > 60) TransitionTo(GetP2Attack());
        }

        /// <summary>瞬移射击——快速传送到多个位置，每到一处就射一波</summary>
        private void AI_P2_FlashStepVolley(Player target) {
            if (SubState == 0) {
                // 闪移前的短暂蓄力
                NPC.velocity *= 0.8f;

                if (Main.netMode != NetmodeID.Server) {
                    // 身体闪烁——即将瞬移的预告
                    if (AttackTimer % 3 == 0) {
                        for (int i = 0; i < 4; i++) {
                            int dustType = i % 2 == 0 ? DustID.PurpleTorch : DustID.BlueTorch;
                            Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(30, 30), 0, 0, dustType, 0, 0, 60, default, 2f);
                            d.noGravity = true;
                        }
                    }
                }

                if (AttackTimer > 12) {
                    SubState = 1;
                    AttackTimer = 0;

                    // 瞬移到新位置
                    flashTarget = GetRepositionTarget(target);
                    NPC.Center = flashTarget;
                    NPC.velocity = Vector2.Zero;
                    NPC.netUpdate = true;

                    // 瞬移特效
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.9f, Pitch = 0.3f }, NPC.Center);
                        for (int i = 0; i < 12; i++) {
                            int dustType = i % 2 == 0 ? DustID.PurpleTorch : DustID.BlueTorch;
                            Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, dustType, 0, 0, 60, default, 2.5f);
                            d.noGravity = true;
                            d.velocity = Main.rand.NextVector2Circular(8, 8);
                        }
                    }
                }
            }
            else {
                NPC.velocity *= 0.9f;

                // 到达位置后立刻射出预判箭扇
                if (AttackTimer == 3 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 toPlayer = (PredictPosition(target, 15f) - NPC.Center).SafeNormalize(Vector2.UnitY);
                    int arrowCount = Main.expertMode ? 5 : 3;
                    float spread = MathHelper.ToRadians(8f);
                    for (int i = -arrowCount / 2; i <= arrowCount / 2; i++) {
                        Vector2 vel = toPlayer.RotatedBy(i * spread) * 22f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<StarSightArrows>(), NPC.damage / 4, 1f, Main.myPlayer);
                    }
                    SoundEngine.PlaySound(SoundID.Item5 with { Pitch = 0.6f, Volume = 0.7f }, NPC.Center);
                }

                if (AttackTimer > 15) {
                    flashStepCount++;
                    if (flashStepCount < (Main.expertMode ? 5 : 4)) {
                        SubState = 0;
                        AttackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    else TransitionTo(BossPhase.Phase2_Reposition);
                }
            }
        }

        /// <summary>星界牢笼——球墙从外围收缩包围，Boss从外部持续射击</summary>
        private void AI_P2_AstralPrison(Player target) {
            // Boss在远处
            MaintainDistance(target, PreferredDistance * 1.2f);

            if (AttackTimer == 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f }, target.Center);

                // 三层收缩球笼
                for (int ring = 0; ring < 3; ring++) {
                    int count = 16 + ring * 6;
                    float radius = 500f + ring * 100f;
                    for (int i = 0; i < count; i++) {
                        float angle = MathHelper.TwoPi / count * i + ring * MathHelper.ToRadians(8);
                        Vector2 pos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                        Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * (2f + ring * 1.2f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                            ModContent.ProjectileType<SpinningGalacticOrbs>(), NPC.damage / 5, 0f, Main.myPlayer);
                    }
                }
            }

            // 牢笼收缩同时从外部射入精确箭——需要在球笼的缝隙中同时躲避箭矢
            if (AttackTimer > 15 && AttackTimer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                FirePredictiveArrow(target, 10f, 24f, 0.25f);
                // 双侧光刃
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                for (int side = -1; side <= 1; side += 2) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                        dir.RotatedBy(side * MathHelper.ToRadians(25f)) * 14f,
                        ModContent.ProjectileType<AetherealWingblades>(), NPC.damage / 4, 1f, Main.myPlayer);
                }
            }

            if (AttackTimer > 150) TransitionTo(BossPhase.Phase2_Reposition);
        }

        /// <summary>光刃布雷——在玩家预测路径上布置多颗休眠光刃"地雷"</summary>
        private void AI_P2_WingbladeMines(Player target) {
            MaintainDistance(target, PreferredDistance);

            // 在玩家预测移动路径上分批放置光刃(ai0=1标记为地雷模式)
            if (AttackTimer % 8 == 0 && AttackTimer < 80 && Main.netMode != NetmodeID.MultiplayerClient) {
                // 以玩家前方的扇形区域为目标
                Vector2 moveDir = target.velocity.SafeNormalize(Main.rand.NextVector2Unit());
                float spread = MathHelper.ToRadians(30f);
                for (int i = -1; i <= 1; i++) {
                    Vector2 minePos = target.Center + moveDir.RotatedBy(i * spread) * (200 + AttackTimer * 3);
                    // 光刃ai0=1为地雷模式——静止等待触发
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), minePos, Vector2.Zero,
                        ModContent.ProjectileType<AetherealWingblades>(), NPC.damage / 4, 0f, Main.myPlayer,
                        ai0: 1f);
                }
            }

            // 同步射出驱赶箭——迫使玩家往布雷区域移动
            if (AttackTimer > 20 && AttackTimer % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                // 从玩家安全方向射箭迫使走向地雷区
                Vector2 safeDir = -(target.velocity.SafeNormalize(Vector2.UnitX));
                Vector2 vel = (target.Center + safeDir * 100 - NPC.Center).SafeNormalize(Vector2.UnitY) * 20f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<StarSightArrows>(), NPC.damage / 4, 1f, Main.myPlayer);
            }

            if (AttackTimer > 120) TransitionTo(BossPhase.Phase2_Reposition);
        }

        /// <summary>星落箭雨——在玩家上空倾泻大量预判箭</summary>
        private void AI_P2_StarFallRain(Player target) {
            // 移动到玩家正上方远处
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime * 2f) * 200f, -600);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.08f);

            int interval = Main.expertMode ? 4 : 6;
            if (AttackTimer % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                // 从上方密集射下——交替使用预判和直射
                Vector2 predicted = AttackTimer % (interval * 2) == 0
                    ? PredictPosition(target, 20f)
                    : target.Center;

                Vector2 spawnPos = NPC.Center + new Vector2(Main.rand.NextFloat(-80, 80), 0);
                Vector2 vel = (predicted - spawnPos).SafeNormalize(Vector2.UnitY) * 20f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                    ModContent.ProjectileType<StarSightArrows>(), NPC.damage / 4, 1f, Main.myPlayer);
            }

            // 两侧光刃封路
            if (AttackTimer % 25 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int side = -1; side <= 1; side += 2) {
                    Vector2 pos = target.Center + new Vector2(side * 400, -300);
                    Vector2 vel = new Vector2(-side * 8f, 6f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                        ModContent.ProjectileType<AetherealWingblades>(), NPC.damage / 4, 1f, Main.myPlayer);
                }
            }

            if (AttackTimer > 140) TransitionTo(BossPhase.Phase2_Reposition);
        }

        /// <summary>狙击对决——凝视锁定+蓄力+致命精准射击(可读电报)</summary>
        private void AI_P2_SniperDuel(Player target) {
            if (SubState == 0) {
                // 拉开极远距离
                Vector2 farPos = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY) * 800f;
                farPos.Y = Math.Min(farPos.Y, target.Center.Y - 300);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (farPos - NPC.Center) * 0.06f, 0.1f);

                if (AttackTimer > 20) {
                    SubState = 1;
                    AttackTimer = 0;
                    gazeLockTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                // 凝视锁定——绘制瞄准线
                NPC.velocity *= 0.92f;
                gazeLockTimer++;

                // 可视电报: 瞄准线
                DrawGazeLine(target);

                // 蓄力粒子
                if (Main.netMode != NetmodeID.Server) {
                    float chargeProgress = MathHelper.Clamp(gazeLockTimer / 60f, 0f, 1f);
                    for (int i = 0; i < (int)(chargeProgress * 8); i++) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(30, 30),
                            0, 0, DustID.PurpleTorch, 0, 0, 50, default, 1.5f + chargeProgress * 2f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - d.position).SafeNormalize(Vector2.Zero) * (3f + chargeProgress * 5f);
                    }
                }

                if (gazeLockTimer >= 60) {
                    SubState = 2;
                    AttackTimer = 0;
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.3f, Pitch = 0.2f }, NPC.Center);

                    // 火力全开——五发超高速精准箭
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int i = 0; i < 5; i++) {
                            Vector2 predicted = PredictPosition(target, 5f + i * 5f);
                            Vector2 vel = (predicted - NPC.Center).SafeNormalize(Vector2.UnitY) * 35f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                                ModContent.ProjectileType<StarSightArrows>(), NPC.damage / 3, 2f, Main.myPlayer);
                        }
                    }

                    if (Main.netMode != NetmodeID.Server) {
                        ACMScreenShakeSystem.Add(8f);
                        gazeBeamFlash = 1f;            // 沿凝视线的处决闪白
                        TriggerBloom(0.16f, TelegraphColors.Lethal);
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.95f;
                if (AttackTimer > 30) TransitionTo(BossPhase.Phase2_Reposition);
            }
        }

        #endregion

        #region 三阶段: 天目审判

        private void AI_Phase3Reposition(Player target) {
            MaintainDistance(target, PreferredDistance * 0.9f);
            CheckFlashRetreat(target);

            // 三阶段——极高压持续射击
            if (PhaseTimer % 8 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                FirePredictiveArrow(target, 15f, 18f, 0.2f);
            }

            // 间隔释放星系球
            if (PhaseTimer % 25 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 3; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 5f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<SpinningGalacticOrbs>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dustType = Main.rand.NextBool() ? DustID.PurpleTorch : DustID.BlueTorch;
                Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(50, 50), 0, 0, dustType, 0, 0, 100, default, 2f);
                d.noGravity = true;
            }

            if (PhaseTimer > 50) TransitionTo(GetP3Attack());
        }

        /// <summary>追魂万矢——从多个方向同时射出预判箭阵</summary>
        private void AI_P3_SoulMyriad(Player target) {
            NPC.velocity *= 0.95f;

            // 每波从不同方向释放预判箭
            int waveInterval = 20;
            int totalWaves = 6;

            for (int wave = 0; wave < totalWaves; wave++) {
                if (AttackTimer == wave * waveInterval + 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float baseAngle = MathHelper.TwoPi / totalWaves * wave;
                    Vector2 spawnCenter = target.Center + new Vector2(MathF.Cos(baseAngle), MathF.Sin(baseAngle)) * 550f;

                    // 从那个方向向玩家预测位置发射扇形箭阵
                    Vector2 predicted = PredictPosition(target, 12f + wave * 3f);
                    Vector2 toTarget = (predicted - spawnCenter).SafeNormalize(Vector2.UnitY);

                    int arrowCount = 7;
                    float spread = MathHelper.ToRadians(30f);
                    for (int i = 0; i < arrowCount; i++) {
                        float t = (float)i / (arrowCount - 1) - 0.5f;
                        Vector2 vel = toTarget.RotatedBy(t * spread) * 16f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnCenter, vel,
                            ModContent.ProjectileType<StarSightArrows>(), NPC.damage / 4, 1f, Main.myPlayer);
                    }

                    SoundEngine.PlaySound(SoundID.Item5 with { Pitch = 0.3f + wave * 0.1f, Volume = 0.7f }, spawnCenter);
                }
            }

            // Boss同步射出光刃
            if (AttackTimer > 15 && AttackTimer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 14f;
                for (int side = -1; side <= 1; side++) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                        vel.RotatedBy(side * MathHelper.ToRadians(15f)),
                        ModContent.ProjectileType<AetherealWingblades>(), NPC.damage / 4, 1f, Main.myPlayer);
                }
            }

            if (AttackTimer > totalWaves * waveInterval + 30)
                TransitionTo(BossPhase.Phase3_Reposition);
        }

        /// <summary>全视之域——形成眼形球阵图案，然后从中心射出穿刺线</summary>
        private void AI_P3_AllSeeingDomain(Player target) {
            Vector2 hoverPos = target.Center + new Vector2(0, -400);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.08f);

            // 球阵成形瞬间 — 天幕巨眼同步睁开锁定 + 处决泛光 (签名节拍)
            if (AttackTimer == 15) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f }, target.Center);
                if (Main.netMode != NetmodeID.Server) {
                    ACMScreenShakeSystem.Add(9f);
                    TriggerBloom(0.28f, new Color(200, 110, 255));
                }
            }

            // 构造眼形球阵(椭圆+中心线)——天目的标志性图案
            if (AttackTimer == 15 && Main.netMode != NetmodeID.MultiplayerClient) {

                // 外层椭圆
                int outerCount = 24;
                for (int i = 0; i < outerCount; i++) {
                    float angle = MathHelper.TwoPi / outerCount * i;
                    float rx = 350f, ry = 180f; // 椭圆参数——眼形
                    Vector2 pos = target.Center + new Vector2(MathF.Cos(angle) * rx, MathF.Sin(angle) * ry);
                    Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * 2f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                        ModContent.ProjectileType<SpinningGalacticOrbs>(), NPC.damage / 5, 0f, Main.myPlayer);
                }

                // 瞳孔圆(内层)
                int innerCount = 10;
                for (int i = 0; i < innerCount; i++) {
                    float angle = MathHelper.TwoPi / innerCount * i;
                    Vector2 pos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 100f;
                    Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * 1.5f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                        ModContent.ProjectileType<SpinningGalacticOrbs>(), NPC.damage / 5, 0f, Main.myPlayer);
                }
            }

            // 从Boss位置通过"瞳孔"中心射出穿刺箭线
            if (AttackTimer > 30 && AttackTimer % 6 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                FirePredictiveArrow(target, 8f, 26f, 0.3f);
                // 两侧光刃
                Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                for (int side = -1; side <= 1; side += 2) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                        dir.RotatedBy(side * MathHelper.ToRadians(30f)) * 12f,
                        ModContent.ProjectileType<AetherealWingblades>(), NPC.damage / 4, 1f, Main.myPlayer);
                }
            }

            if (AttackTimer > 150) TransitionTo(BossPhase.Phase3_Reposition);
        }

        /// <summary>凝视扫射——旋转式箭阵从Boss位置扫过整个战场</summary>
        private void AI_P3_GazeSweep(Player target) {
            // 在玩家附近盘旋
            Vector2 hoverPos = target.Center + new Vector2(0, -350);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.04f, 0.08f);

            if (AttackTimer == 1) {
                gazeAngle = (target.Center - NPC.Center).ToRotation();
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f }, NPC.Center);
            }

            // 旋转扫射——以缓慢旋转的角度持续发射密集箭矢
            float sweepSpeed = 0.035f;
            gazeAngle += sweepSpeed;

            if (AttackTimer % 3 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                // 三箭扇形从旋转角度射出
                for (int i = -1; i <= 1; i++) {
                    float angle = gazeAngle + i * MathHelper.ToRadians(8f);
                    Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 20f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                        ModContent.ProjectileType<StarSightArrows>(), NPC.damage / 4, 1f, Main.myPlayer);
                }
            }

            // 间隔添加旋转星系球
            if (AttackTimer % 20 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float angle = gazeAngle + MathHelper.Pi;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 7f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<SpinningGalacticOrbs>(), NPC.damage / 5, 0f, Main.myPlayer);
            }

            // 凝视线视觉
            if (Main.netMode != NetmodeID.Server) {
                Vector2 gazeDir = new Vector2(MathF.Cos(gazeAngle), MathF.Sin(gazeAngle));
                for (int i = 0; i < 20; i++) {
                    Vector2 pos = NPC.Center + gazeDir * i * 40f;
                    Dust d = Dust.NewDustDirect(pos, 0, 0, DustID.PurpleTorch, 0, 0, 100, default, 1f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }
            }

            if (AttackTimer > 180) TransitionTo(BossPhase.Phase3_Reposition);
        }

        /// <summary>最终审判——蓄力后释放收缩/扩张交替环+大量预判射击</summary>
        private void AI_P3_FinalJudgment(Player target) {
            if (SubState == 0) {
                // 蓄力——所有粒子向独眼汇聚
                NPC.velocity *= 0.88f;
                NPC.dontTakeDamage = true;
                NPC.Center += Main.rand.NextVector2Circular(5, 5);

                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 20; i++) {
                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float dist = Main.rand.NextFloat(150, 600);
                        Vector2 dustPos = NPC.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
                        int dustType = Main.rand.NextBool() ? DustID.PurpleTorch : DustID.BlueTorch;
                        Dust d = Dust.NewDustDirect(dustPos, 0, 0, dustType, 0, 0, 40, default, 3.5f);
                        d.noGravity = true;
                        d.velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 16f;
                    }
                }

                // 蓄力中旋转光刃施压
                if (AttackTimer % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    float spiralAngle = AttackTimer * 0.18f;
                    for (int arm = 0; arm < 3; arm++) {
                        float a = spiralAngle + arm * MathHelper.TwoPi / 3f;
                        Vector2 vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 8f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<AetherealWingblades>(), NPC.damage / 5, 0f, Main.myPlayer);
                    }
                }

                if (AttackTimer > 90) {
                    SubState = 1;
                    AttackTimer = 0;
                    NPC.dontTakeDamage = false;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.6f }, NPC.Center);

                    if (Main.netMode != NetmodeID.Server) {
                        ACMScreenShakeSystem.Add(12f);
                        TriggerBloom(0.34f, new Color(200, 110, 255));
                    }
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.93f;

                // 5波收缩/扩张交替箭环——玩家需要交替向外/向内闪避
                for (int wave = 0; wave < 5; wave++) {
                    if (AttackTimer == wave * 18 + 5 && Main.netMode != NetmodeID.MultiplayerClient) {
                        bool isContracting = wave % 2 == 0;
                        int arrowCount = 16 + wave * 4;
                        float ringRadius = isContracting ? 600f : 80f;
                        float speed = isContracting ? (10f + wave * 2f) : (8f + wave * 2f);

                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = wave * 0.1f, Volume = 0.9f }, NPC.Center);
                        if (Main.netMode != NetmodeID.Server) {
                            ACMScreenShakeSystem.Add(6f);
                            TriggerBloom(isContracting ? 0.20f : 0.12f, isContracting ? TelegraphColors.Lethal : new Color(180, 110, 255));
                        }

                        for (int i = 0; i < arrowCount; i++) {
                            float angle = MathHelper.TwoPi / arrowCount * i + wave * MathHelper.ToRadians(7f);
                            Vector2 pos, vel;
                            if (isContracting) {
                                pos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ringRadius;
                                vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * speed;
                            }
                            else {
                                pos = NPC.Center;
                                vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
                            }
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                                ModContent.ProjectileType<StarSightArrows>(), NPC.damage / 3, 1f, Main.myPlayer);
                        }

                        // 每波附带预判箭
                        for (int i = 0; i < 4; i++) {
                            FirePredictiveArrow(target, 10f + i * 8f, 22f, 0.3f);
                        }
                    }
                }

                // 外环光刃封路
                if (AttackTimer == 15 && Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 12; i++) {
                        float angle = MathHelper.TwoPi / 12 * i;
                        Vector2 pos = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 700f;
                        Vector2 vel = (target.Center - pos).SafeNormalize(Vector2.Zero) * 6f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, vel,
                            ModContent.ProjectileType<AetherealWingblades>(), NPC.damage / 4, 1f, Main.myPlayer);
                    }
                }

                if (AttackTimer > 120) TransitionTo(BossPhase.Phase3_Reposition);
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // V2: 凝视/穿刺/扫射 BeamGrad 预告线 + 离散径向泛光 (绘于本体之下)
            DrawV2Beams();

            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            Vector2 origin = frame.Size() / 2f;
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // 紫蓝双重残影——三阶段更长更亮
            int trailLen = NPCID.Sets.TrailCacheLength[Type];
            for (int i = trailLen - 1; i > 0; i--) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;
                Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float t = (float)i / trailLen;
                float alpha = 0.5f * (1f - t) * (IsPhase3 ? 1.4f : 1f);
                Color trailColor = Color.Lerp(new Color(180, 100, 255), new Color(60, 100, 255), t) * alpha;
                float trailScale = NPC.scale * (1f - t * 0.03f);
                spriteBatch.Draw(texture, trailPos, frame, trailColor, NPC.rotation, origin, trailScale, effects, 0f);
            }

            // 主体
            spriteBatch.Draw(texture, NPC.Center - screenPos, frame, drawColor, NPC.rotation, origin, NPC.scale, effects, 0f);

            // 独眼发光叠加——天目的标志
            float eyePulse = MathF.Sin(globalTime * 4f) * 0.2f + 0.5f;
            Color eyeGlow = new Color(180, 80, 255) * eyePulse * glowIntensity * 0.4f;
            spriteBatch.Draw(texture, NPC.Center - screenPos, frame, eyeGlow, NPC.rotation, origin, NPC.scale * 1.03f, effects, 0f);

            return false;
        }

        /// <summary>
        /// V2 凝视类预告线 (硬化 API <see cref="ACMShaders.DrawBeam"/>): 把旧紫尘瞄准线升级为 BeamGrad 直带。
        /// 致命射击线渐变到纯红 (§预警色彩语言: 红=致命); 域内穿刺线随签名进度变红。须在 PreDraw 活动批内调用。
        /// </summary>
        private void DrawV2Beams() {
            if (Main.dedServ)
                return;
            Player target = Main.player[NPC.target];

            // 狙击对决: 凝视锁定 60-tick 蓄力 — 渐亮 紫→红, 蓄满前可读
            if (Phase == BossPhase.Phase2_SniperDuel && SubState == 1f && target.active) {
                float charge = MathHelper.Clamp(gazeLockTimer / 60f, 0f, 1f);
                Color core = Color.Lerp(new Color(180, 100, 255), TelegraphColors.Lethal, charge);
                ACMShaders.DrawBeam(NPC.Center, target.Center, MathHelper.Lerp(2.5f, 7f, charge),
                    core, new Color(90, 60, 200), 0.4f + charge * 0.6f,
                    flowSpeed: 2.0f, coreSharp: 2.6f, coreGlow: charge);
            }

            // 凝视扫射: 沿旋转角的致命射线 (箭沿此方向连发)
            if (Phase == BossPhase.Phase3_GazeSweep && PhaseTimer > 1f) {
                Vector2 dir = new(MathF.Cos(gazeAngle), MathF.Sin(gazeAngle));
                ACMShaders.DrawBeam(NPC.Center, NPC.Center + dir * 1400f, 5f,
                    TelegraphColors.Lethal, new Color(120, 80, 220), 0.8f,
                    flowSpeed: 2.4f, coreSharp: 2.4f);
            }

            // 全视之域: 瞳孔中心穿刺预告线 (随签名进度变红)
            if (Phase == BossPhase.Phase3_AllSeeingDomain && domainPower > 0.2f && target.active) {
                float pulse = 0.6f + MathF.Sin(globalTime * 6f) * 0.2f;
                ACMShaders.DrawBeam(NPC.Center, target.Center, 4f + domainPower * 4f,
                    Color.Lerp(new Color(200, 120, 255), TelegraphColors.Lethal, domainPower),
                    new Color(80, 120, 255), domainPower * pulse,
                    flowSpeed: 1.8f, coreSharp: 2.6f);
            }

            // 光刃布雷: 预测路径引导束 (布雷期, 紫蓝主题=布置中, 非致命红)
            if (Phase == BossPhase.Phase2_WingbladeMines && target.active && AttackTimer < 80f) {
                Vector2 moveDir = target.velocity.SafeNormalize(Vector2.UnitX);
                ACMShaders.DrawBeam(target.Center - moveDir * 120f, target.Center + moveDir * 360f, 3f,
                    new Color(180, 90, 255), new Color(80, 120, 220), 0.5f, flowSpeed: 1.2f);
            }

            // 处决闪白: 狙击/全视命中瞬间沿凝视线的白闪
            if (gazeBeamFlash > 0.02f && target.active) {
                ACMShaders.DrawBeam(NPC.Center, target.Center, 9f * gazeBeamFlash,
                    Color.White, new Color(200, 180, 255), gazeBeamFlash,
                    flowSpeed: 3f, coreSharp: 3f, coreGlow: gazeBeamFlash);
            }

            // 离散径向泛光 (DrawRadialBloomAt 占全屏名额; 域内让位给 GenericWarp 折射, 故仅域外绘制)
            if (bloomBurst > 0.02f && domainPower < 0.15f)
                ACMShaders.DrawRadialBloomAt(NPC.Center, bloomBurstRadius, bloomBurst, bloomBurstColor,
                    rayCount: 12f, falloff: 2.6f);
        }

        /// <summary>
        /// 「全视之域」签名: 以独眼为中心的弱全屏折射 (GenericWarp · rift, "被注视"的物理化)。
        /// 喂 Main.screenTarget 的昂贵后处理, 受单一全屏名额约束: 仅 domainPower>0 时申请名额绘制, 平时早退。
        /// 其余氛围/虹环/泛光由 <see cref="ArgusScreenSystem"/> 与 <see cref="DrawV2Beams"/> 承担。
        /// </summary>
        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ || domainPower <= 0.05f)
                return;
            if (!ACMShaders.RequestFullscreenSlot())
                return;

            Effect fx = ACMShaders.GenericWarp;
            if (fx == null)
                return;

            float aspect = (float)Main.screenWidth / Main.screenHeight;
            Vector2 uv = (NPC.Center - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uv);
            fx.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(domainPower * 0.55f, 0f, 1f));
            fx.Parameters["uRadius"]?.SetValue(1.0f);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uWarpScale"]?.SetValue(1.1f);
            fx.Parameters["uChroma"]?.SetValue(0.35f);
            fx.Parameters["uRadialPull"]?.SetValue(0.18f);     // 轻微向心吸入 = 被瞳孔"吸住"
            fx.Parameters["uMode"]?.SetValue(3f);              // 3 = rift 主题
            fx.Parameters["uTint"]?.SetValue(new Vector4(new Color(120, 70, 220).ToVector3(), 0.4f));

            ACMShaders.ApplyScreenPostProcess(spriteBatch, fx);
        }

        #endregion
    }
}
