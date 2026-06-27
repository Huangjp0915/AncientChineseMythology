using AncientChineseMythology.Helpers;
using AncientChineseMythology.Items.Materials;
using AncientChineseMythology.NPCs.Boss.KyuubiKitsunes.Items;
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

namespace AncientChineseMythology.NPCs.Boss.KyuubiKitsunes
{
    /// <summary>
    /// 九尾妖狐 Boss (石巨人后强度) — V2 重做。
    /// 身份: "九条妖尾各自为戈 — 你要读的不是它本体, 而是九尾的合奏。"
    /// 一阶段: 固定可读三招轮替 (顺序刺 → 波浪横扫 → 狐火齐射, RNG 只决定领尾)。
    /// 二阶段: 九尾分三组(刺客/术士/鞭尾, 尾尖辉光色编码) + 固定连段 (冲刺/瞬移/狐影九重/狐火曼陀罗)。
    /// 招牌 set-piece: 狐火曼陀罗 (九尾尖钉成绕玩家旋转的九边形, 缺口每 2s 跳 90°)。
    /// 终结技: ≤25% 血一次性加速九方向同刺 (不再作为常态升级)。
    /// 全部占位弹已替换为自定义狐火弹 <see cref="KyuubiFoxFire"/>。
    /// </summary>
    [AutoloadBossHead]
    internal class KyuubiKitsune : ModNPC
    {
        [VaultLoaden("{@namespace}/")]
        public static Texture2D MissesBody;
        [VaultLoaden("{@namespace}/")]
        public static Texture2D MissesTop;

        #region 常量定义

        /// <summary>尾巴数量</summary>
        public const int TailCount = 9;

        /// <summary>二阶段血量百分比阈值</summary>
        public const float Phase2Threshold = 0.5f;

        /// <summary>终结技血量阈值 (≤25%)</summary>
        public const float FinisherThreshold = 0.25f;

        /// <summary>曼陀罗九边形半径 (世界像素)</summary>
        public const float MandalaRadiusValue = 320f;

        #endregion

        #region 阶段枚举

        public enum BossPhase
        {
            Intro,
            Phase1_Idle,        // 一阶段: 固定三招轮替 hub
            Phase1_Slam,        // 一阶段: 瞬移下砸
            Phase1_NineStab,    // 一阶段: 九方向远距离刺击
            PhaseTransition,    // 阶段转换演出 (分尾)
            Phase2_Chase,       // 二阶段: 追击 hub (固定连段)
            Phase2_Dash,        // 二阶段: 高速冲刺
            Phase2_Teleport,    // 二阶段: 瞬移攻击
            Phase2_Illusion,    // 二阶段: 狐影九重 (辨真伪)
            Phase2_Mandala,     // 二阶段: 狐火曼陀罗 set-piece
            Phase2_NineStab     // 终结技: 加速九方向同刺 (仅 ≤25% 一次)
        }

        private enum P1Move { Sequential, Wave, Barrage, Slam, NineStab }
        private enum P2Move { Dash, Teleport, Illusion, Mandala }

        /// <summary>一阶段固定招式序列 (RNG 只决定领尾, 不掷骰选招)。</summary>
        private static readonly P1Move[] P1Script = {
            P1Move.Sequential, P1Move.Wave, P1Move.Barrage,
            P1Move.Sequential, P1Move.Wave, P1Move.NineStab,
            P1Move.Barrage, P1Move.Slam
        };

        /// <summary>二阶段固定连段 (非掷骰)。</summary>
        private static readonly P2Move[] P2Script = {
            P2Move.Dash, P2Move.Teleport, P2Move.Illusion, P2Move.Mandala
        };

        /// <summary>尾巴角色色 (尾尖辉光色编码): 刺客暖橙 / 术士金 / 鞭尾紫。</summary>
        private static readonly Color[] RoleTints = {
            new(255, 140, 50),
            new(255, 215, 120),
            new(190, 120, 255)
        };

        #endregion

        #region 状态属性

