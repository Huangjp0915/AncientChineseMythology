using AncientChineseMythology.Underworlds.Boss.NetherKitsunes.Items;
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

namespace AncientChineseMythology.Underworlds.Boss.NetherKitsunes
{
    /// <summary>
    /// 幽冥青丘狐Boss - 地府版九尾狐
    /// 幽蓝迷雾色调，具有魂魄吸取、幽冥传送、迷雾笼罩等地府特色攻击
    /// 比普通九尾狐更加飘渺、诡异，尾巴有幽灵化效果
    /// </summary>
    [AutoloadBossHead]
    internal class NetherKitsune : ModNPC
    {
        [VaultLoaden("{@namespace}/")]
        public static Texture2D NetherMissesBody;
        [VaultLoaden("{@namespace}/")]
        public static Texture2D NetherMissesTop;

        #region 常量定义

        /// <summary>尾巴数量</summary>
        public const int TailCount = 9;

        /// <summary>二阶段血量百分比阈值</summary>
        public const float Phase2Threshold = 0.55f;

        /// <summary>三阶段血量百分比阈值（地府特色）</summary>
        public const float Phase3Threshold = 0.25f;

        #endregion

        #region 阶段枚举

        public enum BossPhase
        {
            Intro,                  // 出场演出 - 从迷雾中浮现
            Phase1_Haunting,        // 一阶段：幽魂游荡，尾巴攻击
            Phase1_SoulHarvest,     // 一阶段：魂魄收割
            Phase1_VoidStrike,      // 一阶段：虚空九刺
            PhaseTransition,        // 阶段转换 - 迷雾爆发
            Phase2_PhantomChase,    // 二阶段：幻影追击
            Phase2_NetherDash,      // 二阶段：幽冥冲刺
            Phase2_SpiritRealm,     // 二阶段：灵界召唤
            Phase2_VoidStrike,      // 二阶段：加强虚空九刺
            Phase3Transition,       // 三阶段转换
            Phase3_Possession,      // 三阶段：附身狂暴
            Phase3_FinalJudgment    // 三阶段：终极审判
        }

        public enum TailAttackPattern
        {
            GhostSequential,    // 幽灵顺序刺击
            SoulSweep,          // 魂魄横扫
            PhaseWhip,          // 相位鞭打
            SpiritDrain,        // 灵魂吸取
            VoidPierceNine,     // 虚空九刺
            PhantomSlam,        // 幻影下砸
            NetherCoil          // 幽冥盘绕
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

        /// <summary>九条幽冥尾巴</summary>
        public NetherKitsuneTail[] Tails { get; private set; }

        /// <summary>是否处于二阶段</summary>
        public bool IsPhase2 => NPC.life < NPC.lifeMax * Phase2Threshold;

        /// <summary>是否处于三阶段</summary>
        public bool IsPhase3 => NPC.life < NPC.lifeMax * Phase3Threshold;

        /// <summary>当前攻击模式</summary>
        public TailAttackPattern CurrentTailPattern { get; private set; }

        // 私有状态
        private float globalTime;
        private int seed;
        private Random random;
        private float introProgress;
        private bool didPhase2Transition;
        private bool didPhase3Transition;
        private Vector2 teleportTarget;
        private float dashDirection;

        // 幽冥特效
        private float fogIntensity = 0f;
        private float ghostFlicker = 1f;
        private float soulAuraRadius = 0f;

        // 幻影系统
        private int phantomCount;
        private float[] phantomAlpha;
        private Vector2[] phantomPositions;
        private float[] phantomRotations;

        // 尾巴攻击控制
        private int currentAttackingTail;
        private float lastTailAttackTime;

        // 虚空九刺控制
        private int voidStrikeRepeatCount;
        private int voidStrikeMaxRepeats;
        private float voidStrikeBaseAngle;

        // 灵魂吸取控制
        private bool isSoulDraining;
        private float soulDrainTimer;

        #endregion

        #region ModNPC重写

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 12;
        }

        public override void SetDefaults() {
            NPC.boss = true;
            NPC.width = 130;
            NPC.height = 130;
            NPC.damage = 110;
            NPC.defense = 65;
            NPC.lifeMax = 180000; // 地府Boss强度
            NPC.HitSound = SoundID.NPCHit54; // 幽灵音效
            NPC.DeathSound = SoundID.NPCDeath52;
            NPC.value = Item.buyPrice(0, 25, 0, 0);
            NPC.knockBackResist = 0f;
            NPC.noTileCollide = true;
            NPC.noGravity = true;
            NPC.npcSlots = 15f;
            NPC.aiStyle = -1;

            if (Main.expertMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.35f);
                NPC.damage = (int)(NPC.damage * 1.25f);
            }
            if (Main.masterMode) {
                NPC.lifeMax = (int)(NPC.lifeMax * 1.5f);
                NPC.damage = (int)(NPC.damage * 1.35f);
            }

