using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AncientChineseMythology.NPCs.Boss.KyuubiKitsunes
{
    /// <summary>
    /// 九尾狐Boss - 石巨人后强度
    /// 一阶段：本体较少移动，通过九条尾巴刺击和发射射弹攻击玩家
    /// 二阶段：本体开始移动，冲刺、瞬移、产生幻影，尾巴配合更激进的攻击
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

        #endregion

        #region 阶段枚举

        public enum BossPhase
        {
            Intro,              // 出场演出
            Phase1_Idle,        // 一阶段：相对静止，尾巴攻击
            Phase1_Slam,        // 一阶段：瞬移下砸
            Phase1_NineStab,    // 一阶段：九方向远距离刺击
            PhaseTransition,    // 阶段转换演出
            Phase2_Chase,       // 二阶段：追击移动
            Phase2_Dash,        // 二阶段：高速冲刺
            Phase2_Teleport,    // 二阶段：瞬移攻击
            Phase2_Illusion,    // 二阶段：幻影分身
            Phase2_NineStab     // 二阶段：加强版九方向刺击
        }

        public enum TailAttackPattern
        {
            Sequential,        // 顺序刺击
            Simultaneous,      // 同时刺击
            Spiral,            // 螺旋刺击
            Wave,              // 波浪刺击
            ProjectileBarrage, // 射弹齐射
            RandomStab,        // 随机刺击
            NineDirectionStab  // 九方向远距离刺击
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

        /// <summary>九条尾巴</summary>
        public KyuubiTail[] Tails { get; private set; }

        /// <summary>是否处于二阶段</summary>
        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;

        /// <summary>当前攻击模式</summary>
        public TailAttackPattern CurrentTailPattern { get; private set; }

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
        private Vector2[] illusionPositions;

        // 尾巴攻击控制
        private int currentAttackingTail;
        private float tailAttackInterval;
        private float lastTailAttackTime;

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
        private int nineStabRepeatCount;      // 当前重复次数
        private int nineStabMaxRepeats;       // 最大重复次数
        private float nineStabBaseAngle;      // 基准角度偏移
        private float nineStabPhaseTimer;     // 阶段计时器
        private int nineStabPhase;            // 0=预判, 1=刺出, 2=回收

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

            // 调整难度
            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.3f);
                NPC.damage = (int)(NPC.damage * 1.2f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.3f);
            }

            Music = MusicID.Boss4; // 可以替换为自定义音乐
        }

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(10000);
            random = new Random(seed);

            // 初始化九条尾巴
            Tails = new KyuubiTail[TailCount];
            for (int i = 0; i < TailCount; i++) {
                Tails[i] = new KyuubiTail(i);
                // 计算每条尾巴的基准角度（均匀分布在背后180度半圆）
                // 从左上方（-135度）到右上方（-45度），中间是正上方（-90度）
                // 使用均匀分布：从-135度到-45度，共180度范围
                float angleRange = MathHelper.Pi; // 180度
                float startAngle = -MathHelper.Pi * 0.75f; // -135度 (左上)
                float baseAngle = startAngle + angleRange * i / (TailCount - 1);
                Tails[i].Initialize(GetTailRootPosition(i), baseAngle);
            }

            // 初始化幻影数组
            illusionAlpha = new float[4];
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
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhaseTransition = reader.ReadBoolean();
            currentAttackingTail = reader.ReadInt32();
            teleportTarget = reader.ReadVector2();
            dashDirection = reader.ReadSingle();

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

            // 初始化尾巴（如果需要）
            if (Tails == null) {
                InitializeTails();
            }

            // 检测目标
            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    // 没有有效目标，逃离
                    NPC.velocity.Y -= 0.5f;
                    NPC.EncourageDespawn(30);
                    return;
                }
            }

            // 检查阶段转换
            CheckPhaseTransition();

            PhaseTimer++;
            AttackTimer++;

            // 根据当前阶段执行AI
            switch (Phase) {
                case BossPhase.Intro:
                    RunIntro(target);
                    break;
                case BossPhase.Phase1_Idle:
                    RunPhase1Idle(target);
                    break;
                case BossPhase.Phase1_Slam:
                    RunPhase1Slam(target);
                    break;
                case BossPhase.Phase1_NineStab:
                    RunPhase1NineStab(target);
                    break;
                case BossPhase.PhaseTransition:
                    RunPhaseTransition(target);
                    break;
                case BossPhase.Phase2_Chase:
                    RunPhase2Chase(target);
                    break;
                case BossPhase.Phase2_Dash:
                    RunPhase2Dash(target);
                    break;
                case BossPhase.Phase2_Teleport:
                    RunPhase2Teleport(target);
                    break;
                case BossPhase.Phase2_Illusion:
                    RunPhase2Illusion(target);
                    break;
                case BossPhase.Phase2_NineStab:
                    RunPhase2NineStab(target);
                    break;
            }

            // 更新所有尾巴
            UpdateAllTails();

            // 发光
            Lighting.AddLight(NPC.Center, new Vector3(1f, 0.6f, 0.2f) * 0.8f);
        }

        private void InitializeTails() {
            Tails = new KyuubiTail[TailCount];
            for (int i = 0; i < TailCount; i++) {
                Tails[i] = new KyuubiTail(i);
                // 均匀分布在背后180度半圆
                float angleRange = MathHelper.Pi;
                float startAngle = -MathHelper.Pi * 0.75f;
                float baseAngle = startAngle + angleRange * i / (TailCount - 1);
                Tails[i].Initialize(GetTailRootPosition(i), baseAngle);
            }
        }

        private Vector2 GetTailRootPosition(int tailIndex) {
            // 尾巴根部位置：均匀分布在本体背后的半圆弧上
            // 从左上方到右上方，180度范围
            float angleRange = MathHelper.Pi;
            float startAngle = -MathHelper.Pi * 0.75f; // -135度
            float angle = startAngle + angleRange * tailIndex / (TailCount - 1);

            // 根部在本体中心向外偏移
            float radius = 35f;
            Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            return NPC.Center + offset;
        }

        private void UpdateAllTails() {
            for (int i = 0; i < TailCount; i++) {
                if (Tails[i] == null) continue;

                Vector2 rootPos = GetTailRootPosition(i);

                // 计算基准角度（均匀分布在180度半圆）
                float angleRange = MathHelper.Pi;
                float startAngle = -MathHelper.Pi * 0.75f;
                float baseAngle = startAngle + angleRange * i / (TailCount - 1);

                // 根据本体速度动态调整尾巴方向
                if (NPC.velocity.LengthSquared() > 1f) {
                    // 运动时尾巴向后拖曳
                    float velocityAngle = NPC.velocity.ToRotation();
                    float oppositeAngle = velocityAngle + MathHelper.Pi;

                    // 尾巴向运动反方向偏移，但保持扇形分布
                    float spreadOffset = (i - 4) / 4f * MathHelper.PiOver4; // 中间尾巴在中心，两侧展开
                    baseAngle = MathHelper.Lerp(baseAngle, oppositeAngle + spreadOffset, 0.4f);
                }

                // 添加微小的个体差异摆动
                float swayOffset = MathF.Sin(globalTime * 2f + i * 0.7f) * 0.1f;
                baseAngle += swayOffset;

                Tails[i].Update(rootPos, baseAngle, NPC.velocity, globalTime);

                // 检查射弹发射
                if (Tails[i].ShouldFireProjectile()) {
                    FireTailProjectile(i);
                }
            }
        }

        private void CheckPhaseTransition() {
            if (!didPhaseTransition && IsPhase2 && Phase != BossPhase.PhaseTransition && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.PhaseTransition);
                didPhaseTransition = true;
            }
        }

        private void TransitionTo(BossPhase newPhase) {
            Phase = newPhase;
            PhaseTimer = 0;
            AttackTimer = 0;
            SubState = 0;
            NPC.netUpdate = true;
        }

        #endregion

        #region 一阶段AI

        private void RunIntro(Player target) {
            introProgress = MathHelper.Clamp(PhaseTimer / 120f, 0f, 1f);

            // 出场动画：从远处飘来
            Vector2 introOffset = new Vector2(0, -400) * (1f - ACMUtils.SineInOut(introProgress));
            Vector2 desiredPos = target.Center + new Vector2(0, -300) + introOffset;

            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, 0.05f);
            NPC.velocity *= 0.9f;

            // 出场粒子效果
            if (Main.netMode != NetmodeID.Server && PhaseTimer % 3 == 0) {
                for (int i = 0; i < 5; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(100, 100) * (1f - introProgress);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 3f;
                }
            }

            // 播放咆哮
            if (PhaseTimer == 80) {
                SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 40);
            }

            if (PhaseTimer > 150) {
                TransitionTo(BossPhase.Phase1_Idle);
            }
        }

        private void RunPhase1Idle(Player target) {
            // 缓慢悬浮，保持在玩家上方一定距离
            Vector2 hoverPos = target.Center + new Vector2(0, -350);
            Vector2 toHover = hoverPos - NPC.Center;

            // 添加轻微的悬浮晃动
            hoverPos.X += MathF.Sin(globalTime * 1.5f) * 50f;
            hoverPos.Y += MathF.Sin(globalTime * 2f) * 20f;

            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.02f, 0.1f);

            // 尾巴攻击控制
            float attackCooldown = Main.expertMode ? 40f : 50f;

            if (AttackTimer >= lastTailAttackTime + attackCooldown) {
                ExecuteTailAttack(target);
                lastTailAttackTime = AttackTimer;
            }

            // 定期切换攻击模式
            if (PhaseTimer % 300 == 0) {
                CurrentTailPattern = (TailAttackPattern)Main.rand.Next(0, 6);
            }

            // 一定概率进入瞬移下砸或九方向刺击
            if (PhaseTimer > 400) {
                if (Main.rand.NextBool(150)) {
                    TransitionTo(BossPhase.Phase1_NineStab);
                }
                else if (Main.rand.NextBool(200)) {
                    TransitionTo(BossPhase.Phase1_Slam);
                }
            }
        }

        private void ExecuteTailAttack(Player target) {
            switch (CurrentTailPattern) {
                case TailAttackPattern.Sequential:
                    // 顺序刺击：每次一条尾巴
                    if (currentAttackingTail < TailCount) {
                        Tails[currentAttackingTail].StartStabAttack(target.Center, 0.35f);
                        currentAttackingTail = (currentAttackingTail + 1) % TailCount;
                    }
                    break;

                case TailAttackPattern.Simultaneous:
                    // 同时刺击：所有尾巴同时攻击
                    for (int i = 0; i < TailCount; i++) {
                        Vector2 spreadTarget = target.Center + Main.rand.NextVector2Circular(100, 100);
                        Tails[i].StartStabAttack(spreadTarget, 0.4f);
                    }
                    break;

                case TailAttackPattern.Spiral:
                    // 螺旋刺击：间隔触发
                    for (int i = 0; i < TailCount; i++) {
                        if ((int)(PhaseTimer / 10) % TailCount == i) {
                            Tails[i].StartStabAttack(target.Center, 0.3f);
                        }
                    }
                    break;

                case TailAttackPattern.Wave:
                    // 波浪刺击：横扫
                    for (int i = 0; i < TailCount; i++) {
                        float delay = i * 0.05f;
                        if (!Tails[i].IsAttacking) {
                            Tails[i].StartSweepAttack(target.Center, MathHelper.PiOver4, 0.5f);
                        }
                    }
                    break;

                case TailAttackPattern.ProjectileBarrage:
                    // 射弹齐射
                    for (int i = 0; i < TailCount; i += 2) {
                        if (!Tails[i].IsAttacking) {
                            Tails[i].StartProjectileAttack(target.Center, 0.7f);
                        }
                    }
                    break;

                case TailAttackPattern.RandomStab:
                    // 随机刺击
                    int randomTail = Main.rand.Next(TailCount);
                    if (!Tails[randomTail].IsAttacking) {
                        int attackType = Main.rand.Next(3);
                        switch (attackType) {
                            case 0:
                                Tails[randomTail].StartStabAttack(target.Center, 0.35f);
                                break;
                            case 1:
                                Tails[randomTail].StartWhipAttack(target.Center, 0.45f);
                                break;
                            case 2:
                                Tails[randomTail].StartSlamAttack(target.Center, 0.6f);
                                break;
                        }
                    }
                    break;
            }
        }

        private void RunPhase1Slam(Player target) {
            switch ((int)SubState) {
                case 0: // 准备：悬浮到玩家上方高处
                    slamTargetPos = target.Center + new Vector2(Main.rand.NextFloat(-100, 100), 0);
                    slamStartPos = NPC.Center;
                    SubState = 1;
                    PhaseTimer = 0;

                    // 所有尾巴蓄力
                    for (int i = 0; i < TailCount; i++) {
                        Tails[i].StartCoilAttack(0.8f);
                    }
                    break;

                case 1: // 瞬移到玩家上方
                    if (PhaseTimer < 30) {
                        // 淡出
                        NPC.Opacity = 1f - PhaseTimer / 30f;
                    }
                    else if (PhaseTimer == 30) {
                        // 瞬移
                        NPC.Center = slamTargetPos + new Vector2(0, -500);
                        SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                    }
                    else if (PhaseTimer < 60) {
                        // 淡入
                        NPC.Opacity = (PhaseTimer - 30) / 30f;
                    }
                    else {
                        NPC.Opacity = 1f;
                        SubState = 2;
                        PhaseTimer = 0;
                        slamProgress = 0;
                        slamHasHit = false;
                        slamStartPos = NPC.Center;
                    }
                    break;

                case 2: // 下砸
                    slamProgress = MathHelper.Clamp(PhaseTimer / 25f, 0f, 1f);
                    float easedProgress = ACMUtils.QuadIn(slamProgress);

                    Vector2 slamPos = Vector2.Lerp(slamStartPos, slamTargetPos, easedProgress);
                    NPC.Center = slamPos;

                    // 所有尾巴下砸
                    if (!slamHasHit && slamProgress > 0.3f) {
                        for (int i = 0; i < TailCount; i++) {
                            Vector2 tailTarget = slamTargetPos + new Vector2(-200 + i * 50, 50);
                            Tails[i].StartSlamAttack(tailTarget, 0.4f);
                        }
                        slamHasHit = true;
                    }

                    if (slamProgress >= 1f) {
                        // 砸地效果
                        SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(15, 30);

                        // 产生冲击波弹幕（简化实现）
                        SpawnSlamShockwave();

                        SubState = 3;
                        PhaseTimer = 0;
                    }
                    break;

                case 3: // 回收
                    if (PhaseTimer > 60) {
                        TransitionTo(BossPhase.Phase1_Idle);
                    }
                    else {
                        // 缓慢上升
                        NPC.velocity = new Vector2(0, -3f) * (1f - PhaseTimer / 60f);
                    }
                    break;
            }
        }

        #endregion

        #region 阶段转换

        private void RunPhaseTransition(Player target) {
            // 二阶段转换演出
            NPC.velocity *= 0.95f;

            if (PhaseTimer < 60) {
                // 收缩所有尾巴
                for (int i = 0; i < TailCount; i++) {
                    Tails[i].StartCoilAttack(1.5f);
                }
            }

            // 能量爆发粒子
            if (Main.netMode != NetmodeID.Server && PhaseTimer % 2 == 0) {
                for (int i = 0; i < 10; i++) {
                    Vector2 dustVel = Main.rand.NextVector2CircularEdge(8, 8);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.GoldFlame, dustVel.X, dustVel.Y, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            if (PhaseTimer == 90) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.3f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(20, 60);
            }

            if (PhaseTimer > 120) {
                TransitionTo(BossPhase.Phase2_Chase);
            }
        }

        #endregion

        #region 二阶段AI

        private void RunPhase2Chase(Player target) {
            // 追击玩家
            Vector2 toTarget = target.Center - NPC.Center;
            Vector2 desiredVelocity = toTarget.SafeNormalize(Vector2.Zero) * 8f;

            // 添加横向晃动
            desiredVelocity.X += MathF.Sin(globalTime * 3f) * 3f;

            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVelocity, 0.08f);

            // 持续尾巴攻击（更激进）
            float attackCooldown = Main.expertMode ? 25f : 35f;

            if (AttackTimer >= lastTailAttackTime + attackCooldown) {
                ExecutePhase2TailAttack(target);
                lastTailAttackTime = AttackTimer;
            }

            // 随机切换到其他二阶段行为
            if (PhaseTimer > 200) {
                int nextAction = Main.rand.Next(4);
                switch (nextAction) {
                    case 0:
                        TransitionTo(BossPhase.Phase2_Dash);
                        break;
                    case 1:
                        TransitionTo(BossPhase.Phase2_Teleport);
                        break;
                    case 2:
                        TransitionTo(BossPhase.Phase2_Illusion);
                        break;
                    case 3:
                        TransitionTo(BossPhase.Phase2_NineStab);
                        break;
                }
            }
        }

        private void ExecutePhase2TailAttack(Player target) {
            // 二阶段更激进的尾巴攻击
            int pattern = Main.rand.Next(4);

            switch (pattern) {
                case 0: // 多尾同时刺击
                    for (int i = 0; i < TailCount; i += 2) {
                        Tails[i].StartStabAttack(target.Center + Main.rand.NextVector2Circular(50, 50), 0.25f);
                    }
                    break;

                case 1: // 扇形横扫
                    for (int i = 0; i < TailCount; i++) {
                        Tails[i].StartSweepAttack(target.Center, MathHelper.PiOver2, 0.4f);
                    }
                    break;

                case 2: // 射弹风暴
                    for (int i = 0; i < TailCount; i++) {
                        Tails[i].StartProjectileAttack(target.Center, 0.5f);
                    }
                    break;

                case 3: // 连续鞭打
                    for (int i = 0; i < TailCount; i++) {
                        Tails[i].StartWhipAttack(target.Center, 0.35f);
                    }
                    break;
            }
        }

        private void RunPhase2Dash(Player target) {
            switch ((int)SubState) {
                case 0: // 准备冲刺
                    dashDirection = (target.Center - NPC.Center).ToRotation();
                    dashCount = 0;
                    maxDashCount = Main.expertMode ? 4 : 3;
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: // 蓄力
                    NPC.velocity *= 0.9f;

                    // 尾巴蓄力姿态
                    if (PhaseTimer == 1) {
                        for (int i = 0; i < TailCount; i++) {
                            Tails[i].StartCoilAttack(0.4f);
                        }
                    }

                    if (PhaseTimer > 30) {
                        // 更新冲刺方向
                        dashDirection = (target.Center - NPC.Center).ToRotation();
                        dashVelocity = dashDirection.ToRotationVector2() * 25f;
                        SubState = 2;
                        PhaseTimer = 0;

                        SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f }, NPC.Center);
                    }
                    break;

                case 2: // 冲刺
                    NPC.velocity = dashVelocity;

                    // 冲刺时尾巴向后甩动
                    for (int i = 0; i < TailCount; i++) {
                        if (!Tails[i].IsAttacking) {
                            Vector2 trailTarget = NPC.Center - dashVelocity.SafeNormalize(Vector2.Zero) * 300f;
                            Tails[i].TargetPosition = trailTarget + Main.rand.NextVector2Circular(50, 50);
                        }
                    }

                    if (PhaseTimer > 35) {
                        dashCount++;
                        if (dashCount >= maxDashCount) {
                            TransitionTo(BossPhase.Phase2_Chase);
                        }
                        else {
                            SubState = 1;
                            PhaseTimer = 0;
                        }
                    }
                    break;
            }
        }

        private void RunPhase2Teleport(Player target) {
            switch ((int)SubState) {
                case 0: // 选择瞬移位置
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float distance = Main.rand.NextFloat(200, 400);
                    teleportTarget = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: // 淡出
                    NPC.Opacity = 1f - PhaseTimer / 20f;
                    NPC.velocity *= 0.9f;

                    if (PhaseTimer >= 20) {
                        NPC.Center = teleportTarget;
                        SubState = 2;
                        PhaseTimer = 0;
                        SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                    }
                    break;

                case 2: // 淡入
                    NPC.Opacity = PhaseTimer / 20f;

                    if (PhaseTimer >= 20) {
                        NPC.Opacity = 1f;
                        SubState = 3;
                        PhaseTimer = 0;

                        // 瞬移后立即攻击
                        for (int i = 0; i < TailCount; i++) {
                            Tails[i].StartStabAttack(target.Center, 0.3f);
                        }
                    }
                    break;

                case 3: // 攻击后短暂停留
                    if (PhaseTimer > 40) {
                        TransitionTo(BossPhase.Phase2_Chase);
                    }
                    break;
            }
        }

        private void RunPhase2Illusion(Player target) {
            switch ((int)SubState) {
                case 0: // 创建幻影
                    illusionCount = Main.expertMode ? 4 : 3;
                    for (int i = 0; i < illusionCount; i++) {
                        float angle = MathHelper.TwoPi * i / illusionCount;
                        illusionPositions[i] = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 300f;
                        illusionAlpha[i] = 0f;
                    }
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: // 幻影淡入，本体移动
                    for (int i = 0; i < illusionCount; i++) {
                        illusionAlpha[i] = MathHelper.Clamp(PhaseTimer / 30f, 0f, 0.6f);
                    }

                    // 本体移动到其中一个幻影位置
                    int realPosition = (int)(PhaseTimer / 30f) % illusionCount;
                    Vector2 targetPos = illusionPositions[realPosition];
                    NPC.Center = Vector2.Lerp(NPC.Center, targetPos, 0.1f);

                    if (PhaseTimer > 60) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2: // 所有位置同时攻击
                    // 触发攻击
                    if (PhaseTimer == 1) {
                        for (int i = 0; i < TailCount; i++) {
                            Tails[i].StartStabAttack(target.Center, 0.3f);
                        }
                    }

                    // 幻影攻击效果（视觉）
                    if (Main.netMode != NetmodeID.Server && PhaseTimer == 10) {
                        for (int i = 0; i < illusionCount; i++) {
                            for (int j = 0; j < 5; j++) {
                                Vector2 dustPos = illusionPositions[i] + Main.rand.NextVector2Circular(50, 50);
                                int dust = Dust.NewDust(dustPos, 0, 0, DustID.GoldFlame, 0, 0, 100, default, 2f);
                                Main.dust[dust].noGravity = true;
                                Main.dust[dust].velocity = (target.Center - dustPos).SafeNormalize(Vector2.Zero) * 10f;
                            }
                        }
                    }

                    if (PhaseTimer > 40) {
                        SubState = 3;
                        PhaseTimer = 0;
                    }
                    break;

                case 3: // 幻影淡出
                    for (int i = 0; i < illusionCount; i++) {
                        illusionAlpha[i] = MathHelper.Clamp(0.6f - PhaseTimer / 30f, 0f, 0.6f);
                    }

                    if (PhaseTimer > 30) {
                        TransitionTo(BossPhase.Phase2_Chase);
                    }
                    break;
            }
        }

        #endregion

        #region 射弹和冲击波

        private void FireTailProjectile(int tailIndex) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            KyuubiTail tail = Tails[tailIndex];
            Vector2 tipPos = tail.GetTipPosition();
            Vector2 direction = tail.GetTipDirection();

            // 发射狐火弹幕（使用现有的火球弹幕类型或创建新的）
            int damage = NPC.damage / 2;
            float speed = 12f;

            // 这里可以使用自定义弹幕类型
            Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                tipPos,
                direction * speed,
                ProjectileID.CultistBossFireBall, // 临时使用，后续可替换
                damage,
                2f,
                Main.myPlayer
            );

            SoundEngine.PlaySound(SoundID.Item20, tipPos);
        }

        private void SpawnSlamShockwave() {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // 发射环形冲击波
            int projectileCount = 12;
            int damage = NPC.damage / 2;
            float speed = 10f;

            for (int i = 0; i < projectileCount; i++) {
                float angle = MathHelper.TwoPi * i / projectileCount;
                Vector2 velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    velocity,
                    ProjectileID.CultistBossFireBallClone, // 临时使用
                    damage,
                    2f,
                    Main.myPlayer
                );
            }
        }

        #endregion

        #region 九方向远距离刺击

        /// <summary>
        /// 一阶段九方向远距离刺击 - 九条尾巴向九个均匀角度方向刺出很远
        /// </summary>
        private void RunPhase1NineStab(Player target) {
            switch ((int)SubState) {
                case 0: // 初始化
                    nineStabRepeatCount = 0;
                    nineStabMaxRepeats = Main.expertMode ? 4 : 3;
                    nineStabBaseAngle = Main.rand.NextFloat(MathHelper.TwoPi); // 随机起始角度
                    SubState = 1;
                    PhaseTimer = 0;

                    // 本体悬停
                    NPC.velocity *= 0.5f;
                    break;

                case 1: // 预判阶段 - 显示预判线
                    NPC.velocity *= 0.95f;

                    // 保持在玩家附近悬浮
                    Vector2 hoverPos = target.Center + new Vector2(0, -300);
                    NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.02f);

                    // 启动所有尾巴的远距离刺击（带预判线）
                    if (PhaseTimer == 1) {
                        for (int i = 0; i < TailCount; i++) {
                            // 九个均匀分布的角度 + 当前轮次的偏移
                            float angle = nineStabBaseAngle + MathHelper.TwoPi * i / TailCount;
                            Vector2 direction = angle.ToRotationVector2();

                            // 启动远距离刺击，预判时间较长
                            Tails[i].StartLongRangeStabAttack(direction, 0.8f, 0.12f, 0.5f);
                        }

                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f }, NPC.Center);
                    }

                    // 预判阶段持续约48帧（0.8秒 * 60fps）
                    if (PhaseTimer > 48) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2: // 刺出阶段 - 极快速刺出
                    // 刺出时屏幕震动
                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(10, 15);
                    }

                    // 刺出阶段持续约8帧（0.12秒 * 60fps）
                    if (PhaseTimer > 8) {
                        SubState = 3;
                        PhaseTimer = 0;
                    }
                    break;

                case 3: // 回收阶段
                    // 回收阶段持续约30帧（0.5秒 * 60fps）
                    if (PhaseTimer > 30) {
                        nineStabRepeatCount++;

                        if (nineStabRepeatCount >= nineStabMaxRepeats) {
                            // 完成所有轮次，返回idle
                            TransitionTo(BossPhase.Phase1_Idle);
                        }
                        else {
                            // 进入下一轮，角度偏移
                            nineStabBaseAngle += MathHelper.ToRadians(20f); // 每轮偏移20度
                            SubState = 1;
                            PhaseTimer = 0;
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 二阶段加强版九方向刺击 - 更快、更多轮次、更小偏移
        /// </summary>
        private void RunPhase2NineStab(Player target) {
            switch ((int)SubState) {
                case 0: // 初始化
                    nineStabRepeatCount = 0;
                    nineStabMaxRepeats = Main.expertMode ? 6 : 5;
                    nineStabBaseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    SubState = 1;
                    PhaseTimer = 0;
                    NPC.velocity *= 0.3f;
                    break;

                case 1: // 预判阶段 - 更短的预判时间
                    NPC.velocity *= 0.92f;

                    // 追踪玩家位置
                    Vector2 hoverPos = target.Center + new Vector2(0, -250);
                    NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.03f);

                    if (PhaseTimer == 1) {
                        for (int i = 0; i < TailCount; i++) {
                            float angle = nineStabBaseAngle + MathHelper.TwoPi * i / TailCount;
                            Vector2 direction = angle.ToRotationVector2();

                            // 二阶段：更短的预判时间，更快的刺出
                            Tails[i].StartLongRangeStabAttack(direction, 0.5f, 0.1f, 0.35f);
                        }

                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.7f }, NPC.Center);
                    }

                    if (PhaseTimer > 30) // 0.5秒
                    {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2: // 刺出阶段
                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0f, Volume = 1.3f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 12);

                        // 二阶段刺出时发射额外弹幕
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < TailCount; i++) {
                                float angle = nineStabBaseAngle + MathHelper.TwoPi * i / TailCount;
                                Vector2 projVel = angle.ToRotationVector2() * 8f;
                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(),
                                    NPC.Center,
                                    projVel,
                                    ProjectileID.CultistBossFireBall,
                                    NPC.damage / 3,
                                    2f,
                                    Main.myPlayer
                                );
                            }
                        }
                    }

                    if (PhaseTimer > 6) // 0.1秒
                    {
                        SubState = 3;
                        PhaseTimer = 0;
                    }
                    break;

                case 3: // 回收阶段
                    if (PhaseTimer > 21) // 0.35秒
                    {
                        nineStabRepeatCount++;

                        if (nineStabRepeatCount >= nineStabMaxRepeats) {
                            TransitionTo(BossPhase.Phase2_Chase);
                        }
                        else {
                            // 每轮偏移更小的角度，形成更密集的攻击
                            nineStabBaseAngle += MathHelper.ToRadians(15f);
                            SubState = 1;
                            PhaseTimer = 0;
                        }
                    }
                    break;
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 绘制拖尾
            DrawTrail(spriteBatch, screenPos);

            // 绘制幻影
            DrawIllusions(spriteBatch, screenPos, drawColor);

            // 绘制尾巴（在本体之前）
            DrawTails(spriteBatch, screenPos, drawColor);

            // 绘制本体
            DrawMainBody(spriteBatch, screenPos, drawColor);

            return false;
        }

        private void DrawTrail(SpriteBatch spriteBatch, Vector2 screenPos) {
            Texture2D texture = TextureAssets.Npc[Type].Value;

            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - (float)i / NPC.oldPos.Length;
                Color trailColor = Color.OrangeRed * progress * 0.3f * NPC.Opacity;
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float scale = NPC.scale * progress * 0.9f;

                spriteBatch.Draw(
                    texture,
                    drawPos,
                    null,
                    trailColor,
                    NPC.rotation,
                    texture.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawIllusions(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Phase != BossPhase.Phase2_Illusion)
                return;

            Texture2D texture = TextureAssets.Npc[Type].Value;

            for (int i = 0; i < illusionCount; i++) {
                if (illusionAlpha[i] <= 0)
                    continue;

                Vector2 drawPos = illusionPositions[i] - screenPos;
                Color illusionColor = drawColor * illusionAlpha[i];
                illusionColor = Color.Lerp(illusionColor, Color.Cyan, 0.3f);

                spriteBatch.Draw(
                    texture,
                    drawPos,
                    null,
                    illusionColor,
                    NPC.rotation,
                    texture.Size() / 2f,
                    NPC.scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawTails(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Tails == null)
                return;

            // 先绘制所有尾巴的预判线（在尾巴之前）
            for (int i = 0; i < TailCount; i++) {
                Tails[i]?.DrawTelegraph(spriteBatch, screenPos);
            }

            // 再绘制尾巴本体
            for (int i = 0; i < TailCount; i++) {
                Tails[i]?.Draw(spriteBatch, screenPos, drawColor);
            }
        }

        private void DrawMainBody(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;

            // 发光效果
            Color glowColor = Color.OrangeRed * 0.3f * NPC.Opacity;
            glowColor.A = 0;

            for (int i = 0; i < 3; i++) {
                Vector2 offset = Main.rand.NextVector2CircularEdge(3, 3);
                spriteBatch.Draw(
                    texture,
                    drawPos + offset,
                    null,
                    glowColor,
                    NPC.rotation,
                    texture.Size() / 2f,
                    NPC.scale * 1.05f,
                    SpriteEffects.None,
                    0f
                );
            }

            // 本体
            Color bodyColor = drawColor * NPC.Opacity;
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                bodyColor,
                NPC.rotation,
                texture.Size() / 2f,
                NPC.scale,
                SpriteEffects.None,
                0f
            );
        }

        #endregion
    }
}