        public BossPhase Phase {
            get => (BossPhase)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        public ref float PhaseTimer => ref NPC.ai[1];
        public ref float AttackTimer => ref NPC.ai[2];
        public ref float SubState => ref NPC.ai[3];

        /// <summary>九条尾巴</summary>
        public KyuubiTail[] Tails { get; private set; }

        /// <summary>是否处于二阶段</summary>
        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;

        // ===== 狐火曼陀罗 — 供边墙 KyuubiMandalaEdge 读取的权威状态 =====
        /// <summary>当前是否正进行曼陀罗 set-piece。</summary>
        public bool InMandala => Phase == BossPhase.Phase2_Mandala;
        /// <summary>九边形中心 (开场捕获玩家位置, 同步)。</summary>
        public Vector2 MandalaCenter => mandalaCenter;
        /// <summary>九边形半径。</summary>
        public float MandalaRadius => MandalaRadiusValue;
        /// <summary>整环旋转角。</summary>
        public float MandalaRotation => mandalaRotation;
        /// <summary>当前安全缺口边索引。</summary>
        public int MandalaGapIndex => mandalaGapIndex;
        /// <summary>是否进入致命伤害窗口 (预告结束后)。</summary>
        public bool MandalaDamaging => InMandala && (int)SubState == 2;
        /// <summary>边墙可见度 0~1。</summary>
        public float MandalaEdgeAlpha => mandalaEdgeAlpha;

        // 私有状态
        private float globalTime;
        private int seed;
        private Random random;
        private float introProgress;
        private bool didPhaseTransition;
        private Vector2 teleportTarget;
        private float dashDirection;
        private int illusionCount;
        private float[] illusionAlpha;
        private float[] illusionDissolve;
        private Vector2[] illusionPositions;

        // 尾巴攻击控制
        private int currentAttackingTail;
        private float lastTailAttackTime;
        private int leadTail;
        private P1Move p1CurrentMove;
        private int p1EmitCounter;

        // V2 序列 / 终结技
        private int p1ScriptIndex;
        private int p2ScriptIndex;
        private bool didFinisher;

        // 曼陀罗
        private Vector2 mandalaCenter;
        private float mandalaRotation;
        private int mandalaGapIndex;
        private float mandalaEdgeAlpha;
        private bool mandalaSpawnedEdges;

        // 瞬移下砸控制
        private Vector2 slamStartPos;
        private Vector2 slamTargetPos;
        private float slamProgress;
        private bool slamHasHit;

        // 二阶段冲刺控制
        private Vector2 dashVelocity;
        private int dashCount;
        private int maxDashCount;

        // 九方向远距离刺击控制
        private int nineStabRepeatCount;
        private int nineStabMaxRepeats;
        private float nineStabBaseAngle;

        // 演出: 狐火泛光脉冲
        private float bloomPower;
        private Vector2 bloomPos;
        private Color bloomColor = Color.Gold;

        #endregion

        #region ModNPC重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 120;
            NPC.height = 120;
            NPC.damage = 80;
            NPC.defense = 50;
            NPC.lifeMax = 75000; // 石巨人后强度
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.Roar;
            NPC.value = Item.buyPrice(0, 15, 0, 0);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 10f;
            NPC.aiStyle = -1;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.3f);
                NPC.damage = (int)(NPC.damage * 1.2f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.3f);
            }