            Music = MusicLoader.GetMusicSlot("AncientChineseMythology/Sounds/Music/Underworld");
        }

        public override void BossLoot(ref int potionType) {
            potionType = ItemID.SuperHealingPotion;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<NetherKyuubiBook>()));
        }

        public override void OnSpawn(IEntitySource source) {
            seed = Main.rand.Next(10000);
            random = new Random(seed);

            // 初始化九条幽冥尾巴
            Tails = new NetherKitsuneTail[TailCount];
            for (int i = 0; i < TailCount; i++) {
                Tails[i] = new NetherKitsuneTail(i);
                float angleRange = MathHelper.Pi;
                float startAngle = -MathHelper.Pi * 0.75f;
                float baseAngle = startAngle + angleRange * i / (TailCount - 1);
                Tails[i].Initialize(GetTailRootPosition(i), baseAngle);
            }

            // 初始化幻影数组
            phantomAlpha = new float[5];
            phantomPositions = new Vector2[5];
            phantomRotations = new float[5];

            Phase = BossPhase.Intro;
            PhaseTimer = 0;
            fogIntensity = 0f;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.netUpdate = true;
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(seed);
            writer.Write((int)Phase);
            writer.Write(globalTime);
            writer.Write(didPhase2Transition);
            writer.Write(didPhase3Transition);
            writer.Write(currentAttackingTail);
            writer.WriteVector2(teleportTarget);
            writer.Write(dashDirection);
            writer.Write(fogIntensity);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            seed = reader.ReadInt32();
            Phase = (BossPhase)reader.ReadInt32();
            globalTime = reader.ReadSingle();
            didPhase2Transition = reader.ReadBoolean();
            didPhase3Transition = reader.ReadBoolean();
            currentAttackingTail = reader.ReadInt32();
            teleportTarget = reader.ReadVector2();
            dashDirection = reader.ReadSingle();
            fogIntensity = reader.ReadSingle();

            if (random == null)
                random = new Random(seed);
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) {
            NPC.lifeMax = (int)(NPC.lifeMax * 0.75f * balance * bossAdjustment);
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) {
            scale = 1.5f;
            return null;
        }

        public override void OnKill() {
            NetherKitsuneFogSystem.Deactivate();
        }

        #endregion

        #region AI主循环

        public override void AI() {// 激活迷雾系统
            UnderworldPlayer.UnderworldEffect = true;
            if (!NetherKitsuneFogSystem.IsActive) {
                NetherKitsuneFogSystem.Activate(NPC.whoAmI);
            }

            random ??= new Random(seed);
            globalTime += 1f / 60f;

            // 更新幽灵闪烁效果
            ghostFlicker = 0.85f + 0.15f * MathF.Sin(globalTime * 4f);

            if (Tails == null) {
                InitializeTails();
            }

            NPC.TargetClosest();
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead) {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
                if (!target.active || target.dead) {
                    // 幽冥消散
                    NPC.velocity.Y -= 0.3f;
                    NPC.alpha += 3;
                    if (NPC.alpha >= 255) {
                        NPC.active = false;
                        NetherKitsuneFogSystem.Deactivate();
                    }
                    return;
                }
            }

            CheckPhaseTransition();

            PhaseTimer++;
            AttackTimer++;

            // 更新迷雾强度
            UpdateFogIntensity();

            switch (Phase) {
                case BossPhase.Intro:
                    RunIntro(target);
                    break;
                case BossPhase.Phase1_Haunting:
                    RunPhase1Haunting(target);
                    break;
                case BossPhase.Phase1_SoulHarvest:
                    RunPhase1SoulHarvest(target);
                    break;
                case BossPhase.Phase1_VoidStrike:
                    RunPhase1VoidStrike(target);
                    break;
                case BossPhase.PhaseTransition:
                    RunPhaseTransition(target);
                    break;
                case BossPhase.Phase2_PhantomChase:
                    RunPhase2PhantomChase(target);
                    break;
                case BossPhase.Phase2_NetherDash:
                    RunPhase2NetherDash(target);
                    break;
                case BossPhase.Phase2_SpiritRealm:
                    RunPhase2SpiritRealm(target);
                    break;
                case BossPhase.Phase2_VoidStrike:
                    RunPhase2VoidStrike(target);
                    break;
                case BossPhase.Phase3Transition:
                    RunPhase3Transition(target);
                    break;
                case BossPhase.Phase3_Possession:
                    RunPhase3Possession(target);
                    break;
                case BossPhase.Phase3_FinalJudgment:
                    RunPhase3FinalJudgment(target);
                    break;
            }

            UpdateAllTails();

            // 幽蓝色光照
            Lighting.AddLight(NPC.Center, new Vector3(0.3f, 0.5f, 0.8f) * (0.6f + fogIntensity * 0.4f));
        }

        private void InitializeTails() {
            Tails = new NetherKitsuneTail[TailCount];
            for (int i = 0; i < TailCount; i++) {
                Tails[i] = new NetherKitsuneTail(i);
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

            float radius = 40f;
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
                    baseAngle = MathHelper.Lerp(baseAngle, oppositeAngle + spreadOffset, 0.35f);
                }

                // 幽冥尾巴更飘逸的摆动
                float swayOffset = MathF.Sin(globalTime * 2.5f + i * 0.8f) * 0.12f;
                baseAngle += swayOffset;

                Tails[i].Update(rootPos, baseAngle, NPC.velocity, globalTime);

                if (Tails[i].ShouldFireProjectile()) {
                    FireSoulProjectile(i);
                }
            }
        }

        private void UpdateFogIntensity() {
            float targetFog = 0.3f;

            if (IsPhase3)
                targetFog = 0.9f;
            else if (IsPhase2)
                targetFog = 0.6f;
            else if (Phase == BossPhase.Phase1_SoulHarvest)
                targetFog = 0.5f;

            fogIntensity = MathHelper.Lerp(fogIntensity, targetFog, 0.02f);
        }

        private void CheckPhaseTransition() {
            if (!didPhase2Transition && IsPhase2 && Phase != BossPhase.PhaseTransition && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.PhaseTransition);
                didPhase2Transition = true;
            }
            else if (!didPhase3Transition && IsPhase3 && Phase != BossPhase.Phase3Transition &&
                     Phase != BossPhase.PhaseTransition && Phase != BossPhase.Intro) {
                TransitionTo(BossPhase.Phase3Transition);
                didPhase3Transition = true;
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
            introProgress = MathHelper.Clamp(PhaseTimer / 150f, 0f, 1f);

            // 从迷雾中浮现
            Vector2 introOffset = new Vector2(0, -500) * (1f - ACMUtils.SineInOut(introProgress));
            Vector2 desiredPos = target.Center + new Vector2(0, -350) + introOffset;

            NPC.Center = Vector2.Lerp(NPC.Center, desiredPos, 0.04f);
            NPC.velocity *= 0.85f;

            // 幽冥粒子效果
            NPC.alpha = (int)(255 * (1f - introProgress));

            if (Main.netMode != NetmodeID.Server && PhaseTimer % 4 == 0) {
                for (int i = 0; i < 4; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(120, 120) * (1f - introProgress);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 150, default, 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 4f;
                }
            }

            if (PhaseTimer == 100) {
                SoundEngine.PlaySound(SoundID.Zombie105 with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(10, 50);
            }

            if (PhaseTimer > 180) {
                NPC.alpha = 0;
                TransitionTo(BossPhase.Phase1_Haunting);
            }
        }

        private void RunPhase1Haunting(Player target) {
            // 幽魂般的飘浮移动
            Vector2 hoverPos = target.Center + new Vector2(MathF.Sin(globalTime * 1.2f) * 100f, -380 + MathF.Cos(globalTime * 0.8f) * 40f);
            Vector2 toHover = hoverPos - NPC.Center;

            NPC.velocity = Vector2.Lerp(NPC.velocity, toHover * 0.015f, 0.08f);

            // 尾巴攻击
            float attackCooldown = Main.expertMode ? 35f : 45f;

            if (AttackTimer >= lastTailAttackTime + attackCooldown) {
                ExecuteTailAttack(target);
                lastTailAttackTime = AttackTimer;
            }

            if (PhaseTimer % 350 == 0) {
                CurrentTailPattern = (TailAttackPattern)Main.rand.Next(0, 4);
            }

            // 切换到其他攻击
            if (PhaseTimer > 450) {
                if (Main.rand.NextBool(120)) {
                    TransitionTo(BossPhase.Phase1_VoidStrike);
                }
                else if (Main.rand.NextBool(150)) {
                    TransitionTo(BossPhase.Phase1_SoulHarvest);
                }
            }
        }

        private void ExecuteTailAttack(Player target) {
            switch (CurrentTailPattern) {
                case TailAttackPattern.GhostSequential:
                    if (currentAttackingTail < TailCount) {
                        Tails[currentAttackingTail].StartGhostStabAttack(target.Center, 0.3f);
                        currentAttackingTail = (currentAttackingTail + 1) % TailCount;
                    }
                    break;

                case TailAttackPattern.SoulSweep:
                    for (int i = 0; i < TailCount; i += 2) {
                        if (!Tails[i].IsAttacking) {
                            Tails[i].StartSoulSweepAttack(target.Center, MathHelper.PiOver2, 0.5f);
                        }
                    }
                    break;

                case TailAttackPattern.PhaseWhip:
                    int randomTail = Main.rand.Next(TailCount);
                    if (!Tails[randomTail].IsAttacking) {
                        Tails[randomTail].StartPhaseWhipAttack(target.Center, 0.4f);
                    }
                    break;

                case TailAttackPattern.PhantomSlam:
                    for (int i = 0; i < TailCount; i++) {
                        if (!Tails[i].IsAttacking && Main.rand.NextBool(3)) {
                            Vector2 slamTarget = target.Center + Main.rand.NextVector2Circular(100, 50);
                            Tails[i].StartPhantomSlamAttack(slamTarget, 0.55f);
                        }
                    }
                    break;
            }
        }

        private void RunPhase1SoulHarvest(Player target) {
            // 魂魄收割 - 停留并用所有尾巴吸取
            Vector2 hoverPos = target.Center + new Vector2(0, -300);
            NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.03f);
            NPC.velocity *= 0.9f;

            if (PhaseTimer == 1) {
                // 所有尾巴开始吸取
                for (int i = 0; i < TailCount; i++) {
                    float angle = MathHelper.TwoPi * i / TailCount;
                    Vector2 drainTarget = target.Center + angle.ToRotationVector2() * 80f;
                    Tails[i].StartSpiritDrainAttack(drainTarget, 0.8f);
                }
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = 0.3f }, NPC.Center);
            }

            // 吸取粒子
            if (Main.netMode != NetmodeID.Server && PhaseTimer % 3 == 0) {
                Vector2 dustPos = target.Center + Main.rand.NextVector2Circular(100, 100);
                int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 100, default, 1.8f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 8f;
            }

            if (PhaseTimer > 80) {
                TransitionTo(BossPhase.Phase1_Haunting);
            }
        }

        private void RunPhase1VoidStrike(Player target) {
            switch ((int)SubState) {
                case 0: // 初始化
                    voidStrikeRepeatCount = 0;
                    voidStrikeMaxRepeats = Main.expertMode ? 3 : 2;
                    voidStrikeBaseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    SubState = 1;
                    PhaseTimer = 0;
                    NPC.velocity *= 0.4f;
                    break;

                case 1: // 预判阶段
                    NPC.velocity *= 0.92f;
                    Vector2 hoverPos = target.Center + new Vector2(0, -350);
                    NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.025f);

                    if (PhaseTimer == 1) {
                        for (int i = 0; i < TailCount; i++) {
                            float angle = voidStrikeBaseAngle + MathHelper.TwoPi * i / TailCount;
                            Vector2 direction = angle.ToRotationVector2();
                            Tails[i].StartVoidPierceAttack(direction, 0.7f, 0.1f, 0.45f);
                        }
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f }, NPC.Center);
                    }

                    if (PhaseTimer > 42) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2: // 穿刺阶段
                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item125 with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(8, 12);
                        NetherKitsuneFogSystem.CreateRipple(NPC.Center, 1.5f);
                    }

                    if (PhaseTimer > 6) {
                        SubState = 3;
                        PhaseTimer = 0;
                    }
                    break;

                case 3: // 回收阶段
                    if (PhaseTimer > 27) {
                        voidStrikeRepeatCount++;

                        if (voidStrikeRepeatCount >= voidStrikeMaxRepeats) {
                            TransitionTo(BossPhase.Phase1_Haunting);
                        }
                        else {
                            voidStrikeBaseAngle += MathHelper.ToRadians(20f);
                            SubState = 1;
                            PhaseTimer = 0;
                        }
                    }
                    break;
            }
        }

        #endregion

        #region 阶段转换

        private void RunPhaseTransition(Player target) {
            NPC.velocity *= 0.93f;

            if (PhaseTimer < 60) {
                for (int i = 0; i < TailCount; i++) {
                    Tails[i].StartNetherCoilAttack(1.2f);
                }
            }

            // 幽冥能量爆发
            if (Main.netMode != NetmodeID.Server && PhaseTimer % 2 == 0) {
                for (int i = 0; i < 12; i++) {
                    Vector2 dustVel = Main.rand.NextVector2CircularEdge(10, 10);
                    int dust = Dust.NewDust(NPC.Center, 0, 0, DustID.BlueTorch, dustVel.X, dustVel.Y, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                }
            }

            // 幽灵闪烁
            ghostFlicker = 0.3f + 0.7f * MathF.Abs(MathF.Sin(PhaseTimer * 0.15f));

            if (PhaseTimer == 80) {
                SoundEngine.PlaySound(SoundID.Zombie105 with { Pitch = 0.2f, Volume = 1.5f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(18, 50);
                NetherKitsuneFogSystem.CreateRipple(NPC.Center, 2f);
            }

            if (PhaseTimer > 110) {
                ghostFlicker = 1f;
                TransitionTo(BossPhase.Phase2_PhantomChase);
            }
        }

        private void RunPhase3Transition(Player target) {
            NPC.velocity *= 0.9f;

            // 极端的闪烁效果
            ghostFlicker = 0.2f + 0.8f * MathF.Abs(MathF.Sin(PhaseTimer * 0.25f));

            if (Main.netMode != NetmodeID.Server) {
                // 大量魂魄粒子向Boss聚集
                for (int i = 0; i < 8; i++) {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(300, 300);
                    int dust = Dust.NewDust(dustPos, 0, 0, DustID.BlueTorch, 0, 0, 100, default, 2.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = (NPC.Center - dustPos).SafeNormalize(Vector2.Zero) * 12f;
                }
            }

            if (PhaseTimer == 60) {
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.5f, Volume = 1.8f }, NPC.Center);
                Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(25, 80);
            }

            if (PhaseTimer > 100) {
                ghostFlicker = 1f;
                TransitionTo(BossPhase.Phase3_Possession);
            }
        }

        #endregion

        #region 二阶段AI

        private void RunPhase2PhantomChase(Player target) {
            Vector2 toTarget = target.Center - NPC.Center;
            Vector2 desiredVelocity = toTarget.SafeNormalize(Vector2.Zero) * 10f;

            // 幽灵般的飘忽移动
            desiredVelocity.X += MathF.Sin(globalTime * 4f) * 4f;
            desiredVelocity.Y += MathF.Cos(globalTime * 3f) * 2f;

            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVelocity, 0.1f);

            float attackCooldown = Main.expertMode ? 22f : 30f;

            if (AttackTimer >= lastTailAttackTime + attackCooldown) {
                ExecutePhase2TailAttack(target);
                lastTailAttackTime = AttackTimer;
            }

            if (PhaseTimer > 180) {
                int nextAction = Main.rand.Next(4);
                switch (nextAction) {
                    case 0:
                        TransitionTo(BossPhase.Phase2_NetherDash);
                        break;
                    case 1:
                        TransitionTo(BossPhase.Phase2_SpiritRealm);
                        break;
                    case 2:
                        TransitionTo(BossPhase.Phase2_VoidStrike);
                        break;
                    default:
                        PhaseTimer = 0; // 继续追击
                        break;
                }
            }
        }

        private void ExecutePhase2TailAttack(Player target) {
            int pattern = Main.rand.Next(5);

            switch (pattern) {
                case 0:
                    for (int i = 0; i < TailCount; i += 2) {
                        Tails[i].StartGhostStabAttack(target.Center + Main.rand.NextVector2Circular(60, 60), 0.25f);
                    }
                    break;

                case 1:
                    for (int i = 0; i < TailCount; i++) {
                        Tails[i].StartSoulSweepAttack(target.Center, MathHelper.PiOver2 * 0.8f, 0.4f);
                    }
                    break;

                case 2:
                    for (int i = 0; i < TailCount; i++) {
                        Tails[i].StartPhaseWhipAttack(target.Center, 0.35f);
                    }
                    break;

                case 3:
                    for (int i = 0; i < TailCount; i += 3) {
                        Tails[i].StartPhantomSlamAttack(target.Center + Main.rand.NextVector2Circular(80, 80), 0.5f);
                    }
                    break;

                case 4:
                    for (int i = 0; i < TailCount; i++) {
                        Tails[i].StartSpiritDrainAttack(target.Center, 0.6f);
                    }
                    break;
            }
        }

        private void RunPhase2NetherDash(Player target) {
            switch ((int)SubState) {
                case 0:
                    dashDirection = (target.Center - NPC.Center).ToRotation();
                    SubState = 1;
                    PhaseTimer = 0;

                    // 幽冥尾巴盘绕蓄力
                    for (int i = 0; i < TailCount; i++) {
                        Tails[i].StartNetherCoilAttack(0.35f);
                    }
                    break;

                case 1: // 蓄力消隐
                    NPC.velocity *= 0.88f;
                    ghostFlicker = MathHelper.Lerp(1f, 0.2f, PhaseTimer / 25f);

                    if (PhaseTimer > 25) {
                        dashDirection = (target.Center - NPC.Center).ToRotation();
                        NPC.velocity = dashDirection.ToRotationVector2() * 30f;
                        SubState = 2;
                        PhaseTimer = 0;

                        SoundEngine.PlaySound(SoundID.Item130 with { Pitch = 0.3f }, NPC.Center);
                        NetherKitsuneFogSystem.CreateRipple(NPC.Center, 1.2f);
                    }
                    break;

                case 2: // 冲刺
                    ghostFlicker = 0.4f + 0.6f * MathF.Abs(MathF.Sin(PhaseTimer * 0.5f));

                    for (int i = 0; i < TailCount; i++) {
                        if (!Tails[i].IsAttacking) {
                            Vector2 trailTarget = NPC.Center - NPC.velocity.SafeNormalize(Vector2.Zero) * 350f;
                            Tails[i].TargetPosition = trailTarget + Main.rand.NextVector2Circular(60, 60);
                        }
                    }

                    if (PhaseTimer > 30) {
                        ghostFlicker = 1f;
                        TransitionTo(BossPhase.Phase2_PhantomChase);
                    }
                    break;
            }
        }

        private void RunPhase2SpiritRealm(Player target) {
            switch ((int)SubState) {
                case 0: // 创建幻影
                    phantomCount = Main.expertMode ? 4 : 3;
                    for (int i = 0; i < phantomCount; i++) {
                        float angle = MathHelper.TwoPi * i / phantomCount;
                        phantomPositions[i] = target.Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 350f;
                        phantomAlpha[i] = 0f;
                        phantomRotations[i] = angle;
                    }
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: // 幻影淡入
                    for (int i = 0; i < phantomCount; i++) {
                        phantomAlpha[i] = MathHelper.Clamp(PhaseTimer / 40f, 0f, 0.7f);
                        phantomRotations[i] += 0.02f;
                        phantomPositions[i] = target.Center +
                            new Vector2(MathF.Cos(phantomRotations[i]), MathF.Sin(phantomRotations[i])) * 350f;
                    }

                    // 本体移动
                    int realPos = (int)(PhaseTimer / 40f) % phantomCount;
                    NPC.Center = Vector2.Lerp(NPC.Center, phantomPositions[realPos], 0.08f);

                    if (PhaseTimer > 70) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2: // 同时攻击
                    if (PhaseTimer == 1) {
                        for (int i = 0; i < TailCount; i++) {
                            Tails[i].StartGhostStabAttack(target.Center, 0.25f);
                        }

                        // 从幻影位置发射弹幕
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < phantomCount; i++) {
                                Vector2 projVel = (target.Center - phantomPositions[i]).SafeNormalize(Vector2.Zero) * 10f;
                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(),
                                    phantomPositions[i],
                                    projVel,
                                    ProjectileID.CultistBossLightningOrb,
                                    NPC.damage / 3,
                                    2f,
                                    Main.myPlayer
                                );
                            }
                        }
                    }

                    if (PhaseTimer > 50) {
                        SubState = 3;
                        PhaseTimer = 0;
                    }
                    break;

                case 3: // 幻影淡出
                    for (int i = 0; i < phantomCount; i++) {
                        phantomAlpha[i] = MathHelper.Clamp(0.7f - PhaseTimer / 30f, 0f, 0.7f);
                    }

                    if (PhaseTimer > 30) {
                        TransitionTo(BossPhase.Phase2_PhantomChase);
                    }
                    break;
            }
        }

        private void RunPhase2VoidStrike(Player target) {
            // 加强版虚空九刺
            switch ((int)SubState) {
                case 0:
                    voidStrikeRepeatCount = 0;
                    voidStrikeMaxRepeats = Main.expertMode ? 5 : 4;
                    voidStrikeBaseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1:
                    NPC.velocity *= 0.9f;
                    Vector2 hoverPos = target.Center + new Vector2(0, -300);
                    NPC.Center = Vector2.Lerp(NPC.Center, hoverPos, 0.035f);

                    if (PhaseTimer == 1) {
                        for (int i = 0; i < TailCount; i++) {
                            float angle = voidStrikeBaseAngle + MathHelper.TwoPi * i / TailCount;
                            Vector2 direction = angle.ToRotationVector2();
                            Tails[i].StartVoidPierceAttack(direction, 0.45f, 0.08f, 0.3f);
                        }
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.5f }, NPC.Center);
                    }

                    if (PhaseTimer > 27) {
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2:
                    if (PhaseTimer == 1) {
                        SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0f, Volume = 1.4f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(10, 10);
                        NetherKitsuneFogSystem.CreateRipple(NPC.Center, 1.8f);

                        // 发射额外弹幕
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < TailCount; i++) {
                                float angle = voidStrikeBaseAngle + MathHelper.TwoPi * i / TailCount;
                                Vector2 projVel = angle.ToRotationVector2() * 9f;
                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(),
                                    NPC.Center,
                                    projVel,
                                    ProjectileID.CultistBossLightningOrb,
                                    NPC.damage / 3,
                                    2f,
                                    Main.myPlayer
                                );
                            }
                        }
                    }

                    if (PhaseTimer > 5) {
                        SubState = 3;
                        PhaseTimer = 0;
                    }
                    break;

                case 3:
                    if (PhaseTimer > 18) {
                        voidStrikeRepeatCount++;

                        if (voidStrikeRepeatCount >= voidStrikeMaxRepeats) {
                            TransitionTo(BossPhase.Phase2_PhantomChase);
                        }
                        else {
                            voidStrikeBaseAngle += MathHelper.ToRadians(12f);
                            SubState = 1;
                            PhaseTimer = 0;
                        }
                    }
                    break;
            }
        }

        #endregion

        #region 三阶段AI

        private void RunPhase3Possession(Player target) {
            // 附身狂暴 - 极速追击
            Vector2 toTarget = target.Center - NPC.Center;
            Vector2 desiredVelocity = toTarget.SafeNormalize(Vector2.Zero) * 14f;

            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVelocity, 0.15f);

            // 疯狂的幽灵闪烁
            ghostFlicker = 0.5f + 0.5f * MathF.Abs(MathF.Sin(globalTime * 8f));

            // 疯狂攻击
            if (AttackTimer % 15 == 0) {
                int randomTail = Main.rand.Next(TailCount);
                if (!Tails[randomTail].IsAttacking) {
                    int attackType = Main.rand.Next(4);
                    switch (attackType) {
                        case 0:
                            Tails[randomTail].StartGhostStabAttack(target.Center, 0.2f);
                            break;
                        case 1:
                            Tails[randomTail].StartPhaseWhipAttack(target.Center, 0.3f);
                            break;
                        case 2:
                            Tails[randomTail].StartPhantomSlamAttack(target.Center, 0.4f);
                            break;
                        case 3:
                            Tails[randomTail].StartSoulSweepAttack(target.Center, MathHelper.PiOver4, 0.35f);
                            break;
                    }
                }
            }

            // 幽冥粒子
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                int dust = Dust.NewDust(NPC.Center + Main.rand.NextVector2Circular(60, 60), 0, 0, DustID.BlueTorch, 0, 0, 100, default, 2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(6, 6);
            }

            if (PhaseTimer > 300) {
                TransitionTo(BossPhase.Phase3_FinalJudgment);
            }
        }

        private void RunPhase3FinalJudgment(Player target) {
            // 终极审判 - 九方向虚空刺击 + 传送
            switch ((int)SubState) {
                case 0:
                    voidStrikeRepeatCount = 0;
                    voidStrikeMaxRepeats = Main.expertMode ? 7 : 5;
                    voidStrikeBaseAngle = (target.Center - NPC.Center).ToRotation();
                    SubState = 1;
                    PhaseTimer = 0;
                    break;

                case 1: // 传送到玩家附近
                    if (PhaseTimer == 1) {
                        float teleportAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                        teleportTarget = target.Center + teleportAngle.ToRotationVector2() * 250f;
                        ghostFlicker = 0f;
                        SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                    }

                    if (PhaseTimer < 15) {
                        NPC.alpha = (int)(PhaseTimer / 15f * 255);
                    }
                    else if (PhaseTimer == 15) {
                        NPC.Center = teleportTarget;
                    }
                    else if (PhaseTimer < 30) {
                        NPC.alpha = (int)((30 - PhaseTimer) / 15f * 255);
                    }
                    else {
                        NPC.alpha = 0;
                        ghostFlicker = 1f;
                        SubState = 2;
                        PhaseTimer = 0;
                    }
                    break;

                case 2: // 虚空九刺
                    if (PhaseTimer == 1) {
                        voidStrikeBaseAngle = (target.Center - NPC.Center).ToRotation();
                        for (int i = 0; i < TailCount; i++) {
                            float angle = voidStrikeBaseAngle + MathHelper.TwoPi * i / TailCount;
                            Vector2 direction = angle.ToRotationVector2();
                            Tails[i].StartVoidPierceAttack(direction, 0.35f, 0.06f, 0.25f);
                        }
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.7f }, NPC.Center);
                    }

                    if (PhaseTimer == 21) {
                        SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.2f, Volume = 1.5f }, NPC.Center);
                        Main.LocalPlayer.GetModPlayer<ScreenShakePlayer>().ShakeScreen(12, 8);
                        NetherKitsuneFogSystem.CreateRipple(NPC.Center, 2f);
                    }

                    if (PhaseTimer > 40) {
                        voidStrikeRepeatCount++;

                        if (voidStrikeRepeatCount >= voidStrikeMaxRepeats) {
                            TransitionTo(BossPhase.Phase3_Possession);
                        }
                        else {
                            SubState = 1;
                            PhaseTimer = 0;
                        }
                    }
                    break;
            }
        }

        #endregion

        #region 射弹

        private void FireSoulProjectile(int tailIndex) {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            NetherKitsuneTail tail = Tails[tailIndex];
            Vector2 tipPos = tail.GetTipPosition();
            Vector2 direction = tail.GetTipDirection();

            int damage = NPC.damage / 2;
            float speed = 10f;

            Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                tipPos,
                direction * speed,
                ProjectileID.CultistBossLightningOrb,
                damage,
                2f,
                Main.myPlayer
            );

            SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.5f, Volume = 0.8f }, tipPos);
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            DrawTrail(spriteBatch, screenPos);
            DrawPhantoms(spriteBatch, screenPos, drawColor);
            DrawTails(spriteBatch, screenPos, drawColor);
            DrawMainBody(spriteBatch, screenPos, drawColor);

            return false;
        }

        private void DrawTrail(SpriteBatch spriteBatch, Vector2 screenPos) {
            Texture2D texture = TextureAssets.Npc[Type].Value;

            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - (float)i / NPC.oldPos.Length;
                // 幽蓝色拖尾
                Color trailColor = new Color(80, 150, 220) * progress * 0.25f * ghostFlicker;
                trailColor.A = 0;
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                float scale = NPC.scale * progress * 0.85f;

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

        private void DrawPhantoms(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Phase != BossPhase.Phase2_SpiritRealm)
                return;

            Texture2D texture = TextureAssets.Npc[Type].Value;

            for (int i = 0; i < phantomCount; i++) {
                if (phantomAlpha[i] <= 0)
                    continue;

                Vector2 drawPos = phantomPositions[i] - screenPos;
                // 幽蓝色幻影
                Color phantomColor = new Color(100, 180, 255) * phantomAlpha[i];
                phantomColor.A = (byte)(phantomAlpha[i] * 150);

                spriteBatch.Draw(
                    texture,
                    drawPos,
                    null,
                    phantomColor,
                    NPC.rotation,
                    texture.Size() / 2f,
                    NPC.scale * 0.9f,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawTails(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Tails == null)
                return;

            // 幽蓝色调整
            Color tailColor = Color.Lerp(drawColor, new Color(100, 160, 220), 0.4f);
            tailColor *= ghostFlicker;

            // 先绘制预判线
            for (int i = 0; i < TailCount; i++) {
                Tails[i]?.DrawTelegraph(spriteBatch, screenPos);
            }

            // 再绘制尾巴本体
            for (int i = 0; i < TailCount; i++) {
                Tails[i]?.Draw(spriteBatch, screenPos, tailColor);
            }
        }

        private void DrawMainBody(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 drawPos = NPC.Center - screenPos;

            // 幽蓝发光效果
            Color glowColor = new Color(80, 150, 220) * 0.4f * ghostFlicker;
            glowColor.A = 0;

            for (int i = 0; i < 4; i++) {
                Vector2 offset = new Vector2(
                    MathF.Cos(globalTime * 3f + i * MathHelper.PiOver2),
                    MathF.Sin(globalTime * 3f + i * MathHelper.PiOver2)
                ) * 4f;

                spriteBatch.Draw(
                    texture,
                    drawPos + offset,
                    null,
                    glowColor,
                    NPC.rotation,
                    texture.Size() / 2f,
                    NPC.scale * 1.08f,
                    SpriteEffects.None,
                    0f
                );
            }

            // 本体 - 幽蓝色调
            Color bodyColor = Color.Lerp(drawColor, new Color(120, 180, 230), 0.35f);
            bodyColor *= ghostFlicker;
            bodyColor.A = (byte)(255 - NPC.alpha);

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