            Music = MusicID.Boss4;
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.HealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ItemID.GoldCoin, 1, 15, 25));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<YaoQiFragment>(), 1, 12, 18));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<KyuubiBook>()));
        }

        public override void OnKill() {
            DownedBossSystem.downedKyuubi = true;
        }

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(10000);
            random = new Random(seed);

            InitializeTails();

            illusionAlpha = new float[4];
            illusionDissolve = new float[4];
            illusionPositions = new Vector2[4];

            Phase = BossPhase.Intro;
            PhaseTimer = 0;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.netUpdate = true;
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
            writer.Write((int)Phase);
            writer.Write(globalTime);
            writer.Write(didPhaseTransition);
            writer.Write(currentAttackingTail);
            writer.WriteVector2(teleportTarget);
            writer.Write(dashDirection);
            // V2
            writer.Write(p1ScriptIndex);
            writer.Write(p2ScriptIndex);
            writer.Write(didFinisher);
            writer.WriteVector2(mandalaCenter);
            writer.Write(mandalaRotation);
            writer.Write(mandalaGapIndex);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhaseTransition = reader.ReadBoolean();
            currentAttackingTail = reader.ReadInt32();
            teleportTarget = reader.ReadVector2();
            dashDirection = reader.ReadSingle();
            // V2
            p1ScriptIndex = reader.ReadInt32();
            p2ScriptIndex = reader.ReadInt32();
            didFinisher = reader.ReadBoolean();
            mandalaCenter = reader.ReadVector2();
            mandalaRotation = reader.ReadSingle();
            mandalaGapIndex = reader.ReadInt32();

            if (random == null)
                random = new Random(seed);
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.8f * balance * bossAdjustment);
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            return null;
        }

        #endregion

        #region AI主循环

        public override void AI() {
            random ??= new Random(seed);
            globalTime += 1f / 60f;

            if (Tails == null)
                InitializeTails();

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
                case BossPhase.Phase1_Idle: RunPhase1Idle(target); break;
                case BossPhase.Phase1_Slam: RunPhase1Slam(target); break;
                case BossPhase.Phase1_NineStab: RunPhase1NineStab(target); break;
                case BossPhase.PhaseTransition: RunPhaseTransition(target); break;
                case BossPhase.Phase2_Chase: RunPhase2Chase(target); break;
                case BossPhase.Phase2_Dash: RunPhase2Dash(target); break;
                case BossPhase.Phase2_Teleport: RunPhase2Teleport(target); break;
                case BossPhase.Phase2_Illusion: RunPhase2Illusion(target); break;
                case BossPhase.Phase2_Mandala: RunPhase2Mandala(target); break;
                case BossPhase.Phase2_NineStab: RunPhase2NineStab(target); break;
            }

            UpdateAllTails();

            // 演出脉冲衰减
            if (bloomPower > 0f)
                bloomPower = MathF.Max(0f, bloomPower - 0.04f);

            // 妖力发光: 二阶段转赤
            Vector3 light = IsPhase2 ? new Vector3(1f, 0.45f, 0.2f) : new Vector3(1f, 0.6f, 0.2f);
            Lighting.AddLight(NPC.Center, light * 0.8f);
        }

        private void InitializeTails() {
            Tails = new KyuubiTail[TailCount];
            for (int i = 0; i < TailCount; i++) {
                Tails[i] = new KyuubiTail(i);
                float angleRange = MathHelper.Pi;
                float startAngle = -MathHelper.Pi * 0.75f;
                float baseAngle = startAngle + angleRange * i / (TailCount - 1);
                Tails[i].Initialize(GetTailRootPosition(i), baseAngle);
            }
        }

        private Vector2 GetTailRootPosition(int tailIndex) {
            float angleRange = MathHelper.Pi;
            float startAngle = -MathHelper.Pi * 0.75f;
            float angle = startAngle + angleRange * tailIndex / (TailCount - 1);
            float radius = 35f;
            Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            return NPC.Center + offset;
        }

        private void UpdateAllTails() {
            for (int i = 0; i < TailCount; i++) {
                if (Tails[i] == null) continue;

                Vector2 rootPos = GetTailRootPosition(i);

                float angleRange = MathHelper.Pi;
                float startAngle = -MathHelper.Pi * 0.75f;
                float baseAngle = startAngle + angleRange * i / (TailCount - 1);

                if (NPC.velocity.LengthSquared() > 1f) {
                    float velocityAngle = NPC.velocity.ToRotation();
                    float oppositeAngle = velocityAngle + MathHelper.Pi;
                    float spreadOffset = (i - 4) / 4f * MathHelper.PiOver4;
                    baseAngle = MathHelper.Lerp(baseAngle, oppositeAngle + spreadOffset, 0.4f);
                }

                float swayOffset = MathF.Sin(globalTime * 2f + i * 0.7f) * 0.1f;
                baseAngle += swayOffset;

                Tails[i].Update(rootPos, baseAngle, NPC.velocity, globalTime);

                if (Tails[i].ShouldFireProjectile())
                    FireTailProjectile(i);
            }
        }

        private void CheckPhaseTransition() {
            if (!didPhaseTransition && IsPhase2 && Phase != BossPhase.PhaseTransition && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.PhaseTransition);
                didPhaseTransition = true;
            }
        }

        private void TransitionTo(BossPhase newPhase) {
            // 离开任何阶段前解除曼陀罗钉位, 避免尾巴卡死
            if (Tails != null) {
                for (int i = 0; i < TailCount; i++)
                    if (Tails[i] != null) Tails[i].Pinned = false;
            }

            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            lastTailAttackTime = -1000f; // 进入新状态立即可出招
            NPC.netUpdate = true;
        }

        private void TriggerBloom(Vector2 pos, float power, Color color) {
            bloomPos = pos;
            bloomPower = MathHelper.Clamp(power, 0f, 1f);
            bloomColor = color;
        }

        #endregion

        #region 入场 + 一阶段 (固定三招轮替)

        private void RunIntro(Player target) {
            introProgress = MathHelper.Clamp(PhaseTimer / 120f, 0f, 1f);

            Vector2 introOffset = new Vector2(0, -400) * (1f - ACMUtils.SineInOut(introProgress));
            Vector2 desiredPos = target.Center + new Vector2(0, -300) + introOffset;

            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, 0.05f);
            NPC.velocity *= 0.9f;

            if (Main.netMode != NetmodeID.Server && PhaseTimer % 3 == 0) {
                for (int i = 0; i < 5; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(100, 100) * (1f - introProgress);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                }
            }

            if (PhaseTimer == 80) {
                SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
                ACMScreenShakeSystem.Add(12f);
                TriggerBloom(NPC.Center, 0.8f, TelegraphColors.Gold);
            }

            if (PhaseTimer > 150)
                TransitionTo(BossPhase.Phase1_Idle);
        }

        private void RunPhase1Idle(Player target) {
            // 悬浮
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime * 1.4f) * 60f, -340 + MathF.Sin(globalTime * 2f) * 18f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.03f, 0.1f);

            switch ((int)SubState) {
                case 0: // 间歇 + 选招 (固定轮替, RNG 只决定领尾)
                    if (PhaseTimer >= 35) {
                        p1CurrentMove = P1Script[p1ScriptIndex % P1Script.Length];
                        p1ScriptIndex++;
                        leadTail = Main.rand.Next(TailCount);
                        PhaseTimer = 0;
                        p1EmitCounter = 0;
                        NPC.netUpdate = true;

                        if (p1CurrentMove == P1Move.Slam) { TransitionTo(BossPhase.Phase1_Slam); return; }
                        if (p1CurrentMove == P1Move.NineStab) { TransitionTo(BossPhase.Phase1_NineStab); return; }
                        SubState = 1;
                    }
                    break;

                case 1: // 逐尾施放 (顺序刺 / 波浪横扫 / 狐火齐射)
                    EmitP1Move(target);
                    break;
            }
        }

        private void EmitP1Move(Player target) {
            switch (p1CurrentMove) {
                case P1Move.Sequential:
                    if (PhaseTimer % 6 == 0 && p1EmitCounter < TailCount) {
                        int idx = (leadTail + p1EmitCounter) % TailCount;
                        Tails[idx].StartStabAttack(target.Center, 0.4f);
                        p1EmitCounter++;
                    }
                    if (p1EmitCounter >= TailCount && PhaseTimer > TailCount * 6 + 22) { SubState = 0; PhaseTimer = 0; }
                    break;

                case P1Move.Wave:
                    if (PhaseTimer % 4 == 0 && p1EmitCounter < TailCount) {
                        int idx = (leadTail + p1EmitCounter) % TailCount;
                        Tails[idx].StartSweepAttack(target.Center, MathHelper.PiOver2, 0.5f);
                        p1EmitCounter++;
                    }
                    if (p1EmitCounter >= TailCount && PhaseTimer > TailCount * 4 + 28) { SubState = 0; PhaseTimer = 0; }
                    break;

                case P1Move.Barrage:
                    if (PhaseTimer % 10 == 0 && p1EmitCounter < TailCount) {
                        int idx = (leadTail + p1EmitCounter) % TailCount;
                        Tails[idx].StartProjectileAttack(target.Center, 0.7f);
                        p1EmitCounter += 2;
                    }
                    if (p1EmitCounter >= TailCount && PhaseTimer > 80) { SubState = 0; PhaseTimer = 0; }
                    break;
            }
        }

        private void RunPhase1Slam(Player target) {
            switch ((int)SubState) {
                case 0:
                    slamTargetPos = target.Center + new Vector2(Main.rand.NextFloat(-100, 100), 0);
                    slamStartPos = NPC.Center;
                    SubState = 1;
                    PhaseTimer = 0;
                    for (int i = 0; i < TailCount; i++)
                        Tails[i].StartCoilAttack(0.8f);
                    break;

                case 1:
                    if (PhaseTimer < 30)
                        NPC.Opacity = 1f - PhaseTimer / 30f;
                    else if (PhaseTimer == 30) {
                        NPC.Center = slamTargetPos + new Vector2(0, -500);
                        SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                    }
                    else if (PhaseTimer < 60)
                        NPC.Opacity = (PhaseTimer - 30) / 30f;
                    else {
                        NPC.Opacity = 1f;
                        SubState = 2;
                        PhaseTimer = 0;
                        slamProgress = 0;
                        slamHasHit = false;
                        slamStartPos = NPC.Center;
                    }
                    break;

                case 2:
                    slamProgress = MathHelper.Clamp(PhaseTimer / 25f, 0f, 1f);
                    float easedProgress = ACMUtils.QuadIn(slamProgress);
                    NPC.Center = Vector2.Lerp(slamStartPos, slamTargetPos, easedProgress);

                    if (!slamHasHit && slamProgress > 0.3f) {
                        for (int i = 0; i < TailCount; i++) {
                            Vector2 tailTarget = slamTargetPos + new Vector2(-200 + i * 50, 50);
                            Tails[i].StartSlamAttack(tailTarget, 0.4f);
                        }
                        slamHasHit = true;
                    }

                    if (slamProgress >= 1f) {
                        SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        ACMScreenShakeSystem.Add(6f);
                        TriggerBloom(NPC.Center, 0.6f, TelegraphColors.Flame);
                        SpawnSlamShockwave();
                        SubState = 3;
                        PhaseTimer = 0;
                    }
                    break;

                case 3:
                    if (PhaseTimer > 60)
                        TransitionTo(BossPhase.Phase1_Idle);
                    else
                        NPC.velocity = new Vector2(0, -3f) * (1f - PhaseTimer / 60f);
                    break;
            }
        }

        #endregion

        #region 阶段转换 (分尾)

        private void RunPhaseTransition(Player target) {
            NPC.velocity *= 0.95f;

            if (PhaseTimer == 1)
                AssignTailRoles();

            if (PhaseTimer < 60) {
                for (int i = 0; i < TailCount; i++)
                    Tails[i].StartCoilAttack(1.5f);
            }

            if (Main.netMode != NetmodeID.Server && PhaseTimer % 2 == 0) {
                for (int i = 0; i < 10; i++) {
                    Vector2 dustVel = Main.rand.NextVector2CircularEdge(8, 8);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, dustVel.X, dustVel.Y, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (PhaseTimer == 90) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.3f }, NPC.Center);
                ACMScreenShakeSystem.Add(12f);
                TriggerBloom(NPC.Center, 1f, TelegraphColors.Flame); // 妖力解放 金→赤
            }

            if (PhaseTimer > 120)
                TransitionTo(BossPhase.Phase2_Chase);
        }

        /// <summary>二阶段分尾: 三组各三 — 0-2 刺客 / 3-5 术士 / 6-8 鞭尾, 尾尖辉光色编码。</summary>
        private void AssignTailRoles() {
            for (int i = 0; i < TailCount; i++) {
                int role = i / 3;
                Tails[i].Role = role;
                Tails[i].RoleTint = RoleTints[role];
            }
        }

        #endregion

        #region 二阶段 hub (固定连段)

        private void RunPhase2Chase(Player target) {
            Vector2 toTarget = target.Center - NPC.Center;
            Vector2 desired = toTarget.SafeNormalize(Vector2.Zero) * 8f;
            desired.X += MathF.Sin(globalTime * 3f) * 3f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.08f);

            // 分工尾巴群攻 (按角色)
            float cd = Main.expertMode ? 26f : 34f;
            if (AttackTimer >= lastTailAttackTime + cd) {
                ExecutePhase2TailAttack(target);
                lastTailAttackTime = AttackTimer;
            }

            if (PhaseTimer > 170) {
                // 终结技: ≤25% 一次性加速九刺 (保留, 非常态升级)
                if (!didFinisher && NPC.life <= NPC.lifeMax * FinisherThreshold) {
                    didFinisher = true;
                    TransitionTo(BossPhase.Phase2_NineStab);
                    return;
                }

                P2Move move = P2Script[p2ScriptIndex % P2Script.Length];
                p2ScriptIndex++;
                NPC.netUpdate = true;
                switch (move) {
                    case P2Move.Dash: TransitionTo(BossPhase.Phase2_Dash); break;
                    case P2Move.Teleport: TransitionTo(BossPhase.Phase2_Teleport); break;
                    case P2Move.Illusion: TransitionTo(BossPhase.Phase2_Illusion); break;
                    case P2Move.Mandala: TransitionTo(BossPhase.Phase2_Mandala); break;
                }
            }
        }

        /// <summary>二阶段角色分工同步出招: 刺客刺 / 术士狐火 / 鞭尾扫。</summary>
        private void ExecutePhase2TailAttack(Player target) {
            for (int i = 0; i < TailCount; i++) {
                if (Tails[i].IsAttacking) continue;
                switch (Tails[i].Role) {
                    case 0: // 刺客
                        Tails[i].StartStabAttack(target.Center + Main.rand.NextVector2Circular(40, 40), 0.3f);
                        break;
                    case 1: // 术士 (狐火)
                        Tails[i].StartProjectileAttack(target.Center, 0.6f);
                        break;
                    case 2: // 鞭尾
                        Tails[i].StartSweepAttack(target.Center, MathHelper.PiOver2 * 0.9f, 0.4f);
                        break;
                    default:
                        Tails[i].StartStabAttack(target.Center, 0.3f);
                        break;
                }
            }
        }

        private void RunPhase2Dash(Player target) {
            switch ((int)SubState) {
                case 0:
                    dashDirection = (target.Center - NPC.Center).ToRotation();
                    dashCount = 0;
                    maxDashCount = Main.expertMode ? 4 : 3;
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1:
                    NPC.velocity *= 0.9f;
                    if (PhaseTimer == 1) {
                        for (int i = 0; i < TailCount; i++)
                            Tails[i].StartCoilAttack(0.4f);
                    }
                    if (PhaseTimer > 30) {
                        dashDirection = (target.Center - NPC.Center).ToRotation();
                        dashVelocity = dashDirection.ToRotationVector2() * 25f;
                        SubState = 2;
                        PhaseTimer = 0;
                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f }, NPC.Center);
                    }
                    break;

                case 2:
                    NPC.velocity = dashVelocity;
                    for (int i = 0; i < TailCount; i++) {
                        if (!Tails[i].IsAttacking) {
                            Vector2 trailTarget = NPC.Center - dashVelocity.SafeNormalize(Vector2.Zero) * 300f;
                            Tails[i].TargetPosition = trailTarget + Main.rand.NextVector2Circular(50, 50);
                        }
                    }
                    if (PhaseTimer > 35) {
                        dashCount++;
                        if (dashCount >= maxDashCount)
                            TransitionTo(BossPhase.Phase2_Chase);
                        else { SubState = 1; PhaseTimer = 0; }
                    }
                    break;
            }
        }

        private void RunPhase2Teleport(Player target) {
            switch ((int)SubState) {
                case 0:
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float distance = Main.rand.NextFloat(200, 400);
                    teleportTarget = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1:
                    NPC.Opacity = 1f - PhaseTimer / 20f;
                    NPC.velocity *= 0.9f;
                    if (PhaseTimer >= 20) {
                        NPC.Center = teleportTarget;
                        SubState = 2;
                        PhaseTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                    }
                    break;

                case 2:
                    NPC.Opacity = PhaseTimer / 20f;
                    if (PhaseTimer >= 20) {
                        NPC.Opacity = 1f;
                        SubState = 3;
                        PhaseTimer = 0;
                        for (int i = 0; i < TailCount; i++)
                            Tails[i].StartStabAttack(target.Center, 0.3f);
                    }
                    break;

                case 3:
                    if (PhaseTimer > 40)
                        TransitionTo(BossPhase.Phase2_Chase);
                    break;
            }
        }

        #endregion

        #region 二阶段: 狐影九重 (辨真伪)

        private void RunPhase2Illusion(Player target) {
            switch ((int)SubState) {
                case 0: // 创建幻影 (本体=真身, 幻影=诱饵)
                    illusionCount = Main.expertMode ? 4 : 3;
                    for (int i = 0; i < illusionCount; i++) {
                        float angle = MathHelper.TwoPi * i / illusionCount;
                        illusionPositions[i] = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 300f;
                        illusionAlpha[i] = 0f;
                        illusionDissolve[i] = 0f;
                    }
                    // 真身尾尖提亮 — 让"哪个九尾是真"一眼可读
                    for (int i = 0; i < TailCount; i++)
                        Tails[i].TipGlowBoost = 0.6f;
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: // 幻影淡入, 本体游走 (诱饵被攻击则溶解 = 辨真伪交互)
                    for (int i = 0; i < illusionCount; i++)
                        illusionAlpha[i] = MathHelper.Clamp(PhaseTimer / 30f, 0f, 0.6f);

                    int realPosition = (int)(PhaseTimer / 30f) % illusionCount;
                    NPC.Center = Vector2.Lerp(NPC.Center, illusionPositions[realPosition], 0.1f);

                    UpdateIllusionDissolveOnHit();

                    if (PhaseTimer > 60) { SubState = 2; PhaseTimer = 0; }
                    break;

                case 2: // 真身九方向同刺 (仅真身造成伤害)
                    UpdateIllusionDissolveOnHit();

                    if (PhaseTimer == 1) {
                        for (int i = 0; i < TailCount; i++)
                            Tails[i].StartStabAttack(target.Center, 0.3f);

                        // 真身狐火九方向同刺 (server 权威)
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            float baseAngle = (target.Center - NPC.Center).ToRotation();
                            for (int i = 0; i < TailCount; i++) {
                                float a = baseAngle + MathHelper.TwoPi * i / TailCount;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                                    a.ToRotationVector2() * 4f, ModContent.ProjectileType<KyuubiFoxFire>(),
                                    Math.Max(1, NPC.damage / 3), 2f, Main.myPlayer, 0f, 0f);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, NPC.Center);
                        ACMScreenShakeSystem.Add(7f);
                        TriggerBloom(NPC.Center, 0.7f, TelegraphColors.Gold);
                    }

                    if (PhaseTimer > 40) { SubState = 3; PhaseTimer = 0; }
                    break;

                case 3: // 幻影溶解淡出
                    for (int i = 0; i < illusionCount; i++) {
                        illusionAlpha[i] = MathHelper.Clamp(0.6f - PhaseTimer / 30f, 0f, 0.6f);
                        illusionDissolve[i] = MathHelper.Clamp(illusionDissolve[i] + 0.04f, 0f, 1f);
                    }
                    if (PhaseTimer > 30) {
                        for (int i = 0; i < TailCount; i++)
                            Tails[i].TipGlowBoost = 0f;
                        TransitionTo(BossPhase.Phase2_Chase);
                    }
                    break;
            }
        }

        /// <summary>诱饵幻影被玩家弹幕"命中"(就近)即开始溶解 — 主动辨真伪。</summary>
        private void UpdateIllusionDissolveOnHit() {
            for (int i = 0; i < illusionCount; i++) {
                if (illusionDissolve[i] >= 1f)
                    continue;
                bool hit = false;
                for (int p = 0; p < Main.maxProjectiles; p++) {
                    Projectile proj = Main.projectile[p];
                    if (!proj.active || !proj.friendly || proj.damage <= 0)
                        continue;
                    if (Vector2.DistanceSquared(proj.Center, illusionPositions[i]) < 70f * 70f) {
                        hit = true;
                        break;
                    }
                }
                if (hit)
                    illusionDissolve[i] = MathHelper.Clamp(illusionDissolve[i] + 0.08f, 0f, 1f);
            }
        }

        #endregion

        #region 二阶段: 狐火曼陀罗 set-piece

        private void RunPhase2Mandala(Player target) {
            switch ((int)SubState) {
                case 0: // 布置: 捕获中心, 生成九边墙, 钉位尾巴
                    mandalaCenter = target.Center;
                    mandalaRotation = Main.rand.NextFloat(MathHelper.TwoPi);
                    mandalaGapIndex = Main.rand.Next(9);
                    mandalaEdgeAlpha = 0f;

                    if (Main.netMode != NetmodeID.MultiplayerClient && !mandalaSpawnedEdges) {
                        for (int i = 0; i < 9; i++) {
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), mandalaCenter, Vector2.Zero,
                                ModContent.ProjectileType<KyuubiMandalaEdge>(), Math.Max(1, NPC.damage / 2), 3f,
                                Main.myPlayer, NPC.whoAmI, i);
                        }
                        mandalaSpawnedEdges = true;
                    }

                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.2f }, NPC.Center);
                    SubState = 1;
                    PhaseTimer = 0;
                    NPC.netUpdate = true;
                    break;

                case 1: // 预告: 红线渐显, 尾巴钉成九边形
                    NPC.Center = Vector2.Lerp(NPC.Center, mandalaCenter + new Vector2(0, -360), 0.05f);
                    NPC.velocity *= 0.9f;
                    mandalaEdgeAlpha = MathHelper.Clamp(PhaseTimer / 45f, 0f, 1f);
                    PinTailsToMandala();

                    if (PhaseTimer >= 60) {
                        SubState = 2;
                        PhaseTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                        ACMScreenShakeSystem.Add(8f);
                        TriggerBloom(mandalaCenter, 0.8f, TelegraphColors.Flame);
                    }
                    break;

                case 2: // 致命窗口: 旋转 + 缺口每 2s 跳 ~90°, 约 12s
                    NPC.Center = Vector2.Lerp(NPC.Center, mandalaCenter + new Vector2(0, -360), 0.05f);
                    NPC.velocity *= 0.92f;
                    mandalaEdgeAlpha = 1f;
                    mandalaRotation += 0.006f;
                    PinTailsToMandala();

                    if (PhaseTimer > 0 && PhaseTimer % 120 == 0) {
                        mandalaGapIndex = (mandalaGapIndex + 2) % 9; // ~80° 跳, 近似 90°
                        SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                        NPC.netUpdate = true;
                    }

                    if (PhaseTimer >= 720) { SubState = 3; PhaseTimer = 0; }
                    break;

                case 3: // 收束: 边墙溶解淡出
                    mandalaEdgeAlpha = MathHelper.Clamp(1f - PhaseTimer / 40f, 0f, 1f);
                    if (PhaseTimer >= 45) {
                        mandalaSpawnedEdges = false;
                        TransitionTo(BossPhase.Phase2_Chase);
                    }
                    break;
            }
        }

        private void PinTailsToMandala() {
            for (int i = 0; i < TailCount; i++) {
                // 指向边墙中点, 使九尾尖恰好"钉住"九边形
                float a = mandalaRotation + MathHelper.TwoPi * (i + 0.5f) / 9f;
                Tails[i].Pinned = true;
                Tails[i].PinnedTarget = mandalaCenter + a.ToRotationVector2() * MandalaRadiusValue;
            }
        }

        #endregion

        #region 九方向远距离刺击 (一阶段常态 / 二阶段终结技)

        private void RunPhase1NineStab(Player target) {
            switch ((int)SubState) {
                case 0:
                    nineStabRepeatCount = 0;
                    nineStabMaxRepeats = Main.expertMode ? 4 : 3;
                    nineStabBaseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    SubState = 1;
                    PhaseTimer = 0;
                    NPC.velocity *= 0.5f;
                    break;

                case 1:
                    NPC.velocity *= 0.95f;
                    Vector2 hoverPos = target.Center + new Vector2(0, -300);
                    NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.02f);

                    if (PhaseTimer == 1) {
                        for (int i = 0; i < TailCount; i++) {
                            float angle = nineStabBaseAngle + MathHelper.TwoPi * i / TailCount;
                            Tails[i].StartLongRangeStabAttack(angle.ToRotationVector2(), 0.8f, 0.12f, 0.5f);
                        }
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f }, NPC.Center);
                    }

                    if (PhaseTimer > 48) { SubState = 2; PhaseTimer = 0; }
                    break;

                case 2:
                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
                        ACMScreenShakeSystem.Add(8f);
                        TriggerBloom(NPC.Center, 0.7f, TelegraphColors.Gold);
                    }
                    if (PhaseTimer > 8) { SubState = 3; PhaseTimer = 0; }
                    break;

                case 3:
                    if (PhaseTimer > 30) {
                        nineStabRepeatCount++;
                        if (nineStabRepeatCount >= nineStabMaxRepeats)
                            TransitionTo(BossPhase.Phase1_Idle);
                        else {
                            nineStabBaseAngle += MathHelper.ToRadians(20f);
                            SubState = 1;
                            PhaseTimer = 0;
                        }
                    }
                    break;
            }
        }

        /// <summary>终结技: 加速九方向同刺 (仅 ≤25% 触发一次), 末轮回到追击。</summary>
        private void RunPhase2NineStab(Player target) {
            switch ((int)SubState) {
                case 0:
                    nineStabRepeatCount = 0;
                    nineStabMaxRepeats = Main.expertMode ? 6 : 5;
                    nineStabBaseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    SubState = 1;
                    PhaseTimer = 0;
                    NPC.velocity *= 0.3f;
                    break;

                case 1:
                    NPC.velocity *= 0.92f;
                    Vector2 hoverPos = target.Center + new Vector2(0, -250);
                    NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.03f);

                    if (PhaseTimer == 1) {
                        for (int i = 0; i < TailCount; i++) {
                            float angle = nineStabBaseAngle + MathHelper.TwoPi * i / TailCount;
                            Tails[i].StartLongRangeStabAttack(angle.ToRotationVector2(), 0.5f, 0.1f, 0.35f);
                        }
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.7f }, NPC.Center);
                    }

                    if (PhaseTimer > 30) { SubState = 2; PhaseTimer = 0; }
                    break;

                case 2:
                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0f, Volume = 1.3f }, NPC.Center);
                        ACMScreenShakeSystem.Add(10f);
                        TriggerBloom(NPC.Center, 0.9f, TelegraphColors.Flame);

                        // 终结技狐火九方向 (server 权威)
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < TailCount; i++) {
                                float angle = nineStabBaseAngle + MathHelper.TwoPi * i / TailCount;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center,
                                    angle.ToRotationVector2() * 4f, ModContent.ProjectileType<KyuubiFoxFire>(),
                                    Math.Max(1, NPC.damage / 3), 2f, Main.myPlayer, 0f, 0f);
                            }
                        }
                    }
                    if (PhaseTimer > 6) { SubState = 3; PhaseTimer = 0; }
                    break;

                case 3:
                    if (PhaseTimer > 21) {
                        nineStabRepeatCount++;
                        if (nineStabRepeatCount >= nineStabMaxRepeats) {
                            TransitionTo(BossPhase.Phase2_Chase);
                        }
                        else {
                            nineStabBaseAngle += MathHelper.ToRadians(15f);
                            SubState = 1;
                            PhaseTimer = 0;
                        }
                    }
                    break;
            }
        }

        #endregion

        #region 狐火弹 (替换原版占位)

        private void FireTailProjectile(int tailIndex) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            KyuubiTail tail = Tails[tailIndex];
            Vector2 tipPos = tail.GetTipPosition();
            Vector2 direction = tail.GetTipDirection();
            int damage = Math.Max(1, NPC.damage / 2);

            // 狐火弹: 慢起 → 追踪 (homing=1)
            Projectile.NewProjectile(NPC.GetSource_FromAI(), tipPos, direction * 4f,
                ModContent.ProjectileType<KyuubiFoxFire>(), damage, 2f, Main.myPlayer, 0f, 1f);

            SoundEngine.PlaySound(SoundID.Item20, tipPos);
        }

        private void SpawnSlamShockwave() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int projectileCount = 12;
            int damage = Math.Max(1, NPC.damage / 2);
            for (int i = 0; i < projectileCount; i++) {
                float angle = MathHelper.TwoPi * i / projectileCount;
                Vector2 velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 5f;
                // 直线狐火 (homing=0): 环形冲击波
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, velocity,
                    ModContent.ProjectileType<KyuubiFoxFire>(), damage, 2f, Main.myPlayer, 0f, 0f);
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 曼陀罗地纹 (ArenaRunic 法阵, 画在最底)
            DrawMandalaDecal(spriteBatch);

            // 狐火泛光脉冲 (DrawRadialBloomAt, 走全屏名额)
            if (bloomPower > 0.02f)
                ACMShaders.DrawRadialBloomAt(bloomPos, 0.16f, bloomPower * 0.8f, bloomColor, 12f, 2.4f);

            DrawTrail(spriteBatch, screenPos);
            DrawIllusions(spriteBatch, screenPos, drawColor);
            DrawTails(spriteBatch, screenPos, drawColor);
            DrawMainBody(spriteBatch, screenPos, drawColor);

            return false;
        }

        private void DrawMandalaDecal(SpriteBatch spriteBatch) {
            if (Main.dedServ || !InMandala || mandalaEdgeAlpha <= 0.02f)
                return;
            Effect fx = ACMShaders.ArenaRunic;
            if (fx == null)
                return;

            ACMShaders.WorldDecalParams(mandalaCenter, MandalaRadiusValue, out Vector2 uvCenter, out float radiusFrac, out float aspect);
            fx.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
            fx.Parameters["uCenter"]?.SetValue(uvCenter);
            fx.Parameters["uRadius"]?.SetValue(radiusFrac);
            fx.Parameters["uAspect"]?.SetValue(aspect);
            fx.Parameters["uIntensity"]?.SetValue(mandalaEdgeAlpha * 0.7f);
            fx.Parameters["uShape"]?.SetValue(0f);
            fx.Parameters["uMode"]?.SetValue(0f);
            fx.Parameters["uColorPrimary"]?.SetValue(new Color(255, 180, 80).ToVector4());
            fx.Parameters["uColorSecondary"]?.SetValue(new Color(190, 70, 30).ToVector4());

            ACMShaders.DrawScreenSpaceDecal(spriteBatch, fx, BlendState.Additive);
        }

        private void DrawTrail(SpriteBatch spriteBatch, Vector2 screenPos) {
            Texture2D texture = TextureAssets.Npc[Type].Value;

            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - (float)i / NPC.oldPos.Length;
                Color trailColor = (IsPhase2 ? Color.OrangeRed : Color.Gold) * progress * 0.3f * NPC.Opacity;
                trailColor.A = 0;
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float scale = NPC.scale * progress * 0.9f;

                spriteBatch.Draw(texture, drawPos, null, trailColor, NPC.rotation,
                    texture.Size() / 2f, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawIllusions(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Phase != BossPhase.Phase2_Illusion)
                return;

            Texture2D texture = TextureAssets.Npc[Type].Value;

            for (int i = 0; i < illusionCount; i++) {
                if (illusionAlpha[i] <= 0)
                    continue;

                // 诱饵青色半透 — 与真身(实色+九尾+亮尖)区分
                Color illusionColor = drawColor * illusionAlpha[i];
                illusionColor = Color.Lerp(illusionColor, Color.Cyan, 0.4f);

                if (illusionDissolve[i] > 0.01f) {
                    // 被攻击 → DissolveBurn 溶解消散
                    WeaponVFX.ApplyDissolveBurn(texture, illusionPositions[i], null, illusionColor,
                        NPC.rotation, texture.Size() / 2f, NPC.scale,
                        threshold: illusionDissolve[i], intensity: illusionAlpha[i] / 0.6f,
                        edgeColor: new Color(120, 200, 255, 200), edgeWidth: 0.1f, noiseScale: 2.5f);
                }
                else {
                    spriteBatch.Draw(texture, illusionPositions[i] - screenPos, null, illusionColor,
                        NPC.rotation, texture.Size() / 2f, NPC.scale, SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawTails(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Tails == null)
                return;

            for (int i = 0; i < TailCount; i++)
                Tails[i]?.DrawTelegraph(spriteBatch, screenPos);

            for (int i = 0; i < TailCount; i++)
                Tails[i]?.Draw(spriteBatch, screenPos, drawColor);
        }

        private void DrawMainBody(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;

            // 妖力发光: 二阶段转赤
            Color glowColor = (IsPhase2 ? Color.OrangeRed : Color.Gold) * 0.3f * NPC.Opacity;
            glowColor.A = 0;

            for (int i = 0; i < 3; i++) {
                Vector2 offset = Main.rand.NextVector2CircularEdge(3, 3);
                spriteBatch.Draw(texture, drawPos + offset, null, glowColor, NPC.rotation,
                    texture.Size() / 2f, NPC.scale * 1.05f, SpriteEffects.None, 0f);
            }

            Color bodyColor = drawColor * NPC.Opacity;
            spriteBatch.Draw(texture, drawPos, null, bodyColor, NPC.rotation,
                texture.Size() / 2f, NPC.scale, SpriteEffects.None, 0f);
        }

        #endregion
    }
}
